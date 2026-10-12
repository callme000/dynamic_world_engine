using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    class EnvironmentStateArchitectureTester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("   ENVIRONMENT RECORD & STATE ARCHITECTURE UNIT TEST      ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_EnvState_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            int passed = 0;
            int total = 0;

            // [Phase 1] Setup EngineCore & Synthesize Rich Spatial Execution Plan
            Console.WriteLine("[Step 1] Initializing EngineCore with PlanRepository & WorldMemoryStore...");
            EngineCore engine = new EngineCore(testDir);

            SpatialExecutionPlan plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_cartel_compound_01",
                Name = "Cartel Secret Compound",
                Archetype = "bunker",
                CenterPosition = new SpatialVector3(150f, 300f, 45f),
                Heading = 180f,
                Atmosphere = new EnvironmentAtmosphereData { Weather = "OVERCAST", TimeHours = 23, TimeMinutes = 0 },
                PlannedPocket = new PocketTransition
                {
                    PocketId = "pocket_compound_vault",
                    EnvironmentId = "env_cartel_compound_01",
                    StreetCoords = new SpatialVector3(150f, 300f, 45f),
                    InteriorCoords = new SpatialVector3(100f, 200f, 1500f),
                    EntranceTitle = "Reinforced Blast Door",
                    IsLocked = true,
                    IsKnownToPlayer = true
                },
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement
                    {
                        PropId = "blast_door_frame",
                        ModelName = "prop_sec_barrier_01a",
                        WorldPosition = new SpatialVector3(150f, 300f, 45f),
                        WorldRotation = new SpatialVector3(0f, 0f, 180f),
                        Dimensions = new SpatialVector3(3f, 0.4f, 2.8f),
                        BlocksNavMesh = true
                    },
                    new ResolvedPropPlacement
                    {
                        PropId = "vault_safe_01",
                        ModelName = "p_v_43_safe_s",
                        WorldPosition = new SpatialVector3(105f, 205f, 1500f),
                        WorldRotation = new SpatialVector3(0f, 0f, 90f),
                        Dimensions = new SpatialVector3(1f, 1f, 1.5f),
                        BlocksNavMesh = false
                    }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_cartel_boss_diego",
                        Name = "Diego 'El Rayo' Morales",
                        Occupation = "Cartel Kingpin",
                        PublicFaction = "Morales Syndicate",
                        PedModel = "g_m_m_mexboss_01",
                        HiddenRole = "Syndicate Don",
                        WorldPosition = new SpatialVector3(102f, 202f, 1500f),
                        Heading = 0f
                    },
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_guard_hector",
                        Name = "Hector",
                        Occupation = "Bodyguard",
                        PublicFaction = "Morales Syndicate",
                        PedModel = "g_m_y_mexgoon_01",
                        HiddenRole = "Loyal Sentry",
                        WorldPosition = new SpatialVector3(100f, 198f, 1500f),
                        Heading = 45f
                    }
                }
            };

            // [Phase 2] Commission / Execute Spatial Plan
            Console.WriteLine("\n[Step 2] Commissioning Environment via ExecuteBuildEnvironmentPlan...");
            EnvironmentRecord env = engine.ExecuteBuildEnvironmentPlan(plan, null);

            total++;
            if (env != null)
            {
                Console.WriteLine("  ✔ PASS: EnvironmentRecord successfully created.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: EnvironmentRecord is null.");
            }

            total++;
            if (env != null && env.PlanId == "env_cartel_compound_01")
            {
                Console.WriteLine("  ✔ PASS: PlanId correctly bound to '{0}'.", env.PlanId);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: PlanId mismatch. Got: {0}", env != null ? env.PlanId : "null");
            }

            total++;
            if (env != null && env.Status == "COMMISSIONED")
            {
                Console.WriteLine("  ✔ PASS: Initial lifecycle status is 'COMMISSIONED'.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Initial lifecycle status mismatch. Got: {0}", env != null ? env.Status : "null");
            }

            total++;
            if (engine.Plans.HasPlan(env.PlanId))
            {
                Console.WriteLine("  ✔ PASS: CanonicalPlan is stored authoritatively in PlanRepository.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: PlanRepository does not contain plan for '{0}'.", env.PlanId);
            }

            total++;
            var retrievedPlan = engine.Plans.GetPlan(env.PlanId);
            if (retrievedPlan != null && retrievedPlan.ResolvedProps.Count == 2)
            {
                Console.WriteLine("  ✔ PASS: CanonicalPlan in PlanRepository accurately contains 2 resolved props.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Retrieved plan props count mismatch.");
            }

            total++;
            int propsInDb = engine.Store.GetPropsByEnvironment(env.EnvironmentId).Count;
            if (propsInDb == 0)
            {
                Console.WriteLine("  ✔ PASS: Zero props dumped into WorldMemoryStore.Props (Zero prop pollution!).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Found {0} props in WorldMemoryStore.Props! Prop isolation failed.", propsInDb);
            }

            total++;
            if (env != null && env.ResidentEntityIds.Contains("npc_cartel_boss_diego") && env.ResidentEntityIds.Contains("npc_guard_hector"))
            {
                Console.WriteLine("  ✔ PASS: ResidentEntityIds correctly indexed: [{0}]", string.Join(", ", env.ResidentEntityIds.ToArray()));
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: ResidentEntityIds missing expected entities.");
            }

            total++;
            if (env != null && env.PortalStates.ContainsKey("pocket_compound_vault") && env.PortalStates["pocket_compound_vault"] == true)
            {
                Console.WriteLine("  ✔ PASS: PortalStates registered initial planned pocket lock state (pocket_compound_vault: True).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: PortalStates missing or incorrect.");
            }

            // [Phase 3] Test World State Delta Mutations
            Console.WriteLine("\n[Step 3] Testing Dynamic World State Delta Mutations...");

            total++;
            bool portalUpdated = engine.Store.UpdatePortalState(env.EnvironmentId, "pocket_compound_vault", false);
            if (portalUpdated && env.PortalStates["pocket_compound_vault"] == false)
            {
                Console.WriteLine("  ✔ PASS: UpdatePortalState successfully unlocked vault portal (IsLocked = False).");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: UpdatePortalState failed.");
            }

            total++;
            bool containerUpdated = engine.Store.UpdateContainerState(env.EnvironmentId, "vault_safe_01", "unlocked_looted");
            if (containerUpdated && env.ContainerStates["vault_safe_01"] == "unlocked_looted")
            {
                Console.WriteLine("  ✔ PASS: UpdateContainerState recorded state: vault_safe_01 = 'unlocked_looted'.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: UpdateContainerState failed.");
            }

            total++;
            bool scenarioUpdated = engine.Store.UpdateScenarioState(env.EnvironmentId, "active_heist_shootout");
            if (scenarioUpdated && env.ScenarioState == "active_heist_shootout")
            {
                Console.WriteLine("  ✔ PASS: UpdateScenarioState recorded: 'active_heist_shootout'.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: UpdateScenarioState failed.");
            }

            // [Phase 4] Test Lifecycle Status Transitions
            Console.WriteLine("\n[Step 4] Testing Lifecycle Status Transitions...");

            total++;
            bool realized = engine.Store.UpdateEnvironmentStatus(env.EnvironmentId, "REALIZED");
            if (realized && env.Status == "REALIZED" && env.LastRealizedAt.HasValue)
            {
                Console.WriteLine("  ✔ PASS: Status transitioned to 'REALIZED' with timestamp: {0}", env.LastRealizedAt.Value);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Realized transition failed.");
            }

            total++;
            bool derealized = engine.Store.UpdateEnvironmentStatus(env.EnvironmentId, "DEREALIZED");
            if (derealized && env.Status == "DEREALIZED" && env.LastDerealizedAt.HasValue)
            {
                Console.WriteLine("  ✔ PASS: Status transitioned to 'DEREALIZED' with timestamp: {0}", env.LastDerealizedAt.Value);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Derealized transition failed.");
            }

            // [Phase 5] Cold Reload from Disk Verification
            Console.WriteLine("\n[Step 5] Testing Cold Disk Reload of WorldMemoryStore and PlanRepository...");
            engine.Flush();
            EngineCore reloadedEngine = new EngineCore(testDir);
            var reloadedEnv = reloadedEngine.Store.GetEnvironmentById(env.EnvironmentId);

            total++;
            if (reloadedEnv != null)
            {
                Console.WriteLine("  ✔ PASS: Reloaded EnvironmentRecord from disk.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: EnvironmentRecord not found after disk reload.");
            }

            total++;
            if (reloadedEnv != null && reloadedEnv.PlanId == "env_cartel_compound_01" && reloadedEnv.Status == "DEREALIZED")
            {
                Console.WriteLine("  ✔ PASS: Reloaded PlanId and Status ('DEREALIZED') match perfectly.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Reloaded PlanId or Status mismatch. (PlanId='{0}', Status='{1}')",
                    reloadedEnv != null ? reloadedEnv.PlanId : "null", reloadedEnv != null ? reloadedEnv.Status : "null");
            }

            total++;
            if (reloadedEnv != null && reloadedEnv.PortalStates.ContainsKey("pocket_compound_vault") && reloadedEnv.PortalStates["pocket_compound_vault"] == false)
            {
                Console.WriteLine("  ✔ PASS: Reloaded PortalStates preserved unlocked delta.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Reloaded PortalStates mismatch.");
            }

            total++;
            if (reloadedEnv != null && reloadedEnv.ContainerStates.ContainsKey("vault_safe_01") && reloadedEnv.ContainerStates["vault_safe_01"] == "unlocked_looted")
            {
                Console.WriteLine("  ✔ PASS: Reloaded ContainerStates preserved 'unlocked_looted' state.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Reloaded ContainerStates mismatch.");
            }

            total++;
            if (reloadedEnv != null && reloadedEnv.ScenarioState == "active_heist_shootout")
            {
                Console.WriteLine("  ✔ PASS: Reloaded ScenarioState preserved.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Reloaded ScenarioState mismatch.");
            }

            total++;
            var reloadedPlan = reloadedEngine.Plans.GetPlan(reloadedEnv.PlanId);
            if (reloadedPlan != null && reloadedPlan.ResolvedProps.Count == 2 && reloadedPlan.ResolvedNpcs.Count == 2)
            {
                Console.WriteLine("  ✔ PASS: Reloaded CanonicalPlan from PlanRepository matches exact physical blueprint.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Reloaded plan from PlanRepository failed.");
            }

            Console.WriteLine("\n==========================================================");
            Console.WriteLine("  STATE ARCHITECTURE TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
