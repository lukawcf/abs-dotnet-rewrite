# Library Items slice

Routes: `GET /api/items/:id` and `GET /api/libraries/:id/items`. Node sources are in `server/controllers/LibraryItemController.js` (`findOne` 60-100, `middleware` 1220-1250) and `server/controllers/LibraryController.js` (`getLibraryItems` 604-639, `middleware` 1475-1498). The upstream version is the one deployed with this project, 2.37.1.

## What is implemented

| Route | Supported | Answered with 501 so Node can serve it |
|---|---|---|
| `GET /api/items/:id` | Book items, default shape (`LibraryItem.toOldJSON`, `LibraryItem.js:965-990`). 404, 403 by library, explicit content and tag rules | `expanded=1`, podcast items |
| `GET /api/libraries/:id/items` | Book libraries. `limit`, `page`, `sort`, `desc`, `minified` echo. Envelope, item and media shapes as in `toOldJSONMinified` | `filter`, `collapseseries=1`, `include`, unsupported `sort` values (progress sorts, `random`, anything unknown), podcast libraries, users restricted by tags |

`include` on `GET /api/items/:id` is ignored when `expanded` is not 1, as in Node (`findOne` only reads it in the expanded branch).

Supported sorts: `addedAt`, `size`, `birthtimeMs`, `mtimeMs`, `media.duration`, `media.metadata.publishedYear`, `media.metadata.authorName`, `media.metadata.authorNameLF`, `media.metadata.title`. `sequence` behaves as the title sort, as Node does without a series filter (`libraryItemsBookFilters.js:404-406`). The title sort follows the server setting `sortingIgnorePrefix`, read from the `settings` row `server-settings`.

## Rules copied from Node

- Library list: 403 without library access, 404 `Library not found`, 400 `Invalid request. <limit|page> must be a positive integer`. Non-numeric `limit` or `page` becomes 0 (`LibraryController.js:1486-1495`).
- Item access (`User.js:658-682`): library access, then explicit content (`accessExplicitContent` and an active user), then tags. A user without `accessAllTags` and without selected tags cannot open a tagged item. List queries only filter by tags when tags are selected (`libraryItemsBookFilters.js:30-45`), so the two rules differ. The tests keep this quirk (`user-admin` gets 403 on a tagged item).
- List queries keep only books with `explicit = 0` for users who may not see explicit content. Items without a book row are left out, as in Node's inner join.
- `size`: media size is the sum of audio and ebook file sizes, replaced by the item size when that sum is 0 (`LibraryItem.js:318-320`).
- `titleIgnorePrefix` in the JSON is Node's `getTitlePrefixAtEnd` using `sortingPrefixes` from the server settings.

## Tests and fixtures

`tests/tools/record-node-items.js` builds an in-memory database with audiobookshelf's own Sequelize models, inserts five books and one podcast item, and writes three files: the DDL (appended to `schema.sql`), the rows (appended to `seed.sql`) and `node-items.json`, the output of Node's `toOldJSON` and `getByFilterAndSort` for 5 items and 17 list queries. Run it with `ABS_UPSTREAM=<audiobookshelf checkout> NODE_PATH=<checkout>/node_modules node tests/tools/record-node-items.js <out dir>`. Sequelize overwrites `updatedAt` with the current time on every create, so rerunning the recorder changes the dates. Regenerate all three files together.

## Comparison against a running Node (end to end)

Run on 2026-10-14 with the real audiobookshelf 2.37.1 image and a token it issued (signed with `JWT_SECRET_KEY`), against the same SQLite file. Node and .NET were started from `docker-compose.yml` plus an override that published ports 13378 (Node) and 18081 (.NET) and pointed the volumes at a scratch directory. Nginx and `nginx.conf` were not used or changed. The library held 5 generated audiobooks with distinct size, duration, author, title and year, so no sort key ties.

`tests/tools/compare-node-dotnet.py` sent 67 request and user combinations to both services: every item, an unknown item, all 9 sorts plus `sequence` in both directions for a root and a restricted user, paging cases (`limit`, `page`, `limit=0`, `limit=abc`), bad paging, an unknown library and a user without library access. Status and body were identical in all 67.

The first run found one real bug that the unit tests could not see. Node answers `res.sendStatus(403)` and `res.sendStatus(404)` with the plain text `Forbidden` and `Not Found`, while the controllers returned ASP.NET problem-details JSON. `PlainStatus` fixes this and the tests now assert the body. `LibrariesController` (not part of this slice) still has the old behaviour for its 403.

Ties are the exception to "identical": when two items have equal sort keys, Node's order is undefined and this slice breaks the tie by id. An earlier run with tied data showed exactly that difference.

## Known differences from Node

- `authorNameLF` comes from `libraryItems.authorNamesLastFirst`. Node computes it from the author names with `parseNameString.nameToLastFirst`. They match as long as Node keeps the column in sync.
- Without `sort`, Node has no ORDER BY. This slice uses `createdAt`, then id, so paging is stable. Equal sort keys are also broken by id.
- `publishedYear` sort runs in memory over id and year pairs. Memory use is two short strings per item.
- Not verified: the exact tie order Node returns for equal sort keys, podcast libraries, and databases that hold years of real data (the end to end run used 5 generated books).
