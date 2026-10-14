# W2 step 4 — Bookmark reads for human review

## Scope

Human prompt: “继续下一项”. The agent announced a read-only bookmark step before coding.
Implemented GET /api/me/bookmarks and GET /api/me/bookmarks/{libraryItemId}.
The second endpoint returns a list for an item, not one bookmark selected by its time.
Bookmark creation/update/deletion remain outside this step.

Reference: Node v2.37.1 commit 3563d49424d50170324de9236a24c591b6d965e9.
MeController.js:123-146; ApiRouter.js:177-178; User.js:643-682 and canAccessExplicitContent getter.
No production routing or authentication changes.

## Behavior

The all-bookmarks endpoint returns only the authenticated user's stored bookmarks.
It does not filter them by item access, matching Node, and retains orphan item references.
The item endpoint checks existence first (404 Not Found), then library, explicit-content
and tag access (403 Forbidden), and returns that user's matching bookmarks.
Explicit content requires both accessExplicitContent and an active user.
Tag allowlists need at least one match; denylists reject any selected-tag match and allow
untagged media. accessAllTags bypasses tag filtering. Libraries use IPermissionService.

Stored JSON property order, numeric/string times, nullable titles and unknown fields are
preserved. SQL NULL bookmarks become an empty list. Reads do not change stored data.

User.cs and UserConfiguration.cs, owned by Auth, remain unchanged. D adds a shadow mapping
for users.bookmarks, read-only media projections and a service/DTO/controller.
Existing D-owned item/book projections gain only libraryId, explicit and tags.
The application uses LINQ/EF and does not create or modify schema.

## Validation

105 tests passed, 0 failed/skipped, including 20 new checks.
Seventeen fixtures come from real Node bookmark controller methods and real User permission
methods, using synthetic user data and simulated expanded-item lookup.
They cover both endpoints, separate users, NULL/empty bookmarks, unknown fields and string
times, missing items, library denial, allow/deny tag rules, untagged items, explicit access,
inactive explicit access and podcast items.
Two tests reject anonymous requests; one confirms stored bookmark JSON is unchanged.

This is method-level Node evidence, not a full Node HTTP/database/authentication test.
The .NET endpoints run through AbsApiFactory with synthetic SQLite and development auth.
Build has zero warnings/errors. Format verification exits 0.
Evidence: docs/evidence/w2/w2-step4.trx; Fixtures/w2-node-bookmark-reads.json.

## Boundaries and next review

The stored-data contract assumes bookmark arrays contain JSON objects and permissions
use normal JSON boolean flags/string tag arrays. Arbitrary corrupt JSON or legacy scalar
permission coercions are not proven compatible.
An absent book/podcast behind an item or unknown media type currently returns 404.
No live route cutover is supported by this step; production integration limitations remain.

Review the response shape and permission rules before directing bookmark writes.
Implementation stops here for human review. Existing write configuration remains default-off.
