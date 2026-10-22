# Progress log

Gate command: `source/ci.sh` (add `--skip-unity` for the fast path). Evidence below is from real runs on 2026-10-22, Unity 6000.3.26f1, macOS arm64.

## M0 Foundation — DONE (T001–T004)

| Task | Acceptance | Evidence |
|---|---|---|
| T001 Project skeleton | Unity opens; assemblies compile; smoke test | `unity run … -executeMethod Bonkadoo.Editor.ProjectSetup.Apply` exits 0 with no CS errors; EditMode `ProjectSmokeTests` (engine-free assemblies reference no `Unity*` assembly, landscape-only, Bootstrap first in build, Resources content loads, synced content byte-identical to kit); PlayMode `BootstrapSceneComposesServicesOffline` passes 1/1 |
| T002 Content validator | All 45 seeds validate; CI fails on bad IDs | `tools/validate_content.py` PASS (0 errors, 67 warnings). `tests/validator` 24/24: every negative fixture (bad/duplicate IDs, non-allowlisted DSL, `input.*` writes, unknown events, unsatisfiable prerequisites, localization gaps, chapter mapping, malformed entries) is rejected |
| T003 Domain rule engine | Deterministic priority; atomic effects; invalid fallback | `InteractionEngineTests`, `PredicateTests`, `ReplayDeterminismTests` (seeded random play; shuffled declaration order). Mutation check: reversing priority order or dropping the dry-run scratch copy makes 4 tests fail |
| T004 Event journal and save | Replay idempotent; corruption fallback; migration tests | `SaveServiceTests` (interrupted write and interrupted replace keep the previous checkpoint = TC-002; corrupt newest slot falls back; checksum tamper detected; future version neither loaded nor overwritten; branch slot leaves primary untouched), `SaveMigrationTests` (chain order, missing step, non-advancing step, on-disk v0→v1) |

Totals from the last full gate: dotnet 80/80, Unity EditMode 87/87, Unity PlayMode 1/1, validator fixtures 24/24.

### Honest limits of M0
- No shipped save migration exists yet: v1 is the first format. The migration *framework* is tested with test-only steps.
- Autosave triggers were not part of M0; they were wired in T005 (below).
- The 45 puzzles execute through the engine as **engine-executable seeds** (both routes reach their event in chapter order). This proves wiring only; it does not make any puzzle authored, distinct, or fun (GAP_REPORT C1–C2).
- CI workflow (`source/.github/workflows/ci.yml`) has never run: nothing pushed yet, and the Unity job needs licence secrets.
- Nothing has run on a physical device.

## M1 — T005 Office scene and input: IN_PROGRESS

Done, with evidence (last full gate: dotnet 95/95, EditMode 102/102, PlayMode 6/6 of which 4 are ours and 2 are Input System package tests pulled in by `testables`):
- `InteractionController`: verb per hotspot kind; select-item-then-tap produces the same request as drag-item-onto-target; reversible selection; unheld items cannot be used; option hotspots send transient `input.*` only (`InteractionControllerTests`).
- `GestureRecognizer`: tap/drag split measured in points, so the threshold is the same on every screen density; 44pt touch-target helper (`GestureRecognizerTests`).
- `AutosaveCoordinator`: saves on each committed batch (including scene changes) and on app pause/quit, never on fallbacks; save failures are reported without breaking play (`AutosaveRunsOnCommitAndSceneChangeButNotOnFallback`, `AutosaveFailureIsReportedNotThrown`).
- `Hotspot` + `PointerInteractor` (Input System pointer, 2D collider raycast): simulated-mouse PlayMode tests for tap, tap on empty space, and inventory drag onto a hotspot.

Remaining for T005:
- Office scene layout (blockout), inventory bar UI, safe-area layout, hint button: needs the re-authored p01–p03 hotspot graph from T006 to lay out against.
- Touch-target size check per hotspot at 16:9 and 19.5:9.
- **Device run on Android and iOS (acceptance criterion): needs physical hardware, not done.**
- Autosave writes the whole journal synchronously on every commit; profile on a low-tier device.
