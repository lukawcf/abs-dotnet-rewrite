# Entity notes for B, C and D

Read this before you map a table. Sources: `research/db-notes.md` (R1, from Node code), `research/scaffold-assessment.md` (S1, from a real 2.37.1 database copy). Where they disagree, S1 wins because it looked at the real file.

## Rules that apply to every table

1. **Ids are strings.** The database stores lowercase hyphenated GUID text (S1). Do not use `Guid`: Microsoft.Data.Sqlite writes uppercase and EF scaffolding mixes `Guid` and `string` depending on row counts.
2. **Never scaffold.** `dotnet ef dbcontext scaffold` failed on the six core tables: a column declared UUID or DATETIME becomes `string` when the table has rows and `Guid`/`DateTime` when it is empty, so foreign keys disagree with primary keys (S1). Hand-write entities and use `docs/research/scaffold-assessment.md` only as a column list.
3. **Never migrate.** Node owns the schema. No `Migrate`, `EnsureCreated` or `migrations` (AGENTS.md).
4. **Table and column names are camelCase** and set explicitly with `ToTable` / `HasColumnName`. The table is `series` (lowercase), not `Series`: R1 got this wrong and S1 corrected it.
5. **Every non-key column can be NULL.** No Node model sets `allowNull: false` except `createdAt` and `updatedAt` (R1). Make properties nullable.
6. **Dates are text** like `2026-10-06 22:08:12.334 +00:00` (three fractional digits even for `DATE(6)` columns, S1). Use `AbsDateConverter` in `src/Abs.Data/Conversions`. In JSON to clients, Node sends epoch milliseconds.
7. **Booleans** are `TINYINT(1)` and may be NULL: map as `bool?`.
8. **JSON columns are text.** Keep them `string?` on the entity and parse in the DTO or service. NULL and `[]` both occur, even in one row (for example `books.ebookFile` is NULL while the other JSON columns are `[]`, S1). If you ever write a JSON column, write the whole value back and keep every field Node expects.
9. **Test data comes from Node.** Add the table DDL to `tests/Abs.Api.Tests/Fixtures/schema.sql` from `sqlite_master`, and record expected JSON with Node's own models (see `research/k2-shape-comparison.md`, `tests/tools/record-node-items.js`).

## Per table

| Table | Owner | JSON text columns | Things to know |
|---|---|---|---|
| `libraries` | A | `settings`, `extraData` | Done. `settings` is passed through to clients as stored; a NULL value becomes `{}` |
| `libraryFolders` | A | none | Done. FK `libraryId` with cascade delete |
| `users` | B | `permissions`, `bookmarks`, `extraData` | Password hash column is `pash`. Mapped minimally by A (id, username, type, isActive, permissions); B added `createdAt` and `updatedAt`. D reads the `bookmarks` column through a separate shadow property (`BookmarkUserConfiguration.cs`), so the `User` entity does not have it. Bookmarks live here as JSON `{libraryItemId, time, title, createdAt}` with `createdAt` in epoch milliseconds (R1, `User.js:864-869`). Permission rules: `accessAllLibraries`, `librariesAccessible`, `accessExplicitContent`, `accessAllTags`, `itemTagsSelected` |
| `libraryItems` | C | `libraryFiles`, `extraData` | `mediaId` has **no foreign key**: `mediaType` says whether it points to `books` or `podcasts`. `libraryId` and `libraryFolderId` are `SET NULL` on delete. `title`, `titleIgnorePrefix`, `authorNamesFirstLast`, `authorNamesLastFirst` are kept in sync by **triggers** (R1, S1: 7 triggers, no views) |
| `books` | C | `narrators`, `audioFiles`, `ebookFile`, `chapters`, `tags`, `genres` | No relations on the entity itself; authors and series come through `bookAuthors` and `bookSeries` |
| `authors`, `series`, `bookAuthors`, `bookSeries` | C | none | Join tables have a UNIQUE pair. S1 called these close to the generated shape |
| `mediaProgresses` | D | `extraData` | `mediaItemId` has **no foreign key**; `mediaItemType` tells which table. Empty on the sample database, which is why scaffolding broke |
| `podcasts`, `podcastEpisodes` | D (for progress) | `tags`, `genres` (podcasts) | Not mapped for browsing yet; only what progress needs |
| `settings` | C | `value` | Holds `server-settings` with `sortingIgnorePrefix` and `sortingPrefixes`, which change the title sort |

## Triggers and writes

R1 and S1 agree on 7 triggers that keep `libraryItems` title and author-name columns in sync with `books`, `authors` and `bookAuthors`. The author-name ones use `GROUP_CONCAT(... ORDER BY ...)`, which needs SQLite 3.44 or later. **Unconfirmed:** whether the SQLite inside Microsoft.Data.Sqlite 8.x is new enough. Any .NET write to those three tables fires the triggers, so do not write them without testing that.

## Read-only and locking

- The connection is `Mode=ReadOnly` with a 5 second timeout (`src/Abs.Api/appsettings.json`). Do not change the journal mode (`research/wal-and-cache.md`).
- Writes (D's progress and bookmarks) are behind `ProgressWrites:Enabled` and off by default. Writing to a read-only connection will fail by design.

## Unconfirmed

- Exact date format for all `DATE(6)` columns across other tables: checked on the six core tables only.
- About 14 tables exist that R1 did not list (for example `apiKeys`, `sessions`, `playbackSessions`, `devices`, `feeds`, `collections`, `playlists`, `mediaItemShares`, `customMetadataProviders`). Nobody has mapped them.
