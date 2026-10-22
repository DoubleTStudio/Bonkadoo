# Decision register
| ID | Decision | State |
|---|---|---|
| D01 | BONKADOO! working English title, NGƯỢC ĐỜI Vietnamese title | provisional; store/trademark review |
| D02 | Unity 6.3 LTS, C#, URP 2D, uGUI, Unity Input System | baseline |
| D03 | Yarn Spinner adapter; C# owns authoritative state | baseline |
| D04 | 5 chapters, 45 puzzle designs, 4 endings, 3–5h target | provisional until playtest |
| D05 | Entire game free; banner + >=5min scene-break interstitial; one-time ~$5 remove ads | fixed product requirement |
| D06 | Offline first, local saves, no mandatory account or server | fixed |
| D07 | Landscape reference 16:9, safe-area aware, scalable UI | provisional |
| D08 | 60fps midrange goal, 30fps minimum fallback | test target |
| D09 | 2D cartoon cutout rigging + hand-authored reaction frames | art target |
| D10 | VI/EN localized, original authored narrative, no runtime LLM | baseline |
| D11 | >=2 authored puzzle solutions for standard puzzles, documented finale exceptions | content target |
| D12 | Final ad SDK, IAP APIs, platform privacy configuration verified at integration time | pending |
| D13 | Project layout + 6 assemblies; engine-free core also compiled by plain .NET for tests (ADR-0001) | accepted M0 |
| D14 | Rule engine semantics, typed flags, transient `input.*`, request predicates, event model (ADR-0002) | accepted M0 |
| D15 | Metric/NPC ranges in `content/world/world.json`; NPC disposition assumed [-3,3] | reversible assumption |
| D16 | Follow backlog task order where ROADMAP/prompt milestone labels disagree (GAP_REPORT S1) | reversible assumption |
| D17 | Interstitial first-session grace = whole first session (GAP_REPORT S9) | reversible; revisit after playtest |
| D18 | Provisional bundle ID `com.omnicasa.bonkadoo`, company "Omnicasa" | provisional; confirm before any store upload |
| D19 | Newtonsoft via Unity's editor-bundled `com.unity.nuget.newtonsoft-json` 3.2.2 (offline-resolvable) | accepted M0 |
| D20 | Remove Ads product id `com.omnicasa.bonkadoo.removeads` (non-consumable) via Unity IAP 4.15.1 | provisional; must match store consoles |
| D21 | Content format v2 + Yarn Spinner 3.2.8 runtime-compiled dialogue (ADR-0003) | accepted |
| D22 | Placeholder art is procedural (shapes, colours, labels); audio is procedurally synthesized; both original, no third-party assets | interim until T017 |
| D23 | Gameplay stage built in uGUI (labelled hotspot buttons, EventSystem drag/drop) instead of world-space sprites | accepted; production art drops into the same Image slots |
| D24 | Release builds without an integrated ad SDK show no ads at all (NullAdService); dev builds use a simulated ad service | interim until T018 SDK + ad unit IDs |
| D25 | Interstitial "puzzle active" = item selected or drag in progress; scene/chapter transitions are the natural breaks | reversible |
