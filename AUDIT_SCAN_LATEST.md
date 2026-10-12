### Automated Codebase Audit Scan (2026-10-12 13:22:58)
**Total C# Source Files:** 79 | **Total LOC:** 28,738
**Classification:** 🚨 5 God Objects | 🟡 20 Elevated/Watchlist | 🟢 54 Clean

| Status | Class / File | Lines | Methods | Architectural Smells Detected |
| :--- | :--- | :---: | :---: | :--- |
| 🚨 GOD OBJECT / ENTANGLED | `Services/LocalWorldSynthesizer.cs` | 2,716 | 6 | None (Well-bounded) |
| 🚨 GOD OBJECT / ENTANGLED | `Database/SpatialAssetRegistry.cs` | 1,587 | 28 | None (Well-bounded) |
| 🚨 GOD OBJECT / ENTANGLED | `DynamicWorldEngineScript.cs` | 1,088 | 17 | None (Well-bounded) |
| 🚨 GOD OBJECT / ENTANGLED | `Discovery/SemanticClassificationEngine.cs` | 1,015 | 10 | None (Well-bounded) |
| 🚨 GOD OBJECT / ENTANGLED | `Discovery/DynamicInteriorDiscoveryProvider.cs` | 993 | 12 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Runtime/EntitySpawnerService.cs` | 825 | 19 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Database/WorldMemoryStore.cs` | 822 | 46 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Commands/ActionSchemas.cs` | 767 | 3 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `EngineCore.cs` | 758 | 42 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/SpatialAffordanceScanner.cs` | 746 | 12 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/SpatialPerceptionScanner.cs` | 636 | 11 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/ProceduralCrowdDirector.cs` | 617 | 1 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/SpatialGridSampler.cs` | 597 | 6 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Discovery/AssetIndexCacheManager.cs` | 523 | 15 | • Direct File I/O outside persistence/bridge layer |
| 🟡 WATCHLIST / ELEVATED | `Spatial/SpatialPlacementSolver.cs` | 509 | 9 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/SpatialWorldPlanner.cs` | 508 | 3 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Simulation/EphemeralStateRecomputer.cs` | 490 | 8 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Retrieval/EngramResolver.cs` | 486 | 8 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/OpenWorldLocationProvider.cs` | 425 | 6 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/SpatialWorldModel.cs` | 408 | 11 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/LocationSelectionResolver.cs` | 401 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Services/WorldGeneratorService.cs` | 376 | 12 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/SemanticDecomposer.cs` | 363 | 6 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/GtaInstallationLocator.cs` | 355 | 8 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/SpatialPlanValidator.cs` | 355 | 8 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/NpcActivityDirector.cs` | 352 | 7 | None (Well-bounded) |
| 🟢 CLEAN | `Bridge/LivingLsAiBridge.cs` | 347 | 6 | None (Well-bounded) |
| 🟢 CLEAN | `Retrieval/KairosInsightCompiler.cs` | 345 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Services/LlmTransportClient.cs` | 335 | 7 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/CanonicalIdentityResolver.cs` | 329 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Database/EvidenceRecord.cs` | 326 | 13 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/WorldSimulationEngine.cs` | 309 | 10 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/TeleportPocketManager.cs` | 295 | 9 | • Direct Blip creation bypassing BlipManager |
| 🟢 CLEAN | `Discovery/GenericPrimitiveComposer.cs` | 292 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Runtime/RuntimeExecutor.cs` | 288 | 8 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/SpatialVisionEngine.cs` | 285 | 5 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/SalienceEvaluator.cs` | 284 | 8 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Utils/EngineLogger.cs` | 283 | 13 | • Direct File I/O outside persistence/bridge layer |
| 🟡 WATCHLIST / ELEVATED | `Spatial/InteriorManager.cs` | 273 | 6 | • Direct Blip creation bypassing BlipManager |
| 🟢 CLEAN | `Database/Models.cs` | 262 | 0 | None (Well-bounded) |
| 🟢 CLEAN | `Services/JsonRepairUtility.cs` | 261 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Retrieval/ContextRetriever.cs` | 257 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/SpatialExecutionPlan.cs` | 253 | 10 | None (Well-bounded) |
| 🟢 CLEAN | `Observer/WorldObserver.cs` | 227 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Commands/MutationSchemas.cs` | 219 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/VenueSelectionResolver.cs` | 211 | 1 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/WorldContextPacket.cs` | 205 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Database/CanonicalCharacterRegistry.cs` | 203 | 3 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Commands/WorldActionExecutor.cs` | 199 | 4 | • Database state mutation/commit inside parser |
| 🟢 CLEAN | `Commands/ActionParser.cs` | 196 | 7 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/DirectorTimelineManager.cs` | 196 | 5 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/WorldPerceptionSnapshot.cs` | 187 | 1 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Spatial/PlanRepository.cs` | 185 | 8 | • Direct File I/O outside persistence/bridge layer |
| 🟢 CLEAN | `Spatial/SpatialWorldModelRenderer.cs` | 185 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/InteriorActivationJob.cs` | 180 | 1 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/DlcPackDiscoveryEngine.cs` | 162 | 5 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/InteriorCatalog.cs` | 162 | 5 | None (Well-bounded) |
| 🟢 CLEAN | `Runtime/PlayerTeleportCoordinator.cs` | 152 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/IntentInterpreter.cs` | 149 | 1 | None (Well-bounded) |
| 🟢 CLEAN | `Discovery/GameAssetScanner.cs` | 140 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/ActiveNpcContext.cs` | 140 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/BlipManager.cs` | 140 | 7 | None (Well-bounded) |
| 🟢 CLEAN | `Audio/PartyAudioManager.cs` | 136 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/ModularAssetCatalog.cs` | 134 | 5 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Runtime/WorldEventDispatcher.cs` | 133 | 1 | • Direct native World.Create* call in non-spawner class |
| 🟢 CLEAN | `Spatial/PropPlacementEngine.cs` | 128 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Simulation/NpcStateMachine.cs` | 124 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `UI/WorldInspector.cs` | 123 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/NavMeshBlockerManager.cs` | 118 | 7 | None (Well-bounded) |
| 🟢 CLEAN | `Archetypes/EnvironmentCatalog.cs` | 116 | 9 | None (Well-bounded) |
| 🟢 CLEAN | `Runtime/EntityMutationHandler.cs` | 89 | 1 | None (Well-bounded) |
| 🟡 WATCHLIST / ELEVATED | `Services/PromptCache.cs` | 80 | 4 | • Direct File I/O outside persistence/bridge layer |
| 🟢 CLEAN | `Spatial/SpatialQueryService.cs` | 80 | 4 | None (Well-bounded) |
| 🟢 CLEAN | `Runtime/RealizationScope.cs` | 69 | 3 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/SpatialFeasibilityEvaluator.cs` | 60 | 1 | None (Well-bounded) |
| 🟢 CLEAN | `Utils/ClipboardHelper.cs` | 49 | 1 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/HierarchicalTransformEngine.cs` | 44 | 2 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/ModularBuildingKit.cs` | 40 | 0 | None (Well-bounded) |
| 🟢 CLEAN | `Spatial/GroundRaycaster.cs` | 35 | 1 | None (Well-bounded) |