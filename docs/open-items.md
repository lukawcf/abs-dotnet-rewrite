# Open items

DEV.md is not in the repository, so this list lives here. Move it into DEV.md "Open items" when that file exists. Only unfinished things are listed.

## Blocking or open in the code

1. **The e2e suite covers reads only.** Writes are disabled in .NET and routed to Node. Before any write is enabled: Node cache invalidation, socket event `user_updated` and lock handling are needed (`known-limitations.md` entries 1, 2, 3).
2. **`GET /api/users/:id`, podcast libraries and `include=` options** stay in Node by design.
3. **E's role is unfilled.** ShamiaAkterShanta has no commits. `tests/e2e/run_e2e.py` was written by A and can be extended: add podcast data, more users, a browser run.
4. **The suite has only seen the 2026-10-06 sample database** (1 library, 4 items). Run it on a larger copy before trusting the numbers.

## Decisions for the group meeting

6. How many workflows to compare: 3 or 4.
7. Stop-loss retry count (AGENTS.md currently says 3).
8. The two freeze dates.
8b. Whether `tests/e2e/run_e2e.py` becomes the gate for every future nginx change (recommended).
9. Whether the config volume stays read-only for the .NET container. It works while Node uses the rollback journal. If Node ever switches to WAL, a read-only mount breaks reads (`research/wal-and-cache.md` section 4).
10. Whether the W2 write flag (`ProgressWrites:Enabled`) is ever turned on during the project.

## Housekeeping

11. **`JWT_SECRET_KEY` must reach the team** through a private channel (it is not in the repository and must not be). Done by the repository owner, outside git.
12. **Collaborator ShamiaAkterShanta** had not accepted the invitation when last checked.
13. **DEV.md** is missing. Two documents depend on it (ownership scope, this list).
15. **Branch `feat/progress-bookmarks-integration`** can be deleted (merged). GitHub offers a button on PR #2.
16. **Line endings (fixed 2026-10-15):** `.gitattributes` now forces LF, so `dotnet format --verify-no-changes` also passes on Windows checkouts. Existing working copies keep CRLF until files are checked out again; a fresh clone is clean.

## Not verified anywhere

- Behaviour on a database with real, large data.
- SQLite version inside Microsoft.Data.Sqlite versus the `GROUP_CONCAT ... ORDER BY` triggers.
- Podcast libraries.

## Environment

17. **Port 8080 is reserved by Windows on the author's machine** (excluded port range 7981-8080 after a Docker restart), so `docker-compose up` cannot publish nginx there. The `abs-dotnet` and `abs-node` containers run; nginx does not. Releasing the range needs an administrator (restart of the winnat service), which was not done. Checks in this session used a temporary nginx on port 18080 with the same config.
