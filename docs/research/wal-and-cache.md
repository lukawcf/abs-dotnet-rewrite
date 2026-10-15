# T1: SQLite sharing (WAL, locking) and Node response cache

Method note: the subagent ran the experiments and reported findings. Its report file was not actually written, so this document was rewritten by the operator session from the raw result files in `G:\Project\abs-scratch\t1\out_*.json` and from upstream code re-read at the cited lines. The experiment script is `G:\Project\abs-scratch\t1\contend.py`.

## 1. Journal mode of the deployed database

- Backup copy `G:\Project\abs-backup\config-20261008\absdatabase.sqlite`: header bytes 18-19 are `1,1`, i.e. rollback-journal mode (not WAL). A Python `PRAGMA journal_mode` on a copy returned `delete` (subagent report; consistent with the file header).
- The live file itself was not opened; only the backup taken while the containers were stopped was read. Node could change the mode later, but upstream sets no journal mode (see below).
- `busy_timeout` is a per-connection setting, not stored in the file. Upstream `server/Database.js:227-233` creates Sequelize with `dialect: 'sqlite'`, `storage`, and `transactionType: 'IMMEDIATE'`. A grep for `busy` in `Database.js` finds nothing. So Node relies on its sqlite driver's default timeout (not verified, see section 5).

## 2. Writer/reader contention experiment (60 s per run, on copies)

Setup: one writer thread using `BEGIN IMMEDIATE` (like Node), 200 inserts per transaction, transaction held about 20 ms. One reader thread doing two small SELECTs in a loop. Each of the 8 runs lasted 60 s, run in parallel, so latencies are inflated by CPU contention. Each configuration ran once.

| Journal | Reader busy timeout | Reader open | Writes ok | Write errors | Reads ok | Read errors | Read p99 (ms) |
|---|---|---|---|---|---|---|---|
| DELETE | 0 ms | read-only | 589 | 0 | 561 | 235 "database is locked" | 188 |
| DELETE | 0 ms | read-write | 591 | 0 | 581 | 221 "database is locked" | 188 |
| DELETE | 5000 ms | read-only | 584 | 0 | 787 | 0 | 189 |
| DELETE | 5000 ms | read-write | 584 | 0 | 785 | 0 | 192 |
| WAL | 0 ms | read-only | 1979 | 0 | 696 | 0 | 405 |
| WAL | 0 ms | read-write | 1981 | 0 | 689 | 1 | 408 |
| WAL | 5000 ms | read-only | 1978 | 0 | 687 | 0 | 410 |
| WAL | 5000 ms | read-write | 1980 | 0 | 694 | 0 | 407 |

Observations:
- In the mode the deployed database actually uses (DELETE), a reader without a busy timeout failed on about 28-30% of attempts. With a 5 s timeout there were no errors.
- Opening read-only or read-write made no difference to locking.
- The writer never errored in any run.
- WAL gave about 3.3x more writes and almost no reader errors, but it is a persistent property of the file. Switching it is a write to Node's database and is not ours to do.

## 3. Does Node cache `GET /api/libraries`?

Yes, in process.
- `server/routers/ApiRouter.js:68`: `this.router.get(/^\/libraries/, this.apiCacheManager.middleware)`. Every GET under `/libraries` goes through the cache.
- `server/managers/ApiCacheManager.js:89-111`: the key is `{ user: username, url }`. A hit returns the stored body, headers and status. `sort=random` skips the cache.
- Invalidation: Sequelize hooks `afterCreate/Update/Destroy/BulkCreate/BulkUpdate/BulkDestroy/Upsert` (line 17-19) call `clear`, which empties the whole cache (line 63). The models in `highChurnModels` (session, mediaProgress, playbackSession, device, line 8) only clear partial slices (`/personalized`, `/me`).
- TTL: a 30 minute TTL is defined (line 7) but applied only to `/libraries/:id/personalized` URLs (line 103-105). All other cached responses have no expiry.
- Consequence: the hooks fire only for writes made through Node's Sequelize. If .NET (or anything else) writes to the database, Node keeps serving old `/libraries*` responses until some Node write clears the cache. A read-only .NET service does not cause this.
- Live check: through nginx at `http://localhost:8080`, `/status` and `/ping` return 200 with a weak Express ETag and no Cache-Control (subagent observation). `GET /api/libraries` without a token returns 401. Authenticated cache behaviour was NOT tested live, because no credentials were available. It is reasoned from the code above.

## 4. Recommendation for the .NET service

- **Do not change the journal mode.** Do not run `PRAGMA journal_mode=WAL` from .NET.
- **Open read-only with a busy timeout.** Suggested connection string for Microsoft.Data.Sqlite:
  `Data Source=/config/absdatabase.sqlite;Mode=ReadOnly;Cache=Private;Default Timeout=5;Pooling=True`
  and additionally run `PRAGMA busy_timeout=5000;` after opening a connection (or verify it), because the experiment shows a zero timeout fails about 30% of reads while Node writes.
- Keep read transactions short.
- Keep .NET read-only for the endpoints moved first. Any .NET write would leave Node's `/libraries*` cache stale (section 3) and fire the `libraryItems` triggers (`docs/research/scaffold-assessment.md`).
- A read-only connection cannot recover a hot journal left by a crashed writer. If the file were in WAL mode, a read-only connection would also need access to the `-shm` file.
- Docker: mount `./config` read-only into the .NET container only if this is acceptable. A read-only mount blocks creation of a rollback journal by the reader (readers do not create one), but if Node ever switches to WAL, the reader would fail without write access to the `-shm` file. Decide this with the team.

## 5. Unverified

- Journal mode of the live file (only the backup was read).
- Node's sqlite driver default busy timeout (no `node_modules` in the clone; 1000 ms is from memory only and not confirmed).
- How Microsoft.Data.Sqlite maps `Default Timeout` to `busy_timeout`.
- Single non-repeated, parallel experiment; numbers are indicative.
- Authenticated cache behaviour through nginx. (Update 2026-10-15: the cache behaviour itself was tested with a signed test token and confirmed, see `docs/known-limitations.md` entry 2. Not tested through nginx.)
