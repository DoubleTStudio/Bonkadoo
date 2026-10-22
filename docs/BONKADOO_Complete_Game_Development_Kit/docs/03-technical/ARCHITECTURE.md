# Architecture — Unity/C#
## Assemblies
`Bonkadoo.Domain` (pure C#): facts, rules, interaction, inventory, event application.
`Bonkadoo.Application`: use cases, scene transitions, orchestration, checkpoints.
`Bonkadoo.Infrastructure`: save, Yarn adapter, ads, IAP, analytics, localization.
`Bonkadoo.Presentation`: MonoBehaviours, input, UI, animations, cameras.
`Bonkadoo.Tests`: EditMode unit tests, PlayMode integration tests.

## Interaction contract
`InteractionRequest {requestId,action,sourceId?,targetId,sceneId,tick}` -> `InteractionOutcome {matchedRuleId?,success,feedbackKey,events[],animationCues[]}`.
`Rule {id,priority,when: PredicateAST,effects: Effect[],once}`. Evaluate priority descending then rule ID ascending. First matching terminal rule wins. Invalid interaction returns localized non-silent fallback. All effects validated atomically before commit. Repeated requestId must not duplicate effects.

## Predicate DSL
`all`, `any`, `not`, `flagEquals`, `hasItem`, `metricAtLeast`, `npcDispositionAtLeast`, `eventOccurred`, `currentScene`. Never execute arbitrary expressions or reflection from JSON.
## Effect DSL
`setFlag`, `adjustMetric` (clamped), `addItem`, `removeItem`, `emitEvent`, `queueDialogue`, `queueAnimation`, `unlockExit`, `setNpcDisposition`.

## Event model
`WorldEvent {id,type,payload,tick,causationId,schemaVersion}`. World state is projection of journal and latest snapshot. Stable ordering, deterministic replay, idempotent application. Dialogues do not own authoritative state. A scene is re-enterable without duplicating events.

## Save
Local versioned JSON snapshot plus append-only event journal, two rotating slots and checksum. Temp write -> replace, fallback slot on corruption. Autosave on committed event, scene change and background. Migration chain N -> N+1; keep stable IDs and explicit remapping. Settings separate from story saves. Replay chapter in branch slot, never overwrite primary without confirmation.

## Unity presentation
URP 2D + sprite atlases, pooled bubbles/particles, animation state machines, Yarn Spinner narrative, uGUI responsive layout. Scenes: Bootstrap, Menu, ChapterHub, Gameplay, Credits. Prefabs map stable IDs to scene hotspots; no story logic in MonoBehaviour.

## Performance & privacy
60fps target on midrange, >=30fps low tier; measure memory, loading and thermal stability on physical devices. No mandatory internet. Consent-gated analytics/ads where required; privacy disclosures; no raw dialogue text or personal data in telemetry. Store ownership through supported Unity IAP integration, restore purchases on iOS.
