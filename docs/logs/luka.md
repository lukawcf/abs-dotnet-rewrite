# Luka task log


## R1. Database and models
- Task: R1, database layer research
- Workflow: W1 one-shot
- Agent and model: Claude Code subagent (general-purpose), Sonnet 5.5
- Operator: Luka (logged by the main Claude Code session; the subagent did not write to this file)
- Prompt: pasted from A-HANDBOOK.md, section P1 R1, with COMMON RULES. One change: the agent must not edit this log
- Time spent: not measured (ran in parallel with R2 and R3)
- Cost: not recorded
- Result: success. Static reading only, nothing run against a live database. Table names for libraries and libraryFolders are marked inferred in the file. Upstream clone is shallow (depth 1) and the deployed version is 2.37.1, so the live schema may differ
- Outcome: docs/research/db-notes.md. Not committed

## R2. LibraryController
- Task: R2, library endpoints research
- Workflow: W1 one-shot
- Agent and model: Claude Code subagent (general-purpose), Sonnet 5.5
- Operator: Luka (logged by the main Claude Code session)
- Prompt: pasted from A-HANDBOOK.md, section P1 R2, with COMMON RULES. One change: the agent must not edit this log
- Time spent: about 2.4 minutes of agent time
- Cost: about 98.5k subagent tokens
- Result: success. Static reading only. Not verified: podcast stats, where sortingIgnorePrefix is stored, ifAuthNeeded token handling. The agent made a batch of sed wording fixes and did not re-read the file; I checked the headings and length afterwards
- Outcome: docs/research/library-endpoints.md. Not committed

## R3. user_updated client handling
- Task: R3, client user_updated research
- Workflow: W1 one-shot
- Agent and model: Claude Code subagent (general-purpose), Sonnet 5.5
- Operator: Luka (logged by the main Claude Code session)
- Prompt: pasted from A-HANDBOOK.md, section P1 R3, with COMMON RULES. One change: the agent must not edit this log
- Time spent: about 1.2 minutes of agent time
- Cost: about 72.4k subagent tokens
- Result: success. Conclusions about what the user sees are inferred from code, not tested in a browser. Severity rated medium
- Outcome: docs/research/user-updated-impact.md. Not committed

## T1. WAL and cache check
- Task: T1, WAL and cache check
- Workflow: W1 one-shot
- Agent and model: Claude Code subagent (general-purpose), Sonnet 5.5; report rewritten by the main session
- Operator: Luka (logged by the main Claude Code session)
- Prompt: adapted from A-HANDBOOK.md, section P2. Changes: no credentials available, so the authenticated cache call was dropped; the agent was told not to change the journal mode
- Time spent: about 2.2 minutes of agent time, plus about 10 minutes for backup, Docker start and report rewrite
- Cost: about 67.7k subagent tokens
- Result: partial success. The experiment ran and its raw numbers check out against out_*.json. The subagent reported that docs/research/wal-and-cache.md was written, but the file did not exist. Root cause not found. The main session wrote the file from the raw results and re-verified the cache code citations. Authenticated cache behaviour was not tested live
- Outcome: docs/research/wal-and-cache.md, backup at G:\Project\abs-backup\config-20261008 (outside the repo). Not committed

## S1. EF Core scaffold assessment
- Task: S1, EF Core scaffold assessment
- Workflow: W1 one-shot
- Agent and model: Claude Code subagent (general-purpose), Sonnet 5.5
- Operator: Luka (logged by the main Claude Code session)
- Prompt: pasted from A-HANDBOOK.md, section P3, with COMMON RULES. Added: NUGET_PACKAGES set to G:\Project\abs-scratch\nuget to keep packages off C:
- Time spent: about 2.9 minutes of agent time
- Cost: about 91.8k subagent tokens
- Result: success. Scaffolding all six target tables fails (FK type mismatch: Guid? vs string, because the scaffolder infers types from row data). Recommendation: map users, mediaProgresses, libraryItems, books, libraries by hand. Mismatches with db-notes.md are listed in the file (series is lowercase, about 14 tables missing from the note). Not verified: bundled SQLite version vs GROUP_CONCAT ORDER BY, and a real EF query
- Outcome: docs/research/scaffold-assessment.md, scratch in G:\Project\abs-scratch. Not committed

## K1. .NET skeleton
- Task: K1, empty ASP.NET Core skeleton
- Workflow: W1 one-shot (done directly by the main session, not a subagent)
- Agent and model: Claude Code, Sonnet 5.5
- Operator: Luka
- Prompt: pasted from A-HANDBOOK.md, section P4 K1
- Time spent: about 15 minutes
- Cost: not recorded
- Result: success. dotnet build 0 warnings, dotnet test 1/1 passed, dotnet format --verify-no-changes clean (after auto-fixing a missing final newline in the template GlobalUsings.cs), docker-compose up -d --build gives three services Up, /health returns 200 from inside the compose network, nginx unchanged (no diff) and localhost:8080 still serves audiobookshelf. Deviation from the task text: the compose build context is the repo root with dockerfile src/Abs.Api/Dockerfile, because Abs.Api references Abs.Data. Config is mounted read-only (./config:/config:ro), see the caveat in wal-and-cache.md section 4
- Outcome: AbsRewrite.sln, src/Abs.Api, src/Abs.Data, tests/Abs.Api.Tests, .editorconfig, .dockerignore, docker-compose.yml. Not committed

## K2. Template slice: GET /api/libraries and /api/libraries/:id
- Task: K2, template slice, AGENTS.md, shared interfaces, dev fake login
- Workflow: W1 one-shot (done directly by the main session; DEV.md did not exist, so the task ran from the research notes and the upstream code)
- Agent and model: Claude Code, Sonnet 5.5
- Operator: Luka
- Prompt: pasted from A-HANDBOOK.md, section P5 K2
- Time spent: about 40 minutes
- Cost: not recorded
- Result: success with deviations. dotnet build 0 warnings, dotnet test 18/18 passed, dotnet format --verify-no-changes clean. The Debug service run against a database copy returned a body byte-identical to Node (873 bytes). The Release container ignores the dev header (401). Deviations: (1) the Node comparison used Node's own Library.toOldJSON in a throwaway container on a database copy, not a recorded HTTP response, because no token was available; (2) IPermissionService has a real implementation (PermissionService) and a minimal User entity, because the controller needs one at runtime; the task said interface plus fake only; (3) include=stats and include=filterdata return 501; (4) routes are /api/libraries because nginx passes the path through. Two shell heredoc commands failed to parse (apostrophes in the text) and ran nothing; the files were then written with the Write tool
- Outcome: src/Abs.Data (entities, configurations, AbsDateConverter), src/Abs.Api (controller, service, DTOs, auth, abstractions), tests/Abs.Api.Tests, AGENTS.md, docs/research/k2-shape-comparison.md. Not committed

## M1. Merge PR #2 (D module) into main
- Task: merge Dajiben666's draft PR #2 after PR #1 and #3 had landed
- Workflow: W2 small steps (merge, run tests, find cause, fix, rerun)
- Agent and model: Claude Code, Sonnet 5.5
- Operator: Luka
- Prompt: chat requests "merge it" and "let us finish it"; no saved prompt file
- Time spent: about 45 minutes
- Cost: not recorded
- Result: success with caveats. The PR had one conflict (Program.cs, service registrations: kept both sides). After the merge 234 of 235 tests failed because both modules' fixtures created libraryItems and books; after removing the duplicate DDL 18 Library Items tests still failed because the W2 bookmark seed moves three items into library A (6 expected, 9 found). Fixed by lazy seeding in AbsApiFactory with an IncludeW2Fixtures flag that the Library Items tests turn off. Final: build 0 warnings, 235/235 tests pass. The PR was a draft and its author wrote that no merge was requested; the merge was done on the operator's instruction and the author has not been asked. dotnet format shows only ENDOFLINE errors on this machine (core.autocrlf=true; the index holds LF). The auto-mode safety check blocked the first merge click and later a git checkout; after the operator repeated the instruction the click went through
- Outcome: merge commit 0fc4c71 on main; fix commit b51ba80 pushed to feat/progress-bookmarks-integration

## D1. Planning documents
- Task: D1, ownership, known limitations, entity notes
- Workflow: W1 one-shot (main session)
- Agent and model: Claude Code, Sonnet 5.5
- Operator: Luka
- Prompt: pasted from A-HANDBOOK.md, section P6 D1; operator then asked for everything to be finished without further input
- Time spent: about 25 minutes
- Cost: not recorded
- Result: partial. docs/endpoint-ownership.md, docs/known-limitations.md, docs/entity-notes.md written from the research files, the code on main and upstream route lines. DEV.md does not exist in the repository, so (1) scope and roles were taken from PR titles and logs and marked inferred, and (2) the "Open items" update was written to docs/open-items.md instead. Two statements were wrong on the first draft (which table the continue-listening route writes; what B added to the User entity) and were corrected after checking the source. AGENTS.md had been deleted from main by commit a904758 and was restored from ebdc009, with the authentication section updated
- Outcome: docs/endpoint-ownership.md, docs/known-limitations.md, docs/entity-notes.md, docs/open-items.md, AGENTS.md. See the commit that follows
