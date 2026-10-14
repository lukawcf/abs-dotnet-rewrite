# D W2 handover — Progress and bookmarks

Status: local implementation ready for group review, not a production cutover.
Branch: feat/progress-bookmarks-w2-human-guided. Baseline: ebdc009.
Reference: Node v2.37.1, commit 3563d49424d50170324de9236a24c591b6d965e9.
Earlier step-review files are historical snapshots; this document describes the current handover.

## Implemented HTTP routes

| Method | Route | Behavior |
| --- | --- | --- |
| GET | /api/me/progress | Current user's progress list |
| GET | /api/me/progress/{libraryItemId}/{episodeId?} | Item/optional episode progress; absent returns 404 |
| PATCH | /api/me/progress/{libraryItemId}/{episodeId?} | Create/update one progress row |
| PATCH | /api/me/progress/batch/update | Sequential per-entry updates; returned errors skipped |
| DELETE | /api/me/progress/{progressId} | Delete owned progress row; absent/other owner returns 404 |
| GET | /api/me/bookmarks | Current user's full bookmark list |
| GET | /api/me/bookmarks/{libraryItemId} | Item bookmarks after library/tag/explicit checks |
| POST | /api/me/item/{id}/bookmark | Create or update duplicate bookmark title |
| PATCH | /api/me/item/{id}/bookmark | Update existing title; missing bookmark returns 404 |
| DELETE | /api/me/item/{id}/bookmark/{time} | Delete with Node's tested scalar equality behavior |

The separate mutating GET remove-from-continue-listening remains 501 and is not counted as an implemented route.
All routes require authorization. Writes remain default-off.
Source references: MeController.js:107-146,234-320,327-435; User.js:643-682,716-830,841-918;
MediaProgress.js:41-47,155-270. Individual source comments and step reviews give further detail.

## Internal interface for other members

IUserProgressReader is now implemented by PlaybackProgressService.
Program.cs registers both types to the same scoped instance. Other modules can inject:

```csharp
using Abs.Api.Abstractions;

public class ExampleService(IUserProgressReader progress)
{
    public Task<UserProgress?> ReadAsync(string authorizedUserId, string itemId, CancellationToken ct)
        => progress.GetProgressAsync(authorizedUserId, itemId, ct);
}
```

- Returns null if no matching row exists for that user/item, including another user's row.
- Returns LibraryItemId, Progress, CurrentTime and IsFinished otherwise.
- Uses stored extraData.progress, not currentTime/duration.
- The internal double contract converts supported numeric strings to numbers; missing/null currentTime becomes 0.
  Unsupported progress values become 0 in this internal projection. The HTTP DTO retains its JSON types.
- Reads use no tracking and see later persisted updates; no separate .NET progress cache is introduced.
- Cancellation is propagated.
- The caller must authorize the supplied user ID; this internal API does not infer the current HTTP user.
- There is no episode parameter in the existing interface. For ambiguous multiple episodes under one item,
  it follows the first-loaded matching row used by the existing lookup. Do not treat it as a podcast aggregate.
- The existing shared interface signature is unchanged. No other member needs to implement it first.

## Running and configuring

Normal local checks:

```text
dotnet build AbsRewrite.sln
dotnet test AbsRewrite.sln
dotnet format AbsRewrite.sln --verify-no-changes
```

Tests use AbsApiFactory, synthetic in-memory SQLite and X-Dev-User in Development/Debug.
No live database, .env or real credentials are required for these tests.
The application defaults to Mode=ReadOnly and ProgressWrites:Enabled=false.
Enabling D writes requires both ProgressWrites__Enabled=true and an intentionally writable database
connection/mount. Only use an isolated database copy until group integration issues are resolved.
The compose mount is still read-only; setting the feature gate alone does not enable database writes.

Real JWT support is not implemented in this baseline: Production/Staging still use the rejecting
authentication placeholder. The dev header is not a production credential.
The committed nginx configuration still routes all requests to Node; no D routing is enabled.
The compose image uses latest, so A should align the deployed Node version with the pinned comparison
version before evaluating compatibility.

## Validation and evidence

156 tests passed, 0 failed/skipped. This handover step adds 10 internal-reader checks.
Build: 0 warnings, 0 errors. Format verification: exit 0.
Final evidence: docs/evidence/w2/w2-handover.trx.
Earlier milestone evidence remains unchanged in docs/evidence/w2.
Fixtures contain independently authored synthetic inputs and actual Node-generated output.

Node comparisons exercise real model/controller methods; some expanded media lookup,
HTTP request/response, socket and browser-user boundaries are simulated.
Progress/bookmark write fixtures include real Node SQLite save/reload behavior.
These tests do not establish full deployed Node/nginx/JWT parity.
Test totals differ between W1/W2/W3 and are not a measure of which workflow is better.

## Compatibility and integration limits

- The supported input subset is finite scalar progress/bookmark values and supported epoch/ISO dates,
  not arbitrary JavaScript object/date coercions. Unsupported paths return documented 400/501 responses.
- Payload identity fields cannot transfer progress ownership; this intentionally restricts Node's broad set(payload).
- Batch writes preserve earlier successful entries; returned item errors are skipped. Malformed entries are
  skipped under a documented boundary. Database exceptions still propagate and can produce HTTP 500.
- Node's loose bookmark lookup and strict numeric deletion quirk is retained. A string-time bookmark
  can remain after a DELETE returns OK.
- Unknown bookmark/extraData fields and stored time types are preserved in tested writes.
- No Node cache invalidation or user_updated socket bridge is implemented.
- Lock conflict/retry behavior and concurrent Node/.NET writes require coordinated handling before live cutover.
- D uses shadow users.bookmarks and users.updatedAt mappings. When B extends User, reconcile these shadows
  with B's properties rather than mapping the same column twice.
- D's keyless item/book/podcast projections must be reviewed with C's media mapping.
- No migrations, application raw SQL, schema ownership changes or production route changes were introduced.

## Group responsibilities and next steps

A/Luka: review merge/shared mapping decisions, align runtime versions and coordinate nginx cutover.
B: supply real JWT/user authentication integration and reconcile User mappings.
C: check media/progress-reader consumption, especially podcast first-row semantics.
D: resolve reviewer feedback and preserve compatibility/evidence boundaries.
E: validate the same HTTP scenarios through nginx against Node and .NET, then check shared-data behavior.
No message or pull request has been sent to other members by this task.

W2 uses human-directed increments with explicit review stops. Some task subdivision and suggested prompts
were supplied by the agent. The same conversation contains W1/W3 knowledge; sequential learning is a confound.
Time/cost are unavailable rather than estimated. A common W1/W2/W3 benchmark and report synthesis remain.
Self-review actions are excluded from the experiment log as requested by the operator.

No push, PR or merge has been performed. This local handover stops for human review.
