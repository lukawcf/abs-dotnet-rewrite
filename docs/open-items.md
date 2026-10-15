# Open items

DEV.md is not in the repository, so this list lives here. Move it into DEV.md "Open items" when that file exists. Only unfinished things are listed.

## Blocking a cutover

1. **E's integration test suite does not exist.** A route may move in `nginx/nginx.conf` only after the same test passes against Node and against .NET (AGENTS.md). Owner: E (ShamiaAkterShanta, unconfirmed; no commits yet).
2. **Five routes are already cut over** by B's commit `a3d8393` (`/api/libraries`, `/api/libraries/<id>`, `/api/me`, `/api/users`, `/api/users/<id>`), without E's test. Decide: keep, revert, or send requests with `include=` to Node (`known-limitations.md` entry 11). The client calls `/api/libraries/<id>?include=filterdata` (`client/store/libraries.js:115`), which .NET answers with 501.
3. **`/api/me` returns placeholder `mediaProgress` and `bookmarks`** (`research/d-integration-handover.md`) and is already routed to .NET. Needs B and D, or a revert.
4. **Socket event `user_updated` is never emitted by .NET** (`known-limitations.md` entry 1). The group must pick option (a), (b) or (c).
5. **Node cache invalidation and write lock handling** are open before any write is enabled (`known-limitations.md` entries 2 and 3).

## Decisions for the group meeting

6. How many workflows to compare: 3 or 4.
7. Stop-loss retry count (AGENTS.md currently says 3).
8. The two freeze dates.
9. Whether the config volume stays read-only for the .NET container. It works while Node uses the rollback journal. If Node ever switches to WAL, a read-only mount breaks reads (`research/wal-and-cache.md` section 4).
10. Whether the W2 write flag (`ProgressWrites:Enabled`) is ever turned on during the project.

## Housekeeping

11. **`JWT_SECRET_KEY` must reach the team** through a private channel (it is not in the repository and must not be). Done by the repository owner, outside git.
12. **Collaborator ShamiaAkterShanta** had not accepted the invitation when last checked.
13. **DEV.md** is missing. Two documents depend on it (ownership scope, this list).
14. **Authenticated cache behaviour** was never tested live (needs a login token).
15. **Branch `feat/progress-bookmarks-integration`** can be deleted (merged). GitHub offers a button on PR #2.
16. **`core.autocrlf=true` on Windows** makes `dotnet format --verify-no-changes` fail with line-ending errors. A `.gitattributes` with `* text=auto eol=lf` would fix it for everyone. Not added yet; it changes how every file is checked out, so the group should agree first.

## Not verified anywhere

- Behaviour on a database with real, large data.
- SQLite version inside Microsoft.Data.Sqlite versus the `GROUP_CONCAT ... ORDER BY` triggers.
- Podcast libraries.

## Environment

17. **Port 8080 is reserved by Windows on the author's machine** (excluded port range 7981-8080 after a Docker restart), so `docker-compose up` cannot publish nginx there. The `abs-dotnet` and `abs-node` containers run; nginx does not. Releasing the range needs an administrator (restart of the winnat service), which was not done. Checks in this session used a temporary nginx on port 18080 with the same config.
