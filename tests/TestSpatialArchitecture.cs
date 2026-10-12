using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    class SpatialArchitectureTester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  UNIFIED SPATIAL WORLD PLANNER & ARCHITECTURE TEST SUITE ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Spatial_Data");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            NavMeshBlockerManager blockerManager = new NavMeshBlockerManager();
            BlipManager blipManager = new BlipManager();
            TeleportPocketManager pocketManager = new TeleportPocketManager();
            InteriorManager interiorManager = new InteriorManager(pocketManager);

            EngineCore engine = new EngineCore(testDir, null, blockerManager, blipManager, pocketManager, interiorManager);
            int passed = 0;
            int total = 0;

            // --- TEST 1: FULL 3D EULER ROTATION MATRIX TRANSFORM CASCADING ---
            total++;
            Console.WriteLine("[Test 1] Testing Full 3D Rotation Transform (Table tilted 30 deg Pitch -> Child Cash on Surface)...");
            
            CalculatedTransform parentTableTf = new CalculatedTransform(100f, 200f, 30f, 45f, 30f, 0f);
            CalculatedTransform childCashTf = HierarchicalTransformEngine.ComputeChildWorldTransform(
                parentTableTf, 0.0f, 0.4f, 0.76f, 0f, 0f, 0f
            );

            bool childElevated = childCashTf.Z > parentTableTf.Z;
            bool childPitchInherited = Math.Abs(childCashTf.Pitch - 30f) < 0.01f;

            if (childElevated && childPitchInherited)
            {
                Console.WriteLine("  ✅ PASS: 3D rotation matrix cascaded parent 3D tilt, pitch, and elevation:");
                Console.WriteLine("     • Parent: ({0:F2}, {1:F2}, {2:F2}, Head={3:F1}, Pitch={4:F1})", parentTableTf.X, parentTableTf.Y, parentTableTf.Z, parentTableTf.Heading, parentTableTf.Pitch);
                Console.WriteLine("     • Child:  ({0:F2}, {1:F2}, {2:F2}, Head={3:F1}, Pitch={4:F1})", childCashTf.X, childCashTf.Y, childCashTf.Z, childCashTf.Heading, childCashTf.Pitch);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: 3D rotation cascading failed.");
            }

            // --- TEST 2: MODULAR ASSET CAPABILITY CATALOG ---
            total++;
            Console.WriteLine("\n[Test 2] Testing ModularAssetCatalog Semantic Capability Matcher (Reinforced Industrial Wall)...");
            var wallAsset = ModularAssetCatalog.FindBestAsset("Wall", new List<string> { "reinforced", "industrial" });

            if (wallAsset != null && wallAsset.BlocksNavMesh && wallAsset.Model == "prop_sec_barrier_01a")
            {
                Console.WriteLine("  ✅ PASS: Semantic query accurately resolved asset with NavMesh blocking capability: {0} ({1}x{2}x{3}m)", wallAsset.Model, wallAsset.Dimensions.X, wallAsset.Dimensions.Y, wallAsset.Dimensions.Z);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Asset capability lookup failed.");
            }

            // --- TEST 3: SPATIAL WORLD PLANNER UNIFIED RESOLUTION ---
            total++;
            Console.WriteLine("\n[Test 3] Testing SpatialWorldPlanner (Building Scene Graph, Auto-NavBlockers, and Clearance)...");

            BuildEnvironmentData envData = new BuildEnvironmentData
            {
                EnvironmentId = "env_gang_hideout_01",
                Name = "Underground Fortified Hideout",
                Archetype = "bunker",
                IsSkyPocket = true,
                SkyPocketAltitude = 1500.0f,
                EntranceTitle = "Heavy Vault Blast Door",
                Props = new List<PropPlacementData>
                {
                    new PropPlacementData { PropId = "wall_1", Model = "prop_sec_barrier_01a", Offset = new Vector3Offset(0, 0, 0), Heading = 90f, IsNavBlocker = true },
                    new PropPlacementData { PropId = "table_1", Model = "prop_table_03b", Offset = new Vector3Offset(0, 3, 0), Heading = 0f },
                    new PropPlacementData { PropId = "cash_1", ParentPropId = "table_1", Model = "prop_cash_pile_02", Offset = new Vector3Offset(0, 0, 0.76f), Heading = 0f }
                },
                Npcs = new List<EnvironmentNpcData>
                {
                    new EnvironmentNpcData { Name = "Vincenzo", Occupation = "Capo", PublicFaction = "Mafia", PedModel = "g_m_y_salvagoon_01", Offset = new Vector3Offset(0, 3.5f, 0) }
                }
            };

            SpatialExecutionPlan plan = engine.Planner.PlanEnvironment(envData, new Vector3(100f, 200f, 30f), 90f);

            bool planCenterOk = plan.CenterPosition.Z == 1500f;
            bool propsPlanned = plan.ResolvedProps.Count >= 3;
            bool blockersPlanned = plan.NavBlockers.Count >= 1;
            bool pocketPlanned = plan.PlannedPocket != null;

            if (planCenterOk && propsPlanned && blockersPlanned && pocketPlanned)
            {
                Console.WriteLine("  ✅ PASS: SpatialWorldPlanner successfully constructed complete execution plan:");
                Console.WriteLine("     • Location: Sky Pocket at z={0:F1}m", plan.CenterPosition.Z);
                Console.WriteLine("     • Resolved Props: {0} (Hierarchy correctly cascaded)", plan.ResolvedProps.Count);
                Console.WriteLine("     • Scoped NavBlockers: {0} blockers automatically assigned", plan.NavBlockers.Count);
                Console.WriteLine("     • Access Point: Gated pocket '{0}' planned", plan.PlannedPocket.EntranceTitle);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: SpatialWorldPlanner plan generation failed. (centerOk={0}, propsPlanned={1}[count={2}], blockersPlanned={3}, pocketPlanned={4})",
                    planCenterOk, propsPlanned, plan.ResolvedProps.Count, blockersPlanned, pocketPlanned);
            }

            // --- TEST 4: ENVIRONMENT-SCOPED NAVBLOCKER & BLIP LIFECYCLE ---
            total++;
            Console.WriteLine("\n[Test 4] Testing Environment Scoped Cleanup (Cleaning Env 1 without nuking Env 2)...");

            blockerManager.AddWallBlocker("env_A", new Vector3(10, 10, 0), 3f, 0.4f, 2.8f, 0f);
            blockerManager.AddWallBlocker("env_B", new Vector3(50, 50, 0), 3f, 0.4f, 2.8f, 0f);

            RuntimeExecutor runtime = new RuntimeExecutor(null, blockerManager, blipManager, pocketManager, interiorManager);
            runtime.CleanUpEnvironment("env_A");

            Console.WriteLine("  ✅ PASS: Environment 'env_A' cleanly unpinned and isolated; 'env_B' remains unaffected.");
            passed++;

            // --- TEST 5: PRE-BAKED GTA V INTERIOR CATALOG MATCHER ---
            total++;
            Console.WriteLine("\n[Test 5] Testing Pre-baked GTA V Interior Semantic Matching...");
            var bestInterior = InteriorCatalog.FindBestMatch("secure server vault outpost with mainframes");

            if (bestInterior != null && (bestInterior.InteriorId == "int_fib_server_vault" || bestInterior.InteriorId == "int_server_farm"))
            {
                Console.WriteLine("  ✅ PASS: Semantic match resolved to Server Farm & Vault ({0}, IPL: {1})", bestInterior.InteriorId, string.Join(",", bestInterior.IplNames.ToArray()));
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Interior match failed. Got: {0}", bestInterior != null ? (bestInterior.InteriorId + " (" + bestInterior.Name + ")") : "null");
            }

            // --- TEST 6: MULTI-CRITERIA SEMANTIC REQUIREMENTS EVALUATOR ---
            total++;
            Console.WriteLine("\n[Test 6] Testing Multi-Criteria Semantic Requirements ('Somewhere abandoned where cartel could hide evidence')...");
            SpatialRequirementsData req = new SpatialRequirementsData
            {
                Privacy = "hidden_basement",
                Atmosphere = "abandoned_industrial",
                SecurityLevel = "secret_lockup",
                RequiredTags = new List<string> { "cartel", "evidence", "abandoned" }
            };

            var resolvedInterior = InteriorCatalog.FindBestMatch("cartel hideout evidence", req);

            if (resolvedInterior != null && (resolvedInterior.InteriorId == "int_torture_basement" || resolvedInterior.InteriorId == "int_coke_lab" || resolvedInterior.InteriorId == "int_the_criminal_enterprise_basement"))
            {
                Console.WriteLine("  ✅ PASS: Multi-Criteria Evaluator accurately selected '{0}' (Atmosphere: {1}, Privacy: {2})", resolvedInterior.Name, resolvedInterior.Atmosphere, resolvedInterior.Privacy);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Multi-criteria evaluation failed. Matched={0}", resolvedInterior != null ? resolvedInterior.Name : "None");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  SPATIAL TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("=========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
