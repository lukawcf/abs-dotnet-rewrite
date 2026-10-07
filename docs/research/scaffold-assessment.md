# EF Core scaffold assessment (S1)

Environment verified: `dotnet --version` = 8.0.101; `dotnet ef --version` = 8.0.1 (the tool warns that the runtime is 8.0.31). Packages: Microsoft.EntityFrameworkCore.Sqlite 8.0.* and .Design 8.0.*, restored into `G:\Project\abs-scratch\nuget` (NUGET_PACKAGES). Work was done on a copy (`G:\Project\abs-scratch\abs-copy.sqlite`, copied with `cp`; the original was never opened by a DB tool). Scratch project: `G:\Project\abs-scratch\scaf`. Python sqlite3 (SQLite 3.45.1) was opened read-only (`mode=ro`) on the copy.

The live DB is ABS 2.37.1 (`migrationsMeta`: version = maxVersion = 2.37.1). It is small: 1 library, 1 folder, 4 libraryItems, 4 books, 1 user, 0 mediaProgresses, 0 series.

## What was tried

| # | Command | Result |
|---|---|---|
| 1 | `dotnet ef dbcontext scaffold ... Microsoft.EntityFrameworkCore.Sqlite -o Models` (all tables) | FAILED. `InvalidOperationException`: FK `ApiKey.CreatedByUserId : Guid?` does not match principal key `User.Id : string`. No files written. |
| 2 | Same with `-t libraries -t libraryFolders -t libraryItems -t books -t users -t mediaProgresses` | FAILED. Same error for `MediaProgress.UserId : Guid?` vs `User.Id : string`. |
| 3 | Same with the 5 tables (without mediaProgresses) | OK. Files: Book, Library, LibraryFolder, LibraryItem, User, AbsCopyContext. |
| extra | `-t mediaProgresses -t podcastEpisodes -t series -t authors -t bookAuthors -t bookSeries` (no users table, separate output dir) | OK. |

Root cause of the failure: the scaffolder infers the CLR type from sampled data. Columns declared `UUID` or `DATETIME` that contain rows are scaffolded as `string` (with the message "should map to Guid/DateTime, but its values are in an incompatible format"), while the same declared types in EMPTY tables become `Guid` / `DateTime`. The result depends on current data, so FK columns and the PKs they reference can disagree (users.id has data, so string; mediaProgresses.userId and apiKeys.createdByUserId are empty, so Guid?). The scaffold output is therefore not stable or reproducible: a DB with different row counts gives different types.

## Actual schema facts (from the copy)

Tables (27): apiKeys, authors, bookAuthors, bookSeries, books, collectionBooks, collections, customMetadataProviders, devices, feedEpisodes, feeds, libraries, libraryFolders, libraryItems, mediaItemShares, mediaProgresses, migrationsMeta, playbackSessions, playlistMediaItems, playlists, podcastEpisodes, podcasts, series, sessions, settings, sqlite_stat1, users. Views: none. Triggers: 7.

- The series table is lowercase `series` (not `Series`). db-notes.md left this open. It has a `libraryId` FK (ON DELETE CASCADE) and unique index `unique_series_name_per_library`.
- Table names match db-notes.md section 5 for the inferred ones (`libraries`, `libraryFolders`, `podcasts`) and the confirmed ones. Column names are case-preserved camelCase as predicted.
- Tables not mentioned in db-notes.md: apiKeys, collections, collectionBooks, customMetadataProviders, devices, feeds, feedEpisodes, mediaItemShares, playbackSessions, playlists, playlistMediaItems, sessions, settings, migrationsMeta (plus SQLite's sqlite_stat1). `apiKeys` has `userId` (CASCADE) and `createdByUserId` (SET NULL) FKs to users.
- Triggers present, matching db-notes.md section 6: `update_library_items_title`, `update_library_items_title_ignore_prefix`, `update_library_items_title_from_podcasts_title`, `update_library_items_title_ignore_prefix_from_podcasts_title_ignore_prefix`, `update_library_items_author_names_on_book_authors_insert`, `..._on_book_authors_delete`, `..._on_authors_update`. The three author triggers use `GROUP_CONCAT(... ORDER BY ...)`. The SQLite used for inspection was 3.45.1 (Python's); the version bundled with Microsoft.Data.Sqlite 8.0.x was not checked and must be verified before .NET writes to bookAuthors/authors.
- Column types: ids declared `UUID` (values are TEXT), booleans `TINYINT(1)` (stored as integer 0/1), JSON columns declared `JSON` (stored as TEXT), `size` BIGINT integer, `duration` FLOAT real. Only createdAt/updatedAt are NOT NULL in the six target tables; `isActive`/`isLocked` have DEFAULT 0; everything else is nullable, as db-notes.md predicted.
- Id format: lowercase hyphenated 36-char GUID text in every sample (users, libraries, libraryFolders, libraryItems, books, FK columns, mediaId).
- Date format: `2026-10-06 22:08:12.334 +00:00` (`YYYY-MM-DD HH:MM:SS.mmm +00:00`, UTC text) for createdAt, updatedAt, lastScan, lastSeen, mtime, ctime, birthtime (birthtime was `1970-01-01 00:00:00.000 +00:00` in the sample). The DATE(6) columns show only 3 fractional digits.
- JSON samples: `users.bookmarks` = `[]`, `users.extraData` = `{"seriesHideFromContinueListening":[]}`, `libraryItems.extraData` = `{}`, `libraries.extraData` = `{"lastScanMetadataPrecedence":[...]}`. In `books`, `ebookFile` is SQL NULL while narrators/chapters/tags/genres are `[]`, so NULL and `[]` both occur.
- `libraryItems.ino` is VARCHAR holding a digit string; the scaffolder warned it "should map to decimal" and fell back to string. Keep it a string.
- FKs (`pragma foreign_key_list`): libraryFolders.libraryId -> libraries CASCADE/CASCADE; libraryItems.libraryId and .libraryFolderId -> SET NULL/CASCADE; mediaProgresses.userId -> users CASCADE/CASCADE. All as in db-notes.md. libraryItems.mediaId and mediaProgresses.mediaItemId have no FK. Index names match (`library_items_library_id_media_type_title`, `media_progresses_user_item_finished_time`, ...).
- No composite primary keys in the six target tables (single `id` each). bookAuthors and bookSeries have surrogate `id` plus unique (bookId, authorId) / (bookId, seriesId) indexes.

## Per-table assessment of generated entities

5-table run property types: all `id`/FK columns `string`, all date columns `string`, booleans `byte?`, JSON `string?`, `size` `long?`, `duration` `double?`.

| Table | Generated | Issues |
|---|---|---|
| libraries | Library, nav collection to LibraryFolders and LibraryItems | Id/dates as string; settings/extraData raw JSON; ToTable("libraries") correct. |
| libraryFolders | LibraryFolder, FK to Library with cascade | LibraryId string?; relation fine. |
| libraryItems | LibraryItem with navs Library and LibraryFolder | `MediaId` string with no relation (correct, polymorphic). Booleans `byte?`. Mtime/Ctime/Birthtime/LastScan strings. LibraryFiles/ExtraData raw JSON. Trigger-maintained denormalised columns (title, titleIgnorePrefix, authorNames*) are plain writable properties. NOCASE index collation is not reflected in the context. |
| books | Book | No relations at all (correct; link is via libraryItems.mediaId and bookAuthors/bookSeries). Explicit/Abridged `byte?`. Six JSON string columns. PublishedYear is a string. |
| users | User | Id string, IsActive/IsLocked `byte?`, dates strings, `Pash` hash, permissions/bookmarks/extraData JSON strings. No nav to mediaProgresses because that table was excluded. |
| mediaProgresses | Scaffolds only without `users` (extra run): Id Guid, MediaItemId Guid?, FinishedAt DateTime?, UserId Guid? | In a combined model it breaks the scaffold (Guid FK vs string PK). The Guid/DateTime types come only from the table being empty today. |

Awkward everywhere: nullable everything, `byte?` instead of `bool`, no DateTime, no typed JSON, DB types kept as `UUID`/`DATETIME`/`JSON` via HasColumnType (harmless on SQLite).

## Problems scaffolding cannot solve

1. Types depend on row data; output is not reproducible. Scaffold at most once, then hand-edit and commit. Never regenerate in a build.
2. `Guid` ids: Microsoft.Data.Sqlite writes uppercase hyphenated text and SQLite text comparison is case-sensitive, while Node ids are lowercase. Use `string` ids or a lowercase converter. (Reasoning from db-notes.md and observed lowercase data; not tested in code here.)
3. Dates: stored with a ` +00:00` suffix; default `DateTime` handling will not round-trip the same text. Use a value converter (write `yyyy-MM-dd HH:mm:ss.fff +00:00`) or keep strings and convert at the edge.
4. JSON stays text; NULL and `[]` both occur.
5. The 7 triggers fire on .NET writes.
6. The schema evolves with Node migrations; hand-mapping only needed columns is safer than regenerating.

## Recommendation

Use the scaffold only as a column-name reference, not as-is.

Map by hand: `users` (permissions/bookmarks JSON, `pash`, boolean semantics, dates), `mediaProgresses` (empty now but will hold data; the scaffold already breaks on it), `libraryItems` (polymorphic mediaId without relation, denormalised trigger-maintained columns read-only from .NET, NOCASE ordering), `books` (JSON columns, no FKs), `libraries` (settings JSON, display order).

Use generated shape with minimal edits: `libraryFolders`, and later `authors`, `series`, `bookAuthors`, `bookSeries`, `podcastEpisodes` (plain tables, simple FKs). Even for these, force id and FK properties to `string` first so they cannot flip to `Guid` when data changes, and add a bool conversion for the `byte?` flags plus a shared date converter.

Cross-cutting: one hand-written DbContext, `ToTable` with exact names above, string ids with `ValueGeneratedNever`, read-only denormalised libraryItems columns, `AsNoTracking` by default, no `EnsureCreated`/`Migrate`.

## Mismatches against docs/research/db-notes.md

- `Series` casing: live table is lowercase `series` (the note flagged this as unknown; migration v2.15.0 used capital S).
- DATE(6) columns show only 3 fractional digits; otherwise the date format inference is confirmed.
- The note's table list is incomplete: about 14 tables are not mentioned (see above).
- Triggers: 7 in sqlite_master, same as the note (4 title + 3 author-name). Views: none, confirmed.
- New findings not in the note: scaffolding is data-dependent; `apiKeys.createdByUserId` references users (SET NULL), relevant to user deletion.
- All other items marked [inferred] (names, lowercase UUID text, 0/1 booleans, JSON text, FK actions) were confirmed on the copy.

Not verified: SQLite version bundled with Microsoft.Data.Sqlite 8.0.x (for `GROUP_CONCAT ... ORDER BY` triggers), journal mode of the live file, an actual EF query against the copy.
