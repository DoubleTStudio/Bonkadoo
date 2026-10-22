# Quality and acceptance plan
## Test layers
EditMode: predicates/effects, deterministic rule order, event idempotency, inventory invariants, save migration, AdPolicy. PlayMode: touch->rule->feedback->scene, Yarn dialogue state, safe area and inventory. Content validation: IDs, all referenced rule IDs, two solutions, localization parity, scene/event consistency. Device E2E: iOS and Android physical hardware, cold/warm start, offline, background/restore, thermal, accessibility and IAP sandbox.

## Coverage targets
Domain rule branches >=90% line and all invariants/property tests; every puzzle has tests for each authored solution and at least one failure path. All endings reachable via deterministic fixtures. CI fails on content validator errors, missing localization, failed EditMode/PlayMode, or packaging regressions.

## Playtest gate
Prototype: 15–20 external players; observe first 30s, time to solve, confusion, laughter, creative attempts, understanding of consequences, desire to continue. Chapter gate: no recurring softlock, puzzle fairness, comprehension and accessibility. Targets are hypotheses; capture actual findings and revise content.

## Definition of done
Implemented, tested, reviewed on devices, translated, save compatible, documented, telemetry safe, asset licenses confirmed. Report evidence and known limitations. No 'done' based solely on generated code or mock screenshot.
