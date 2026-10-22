# Agent execution playbook
## Task loop
Read AGENTS -> select first unblocked TODO in backlog -> read skill and relevant docs -> implement smallest testable change -> run validator/tests/build -> record evidence -> peer/agent review -> update task -> next task.

## Parallel agents
One agent owns domain contracts, one content, one art/audio, one QA. Do not concurrently modify same scene/puzzle JSON without coordination. Content merges require validator and localization parity. Code changes to DSL require an ADR and content migration.

## Definition of ready
Requirement linked; source files and IDs known; test oracle specified; assets/licensing dependencies listed; no unresolved architectural blocker.

## Definition of done
Acceptance criteria met with real evidence; automated tests pass; VI/EN keys complete; save migrations assessed; mobile UI/performance reviewed; documented limitations.

## Review rubric
Functional correctness; rule determinism; puzzle fairness; comedy payoff; story continuity; accessibility; mobile performance; licensing/privacy; monetization contract. Reject superficial test coverage and nonfunctional placeholders.

## Escalation
Ask human for branding/trademark, production spending, app accounts/credentials, irreversible store publication, third-party licensing and final narrative/art approvals. For other choices, record reversible assumption in DECISIONS and proceed.
