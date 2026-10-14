# AGENTS.md

Rules for anyone (human or AI agent) adding a slice to this repo. The reference slice is **Libraries**
(`GET /api/libraries`, `GET /api/libraries/:id`). Copy its structure.

## What this project is

We rewrite endpoints of audiobookshelf (Node.js) in ASP.NET Core + EF Core. The Node app keeps running
behind nginx and both read the same SQLite file.

- **Node owns the schema.** .NET only maps and reads it.
- **Never** call `Migrate`, `EnsureCreated`, or `dotnet ef migrations`, and never regenerate entities with
  `dotnet ef dbcontext scaffold` (see `docs/research/scaffold-assessment.md`: the output depends on row counts).
- **No raw SQL** in `src/`. Use LINQ through `AbsDbContext`. (Test fixtures under `tests/**/Fixtures` hold plain SQL for setup only.)
- The database is opened read-only (`Mode=ReadOnly`, see `src/Abs.Api/appsettings.json`). Do not add writes
  unless your task says so: a write behind Node's back leaves Node's `/libraries*` response cache stale
  (`docs/research/wal-and-cache.md`).
- Do not change `nginx/nginx.conf` or add routes to it unless the task says so. A route moves to .NET only after the
  same test passes against Node and against .NET.

## Structure of a slice

| Piece | Location | Libraries example |
|---|---|---|
| Entity | `src/Abs.Data/Entities/` | `Library.cs`, `LibraryFolder.cs` |
| EF configuration | `src/Abs.Data/Configurations/` | `LibraryConfiguration.cs` |
| DTO | `src/Abs.Api/Dtos/` | `LibraryDto.cs` |
| Service | `src/Abs.Api/Services/` | `LibraryService.cs` |
| Controller | `src/Abs.Api/Controllers/` | `LibrariesController.cs` |
| Tests | `tests/Abs.Api.Tests/` | `LibrariesEndpointTests.cs` |
| Shared interfaces | `src/Abs.Api/Abstractions/` | `IPermissionService`, `IUserProgressReader` |

Steps:

1. **Entity.** Plain class, string ids (the database stores lowercase GUID text), nullable for every column except the
   NOT NULL ones (`createdAt`, `updatedAt`). JSON columns stay `string?`; parse them in the DTO. Booleans are `bool?`.
2. **Configuration.** One `IEntityTypeConfiguration<T>` per entity. Set `ToTable` and `HasColumnName` explicitly: table
   and column names are camelCase (`libraryFolders`, `displayOrder`). Date columns use
   `.HasConversion<AbsDateConverter>()` (text like `2026-10-06 22:08:12.334 +00:00`). It is picked up automatically by
   `ApplyConfigurationsFromAssembly`.
3. **DTO.** Records whose JSON matches Node's response field for field, in the same order. Dates are epoch
   milliseconds (`ToUnixTimeMilliseconds()`). Cite the Node source file and lines in a comment.
4. **Service.** Takes `AbsDbContext`, returns DTOs. No HTTP types, no permission checks.
5. **Controller.** `[Authorize]`, route under `api/...` (nginx passes the path through unchanged). Ask
   `IPermissionService` for access decisions. Copy Node's status codes and messages exactly (403, 404 `Library not found`).
   For options you do not support, return `501` so the request can be left to Node.
6. **Tests.** See below. Register services in `Program.cs`.

## Naming

- Namespaces are file-scoped and follow the folder (`Abs.Api.Services`).
- Controllers: `<Plural>Controller`. Services: `<Thing>Service`. DTOs: `<Thing>Dto`. Interfaces start with `I`.
- Only touch files in your own module. Shared files (`Program.cs`, `AbsDbContext.cs`, `Abstractions/`) need a short, additive change only.
  `User` is mapped minimally for permission checks; the Auth and Users module extends it.

## Tests

- Use `AbsApiFactory` (in-memory SQLite built from `tests/Abs.Api.Tests/Fixtures/schema.sql` and `seed.sql`). Never open the live
  database or `config/`.
- Add your tables to `schema.sql` using the DDL from a real database (`sqlite_master`), and rows to `seed.sql`.
- Compare JSON against what **Node produced**, not against what you think it should be. See
  `docs/research/k2-shape-comparison.md` for how the Libraries fixture (`Fixtures/node-libraries.json`) was recorded.
- Use the fakes in `tests/Abs.Api.Tests/Fakes/` for `IPermissionService` and `IUserProgressReader`.
- Authenticate tests with the header `X-Dev-User: <user id>` (see below).

## Dev-only fake login

When `ASPNETCORE_ENVIRONMENT=Development`, the header `X-Dev-User: <user id>` authenticates that user id. No token is checked.

- The handler (`Auth/DevHeaderAuthenticationHandler.cs`) is wrapped in `#if DEBUG`, so Release builds (the Docker image) do not
  contain it. It is also registered only when the environment is Development (`Auth/AuthenticationSetup.cs`).
- Everywhere else the scheme is `JwtAuthenticationHandler`, which validates tokens issued by Node with `JWT_SECRET_KEY`
  (payload claim `userId`). Without a valid token protected endpoints return 401. Tests cover Production and Staging.
- Write endpoints (progress, bookmarks) are off unless `ProgressWrites:Enabled` is true.
- Test fixtures: `AbsApiFactory` loads the W2 progress and bookmark fixtures by default. Tests that count library items
  set `IncludeW2Fixtures = false`, because those fixtures move three items into library A.

## Commands (all must pass before you hand work over)

```bash
dotnet build AbsRewrite.sln
dotnet test AbsRewrite.sln
dotnet format AbsRewrite.sln --verify-no-changes
```

## When something fails

- Stop and report after 3 failed attempts at fixing the same problem. Say what you tried and show the error.
- Never fake results, skip a test, or mark a failing test as passing.
- Do not invent facts about the Node code. Cite the upstream file and line, for example `server/controllers/LibraryController.js:188-212`.

## Logging your work

Append one entry per task to `docs/logs/<your name>.md` with: task, workflow (W1 one-shot / W2 small steps / W3 planning-first),
agent and model, operator, prompt (path or source), time spent, cost, result (success, or failure and root cause), outcome
(files changed, commit hash).

## Never commit

`.env`, `library/`, `config/`, `metadata/` (already in `.gitignore`).
