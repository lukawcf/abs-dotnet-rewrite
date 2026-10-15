# Known limitations

Each entry says what differs from Node, who notices, how serious it is, and what to do. Severity is a proposal. The group decides.

## 1. Pages do not refresh after a progress or bookmark change (`user_updated`)

- **Difference:** Node sends the socket event `user_updated` after progress and bookmark changes (`server/controllers/MeController.js:264, 286, 315, 350, 388, 422`). If .NET handles those requests, nothing sends it.
- **Who sees it:** The same tab, other tabs and other devices. The client replaces the whole user object only on that event (`client/layouts/default.vue:270-274`, `client/store/user.js:173-175`) and PATCH answers with an empty 200 (`MeController.js:286-287`). Progress bars, the finished toggle and the bookmark list stay old until a reload. Real playback sync stays in Node and is not affected.
- **Severity:** Medium (`research/user-updated-impact.md`). Data is correct; the screen looks as if the action failed.
- **Options:** (a) .NET calls an internal Node endpoint that emits the event, (b) .NET emits the socket event itself with the full user JSON, (c) keep these routes in Node. Option (c) is what is true today: D's writes are disabled and not routed.
- **Status:** Open. Also listed as outstanding in `research/d-integration-handover.md`.

## 2. Node's response cache goes stale if .NET writes

- **Difference:** Every `GET /api/libraries*` is cached in Node's process, keyed by user and URL (`server/routers/ApiRouter.js:68`, `server/managers/ApiCacheManager.js:89-111`). Only Node's own Sequelize write hooks clear it (`ApiCacheManager.js:17-19, 63`). Only `/personalized` URLs expire, after 30 minutes (`ApiCacheManager.js:7, 103-105`).
- **Who sees it:** Nobody while .NET is read-only. Once any .NET write is enabled, Node can keep serving old library data.
- **Severity:** High for writes, none for reads.
- **Workaround:** Keep writes in Node, or add an invalidation call before enabling writes.
- **Status:** Open. The authenticated cache behaviour was reasoned from code and **not tested live** (no credentials were available in `research/wal-and-cache.md`).

## 3. "database is locked" under concurrent use

- **Difference:** The deployed database uses the rollback journal, not WAL (file header bytes 18-19 are 1,1). Node writes with `BEGIN IMMEDIATE` and sets no busy timeout (`server/Database.js:227-233`).
- **Measured:** In a 60 second test on copies, a reader without a busy timeout failed about 28 to 30% of reads in rollback mode. With 5000 ms there were no errors. WAL gave about 3.3 times more writes (`research/wal-and-cache.md`). One run per setting, run in parallel, so numbers are indicative.
- **Who sees it:** Users of any .NET endpoint while Node is writing, for example during a scan.
- **Severity:** Medium if the busy timeout is missing, low with it.
- **Workaround:** The connection string in `src/Abs.Api/appsettings.json` has `Mode=ReadOnly;Default Timeout=5`. .NET must never change the journal mode. D's handover says lock conflicts on writes can still return 500.
- **Status:** Reads mitigated. Write-side lock handling open.

## 4. `GET /api/libraries/:id/items` supports a small part of Node's options

- **Difference:** Supported: book libraries, `limit`, `page`, `sort`, `desc`, `minified`, permission and explicit-content rules. Answered with 501: `filter`, `collapseseries`, `include`, progress sorts, `sort=random`, podcast libraries (`research/library-items-slice.md`, `research/library-endpoints.md` section 4).
- **Who sees it:** Anyone using filters, series grouping or progress sorting. Today nobody, because nothing is routed.
- **Severity:** Medium. The web client uses these options heavily.
- **Workaround:** When the route is cut over, nginx must send requests with an unsupported option to Node. A bare 501 would break the page.
- **Status:** Open by design (scope decision).

## 5. `GET /api/items/:id` default shape only

- `expanded=1` and podcast items return 501 (`research/library-items-slice.md`). Node source: `LibraryItemController.js:70`. Same routing workaround as 4.

## 6. Includes that need Node-only state

- `GET /api/libraries?include=stats` and `GET /api/libraries/:id?include=filterdata` return 501. The second uses Node's in-memory filter data with a 30 minute TTL (`research/library-endpoints.md` section 3). `include=share` and `filter=share-open` depend on Node's `ShareManager` state.
- **Severity:** Low to medium. **Workaround:** leave these requests in Node.

## 7. Tie order and no-sort order differ

- When two items have equal sort values, Node's order is undefined; .NET breaks ties by id. Without `sort`, Node has no ORDER BY and .NET uses `createdAt` then id (`research/library-items-slice.md`, "Known differences").
- **Severity:** Low. **Who sees it:** Lists with duplicate sort values may list in a different order.

## 8. `/api/me` is incomplete

- The user payload from .NET has placeholder `mediaProgress` and `bookmarks` (`research/d-integration-handover.md`). The browser reads those fields to draw progress.
- **Severity:** High, because `/api/me` **is** routed to .NET already (entry 11). **Workaround:** revert that nginx block until B and D fill the fields.

## 9. Permission rules differ between list and single library

- `GET /api/libraries` filters by `librariesAccessible` only when it is not empty and ignores `accessAllLibraries` (`LibraryController.js:191-194`). `GET /api/libraries/:id` honours the flag (`User.js:646-650`). .NET copies both and tests both. It is a Node quirk, listed so nobody "fixes" it.

## 11. nginx already sends five routes to .NET, query strings included

- **Difference:** `nginx/nginx.conf` (commit `a3d8393`) sends `/api/libraries`, `/api/libraries/<id>`, `/api/me`, `/api/users` and `/api/users/<id>` to .NET. The `location` blocks match the path only, so `?include=stats` and `?include=filterdata` also reach .NET, where they return **501** (entries 6). `/api/me` returns placeholder `mediaProgress` and `bookmarks` (entry 8).
- **Who sees it:** Anyone using the web UI through `http://localhost:8080`. The web client reads the user object from `/api/me` to draw progress and bookmarks. The client does request `/api/libraries/<id>?include=filterdata` (`client/store/libraries.js:115` in the upstream repo), so the library filter data call hits the 501. It was **not** checked whether the UI still renders without it. `include=stats` was not found in the client code searched.
- **Checked on 2026-10-15** with the real `nginx.conf` in a throwaway nginx container on the compose network (without a token): `/api/libraries`, `/api/libraries/<id>`, `/api/libraries/<id>?include=filterdata`, `/api/me` and `/api/users` were answered by .NET (empty 401 body); `/api/libraries/<id>/items`, `/api/me/progress` and `/api/items/<id>` by Node (text body `Unauthorized`). With a valid token the `include=filterdata` request gets 501 from .NET (unit-tested, not tried through nginx).
- **Severity:** High until checked. This cutover happened without E's comparison test.
- **Workaround:** Revert the `/api/me` block, or make nginx send any request that has an `include` query argument to Node. Not applied; it is B's change and needs a group decision.
- **Status:** Open. Found on 2026-10-15 by diffing `nginx/nginx.conf` against `60c88f2`; no one had listed it.

## 10. Verification gaps

- Comparisons ran on **synthetic or very small data** (1 library, 4 items; 5 generated books in C's run).
- No deployed end-to-end test of nginx, Node and .NET together (D states this itself).
- No integration test suite by E exists yet.
- `dotnet format --verify-no-changes` reports line-ending errors on a Windows checkout with `core.autocrlf=true`. The repository itself stores LF. Not a code problem, but anyone on Windows sees it.

## Unconfirmed

- The SQLite version bundled with Microsoft.Data.Sqlite 8.x supports `GROUP_CONCAT ... ORDER BY`, which Node's author-name triggers use (`research/db-notes.md`). Only matters if .NET ever writes `books`, `authors` or `bookAuthors`.
- Node's sqlite driver default busy timeout (recalled as 1000 ms, not confirmed).
- Behaviour with a database that holds years of real data.
