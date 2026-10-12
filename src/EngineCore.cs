using System;
using System.Collections.Generic;
using DynamicWorldEngine.Bridge;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Observer;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Services;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;

namespace DynamicWorldEngine
{
    public class SpatialOrchestrationResult
    {
        public bool Success { get; set; }
        public string Prompt { get; set; }
        public LocationSelectionResult LocationResult { get; set; }
        public TeleportAnchorResult TeleportResult { get; set; }
        public WorldPerceptionSnapshot Perception { get; set; }
        public SpatialWorldModel WorldModel { get; set; }
        public WorldContextPacket ContextPacket { get; set; }
        public string GeneratedSceneJson { get; set; }
        public SpatialExecutionPlan Plan { get; set; }
        public SpatialValidationResult ValidationResult { get; set; }
        public EnvironmentRecord Environment { get; set; }
        public string DiagnosticMessage { get; set; }

        public SpatialOrchestrationResult()
        {
            Success = false;
            Prompt = "";
            DiagnosticMessage = "";
        }
    }

    public class EngineCore
    {
        private readonly WorldMemoryStore _store;
        private readonly ActionParser _parser;
        private readonly WorldActionExecutor _executor;
        private readonly LivingLsAiBridge _bridge;
        private readonly WorldGeneratorService _generator;
        private readonly WorldSimulationEngine _simulation;
        private readonly WorldObserver _observer;
        private readonly SpatialWorldPlanner _planner;
        private readonly PlanRepository _plans;
        private readonly RuntimeExecutor _runtime;
        private readonly ContextRetriever _retriever;
        private readonly KairosInsightCompiler _insightCompiler;
        private readonly SpatialPerceptionScanner _perceptionScanner;
        private readonly SpatialPlacementSolver _placementSolver;
        private readonly LocationSelectionResolver _locationResolver;
        private readonly PlayerTeleportCoordinator _teleportCoordinator;
        private readonly SpatialPlanValidator _planValidator;

        public EngineCore(
            string dbDirectory = null,
            string livingLsAisFolderPath = null,
            NavMeshBlockerManager navBlockers = null,
            BlipManager blips = null,
            TeleportPocketManager pockets = null,
            InteriorManager interiorMgr = null,
            RuntimeExecutor runtime = null)
        {
            _store = new WorldMemoryStore(dbDirectory);
            _parser = new ActionParser();
            _executor = new WorldActionExecutor(_store);
            _bridge = new LivingLsAiBridge(_store, livingLsAisFolderPath);
            _generator = new WorldGeneratorService(dbDirectory, livingLsAisFolderPath);

            string plansDir = !string.IsNullOrEmpty(dbDirectory)
                ? System.IO.Path.Combine(dbDirectory, "plans")
                : null;
            _plans = new PlanRepository(plansDir);

            _simulation = new WorldSimulationEngine(_store, _bridge, pockets, blips);
            _observer = new WorldObserver(_simulation);
            _planner = new SpatialWorldPlanner();

            var nb = navBlockers ?? new NavMeshBlockerManager();
            var bm = blips ?? new BlipManager();
            var pm = pockets ?? new TeleportPocketManager();
            var im = interiorMgr ?? new InteriorManager(pm, bm);

            _runtime = runtime ?? new RuntimeExecutor(
                new EntitySpawnerService(),
                nb,
                bm,
                pm,
                im
            );
            _retriever = new ContextRetriever();
            _insightCompiler = new KairosInsightCompiler(_store, _simulation.Recomputer, _simulation.Salience, _retriever);
            _perceptionScanner = new SpatialPerceptionScanner();
            _placementSolver = new SpatialPlacementSolver();
            _locationResolver = new LocationSelectionResolver();
            _teleportCoordinator = new PlayerTeleportCoordinator();
            _planValidator = new SpatialPlanValidator();
        }

        public WorldMemoryStore Store { get { return _store; } }
        public PlanRepository Plans { get { return _plans; } }
        public RuntimeExecutor Runtime { get { return _runtime; } }
        public ActionParser Parser { get { return _parser; } }
        public WorldActionExecutor Executor { get { return _executor; } }
        public SpatialPerceptionScanner PerceptionScanner { get { return _perceptionScanner; } }
        public SpatialPlacementSolver PlacementSolver { get { return _placementSolver; } }
        public LocationSelectionResolver LocationResolver { get { return _locationResolver; } }
        public PlayerTeleportCoordinator TeleportCoordinator { get { return _teleportCoordinator; } }
        public SpatialPlanValidator PlanValidator { get { return _planValidator; } }
        public LivingLsAiBridge Bridge { get { return _bridge; } }
        public WorldGeneratorService Generator { get { return _generator; } }
        public WorldSimulationEngine Simulation { get { return _simulation; } }
        public WorldObserver Observer { get { return _observer; } }
        public SpatialWorldPlanner Planner { get { return _planner; } }
        public SpatialAssetRegistry AssetRegistry { get { return SpatialAssetRegistry.Instance; } }
        public ContextRetriever Retriever { get { return _retriever; } }
        public SalienceEvaluator Salience { get { return _simulation.Salience; } }
        public EphemeralStateRecomputer Recomputer { get { return _simulation.Recomputer; } }
        public KairosInsightCompiler InsightCompiler { get { return _insightCompiler; } }

        public SalienceAssessment EvaluateEventSalience(EventRecord evt, NpcRecord npc = null, float distanceToPlayer = 0f)
        {
            return _simulation.Salience.EvaluateEvent(evt, npc, distanceToPlayer);
        }

        public EphemeralNpcPosture GetEffectiveNpcPosture(int npcId, DateTime? asOf = null)
        {
            return _simulation.GetEffectiveNpcPosture(npcId, asOf);
        }

        public LocationHeatAssessment GetLocationHeat(float x, float y, float z, float radiusMeters = 75f, DateTime? asOf = null)
        {
            return _simulation.GetLocationHeat(x, y, z, radiusMeters, asOf);
        }

        public FactionTensionReport GetFactionHostility(string factionA, string factionB, DateTime? asOf = null)
        {
            return _simulation.GetFactionHostility(factionA, factionB, asOf);
        }

        public KairosSituationBrief GetKairosSituationBrief(float? x = null, float? y = null, float? z = null, int? focusNpcId = null, string targetFaction = null)
        {
            return _insightCompiler.CompileSituationBrief(x, y, z, focusNpcId, targetFaction);
        }

        public KairosNpcDossier GetKairosNpcDossier(int npcId)
        {
            return _insightCompiler.CompileNpcDossier(npcId);
        }

        public WorldPerceptionSnapshot GetPerceptionSnapshot(float x, float y, float z, float radius = 50.0f)
        {
            return _perceptionScanner.ScanLocation(new GTA.Math.Vector3(x, y, z), radius, _store, _simulation.Recomputer);
        }

        public SpatialWorldModel GetSpatialWorldModel(float x, float y, float z, float radius = 50.0f)
        {
            return _perceptionScanner.ScanSpatialWorldModel(new GTA.Math.Vector3(x, y, z), radius, _store, _simulation.Recomputer);
        }

        public string RenderSpatialAsciiMap(SpatialWorldModel model, string title = "SPATIAL PERCEPTION MAP")
        {
            return SpatialWorldModelRenderer.RenderAsciiMap(model, title);
        }

        public SpatialSceneSolution SolveSpatialPlacement(SpatialWorldModel model, SceneInteractionRequirements requirements)
        {
            return _placementSolver.Solve(model, requirements);
        }

        public bool CheckLineOfSight(GTA.Math.Vector3 a, GTA.Math.Vector3 b, SpatialWorldModel model = null)
        {
            return SpatialPlacementSolver.HasLineOfSight(a, b, model);
        }

        public List<CandidatePerceptionScore> RankCandidateLocations(List<GTA.Math.Vector3> candidatePositions, string requiredArchetype)
        {
            return _perceptionScanner.RankCandidates(candidatePositions, requiredArchetype, _store, _simulation.Recomputer);
        }

        public LocationSelectionResult DiscoverAndSelectLocation(string prompt, GTA.Math.Vector3? playerPos = null, float playerHeading = 0f)
        {
            return _locationResolver.ResolveLocation(prompt, playerPos, playerHeading);
        }

        public TeleportAnchorResult AnchorPhysicalContext(GTA.Math.Vector3 coords, float heading = 0f, bool waitCollision = true, bool fadeScreen = false)
        {
            return _teleportCoordinator.AnchorPhysicalContext(coords, heading, waitCollision, fadeScreen);
        }

        public WorldContextPacket BuildWorldContextPacket(LocationCandidate location, SpatialWorldModel model, string userQuery = "")
        {
            return WorldContextPacket.Build(location, model, _store, userQuery);
        }

        public SpatialValidationResult ValidateSpatialPlan(SpatialExecutionPlan plan, SpatialWorldModel model, SceneInteractionRequirements requirements = null, bool applyAutoCorrections = true)
        {
            return _planValidator.ValidatePlan(plan, model, requirements, applyAutoCorrections);
        }

        /// <summary>
        /// Executes the complete 11-Stage Spatial Orchestration Pipeline:
        /// Discover -> Choose Location -> Teleport Player -> Observe -> Model -> Contextualize -> Generate Intent -> Solve -> Validate -> Commit -> Realize
        /// Solves the "spawning while blind" architectural gap.
        /// </summary>
        public SpatialOrchestrationResult ExecuteOrchestrationPipeline(
            string userPrompt,
            GTA.Math.Vector3? playerOrigin = null,
            float playerHeading = 0f,
            bool autoRealize = true,
            SpatialWorldModel preScannedModel = null,
            LocationSelectionResult preResolvedLocation = null)
        {
            var result = new SpatialOrchestrationResult { Prompt = userPrompt };

            // Stage 1 & 2: Discover & Choose Macro-Location
            result.LocationResult = preResolvedLocation ?? _locationResolver.ResolveLocation(userPrompt, playerOrigin, playerHeading);
            var selectedLoc = result.LocationResult != null ? result.LocationResult.SelectedLocation : null;
            if (selectedLoc == null)
            {
                result.DiagnosticMessage = "Failed to resolve macro-location candidate.";
                return result;
            }

            // Stage 3: Teleport Player / Anchor Physical Context Frame (only if not pre-scanned on main thread)
            if (preScannedModel == null)
            {
                result.TeleportResult = _teleportCoordinator.AnchorPhysicalContext(selectedLoc.Coordinates, selectedLoc.Heading, waitCollision: false, fadeScreen: false);
            }
            else
            {
                result.TeleportResult = new TeleportAnchorResult
                {
                    Success = true,
                    AnchoredPosition = selectedLoc.Coordinates,
                    AnchoredHeading = selectedLoc.Heading,
                    CollisionLoaded = true
                };
            }

            // Stage 4 & 5: Observe & Model
            result.WorldModel = preScannedModel ?? _perceptionScanner.ScanSpatialWorldModel(selectedLoc.Coordinates, selectedLoc.DefaultRadius, _store, _simulation.Recomputer);
            result.Perception = result.WorldModel != null ? result.WorldModel.SemanticSnapshot : _perceptionScanner.ScanLocation(selectedLoc.Coordinates, selectedLoc.DefaultRadius, _store, _simulation.Recomputer);

            // Stage 6: Contextualize (World Context Packet)
            result.ContextPacket = WorldContextPacket.Build(selectedLoc, result.WorldModel, _store, userPrompt);

            // Stage 7: Generate Intent (LLM / Synthesizer guided by grounded WorldContextPacket)
            string sceneJson = _generator.GenerateSceneJsonFromPrompt(userPrompt, result.ContextPacket);
            result.GeneratedSceneJson = sceneJson;

            // Parse BuildEnvironmentData
            BuildEnvironmentPayload payload = _parser.ParseBuildEnvironment(sceneJson);
            if (payload == null || payload.Data == null)
            {
                result.DiagnosticMessage = "Failed to parse scene architect payload.";
                return result;
            }

            // Ensure environment name / location anchoring
            if (string.IsNullOrEmpty(payload.Data.Name) || payload.Data.Name.Equals("Custom Generated Environment", StringComparison.OrdinalIgnoreCase))
            {
                payload.Data.Name = selectedLoc.DisplayName;
            }

            // Stage 8: Solve Transforms (orientation, facing, clearances)
            SpatialExecutionPlan plan = _planner.PlanEnvironment(payload.Data, selectedLoc.Coordinates, selectedLoc.Heading);
            if (plan.WorldModel == null && result.WorldModel != null)
            {
                plan.WorldModel = result.WorldModel;
            }
            result.Plan = plan;

            // Stage 9: Validate (Hard Validation Barrier & Auto-Correction)
            result.ValidationResult = _planValidator.ValidatePlan(plan, result.WorldModel, null, applyAutoCorrections: true);
            plan.ValidationResult = result.ValidationResult;

            // Stage 10: Commit
            result.Environment = CommissionEnvironment(plan, payload.Data);

            // Stage 11: Realize
            if (autoRealize && result.Environment != null)
            {
                try
                {
                    Ped playerPed = null;
                    try { if (Game.Player != null) playerPed = Game.Player.Character; } catch {}
                    Realize(result.Environment.EnvironmentId, playerPed, payload.Data);
                }
                catch {}
            }

            result.Success = result.Environment != null;
            result.DiagnosticMessage = string.Format("Orchestration Pipeline Complete: Location='{0}', CandidateAreas={1}, Props={2}, NPCs={3}, Status='{4}'",
                selectedLoc.DisplayName,
                result.WorldModel != null ? result.WorldModel.CandidateAreas.Count : 0,
                plan.ResolvedProps.Count,
                plan.ResolvedNpcs.Count,
                result.Environment != null ? result.Environment.Status : "FAILED");

            return result;
        }

        public string GenerateSceneJsonFromNaturalLanguage(string prompt)
        {
            return _generator.GenerateSceneJsonFromPrompt(prompt);
        }

        public string GenerateDirectorJsonFromNaturalLanguage(string prompt, string worldContext = "")
        {
            return _generator.GenerateScenarioDirectorJsonFromPrompt(prompt, worldContext);
        }

        public string GenerateDirectorJsonFromNaturalLanguage(string prompt, KairosSituationBrief brief)
        {
            return _generator.GenerateScenarioDirectorJsonFromPrompt(prompt, brief);
        }

        public string GenerateDirectorJsonFromNaturalLanguage(string prompt, KairosSituationBrief brief, WorldPerceptionSnapshot perception)
        {
            return _generator.GenerateScenarioDirectorJsonFromPrompt(prompt, perception, brief);
        }

        public string GenerateEnvironmentJsonFromNaturalLanguage(string prompt)
        {
            return GenerateSceneJsonFromNaturalLanguage(prompt);
        }

        public string GenerateMutationOrEventFromNaturalLanguage(string prompt, string worldContext = "")
        {
            return GenerateDirectorJsonFromNaturalLanguage(prompt, worldContext);
        }

        public NpcRecord ExecuteSpawnNpcCommand(string rawJsonPayload)
        {
            ActionPayload payload = _parser.ParseAndValidate(rawJsonPayload);
            NpcRecord npc = _executor.ExecuteSpawnNpcAction(payload);
            _bridge.SyncAllNpcFactsToCustomRules(null, true);
            return npc;
        }

        /// <summary>
        /// Commissions an environment: writes the physical CanonicalPlan into PlanRepository,
        /// enrolls resident NPCs and metadata in WorldMemoryStore with Status = 'COMMISSIONED',
        /// flushes stores to disk, and synchronizes living facts to LivingLSAIs.
        /// Guaranteed to spawn ZERO GTA V entities (pure domain transaction).
        /// </summary>
        public EnvironmentRecord CommissionEnvironment(SpatialExecutionPlan plan, BuildEnvironmentData data = null)
        {
            if (plan == null) throw new ArgumentNullException("plan");
            if (string.IsNullOrEmpty(plan.EnvironmentId))
            {
                plan.EnvironmentId = "env_" + Guid.NewGuid().ToString().Substring(0, 8);
            }

            // 1. Authoritative physical persistence in PlanRepository
            if (_plans != null)
            {
                bool planSaved = _plans.SavePlan(plan);
                if (!planSaved)
                {
                    Console.WriteLine("[EngineCore] Warning: Failed to save CanonicalPlan to PlanRepository for {0}", plan.EnvironmentId);
                }
            }

            // 2. Authoritative narrative/world-state enrollment in WorldMemoryStore
            EnvironmentRecord env = _executor.ExecuteSpatialExecutionPlan(plan, data);

            // 3. Flush stores to disk
            if (_store != null)
            {
                _store.Flush();
            }

            // 4. Hot-sync living world facts to AI bridge
            if (_bridge != null)
            {
                _bridge.SyncAllNpcFactsToCustomRules(null, true);
            }

            return env;
        }

        /// <summary>
        /// Commissions an environment from raw JSON payload and placement coordinates.
        /// </summary>
        public EnvironmentRecord CommissionEnvironment(string rawJsonPayload, float originX, float originY, float originZ, float originHeading)
        {
            BuildEnvironmentPayload payload = _parser.ParseBuildEnvironment(rawJsonPayload);
            SpatialExecutionPlan plan = _planner.PlanEnvironment(payload.Data, new GTA.Math.Vector3(originX, originY, originZ), originHeading);
            return CommissionEnvironment(plan, payload.Data);
        }

        /// <summary>
        /// Authoritatively realizes a commissioned environment into the live GTA engine.
        /// Retrieves the immutable spatial blueprint from PlanRepository and mutable world state from WorldMemoryStore,
        /// physically manifests props, vehicles, NPCs, blips, navigation blockers, and audio,
        /// reconciles dynamic world state over the physical baseline, and updates Status to 'REALIZED'.
        /// </summary>
        public EnvironmentRecord Realize(string environmentId, Ped playerPed = null, BuildEnvironmentData data = null)
        {
            if (string.IsNullOrEmpty(environmentId)) return null;

            SpatialExecutionPlan plan = _plans != null ? _plans.GetPlan(environmentId) : null;
            EnvironmentRecord env = _store != null ? _store.GetEnvironment(environmentId) : null;

            if (plan == null && env != null && !string.IsNullOrEmpty(env.PlanId))
            {
                plan = _plans != null ? _plans.GetPlan(env.PlanId) : null;
            }

            if (plan == null)
            {
                throw new InvalidOperationException(string.Format("Cannot realize environment '{0}': No CanonicalPlan found in PlanRepository.", environmentId));
            }

            // If already realized and has live entities, return existing record (idempotent)
            if (env != null && string.Equals(env.Status, "REALIZED", StringComparison.OrdinalIgnoreCase) && _runtime != null && _runtime.HasRealizedEntities(environmentId))
            {
                return env;
            }

            // STAGE-BEFORE-COMMIT INVARIANT:
            // Wrap physical staging, dynamic reconciliation, and durable persistence in a transactional scope.
            // If any step throws or fails to persist, exiting the scope without Commit() automatically triggers
            // rollback deallocation via RuntimeExecutor.CleanUpEnvironment, ensuring zero orphaned entities.
            using (var scope = _runtime != null ? _runtime.BeginRealizationScope(environmentId) : null)
            {
                int propsCount = 0;
                int npcsCount = 0;
                int vehsCount = 0;

                // Collect deceased resident NPC identifiers so they are never respawned
                HashSet<string> deadEntityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (env != null && env.NpcIds != null && _store != null)
                {
                    foreach (var npcId in env.NpcIds)
                    {
                        var rec = _store.GetNpc(npcId);
                        if (rec != null && !rec.IsAlive)
                        {
                            if (!string.IsNullOrEmpty(rec.EntityId)) deadEntityIds.Add(rec.EntityId);
                            if (!string.IsNullOrEmpty(rec.Name)) deadEntityIds.Add(rec.Name);
                            deadEntityIds.Add(rec.NpcId.ToString());
                        }
                    }
                }

                if (_runtime != null)
                {
                    _runtime.RealizeEnvironmentFromPlan(
                        plan,
                        data,
                        playerPed,
                        env,
                        out propsCount,
                        out npcsCount,
                        out vehsCount,
                        deadEntityIds
                    );
                }

                // Reconcile dynamic narrative state from WorldMemoryStore
                if (env != null && _runtime != null)
                {
                    var liveNpcs = _runtime.GetNpcsForEnvironment(environmentId);
                    if (liveNpcs != null && env.NpcIds != null)
                    {
                        foreach (var npcId in env.NpcIds)
                        {
                            var npcRecord = _store.GetNpc(npcId);
                            if (npcRecord != null)
                            {
                                ActiveNpcContext ctx = null;
                                foreach (var c in liveNpcs)
                                {
                                    if (c != null && c.Record != null && (c.Record.NpcId == npcId || (!string.IsNullOrEmpty(npcRecord.EntityId) && string.Equals(c.EntityId, npcRecord.EntityId, StringComparison.OrdinalIgnoreCase))))
                                    {
                                        ctx = c;
                                        break;
                                    }
                                }

                                if (ctx != null)
                                {
                                    if (!npcRecord.IsAlive && ctx.PedInstance != null && ctx.PedInstance.Exists())
                                    {
                                        try { ctx.PedInstance.Kill(); } catch { }
                                    }
                                    else if (!string.IsNullOrEmpty(npcRecord.CurrentState))
                                    {
                                        ctx.LastAppliedBehaviorState = npcRecord.CurrentState;
                                    }
                                }
                            }
                        }
                    }
                }

                // Update persistent lifecycle state
                if (env != null)
                {
                    string prevStatus = env.Status;
                    DateTime? prevRealizedAt = env.LastRealizedAt;

                    env.Status = "REALIZED";
                    env.LastRealizedAt = DateTime.UtcNow;

                    if (_store != null)
                    {
                        try
                        {
                            _store.Save(true);
                        }
                        catch (Exception ex)
                        {
                            env.Status = prevStatus;
                            env.LastRealizedAt = prevRealizedAt;
                            Console.WriteLine(string.Format("[EngineCore] Stage-Before-Commit invariant triggered: Persistence failed during realization of '{0}': {1}", environmentId, ex.Message));
                            throw;
                        }
                    }
                }

                // Commit scope: physical staging, reconciliation, and disk persistence are all confirmed
                if (scope != null)
                {
                    scope.Commit();
                }
            }

            return env;
        }

        /// <summary>
        /// Derealizes an active environment from the live GTA engine.
        /// Harvests persistent runtime entity mutations back into WorldMemoryStore,
        /// deallocates all native props, npcs, vehicles, blips, navmesh blockers, and portals for this environment,
        /// and updates Status to 'DEREALIZED'.
        /// The authoritative CanonicalPlan remains preserved in PlanRepository on disk.
        /// </summary>
        public bool Derealize(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return false;

            EnvironmentRecord env = _store != null ? _store.GetEnvironment(environmentId) : null;

            // 1. Harvest live runtime state to WorldMemoryStore before entity disposal
            if (_runtime != null)
            {
                var liveNpcs = _runtime.GetNpcsForEnvironment(environmentId);
                if (liveNpcs != null && _store != null)
                {
                    foreach (var ctx in liveNpcs)
                    {
                        if (ctx != null && ctx.Record != null)
                        {
                            var storedNpc = _store.GetNpc(ctx.Record.NpcId);
                            if (storedNpc != null)
                            {
                                try
                                {
                                    if (ctx.CheckExists() && ctx.CheckIsDead())
                                    {
                                        _store.MarkNpcDead(storedNpc.NpcId, "Unknown", "Derealization casualty audit");
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }

            // 2. Update persistent lifecycle state
            if (env != null)
            {
                env.Status = "DEREALIZED";
                env.LastDerealizedAt = DateTime.UtcNow;
            }

            // 3. WRITE-BEFORE-DESTROY INVARIANT:
            // Ensure all semantic mutations (mortality, container loot, portal states, lifecycle)
            // are durably flushed to disk BEFORE any native entity is physically destroyed.
            // If disk persistence fails, abort immediately without destroying native entities!
            if (_store != null)
            {
                try
                {
                    _store.Save(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[EngineCore] Write-Before-Destroy invariant violation avoided! Persistence failed: " + ex.Message);
                    return false;
                }
            }

            // 4. Native Entity Destruction:
            // Only after durable persistence is guaranteed do we evict environment-scoped live runtime entities
            if (_runtime != null)
            {
                _runtime.CleanUpEnvironment(environmentId);
            }

            return true;
        }

        /// <summary>
        /// Decommissions an environment: derealizes any live runtime entities,
        /// deletes the physical plan from PlanRepository, deletes the environment record
        /// and associated NPC state from WorldMemoryStore, flushes stores, and updates the AI bridge.
        /// </summary>
        public bool DecommissionEnvironment(string envId)
        {
            if (string.IsNullOrEmpty(envId)) return false;

            Derealize(envId);

            bool planDeleted = false;
            if (_plans != null && _plans.HasPlan(envId))
            {
                planDeleted = _plans.DeletePlan(envId);
            }

            bool envDeleted = _store != null && _store.DeleteEnvironment(envId);

            if (_store != null)
            {
                _store.Flush();
            }

            if (_bridge != null)
            {
                _bridge.SyncAllNpcFactsToCustomRules(null, true);
            }

            return envDeleted || planDeleted;
        }

        // --- BACKWARD-COMPATIBLE ALIASES ---

        public EnvironmentRecord ExecuteBuildEnvironmentCommand(string rawJsonPayload, float originX, float originY, float originZ, float originHeading)
        {
            return CommissionEnvironment(rawJsonPayload, originX, originY, originZ, originHeading);
        }

        public EnvironmentRecord ExecuteBuildEnvironmentPlan(SpatialExecutionPlan plan, BuildEnvironmentData data = null)
        {
            return CommissionEnvironment(plan, data);
        }

        public bool ExecuteMutateEntityCommand(string rawJsonPayload, out NpcRecord modifiedNpc)
        {
            MutateEntityPayload payload = _parser.ParseMutateEntity(rawJsonPayload);
            bool ok = _executor.ExecuteMutateEntityAction(payload, out modifiedNpc);
            if (ok)
            {
                _bridge.SyncAllNpcFactsToCustomRules(null, true);
            }
            return ok;
        }

        public TriggerWorldEventPayload ParseTriggerWorldEventCommand(string rawJsonPayload)
        {
            return _parser.ParseTriggerWorldEvent(rawJsonPayload);
        }

        public bool ExecuteDirectScenarioCommand(string rawJsonPayload, string targetEnvironmentId = null)
        {
            DirectScenarioPayload payload = _parser.ParseDirectScenario(rawJsonPayload);
            return DirectScenario(payload.Data, targetEnvironmentId);
        }

        public bool DirectScenario(ScenarioDirectorData scenario, string targetEnvironmentId = null)
        {
            if (scenario == null) return false;
            if (_runtime != null)
            {
                return _runtime.DirectScenario(scenario, targetEnvironmentId);
            }
            return false;
        }

        public bool ExecuteClearEnvironmentCommand(string rawJsonPayload)
        {
            ClearEnvironmentPayload payload = _parser.ParseClearEnvironment(rawJsonPayload);
            if (payload == null || payload.Data == null || string.IsNullOrEmpty(payload.Data.EnvironmentId))
            {
                return false;
            }
            return DecommissionEnvironment(payload.Data.EnvironmentId);
        }

        public bool ClearEnvironment(string envId)
        {
            return DecommissionEnvironment(envId);
        }

        public string GetNpcFactForLivingLSAIs(int npcId)
        {
            return _store.FormatNpcContextForLivingLSAIs(npcId);
        }

        /// <summary>
        /// Retrieves a focused, lean, scoped world context for AI Director reasoning (Laws 1 & 4).
        /// Performs entity candidate scoring, relational pruning (omits third-party NPCs),
        /// and event log windowing to minimize token waste and eliminate hallucinations.
        /// </summary>
        public string GetScopedWorldFactSummary(string prompt, string environmentId = null, GTA.Math.Vector3? playerPosition = null)
        {
            if (_store == null) return string.Empty;
            var res = _retriever.RetrieveScopedContext(_store, prompt, environmentId, playerPosition);
            return res.FormattedContext;
        }

        public string GetScopedWorldFactSummary(string prompt, string environmentId, GTA.Math.Vector3? playerPosition, bool includePerception)
        {
            string context = GetScopedWorldFactSummary(prompt, environmentId, playerPosition);
            if (includePerception && playerPosition.HasValue)
            {
                var p = GetPerceptionSnapshot(playerPosition.Value.X, playerPosition.Value.Y, playerPosition.Value.Z);
                if (p != null)
                {
                    string pText = p.FormatForPrompt();
                    if (!string.IsNullOrEmpty(pText))
                    {
                        return string.IsNullOrEmpty(context) ? pText : (pText + "\n\n" + context);
                    }
                }
            }
            return context;
        }

        public string GetFullWorldFactSummary()
        {
            var npcs = _store.GetAllNpcs();
            var sb = new System.Text.StringBuilder();
            foreach (var npc in npcs)
            {
                sb.AppendLine(_store.FormatNpcContextForLivingLSAIs(npc.NpcId));
            }
            return sb.ToString();
        }

        public void Flush()
        {
            if (_store != null)
            {
                _store.Flush();
            }
        }
    }
}
