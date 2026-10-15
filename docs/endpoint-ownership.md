# Endpoint ownership

State of `main` at commit `0fc4c71` (PR #1, #2, #3 merged). 235 tests pass.

**nginx cutover has already happened for five routes.** Commit `a3d8393` (B, PR #1) changed `nginx/nginx.conf` so that `=/api/libraries`, `/api/libraries/<id>`, `=/api/me`, `=/api/users` and `/api/users/<id>` go to the .NET service. Everything else, including `/api/libraries/<id>/items`, `/api/items/...` and all `/api/me/progress` and bookmark routes, still goes to Node. This was done without E's comparison test (the handbook's rule) and the blocks match on the path only, so every query string, such as `?include=stats`, reaches .NET as well. See known limitations 11.

## Who owns what

DEV.md is not in the repository, so the scope and the role letters are taken from the handbook and from who opened which pull request. Treat the people column as **inferred** and correct it if it is wrong.

| Role | Module | Person (inferred from the pull requests) | Evidence |
|---|---|---|---|
| A | Libraries and platform | Chaofan Wang (lukawcf) | commits `ebdc009`, `6b08ba8` |
| B | Auth and Users | Quaiyyum | PR #1 `feat(person-b): JWT auth + GET /api/me, /api/users, /api/users/:id` |
| C | Library Items | boqiaok (log: `docs/logs/asher.md`, operator Asher Jin) | PR #3 `Feature: refactor library items w3` |
| D | Progress and Bookmarks | Dajiben666 (log: `docs/logs/d-w2.md`, author Xiao Hu) | PR #2 `D: playback progress and bookmarks...` |
| E | Integration tests | ShamiaAkterShanta | Not confirmed: no commits or pull requests yet. Assigned by elimination only |

## Endpoints

"Status" is what exists in `src/Abs.Api/Controllers` on `main`. **Only five routes are cut over in nginx** (marked "routed to .NET" in the Status column, from `nginx/nginx.conf` lines added in `a3d8393`). Every other route below is reachable only inside the compose network.

| Method | Path | Owner | .NET controller | Status | Node source | Tables | Notes |
|---|---|---|---|---|---|---|---|
| GET | `/health` | A | `HealthController` | Done | none | none | .NET only, used by tests and compose |
| GET | `/api/libraries` | A | `LibrariesController` | Done, **routed to .NET** | `LibraryController.js:188-212`, route `ApiRouter.js:70` | libraries, libraryFolders, users | Body matched Node byte for byte on a database copy (`research/k2-shape-comparison.md`). `include=stats` returns 501 |
| GET | `/api/libraries/:id` | A | `LibrariesController` | Done, **routed to .NET** | `LibraryController.js:220-233`, route `ApiRouter.js:71` | libraries, libraryFolders, users | `include=filterdata` returns 501 |
| GET | `/api/libraries/:id/items` | C | `LibraryItemsController` | Partial | `LibraryController.js:604-639`, route `ApiRouter.js:75` | libraryItems, books, authors, bookAuthors, series, bookSeries, settings | Book libraries only. `filter`, `collapseseries`, `include`, unsupported `sort` return 501 (`research/library-items-slice.md`) |
| GET | `/api/items/:id` | C | `ItemsController` | Partial | `LibraryItemController.js:70`, route `ApiRouter.js:108` | libraryItems, books, authors, series | Default shape, book items. `expanded=1` and podcast items return 501 |
| GET | `/api/me` | B | `MeController` | Partial, **routed to .NET** | `MeController.js:27`, route `ApiRouter.js:173` | users | `mediaProgress` and `bookmarks` in the user payload are placeholders (`research/d-integration-handover.md`) |
| GET | `/api/users` | B | `UsersController` | Done (see note), **routed to .NET** | `UserController.js:29`, route `ApiRouter.js:134` | users | No test evidence read by A; B's PR text only |
| GET | `/api/users/:id` | B | `UsersController` | Done (see note), **routed to .NET** | `UserController.js:59`, route `ApiRouter.js:136` | users | same |
| GET | `/api/me/progress` | D | `PlaybackProgressController` | Done, default read-only | `MeController.js`, route `ApiRouter.js:176` | mediaProgresses, users | |
| GET | `/api/me/progress/:id/:episodeId?` | D | `PlaybackProgressController` | Done | `MeController.js:239`, route `ApiRouter.js:183` | mediaProgresses, libraryItems, books, podcastEpisodes | |
| PATCH | `/api/me/progress/:id/:episodeId?` | D | `PlaybackProgressController` | Written, **disabled by default** | `MeController.js:275`, route `ApiRouter.js:185` | mediaProgresses | Write flag `ProgressWrites:Enabled`. See known limitations |
| PATCH | `/api/me/progress/batch/update` | D | `PlaybackProgressController` | Written, disabled by default | `MeController.js:297`, route `ApiRouter.js:184` | mediaProgresses | |
| DELETE | `/api/me/progress/:id` | D | `PlaybackProgressController` | Written, disabled by default | `MeController.js:253`, route `ApiRouter.js:186` | mediaProgresses | Only the user's own rows |
| GET | `/api/me/progress/:id/remove-from-continue-listening` | D | `PlaybackProgressController` | Written, disabled by default | `MeController.js:570`, route `ApiRouter.js:182` | mediaProgresses | A GET that writes: sets `hideFromContinueListening` on the progress row and emits `user_updated` (`MeController.js:570-584`) |
| GET | `/api/me/bookmarks` | D | `BookmarksController` | Done | `MeController.js:123`, route `ApiRouter.js:177` | users (bookmarks JSON) | |
| GET | `/api/me/bookmarks/:libraryItemId` | D | `BookmarksController` | Done | route `ApiRouter.js:178` | users, libraryItems | |
| POST | `/api/me/item/:id/bookmark` | D | `BookmarkWriteActions` | Written, disabled by default | `MeController.js:327`, route `ApiRouter.js:187` | users (bookmarks JSON) | |
| PATCH | `/api/me/item/:id/bookmark` | D | `BookmarkWriteActions` | Written, disabled by default | `MeController.js:360`, route `ApiRouter.js:188` | users | |
| DELETE | `/api/me/item/:id/bookmark/:time` | D | `BookmarkWriteActions` | Written, disabled by default | `MeController.js:398`, route `ApiRouter.js:189` | users | |
| any | integration test suite against Node and .NET | E | none yet | **Not started** | n/a | n/a | Required before any nginx cutover (handbook, P5) |

Note on `/api/users*`: the controllers exist and the tests pass, but I did not review B's code or tests. "Done" here only reports what B's pull request states.

## Corrections to the original scope

- The handbook assumed bookmarks would be a separate table. They are a JSON column on `users` (`research/db-notes.md`, question 3), so bookmark writes are writes to the user row, and they trigger the same concerns as any user update.
- `GET /api/libraries` and `GET /api/libraries/:id` are the only Libraries endpoints. Node also has `POST/PATCH/DELETE /libraries` (`ApiRouter.js:69,72,73`); they stay in Node.
- The `/api/me/progress/:libraryItemId` PATCH route is named `:id` in D's controller but `:libraryItemId` in Node (`ApiRouter.js:185`). Same path shape, no behaviour difference.

## Unconfirmed

- The identity behind ShamiaAkterShanta and the E role.
- Whether B reviewed or tested `/api/users` against Node. No B comparison document is in `docs/research/`.
- Node source line for `GET /api/me/progress` (`getAllMediaProgress`): only the route line was read, not the method.
