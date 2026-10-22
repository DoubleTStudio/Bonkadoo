# Progress log

Gate command: `source/ci.sh` (`--skip-unity` for the fast path). Evidence is from real runs on this machine
(Unity 6000.3.26f1, macOS arm64, Metal). Status words: **DONE** = acceptance met with runnable evidence;
**IMPLEMENTED** = runnable + tested in editor, but acceptance also needs a human/device/store step; **BLOCKED** = needs a
human gate (art, playtest, devices, accounts, legal).

## Gates (last full run)
| Gate | Result |
|---|---|
| `tools/author/build.py --check` (generated content = chapter specs) | up to date |
| `tools/author/simulate.py` (45 puzzles × 2 routes chained through 5 chapters, 4 endings) | PASS |
| `tools/validate_content.py` | PASS, 0 errors, 0 warnings |
| Validator negative fixtures | 31/31 |
| Unity Resources in sync with kit | in sync |
| dotnet engine-free tests | 128/128 |
| Unity EditMode | 137/137 |
| Unity PlayMode (UI end-to-end) | 4/4 |

## Backlog
| Task | Status | Evidence / what remains |
|---|---|---|
| T001–T004 (M0) | DONE | See ADR-0001/0002; save, journal, validator, rule engine tests |
| T005 Office scene and input | IMPLEMENTED | Office scene in UI; tap, select-then-use, drag-and-drop all tested (`GameUiPlayModeTests`, `InteractionControllerTests`). **Not yet run on Android/iOS hardware.** |
| T006 Office p01–p03 | DONE | Two distinct routes each, method callbacks (Cộc, Vượng), tested through UI and engine |
| T007 Yarn adapter | DONE | Yarn 3.2.8 runtime compile; `ConversationRunnerTests`; every node plays (`EveryYarnNodeCompilesAndPlaysToTheEnd`); VI/EN keys validated |
| T008 Character animation prototype | BLOCKED | Procedural placeholder figures only (D22); real rigs need art direction |
| T009 Vertical slice playtest | BLOCKED | Needs 15–20 external players |
| T010 Hints, NPC memory, inventory | DONE | `HintServiceTests` (ladder, nudge never spoils), talk-memory rules, inventory invariants; validator softlock check (locked exits must be unlockable) |
| T011 Replay, checkpoint, migration | DONE | `ReplayBranchLeavesMainSaveUntouched` (byte-identical main save, TC-010), chapter start checkpoints, migration chain tests |
| T012–T016 Chapters 1–5 + endings | IMPLEMENTED | 45 authored puzzles, 15 scenes, 186 hotspots, 112 items, 18 Yarn conversations, 1,673 VI/EN string pairs, 4 endings; `FullGameRouteTests` plays all-a (PATCH) and all-b (WALK_AWAY) through the whole game; REWRITE/REVEAL reachable from worst meters via correctives (TC-008). **Remaining: editorial review of all VI/EN copy, playtest for fun/fairness, device QA.** |
| T017 Art/audio finalization | BLOCKED | Art is procedural shapes; audio is synthesized (both original). Needs commissioned/licensed assets |
| T018 AdMob policy integration | IN_PROGRESS | `AdPolicy` + `AdCoordinator` tested (TC-003–006, no-fill, SDK exceptions, background time excluded, banner never over stage). Release builds currently show **no ads** (D24). Remaining: SDK + consent (UMP) + ad unit IDs |
| T019 Remove Ads IAP | IMPLEMENTED | Unity IAP non-consumable, restore, revocation sync on Android, offline cache; editor fake store works. Remaining: product in App Store Connect / Play Console, sandbox purchase/restore/refund |
| T020 Device QA and localization | BLOCKED | Needs physical device matrix and native-speaker review |
| T021 Store release | BLOCKED | Accounts, signing, legal, human approval. Drafts in `docs/08-delivery/RELEASE_PREP.md` |

## Known limitations
- All dialogue and UI copy is draft (agent-written, VI/EN); jokes and natural Vietnamese need editorial review.
- Puzzles are mechanically reachable and distinct, not playtested for fun or fairness.
- Visuals are labelled placeholder shapes; music/SFX are simple synthesized loops.
- Autosave writes snapshot + whole journal after every committed interaction; profile on low-tier devices.
- No analytics or crash reporting (none required for offline play; decision pending).
- Yarn version skew: Unity uses 3.2.8 DLLs, the dotnet test project uses NuGet 3.2.2 (same API).
