---
name: gameplay-system
description: Implement deterministic gameplay state transitions
---
# gameplay-system
## Trigger
Use when a task involves: implement deterministic gameplay state transitions.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read DATA_CONTRACTS and INTERACTION_IMPLEMENTATION_NOTES; implement allowlisted DSL, atomic effect validation, ordered resolution, NPC dispositions and inventory; property-test idempotency.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Each interaction is deterministic and emits expected events.
