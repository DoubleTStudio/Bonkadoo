---
name: unity-architecture
description: Build assembly separation and Unity project setup
---
# unity-architecture
## Trigger
Use when a task involves: build assembly separation and unity project setup.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read ARCHITECTURE; create asmdefs; isolate domain; setup Input System/URP 2D; add CI smoke checks.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Domain tests run without Unity engine references.
