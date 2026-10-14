# W2 step 1 — Progress reads for human review

Implemented only:

- GET /api/me/progress
- GET /api/me/progress/{libraryItemId}
- GET /api/me/progress/{libraryItemId}/{episodeId}

Node reference: v2.37.1, commit 3563d49424d50170324de9236a24c591b6d965e9.
MeController.js:107-115,234-245; User.js:716-721; MediaProgress.js:155-178;
ApiRouter.js:176,182-183.

The current authenticated user's rows are returned without additional library filtering,
matching the inspected Node read methods. A lookup uses extraData.libraryItemId, not the
progress ID or book mediaItemId. The optional episode parameter matches mediaItemId.
Missing rows return 404 with plain text Not Found. Responses preserve Node field order,
extraData.progress (including its JSON type), nullable fields and epoch-millisecond dates.
Null booleans become false; an epoch-zero finishedAt becomes null, matching Node truthiness.

The state-changing GET remove-from-continue-listening is reserved with a 501 response
so it cannot accidentally be treated as an episode query. It is not implemented or routed
through nginx. No write operations or bookmark functionality were added.

## Evidence and limitations

- 29 tests pass: original 18 plus 11 new checks, with no failures or skips.
- Fixture recorder executes real Node model and controller methods on synthetic plain objects.
  These methods are not rewritten or mocked; request/response objects and model loading are simulated.
  This is method-level evidence, not a Node HTTP/database/authentication integration test.
- DDL is freshly extracted from the isolated seed archive copy. Only synthetic rows enter tests.
- Build passes with zero warnings/errors. Formatting verification passes.
- Real JWT and nginx integration remain unverified. Production database configuration remains read-only.
- Multiple rows for one libraryItemId without an episode select the first loaded row in both implementations;
  cross-runtime ordering for ambiguous duplicates is not established by these fixtures.
- Reads currently load all progress rows for the user, then match the JSON libraryItemId in memory.
  Large-user performance is not benchmarked. IUserProgressReader integration is outside this first task.

## Suggested human checks

Review the three route shapes, ownership filtering, expected Node JSON, and the 404 behavior.
Record acceptance or specific corrections in the chat before selecting the next W2 task.
This run ends at this review point; there is no automatic next implementation stage.
