using System;
using System.Collections.Generic;
using GTA.Math;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Tests
{
    public class TestSpatialOrchestrationPipeline
    {
        private static int _passed = 0;
        private static int _failed = 0;

        private static void Assert(bool condition, string message)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("  [PASS] " + message);
            }
            else
            {
                _failed++;
                Console.WriteLine("  [FAIL] " + message);
            }
        }

        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine("  TEST SUITE: 11-Stage Spatial Orchestration Pipeline");
            Console.WriteLine("=================================================================");

            // Initialize registry
            SpatialAssetRegistry.Instance.InitializeRegistry("");

            TestMacroLocationDiscoveryAndSelection();
            TestPhysicalContextTeleportAnchoring();
            TestSpatialWorldModelProbingAndCandidateAreas();
            TestWorldContextPacketSynthesis();
            TestOrientationFirstIntentSolving();
            TestHardSpatialValidationGateAndCorrections();
            TestFull11StageOrchestrationPipeline();
            TestPipelineHeadlessRobustness();

            Console.WriteLine();
            Console.WriteLine(string.Format("Results: {0} passed, {1} failed.", _passed, _failed));
            Console.WriteLine("=================================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void TestMacroLocationDiscoveryAndSelection()
        {
            Console.WriteLine("\n--- Scenario 1: Macro-Location Discovery & Selection (Stages 1 & 2) ---");

            var resolver = new LocationSelectionResolver();

            // Del Perro Beach matching
            var resBeach = resolver.ResolveLocation("Set up a secret meeting with the president at Del Perro Beach");
            Assert(resBeach != null, "LocationSelectionResult is non-null for beach prompt");
            Assert(resBeach.SelectedLocation != null, "SelectedLocation is resolved");
            Assert(resBeach.SelectedLocation.DisplayName.IndexOf("Del Perro", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   resBeach.SelectedLocation.LocationId.IndexOf("del_perro", StringComparison.OrdinalIgnoreCase) >= 0,
                   "Selected location is Del Perro Beach");
            Assert(resBeach.SelectedLocation.Category == "beach", "Selected location category is 'beach'");
            Assert(resBeach.IsExplicitTarget, "Explicit landmark prompt flagged as explicit target");

            // Drag strip matching
            var resDrag = resolver.ResolveLocation("Illegal midnight drag race at Cypress Flats");
            Assert(resDrag.SelectedLocation != null, "SelectedLocation resolved for drag race");
            Assert(resDrag.SelectedLocation.DisplayName.IndexOf("Cypress", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   resDrag.SelectedLocation.LocationId.IndexOf("cypress", StringComparison.OrdinalIgnoreCase) >= 0,
                   "Selected location is Cypress Flats");
            Assert(resDrag.SelectedLocation.SupportsVehicles, "Cypress Flats supports vehicles");

            // Interior Penthouse matching
            var resIndoor = resolver.ResolveLocation("Underground high-stakes poker tournament in the casino penthouse");
            Assert(resIndoor.SelectedLocation != null, "SelectedLocation resolved for indoor poker");
            Assert(resIndoor.SelectedLocation.IsInterior, "Penthouse/casino location correctly classified as interior");
        }

        private static void TestPhysicalContextTeleportAnchoring()
        {
            Console.WriteLine("\n--- Scenario 2: Physical Context Teleport Anchoring (Stage 3) ---");

            var coordinator = new PlayerTeleportCoordinator();
            Vector3 target = new Vector3(-1500.0f, -1190.0f, 2.5f);
            float heading = 140.0f;

            Assert(!coordinator.IsAnchored, "Coordinator begins unanchored");

            var result = coordinator.AnchorPhysicalContext(target, heading, waitCollision: true, fadeScreen: false);
            Assert(result != null, "TeleportAnchorResult is non-null");
            Assert(result.Success, "AnchorPhysicalContext reports success");
            Assert(coordinator.IsAnchored, "Coordinator is marked anchored");
            Assert(coordinator.CurrentAnchorPosition == target, "CurrentAnchorPosition matches target");
            Assert(coordinator.CurrentAnchorHeading == heading, "CurrentAnchorHeading matches target heading");
            Assert(!string.IsNullOrEmpty(result.DiagnosticMessage), "Diagnostic message logged");
        }

        private static void TestSpatialWorldModelProbingAndCandidateAreas()
        {
            Console.WriteLine("\n--- Scenario 3: Spatial World Model Probing & Candidate Areas (Stages 4 & 5) ---");

            var sampler = new SpatialGridSampler();
            Vector3 beachCoords = new Vector3(-1390.5f, -1400.2f, 2.5f);
            var model = sampler.SampleArea(beachCoords, 50.0f, 5.0f, null, "Del Perro Beach Sands");

            Assert(model != null, "SpatialWorldModel successfully sampled");
            Assert(model.Surfaces.Count > 0, "Model contains sampled surfaces");
            Assert(model.CandidateAreas.Count >= 2, "Model clusters at least 2 distinct Candidate Areas");

            var areaA = model.CandidateAreas.Find(a => a.AreaId == "Area_A");
            Assert(areaA != null, "Candidate Area_A identified");
            Assert(areaA.SurfaceType.Equals("SAND", StringComparison.OrdinalIgnoreCase), "Area_A surface is SAND");
            Assert(areaA.SlopeDegrees < 5.0f, "Area_A has flat walkable slope");
            Assert(areaA.SeclusionScore >= 0.5f, "Area_A has high seclusion score");
            Assert(areaA.SuitabilityTags.Contains("secluded_meeting") || areaA.SuitabilityTags.Contains("party_bonfire"), "Area_A has meeting/bonfire suitability tags");
        }

        private static void TestWorldContextPacketSynthesis()
        {
            Console.WriteLine("\n--- Scenario 4: Scoped World Context Packet Synthesis (Stage 6) ---");

            var loc = new LocationCandidate
            {
                LocationId = "loc_del_perro_beach",
                DisplayName = "Del Perro Beach Sands",
                Category = "beach",
                Coordinates = new Vector3(-1500.0f, -1190.0f, 2.5f),
                Heading = 140.0f
            };

            var sampler = new SpatialGridSampler();
            var model = sampler.SampleArea(loc.Coordinates, 50.0f, 5.0f, null, loc.DisplayName);

            var packet = WorldContextPacket.Build(loc, model, null, "secret meeting with the president");
            Assert(packet != null, "WorldContextPacket is non-null");
            Assert(packet.Location == loc, "Packet binds correct LocationCandidate");
            Assert(packet.CandidateAreas.Count >= 2, "Packet includes CandidateAreas");
            Assert(packet.HardConstraints.Count > 0, "Packet includes physical constraints");
            Assert(packet.RecommendedAssets.Count > 0, "Packet provides recommended authentic GTA assets");

            string brief = packet.FormatForPrompt(includeAsciiMap: true);
            Assert(!string.IsNullOrEmpty(brief), "FormatForPrompt emits non-empty string");
            Assert(brief.Contains("=== GROUNDED SPATIAL CONTEXT BRIEF ==="), "Brief contains standardized header");
            Assert(brief.Contains("[Area_A]"), "Brief exposes Candidate Area_A");
            Assert(brief.Contains("[Area_B]"), "Brief exposes Candidate Area_B");
            Assert(brief.Length < 3000, string.Format("Brief is token-efficient (length {0} chars < 3000 chars / ~500 tokens)", brief.Length));
        }

        private static void TestOrientationFirstIntentSolving()
        {
            Console.WriteLine("\n--- Scenario 5: Orientation-First Intent Solving within Candidate Area (Stages 7 & 8) ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1390.5f, -1400.2f, 2.5f);
            var model = sampler.SampleArea(origin, 50.0f, 5.0f, null, "Del Perro Beach Sands");

            var solver = new SpatialPlacementSolver();
            var req = new SceneInteractionRequirements
            {
                Pattern = "FaceToFaceMeeting",
                TargetAreaId = "Area_A",
                ParticipantCount = 2,
                Clearance = 2.0f,
                RequireLineOfSight = true
            };

            var solution = solver.Solve(model, req);
            Assert(solution != null, "SpatialPlacementSolver produces solution");
            Assert(solution.FeasibilityConfirmed, "Solution feasibility confirmed");
            Assert(solution.Props.Count >= 3, "Solution places focal table + 2 chairs");

            // Verify facing relationships between chairs
            var c1 = solution.Props.Find(p => p.PropId.Contains("seat_principal"));
            var c2 = solution.Props.Find(p => p.PropId.Contains("seat_player"));
            Assert(c1 != null && c2 != null, "Both opposing chairs placed");

            float headingDiff = Math.Abs(c1.Rotation.Z - c2.Rotation.Z);
            if (headingDiff > 180f) headingDiff = 360f - headingDiff;
            Assert(Math.Abs(headingDiff - 180.0f) < 5.0f, string.Format("Chairs have opposing 180 deg differential (actual diff: {0:F1} deg)", headingDiff));
        }

        private static void TestHardSpatialValidationGateAndCorrections()
        {
            Console.WriteLine("\n--- Scenario 6: Hard Spatial Validation Gate & Auto-Corrector (Stage 9) ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1390.5f, -1400.2f, 2.5f);
            var model = sampler.SampleArea(origin, 50.0f, 5.0f, null, "Del Perro Beach Sands");

            var validator = new SpatialPlanValidator();
            var plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_test_val",
                Name = "Validation Test Plan"
            };

            // Inject floating prop (+1.2m above ground)
            plan.ResolvedProps.Add(new ResolvedPropPlacement
            {
                PropId = "floating_table",
                ModelName = "prop_table_01",
                WorldPosition = new Vector3(-1390.5f, -1400.2f, 3.7f) // ground is at ~2.5m
            });

            // Inject two overlapping props (10cm apart)
            plan.ResolvedProps.Add(new ResolvedPropPlacement
            {
                PropId = "chair_a",
                ModelName = "prop_chair_02",
                WorldPosition = new Vector3(-1385.0f, -1400.0f, 2.5f)
            });
            plan.ResolvedProps.Add(new ResolvedPropPlacement
            {
                PropId = "chair_b",
                ModelName = "prop_chair_02",
                WorldPosition = new Vector3(-1385.05f, -1400.0f, 2.5f) // 5cm overlap!
            });

            // Inject two participants facing opposite directions away from each other
            plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
            {
                EntityId = "npc_president",
                Name = "President",
                WorldPosition = new Vector3(-1390.0f, -1400.0f, 2.5f),
                Heading = 0f // Facing north instead of towards interlocutor south
            });
            plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
            {
                EntityId = "npc_player",
                Name = "Agent",
                WorldPosition = new Vector3(-1390.0f, -1402.0f, 2.5f),
                Heading = 180f // Facing south instead of towards interlocutor north
            });

            var req = new SceneInteractionRequirements
            {
                Pattern = "FaceToFaceMeeting",
                ParticipantCount = 2,
                RequireLineOfSight = true
            };

            // Run validation with auto-correction enabled
            var valResult = validator.ValidatePlan(plan, model, req, applyAutoCorrections: true);
            Assert(valResult != null, "SpatialValidationResult is non-null");
            Assert(valResult.AutoCorrectionsApplied.Count >= 3, string.Format("Auto-corrections applied: {0}", valResult.AutoCorrectionsApplied.Count));

            // Verify floating prop was clamped to ground elevation
            float correctedZ = plan.ResolvedProps[0].WorldPosition.Z;
            Assert(Math.Abs(correctedZ - 2.30f) <= 0.1f, string.Format("Floating prop Z clamped from 3.7 to {0:F2}", correctedZ));

            // Verify overlapping props were nudged apart
            float separatedDist = ((Vector3)plan.ResolvedProps[1].WorldPosition).DistanceTo(plan.ResolvedProps[2].WorldPosition);
            Assert(separatedDist >= 0.4f, string.Format("Overlapping props separated to {0:F2}m", separatedDist));

            // Verify facing headings were aligned to interlocutors
            Assert(valResult.IsValid, "Plan passed validation barrier after auto-corrections");
        }

        private static void TestFull11StageOrchestrationPipeline()
        {
            Console.WriteLine("\n--- Scenario 7: Full 11-Stage End-to-End Orchestration Pipeline (Stages 1-11) ---");

            var engine = new EngineCore();
            string prompt = "Set up a secret meeting with the president at Del Perro Beach";

            var result = engine.ExecuteOrchestrationPipeline(prompt, null, 0f, autoRealize: false);

            Assert(result != null, "SpatialOrchestrationResult is non-null");
            Assert(result.Success, "ExecuteOrchestrationPipeline succeeded");
            Assert(result.LocationResult != null && result.LocationResult.SelectedLocation != null, "Stage 1 & 2: Macro-location resolved");
            Assert(result.TeleportResult != null && result.TeleportResult.Success, "Stage 3: Physical context anchored");
            Assert(result.WorldModel != null, "Stage 4 & 5: Physical World Model generated");
            Assert(result.ContextPacket != null, "Stage 6: World Context Packet synthesized");
            Assert(!string.IsNullOrEmpty(result.GeneratedSceneJson), "Stage 7: Scene Intent JSON generated");
            Assert(result.Plan != null, "Stage 8: SpatialExecutionPlan produced");
            Assert(result.Plan.ValidationResult != null && result.Plan.ValidationResult.IsValid, "Stage 9: Hard Spatial Validation Gate passed");
            Assert(result.Environment != null, "Stage 10: Environment committed to storage");
            Assert(result.Environment.Status == "COMMISSIONED", "Environment enrolled with COMMISSIONED status");

            // Verify durable persistence in PlanRepository
            Assert(engine.Plans.HasPlan(result.Plan.EnvironmentId), "Plan durably persisted in PlanRepository");
            Assert(engine.Store.GetEnvironment(result.Environment.EnvironmentId) != null, "Environment enrolled in WorldMemoryStore");
        }

        private static void TestPipelineHeadlessRobustness()
        {
            Console.WriteLine("\n--- Scenario 8: Headless Robustness & Edge-Case Resilience ---");

            var engine = new EngineCore();

            // Edge case 1: Open ground prompt with no specific landmark named
            var resGeneric = engine.ExecuteOrchestrationPipeline("Tactical arms deal in a secluded alleyway", autoRealize: false);
            Assert(resGeneric != null && resGeneric.Success, "Generic prompt without landmark executes cleanly");
            Assert(resGeneric.Plan != null, "Generic prompt produces valid execution plan");

            // Edge case 2: Empty/minimal prompt fallback
            var resMinimal = engine.ExecuteOrchestrationPipeline("briefing", autoRealize: false);
            Assert(resMinimal != null && resMinimal.Success, "Minimal single-word prompt executes without throwing");

            // Edge case 3: Direct API queries
            var locRes = engine.DiscoverAndSelectLocation("beach party");
            Assert(locRes != null && locRes.SelectedLocation != null, "DiscoverAndSelectLocation works as standalone API");

            var anchorRes = engine.AnchorPhysicalContext(new Vector3(100f, 200f, 30f));
            Assert(anchorRes != null && anchorRes.Success, "AnchorPhysicalContext works as standalone API");
        }
    }
}
