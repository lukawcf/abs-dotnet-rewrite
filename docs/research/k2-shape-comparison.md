# K2: response shape comparison against Node

Goal: show that `GET /api/libraries` from .NET returns the same JSON as Node.

## Why this was not recorded with a token

A real token needs the user's password, which the agent does not have and must not guess, and `.env` must not be read.
Instead the ground truth was produced by Node's own code:

1. `G:\Project\abs-scratch\groundtruth\abs-copy.sqlite` is a copy of `config/absdatabase.sqlite` (the live file is never opened).
2. `groundtruth/gt.js` runs inside a throwaway container of the same image the stack uses
   (`ghcr.io/advplyr/audiobookshelf:latest`, version 2.37.1, `docker run --rm --entrypoint node -v <dir>:/data ...`).
   It loads the real `server/models/Library.js` and `LibraryFolder.js`, calls `Library.getAllWithFolders()` and prints
   `{ libraries: libs.map(l => l.toOldJSON()) }`. That is the body Node builds in `LibraryController.findAll`
   (upstream `server/controllers/LibraryController.js:188-212`).
3. The output was saved as `tests/Abs.Api.Tests/Fixtures/node-libraries.json`.

What this does not cover: Node's HTTP layer (headers, middleware, the response cache) and authentication.
It covers the body, which is what the shape question is about.

## Result

The .NET service (Debug build, `ASPNETCORE_ENVIRONMENT=Development`, connected read-only to a second copy of the database)
was called with the dev header of the real root user:

```
curl -H "X-Dev-User: <root user id>" http://localhost:5099/api/libraries
```

| Check | Result |
|---|---|
| Byte-for-byte equal to Node's output | yes, 873 bytes each |
| Parsed JSON equal | yes |
| Without the header | 401 |

Unit tests repeat the check (`LibrariesEndpointTests`): the same fixture is compared with `JsonNode.DeepEquals`, and the property
order is compared separately.

## Behaviour copied from Node (and where it comes from)

- `findAll` filters by `librariesAccessible` only when the list is non-empty and ignores `accessAllLibraries`
  (`LibraryController.js:191-194`). `findOne` uses `User.checkCanAccessLibrary` (`User.js:646-650`), which honours the flag.
  Both are tested, including a user that has the flag and a list.
- 403 when the user cannot access the library, 404 with body `Library not found` (`LibraryController.js:1475-1498`).
- Libraries are ordered by `displayOrder` ascending (`models/Library.js:92-97`).
- Dates are epoch milliseconds and `updatedAt` is emitted as `lastUpdate` (`models/Library.js:202-219`).
- A null `settings` column gives `{}` because Node spreads the value.

## Not implemented (left to Node, answered with 501)

- `GET /api/libraries?include=stats`
- `GET /api/libraries/:id?include=filterdata`

## Not verified

- A podcast library with real data (the fixture's second library is synthetic).
- Behaviour with a library whose `settings` has keys in a different order. The order is preserved from the stored text.
- Pagination parameter validation (`limit`, `page`) applies to `/items`, not to these two endpoints.
