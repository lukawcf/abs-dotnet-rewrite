# W2 step 2 — Single progress creation/update review

## Human-directed scope

The operator accepted step 1 and explicitly requested single-item progress creation/update,
partial-update/completion/timestamp parity tests, writes disabled by default, and a review stop.
No batch update, delete or bookmarks were implemented.

Routes: PATCH /api/me/progress/{libraryItemId} and the optional /{episodeId} variant.
Success returns 200 with plain text OK, matching Node MeController.js:275-288.
Missing item/episode and non-book item return the source's 404/400 messages.
The reserved batch/update route returns 501 rather than being captured by the optional-episode route.

Reference: Node v2.37.1 commit 3563d49424d50170324de9236a24c591b6d965e9;
User.js:730-830, MediaProgress.js:189-270, MeController.js:275-288.
Only D-owned classes/projections were added; shared changes are test factory configuration.
Node owns the schema: no application SQL, migrations, schema creation or nginx changes.

## Behavior verified

- Existing rows are located by the authenticated user and media ID; missing rows are created for that user.
- Missing payload properties preserve stored values; explicit null and supported numeric strings follow tested Node cases.
- Unknown stored extraData properties survive partial updates.
- New records use Node's separate creation defaults and do not auto-finish based on remaining time.
- Existing records finish only when remaining time is strictly below the threshold or percentage is strictly above it.
- Marking unfinished resets currentTime/progress and clears finishedAt.
- Existing createdAt is preserved. New createdAt may use a supplied supported date.
- Changed records refresh updatedAt; no-op updates preserve it.
  A supplied progress that triggers Node's extraData dirty flag refreshes updatedAt even if its stored value is unchanged.
- Existing records honor supported lastUpdate sync timestamps; invalid lastUpdate is ignored.
  Creation ignores lastUpdate, matching the source.
- Writes are transactional within .NET. This does not establish concurrency safety with Node.
- Payload identity fields cannot change row ownership/media identity. This is an intentional restriction
  relative to Node's broad Sequelize set(payload), not a claim of complete input parity.

## Write gate and input boundaries

ProgressWrites:Enabled is explicitly false in appsettings.json. Disabled writes return 501 without mutation.
The production connection remains Mode=ReadOnly. No live database was used or modified.
Testing explicitly enables writes only against the synthetic in-memory database.

Finite scalar numeric values, JSON booleans/null, and supported epoch/ISO timestamps are covered.
Unsupported numeric values, non-boolean completion/hide flags, invalid creation/completion dates,
and direct extraData replacement return 501; non-object JSON returns 400.
These responses are explicit implementation boundaries rather than original Node error contracts.
Not every JavaScript date/string/object coercion is reproduced.
Book media missing behind an existing item returns 501, an additional unsupported-corrupt-data boundary.
Large-data performance, real JWT/nginx integration, Node user cache/socket notifications and concurrent
Node/.NET writes remain unverified. These prevent a live route cutover at this stage.

## Evidence

- 63 passed, 0 failed/skipped: previous 29 plus 34 new checks.
- 23 scenarios compare real Node creation/update logic and saved/reloaded progress against .NET API results.
- The Node recorder runs actual User.createUpdateMediaProgressFromPayload and
  MediaProgress.applyProgressUpdate/create/save/reload on SQLite with extracted progress DDL.
  Item/episode lookups are simulated, and a minimal synthetic user table supplies foreign-key parents.
  It is not a full Node HTTP, relationship-query, JWT, cache or socket integration test.
- Generated IDs are validated as GUIDs; only generated timestamps are normalized and checked
  against the actual .NET request time window. All other response fields compare exactly.
- Additional checks cover write gating/no mutation, authentication, ownership, unsupported payloads,
  stored JSON extension preservation, and the unimplemented batch route.
- Initial fixture failure (56 failed, 1 passed) came from omitted podcasts foreign-key parent DDL.
  Added the real podcasts table and synthetic parent; subsequent 57 and then 63 checks passed.
  Original failure evidence retained at docs/evidence/w2/w2-step2-initial-fixture-failure.trx.
- Final test evidence: docs/evidence/w2/w2-step2.trx.
- Build: zero warnings/errors. Format verification: exit code 0.

## Human review

Review the endpoint success/error contracts, partial-update semantics, separate create/update completion
behavior, timestamp rules, write gate, and documented boundaries before selecting the next W2 task.
Implementation stops here awaiting human review; no subsequent feature work is authorized by this task.
