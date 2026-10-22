# Implementation roadmap
M0 **Foundation** (2–3 weeks indicative): Unity project, assembly boundaries, content loader, validator, tests, CI, input/scene skeleton, local save.
M1 **Office Escape vertical slice** (4–6 weeks): first 3 puzzles, Tí/Bốp/Vượng/Cộc, two scenes, dialogue, consequence callback, UI, VI/EN, save/resume, device playtest. STOP/GO gate.
M2 **Core systems hardening** (3–4 weeks): inventory combinations, predicate/effect DSL, NPC state, Yarn, replay, hints, migrations, analytics contract.
M3 **Chapters 1–2** (6–10 weeks): production art, full content, QA, localization.
M4 **Chapters 3–5** (10–16 weeks): remaining puzzles, narrative branching, 4 endings, asset/audio production.
M5 **Monetization & polish** (4–6 weeks): AdMob/IAP sandbox, privacy, accessibility, profiling, device matrix.
M6 **Beta/release** (3–5 weeks): store listing, age/privacy review, localization, crash monitoring, store approval.

These are rough planning estimates, not commitments; AI coding does not remove narrative/art/QA bottlenecks. Parallel art/audio may shorten calendar time but increases review overhead.

## CI/CD
GitHub Actions: format/lint, content validator, Unity EditMode/PlayMode (runner/license), Android development build, iOS build on macOS runner, artifact signing only in secured secrets, beta TestFlight/Play internal track after human approval. Never commit keystores, certificates, API keys, store credentials.

## Release checklist
Title/trademark and store-name clearance; icon/screenshots/trailer; privacy policy, consent and SDK declarations; age rating; VI/EN review; restore purchases; offline mode; crash-free sessions; hardware matrix; content licensing; accessibility; user support; store human approval.
