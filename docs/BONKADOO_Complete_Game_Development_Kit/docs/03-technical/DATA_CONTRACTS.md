# Content and data contracts
Source of truth: `content/puzzles/puzzles.json` and `schemas/puzzle.schema.json`. Each puzzle has stable ID, chapter, scene, goal, hotspots, prerequisites, at least two solutions (unless explicit finale set-piece), rule definitions, failure feedback, hints, consequence events, callback keys and test cases. Solutions are paths of input actions and expected effects, not just prose descriptions.

**Naming:** `ch01.p01`, `scene.ch01.office`, `item.ch01.termination_notice`, `evt.ch01.p01.solved.stamp`, `dlg.ch01.p01.hint1`. IDs never reused. A removed content ID must be mapped in a migration.

**Rule resolution:** only allowlisted predicate/effect DSL, first matching rule wins; prevalidate effects; commit all or none; event journal stores causationId. Unknown action yields fallback feedback. Branch flags are explicit.

**Localization:** all user-facing text keys must exist in `content/localization/vi.json` and `en.json`; language switch works without restart. Seed text is placeholder editorial copy and must be reviewed for jokes and natural language.

**Save contract:** saveVersion, contentVersion, localProfileId, chapterId, sceneId, checkpointId, worldState, eventJournal, journalCursor, checksum. Recovery tests for interrupted writes, migrations and event replay.
