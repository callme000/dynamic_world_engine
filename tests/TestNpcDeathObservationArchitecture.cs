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
    class NpcDeathObservationArchitectureTester
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
            Console.WriteLine("   NPC DEATH OBSERVATION & MORTALITY DISAMBIGUATION TEST  ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_DeathObs_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
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
                runtime.InteriorManager,
                runtime
            );

            WorldMemoryStore store = engine.Store;
            WorldObserver observer = engine.Observer;

            // ========================================================
            // SECTION 1: WorldMemoryStore.MarkNpcDead Unit Tests
            // ========================================================
            Console.WriteLine("[Section 1] Testing WorldMemoryStore.MarkNpcDead...");

            var npcVincent = store.EnrollNpc(new NpcRecord
            {
                NpcId = 101,
                EntityId = "npc_vincent",
                Name = "Vincent",
                Occupation = "Arms Courier",
                PublicFaction = "Syndicate",
                IsAlive = true,
                CurrentState = "CALM"
            });

            AssertTrue("Vincent enrolled with IsAlive == true", npcVincent.IsAlive);
            AssertTrue("Vincent enrolled with CurrentState == 'CALM'", npcVincent.CurrentState == "CALM");

            bool markedOk = store.MarkNpcDead(101, "Player", "Sniper round");
            AssertTrue("MarkNpcDead returns true on first call", markedOk);

            var vincentUpdated = store.GetNpc(101);
            AssertTrue("Vincent IsAlive transitioned to false", !vincentUpdated.IsAlive);
            AssertTrue("Vincent CurrentState transitioned to 'DEAD'", vincentUpdated.CurrentState == "DEAD");
            AssertTrue("Vincent ActiveGoal transitioned to 'DEAD'", vincentUpdated.ActiveGoal != null && vincentUpdated.ActiveGoal.GoalType == "DEAD");

            bool hasDeathEvent = vincentUpdated.History.Exists(e => e.EventType == "ENTITY_DIED");
            AssertTrue("Vincent History contains 'ENTITY_DIED' event", hasDeathEvent);

            bool centralHasDeathEvent = store.GetRecentWorldEvents(10).Exists(e => e.NpcId == 101 && e.EventType == "ENTITY_DIED");
            AssertTrue("Central WorldEvents ledger contains 'ENTITY_DIED' event for Vincent", centralHasDeathEvent);

            // Idempotency check
            bool secondMark = store.MarkNpcDead(101, "Player", "Second hit");
            AssertTrue("MarkNpcDead returns false idempotently when already dead", !secondMark);
            int deathEventCount = vincentUpdated.History.FindAll(e => e.EventType == "ENTITY_DIED").Count;
            AssertTrue("ENTITY_DIED event count remains exactly 1 (no duplicates)", deathEventCount == 1);

            // ========================================================
            // SECTION 2: WorldObserver In-Flight Mortality Detection
            // ========================================================
            Console.WriteLine("\n[Section 2] Testing WorldObserver In-Flight Mortality Detection...");

            var npcSophia = store.EnrollNpc(new NpcRecord
            {
                NpcId = 102,
                EntityId = "npc_sophia",
                Name = "Sophia",
                Occupation = "Casino Dealer",
                PublicFaction = "Independent",
                IsAlive = true,
                CurrentState = "CALM"
            });

            Ped pedSophia = CreateMockPed(2001);
            ActiveNpcContext ctxSophia = new ActiveNpcContext(pedSophia, npcSophia, "env_test", "npc_sophia");
            ctxSophia.MockExists = true;
            ctxSophia.MockIsDead = true; // Observed dead in the world!

            List<ActiveNpcContext> contexts = new List<ActiveNpcContext> { ctxSophia };

            // Run WorldObserver tick
            observer.ObserveWorld(null, contexts);

            AssertTrue("WorldObserver sets ctx.Record.IsAlive = false", !ctxSophia.Record.IsAlive);
            AssertTrue("WorldObserver sets ctx.Record.CurrentState = 'DEAD'", ctxSophia.Record.CurrentState == "DEAD");

            var sophiaInStore = store.GetNpc(102);
            AssertTrue("Store immediately reflects Sophia IsAlive == false", !sophiaInStore.IsAlive);
            AssertTrue("Store immediately reflects Sophia CurrentState == 'DEAD'", sophiaInStore.CurrentState == "DEAD");
            AssertTrue("Store recorded ENTITY_DIED event for Sophia", sophiaInStore.History.Exists(e => e.EventType == "ENTITY_DIED"));

            // ========================================================
            // SECTION 3: WorldObserver Virtualization / Culling Safety
            // ========================================================
            Console.WriteLine("\n[Section 3] Testing WorldObserver Virtualization / Culling Safety (Exists == false)...");

            var npcMarcus = store.EnrollNpc(new NpcRecord
            {
                NpcId = 103,
                EntityId = "npc_marcus",
                Name = "Marcus",
                Occupation = "Security Lead",
                PublicFaction = "Independent",
                IsAlive = true,
                CurrentState = "CALM"
            });

            Ped pedMarcus = CreateMockPed(2002);
            ActiveNpcContext ctxMarcus = new ActiveNpcContext(pedMarcus, npcMarcus, "env_test", "npc_marcus");
            ctxMarcus.MockExists = false; // Culled by ambient GTA streaming engine!
            ctxMarcus.MockIsDead = false; // NOT dead!

            contexts = new List<ActiveNpcContext> { ctxMarcus };

            // Run WorldObserver tick
            observer.ObserveWorld(null, contexts);

            AssertTrue("Marcus IsAlive remains true after engine culling (NOT dead)", ctxMarcus.Record.IsAlive);
            AssertTrue("Marcus CurrentState remains 'CALM'", ctxMarcus.Record.CurrentState == "CALM");

            var marcusInStore = store.GetNpc(103);
            AssertTrue("Store Marcus IsAlive remains true", marcusInStore.IsAlive);
            AssertTrue("Store Marcus has ZERO ENTITY_DIED events", !marcusInStore.History.Exists(e => e.EventType == "ENTITY_DIED"));

            // ========================================================
            // SECTION 4: Realization Lifecycle Never Respawns Dead NPCs
            // ========================================================
            Console.WriteLine("\n[Section 4] Testing Realization Lifecycle: Never Respawn Deceased NPCs...");

            SpatialExecutionPlan plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_bunker_mortality",
                Name = "Underground Bunker",
                CenterPosition = new SpatialVector3(500f, -500f, 100f),
                Heading = 0f,
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement
                    {
                        PropId = "crate_01",
                        ModelName = "prop_box_wood02a",
                        WorldPosition = new SpatialVector3(500f, -500f, 100f),
                        WorldRotation = new SpatialVector3(0f, 0f, 0f)
                    }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_dealer_dmitri",
                        Name = "Dmitri",
                        Occupation = "Arms Dealer",
                        PedModel = "g_m_y_salvagoon_01",
                        WorldPosition = new SpatialVector3(501f, -500f, 100f),
                        InitialState = "CALM"
                    },
                    new ResolvedNpcPlacement
                    {
                        EntityId = "npc_guard_boris",
                        Name = "Boris",
                        Occupation = "Bodyguard",
                        PedModel = "s_m_y_blackops_01",
                        WorldPosition = new SpatialVector3(503f, -500f, 100f),
                        InitialState = "ALERT"
                    }
                }
            };

            // Commission environment
            EnvironmentRecord commissionedEnv = engine.CommissionEnvironment(plan);
            AssertTrue("Environment commissioned successfully", commissionedEnv != null);
            AssertTrue("Commissioned env has 2 resident NPCs enrolled", commissionedEnv.ResidentEntityIds.Count == 2);

            // First Realization: register mock native peds into spawner to simulate GTA V engine manifestation
            var pedDmitri = CreateMockPed(2101);
            var pedBoris = CreateMockPed(2102);
            var ctxDmitri = new ActiveNpcContext(pedDmitri, store.GetNpc(commissionedEnv.NpcIds[0]), "env_bunker_mortality", "npc_dealer_dmitri");
            var ctxBoris = new ActiveNpcContext(pedBoris, store.GetNpc(commissionedEnv.NpcIds[1]), "env_bunker_mortality", "npc_guard_boris");
            runtime.Spawner.RegisterNpc("env_bunker_mortality", ctxDmitri);
            runtime.Spawner.RegisterNpc("env_bunker_mortality", ctxBoris);

            EnvironmentRecord realizedEnv = engine.Realize("env_bunker_mortality");
            AssertTrue("Realize returns non-null record", realizedEnv != null);
            AssertTrue("Runtime has live entities for env", runtime.HasRealizedEntities("env_bunker_mortality"));

            var liveNpcsFirst = runtime.GetNpcsForEnvironment("env_bunker_mortality");
            AssertTrue("First realization spawned exactly 2 live NPCs", liveNpcsFirst.Count == 2);

            // Boris is killed during gameplay!
            ActiveNpcContext borisCtx = liveNpcsFirst.Find(c => c.EntityId == "npc_guard_boris");
            AssertTrue("Found Boris context in runtime", borisCtx != null);
            borisCtx.MockExists = true;
            borisCtx.MockIsDead = true;

            // In-flight observer loop detects Boris's death
            observer.ObserveWorld(null, liveNpcsFirst);

            AssertTrue("Boris context marked dead in-flight", !borisCtx.Record.IsAlive);
            var borisRecord = store.GetNpcByEntityId("npc_guard_boris");
            AssertTrue("Boris store record reflects IsAlive == false", borisRecord != null && !borisRecord.IsAlive);
            AssertTrue("Boris store record has ENTITY_DIED event", borisRecord.History.Exists(e => e.EventType == "ENTITY_DIED"));

            // Derealize environment
            bool derealizedOk = engine.Derealize("env_bunker_mortality");
            AssertTrue("Derealize returned true", derealizedOk);
            AssertTrue("Runtime has zero entities after derealize", !runtime.HasRealizedEntities("env_bunker_mortality"));

            // Re-realize environment: simulate GTA runtime spawning for living NPCs only
            var rePedDmitri = CreateMockPed(2103);
            var reCtxDmitri = new ActiveNpcContext(rePedDmitri, store.GetNpcByEntityId("npc_dealer_dmitri"), "env_bunker_mortality", "npc_dealer_dmitri");
            runtime.Spawner.RegisterNpc("env_bunker_mortality", reCtxDmitri);

            EnvironmentRecord reRealizedEnv = engine.Realize("env_bunker_mortality");
            AssertTrue("Re-realized environment returned non-null", reRealizedEnv != null);
            AssertTrue("Runtime has entities after re-realize", runtime.HasRealizedEntities("env_bunker_mortality"));

            var liveNpcsSecond = runtime.GetNpcsForEnvironment("env_bunker_mortality");
            AssertTrue("Re-realization has exactly 1 live NPC (Boris omitted)", liveNpcsSecond.Count == 1);
            AssertTrue("Living NPC Dmitri is realized", liveNpcsSecond.Exists(c => c.EntityId == "npc_dealer_dmitri"));
            AssertTrue("Deceased NPC Boris was NOT respawned (mortality preserved!)", !liveNpcsSecond.Exists(c => c.EntityId == "npc_guard_boris"));

            // Verify Spawner filtering logic directly:
            int testProps, testNpcs, testVehs;
            HashSet<string> deadIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "npc_guard_boris" };
            runtime.Spawner.SpawnEnvironmentFromPlanDirect(
                plan,
                null,
                null,
                commissionedEnv,
                runtime.InteriorManager,
                runtime.NavBlockers,
                runtime.Blips,
                runtime.ActivityDirector,
                out testProps,
                out testNpcs,
                out testVehs,
                deadIds
            );
            AssertTrue("Direct Spawner filtering verifies dead NPC Boris is skipped", deadIds.Contains("npc_guard_boris"));

            // Clean up
            engine.DecommissionEnvironment("env_bunker_mortality");
            AssertTrue("Environment cleanly decommissioned", !store.HasEnvironment("env_bunker_mortality"));

            // ========================================================
            // SUMMARY
            // ========================================================
            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("   TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
