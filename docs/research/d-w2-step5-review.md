# W2 step 5 — Bookmark creation, update and deletion review

## Scope and source

Human prompt: “下一步”. Agent announced bookmark writes with a review stop.
Implemented:
- POST /api/me/item/{id}/bookmark
- PATCH /api/me/item/{id}/bookmark
- DELETE /api/me/item/{id}/bookmark/{time}

Node reference: v2.37.1, commit 3563d49424d50170324de9236a24c591b6d965e9.
MeController.js:327-435; User.js:841-918.
The existing ProgressWrites:Enabled gate covers all D writes and remains false.
No live database writes, schema changes, nginx routing or authentication changes.

## Behavior for review

Existence and library/tag/explicit access are checked before validating payloads.
Missing items return 404 Not Found; denied access returns 403 Forbidden.
POST/PATCH require a non-null supported numeric-coercible time and a nonempty string title.
Invalid values return 400 Invalid time or Invalid title.
Time scalar types are preserved, including numeric strings and booleans in the tested cases.

POST creates a bookmark in Node property order with a generated millisecond createdAt.
If a bookmark at that time exists, it returns the existing object and changes only its title
when necessary. A duplicate with unchanged title does not save or refresh user updatedAt.
PATCH returns 404 for an absent bookmark; existing bookmarks retain their createdAt,
time type and unknown JSON fields. PATCH forces persistence and user updatedAt refresh,
even when the supplied title is identical.

Find uses Node's loose equality for supported scalar times. Two strings compare as strings;
a numeric/string pair compares numerically. DELETE converts its route time to a number,
then uses strict equality for removal. As in Node, a stored string time may match the initial
lookup and produce 200 OK while remaining in the array. This reproduces the source quirk.

An EF transaction wraps JSON read/modify/write. Only the authenticated user's bookmarks
and updatedAt shadow properties are modified. Auth-owned User files remain unchanged.
This does not establish cross-process cache consistency or concurrency safety with Node.

## Validation

142 tests passed, 0 failed/skipped. This step adds 37 checks:
31 compare actual Node controller/User methods and real SQLite persistence against .NET
responses and every synthetic bookmark user's stored JSON/updatedAt.
Cases include new numeric/string/zero/negative/boolean times, unchanged/changed duplicates,
distinct numeric strings, PATCH to numeric/string stored times, missing bookmarks, DELETE's
string-time quirk, invalid times/titles, error precedence, permission failures, explicit access,
podcast creation and another user's isolated update.
Six checks verify default-off and anonymous POST/PATCH/DELETE leave all users unchanged.

Generated timestamps alone are normalized and checked against the .NET request window.
All other compared response properties, stored bookmarks and updatedAt values remain exact.
Unknown fields/property order and other users' data are included in the comparisons.
Node uses actual User models and extracted real users DDL. Expanded media lookup, socket
emission and browser-user serialization are simulated. This is not full Node HTTP/auth/cache
integration evidence.

Evidence: Fixtures/w2-node-bookmark-writes.json; docs/evidence/w2/w2-step5.trx.
Build: zero warnings/errors. Format verification: exit code 0.

## Boundaries

Numeric input support is limited to finite scalar values handled by ProgressPatchValues;
arbitrary arrays/objects, non-finite numbers and every JavaScript string coercion are not
claimed compatible. Unsupported time inputs receive 400.
Writes to NULL/non-array bookmark storage return 501 Unsupported bookmark data instead
of reproducing a Node runtime error; valid stored arrays are the intended contract.
Malformed request bodies may be rejected by ASP.NET before controller execution.
No socket/cache invalidation bridge is added, and production integration remains pending.

This completes the local playback-progress/bookmark endpoint implementations for W2.
It does not complete live integration or the common W1/W2/W3 evaluation.
Shared IUserProgressReader handover integration is still outside the implemented endpoint tasks.
Review this step before proceeding to final handover or workflow comparison.
