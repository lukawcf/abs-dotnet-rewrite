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
