---
name: content-validation
description: Validate schemas and references
---
# content-validation
## Trigger
Use when a task involves: validate schemas and references.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read schemas and JSON; enforce IDs, localized keys, rule references, event catalog, alternate solutions and tests.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
Validator fails invalid fixtures and passes catalog.
