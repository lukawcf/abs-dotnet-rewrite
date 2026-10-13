# D integration candidate

Branch: feat/progress-bookmarks-integration. Selected from W2 and integrated with main at 91109bd.
This document supersedes the baseline-only runtime notes in d-w2-handover.md for this branch.
W1/W2/W3 experiment branches and historical evidence remain separate.

The candidate implements the ten progress/bookmark route patterns listed in d-w2-handover.md
and IUserProgressReader. Six common-suite coercion mismatches have been corrected:
numeric 0/1 and exact true/false strings are supported for the two progress flags.
Creation uses JavaScript truthiness; existing assignments separately follow Sequelize BOOLEAN
sanitization. This distinction reproduces the tested isFinished="false" transition.
Other arbitrary string/object flag coercions remain outside the implemented subset.

Integration retains the upstream UserService/JWT handler and existing upstream nginx routes.
D's bookmark updatedAt shadow mapping was replaced with Auth's real User.UpdatedAt.
CreatedAt/UpdatedAt use explicit camelCase columns and AbsDateConverter for Sequelize date text.
No D route was enabled in nginx and writes remain false/Mode=ReadOnly by default.
The only upstream authentication-source difference is using-directive ordering for formatting.

Validation:
- 174 application tests passed, no failures/skips.
- 118/118 common comparison checks passed on the integrated candidate.
- Production HS256 bearer tests cover progress, bookmarks and /api/me reads with active synthetic users;
  expired/wrong-signature/missing-user/inactive/unknown-user cases return 401.
- Tests generate fresh random test keys, not real credentials.
- Full solution format verification passes; test compilation succeeds.

These checks are synthetic SQLite and TestServer integration, not a deployed nginx/Node/JWT system test.
Node cache invalidation/socket emission is still absent. Lock conflict handling can return 500.
The upstream /api/me UserDto still has placeholder mediaProgress/bookmarks; populating that
browser-user payload requires coordination with B and is not solved by the separate D routes.
No live write cutover should occur before shared-data/cache/concurrency integration is addressed.

Historical experiment logs and milestone TRX files have not been rewritten or supplemented with
self-review actions. The common-suite comparison results live outside the repository.
The PR should remain draft for group integration review; no merge is performed by this task.
