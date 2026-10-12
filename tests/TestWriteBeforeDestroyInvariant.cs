using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Observer;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;

namespace DynamicWorldEngine.Tests
{
    class WriteBeforeDestroyInvariantTester
    {
        private static int passed = 0;
        private static int total = 0;

        static void AssertTrue(string name, bool condition)
        {
            total++;
            if (condition)
            {
                Console.WriteLine(string.Format("  [PASS] {0}", name));
                passed++;
            }
            else
            {
                Console.WriteLine(string.Format("  [FAIL] {0}", name));
            }
        }

        static Ped CreateMockPed(int handle)
        {
            return (Ped)Activator.CreateInstance(typeof(Ped), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { handle }, null);
        }

        static Prop CreateMockProp(int handle)
        {
            return (Prop)Activator.CreateInstance(typeof(Prop), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { handle }, null);
        }

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("     WRITE-BEFORE-DESTROY INVARIANT ARCHITECTURAL TEST    ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_WriteBeforeDestroy_Data");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            EntitySpawnerService spawner = new EntitySpawnerService();
            TeleportPocketManager pockets = new TeleportPocketManager();
            BlipManager blips = new BlipManager();
            InteriorManager interiorMgr = new InteriorManager(pockets, blips);
            RuntimeExecutor runtime = new RuntimeExecutor(
                spawner,
                new NavMeshBlockerManager(),
                blips,
                pockets,
                interiorMgr
            );

            EngineCore engine = new EngineCore(
                testDir,
                null,
                runtime.NavBlockers,
                runtime.Blips,
                runtime.TeleportPockets,
                interiorMgr,
                runtime
            );

            string dbPath = Path.Combine(testDir, "world_memory.json");

            // =====================================================================
            // SECTION 1: IN-FLIGHT NPC DEATH IMMEDIATE FLUSH TEST
            // =====================================================================
            Console.WriteLine("[Section 1] Verifying MarkNpcDead achieves immediate durable disk flush...");

            var npc1 = engine.Store.CreateNpc("Viktor", "Syndicate Lieutenant", "Mafia", "g_m_y_salvagoon_01", "Arms courier");
            engine.Store.Save(true);
            AssertTrue("Viktor enrolled with IsAlive == true", npc1.IsAlive);

            // Directly inspect cold disk file before death
            string rawJsonBefore = File.ReadAllText(dbPath);
            AssertTrue("Cold disk file reflects Viktor IsAlive == true initially", rawJsonBefore.Contains("\"IsAlive\": true"));

            // Trigger MarkNpcDead
            bool marked = engine.Store.MarkNpcDead(npc1.NpcId, "Player", "Shotgun blast");
            AssertTrue("MarkNpcDead returned true", marked);

            // Cold disk inspection IMMEDIATELY (with zero delay / no 1000ms wait)
            string rawJsonAfter = File.ReadAllText(dbPath);
            AssertTrue("Cold disk file immediately reflects Viktor IsAlive == false (no 1-second throttle lag!)", rawJsonAfter.Contains("\"IsAlive\": false"));
            AssertTrue("Cold disk file immediately contains ENTITY_DIED event", rawJsonAfter.Contains("ENTITY_DIED"));

            // =====================================================================
            // SECTION 2: DEREALIZE WRITE-BEFORE-DESTROY SEQUENCE
            // =====================================================================
            Console.WriteLine("\n[Section 2] Verifying Derealize flushes domain state to disk before native destruction...");

            string envId = "env_dock_heist";
            SpatialExecutionPlan plan = new SpatialExecutionPlan
            {
                EnvironmentId = envId,
                Name = "Dockside Smuggler Stash",
                Archetype = "docks",
                CenterPosition = new SpatialVector3(100f, -500f, 5f),
                Heading = 0f,
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "crate_01", ModelName = "prop_box_wood02a", WorldPosition = new SpatialVector3(102f, -500f, 5f) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_guard_1", Name = "Guard Alexei", PedModel = "g_m_m_chicold_01", InitialState = "CALM" }
                }
            };

            var envRec = engine.CommissionEnvironment(plan);
            AssertTrue("Environment commissioned", envRec != null && envRec.Status == "COMMISSIONED");

            // Mock native runtime realization
            var mockPed = CreateMockPed(9001);
            var mockProp = CreateMockProp(9002);
            spawner.RegisterProp(envId, mockProp);
            var ctxAlexei = new ActiveNpcContext(mockPed, engine.Store.GetNpcByEntityId("npc_guard_1"), envId);
            spawner.RegisterNpc(envId, ctxAlexei);

            AssertTrue("Runtime has realized entities before derealization", runtime.HasRealizedEntities(envId));

            // Execute Derealize
            bool derealized = engine.Derealize(envId);
            AssertTrue("Derealize succeeded", derealized);

            // Read cold disk file immediately
            string rawJsonDerealized = File.ReadAllText(dbPath);
            AssertTrue("Disk file reflects Environment Status == 'DEREALIZED'", rawJsonDerealized.Contains("\"Status\": \"DEREALIZED\""));
            AssertTrue("Runtime entities cleanly evicted after confirmed disk write", !runtime.HasRealizedEntities(envId));

            // =====================================================================
            // SECTION 3: ANTI-RESURRECTION GUARD (ATOMICITY ON PERSISTENCE FAILURE)
            // =====================================================================
            Console.WriteLine("\n[Section 3] Verifying Anti-Resurrection Guard: Persistence failure halts entity eviction...");

            string failEnvId = "env_secure_bunker";
            SpatialExecutionPlan failPlan = new SpatialExecutionPlan
            {
                EnvironmentId = failEnvId,
                Name = "Deep Mountain Vault",
                Archetype = "bunker",
                CenterPosition = new SpatialVector3(200f, 300f, 15f),
                Heading = 90f,
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_bunker_boss", Name = "General Vance", PedModel = "s_m_m_security_01", InitialState = "CALM" }
                }
            };

            var failEnvRec = engine.CommissionEnvironment(failPlan);
            AssertTrue("Fail test environment commissioned", failEnvRec != null);

            var bossPed = CreateMockPed(9003);
            var ctxBoss = new ActiveNpcContext(bossPed, engine.Store.GetNpcByEntityId("npc_bunker_boss"), failEnvId);
            spawner.RegisterNpc(failEnvId, ctxBoss);
            AssertTrue("Fail test environment has realized live entities", runtime.HasRealizedEntities(failEnvId));

            // Simulate catastrophic disk failure: make database file read-only so Save(true) will throw UnauthorizedAccessException
            File.SetAttributes(dbPath, FileAttributes.ReadOnly);

            bool derealizeResult = false;
            try
            {
                derealizeResult = engine.Derealize(failEnvId);
            }
            catch
            {
                derealizeResult = false;
            }

            // Invariant Verification:
            // When durable disk persistence fails, Derealize MUST return false,
            // AND native entities MUST NOT BE DESTROYED (HasRealizedEntities remains true)!
            AssertTrue("Derealize safely reported failure when disk write failed", !derealizeResult);
            AssertTrue("ANTI-RESURRECTION INVARIANT PROVEN: Native entities were NOT destroyed when persistence failed!", runtime.HasRealizedEntities(failEnvId));

            // Restore attributes for cleanup
            File.SetAttributes(dbPath, FileAttributes.Normal);

            // Re-attempt Derealize now that disk is writable
            bool derealizeRetry = engine.Derealize(failEnvId);
            AssertTrue("Derealize succeeds once disk write is healthy", derealizeRetry);
            AssertTrue("Native entities evicted only after durable disk write confirmed", !runtime.HasRealizedEntities(failEnvId));

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("   TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
