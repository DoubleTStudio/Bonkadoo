---
name: puzzle-authoring
description: Turn puzzle seeds into fully playable scene content
---
# puzzle-authoring
## Trigger
Use when a task involves: turn puzzle seeds into fully playable scene content.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read puzzle JSON, chapter outline and GDD; create hotspot graph, discoverable clues, two genuinely different strategies, localized feedback, hints, NPC callbacks; record authored tests.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Both solution routes and failure paths work on device; no softlock.
