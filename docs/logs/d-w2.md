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
