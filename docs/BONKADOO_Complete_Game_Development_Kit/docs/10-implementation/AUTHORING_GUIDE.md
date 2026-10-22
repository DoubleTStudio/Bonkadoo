# Chapter authoring guide (content format v2, ADR-0003)

Source of truth for playable content: `tools/author/chapters/chNN.py` (one per chapter), compiled by
`tools/author/build.py` into `content/`. **Never hand-edit generated JSON/yarn/localization.**
Reference implementation: `tools/author/chapters/ch01.py`. DSL: `tools/author/bonk.py`.
Design inputs: `content/seed/puzzles_seed_v1.json` (frozen original seeds — keep each puzzle's id,
title, goal, rule twist, featured NPC and the two seed strategies as the starting idea),
`docs/02-narrative/STORY_BIBLE.md`, `docs/02-narrative/chapters/chNN.md`, `CHARACTER_AND_DIALOGUE.md`,
`ENDING_MATRIX.md`, `docs/01-product/GDD.md`, `PROGRESSION_AND_HINTS.md`.

Self-check (must print PASS): `python3 tools/author/check_chapter.py chNN`

## Chapter shape (required)
- Exactly **9 puzzles** `chNN.p01`..`chNN.p09`, in seed order, **3 scenes** with 3 puzzles each
  (p01–p03 scene 1, p04–p06 scene 2, p07–p09 scene 3). Scene IDs `scene.chNN.<ascii_name>`.
- First scene has `enter='chNN_intro'` (chapter intro conversation).
- p03 unlocks the exit from scene 1 to 2; p06 unlocks scene 2 to 3. Back-exits use `start_unlocked=True`.
- One quiet Tí/Bốp beat with a **relationship choice** (`Choice` with `Option(act=...)`), started by a
  puzzle's `after=[converse(...)]` (usually p06). Choice `act` IDs: `choice.chNN.<name>`.
- p09 `after=[converse('chNN_outro')]` delivering the chapter's required reveal and a clue to the next chapter.
- At least one **method-dependent NPC reaction**: a `Talk(..., when=solved('chNN.pXX','a'))` and the `'b'`
  counterpart on an NPC in a later scene (see Cộc in ch01).

## Puzzle rules
- Two routes `a` and `b`, each with **2–3 steps**. Routes must be genuinely different interactions:
  different first action, different items or NPCs. Final step completes the puzzle.
- Typical step: `INSPECT` a prop that `gives` an item, then `USE` the item (`src=`) on a prop or NPC.
  `TALK` to an NPC can also give an item or be a step. Items needed by a route must be given earlier
  in the **same route** (players may take either route).
- Do not start both routes with the same `(verb, target, src)`. Avoid making a step of one puzzle identical
  to a still-active step of the same puzzle's other route; later steps get higher priority automatically.
- Every step target must be a hotspot in the puzzle's scene. Items may be carried across scenes.
- Consequences: each route has `effects` adjusting meters
  (`trust_bop` [-3,3], `public_trust` [-3,3], `officer_suspicion` [0,3], `evidence_count` [0,8],
  `rule_instability` [0,5]) and/or `npc('npc.x', value)` (absolute, [-3,3]). Routes should differ in
  consequence. Over a full chapter, either route choice should keep every ending reachable (finale has correctives).
- `fail` line (wrong item use), exactly 3 `hints` (1: name the contradiction, 2: point to object/NPC, 3: concrete action).
- Puzzle `item=` (optional) must be one of the chapter's items.

## Hotspots
- IDs ASCII only: `npc.<name>`, `prop.chNN.<name>`, `exit.chNN.<from>_<to>`. Globally unique (ch01 owns
  `npc.vuong`, `npc.bop`, `npc.janitor`, `npc.coc`). Recurring characters in later chapters get chapter-scoped
  IDs, e.g. `npc.bop_ch02`, `npc.coc_ch02`, `npc.mai_ch03`, `npc.lan_ch04`, with `character='bop'` etc.
- Every Prop and Exit has a `look` line (every touch gives feedback). Every Npc has `talk` entries with one
  default (`when=None`, listed last). Coordinates are normalized stage positions: x 0..1 left→right,
  y 0..1 bottom→top (hotspot centre), w/h fraction of stage. Keep y between 0.25 and 0.85; avoid overlap.
- 8–13 hotspots per scene. Characters: `ti, bop, vuong, coc, mai, lan, logic, narrator` plus extras listed in
  `bonk.SPEAKERS` (add a new speaker there only if truly needed, with EN/VI display names).

## Copy
- Every string is `(english, vietnamese)`. Natural Vietnamese with full diacritics; not literal translation.
  Keep ≤110 characters per line; split into more lines instead. No untranslatable puns for mandatory clues.
- Voices: Tí deadpan; Bốp precise, sarcastic, evasive; Vượng grandiose, audit-averse; Cộc literal procedure;
  Mai practical solidarity; Lan calm curiosity; LOGIC impeccable politeness masking indifference.
- Humor targets bureaucracy and contradiction, never protected identities. No violence.
- Yarn `If` conditions may only use: `metric("name")`, `has_event("evt...")`, `flag("flag...")`,
  `flag_text("flag...")`. Dialogue never changes state except through `Option(act=..., effects=[...])`.

## Chapter 5 endings (exact contract)
Final scene must contain four Prop hotspots `prop.ch05.door_patch`, `prop.ch05.door_rewrite`,
`prop.ch05.door_reveal`, `prop.ch05.door_walk`, plus corrective hotspots `npc.mai_ch05` (community testimony)
and `prop.ch05.audit_terminal` (final audit). Generate the choice and corrective rules with the helper (do not hand-write them):
```python
rules, strings = ending_rules(FINAL_SCENE, chosen_lines={'patch': L(...), 'rewrite': L(...), 'reveal': L(...), 'walk': L(...)},
                              locked_lines={'rewrite': L(...), 'reveal': L(...)},
                              corrective_lines={'testimony': L(...), 'audit': L(...)})
CHAPTER = Chapter(..., extra_rules=rules, extra_strings=strings)
```
It implements: doors choosable once p08 is complete and until p09 completes (re-choosing allowed);
PATCH and WALK always; REWRITE needs `public_trust >= 0` or the testimony corrective (Talk `npc.mai_ch05`);
REVEAL needs `evidence_count >= 4` or the audit corrective (Inspect `prop.ch05.audit_terminal`); locked doors
explain the corrective. The four doors, `npc.mai_ch05` and `prop.ch05.audit_terminal` still need normal
hotspot entries (look/talk lines) in the final scene.
- p09 both routes' first step `requires=ENDING_CHOSEN` (from bonk); p09 `after=[set_flag('flag.game.complete'),
  converse('ending_epilogue')]`. Conversation `ending_epilogue` branches on `flag_text("flag.ending") == "PATCH"`
  etc. with the ENDING_MATRIX epilogue callback for each, and Bốp tone via `metric("trust_bop")`.
- Never hard-lock: PATCH and WALK_AWAY always selectable.
