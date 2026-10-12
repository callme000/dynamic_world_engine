using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;

namespace DynamicWorldEngine.Tests
{
    class RealizationLifecycleArchitectureTester
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
            Console.WriteLine("   DYNAMIC WORLD ENGINE — REALIZATION LIFECYCLE TEST      ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Realization_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            // [Phase 1] Setup EngineCore with custom test directory
            Console.WriteLine("[Step 1] Initializing EngineCore...");
            EngineCore engine = new EngineCore(testDir);

            // Construct Canonical Plan for Luxury Casino
            string envId1 = "env_luxury_casino_01";
            SpatialExecutionPlan plan1 = new SpatialExecutionPlan
            {
                EnvironmentId = envId1,
                Name = "The Diamond VIP Suite",
                Archetype = "casino",
                CenterPosition = new SpatialVector3(100f, 200f, 15f),
                Heading = 45f,
                Atmosphere = new EnvironmentAtmosphereData { Weather = "EXTRASUNNY", TimeHours = 22, TimeMinutes = 30 },
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "poker_table_01", ModelName = "prop_table_01", WorldPosition = new SpatialVector3(102f, 200f, 15f) },
                    new ResolvedPropPlacement { PropId = "vip_bar_01", ModelName = "prop_bar_01", WorldPosition = new SpatialVector3(98f, 200f, 15f) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_dealer_01", Name = "Dealer Sophia", PedModel = "a_f_y_topless", InitialState = "CALM" },
                    new ResolvedNpcPlacement { EntityId = "npc_guard_01", Name = "Guard Viktor", PedModel = "s_m_m_security_01", InitialState = "ALERT" }
                },
                ResolvedVehicles = new List<ResolvedVehiclePlacement>
                {
                    new ResolvedVehiclePlacement { VehicleId = "veh_limo_01", ModelName = "stretch", WorldPosition = new SpatialVector3(90f, 200f, 15f) }
                }
            };

            // Construct Canonical Plan for Dock Warehouse
            string envId2 = "env_dock_warehouse_02";
            SpatialExecutionPlan plan2 = new SpatialExecutionPlan
            {
                EnvironmentId = envId2,
                Name = "Elysian Island Contraband Dock",
                Archetype = "dockyard",
                CenterPosition = new SpatialVector3(500f, -1200f, 3f),
                Heading = 180f,
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "crate_stack_01", ModelName = "prop_box_wood02a_pu", WorldPosition = new SpatialVector3(502f, -1200f, 3f) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_dockworker_01", Name = "Worker Joe", PedModel = "s_m_m_dockwork_01", InitialState = "CALM" }
                }
            };

            // [Phase 2] Commission both environments (Zero entities realized)
            Console.WriteLine("\n[Step 2] Commissioning environments 1 & 2...");
            var envRec1 = engine.CommissionEnvironment(plan1);
            var envRec2 = engine.CommissionEnvironment(plan2);

            AssertTrue("Environment 1 commissioned with status 'COMMISSIONED'", envRec1 != null && envRec1.Status == "COMMISSIONED");
            AssertTrue("Environment 2 commissioned with status 'COMMISSIONED'", envRec2 != null && envRec2.Status == "COMMISSIONED");
            AssertTrue("Plan 1 exists in PlanRepository", engine.Plans.HasPlan(envId1));
            AssertTrue("Plan 2 exists in PlanRepository", engine.Plans.HasPlan(envId2));
            AssertTrue("Runtime has zero realized entities for env1 prior to realization", !engine.Runtime.HasRealizedEntities(envId1));
            AssertTrue("Runtime has zero realized entities for env2 prior to realization", !engine.Runtime.HasRealizedEntities(envId2));

            // [Phase 3] Authoritatively Realize Environment 1
            Console.WriteLine("\n[Step 3] Realizing Environment 1 via engine.Realize()...");
            // Mock native spawning registrations into spawner to simulate GTA engine manifestation
            var prop1 = CreateMockProp(1101);
            var prop2 = CreateMockProp(1102);
            var ped1 = CreateMockPed(2101);
            var ped2 = CreateMockPed(2102);
            var veh1 = CreateMockVehicle(3101);

            engine.Runtime.Spawner.RegisterProp(envId1, prop1);
            engine.Runtime.Spawner.RegisterProp(envId1, prop2);
            var ctx1 = new ActiveNpcContext(ped1, engine.Store.GetNpc(envRec1.NpcIds[0]), envId1, "npc_dealer_01");
            var ctx2 = new ActiveNpcContext(ped2, engine.Store.GetNpc(envRec1.NpcIds[1]), envId1, "npc_guard_01");
            engine.Runtime.Spawner.RegisterNpc(envId1, ctx1);
            engine.Runtime.Spawner.RegisterNpc(envId1, ctx2);
            engine.Runtime.Spawner.RegisterVehicle(envId1, veh1);

            var realizedRec1 = engine.Realize(envId1);

            AssertTrue("Realize returns non-null EnvironmentRecord", realizedRec1 != null);
            AssertTrue("Environment 1 status transitioned to 'REALIZED'", realizedRec1.Status == "REALIZED");
            AssertTrue("LastRealizedAt timestamp is set", realizedRec1.LastRealizedAt.HasValue);
            AssertTrue("Runtime confirmed has realized entities for env1", engine.Runtime.HasRealizedEntities(envId1));
            AssertTrue("env1 has 2 registered props", engine.Runtime.GetPropsForEnvironment(envId1).Count == 2);
            AssertTrue("env1 has 2 registered NPCs", engine.Runtime.GetNpcsForEnvironment(envId1).Count == 2);
            AssertTrue("env1 has 1 registered vehicle", engine.Runtime.GetVehiclesForEnvironment(envId1).Count == 1);

            // [Phase 4] Test Realize Idempotency
            Console.WriteLine("\n[Step 4] Testing Realize idempotency...");
            var idempotentRec = engine.Realize(envId1);
            AssertTrue("Idempotent Realize returns existing record without error", idempotentRec != null && idempotentRec.Status == "REALIZED");
            AssertTrue("Prop count unchanged after idempotent Realize", engine.Runtime.GetPropsForEnvironment(envId1).Count == 2);

            // [Phase 5] Mutate resident state in WorldMemoryStore
            Console.WriteLine("\n[Step 5] Mutating narrative state of Dealer Sophia...");
            engine.Store.UpdateNpcEmotionalState(envRec1.NpcIds[0], 0, 50, 0, "PANICKED");
            var sophiaRecord = engine.Store.GetNpc(envRec1.NpcIds[0]);
            AssertTrue("Sophia state in WorldMemoryStore mutated to 'PANICKED'", sophiaRecord.CurrentState == "PANICKED");

            // [Phase 6] Derealize Environment 1
            Console.WriteLine("\n[Step 6] Derealizing Environment 1 via engine.Derealize()...");
            bool derealized = engine.Derealize(envId1);
            AssertTrue("Derealize returned true", derealized);

            var derealizedRec1 = engine.Store.GetEnvironment(envId1);
            AssertTrue("Environment 1 status in WorldMemoryStore transitioned to 'DEREALIZED'", derealizedRec1.Status == "DEREALIZED");
            AssertTrue("LastDerealizedAt timestamp is set", derealizedRec1.LastDerealizedAt.HasValue);
            AssertTrue("Runtime has zero realized entities for env1 after derealization", !engine.Runtime.HasRealizedEntities(envId1));
            AssertTrue("Authoritative CanonicalPlan is PRESERVED on disk in PlanRepository", engine.Plans.HasPlan(envId1));

            // [Phase 7] Re-Realize from Preserved Canonical Plan
            Console.WriteLine("\n[Step 7] Re-realizing Environment 1 from preserved CanonicalPlan...");
            // Simulate re-spawning
            var reProp1 = CreateMockProp(1103);
            var reProp2 = CreateMockProp(1104);
            var rePed1 = CreateMockPed(2103);
            var reCtx1 = new ActiveNpcContext(rePed1, sophiaRecord, envId1, "npc_dealer_01");
            engine.Runtime.Spawner.RegisterProp(envId1, reProp1);
            engine.Runtime.Spawner.RegisterProp(envId1, reProp2);
            engine.Runtime.Spawner.RegisterNpc(envId1, reCtx1);

            var reRealizedRec1 = engine.Realize(envId1);
            AssertTrue("Re-realized environment transitioned back to 'REALIZED'", reRealizedRec1 != null && reRealizedRec1.Status == "REALIZED");
            AssertTrue("Re-realized environment has live entities", engine.Runtime.HasRealizedEntities(envId1));
            AssertTrue("Dynamic narrative state ('PANICKED') reconciled onto active context", reCtx1.LastAppliedBehaviorState == "PANICKED");

            // [Phase 8] Multi-Environment Isolation
            Console.WriteLine("\n[Step 8] Testing multi-environment isolation...");
            var propB = CreateMockProp(1201);
            engine.Runtime.Spawner.RegisterProp(envId2, propB);
            engine.Realize(envId2);

            AssertTrue("Environment 2 is REALIZED", engine.Store.GetEnvironment(envId2).Status == "REALIZED");
            AssertTrue("Environment 1 is REALIZED", engine.Store.GetEnvironment(envId1).Status == "REALIZED");

            // Derealize env1 only
            engine.Derealize(envId1);
            AssertTrue("Environment 1 is DEREALIZED", engine.Store.GetEnvironment(envId1).Status == "DEREALIZED");
            AssertTrue("Environment 2 remains REALIZED (untouched!)", engine.Store.GetEnvironment(envId2).Status == "REALIZED");
            AssertTrue("Environment 2 entities still active", engine.Runtime.HasRealizedEntities(envId2));

            // [Phase 9] DecommissionEnvironment (Permanent Purge)
            Console.WriteLine("\n[Step 9] Decommissioning Environment 2...");
            engine.DecommissionEnvironment(envId2);
            AssertTrue("Environment 2 purged from WorldMemoryStore", engine.Store.GetEnvironment(envId2) == null);
            AssertTrue("Environment 2 plan purged from PlanRepository", !engine.Plans.HasPlan(envId2));
            AssertTrue("Environment 2 runtime entities cleared", !engine.Runtime.HasRealizedEntities(envId2));

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("   TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
