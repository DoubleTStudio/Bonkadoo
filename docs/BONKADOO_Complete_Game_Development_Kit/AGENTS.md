# BONKADOO Agent Contract (Codex and Claude)
## Authority
1. `docs/DECISIONS.md`, `docs/01-product/GDD.md`
2. `docs/03-technical/ARCHITECTURE.md`, schemas and monetization rules
3. Chapter outlines and puzzle catalog
4. Task acceptance criteria
5. Explicitly recorded reversible assumptions

## Mandatory rules
- No mandatory backend, account, always-online checks, paywall, paid chapters, energy, randomized purchases, rewarded-ad-locked hints or content.
- Never embed puzzle rules, branching, or user-facing strings directly in MonoBehaviours. Use data-driven rules and localization keys.
- Domain C# must not depend on Unity APIs. All state-changing actions emit idempotent events.
- No arbitrary expression evaluation from content; use allowlisted predicate/effect DSL.
- Every feature: tests, documentation, localized VI/EN copy, error handling, mobile profiling consideration, save compatibility.
- Never pretend a test, device run, store submission or asset review succeeded; report actual evidence.
- Never use unlicensed external assets, copied IP, private keys or credentials in repo.
- Only display interstitial at an eligible natural transition after >=300 seconds of active gameplay, with first-session grace, frequency cap and Remove Ads override. Never interrupt dialogue, puzzle, cutscene or active interaction.
- For ambiguous reversible choices, choose the simplest compliant behavior, document assumption and proceed. Ask humans for legal/licensing, credentials, production spending, brand and irreversible publishing decisions.
- Every agent handoff: goal, files changed, contract references, tests/outputs, risks, next task.

## Completion gates
No task DONE without runnable evidence for its acceptance criteria. No chapter DONE without all puzzle paths tested, content localized, all endings reachable where applicable, assets reviewed and device QA.
