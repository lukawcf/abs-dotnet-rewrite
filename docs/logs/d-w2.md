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
