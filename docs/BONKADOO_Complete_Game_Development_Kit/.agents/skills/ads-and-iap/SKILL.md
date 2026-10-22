---
name: ads-and-iap
description: Implement free-game ad/IAP contract
---
# ads-and-iap
## Trigger
Use when a task involves: implement free-game ad/iap contract.
## Inputs
`AGENTS.md`, relevant `docs/` contracts, `tasks/backlog.csv`, and content/assets referenced by task.
## Workflow
1. Read ADS_AND_IAP; implement consent-aware ads, eligibility policy, one-time remove ads, restore and offline behavior; sandbox test.
2. Create or modify source/content with stable IDs and no unreviewed external dependencies.
3. Add tests and run validation; capture actual commands and results.
4. Review localization, save compatibility, mobile performance and content license impacts.
5. Update task status only when evidence satisfies acceptance criteria.
## Output
Changed files, tests and evidence, open issues, handoff note.
## Definition of Done
No forced ad interruptions; Remove Ads disables all ads.
