# W2 step 3 — Batch progress updates and deletion review

## Scope and source

- PATCH /api/me/progress/batch/update
- DELETE /api/me/progress/{progressId}

Reference: Node v2.37.1, commit 3563d49424d50170324de9236a24c591b6d965e9.
MeController.js:253-265 (delete), 296-320 (batch); User.js:730-830 (individual upsert);
MediaProgress.js:41-47 (removeById). Production writes remain disabled by default.
No bookmark features, authentication changes, nginx changes, push or PR.

## Implemented behavior

Batch requests with a missing/null/empty payload return 400, plain text Missing request payload.
Valid arrays are processed sequentially with one transaction per upsert.
Returned per-item errors are skipped; successful writes are retained.
The final response is 200 OK even if every supported entry returns an item error.
Repeated entries for one media item apply in order. Database exceptions still propagate,
rather than being swallowed or converted to successful responses.

Deletion uses a progress row ID, not a library item or media ID.
A single EF DELETE includes both row ID and authenticated user ID, avoiding a separate
ownership-check/delete race. Owned records return 200 OK; absent and other users' records
both return 404 Not Found. A repeated successful deletion therefore returns 404.
No other user's row is changed.

Both endpoints use the existing ProgressWrites:Enabled gate (default false); disabled
writes return 501 without changing rows. Anonymous requests return 401.

## Tests and evidence

83 tests passed, 0 failed, 0 skipped. This step adds 18 checks.
Thirteen cases compare actual Node controller/model execution and saved/reloaded SQLite rows
against .NET responses and all users' persisted data. Cases cover mixed success/failure,
all entries failing, sequential writes, episode errors, separate ownership on a shared item,
empty/null/object bodies, owned/other/missing/item-ID deletion and repeated deletion.
Four additional checks cover default-off and anonymous batch/delete requests without mutation.
One checks malformed/unsupported entries followed by a valid update.

The existing test expecting batch to remain unimplemented was updated to assert rejection
of its non-array payload, because this step now implements the endpoint.
No tests were removed or skipped.

Node recording uses real batch/delete controllers, User upsert and MediaProgress SQLite
persistence. Media lookup and socket emission/browser-user serialization are simulated.
It is method-level compatibility evidence, not complete Node HTTP or socket integration.
Only generated IDs/timestamps are normalized; IDs are checked as GUIDs and timestamps
against the .NET request window. Other persisted response fields compare exactly.

Evidence: tests/Abs.Api.Tests/Fixtures/w2-node-batch-delete.json and
docs/evidence/w2/w2-step3.trx. Build: zero warnings/errors. Format verification: exit 0.

## Boundaries for review

Only JSON arrays are supported for batch updates. Non-array objects receive the Node
missing-payload error; string-like/iterable edge cases are outside the implemented contract.
Malformed entries or missing/non-string libraryItemId are skipped rather than reproducing
possible Node exceptions. Individual unsupported values retain the single-upsert boundary
and are skipped; this is not a claim of arbitrary JavaScript input compatibility.

Node socket/cache coordination is not implemented. Existing production authentication,
nginx and concurrent-write integration limitations remain outside this task.
This step does not authorize live writes or routing changes.

Review partial-success semantics, deletion IDs, cross-user isolation and default-off behavior.
Implementation stops here for human review; bookmarks require the next explicit W2 task.
