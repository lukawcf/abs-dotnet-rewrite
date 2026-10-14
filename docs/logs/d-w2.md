# D W2 experiment log

## 2026-10-13 — Setup and human task selection

- Task: start W2 for playback progress and bookmarks. Exact human prompt: “做w2”.
- Workflow: W2 small steps, directed and reviewed by the human between implementation tasks.
- Agent: Codex; exact model identifier unavailable. Operator: D member.
- Branch: feat/progress-bookmarks-w2-human-guided. Baseline: ebdc009, matching W1/W3.
- Context: repository AGENTS.md, original source and team assignment. The same conversation already contains W1/W3 findings; prior knowledge is a confound, not a context-free independent run.
- No W1/W3 implementation or test fixtures copied. No D feature implemented during setup.
- Human selects the first task, then reviews its result before directing the next task. Suggested first task: progress read endpoints only.
- Baseline verification: sandbox test invocation failed with exit code 1 and no actionable compiler error; verbose retry also failed without diagnostics. Re-running outside the sandbox produced build output and started tests; final result recorded separately below.
- Time/cost: not measured; unavailable. Keep the actual chat instructions and review feedback for the report.
- Outcome: experiment branch prepared; human task selection pending. No push, PR or nginx changes.
- Baseline result: 18 passed, 0 failed, 0 skipped. Evidence: docs/evidence/w2/w2-baseline.trx. The successful run outside the sandbox indicates an execution-environment issue; its precise cause is unconfirmed.

## 2026-10-13 — Step 1: human-directed progress reads

- Exact human prompt: “先实现查询播放进度的接口，核对原 Node.js 行为并测试，完成后让我检查，暂时不要做写入和书签功能”.
- Workflow: W2; agent Codex, exact model unavailable; operator D member. Prior W1/W3 knowledge remains disclosed.
- Result: implemented progress list and item/episode reads with D-owned EF mapping, DTO, service and controller.
  Shared changes are DI registration and adding synthetic fixture files to the existing test factory.
- Independently authored Node recorder executes real pinned model/controller methods; no W1/W3 fixtures copied.
- Setup issue: Python 3.11's SQLite could not parse the seed's ORDER-containing trigger schema.
  Bundled Python read the DDL successfully. Initial inline Python also failed from PowerShell quoting;
  replaced with a saved script. Neither issue changed source schema or test expectations.
- Validation: 29 passed (18 existing + 11 new), 0 failed/skipped; build zero warnings/errors;
  formatting verification passed. Evidence: docs/evidence/w2/w2-step1.trx.
- Limitations and review notes: docs/research/d-w2-step1-review.md. No real Node HTTP/JWT/nginx validation.
- No writes/bookmarks, nginx changes, push or PR. Awaiting human review; no subsequent task selected.
- Time/cost: unavailable; no measured duration or monetary estimate claimed.
- Outcome: step 1 implementation ready for review; commit identifiable by this log's Git history.

## 2026-10-13 — Step 2: single progress creation/update

- Human accepted the query implementation: “查询部分通过”.
- Exact prompt: “查询部分通过。接下来实现单项播放进度的创建和更新，核对 Node.js 的部分更新、完成状态和时间戳行为，补充测试。写入默认关闭，完成后让我检查，暂时不要做批量更新、删除和书签。”
- Workflow W2; agent Codex, exact model unavailable; operator D member. Prior W1/W3 knowledge remains disclosed.
- Independently implemented single PATCH, D-owned keyless media lookups, finite scalar conversion,
  partial update/completion/time transitions and explicit default-off write gate. No W1/W3 code copied.
- Node recorder executes actual User creation/update and MediaProgress persistence; media lookups simulated.
- Recorder first failed because its synthetic users table had not been initialized; initialized that test-only FK parent.
- First .NET run: 56 failed, 1 passed from missing podcasts FK parent in fixture setup.
  Extracted actual podcasts DDL and seeded the parent; second run: 57 passed.
- Review found Node's forced extraData dirty flag matters even when progress has the same stored value;
  implemented explicit EF property modification and added a regression case, not a failing test suppression.
- Expanded final run: 63 passed, 0 failed/skipped. Build zero warnings/errors; format verify exit 0.
- Raw failure/final TRX retained in docs/evidence/w2. Review artifact: docs/research/d-w2-step2-review.md.
- Intentional input boundaries and unverified cache/socket/JWT/nginx/concurrency behavior are disclosed there.
- No batch/delete/bookmarks, live write enablement, nginx routing, push or PR.
- Time/cost: not measured, unavailable. Outcome: ready for human review; this entry's Git commit identifies the result.

## 2026-10-13 — Step 3: batch update and delete

- Exact human prompt: “实现播放进度批量更新和删除，核对 Node.js 的成功、失败及用户数据隔离行为，补充测试。写入保持默认关闭，完成后让我检查，暂时不要做书签。”
- Workflow W2; agent Codex, exact model unavailable; operator D member.
- Implemented sequential batch upserts, returned-error skipping and ownership-filtered atomic deletion.
  Default-off write gate retained. No bookmarks or routing changes.
- Independently authored Node recorder exercises real batch/delete controllers and persisted model methods;
  media lookups and socket boundary are simulated. No W1/W3 fixtures copied.
- First test run for this step: 83 passed, 0 failed/skipped. Includes 18 added checks.
  Updated the prior batch-not-implemented test to assert non-array rejection under the new endpoint.
- Build zero warnings/errors; format verification exit 0. No fixes from failing tests were required this step.
- Evidence: docs/evidence/w2/w2-step3.trx. Review: docs/research/d-w2-step3-review.md.
- Time/cost unavailable; no measured duration or estimate claimed.
- Outcome: ready for human review; this entry's Git commit identifies the result. No push or PR.

## 2026-10-13 — Step 4: bookmark reads

- Exact human prompt: “继续下一项”. Agent announced bookmark reads only, with a review stop before bookmark writes.
- Workflow W2; agent Codex, exact model unavailable; operator D member. Scope subdivision was agent-selected under the human continuation instruction.
- Implemented all-bookmark and per-library-item bookmark lists; latter checks library, tags and explicit access.
- D-owned shadow mapping reads users.bookmarks without changing Auth-owned User files. No schema changes or application SQL.
- Independently authored Node recorder uses real bookmark controller and User permission methods with simulated expanded-item lookup.
- First tests: 105 passed, 0 failed/skipped, including 20 new checks. Build zero warnings/errors; format verification exit 0.
- Evidence: docs/evidence/w2/w2-step4.trx. Review: docs/research/d-w2-step4-review.md.
- No bookmark writes, nginx/auth changes, push or PR. Existing writes remain default-off.
- Time/cost unavailable. Outcome: ready for human review; this entry's Git commit identifies the result.

## 2026-10-13 — Step 5: bookmark creation/update/deletion

- Exact human prompt: “下一步”. Agent announced bookmark writes, default-off gate and a human review stop.
- Workflow W2; agent Codex, exact model unavailable; operator D member.
- Implemented POST/PATCH/DELETE, source-compatible error ordering and permission checks, scalar time matching,
  duplicate handling and Node's loose-find/strict-delete quirk. D-owned shadow updatedAt mapping added.
- Independently authored recorder executes real Node controllers/User methods with SQLite persistence;
  expanded-item lookup, socket boundary and browser serialization are simulated. No W1/W3 code/fixtures copied.
- First test run: 142 passed, 0 failed/skipped, including 37 added checks.
- Build zero warnings/errors; format verification exit 0.
- Evidence: docs/evidence/w2/w2-step5.trx. Review: docs/research/d-w2-step5-review.md.
- No production writes, auth/nginx changes, push or PR. Time/cost not measured, unavailable.
- Outcome: bookmark writes ready for human review; this entry's Git commit identifies the result.

## 2026-10-13 — Step 6: internal reader and handover

- Exact human prompt: “下一步”, following discussion of independently implementing IUserProgressReader and final handover.
- Workflow W2; agent Codex, exact model unavailable; operator D member.
- PlaybackProgressService now implements the existing IUserProgressReader contract and resolves to the same scoped DI instance.
  Reuses current item/user lookup; numeric internal projection and nullable time defaults are documented.
- Added 10 checks: stored progress, numeric-string/null projection, missing/other users' rows, scoped DI, fresh reads and cancellation.
- First run: 156 passed, 0 failed/skipped. Build zero warnings/errors; format verification exit 0.
- Evidence: docs/evidence/w2/w2-handover.trx. Handover: docs/research/d-w2-handover.md.
- No shared interface signature changes, nginx/auth changes, production write enablement, push, PR or merge.
- Time/cost not measured, unavailable. Outcome: local W2 handover ready for human review; this entry's Git commit identifies the result.
