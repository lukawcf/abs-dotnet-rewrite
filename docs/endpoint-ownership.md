# Endpoint ownership

State of `main` after the end-to-end work of 2026-10-15. 245 unit tests pass, and `tests/e2e/run_e2e.py` reports 0 unexpected differences (details at the end).

## Who owns what

DEV.md is not in the repository, so the scope and the role letters come from the handbook and from who opened which pull request. The people column is **inferred**; correct it if it is wrong.

| Role | Module | Person (inferred from the pull requests) | Evidence |
|---|---|---|---|
| A | Libraries and platform | Chaofan Wang (lukawcf) | commits `ebdc009`, `6b08ba8` |
| B | Auth and Users | Quaiyyum | PR #1 `feat(person-b): JWT auth + GET /api/me, /api/users, /api/users/:id` |
| C | Library Items | boqiaok (log: `docs/logs/asher.md`, operator Asher Jin) | PR #3 `Feature: refactor library items w3` |
| D | Progress and Bookmarks | Dajiben666 (log: `docs/logs/d-w2.md`, author Xiao Hu) | PR #2 `D: playback progress and bookmarks...` |
| E | Integration tests | ShamiaAkterShanta, **not confirmed**: no commits. `tests/e2e/run_e2e.py` was written by A | Assigned by elimination only |

## How nginx routes (`nginx/nginx.conf`)

A request goes to .NET only when .NET answers it exactly like Node. Everything else goes to Node, so the browser never sees a 501. The rules, all for `GET`:

| Path | To .NET when | Otherwise |
|---|---|---|
| `/api/libraries`, `/api/libraries/<id>`, `/api/users`, `/api/me`, `/api/items/<id>`, `/api/me/progress`, `/api/me/progress/<id>[/<episodeId>]`, `/api/me/bookmarks`, `/api/me/bookmarks/<libraryItemId>` | the query string is empty | Node (for example `?include=filterdata`, `?expanded=1`, `?token=...`) |
| `/api/libraries/<id>/items` | the query string uses only `limit`, `page`, `desc`, `minified`, `include` (any value) and `sort` with a supported value (`addedAt`, `size`, `birthtimeMs`, `mtimeMs`, `media.duration`, `media.metadata.publishedYear/authorName/authorNameLF/title`) | Node (`filter`, `collapseseries`, `include`, other sorts) |
| a request .NET cannot answer exactly: .NET returns **501** and nginx sends the same GET to Node (`error_page 501 = @abs_node`) | never reaches the browser | Node |
| every other method (POST, PATCH, DELETE), `/api/users/<id>`, `/api/me/progress/<id>/remove-from-continue-listening`, `/api/me/progress/batch/update`, bookmark writes, sockets, the web client | never | Node |

## Endpoints

| Method | Path | Owner | .NET controller | Status | Node source | Tables | Notes |
|---|---|---|---|---|---|---|---|
| GET | `/health` | A | `HealthController` | Done | none | none | .NET only, used by compose and tests |
| GET | `/api/libraries` | A | `LibrariesController` | Done, routed | `LibraryController.js:188-212`, route `ApiRouter.js:70` | libraries, libraryFolders, users | Identical to Node in the e2e run. `include=stats` returns 501 (nginx never sends it) |
| GET | `/api/libraries/:id` | A | `LibrariesController` | Done, routed | `LibraryController.js:220-233`, route `ApiRouter.js:71` | libraries, libraryFolders, users | `include=filterdata` returns 501 (nginx never sends it; the web client does use it, `client/store/libraries.js:115`) |
| GET | `/api/libraries/:id/items` | C | `LibraryItemsController` | Partial, routed for supported options (now including `include=rssfeed` and `include=share`) | `LibraryController.js:604-639`, route `ApiRouter.js:75` | libraryItems, books, authors, bookAuthors, series, bookSeries, settings | Book libraries only (`research/library-items-slice.md`) |
| GET | `/api/items/:id` | C | `ItemsController` | Partial, routed without a query | `LibraryItemController.js:70`, route `ApiRouter.js:108` | libraryItems, books, authors, series | Default shape. `expanded=1` and podcast items return 501 |
| GET | `/api/me` | B + D | `MeController` | Done, routed | `MeController.js:27`, route `ApiRouter.js:173` | users, mediaProgresses | Now includes real `mediaProgress`, `bookmarks`, `token`, `isOldToken`, `lastSeen`, `seriesHideFromContinueListening` (A fixed this on 2026-10-15) |
| GET | `/api/users` | B | `UsersController` | Done, routed without a query | `UserController.js:29`, route `ApiRouter.js:134` | users | Minimal form, root token hidden from non-root, creation order. `include=latestSession` returns 501 |
| GET | `/api/users/:id` | B | `UsersController` | **501, stays in Node** | `UserController.js:59`, route `ApiRouter.js:136` | users, mediaProgresses, books, podcastEpisodes | Node adds display titles and covers to each progress row; not implemented |
| GET | `/api/me/progress` | D | `PlaybackProgressController` | Done, routed | `MeController.js` (`getAllMediaProgress`), route `ApiRouter.js:176` | mediaProgresses | Ordered by createdAt like Node's insertion order |
| GET | `/api/me/progress/:id/:episodeId?` | D | `PlaybackProgressController` | Done, routed | `MeController.js:239`, route `ApiRouter.js:183` | mediaProgresses, libraryItems | |
| PATCH | `/api/me/progress/:id/:episodeId?` | D | `PlaybackProgressController` | Written, **disabled**, not routed | `MeController.js:275`, route `ApiRouter.js:185` | mediaProgresses | Flag `ProgressWrites:Enabled` is false |
| PATCH | `/api/me/progress/batch/update` | D | `PlaybackProgressController` | Written, disabled, not routed | `MeController.js:297`, route `ApiRouter.js:184` | mediaProgresses | |
| DELETE | `/api/me/progress/:id` | D | `PlaybackProgressController` | Written, disabled, not routed | `MeController.js:253`, route `ApiRouter.js:186` | mediaProgresses | |
| GET | `/api/me/progress/:id/remove-from-continue-listening` | D | `PlaybackProgressController` | Written, disabled, not routed | `MeController.js:570-584`, route `ApiRouter.js:182` | mediaProgresses | A GET that writes `hideFromContinueListening` and emits `user_updated` |
| GET | `/api/me/bookmarks` | D | `BookmarksController` | Done, routed | `MeController.js:123`, route `ApiRouter.js:177` | users (bookmarks JSON) | |
| GET | `/api/me/bookmarks/:libraryItemId` | D | `BookmarksController` | Done, routed | route `ApiRouter.js:178` | users, libraryItems | |
| POST, PATCH, DELETE | `/api/me/item/:id/bookmark[/:time]` | D | `BookmarkWriteActions` | Written, disabled, not routed | `MeController.js:327, 360, 398`, routes `ApiRouter.js:187-189` | users (bookmarks JSON) | |
| any | end-to-end comparison Node, .NET and nginx | E | `tests/e2e/run_e2e.py` | Done (by A) | n/a | n/a | See below |

## End-to-end result (`python tests/e2e/run_e2e.py --db <copy of an abs database>`)

Run on 2026-10-15 against Node 2.37.1 and the .NET service from `main`, on a copy of the sample database (1 library, 4 items, 1 root user, plus users `reader` and `nolib` created through Node):

| Check | Result |
|---|---|
| .NET against Node, 103 request and user combinations | 102 identical, 10 expected 501 answers, 0 unexpected (112 cases) |
| nginx against Node, 229 combinations (with and without Node's `/audiobookshelf` prefix) including every unsupported option, plus writes sent through nginx and read back | 229 identical |
| Routing probes (40 requests, upstream read from nginx's access log) | 40 correct |

It does **not** cover: write endpoints of .NET (disabled), podcast libraries, real data volumes, the web client in a browser, and `/socket.io`.

## Corrections to the original scope

- Bookmarks are a JSON column on `users` (`research/db-notes.md`, question 3), so bookmark writes are writes to the user row.
- Node also has `POST/PATCH/DELETE /libraries`; they stay in Node (`ApiRouter.js:69,72,73`).
- B's first nginx cutover (commit `a3d8393`) sent the five routes to .NET with every query string and with `/api/me` incomplete. It was replaced by the rules above.

## Unconfirmed

- The identity behind ShamiaAkterShanta and the E role.
- Whether B has a comparison document of their own; none is in `docs/research/`.
- Node's `getAllMediaProgress` source line (only the route line was read).
