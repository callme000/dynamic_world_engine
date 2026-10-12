using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Archetypes;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    class CommissioningArchitectureTester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("   DYNAMIC WORLD ENGINE — COMMISSIONING ARCHITECTURE TEST ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Commission_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            int passed = 0;
            int total = 0;

            // [Phase 1] Setup EngineCore
            Console.WriteLine("[Step 1] Initializing EngineCore with PlanRepository & WorldMemoryStore...");
            EngineCore engine = new EngineCore(testDir);

            // Synthesize Canonical Plan 1: Nightclub VIP Lounge
            SpatialExecutionPlan plan1 = new SpatialExecutionPlan
            {
                EnvironmentId = "env_nightclub_vip_01",
                Name = "The Neon Penthouse VIP",
                Archetype = "nightclub",
                CenterPosition = new SpatialVector3(-1200f, -1480f, 4.3f),
                Heading = 90f,
                Atmosphere = new EnvironmentAtmosphereData { Weather = "EXTRASUNNY", TimeHours = 2, TimeMinutes = 0, AmbientFx = "club_lights" },
                PlannedPocket = new PocketTransition
                {
                    PocketId = "pocket_vip_lounge",
                    EnvironmentId = "env_nightclub_vip_01",
                    StreetCoords = new SpatialVector3(-1200f, -1480f, 4.3f),
                    InteriorCoords = new SpatialVector3(-1205f, -1485f, 10.0f),
                    EntranceTitle = "VIP Velvet Rope",
                    IsLocked = true,
                    IsKnownToPlayer = true
                },
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement
                    {
                        PropId = "dj_booth_01",
                        ModelName = "ba_prop_battle_dj_stand",
                        WorldPosition = new SpatialVector3(-1202f, -1480f, 4.3f),
                        WorldRotation = new SpatialVector3(0f, 0f, 90f),
                        Dimensions = new SpatialVector3(2.5f, 1.2f, 1.1f),
                        BlocksNavMesh = true
                    },
                    new ResolvedPropPlacement
                    {
                        PropId = "vip_sofa_01",
                        ModelName = "prop_couch_sm_02",
                        WorldPosition = new SpatialVector3(-1205f, -1483f, 4.3f),
                        WorldRotation = new SpatialVector3(0f, 0f, 0f),
                        Dimensions = new SpatialVector3(2.0f, 0.9f, 0.8f),
                        BlocksNavMesh = false
                    }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_resident_dj_solomun",
                        Name = "DJ Solomun",
                        Occupation = "Resident DJ",
                        PublicFaction = "Afterlife Records",
                        PedModel = "ig_djblamadon",
                        HiddenRole = "Headline Artist",
                        WorldPosition = new SpatialVector3(-1202f, -1480f, 4.3f),
                        Heading = 90f,
                        Scenario = "WORLD_HUMAN_MUSICIAN",
                        Relationships = new Dictionary<string, int> { { "Player", 15 } },
                        InitialEvents = new List<InitialEventData>
                        {
                            new InitialEventData { Description = "Solomun is testing the VIP mixer before set", SentimentTag = "neutral" }
                        }
                    },
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_head_bouncer_vlad",
                        Name = "Vladislav 'The Gate' Petrenko",
                        Occupation = "Head Bouncer",
                        PublicFaction = "Nightlife Security",
                        PedModel = "s_m_m_highsec_01",
                        HiddenRole = "Strict Velvet Rope Enforcer",
                        WorldPosition = new SpatialVector3(-1200f, -1480f, 4.3f),
                        Heading = 270f,
                        Weapon = "WEAPON_PUMPSHOTGUN",
                        Relationships = new Dictionary<string, int> { { "Player", -10 } }
                    }
                },
                ResolvedVehicles = new List<ResolvedVehiclePlacement>
                {
                    new ResolvedVehiclePlacement
                    {
                        VehicleId = "veh_vip_nero_01",
                        ModelName = "nero2",
                        WorldPosition = new SpatialVector3(-1195f, -1475f, 4.3f),
                        Heading = 45f,
                        PrimaryColor = "metallic_black",
                        IsEngineRunning = false
                    }
                }
            };

            // [Phase 2] Commission Canonical Plan 1
            Console.WriteLine("\n[Step 2] Commissioning Environment 1 (The Neon Penthouse VIP)...");
            EnvironmentRecord env1 = engine.CommissionEnvironment(plan1, null);

            total++;
            if (env1 != null)
            {
                Console.WriteLine("  ✔ PASS: CommissionEnvironment successfully returned non-null EnvironmentRecord.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: CommissionEnvironment returned null.");
            }

            total++;
            if (env1 != null && env1.Status == "COMMISSIONED")
            {
                Console.WriteLine("  ✔ PASS: Lifecycle status is 'COMMISSIONED'.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Lifecycle status mismatch: {0}", env1 != null ? env1.Status : "null");
            }

            total++;
            if (env1 != null && env1.PlanId == "env_nightclub_vip_01")
            {
                Console.WriteLine("  ✔ PASS: PlanId correctly references 'env_nightclub_vip_01'.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: PlanId mismatch.");
            }

            total++;
            if (engine.Plans.HasPlan("env_nightclub_vip_01"))
            {
                Console.WriteLine("  ✔ PASS: CanonicalPlan persisted in PlanRepository on disk.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: PlanRepository missing plan 'env_nightclub_vip_01'.");
            }

            total++;
            var retrievedPlan1 = engine.Plans.GetPlan("env_nightclub_vip_01");
            if (retrievedPlan1 != null && retrievedPlan1.ResolvedProps.Count == 2 && retrievedPlan1.ResolvedNpcs.Count == 2 && retrievedPlan1.ResolvedVehicles.Count == 1)
            {
                Console.WriteLine("  ✔ PASS: CanonicalPlan matches exact physical specification (2 props, 2 NPCs, 1 vehicle).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: CanonicalPlan in PlanRepository content mismatch.");
            }

            total++;
            if (env1 != null && env1.ResidentEntityIds.Contains("npc_resident_dj_solomun") && env1.ResidentEntityIds.Contains("npc_head_bouncer_vlad"))
            {
                Console.WriteLine("  ✔ PASS: ResidentEntityIds correctly registered in WorldMemoryStore: [{0}]", string.Join(", ", env1.ResidentEntityIds.ToArray()));
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: ResidentEntityIds missing expected entities.");
            }

            total++;
            if (env1 != null && env1.NpcIds.Count == 2)
            {
                var djRecord = engine.Store.GetNpcById(env1.NpcIds[0]);
                if (djRecord != null && djRecord.Name == "DJ Solomun" && djRecord.Occupation == "Resident DJ")
                {
                    Console.WriteLine("  ✔ PASS: Resident NPC enrolled in WorldMemoryStore with narrative truth (NpcId={0}, Name='{1}').", djRecord.NpcId, djRecord.Name);
                    passed++;
                }
                else
                {
                    Console.WriteLine("  ❌ FAIL: Resident NPC in WorldMemoryStore mismatch.");
                }
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: NpcIds count in env1 mismatch.");
            }

            // [Phase 3] Commission Environment 2 via JSON Command
            Console.WriteLine("\n[Step 3] Commissioning Environment 2 via CommissionEnvironment(JSON payload)...");
            string bikerJson = EnvironmentCatalog.GetChopShopJson();
            EnvironmentRecord env2 = engine.CommissionEnvironment(bikerJson, 200f, 400f, 15f, 0f);

            total++;
            if (env2 != null && env2.Status == "COMMISSIONED")
            {
                Console.WriteLine("  ✔ PASS: Environment 2 commissioned from JSON with PlanId='{0}'.", env2.PlanId);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Environment 2 commissioning failed.");
            }

            total++;
            var allPlans = engine.Plans.GetAllPlanIds();
            var allEnvs = engine.Store.GetAllEnvironments();
            if (allPlans.Count == 2 && allEnvs.Count == 2)
            {
                Console.WriteLine("  ✔ PASS: Total plans in PlanRepository is 2; Total environments in WorldMemoryStore is 2.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Store counts mismatch (plans={0}, envs={1}).", allPlans.Count, allEnvs.Count);
            }

            // [Phase 4] Decommissioning Isolated Environment
            Console.WriteLine("\n[Step 4] Decommissioning Environment 1 (The Neon Penthouse VIP)...");
            bool decommissioned = engine.DecommissionEnvironment("env_nightclub_vip_01");

            total++;
            if (decommissioned)
            {
                Console.WriteLine("  ✔ PASS: DecommissionEnvironment returned true.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: DecommissionEnvironment returned false.");
            }

            total++;
            bool plan1Purged = !engine.Plans.HasPlan("env_nightclub_vip_01");
            bool env1Purged = engine.Store.GetEnvironmentById("env_nightclub_vip_01") == null;
            if (plan1Purged && env1Purged)
            {
                Console.WriteLine("  ✔ PASS: Physical plan purged from PlanRepository AND world record purged from WorldMemoryStore.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Decommissioning did not clean both repositories (planPurged={0}, envPurged={1}).", plan1Purged, env1Purged);
            }

            total++;
            bool env2Intact = engine.Store.GetEnvironmentById(env2.EnvironmentId) != null;
            bool plan2Intact = engine.Plans.HasPlan(env2.PlanId);
            if (env2Intact && plan2Intact)
            {
                Console.WriteLine("  ✔ PASS: Environment 2 remains 100% untouched (Zero cross-environment contamination!).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Environment 2 was unexpectedly contaminated or deleted.");
            }

            Console.WriteLine("\n==========================================================");
            Console.WriteLine("  COMMISSIONING TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
