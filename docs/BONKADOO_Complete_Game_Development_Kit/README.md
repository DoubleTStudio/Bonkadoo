# BONKADOO! — Full Game Development Kit
Version 1.0 design baseline | status: **specification, not finished game** | working title, brand clearance pending.
Vietnamese title: **NGƯỢC ĐỜI**.

A five-chapter, offline-first, 2D systemic comedy adventure for Android and iOS. Unity 6.3 LTS/C#, URP 2D, Yarn Spinner, localized Vietnamese/English. Free entire game, modest banner advertising, interstitial only at natural breaks after >=300 seconds of eligible gameplay, and a one-time approximately US$5 remove-ads purchase. No paid chapters, hints or endings.

**Package includes:** product, narrative, technical, art, audio, UX, monetization, QA, release specifications; complete chapter and puzzle *design catalog* (45 puzzles); machine-readable puzzle content, JSON Schema, event catalog, story beats, localization seed, implementation backlog, test plans, agent skills and rules, validation script.

**Important:** Content entries are implementable design seeds, not fully produced game assets or finished cinematic dialogue. Final performance, pacing, puzzle fun, translations, age rating, store acceptance, title availability and purchase pricing require human/device/store validation. Do not mark a chapter complete based on seed content alone.

## Agent quick start
1. Read `AGENTS.md`, `docs/01-product/GDD.md`, `docs/03-technical/ARCHITECTURE.md`, `docs/08-delivery/ROADMAP.md`.
2. Run `python3 tools/validate_content.py`.
3. Implement M0 architecture, M1 vertical slice and test on real devices; expand chapters only after gate.
4. Work through `tasks/backlog.csv` in dependency order; read the matching skill.
5. Record any new assumptions in `docs/DECISIONS.md` and use ADRs for architectural changes.

## Definitions
- **Specified**: authored content and acceptance criteria exist.
- **Implemented**: runnable Unity behavior with tests and localized content.
- **Production-ready**: reviewed art/audio, playtesting, performance, accessibility, store and legal sign-off.
