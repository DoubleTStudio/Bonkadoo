---
name: automated-testing
description: Implement QA and device checks
---
# automated-testing
## Trigger
Use when a task involves: implement qa and device checks.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read TEST_STRATEGY; EditMode, PlayMode, deterministic fixtures, performance, offline and accessibility; log evidence.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Tests and actual device runs documented.
