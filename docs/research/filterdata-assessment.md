# Assessment: answering `GET /api/libraries/:id?include=filterdata` in .NET

Written 2026-10-15. This is an assessment, **nothing was implemented**. The experiment in section 3 is reproducible with `tests/tools/sortcheck/`.

## 1. What the request is and who uses it

- The web client calls it on every library page: `client/store/libraries.js:115` (`/api/libraries/${libraryId}?include=filterdata`). In one browsing session it was 9 of 27 API calls, the most frequent API request.
- The client reads `data.library`, `data.filterdata`, `data.issues`, `data.numUserPlaylists` (`libraries.js:116-130`) and nothing else. **`loadedAt` is never read by the client** (searched `client/` and `server/`: it appears only inside `libraryFilters.js`).
- Node's answer is `{ filterdata, issues, numUserPlaylists, library }` (`LibraryController.js:220-233`). `issues` is `filterdata.numIssues`.

## 2. How Node builds `filterdata`

Source: `server/utils/queries/libraryFilters.js:449-697`, `server/Database.js:477-660`.

| Field | Book library | Podcast library |
|---|---|---|
| `authors`, `series` | `{id, name}` rows of the library (series name `'No Title'` when empty) | empty |
| `genres`, `tags`, `languages` | distinct values from `books` of the library | from `podcasts` |
| `narrators`, `publishers`, `publishedDecades` | from `books` (decade = floor(year/10)*10, only when 0 < year < 3000) | empty |
| `bookCount`, `authorCount`, `seriesCount`, `podcastCount` | counts | counts |
| `numIssues` | books whose item `isMissing` or `isInvalid` | 0 |
| `loadedAt` | `Date.now()` when the cache entry was built | same |

All lists are sorted with `Intl.Collator(undefined, { numeric: true, sensitivity: 'base' })` (`libraryFilters.js:8-10`), stable.

**The data is cached in Node's memory** (`Database.libraryFilterData`), for 30 minutes (`libraryFilters.js:449-458`), and Node patches the cached lists in place when it edits things: new genres, tags, narrators, publishers, series and authors are **appended with `push`, not re-sorted** (`Database.js:492-583`).

## 3. Experiment: can .NET reproduce the sort order?

119 hard names (numbers, `Book 02`/`Book 2`, case, accents, `ß`/`ss`, punctuation, CJK, Cyrillic, Greek, full-width digits, Turkish dotted i, ties such as `a`/`A`) were sorted in the Node container (ICU 78.3, locale en-US) and in .NET 8.0.31 inside `mcr.microsoft.com/dotnet/aspnet:8.0` (the image `abs-dotnet` runs in).

| .NET comparer | Positions that differ from Node |
|---|---|
| `CompareInfo.Compare` with ignore case, accents, width, kana | 34 of 119 (it has no numeric ordering in .NET 8) |
| Digit runs compared by numeric value (Unicode decimal digits, leading zeros ignored), all other chunks by `CompareInfo.Compare` with the same options, ties keep input order | **0 of 119** |

So the sort is solvable. Not tested: other server locales (Node uses the machine default; both containers ran en-US), very long numbers beyond 64 bit are handled by length comparison but were not in the list, and the order of rows that compare equal when the database returns them in different orders (the database has no ORDER BY in Node's queries either).

## 4. What can still differ from Node if .NET computes it fresh from the database

| # | Difference | When | Visible to the user |
|---|---|---|---|
| 1 | `loadedAt` is a different number | always | no, the client never reads it |
| 2 | Order inside a list after an edit made in Node: Node has appended the new value at the end of its cached list, .NET returns it sorted | for up to 30 minutes after such an edit | the dropdown order; .NET's order is the correct one |
| 3 | Node's cache is stale: a change made outside Node, or by a path that Node does not patch (for example a deleted book), is missing in Node and present in .NET | up to 30 minutes | a stale value in Node, a current one in .NET |
| 4 | Ties between equal-sorting names come out in database order, which is unspecified in both | only for names that differ by case or accents only | order of such names |

Everything else (counts, `numIssues`, `numUserPlaylists`, the library JSON) can be computed exactly. Podcast libraries would have to return 501 (nginx then falls back to Node).

## 5. What it would take

About 1.5 to 2 hours:

- Entities for `playlists` (for `numUserPlaylists`: playlists of the user and library, `Database.playlistModel.getNumPlaylistsForUserAndLibrary`) and the book columns `tags`, `genres`, `narrators`, `publisher`, `publishedYear`, `language` that are not mapped yet.
- A service with about six queries, the comparer above, a DTO, controller branch (`include=filterdata` only, nothing else combined).
- Unit tests and an e2e case with `loadedAt` normalised, plus a case with many tricky names (the sample database has 4 books, which would hide sort bugs).
- Docs: this would be the first answer that is **not byte-identical to Node** by design (differences 1 to 4), so it needs an explicit exception in `known-limitations.md` and in AGENTS.md ("answer exactly like Node").

## 6. Options

| Option | Effect | Risk |
|---|---|---|
| **A. Keep it in Node (today)** | nothing changes | none. Node answers from memory and it is cheap |
| **B. Implement in .NET, fresh from the database** | .NET answers the most frequent API request | differences 2 to 4 above, a new documented exception, and a larger surface that must match Node in every later Node version |
| **C. B plus a guard in nginx** | same as B, but only when Node's data cannot be stale | not possible: nginx cannot see Node's cache state |

## 7. Recommendation

**Keep option A unless the goal is to show .NET answering the library page.** The benefit is small (Node already answers from memory, one request per page) and the cost is a permanent exception to the rule that .NET answers exactly like Node. If the group wants B anyway, the experiment shows the hard part (the sort) is solved; do it with the guardrails in section 5 and record differences 2 to 4 as known limitations.

## Unconfirmed

- Behaviour with a locale other than en-US on the Node host.
- Whether Node patches the cache for every kind of edit; only the functions listed in `Database.js:477-660` were read.
- Real data volume: the measured session used a 4-book library.
