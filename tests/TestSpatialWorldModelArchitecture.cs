using System;
using System.Collections.Generic;
using GTA.Math;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Tests
{
    public class TestSpatialWorldModelArchitecture
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
            Console.WriteLine("  TEST SUITE: Spatial World Model, Candidate Areas & Solver");
            Console.WriteLine("=================================================================");

            TestSpatialGridSamplingAndSurfaces();
            TestGeometryClusteringAndCandidateAreas();
            TestSpatialGraphConstructionAndQueries();
            TestFaceToFaceMeetingPlacementSolver();
            TestCircularBonfireGatheringSolver();
            TestVisibilityAndLineOfSight();
            TestAsciiMapRenderingAndObjectInspector();
            TestEngineCoreAndPlannerPipelineIntegration();

            Console.WriteLine();
            Console.WriteLine(string.Format("Results: {0} passed, {1} failed.", _passed, _failed));
            Console.WriteLine("=================================================================");

            return _failed == 0 ? 0 : 1;
        }

        private static void TestSpatialGridSamplingAndSurfaces()
        {
            Console.WriteLine("\n--- Test 1: Spatial Grid Sampling & Surface Extraction ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            Assert(model != null, "SpatialGridSampler produces non-null SpatialWorldModel");
            Assert(model.Surfaces.Count >= 20, string.Format("Grid sampling generates surface patches (count: {0})", model.Surfaces.Count));

            bool hasSand = false;
            bool hasWater = false;
            bool hasConcrete = false;
            bool hasAsphalt = false;

            foreach (var s in model.Surfaces)
            {
                if (s.SurfaceType == "SAND") hasSand = true;
                if (s.SurfaceType == "WATER") hasWater = true;
                if (s.SurfaceType == "CONCRETE") hasConcrete = true;
                if (s.SurfaceType == "ASPHALT") hasAsphalt = true;
            }

            Assert(hasSand, "Sampled model contains sandy beach surface");
            Assert(hasWater, "Sampled model contains water surface");
            Assert(hasConcrete, "Sampled model contains concrete walkway surface");
            Assert(hasAsphalt, "Sampled model contains asphalt roadway surface");
        }

        private static void TestGeometryClusteringAndCandidateAreas()
        {
            Console.WriteLine("\n--- Test 2: Geometry Clustering & Candidate Area Detection ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            Assert(model.CandidateAreas.Count >= 2, string.Format("Discovered distinct Candidate Areas (count: {0})", model.CandidateAreas.Count));

            var areaA = model.FindCandidateArea("Area_A");
            Assert(areaA != null, "Area_A discovered in Del Perro Beach model");
            Assert(areaA.SurfaceType == "SAND", "Area_A surface is classified as SAND");
            Assert(areaA.Dimensions.X >= 15.0f && areaA.Dimensions.Y >= 10.0f, string.Format("Area_A has ample clear footprint ({0:F0}m x {1:F0}m)", areaA.Dimensions.X, areaA.Dimensions.Y));
            Assert(areaA.SeclusionScore >= 0.70f, string.Format("Area_A beachfront has high seclusion score ({0:F2})", areaA.SeclusionScore));
            Assert(areaA.SuitabilityTags.Contains("secluded_meeting"), "Area_A tagged as suitable for 'secluded_meeting'");
            Assert(areaA.SuitabilityTags.Contains("party_bonfire"), "Area_A tagged as suitable for 'party_bonfire'");

            var areaB = model.FindCandidateArea("Area_B");
            Assert(areaB != null, "Area_B (Boardwalk Walkway) discovered");
            Assert(areaB.SurfaceType == "CONCRETE", "Area_B surface is CONCRETE");
            Assert(areaB.PedestrianAccess, "Area_B has pedestrian access");
        }

        private static void TestSpatialGraphConstructionAndQueries()
        {
            Console.WriteLine("\n--- Test 3: 3D Spatial Graph Construction & Querying ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            var g = model.Graph;
            Assert(g != null && g.Nodes.Count >= 3, string.Format("Spatial graph populated with nodes (count: {0})", g.Nodes.Count));
            Assert(g.Edges.Count >= 2, string.Format("Spatial graph populated with relational edges (count: {0})", g.Edges.Count));

            Assert(g.Nodes.ContainsKey("Area_A"), "Spatial graph contains Area_A node");
            Assert(g.Nodes.ContainsKey("boardwalk_walkway"), "Spatial graph contains boardwalk_walkway node");

            var neighbors = g.GetNeighbors("Area_A");
            Assert(neighbors.Count > 0, string.Format("Area_A has connected/adjacent topological neighbors (count: {0})", neighbors.Count));

            bool approachEdgeExists = false;
            foreach (var e in g.Edges)
            {
                if (e.RelationshipType == "APPROACHABLE_FROM" || e.RelationshipType == "CONNECTED_TO")
                {
                    approachEdgeExists = true;
                    break;
                }
            }
            Assert(approachEdgeExists, "Spatial graph maintains APPROACHABLE_FROM / CONNECTED_TO relationships");
        }

        private static void TestFaceToFaceMeetingPlacementSolver()
        {
            Console.WriteLine("\n--- Test 4: Orientation-First Placement Solver (Meeting Pattern) ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            var solver = new SpatialPlacementSolver();
            var req = new SceneInteractionRequirements
            {
                Pattern = "FaceToFaceMeeting",
                TargetAreaId = "Area_A",
                FocalAsset = "prop_table_01",
                SeatingAsset = "prop_chair_01",
                ParticipantCount = 2
            };

            var solution = solver.Solve(model, req);
            Assert(solution != null, "Solver produced non-null SpatialSceneSolution");
            Assert(solution.SelectedAreaId == "Area_A", "Solver anchored meeting inside requested Area_A");
            Assert(solution.Props.Count >= 3, string.Format("Solver placed props (count: {0}, expected >= 3: table + 2 chairs)", solution.Props.Count));

            var table = solution.Props.Find(p => p.PropId == "focal_table");
            var chair1 = solution.Props.Find(p => p.PropId == "seat_principal");
            var chair2 = solution.Props.Find(p => p.PropId == "seat_player");

            Assert(table != null, "Focal table placed at scene center");
            Assert(chair1 != null && chair2 != null, "Both principal and player chairs placed");

            // Verify opposing orientations (180 degree difference)
            float diffHeading = Math.Abs(chair1.Rotation.Z - chair2.Rotation.Z);
            if (diffHeading > 180f) diffHeading = 360f - diffHeading;
            Assert(Math.Abs(diffHeading - 180.0f) < 1.0f, string.Format("Chairs face each other with exact opposing heading (diff: {0:F1}°)", diffHeading));

            // Verify clearance buffers
            Assert(chair1.ClearanceFront >= 1.0f && chair1.ClearanceBack >= 1.0f, "Principal chair has adequate clearance buffers");

            // Verify Security Guard post
            var guard = solution.Npcs.Find(n => n.Role == "Security Guard");
            Assert(guard != null, "Security Guard post created");
            Assert(guard.Position.DistanceTo(table.Position) >= 3.0f, "Guard positioned at security perimeter (> 3m from meeting table)");
        }

        private static void TestCircularBonfireGatheringSolver()
        {
            Console.WriteLine("\n--- Test 5: Radial Placement Solver (Bonfire Gathering Pattern) ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            var solver = new SpatialPlacementSolver();
            var req = new SceneInteractionRequirements
            {
                Pattern = "CircularGathering",
                TargetAreaId = "Area_A",
                FocalAsset = "prop_beach_fire",
                SeatingAsset = "prop_chair_02",
                ParticipantCount = 8
            };

            var solution = solver.Solve(model, req);
            Assert(solution != null, "Radial gathering solution generated");

            var fire = solution.Props.Find(p => p.PropId == "focal_centerpiece");
            Assert(fire != null, "Focal centerpiece (bonfire) placed at center");

            // Verify 8 radial chairs
            int radialSeatCount = 0;
            foreach (var p in solution.Props)
            {
                if (p.PropId.StartsWith("seat_radial_"))
                {
                    radialSeatCount++;
                    // Check that distance to center fire is ~3.0m
                    float dist = p.Position.DistanceTo(fire.Position);
                    Assert(Math.Abs(dist - 3.0f) < 0.2f, string.Format("Radial seat {0} positioned on 3.0m radius circle (dist: {1:F2}m)", p.PropId, dist));

                    // Check inward-facing heading: heading should point towards center
                    float expectedHeading = SpatialPlacementSolver.ComputeFacingHeading(p.Position, fire.Position);
                    float hDiff = Math.Abs(p.Rotation.Z - expectedHeading);
                    if (hDiff > 180f) hDiff = 360f - hDiff;
                    Assert(hDiff < 1.0f, string.Format("Radial seat {0} faces directly inward towards fire (heading: {1:F1}°)", p.PropId, p.Rotation.Z));
                }
            }
            Assert(radialSeatCount == 8, string.Format("Exactly 8 radial seats created (count: {0})", radialSeatCount));

            // Verify auxiliary cooler table placed outside fire danger radius
            var cooler = solution.Props.Find(p => p.PropId == "party_cooler_table");
            Assert(cooler != null, "Cooler/supply table placed");
            Assert(cooler.Position.DistanceTo(fire.Position) >= 5.0f, "Cooler table offset safely outside fire hazard radius (> 5.0m)");
        }

        private static void TestVisibilityAndLineOfSight()
        {
            Console.WriteLine("\n--- Test 6: Visibility & Line-of-Sight Reasoning ---");

            var model = new SpatialWorldModel();

            Vector3 viewerPos = new Vector3(0f, 0f, 1f);
            Vector3 targetVisible = new Vector3(0f, 10f, 1f);
            Vector3 targetBehindWall = new Vector3(0f, 20f, 1f);

            // Add an obstacle wall directly in between viewer and targetBehindWall
            model.Obstacles.Add(new SpatialObstacle
            {
                Id = "concrete_wall",
                Position = new Vector3(0f, 15f, 1f),
                Dimensions = new Vector3(8f, 1.5f, 3f),
                ObstacleType = "WALL",
                BlocksLineOfSight = true
            });

            // 1. Line of Sight test
            bool los1 = SpatialPlacementSolver.HasLineOfSight(viewerPos, targetVisible, model);
            Assert(los1, "Unobstructed line of sight evaluates to TRUE");

            bool los2 = SpatialPlacementSolver.HasLineOfSight(viewerPos, targetBehindWall, model);
            Assert(!los2, "Line of sight blocked by wall obstacle evaluates to FALSE");

            // 2. Field of View (CanSee) test
            // Viewer facing North (0 deg) looking at target North (0 deg)
            bool canSeeFront = SpatialPlacementSolver.CanSee(viewerPos, 0f, targetVisible, 120.0f, model);
            Assert(canSeeFront, "Target in front within 120° FOV is visible");

            // Viewer facing South (180 deg) looking at target North (0 deg) -> Target is behind viewer!
            bool canSeeBehind = SpatialPlacementSolver.CanSee(viewerPos, 180f, targetVisible, 120.0f, model);
            Assert(!canSeeBehind, "Target behind viewer (180° away) is NOT visible in 120° FOV");
        }

        private static void TestAsciiMapRenderingAndObjectInspector()
        {
            Console.WriteLine("\n--- Test 7: ASCII Spatial Map Generation & Object Inspector ---");

            var sampler = new SpatialGridSampler();
            Vector3 origin = new Vector3(-1450.0f, -1000.0f, 2.3f);
            var model = sampler.SampleArea(origin, 40.0f, 5.0f, null, "Del Perro Beach");

            string asciiMap = SpatialWorldModelRenderer.RenderAsciiMap(model, "DEL PERRO BEACH - TEST MAP", 30, 15);
            Assert(!string.IsNullOrEmpty(asciiMap), "RenderAsciiMap returns non-empty string");
            Assert(asciiMap.Contains("DEL PERRO BEACH"), "ASCII map contains header title");
            Assert(asciiMap.Contains("[NORTH ^]"), "ASCII map contains compass indicator");
            Assert(asciiMap.Contains("[A]"), "ASCII map contains Candidate Area [A] marker");
            Assert(asciiMap.Contains("CANDIDATE REGIONS:"), "ASCII map contains candidate regions legend");

            // Object Inspector test
            string inspection = SpatialWorldModelRenderer.InspectObject(
                "presidential_chair_01",
                "prop_chair_01",
                new Vector3(-1450f, -998.8f, 2.3f),
                new Vector3(0f, 0f, 91.4f),
                "SAND",
                1.8f, 0.9f, 0.7f, 0.8f,
                "focal_table",
                new List<string> { "FACES focal_table", "OPPOSES seat_player" }
            );

            Assert(!string.IsNullOrEmpty(inspection), "InspectObject produces non-empty report");
            Assert(inspection.Contains("OBJECT INSPECTOR: presidential_chair_01"), "Report contains object id");
            Assert(inspection.Contains("Heading: 91.4°"), "Report contains heading angle");
            Assert(inspection.Contains("Front: 1.8m"), "Report contains clearance dimensions");
            Assert(inspection.Contains("Facing:      focal_table"), "Report contains facing target");
            Assert(inspection.Contains("OPPOSES seat_player"), "Report contains spatial relationships");
        }

        private static void TestEngineCoreAndPlannerPipelineIntegration()
        {
            Console.WriteLine("\n--- Test 8: EngineCore & SpatialWorldPlanner Pipeline Integration ---");

            var core = new EngineCore();
            Assert(core.PlacementSolver != null, "EngineCore exposes PlacementSolver");

            var model = core.GetSpatialWorldModel(-1450.0f, -1000.0f, 2.3f, 40.0f);
            Assert(model != null, "EngineCore.GetSpatialWorldModel returns valid model");
            Assert(model.CandidateAreas.Count >= 2, "EngineCore model contains candidate areas");

            string mapStr = core.RenderSpatialAsciiMap(model);
            Assert(!string.IsNullOrEmpty(mapStr), "EngineCore.RenderSpatialAsciiMap produces ASCII map");

            // Test SpatialWorldPlanner with face-to-face meeting prompt
            var planner = new SpatialWorldPlanner();
            var data = new BuildEnvironmentData
            {
                Name = "Presidential Beach Summit",
                Archetype = "diplomatic_meeting",
                OriginalPrompt = "Set up a private meeting with the president at Del Perro Beach with two chairs and a table"
            };

            var plan = planner.PlanEnvironment(data, new Vector3(-1450.0f, -1000.0f, 2.3f), 0f);
            Assert(plan != null, "Planner produced non-null SpatialExecutionPlan");
            Assert(plan.WorldModel != null, "Plan contains populated SpatialWorldModel");
            Assert(plan.SceneSolution != null, "Plan contains solved SceneSolution");
            Assert(plan.SceneSolution.Pattern == "FaceToFaceMeeting", "Plan detected FaceToFaceMeeting pattern");
            Assert(plan.ResolvedProps.Count >= 2, string.Format("Plan contains resolved props grounded by solver (count: {0})", plan.ResolvedProps.Count));
        }
    }
}
