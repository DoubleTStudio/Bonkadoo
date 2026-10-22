# Traceability: requirement → code → test

Paths: code under `source/Bonkadoo/Assets/Bonkadoo/Scripts/`, tests under `source/Bonkadoo/Assets/Bonkadoo/Tests/`.

| Requirement (source) | Code | Tests | Status |
|---|---|---|---|
| Domain has no Unity dependency (AGENTS, ARCHITECTURE) | `*.asmdef` `noEngineReferences`; `CoreTests/*.csproj` | `ProjectSmokeTests.EngineFreeAssembliesDoNotReferenceUnity`; dotnet build itself | Implemented |
| Allowlisted predicate/effect DSL, no arbitrary eval (AGENTS, ARCHITECTURE) | `Domain/Predicates.cs`, `Domain/Effects.cs`, `Infrastructure/Content/ContentLoader.cs`, `tools/validate_content.py` | `ContentLoaderTests.RejectsNonAllowlisted*`, `ParsesEveryAllowlistedOperator`, validator fixtures | Implemented |
| Priority desc then ID asc, first match wins (ARCHITECTURE) | `Domain/Rules.cs` `ResolutionOrder` | `HigherPriorityRuleWins`, `EqualPriorityResolvesByOrdinalRuleId`, `ResolutionIsIndependentOfDeclarationOrder` | Implemented |
| Atomic effect validation (ARCHITECTURE) | `Domain/InteractionEngine.cs`, `EffectPlan` | `InvalidEffectRejectsWholeRuleAtomically`, `UnknownMetricIsRejected`, `NpcDispositionOutOfRangeIsRejected` | Implemented |
| Localized non-silent fallback (ARCHITECTURE) | `RuleSet.ResolveFallbackKey` | `UnmatchedRequestReturnsNonSilentFallback`, `FallbackUsesOwningPuzzleFailureLine`, `PrerequisitesGateRules` | Implemented |
| Repeated requestId no duplicate effects — TC-001 | `InteractionEngine` duplicate check, `WorldStateProjector` event-ID guard | `DuplicateRequestIdCommitsOnce`, `ResendingEveryRequestChangesNothing`, `ReapplyingJournalTwiceIsNoOp` | Implemented |
| Transient `input.*` never persisted (INTERACTION_NOTES) | `EvaluationContext.ResolveFlag`, `SetFlagEffect` | `TransientInputIsReadFromRequestAndNeverPersisted`, `WritingTransientKeyIsRejected` | Implemented |
| Deterministic replay; state = journal projection (ARCHITECTURE) | `WorldStateProjector`, `GameSession.Restore` | `ReplayDeterminismTests.*`, `SnapshotPlusTailReplaysOnlyNewEvents` | Implemented |
| Metrics clamped (STORY_BIBLE) | `AdjustMetricEffect`, `content/world/world.json` | `MetricsClampToDefinedRange`, `InvariantsHoldThroughoutRandomPlay` | Implemented |
| Versioned save, checksum, 2 rotating slots, temp→replace — TC-002 | `Infrastructure/Save/*` | `SaveServiceTests.*` | Implemented |
| Migration chain N→N+1 | `SaveMigrator` | `SaveMigrationTests.*` | Framework implemented; no real migration yet |
| Replay branch slot never overwrites primary — TC-010 | `GameFlow.StartReplay` | `ReplayBranchLeavesMainSaveUntouched` | Implemented |
| VI/EN parity; runtime switch — TC-009 | `Localizer`, `LocalizedText`, validator | `LocalizerTests`, `LanguageSwitchRelabelsWithoutRestart` | Implemented |
| Offline-first launch (D06) — TC-007 | `GameBootstrap` (Resources + local disk only), non-blocking ads/IAP | `GameUiPlayModeTests` (boot→chapter 1 with no network calls), `AdCoordinatorTests.SdkExceptionsNeverEscape` | Editor-verified; airplane-mode device run pending |
| Every puzzle: two routes + failure path (TEST_STRATEGY) | `tools/author/chapters/*.py` → content | `FullGameRouteTests`, `simulate.py`, `EveryPropAndExitGivesFeedback` | Implemented; editorial/playtest pending |
| Landscape, safe area (D07) | `Editor/ProjectSetup.cs` | `ProjectSmokeTests.LandscapeOnly`, `SafeAreaFitter` on every screen | Implemented; notch devices pending |
| Ad policy TC-003..TC-006 | `Domain/AdPolicy.cs`, `Application/Monetization.cs` | `AdPolicyTests`, `AdCoordinatorTests`, `ActivePlayClockTests` | Policy implemented; SDK pending |
| Remove Ads IAP, restore (ADS_AND_IAP) | `Infrastructure.Unity/UnityIapStore.cs`, `SettingsStore` | `SettingsStoreTests` (entitlement), editor fake store | Store sandbox pending |
| Yarn dialogue, C# owns state (D03) | `Narrative/YarnNarrative.cs` | `ConversationRunnerTests`, `EveryYarnNodeCompilesAndPlaysToTheEnd` | Implemented |
| Hint ladder, nudge (PROGRESSION_AND_HINTS) | `Application/HintService.cs` | `HintServiceTests` | Implemented |
| Mobile UI: safe area, tap+drag, text scale, captions, reduced motion (UX) | `Presentation/Ui`, `Presentation/Gameplay`, `Presentation/Screens` | `GameUiPlayModeTests`, screenshots | Implemented; device review pending |
| All 4 endings reachable — TC-008 | `bonk.ending_rules`, `GameFlow.CurrentEnding` | `FullGameRouteTests` (all four), `simulate.check_endings` | Implemented |
