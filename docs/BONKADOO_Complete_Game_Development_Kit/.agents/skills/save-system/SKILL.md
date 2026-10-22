---
name: save-system
description: Implement robust local save and replay
---
# save-system
## Trigger
Use when a task involves: implement robust local save and replay.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read ARCHITECTURE; versioned snapshots, journal, checksum, atomic write, migration, branch replay; corruption tests.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Idempotent resume and recovery on interrupted writes.
