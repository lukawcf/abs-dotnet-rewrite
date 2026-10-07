# Audiobookshelf database layer notes (R1)

Source: G:\Project\abs-upstream (server version 2.37.1, package.json:3). Paths are relative to `server/` unless stated. Line numbers refer to the shallow clone. Statements marked **[inferred]** are Sequelize v6 default behaviour (package.json:58 `sequelize ^6.35.2`, :60 `sqlite3 ^5.1.7`) and are not spelled out in the repo. Verify them against a real `absdatabase.sqlite` (`.schema`) before relying on them.

## 1. Connection, pragmas, WAL, busy_timeout

- DB file: `Path.join(global.ConfigPath, 'absdatabase.sqlite')` - Database.js:177. `isNew` is true when the file does not exist (Database.js:180).
- Connection: `new Sequelize({ dialect: 'sqlite', storage: this.dbPath, logging, benchmark, transactionType: 'IMMEDIATE' })` - Database.js:227-233. No `dialectOptions`, `retry` or `pool` options.
- The only pragmas ever set are env-driven: `mmap_size` (SQLITE_MMAP_SIZE), `cache_size` (SQLITE_CACHE_SIZE), `temp_store` (SQLITE_TEMP_STORE) - Database.js:241-260. They run only if the env var is defined.
- **WAL / busy_timeout: not set anywhere.** A grep for `journal_mode` and `busy_timeout` across the upstream repo (excluding node_modules) found no hits in ABS code. So the DB runs in SQLite's default rollback-journal mode **[inferred]**, unless the file was switched to WAL externally. ABS does not change the node-sqlite3 busy timeout.
- `PRAGMA foreign_keys = OFF` / `= ON` are toggled only inside migration v2.17.3 (migrations/v2.17.3-fk-constraints.js:24 and :94). Sequelize's sqlite dialect enables foreign keys on connect **[inferred]**.
- `transactionType: 'IMMEDIATE'` (Database.js:232): Node takes the write lock at BEGIN. A concurrent .NET writer can hit SQLITE_BUSY, and Node has no busy timeout of its own.
- Optional extension: `NUSQLITE3_PATH` loads a native extension providing `unaccent` and unicode folding (Database.js:262-266; `loadExtension` at :271-296). Used only for search (`TextSearchQuery`, `unaccent(...)` at Database.js:971), not by the stored schema.
- Startup order: connect (Database.js:187) -> MigrationManager runs migrations (:192-196) -> `buildModels`, ending in `sequelize.sync({ force, alter: false })` (Database.js:195, :317-344) -> `addTriggers()` (:198) -> `loadData` -> `ANALYZE` (:203).
- Implication: `sync()` with `alter:false` only creates missing tables from the model definitions on a fresh DB. Existing DBs change only through `server/migrations/*.js` (2.15.0 ... 2.35.0). Node owns the schema.

## 2. Tables and columns

Conventions for every model below (all **[inferred]** Sequelize defaults unless a citation is given): `id` is `UUID` and is stored as a 36-char text GUID; `createdAt`/`updatedAt` are `DATETIME NOT NULL`, added automatically (none of these models sets `timestamps:false`); `STRING` is `VARCHAR(255)`; `JSON` is stored as TEXT; `BOOLEAN` is `TINYINT(1)`; `DATE` is `DATETIME` text; `DATE(6)` is `DATETIME(6)`. No attribute in these `init` blocks sets `allowNull:false`, so all non-PK columns are nullable.

### libraries (models/Library.js:155-175, modelName `library`)
| column | Sequelize type | notes |
|---|---|---|
| id | UUID, PK (:158-162) | |
| name | STRING | |
| displayOrder | INTEGER | |
| icon | STRING | |
| mediaType | STRING | 'book' or 'podcast' (:178-183) |
| provider | STRING | |
| lastScan | DATE | |
| lastScanVersion | STRING | |
| settings | **JSON (text)** | falls back to defaults if null (:196-198) |
| extraData | **JSON (text)** | e.g. `lastScanMetadataPrecedence` (:189-191) |
| createdAt, updatedAt | auto | |

No outgoing FKs. Referenced by libraryFolders, libraryItems, series, authors.

### libraryFolders (models/LibraryFolder.js:24-45, modelName `libraryFolder`)
id UUID PK; path STRING; **libraryId UUID FK -> libraries.id**, created by `library.hasMany(LibraryFolder, {onDelete:'CASCADE'})` + `belongsTo(library)` (LibraryFolder.js:37-42); createdAt/updatedAt.

### libraryItems (models/LibraryItem.js:719-776, modelName `libraryItem`)
| column | type | notes |
|---|---|---|
| id | UUID PK (:722-726) | |
| ino | STRING | |
| path, relPath | STRING | |
| mediaId | UUID | polymorphic id of books.id or podcasts.id, **no DB FK** (`constraints:false`, LibraryItem.js:798-805). Column type made UUID by migration v2.17.0:21-24 |
| mediaType | STRING | 'book' / 'podcast', discriminator for mediaId |
| isFile, isMissing, isInvalid | BOOLEAN | |
| mtime, ctime, birthtime | DATE(6) | (:734-736) |
| size | BIGINT | |
| lastScan | DATE | |
| lastScanVersion | STRING | |
| libraryFiles | **JSON (text)** | |
| extraData | **JSON (text)** | |
| title, titleIgnorePrefix | STRING | denormalised copies of books/podcasts values, kept in sync by triggers (section 6); added by migration v2.19.1:28-33 |
| authorNamesFirstLast, authorNamesLastFirst | STRING | denormalised, maintained by triggers; added by migration v2.20.0 (addColumns, :112-116) |
| libraryId | UUID FK -> libraries.id | `library.hasMany(LibraryItem)` / `belongsTo` (:792-793). ON DELETE SET NULL, ON UPDATE CASCADE per migration v2.17.3:32-34 |
| libraryFolderId | UUID FK -> libraryFolders.id | (:795-796). Same SET NULL / CASCADE (v2.17.3:33-34) |
| createdAt, updatedAt | auto | |

Indexes (:736-776): createdAt; mediaId; (libraryId,mediaType); (libraryId,mediaType,size); (libraryId,mediaType,createdAt); NOCASE indexes on (libraryId,mediaType,title / titleIgnorePrefix / authorNamesFirstLast / authorNamesLastFirst); (libraryId,mediaId,mediaType); birthtime; mtime.

### books (models/Book.js:141-200, modelName `book`)
id UUID PK (:144-148); title STRING; titleIgnorePrefix STRING; subtitle STRING; publishedYear **STRING**; publishedDate **STRING**; publisher STRING; description TEXT; isbn STRING; asin STRING; language STRING; explicit BOOLEAN; abridged BOOLEAN; coverPath STRING; duration FLOAT; **JSON (text): narrators, audioFiles, ebookFile, chapters, tags, genres** (:163-168). No FKs: the link to a library is only through libraryItems.mediaId; authors and series connect through bookAuthors / bookSeries. Indexes: title NOCASE, publishedYear, duration.

### users (models/User.js:506-540, modelName `user`)
id UUID PK; username STRING; email STRING; pash STRING (password hash); type STRING ('root' | 'admin' | 'user' | 'guest', :546-555); token STRING (legacy non-expiring token, User.js:617 TODO); isActive BOOLEAN default false; isLocked BOOLEAN default false; lastSeen DATE; **permissions JSON**; **bookmarks JSON**; **extraData JSON** (:521-529). No outgoing FKs. `beforeDestroy` hook blocks deleting type 'root' (:534-539). Table name `users` is confirmed by `tableName: 'users'` in migrations/v2.26.0-create-auth-tables.js:59.

### mediaProgresses (models/MediaProgress.js:58-85, modelName `mediaProgress`)
id UUID PK; mediaItemId UUID (polymorphic -> books.id or podcastEpisodes.id, **no DB FK**, `constraints:false`; column made UUID by v2.17.0:50); mediaItemType STRING ('book' | 'podcastEpisode'); duration FLOAT; currentTime FLOAT; isFinished BOOLEAN; hideFromContinueListening BOOLEAN; ebookLocation STRING; ebookProgress FLOAT; finishedAt DATE; **extraData JSON**; podcastId UUID (nullable, no FK, added by v2.19.4:36); **userId UUID FK -> users.id** via `user.hasMany(MediaProgress, {onDelete:'CASCADE'})` + `belongsTo(user)` (MediaProgress.js:143-146; CASCADE/CASCADE confirmed in migration v2.17.3 "mediaProgresses" block). Indexes: updatedAt; `media_progresses_user_item_finished_time` (userId, mediaItemId, isFinished, currentTime).

### Related tables you will probably also need
- authors (models/Author.js:132-): id, name, lastFirst, asin, description TEXT, imagePath, **libraryId FK -> libraries CASCADE** (Author.js:171-175).
- series (models/Series.js:90-): id, name, nameIgnorePrefix, description TEXT, **libraryId FK CASCADE** (Series.js:133-136); unique (name, libraryId) `unique_series_name_per_library`. Migration v2.15.0:184 writes the table as `'Series'` (capital S); check live casing.
- bookAuthors (BookAuthor.js:71-): id, createdAt only (`updatedAt:false`), bookId, authorId. bookSeries (BookSeries.js:33-): id, sequence STRING, createdAt only, bookId, seriesId.
- podcasts (Podcast.js:128-167): id, title, titleIgnorePrefix, author, releaseDate STRING, feedURL, imageURL, description TEXT, itunesPageURL, itunesId, itunesArtistId, language, podcastType, explicit BOOLEAN, autoDownloadEpisodes BOOLEAN, autoDownloadSchedule STRING, lastEpisodeCheck DATE, maxEpisodesToKeep INT, maxNewEpisodesToDownload INT, coverPath, **tags JSON, genres JSON**, numEpisodes INTEGER (NOT NULL default 0, migration v2.19.4:30).

## 3. Bookmarks

Confirmed: bookmarks are a **JSON column `bookmarks` on the `users` table**, not a separate table (User.js:108 typedef, :528 `bookmarks: DataTypes.JSON`). New users get `bookmarks: []` (User.js:205, :328). Element shape (created at User.js:864-869): `{ libraryItemId, time, title, createdAt: Date.now() }`, so `createdAt` is an epoch-**millisecond** number and `time` is a number of seconds. Lookup is by `(libraryItemId, time)` with loose equality `bm.time == time` (:842). Create/update/delete mutate the array in place, call `changed('bookmarks', true)`, then `save()` (:858-909). A TODO says they should use mediaItemId instead of libraryItemId (User.js:835). API output copies each element as-is (:623).

## 4. User permissions: storage and library visibility

- Stored in `users.permissions` (JSON text) plus scalar columns `type`, `isActive`, `isLocked`. Default permission object (User.js:168-183): `download`, `update`, `delete`, `upload`, `createEreader`, `accessAllLibraries` (default true), `accessAllTags` (default true), `accessExplicitContent`, `selectedTagsNotAccessible`, `librariesAccessible: []` (library id strings), `itemTagsSelected: []` (tag strings).
- Read through model getters (User.js:557-570): `canAccessExplicitContent`, `canDelete`, `canUpdate`, `canDownload`, `canUpload` all require the `permissions.<flag>` to be truthy **and** `isActive`. Admin-or-up is `type` in ('root','admin') (:548-550).
- **"Can this user see library X"** = `checkCanAccessLibrary(libraryId)` (User.js:646-650): true if `permissions.accessAllLibraries`; else false if `permissions.librariesAccessible` is missing; else `librariesAccessible.includes(libraryId)`. Item level adds an explicit-content check and a tag check (`checkCanAccessLibraryItemWithTags`, :658-667; `checkCanAccessLibraryItem`, :675-683). Tag rule: `accessAllTags` -> allowed; `selectedTagsNotAccessible` makes `itemTagsSelected` a denylist (untagged items allowed); otherwise an allowlist (untagged items denied).
- **Inconsistency to be aware of:** `GET /libraries` (controllers/LibraryController.js:188-195) filters only on `librariesAccessible.length` and does not look at `accessAllLibraries`. Other endpoints use `checkCanAccessLibrary` (e.g. AuthorController.js:430, LibraryController.js:1476, MiscController.js:65). `toOldJSONForBrowser` strips `librariesAccessible` / `itemTagsSelected` out of `permissions` and exposes them top-level (User.js:604-630).
- Other per-user JSON state: `extraData.seriesHideFromContinueListening` (string[]; User.js:604, :921-942), `extraData.authOpenIDSub` (:574, :298), `extraData.oldUserId` (:31, :419).

## 5. Exact SQLite names

Table name = Sequelize-pluralised `modelName`; no `tableName` / `freezeTableName` option appears in server/models (grep: no matches) **[inferred]**. Confirmed by repo text: `libraryItems`, `bookAuthors`, `books`, `podcasts`, `authors` (Database.js:855-858, :885-887); `mediaProgresses` (migrations/v2.17.0-uuid-replacement.js:50); `bookSeries` (v2.15.2:35); `podcastEpisodes` (v2.15.2:50); `users` (v2.26.0:59); `Series` (v2.15.0:184).

| JS model | SQLite table | status |
|---|---|---|
| library | `libraries` | inferred |
| libraryFolder | `libraryFolders` | inferred |
| libraryItem | `libraryItems` | confirmed |
| book | `books` | confirmed |
| podcast | `podcasts` | confirmed |
| user | `users` | confirmed |
| mediaProgress | `mediaProgresses` | confirmed |
| author / series | `authors` / `series` or `Series` | authors confirmed; series casing conflicts, check live DB |

Column names equal the JS attribute names (camelCase, case-preserved), e.g. `libraryId`, `libraryFolderId`, `mediaItemId`, `titleIgnorePrefix`, `createdAt`. FK columns not declared in `init` (libraryId, libraryFolderId, userId, bookId, authorId, seriesId) come from the association calls with Sequelize's default `<model>Id` naming **[inferred]**. Trigger names are snake_case via `convertToSnakeCase` (Database.js:865, :899, :923, :954-956). One explicit index name: `media_progresses_user_item_finished_time`.

## 6. Triggers, views, custom functions

- **Triggers** (created by Node at startup, Database.js:854-860, plus earlier in migrations v2.19.1:141, v2.19.4:156, v2.20.0:166 and :190):
  - `AFTER UPDATE OF title ON books` -> `UPDATE libraryItems SET title = NEW.title WHERE libraryItems.mediaId = NEW.id`. The same pattern exists for `books.titleIgnorePrefix`, `podcasts.title`, `podcasts.titleIgnorePrefix` (Database.js:855-858, template :872-881).
  - `AFTER INSERT ON bookAuthors` and `AFTER DELETE ON bookAuthors` recompute `libraryItems.authorNamesFirstLast` / `authorNamesLastFirst` as `GROUP_CONCAT(authors.name, ', ' ORDER BY bookAuthors.createdAt ASC)` (Database.js:897-920). `AFTER UPDATE OF name ON authors` does the same recompute (:922-945).
- **Views:** none found (no `CREATE VIEW` in Database.js or migrations).
- **Custom SQLite functions:** none registered by ABS. Only the optional loadable extension for `unaccent` (Database.js:262-266, :971), which is search-only. Indexes use built-in `COLLATE NOCASE` (LibraryItem.js:766-775; Book.js title index).
- JSON path operators used in queries: `extraData->>"authOpenIDSub"` (User.js:441) and `extraData.oldUserId` (User.js:419).

## Risks for EF Core mapping

1. **JSON as text.** Columns: libraries.settings, libraries.extraData; libraryItems.libraryFiles, extraData; books.narrators, audioFiles, ebookFile, chapters, tags, genres; podcasts.tags, genres; users.permissions, bookmarks, extraData; mediaProgresses.extraData. Map as `string` or a value converter; do not rely on EF `ToJson()` owned-entity mapping. Node tolerates NULL JSON everywhere (`?.`, `|| []`: User.js:604-606, Library.js:189, :196), so NULL is a legal state. A .NET write must not drop properties Node expects (use JsonNode/JsonElement or extension data) and must replace the whole value (Node does `changed('x', true)` for the same reason).
2. **Bookmark numbers.** `createdAt` is epoch ms inside JSON, `time` is seconds, compared loosely (`==`). Use `long` and `double` and compare numerically.
3. **Nullable everything.** No column in these models is `allowNull:false`, so any non-PK, non-timestamp column can be NULL in a live DB. Only known NOT NULL exception: `podcasts.numEpisodes` (v2.19.4:30). libraryItems.libraryId / libraryFolderId can become NULL through ON DELETE SET NULL. Treat NULL booleans as false (Node uses `!!`).
4. **Dates.** `DATE` on SQLite via Sequelize is stored as text, expected form `YYYY-MM-DD HH:MM:SS.SSS +00:00` (UTC) **[inferred; verify with a SELECT on real data]**. mtime/ctime/birthtime (`DATE(6)`) are assigned from epoch-ms numbers (LibraryItem.js:695) and also go through Sequelize's date serialisation. Microsoft.Data.Sqlite's default text format has no ` +00:00` suffix, so use an explicit value converter or reads/ordering may break. Node clients receive epoch ms (`.valueOf()`, Library.js:208-212), so convert at the API edge. `publishedYear`, `publishedDate`, `releaseDate` are plain strings.
5. **Booleans** are 0/1 integers (TINYINT(1)); EF's default bool mapping works, but NULLs are possible.
6. **UUIDs** are text GUIDs (uuid v4 / Sequelize UUIDV4, lowercase hyphenated) **[inferred]**. SQLite text comparison is case-sensitive, and Microsoft.Data.Sqlite writes Guid as uppercase text by default, so use a converter that emits lowercase (or map ids as `string`). Polymorphic ids (libraryItems.mediaId, mediaProgresses.mediaItemId) have no FK and need their discriminator column; do not configure EF relationships or cascades for them.
7. **FKs and cascades are Node's.** libraryFolders, authors, series -> libraries CASCADE; libraryItems -> libraries / libraryFolders SET NULL; mediaProgresses -> users CASCADE. Never call `EnsureCreated` or `Migrate`. Use explicit `ToTable` / `HasColumnName` with `ValueGeneratedNever` for ids, and read-only mapping if possible.
8. **Triggers fire on .NET writes.** Any write to books/podcasts title fields, bookAuthors, or authors.name updates libraryItems. The author-name triggers use `GROUP_CONCAT(... ORDER BY ...)`, which needs SQLite >= 3.44; if the bundled SQLite in the .NET provider is older, such writes can fail at trigger execution **[inferred from SQLite docs, not from repo]**. Do not write the denormalised libraryItems title/author columns from .NET independently.
9. **Collation and index use.** Indexes are NOCASE (LibraryItem.js:766-775); EF queries ordering/filtering by title need `COLLATE NOCASE` to use them.
10. **Locking.** No WAL, no busy_timeout, Node transactions are IMMEDIATE (Database.js:232). In rollback-journal mode a .NET reader can get SQLITE_BUSY while Node writes (and vice versa). Set a busy timeout on the .NET connection (`Default Timeout` or `PRAGMA busy_timeout`), prefer `Mode=ReadOnly`, and do not switch `journal_mode` to WAL from .NET without a decision, since it changes the shared file for Node.
11. **Naming.** EF default PascalCase / singular names will not match; `Series` vs `series` casing needs checking; password hash column is `pash`; `users.token` is the legacy token.
12. **Permission semantics.** Reproduce `accessAllLibraries` vs `librariesAccessible` precedence, the `isActive` gating of `canX` flags, and be aware `/libraries` filters differently (LibraryController.js:191-194).
13. **Schema drift.** Upstream is 2.37.1 and migrations keep adding columns (e.g. sessions.lastRefreshToken in v2.35.0). Map only the needed columns, tolerate extras, and check the live DB's migration state before assuming it matches this clone.
