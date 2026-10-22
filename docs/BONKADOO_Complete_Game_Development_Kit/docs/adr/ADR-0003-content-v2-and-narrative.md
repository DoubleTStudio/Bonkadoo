# ADR-0003 Content format v2, authored chapters, Yarn adapter
Status: accepted (M1–M3)

## Context
The v1 seeds solved every puzzle with an option alias (`input.<pid>.choice`), shared one success line, never moved
meters and had no scenes, items, dialogue or endings (GAP_REPORT C1–C12). INTERACTION_IMPLEMENTATION_NOTES required
real interaction traces before claiming puzzle implementation.

## Decision
1. **Authoring source:** `tools/author/chapters/chNN.py` (Python DSL in `tools/author/bonk.py`) compiled by
   `tools/author/build.py` into `content/` (puzzles, events, scenes, ambient rules, items, dialogue speakers,
   `.yarn`, VI/EN). Generated files are committed; CI fails if they drift from the specs (`build.py --check`).
   Original seeds are frozen in `content/seed/puzzles_seed_v1.json`; every puzzle keeps its id, title, goal,
   twist, featured NPC and seed strategy idea.
2. **Puzzle steps:** each route is 2–3 steps compiled to rules gated by the previous step flag
   (`flag.<pid>.<route><n>`), matching `requestVerb/requestTarget/requestSource` (ADR-0002). The final step
   rule keeps the v1 id `rule.<pid>.<route>` and event `evt.<pid>.solved.<route>`. `input.*` remains supported
   but unused.
3. **DSL additions (allowlisted):** `queueDialogue.speaker` (optional), `startConversation {node}`, verb `Enter`
   (sent by navigation on scene entry so content can react to arrival).
4. **Ambient rules** (`content/scenes/ambient.json`): look lines (every touch gives feedback), NPC talk lines with
   memory conditions (method-dependent callbacks), scene-enter conversations, dialogue-choice rules, ending rules.
5. **Scenes:** normalized stage coordinates, hotspot kinds Prop/Npc/Exit, `visibleWhen` predicates, exits locked
   until `unlockExit` or `startUnlocked`. Navigation is an Application concern (InteractionController).
6. **Yarn Spinner 3.2.8** (`dev.yarnspinner.unity`, MIT, git tag pin). Dialogue is compiled at runtime from the
   synced `.yarn` text with Yarn's compiler; lines are looked up by `#line:` id in our VI/EN tables (`yarn.*`),
   not Yarn's string tables. Yarn reads state only through registered functions (`metric`, `has_event`, `flag`,
   `flag_text`, `npc`); `<<act id>>` turns a choice into a normal engine request handled by content rules.
   C# owns all authoritative state (D03).
7. **Endings:** chapter 5 doors set `flag.ending`; gates follow ENDING_MATRIX with correctives
   (`bonk.ending_rules`). p09 sets `flag.game.complete` and plays `ending_epilogue`.

## Consequences
Python simulator (`simulate.py`) and C# `FullGameRouteTests` both play every route through the whole game.
DSL changes still require an ADR and regenerated content. Copy is draft and needs editorial review.
