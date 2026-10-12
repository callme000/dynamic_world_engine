using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Tests
{
    class PlanRepositoryTester
    {
        static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ❌ FAIL: " + message);
                Console.ResetColor();
                Environment.Exit(1);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✅ PASS: " + message);
                Console.ResetColor();
            }
        }

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("          PLAN REPOSITORY UNIT & PERSISTENCE TEST         ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Plan_Repo_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            try
            {
                PlanRepository repo = new PlanRepository(testDir);
                Assert(repo.StorageDirectory == testDir, "Repository storage directory configured properly.");

                // 1. Test HasPlan on non-existent plan
                Assert(!repo.HasPlan("env_nonexistent_01"), "HasPlan returns false for non-existent environment.");
                Assert(repo.GetPlan("env_nonexistent_01") == null, "GetPlan returns null for non-existent environment.");

                // 2. Construct canonical plan
                SpatialExecutionPlan plan = new SpatialExecutionPlan
                {
                    EnvironmentId = "env_bunker_repo_test",
                    Name = "Repo Test Bunker",
                    Archetype = "bunker",
                    CenterPosition = new SpatialVector3(250.0f, -100.0f, 1200.0f),
                    Heading = 180.0f,
                    IsSkyPocket = true,
                    Atmosphere = new EnvironmentAtmosphereData { Weather = "CLEAR", TimeHours = 14, TimeMinutes = 0 },
                    PlannedPocket = new PocketTransition
                    {
                        PocketId = "pocket_test_01",
                        EnvironmentId = "env_bunker_repo_test",
                        StreetCoords = new SpatialVector3(250.0f, -100.0f, 30.0f),
                        InteriorCoords = new SpatialVector3(250.0f, -100.0f, 1200.0f),
                        InteriorExitCoords = new SpatialVector3(250.0f, -98.0f, 1200.0f),
                        EntranceTitle = "Bunker Blast Door",
                        IsLocked = true
                    },
                    ResolvedProps = new List<ResolvedPropPlacement>
                    {
                        new ResolvedPropPlacement
                        {
                            PropId = "barricade_01",
                            ModelName = "prop_sec_barrier_01a",
                            WorldPosition = new SpatialVector3(250.0f, -95.0f, 1200.0f),
                            WorldRotation = new SpatialVector3(0f, 0f, 90f),
                            BlocksNavMesh = true,
                            Dimensions = new SpatialVector3(3f, 0.4f, 2.5f)
                        }
                    },
                    ResolvedNpcs = new List<ResolvedNpcPlacement>
                    {
                        new ResolvedNpcPlacement
                        {
                            EntityId = "guard_sergei",
                            Name = "Sergei",
                            Occupation = "Vault Sentinel",
                            PublicFaction = "BunkerGuards",
                            PedModel = "s_m_m_highsec_01",
                            WorldPosition = new SpatialVector3(251.0f, -96.0f, 1200.0f),
                            Heading = 0f,
                            Weapon = "WEAPON_ASSAULTRIFLE"
                        }
                    },
                    NavBlockers = new List<WallNavBlockerDefinition>
                    {
                        new WallNavBlockerDefinition(
                            new SpatialVector3(250.0f, -95.0f, 1200.0f),
                            new SpatialVector3(3f, 0.4f, 2.5f),
                            90f
                        )
                    }
                };

                // 3. Save plan
                Console.WriteLine("[Step 1] Saving plan to repository...");
                bool saved = repo.SavePlan("env_bunker_repo_test", plan);
                Assert(saved, "SavePlan returns true on successful write.");
                Assert(repo.HasPlan("env_bunker_repo_test"), "HasPlan returns true immediately after save.");

                string expectedFile = Path.Combine(testDir, "env_bunker_repo_test.json");
                Assert(File.Exists(expectedFile), "File exists on disk at expected path: " + expectedFile);

                // 4. Retrieve plan
                Console.WriteLine("\n[Step 2] Retrieving plan from repository...");
                SpatialExecutionPlan retrievedPlan = repo.GetPlan("env_bunker_repo_test");
                Assert(retrievedPlan != null, "GetPlan returns non-null plan.");
                Assert(retrievedPlan.EnvironmentId == "env_bunker_repo_test", "EnvironmentId matches.");
                Assert(retrievedPlan.CenterPosition == plan.CenterPosition, "CenterPosition matches exactly: " + retrievedPlan.CenterPosition);
                Assert(retrievedPlan.ResolvedProps.Count == 1, "Props count matches.");
                Assert(retrievedPlan.ResolvedProps[0].PropId == "barricade_01", "PropId matches.");
                Assert(retrievedPlan.ResolvedProps[0].BlocksNavMesh == true, "BlocksNavMesh matches.");
                Assert(retrievedPlan.ResolvedNpcs.Count == 1, "NPCs count matches.");
                Assert(retrievedPlan.ResolvedNpcs[0].EntityId == "guard_sergei", "EntityId matches.");
                Assert(retrievedPlan.NavBlockers.Count == 1, "NavBlockers count matches.");
                Assert(retrievedPlan.PlannedPocket != null, "PlannedPocket matches.");
                Assert(retrievedPlan.PlannedPocket.PocketId == "pocket_test_01", "PocketId matches.");
                Assert(retrievedPlan.PlannedPocket.EntranceBlip == null, "EntranceBlip remains cleanly null.");

                // 5. Test GetAllPlanIds
                Console.WriteLine("\n[Step 3] Enumerating stored plans...");
                List<string> ids = repo.GetAllPlanIds();
                Assert(ids.Contains("env_bunker_repo_test"), "GetAllPlanIds contains 'env_bunker_repo_test'.");
                Assert(ids.Count == 1, "Exact count of stored plans is 1.");

                // 6. Test DeletePlan
                Console.WriteLine("\n[Step 4] Deleting plan from repository...");
                bool deleted = repo.DeletePlan("env_bunker_repo_test");
                Assert(deleted, "DeletePlan returns true.");
                Assert(!repo.HasPlan("env_bunker_repo_test"), "HasPlan returns false after deletion.");
                Assert(!File.Exists(expectedFile), "Plan file removed from disk.");
                Assert(repo.GetAllPlanIds().Count == 0, "Plan enumeration is empty.");

                // 7. Test Delete on non-existent plan
                Assert(!repo.DeletePlan("env_bunker_repo_test"), "DeletePlan returns false on non-existent plan.");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==========================================================");
                Console.WriteLine("    PERFECT PASS: PLAN REPOSITORY PASSED ALL TESTS!       ");
                Console.WriteLine("==========================================================");
                Console.ResetColor();

                return 0;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
                }
                catch { }
            }
        }
    }
}
