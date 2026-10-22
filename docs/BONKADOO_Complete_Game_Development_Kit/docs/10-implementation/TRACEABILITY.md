# Traceability: requirement → code → test

Paths: code under `source/Bonkadoo/Assets/Bonkadoo/Scripts/`, tests under `source/Bonkadoo/Assets/Bonkadoo/Tests/`.

| Requirement (source) | Code | Tests | Status |
|---|---|---|---|
| Domain has no Unity dependency (AGENTS, ARCHITECTURE) | `*.asmdef` `noEngineReferences`; `CoreTests/*.csproj` | `ProjectSmokeTests.EngineFreeAssembliesDoNotReferenceUnity`; dotnet build itself | Implemented |
| Allowlisted predicate/effect DSL, no arbitrary eval (AGENTS, ARCHITECTURE) | `Domain/Predicates.cs`, `Domain/Effects.cs`, `Infrastructure/Content/ContentLoader.cs`, `tools/validate_content.py` | `ContentLoaderTests.RejectsNonAllowlisted*`, `ParsesEveryAllowlistedOperator`, validator fixtures | Implemented |
| Priority desc then ID asc, first match wins (ARCHITECTURE) | `Domain/Rules.cs` `ResolutionOrder` | `HigherPriorityRuleWins`, `EqualPriorityResolvesByOrdinalRuleId`, `ResolutionIsIndependentOfDeclarationOrder` | Implemented |
| Atomic effect validation (ARCHITECTURE) | `Domain/InteractionEngine.cs`, `EffectPlan` | `InvalidEffectRejectsWholeRuleAtomically`, `UnknownMetricIsRejected`, `NpcDispositionOutOfRangeIsRejected` | Implemented |
| Localized non-silent fallback (ARCHITECTURE) | `RuleSet.ResolveFallbackKey` | `UnmatchedRequestReturnsNonSilentFallback`, `FallbackUsesOwningPuzzleFailureLine`, `PrerequisitesGateRules` | Implemented (needs `dlg.common.fail` copy, M1) |
| Repeated requestId no duplicate effects — TC-001 | `InteractionEngine` duplicate check, `WorldStateProjector` event-ID guard | `DuplicateRequestIdCommitsOnce`, `ResendingEveryRequestChangesNothing`, `ReapplyingJournalTwiceIsNoOp` | Implemented |
| Transient `input.*` never persisted (INTERACTION_NOTES) | `EvaluationContext.ResolveFlag`, `SetFlagEffect` | `TransientInputIsReadFromRequestAndNeverPersisted`, `WritingTransientKeyIsRejected` | Implemented |
| Deterministic replay; state = journal projection (ARCHITECTURE) | `WorldStateProjector`, `GameSession.Restore` | `ReplayDeterminismTests.*`, `SnapshotPlusTailReplaysOnlyNewEvents` | Implemented |
| Metrics clamped (STORY_BIBLE) | `AdjustMetricEffect`, `content/world/world.json` | `MetricsClampToDefinedRange`, `InvariantsHoldThroughoutRandomPlay` | Implemented |
| Versioned save, checksum, 2 rotating slots, temp→replace — TC-002 | `Infrastructure/Save/*` | `SaveServiceTests.*` | Implemented |
| Migration chain N→N+1 | `SaveMigrator` | `SaveMigrationTests.*` | Framework implemented; no real migration yet |
| Replay branch slot never overwrites primary — TC-010 | slot IDs in `SaveService` | `SlotsAreIndependent` | Storage layer only; replay flow is T011 |
| VI/EN parity; runtime switch — TC-009 | `Infrastructure/Localization/Localizer.cs`, validator | `LocalizerTests.*`, validator fixtures | Tables implemented; UI binding M1 |
| Offline-first launch (D06) — TC-007 | `GameBootstrap` (Resources + local disk only) | `BootstrapSceneComposesServicesOffline` | Bootstrap only; full chapter in airplane mode needs device |
| Every puzzle: two routes + failure path (TEST_STRATEGY) | content + engine | `EverySeedRouteIsReachableInChapterOrder` (a, b) | Seed routes only; authored puzzles outstanding |
| Landscape, safe area (D07) | `Editor/ProjectSetup.cs` | `ProjectSmokeTests.LandscapeOnly` | Orientation only; safe area M1 |
| Ad policy TC-003..TC-006 | — | — | Not started (T018) |
| All 4 endings reachable — TC-008 | — | — | Not started (T016) |
