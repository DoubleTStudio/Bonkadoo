# Gap report — kit v1.0 audit (2026-10-22)

Scope: every file in the kit (docs, schemas, content, tools, skills, backlog). `python3 tools/validate_content.py` passes on the unmodified kit.

## Toolchain found
- Unity 6000.3.26f1 (6.3 LTS) with Android + iOS modules, Unity Personal licence, `unity` CLI (headless test runner).
- .NET SDK 8/9/10 (used for fast Unity-free domain tests).
- Git repository rooted at `source/` (remote configured, no commits yet); no CI runner; no physical-device access from this agent session.

## Content gaps (block "chapter DONE", not M0)
| # | Gap | Evidence | Impact |
|---|---|---|---|
| C1 | All 45 puzzles share one template: 2 rules, `Inspect → Talk → UseItem(option.a/b)` | 90/90 recipes identical in verb sequence | Solutions are option aliases, not distinct interactions (INTERACTION_IMPLEMENTATION_NOTES admits this). Must be re-authored per puzzle from M1. |
| C2 | All 90 success lines are the same string "That should not work. Yet it does!" | en.json | Placeholder copy; comedy/feedback pillar unmet. |
| C3 | No content drives the relationship meters | 0 `adjustMetric` / `setNpcDisposition` effects | Ending gates (`public_trust`, `evidence_count`) are unreachable from content except via finale correctives. |
| C4 | No ending content | ENDING_MATRIX only | Need ending flags, finale rules, epilogue keys. |
| C5 | `chapterCompleteFlag` never set by any rule | ch0N.json vs puzzles.json | Chapter progression must be wired in Application layer or content. |
| C6 | Puzzle `prerequisites` are not part of rule predicates | puzzles.json | Engine must AND prerequisites into rule eligibility (done in loader, see ADR-0002). |
| C7 | Scene IDs (`scene.ch01.01`) don't match naming doc (`scene.ch01.office`) | DATA_CONTRACTS vs puzzles.json | Kept content IDs (content is source of truth); doc example is illustrative. |
| C8 | Non-ASCII IDs (`npc.vượng`, `npc.cộc`, …) while art IDs are ASCII (`chr.vuong`) | puzzles.json, ART_BIBLE | File-path / platform-normalisation risk. Validator warns; normalise when p01–p03 are re-authored (no saves exist yet, so no migration needed). |
| C9 | No UI strings, menu, settings, chapter titles, NPC names in localization | en.json has only `dlg.*`/`txt.*` | Needed for M1. |
| C10 | No Yarn scripts, no dialogue IDs in `chNN.scene.character.beat.NNN` form | CHARACTER_AND_DIALOGUE | Narrative authoring is fully outstanding. |
| C11 | Asset manifest: 100% `concept-needed`, `unlicensed/unproduced` | manifest.csv | No art/audio exists. Production art requires human art direction + licensing. |
| C12 | Metric/NPC-disposition ranges only in prose | STORY_BIBLE | Added machine-readable `content/world/world.json` (ADR-0002). |

## Specification contradictions / ambiguities
| # | Issue | Resolution (reversible assumption) |
|---|---|---|
| S1 | Backlog places Ch3–5 in M4 and art/ads in M4–M5; ROADMAP places Ch1–2 in M3, Ch3–5 in M4; prompt places all chapters in M3 | Follow backlog task order (dependency chain is identical); milestone labels are informational. |
| S2 | Backlog has M6 (store release); prompt stops at M5 | M6 tasks require store accounts → human gate. |
| S3 | `SYSTEM_INTERFACES.cs` uses C# `record` and `PayloadJson` string | Unity's C# 9 lacks `IsExternalInit`; Domain must not depend on JSON. Use sealed classes and a sorted string payload map (ADR-0002). |
| S4 | "First matching *terminal* rule wins" but schema has no terminal flag | Every rule is terminal (ADR-0002). |
| S5 | Absent flags vs `flagEquals {value:false}` | An absent boolean flag equals `false`; an absent string flag equals nothing. |
| S6 | Validator checks distinctness via last step target only | Kept, plus stricter checks added in T002. |
| S7 | Hint unlock timing (45 s idle / 3 attempts) vs "available at any time" | Hints always available from UI; idle/attempt thresholds only trigger a *nudge*. |
| S8 | Interstitial "frequency cap at most 1 per 5 minutes" and ">=300 s active play since last shown" | Same constraint; implemented once in `AdPolicy`. |
| S9 | First-session grace length unspecified | Assume the whole first session is ad-free for interstitials (banner still allowed). Revisit after playtest. |
| S10 | NPC disposition range unspecified | Assume [-3, 3], matching `trust_bop`. |

## Human-gated items (cannot be completed by an agent)
Brand/trademark (D01), production art & audio and their licences (T008, T017), external playtest (T009), physical-device QA (T005, T020), AdMob/App Store/Play accounts, ad unit IDs and IAP product IDs (T018–T019), privacy policy, age rating, store submission (T021).

## What "done" will mean in progress reports
Per README definitions: *Implemented* = runnable Unity behaviour with tests and localized content. Seed content running through the engine is reported as **engine-executable seed**, never as an authored chapter.
