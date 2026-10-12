using System;
using System.Collections.Generic;
using System.Reflection;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;

namespace DynamicWorldEngine.Tests
{
    class RuntimeOwnershipArchitectureTester
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

        static Prop CreateMockProp(int handle)
        {
            return (Prop)Activator.CreateInstance(typeof(Prop), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { handle }, null);
        }

        static Ped CreateMockPed(int handle)
        {
            return (Ped)Activator.CreateInstance(typeof(Ped), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { handle }, null);
        }

        static Vehicle CreateMockVehicle(int handle)
        {
            return (Vehicle)Activator.CreateInstance(typeof(Vehicle), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { handle }, null);
        }

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("   DYNAMIC WORLD ENGINE — RUNTIME OWNERSHIP ARCHITECTURE  ");
            Console.WriteLine("==========================================================\n");

            // Test Setup
            EntitySpawnerService spawner = new EntitySpawnerService();
            NavMeshBlockerManager navBlockers = new NavMeshBlockerManager();
            BlipManager blips = new BlipManager();
            TeleportPocketManager pockets = new TeleportPocketManager();
            InteriorManager interiorMgr = new InteriorManager(pockets, blips);
            DirectorTimelineManager timeline = new DirectorTimelineManager();
            RuntimeExecutor executor = new RuntimeExecutor(
                spawner,
                navBlockers,
                blips,
                pockets,
                interiorMgr,
                null,
                null,
                timeline
            );

            string envA = "env_luxury_penthouse";
            string envB = "env_underground_dock";

            Console.WriteLine("[Step 1] Registering entities into separate environments...");

            var propA1 = CreateMockProp(1001);
            var propA2 = CreateMockProp(1002);
            var pedA1 = CreateMockPed(2001);
            var vehA1 = CreateMockVehicle(3001);

            var propB1 = CreateMockProp(1003);
            var pedB1 = CreateMockPed(2002);
            var vehB1 = CreateMockVehicle(3002);

            var ctxA1 = new ActiveNpcContext(pedA1, new NpcRecord { NpcId = 1, EntityId = "npc_host_01", Name = "Penthouse Host" }, envA, "npc_host_01");
            var ctxB1 = new ActiveNpcContext(pedB1, new NpcRecord { NpcId = 2, EntityId = "npc_guard_01", Name = "Dock Guard" }, envB, "npc_guard_01");

            // Register envA entities
            spawner.RegisterProp(envA, propA1);
            spawner.RegisterProp(envA, propA2);
            spawner.RegisterNpc(envA, ctxA1);
            spawner.RegisterVehicle(envA, vehA1);

            // Register envB entities
            spawner.RegisterProp(envB, propB1);
            spawner.RegisterNpc(envB, ctxB1);
            spawner.RegisterVehicle(envB, vehB1);

            // Assertions for initial registration
            AssertTrue("Spawner total props registered is 3", spawner.SpawnedProps.Count == 3);
            AssertTrue("Spawner total NPCs registered is 2", spawner.ActiveNpcContexts.Count == 2);
            AssertTrue("Spawner total vehicles registered is 2", spawner.SpawnedVehicles.Count == 2);

            AssertTrue("envA has 2 props", spawner.GetPropsForEnvironment(envA).Count == 2);
            AssertTrue("envB has 1 prop", spawner.GetPropsForEnvironment(envB).Count == 1);
            AssertTrue("envA has 1 NPC", spawner.GetNpcsForEnvironment(envA).Count == 1);
            AssertTrue("envB has 1 NPC", spawner.GetNpcsForEnvironment(envB).Count == 1);
            AssertTrue("envA has 1 Vehicle", spawner.GetVehiclesForEnvironment(envA).Count == 1);
            AssertTrue("envB has 1 Vehicle", spawner.GetVehiclesForEnvironment(envB).Count == 1);

            AssertTrue("Spawner HasRealizedEntities(envA) == true", spawner.HasRealizedEntities(envA));
            AssertTrue("Spawner HasRealizedEntities(envB) == true", spawner.HasRealizedEntities(envB));
            AssertTrue("Spawner HasRealizedEntities('non_existent') == false", !spawner.HasRealizedEntities("non_existent"));

            Console.WriteLine("\n[Step 2] Testing RuntimeExecutor queries forwarding...");
            AssertTrue("RuntimeExecutor GetPropsForEnvironment(envA) == 2", executor.GetPropsForEnvironment(envA).Count == 2);
            AssertTrue("RuntimeExecutor GetNpcsForEnvironment(envB) == 1", executor.GetNpcsForEnvironment(envB).Count == 1);
            AssertTrue("RuntimeExecutor HasRealizedEntities(envA) == true", executor.HasRealizedEntities(envA));

            Console.WriteLine("\n[Step 3] Setting up environment-scoped navigation blockers and timeline...");
            navBlockers.AddWallBlocker(envA, new GTA.Math.Vector3(10f, 10f, 10f), 2f, 2f, 2f, 0f);
            navBlockers.AddWallBlocker(envB, new GTA.Math.Vector3(50f, 50f, 10f), 3f, 3f, 2f, 0f);

            var scenarioA = new ScenarioDirectorData
            {
                ScenarioTitle = "Penthouse Heist Scenario",
                Beats = new List<ScenarioBeatData>
                {
                    new ScenarioBeatData { BeatId = "beat_01", DelaySeconds = 5.0f, Trigger = "timer" }
                }
            };
            timeline.LoadScenario(scenarioA, envA, new GTA.Math.Vector3(10f, 10f, 10f), spawner.ActiveNpcContexts);
            AssertTrue("Timeline is active for envA", timeline.IsScenarioActive);

            Console.WriteLine("\n[Step 4] Executing isolated deallocation of envA via CleanUpEnvironment...");
            executor.CleanUpEnvironment(envA);

            // envA should be completely evicted
            AssertTrue("envA props evicted (count == 0)", spawner.GetPropsForEnvironment(envA).Count == 0);
            AssertTrue("envA npcs evicted (count == 0)", spawner.GetNpcsForEnvironment(envA).Count == 0);
            AssertTrue("envA vehicles evicted (count == 0)", spawner.GetVehiclesForEnvironment(envA).Count == 0);
            AssertTrue("HasRealizedEntities(envA) == false", !executor.HasRealizedEntities(envA));

            // envB must be 100% intact and untouched
            AssertTrue("envB prop remained intact (count == 1)", spawner.GetPropsForEnvironment(envB).Count == 1);
            AssertTrue("envB npc remained intact (count == 1)", spawner.GetNpcsForEnvironment(envB).Count == 1);
            AssertTrue("envB vehicle remained intact (count == 1)", spawner.GetVehiclesForEnvironment(envB).Count == 1);
            AssertTrue("HasRealizedEntities(envB) == true", executor.HasRealizedEntities(envB));

            // Global lists should now only have envB's entities
            AssertTrue("Global props count is exactly 1 (propB1)", spawner.SpawnedProps.Count == 1 && spawner.SpawnedProps[0].Handle == 1003);
            AssertTrue("Global npcs count is exactly 1 (ctxB1)", spawner.ActiveNpcContexts.Count == 1 && spawner.ActiveNpcContexts[0].EntityId == "npc_guard_01");
            AssertTrue("Global vehicles count is exactly 1 (vehB1)", spawner.SpawnedVehicles.Count == 1 && spawner.SpawnedVehicles[0].Handle == 3002);

            // Timeline for envA was reset
            AssertTrue("Timeline was reset when envA was cleaned up", !timeline.IsScenarioActive);

            Console.WriteLine("\n[Step 5] Global ClearAll evicts remaining envB...");
            executor.ClearAll();

            AssertTrue("Global props count is 0 after ClearAll", spawner.SpawnedProps.Count == 0);
            AssertTrue("Global npcs count is 0 after ClearAll", spawner.ActiveNpcContexts.Count == 0);
            AssertTrue("Global vehicles count is 0 after ClearAll", spawner.SpawnedVehicles.Count == 0);
            AssertTrue("HasRealizedEntities(envB) == false after ClearAll", !executor.HasRealizedEntities(envB));

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("   TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
