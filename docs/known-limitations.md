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
- **Tested live on 2026-10-15** (Node 2.37.1 with a token signed by a test key, on a database copy): after the `libraries.name` column was changed by a process other than Node, `GET /api/libraries` through Node still returned the old name while .NET returned the new one. After a `PATCH /api/libraries/<id>` made through Node, both returned the new name. So the cache is real, only Node's own writes clear it, and a .NET write would behave like the outside write.
- **Status:** Open for writes; harmless while .NET only reads.

## 3. "database is locked" under concurrent use

- **Difference:** The deployed database uses the rollback journal, not WAL (file header bytes 18-19 are 1,1). Node writes with `BEGIN IMMEDIATE` and sets no busy timeout (`server/Database.js:227-233`).
- **Measured:** In a 60 second test on copies, a reader without a busy timeout failed about 28 to 30% of reads in rollback mode. With 5000 ms there were no errors. WAL gave about 3.3 times more writes (`research/wal-and-cache.md`). One run per setting, run in parallel, so numbers are indicative.
- **Who sees it:** Users of any .NET endpoint while Node is writing, for example during a scan.
- **Severity:** Medium if the busy timeout is missing, low with it.
- **Workaround:** The connection string in `src/Abs.Api/appsettings.json` has `Mode=ReadOnly;Default Timeout=5`. .NET must never change the journal mode. D's handover says lock conflicts on writes can still return 500.
- **Status:** Reads mitigated. Write-side lock handling open.

## 4. `GET /api/libraries/:id/items` supports a small part of Node's options

- **Difference:** Supported: book libraries, `limit`, `page`, `sort`, `desc`, `minified`, permission and explicit-content rules. Answered with 501: `filter`, `collapseseries`, `include`, progress sorts, `sort=random`, podcast libraries (`research/library-items-slice.md`, `research/library-endpoints.md` section 4).
- **Who sees it:** Nobody. nginx sends a request to .NET only when its query string uses supported options, so everything else is answered by Node (`endpoint-ownership.md`, routing table). This was checked by `tests/e2e/run_e2e.py` (nginx against Node, 108 of 108 identical).
- **Severity:** Low now (was medium before the nginx rules). The web client's usual calls carry `include` or `filter` and therefore still hit Node, so the .NET path is used less than the numbers suggest.
- **Status:** By design.

## 5. `GET /api/items/:id` default shape only

- `expanded=1` and podcast items return 501 (`research/library-items-slice.md`). Node source: `LibraryItemController.js:70`. nginx sends `/api/items/<id>` to .NET only without a query string (a podcast item without a query would still reach .NET and get 501; not tested, podcast libraries are out of scope).

## 6. Includes that need Node-only state

- `GET /api/libraries?include=stats` and `GET /api/libraries/:id?include=filterdata` return 501. The second uses Node's in-memory filter data with a 30 minute TTL (`research/library-endpoints.md` section 3). `include=share` and `filter=share-open` depend on Node's `ShareManager` state.
- **Severity:** Low. nginx never sends these requests to .NET (a query string always goes to Node).

## 7. Tie order and no-sort order differ

- When two items have equal sort values, Node's order is undefined; .NET breaks ties by id. Without `sort`, Node has no ORDER BY and .NET uses `createdAt` then id (`research/library-items-slice.md`, "Known differences").
- **Severity:** Low. **Who sees it:** Lists with duplicate sort values may list in a different order.

## 8. `/api/me` (fixed on 2026-10-15)

- Was: placeholder `mediaProgress`, `bookmarks`, empty `token`, wrong `isOldToken`, no `lastSeen`. Now: filled from the same data as Node. Compared with Node in the e2e run for root, an ordinary user and a user without library access: identical (token without expiry and token with expiry are both covered by unit tests, `UserEndpointTests`).
- **Still open:** `hasOpenIDLink` is always false (the column `authOpenIDSub` is not in the 2.37.1 schema that was read); `GET /api/users/:id` stays in Node because Node adds display titles and covers to each progress row.

## 9. Permission rules differ between list and single library

- `GET /api/libraries` filters by `librariesAccessible` only when it is not empty and ignores `accessAllLibraries` (`LibraryController.js:191-194`). `GET /api/libraries/:id` honours the flag (`User.js:646-650`). .NET copies both and tests both. It is a Node quirk, listed so nobody "fixes" it.

## 11. nginx cutover of 2026-10-08 (replaced on 2026-10-15)

- **What happened:** Commit `a3d8393` (B) sent five routes to .NET with every query string, while `/api/me` was incomplete and `?include=filterdata` (used by the web client, `client/store/libraries.js:115`) returned 501. It was found by diffing `nginx/nginx.conf` against `60c88f2`.
- **Fix:** `nginx/nginx.conf` now decides per request (empty query string, or only the supported options) and sends the rest to Node. `tests/e2e/run_e2e.py` checks the result: nginx answers all 108 compared requests exactly like Node, and 26 routing probes go to the expected backend.
- **Second fix, same day:** the web client calls `/audiobookshelf/api/...` (Node's base path), which the first version of the new rules did not match, so the browser still reached only Node. The rules now accept the prefix and strip it before proxying; the e2e suite tests both path forms (211 of 211 identical through nginx, 37 of 37 routing probes).
- **Not covered:** the web client in a real browser after the second fix; the e2e run uses a small sample database.

## 12. JWT secrets shorter than 32 characters (fixed on 2026-10-15)

- **What happened:** The deployed `JWT_SECRET_KEY` has 30 characters (240 bits). Node signs and verifies with any length. The .NET JWT library refuses HS256 keys under 256 bits, so every real browser token got `401` from .NET (log message `IDX10517 ... Signature validation failed`). It only showed once nginx really sent browser traffic to .NET; the unit tests and the e2e suite used 64 character keys.
- **Fix:** `JwtAuthenticationHandler.SigningKeyBytes` pads the key with zero bytes to 32 bytes. HMAC pads short keys with zeros anyway, so the signature is identical to Node's. Unit tests cover key lengths 1, 20, 30, 32 and 64; the e2e suite now uses a 30 character key.
- **Checked on the running stack:** tokens signed inside the Node container with the real key (old style and new style) are accepted by both Node and .NET, with the same fields in `/api/me`.
- **Side effect while it was broken:** browser requests that nginx sent to .NET (for example `GET /api/libraries`) failed with 401 once, the client refreshed its token and the page kept working because the other calls went to Node.

## 10. Verification gaps

- Comparisons ran on **synthetic or very small data** (1 library, 4 items; 5 generated books in C's run).
- The end-to-end suite (`tests/e2e/run_e2e.py`) runs Node, .NET and nginx together, but on a copy of a very small database and through scripted requests, not a browser.
- The suite uses port 18080 so it can run next to the normal stack; the normal stack runs on 8080 (see `open-items.md` item 17 if Windows blocks that port).
- `dotnet format --verify-no-changes` reported line-ending errors on Windows checkouts with `core.autocrlf=true`; `.gitattributes` (added 2026-10-15) fixes this for fresh clones.

## Unconfirmed

- The SQLite version bundled with Microsoft.Data.Sqlite 8.x supports `GROUP_CONCAT ... ORDER BY`, which Node's author-name triggers use (`research/db-notes.md`). Only matters if .NET ever writes `books`, `authors` or `bookAuthors`.
- Node's sqlite driver default busy timeout (recalled as 1000 ms, not confirmed).
- Behaviour with a database that holds years of real data.
