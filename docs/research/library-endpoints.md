# Library endpoints research (R2)

Source: `G:\Project\abs-upstream\server` (read-only, shallow clone). Paths below are relative to that `server` dir. Line numbers refer to the clone.

## 0. Routing and middleware chain

- API is mounted at `/api` behind auth: `Server.js:354` (`router.use('/api', this.auth.ifAuthNeeded(this.authMiddleware...), this.apiRouter.router)`); `authMiddleware` delegates to passport `isAuthenticated` (`Server.js:158-161`). This sets `req.user`.
- `routers/ApiRouter.js:68`: `this.router.get(/^\/libraries/, this.apiCacheManager.middleware)` - a response cache in front of ALL GET `/libraries*` routes (see section 3).
- `routers/ApiRouter.js:70`: `GET /libraries` -> `LibraryController.findAll` (no per-library middleware).
- `routers/ApiRouter.js:71`: `GET /libraries/:id` -> `LibraryController.middleware`, `findOne`.
- `routers/ApiRouter.js:75`: `GET /libraries/:id/items` -> `LibraryController.middleware`, `getLibraryItems`.
- `LibraryController.middleware` (`controllers/LibraryController.js:1475-1498`):
  - 403 (empty body) if `!req.user.checkCanAccessLibrary(req.params.id)` (1476-1479). `checkCanAccessLibrary` = true if `permissions.accessAllLibraries`; false if no `librariesAccessible`; else `librariesAccessible.includes(id)` (`models/User.js:646-650`).
  - 404 `"Library not found"` if `Database.libraryModel.findByIdWithFolders` returns null (1481-1484); loaded with `libraryFolders` included (`models/Library.js:104-108`). Sets `req.library`.
  - For `limit` and `page` if present: non-numeric becomes 0; non-integer or negative -> 400 `Invalid request. <key> must be a positive integer` (1487-1495).

## 1. GET /api/libraries and GET /api/libraries/:id

### GET /api/libraries - `findAll` (`LibraryController.js:188-212`)

- Query: `include` (comma separated). Only `stats` is honored (198-207). Doc comment 183.
- Permission: no explicit 403. Loads all libraries with folders ordered by `displayOrder ASC` (189; `models/Library.js:92-97`). Then, if `req.user.permissions.librariesAccessible` is a NON-EMPTY array, filters to those ids (191-194). Quirk: it does NOT consult `accessAllLibraries`; an empty list means "all" here (unlike `checkCanAccessLibrary`, `models/User.js:646`).
- Response: `{ libraries: [ <library> ... ] }` (209-211).
- With `include=stats`: each library gets `stats` (book: `getBookLibraryStats`, podcast: `getPodcastLibraryStats`, 201-205). Book stats shape (`utils/queries/libraryItemsBookFilters.js:1312-1325`): `{ totalSize:number, totalDuration:number, numAudioFiles:number, totalItems:number }` (raw SQL `SUM`/`COUNT` over `libraryItems li, books b`). Podcast stats: see `utils/queries/libraryItemsPodcastFilters.js` (not read in depth).

### GET /api/libraries/:id - `findOne` (`LibraryController.js:220-233`)

- Permission: middleware above (403/404).
- Query: `include=filterdata` (221-222).
- Default response: `req.library.toOldJSON()` (232), bare object (not wrapped).
- With `include=filterdata`: `{ filterdata, issues: filterdata.numIssues, numUserPlaylists, library: <library> }` (225-230). `numUserPlaylists` from `Database.playlistModel.getNumPlaylistsForUserAndLibrary(user.id, library.id)` (228). `filterdata` comes from the in-memory cached `libraryFilters.getFilterData` (see section 3). `filterdata` fields (`utils/queries/libraryFilters.js:461-474`): authors, genres, tags, series, narrators, languages, publishers, publishedDecades, bookCount, authorCount, seriesCount, podcastCount, and `numIssues` (474), `bookCount/seriesCount/authorCount/podcastCount`, `loadedAt` (used at 522-528, 626-628, 695).

### Library JSON shape (`toOldJSON`, `models/Library.js:202-219`)

| field | type | source |
|---|---|---|
| id | string (UUID) | `this.id` |
| name | string | |
| folders | array of `{ id, fullPath, libraryId, addedAt }` | `models/LibraryFolder.js:49-56`; `addedAt` = createdAt epoch ms (number) |
| displayOrder | number | |
| icon | string | |
| mediaType | string (`book` or `podcast`) | |
| provider | string | |
| settings | object (spread of `this.settings` JSON column) | keys per `LibrarySettingsObject` `models/Library.js:5-17` and defaults 56-81: coverAspectRatio, disableWatcher, autoScanCronExpression, skipMatchingMediaWithAsin/Isbn, audiobooksOnly, epubsAllowScriptedContent, hideSingleBookSeries, onlyShowLaterBooksInContinueSeries, metadataPrecedence, markAsFinishedPercentComplete, markAsFinishedTimeRemaining (podcast: podcastSearchRegion) |
| lastScan | number (epoch ms) or null | `lastScan?.valueOf() \|\| null` |
| lastScanVersion | string or null | |
| createdAt | number (epoch ms) | |
| lastUpdate | number (epoch ms) | mapped from `updatedAt` (renamed) |

Note: dates are epoch milliseconds, `updatedAt` is emitted as `lastUpdate`, `settings` is passed through as stored JSON (so .NET should map it as free-form JSON, not a fixed class).

## 2. GET /api/libraries/:id/items - `getLibraryItems` (`LibraryController.js:604-639`)

### Response envelope (610-622, 638)

```
{ results: [libraryItem...], total: number|undefined, limit: number, page: number,
  sortBy: string|undefined, sortDesc: boolean, filterBy: string|undefined,
  mediaType: string, minified: boolean, collapseseries: boolean,
  include: string, offset: number }
```
`limit` and `page` default to 0 (613-614, `req.query` already coerced to Number by middleware, so they are numbers if sent); `offset = page * limit` (624). `limit` 0/falsy means no limit (`findOptions.limit = limit || null`, `utils/queries/libraryItemsBookFilters.js:372,381`). `total` is the count (635); for the subseries path it is not set (stays undefined, so omitted in JSON).

### Query parameters

| param | handling | cite | weight |
|---|---|---|---|
| `limit` | integer >= 0; 0 = all | middleware 1488-1495; 613 | light |
| `page` | integer >= 0; offset = page*limit | 614, 624 | light |
| `sort` | echoed as `sortBy`; mapped by `getOrder` (below) | 615; `libraryItemsBookFilters.js:254-300` | light to medium |
| `desc` | `'1'` means descending | 616 | light |
| `filter` | `<group>.<URL-encoded base64 value>` or bare group; decoded by `libraryFilters.decode` | 617, 627-628; `utils/queries/libraryFilters.js:13,29-39` | HEAVY (see below) |
| `collapseseries` | `'1'` enables series collapsing (book libraries) | 620 | HEAVY |
| `include` | comma list, lowercased: `rssfeed`, `share` (admin only), `numepisodesincomplete` (podcast) | 605-609; `libraryItemsBookFilters.js:405-406`; podcast `libraryItemsPodcastFilters.js:147-148` | medium |
| `minified` | `'1'` only echoed into payload; has NO effect, items are always minified (`getByFilterAndSort` uses `toOldJSONMinified`, `models/LibraryItem.js:301`) | 619 | none |

`sort` values for books (`getOrder`, `libraryItemsBookFilters.js:254-300`): `addedAt`, `size`, `birthtimeMs`, `mtimeMs`, `media.duration`, `media.metadata.publishedYear` (CAST to int), `media.metadata.authorNameLF`, `media.metadata.authorName` (secondary title sort), `media.metadata.title` (uses `titleIgnorePrefix` if server setting `sortingIgnorePrefix`, `COLLATE NOCASE`; `display_title` when collapsing), `sequence` (only valid with series filter; otherwise replaced by title, 404-406), `progress`, `progress.createdAt`, `progress.finishedAt` (these join per-user `mediaProgresses`, 507-521), `random`. Unknown value -> no ORDER BY (`return []`, 299). Title order depends on `global.ServerSettings.sortingIgnorePrefix` (a server setting).

`filter` groups (`libraryFilters.js:35`): genres, tags, series, authors, progress, narrators, publishers, publishedDecades, missing, languages, tracks, ebooks; plus special bare values handled in `libraryItemsBookFilters.js`: `feed-open` (433), `share-open` (443), `issues` (497-505), `recent` (last 60 days, 524-527), `abridged`, `explicit`, `ebooks.supplementary`/`no-supplementary` (448-458), and `progress.*` values (not-finished, not-started, finished, in-progress, audio-in-progress, ebook-in-progress, ebook-finished; `getMediaGroupQuery` `libraryItemsBookFilters.js:103-250`). Genres/tags/narrators use `json_each` SQLite JSON subqueries (`:176-181`). Filter values are decoded by `libraryFilters.decode` (`libraryFilters.js:13-16`): `Buffer.from(decodeURIComponent(text), "base64").toString()`, i.e. URL-encoded base64.

### Which parts need heavy logic

- Per-user progress joins: `filter=progress.*` and `sort=progress*` join `mediaProgresses` where `userId = req.user.id` (`libraryItemsBookFilters.js:488-521`).
- Permission filtering applied on every call (`getUserPermissionBookWhereQuery`, `:17-47`): hides `explicit` books if `!user.canAccessExplicitContent` (`models/User.js:557`), and applies tag allow/deny lists using `json_each(tags)`.
- Series collapsing: `collapseseries=1` runs an extra query to find books to exclude and per-series `numBooks`/`libraryItemIds` (`getCollapseSeriesBooksToExclude` `:312-352`, usage `:529-570`, result attached as `libraryItem.collapsedSeries` `:620-632`, copied in `models/LibraryItem.js:302-304`). Special case: `filter=series.<id>` + `collapseseries=1` takes a separate path, `libraryHelpers.handleCollapseSubseries` (`LibraryController.js:626-631`, `utils/libraryHelpers.js:105`, with `VALID_COLLAPSE_SUBSERIES_SORTS` line 9; marked TODO as temporary). Inside the book filter, collapse is force-disabled for a series filter (`:400-403`).
- Series filter adds `series` info to each item: `libraryItem.series = {id,name,sequence}` surfaced as `media.metadata.series` (`libraryItemsBookFilters.js:631-638`; `models/LibraryItem.js:305-307`).
- Optional joins: RSS feed (`include=rssfeed`, `models/LibraryItem.js:308-310`), media item share (`include=share`, admin only, uses in-memory `ShareManager.findByMediaItemId`, `libraryItemsBookFilters.js:406,663-665`).
- Podcast libraries use a separate query module (`utils/queries/libraryItemsPodcastFilters.js:146`, dispatch `libraryFilters.js:41-45`) with a different item shape.

### Item JSON shape (book library; `toOldJSONMinified`)

Library item (`models/LibraryItem.js:1000-1025`): `id, ino, oldLibraryItemId, libraryId, folderId, path, relPath, isFile, mtimeMs, ctimeMs, birthtimeMs, addedAt, updatedAt` (epoch ms numbers), `isMissing, isInvalid` (bool), `mediaType, media, numFiles, size`. Extras possibly added: `collapsedSeries`, `rssFeed`, `mediaItemShare`, `numEpisodesIncomplete` (`:301-322`).

`media` for books (`models/Book.js:640-659`): `id, metadata, coverPath, tags[], numTracks, numAudioFiles, numChapters, duration, size, ebookFormat`. `metadata` (`Book.js:572-591`): `title, titleIgnorePrefix, subtitle, authorName, authorNameLF, narratorName, seriesName, genres[], publishedYear, publishedDate, publisher, description, isbn, asin, language, explicit, abridged`. Note: `titleIgnorePrefix` is computed (`getTitlePrefixAtEnd`), `narratorName` is a join of the narrators array, `numTracks` is computed from `includedAudioFiles`; `tags/genres` are JSON columns.

Podcast `media` (`models/Podcast.js:459-474`): `id, metadata, coverPath, tags, numEpisodes, autoDownloadEpisodes, autoDownloadSchedule, lastEpisodeCheck, maxEpisodesToKeep, maxNewEpisodesToDownload, size`.

## 3. Caching and in-memory state that .NET cannot replicate or could make stale

1. HTTP response cache `ApiCacheManager` (`managers/ApiCacheManager.js`), applied to every GET `/libraries*` including `/items`, `/libraries`, `/libraries/:id` (`ApiRouter.js:68`).
   - LRU, max 1000 entries / 10 MB (`ApiCacheManager.js:6`); key = `{ user: username, url: req.url }` (88); skipped when `sort=random` (83-86).
   - Cleared entirely on ANY Sequelize create/update/destroy/bulk/upsert hook on any model (`init` line 17-19, `clear` 55-64 -> `cache.clear()` at 63), except high-churn models `session, mediaProgress, playbackSession, device` (line 8) which only clear `/personalized`, `/recent-episodes` and `/me` slices (24-53 region). TTL 30 min applies only to `/personalized` (105); everything else has no TTL, only invalidation by Sequelize hooks.
   - Consequence for .NET: Node's cache is invalidated by Node-process writes only. If .NET serves reads from the same SQLite DB, .NET always reads fresh data (not stale) provided it has no cache of its own. But the reverse risk: if the .NET side (or anything not going through Node's Sequelize) writes to the DB, Node's cache will serve stale `/libraries*` responses indefinitely. Keep .NET read-only on these tables (matches the "Node owns schema" rule). Also note: because `mediaProgress` writes do NOT clear the cache, a cached `/items?filter=progress.*` or `sort=progress` response in Node can be stale until some other model changes - .NET reading the DB directly would return fresher data than Node, so responses can legitimately differ between the two.
2. Library filter data `Database.libraryFilterData` (`Database.js:23`): per-library in-process object, 30 min TTL (`utils/queries/libraryFilters.js:449-459`), validated against book/series/author counts (`:626-628`) and surgically patched by mutation helpers when tags/genres/narrators/series/authors change (`Database.js:476-534+`, plus `ApiRouter.js:452,514`). Only used by `/filterdata` and `GET /libraries/:id?include=filterdata` (not by `/items`). .NET cannot share this; it would have to recompute (cheap enough with queries) - leave to Node.
3. Count cache `countCache` (module-level `Map`) in `utils/queries/libraryItemsBookFilters.js:9`, used when there is no filter and no permission where-clause (`:360-378, 581`); cleared by `clearCountCache(hook)` (355-358). Affects only `total`; Node may serve a stale total until a hook fires, .NET would compute exact. No action needed other than being aware.
4. `global.ServerSettings.sortingIgnorePrefix` (`libraryItemsBookFilters.js:260-266`) is a Node in-memory/server-settings value; .NET needs to read it from the `settings` table (not verified where in this research) to match title sorting.
5. `ShareManager.findByMediaItemId` (`libraryItemsBookFilters.js:663-665`) is in-memory state: `include=share` and `filter=share-open` cannot be replicated.
6. Item JSON is computed in Node code (`titleIgnorePrefix`, `narratorName`, `numTracks`, `authorName` etc., see section 2); these must be re-implemented or read from stored columns (`authorNamesFirstLast`, `authorNamesLastFirst`, `titleIgnorePrefix` columns referenced in `getOrder`, `:259-282`).

## 4. Recommendation: minimal scope for GET /api/libraries/:id/items (about one week, one person)

Support (book libraries; read-only, no cache):
- Route + auth/permission parity: 403 if user cannot access library, 404 if missing, 400 for bad `limit`/`page` (`LibraryController.js:1475-1498`).
- Envelope fields exactly as in section 2, including `total`, `limit`, `page`, `offset`, `sortBy`, `sortDesc`, `filterBy`, `mediaType`, `minified`, `collapseseries`, `include`.
- Query params: `limit`, `page`, `sort`, `desc`.
- Sorts: `media.metadata.title`, `addedAt`, `media.metadata.authorName`, `media.metadata.authorNameLF`, `media.duration`, `size`, `media.metadata.publishedYear`. Fix the title sort to respect `sortingIgnorePrefix` or document that as a limitation.
- Item JSON: library item minified + book minified + metadata (fields listed above), computed from DB columns. Authors/series come from `bookAuthors`/`bookSeries` joins (separate queries, as in `libraryItemsBookFilters.js:130-170` region).
- Permission filtering for explicit content and tags (`getUserPermissionBookWhereQuery`, 17-47) - required for correctness and security; small.
- Optionally one cheap filter: `filter=authors.<id>` or `genres.<value>`; otherwise ignore.

Leave to Node (do not implement; route them to Node via nginx/YARP when the query contains these):
- `collapseseries=1` and the `series.<id>` + collapse subseries path.
- `filter=progress.*`, `sort=progress*` (per-user progress joins), `sort=random` and `sequence`.
- `filter` groups: series, missing, tracks, ebooks, publishedDecades, issues, recent, feed-open, share-open, abridged, explicit.
- `include=rssfeed|share|numepisodesincomplete`, podcast libraries (different shape and query module).
- `/filterdata`, `/personalized`, `/series`, `/search`, `/stats`, etc.

Routing rule suggestion: the .NET handler returns the supported subset; if any unsupported param is present the gateway forwards to Node (or .NET proxies to Node), so behaviour never silently degrades.

Also easy to include in the same week: GET `/api/libraries` (without `include=stats`, or with book stats via one SQL query, `libraryItemsBookFilters.js:1312-1325`) and GET `/api/libraries/:id` (without `include=filterdata`).

Open items / not verified: podcast stats and podcast filter details, where `sortingIgnorePrefix` is persisted, and `Server.js` `ifAuthNeeded` token handling.
