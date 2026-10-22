---
name: release-management
description: Prepare signed mobile release and store compliance
---
# release-management
## Trigger
Use when a task involves: prepare signed mobile release and store compliance.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read ROADMAP; CI signing secrets, beta builds, privacy/age/store listing, brand review and release gate.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Human-approved TestFlight/Play beta and complete release checklist.
