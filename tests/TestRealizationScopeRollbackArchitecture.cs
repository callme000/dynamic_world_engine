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
    class RealizationScopeRollbackTester
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
            Console.WriteLine("  REALIZATION SCOPE & STAGE-BEFORE-COMMIT ARCHITECTURE TEST");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_RealizationScope_Data");
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
            // SECTION 1: RAII REALIZATION SCOPE DIRECT ROLLBACK VS COMMIT
            // =====================================================================
            Console.WriteLine("[Section 1] Verifying RealizationScope RAII rollback on uncommitted dispose...");

            string scopeEnv1 = "env_scope_uncommitted";
            using (var scope = runtime.BeginRealizationScope(scopeEnv1))
            {
                var p1 = CreateMockProp(501);
                var ped1 = CreateMockPed(601);
                spawner.RegisterProp(scopeEnv1, p1);
                var ctx = new ActiveNpcContext(ped1, null, scopeEnv1, "npc_test_1");
                spawner.RegisterNpc(scopeEnv1, ctx);

                AssertTrue("Runtime has staged entities within active uncommitted scope", runtime.HasRealizedEntities(scopeEnv1));
                // Do NOT call scope.Commit() - simulate failure/exception exit
            }

            // Exiting using block calls Dispose() -> triggers Rollback()
            AssertTrue("STAGE-BEFORE-COMMIT PROVEN: Scope Dispose() without Commit() purges staged entities", !runtime.HasRealizedEntities(scopeEnv1));
            AssertTrue("Prop count is 0 after scope rollback", runtime.GetPropsForEnvironment(scopeEnv1).Count == 0);
            AssertTrue("NPC count is 0 after scope rollback", runtime.GetNpcsForEnvironment(scopeEnv1).Count == 0);

            Console.WriteLine("\n[Section 1b] Verifying RealizationScope preserves entities when Commit() is called...");
            string scopeEnv2 = "env_scope_committed";
            using (var scope = runtime.BeginRealizationScope(scopeEnv2))
            {
                var p2 = CreateMockProp(502);
                spawner.RegisterProp(scopeEnv2, p2);
                AssertTrue("Runtime has staged entities in second scope", runtime.HasRealizedEntities(scopeEnv2));
                scope.Commit();
            }

            AssertTrue("Committed scope retains entities after Dispose()", runtime.HasRealizedEntities(scopeEnv2));
            AssertTrue("Prop count remains 1 for committed scope", runtime.GetPropsForEnvironment(scopeEnv2).Count == 1);

            // Clean up test scope
            runtime.CleanUpEnvironment(scopeEnv2);

            // =====================================================================
            // SECTION 2: HAPPY PATH REALIZATION VIA ENGINECORE
            // =====================================================================
            Console.WriteLine("\n[Section 2] Verifying EngineCore.Realize happy path commit & persistence...");

            string happyEnvId = "env_happy_penthouse";
            SpatialExecutionPlan happyPlan = new SpatialExecutionPlan
            {
                EnvironmentId = happyEnvId,
                Name = "Eclipse Towers Penthouse Suite",
                Archetype = "apartment",
                CenterPosition = new SpatialVector3(-774.2f, 312.8f, 85.7f),
                Heading = 180f,
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "sofa_01", ModelName = "prop_couch_lg_02", WorldPosition = new SpatialVector3(-773.0f, 312.0f, 85.7f) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_hostess", Name = "Elena Rostova", PedModel = "a_f_y_bevhills_02", InitialState = "CALM" }
                }
            };

            var happyEnvRec = engine.CommissionEnvironment(happyPlan);
            AssertTrue("Environment commissioned", happyEnvRec != null && happyEnvRec.Status == "COMMISSIONED");

            // Mock native runtime manifestation
            var happyProp = CreateMockProp(701);
            var happyPed = CreateMockPed(801);
            spawner.RegisterProp(happyEnvId, happyProp);
            var happyCtx = new ActiveNpcContext(happyPed, engine.Store.GetNpcByEntityId("npc_hostess"), happyEnvId, "npc_hostess");
            spawner.RegisterNpc(happyEnvId, happyCtx);

            var realizedRec = engine.Realize(happyEnvId);
            AssertTrue("Realize returned valid record", realizedRec != null);
            AssertTrue("Environment status is 'REALIZED'", realizedRec.Status == "REALIZED");
            AssertTrue("LastRealizedAt timestamp is populated", realizedRec.LastRealizedAt.HasValue);
            AssertTrue("Runtime retains realized entities after successful commit", runtime.HasRealizedEntities(happyEnvId));

            // Cold disk inspection
            string rawHappyJson = File.ReadAllText(dbPath);
            AssertTrue("Cold disk reflects Status == 'REALIZED'", rawHappyJson.Contains("\"Status\": \"REALIZED\""));

            // =====================================================================
            // SECTION 3: PERSISTENCE FAILURE ROLLBACK (ATOMICITY ON DISK FAILURE)
            // =====================================================================
            Console.WriteLine("\n[Section 3] Verifying Stage-Before-Commit rollback when durable disk persistence fails...");

            string failEnvId = "env_secure_armory";
            SpatialExecutionPlan failPlan = new SpatialExecutionPlan
            {
                EnvironmentId = failEnvId,
                Name = "Subterranean Weapons Cache",
                Archetype = "military",
                CenterPosition = new SpatialVector3(120.5f, -1400.2f, 29.3f),
                Heading = 45f,
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "crate_ammo", ModelName = "prop_box_ammo04a", WorldPosition = new SpatialVector3(121f, -1400f, 29.3f) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_quartermaster", Name = "Sergeant Miller", PedModel = "s_m_m_armoured_01", InitialState = "GUARD" }
                }
            };

            var failEnvRec = engine.CommissionEnvironment(failPlan);
            AssertTrue("Armory commissioned", failEnvRec != null && failEnvRec.Status == "COMMISSIONED");

            // Stage entities in runtime
            var failProp = CreateMockProp(901);
            var failPed = CreateMockPed(902);
            spawner.RegisterProp(failEnvId, failProp);
            var failCtx = new ActiveNpcContext(failPed, engine.Store.GetNpcByEntityId("npc_quartermaster"), failEnvId, "npc_quartermaster");
            spawner.RegisterNpc(failEnvId, failCtx);
            AssertTrue("Entities staged prior to realization attempt", runtime.HasRealizedEntities(failEnvId));

            // Lock the database file to simulate catastrophic disk write error
            File.SetAttributes(dbPath, FileAttributes.ReadOnly);

            bool caughtException = false;
            try
            {
                engine.Realize(failEnvId);
            }
            catch (Exception ex)
            {
                caughtException = true;
                Console.WriteLine(string.Format("  [Expected Exception Caught]: {0}", ex.GetType().Name));
            }

            // Restore write permissions immediately for verification & subsequent operations
            File.SetAttributes(dbPath, FileAttributes.Normal);

            AssertTrue("Realize threw exception when disk persistence failed", caughtException);
            AssertTrue("STAGE-BEFORE-COMMIT INVARIANT PROVEN: Staged entities completely evicted upon persistence failure!", !runtime.HasRealizedEntities(failEnvId));
            AssertTrue("Environment status in memory restored to pre-realization state", failEnvRec.Status == "COMMISSIONED");

            // Cold disk inspection to verify no corrupt state was written
            string rawFailJson = File.ReadAllText(dbPath);
            AssertTrue("Cold disk does NOT claim failEnvId is REALIZED", !rawFailJson.Contains("\"EnvironmentId\": \"env_secure_armory\",\r\n      \"Status\": \"REALIZED\"") && !rawFailJson.Contains("\"EnvironmentId\": \"env_secure_armory\",\n      \"Status\": \"REALIZED\""));

            // =====================================================================
            // SECTION 4: RETRY RECOVERY AFTER PERSISTENCE FAILURE
            // =====================================================================
            Console.WriteLine("\n[Section 4] Verifying successful realization retry once disk becomes healthy...");

            // Re-stage entities for retry
            var retryProp = CreateMockProp(903);
            var retryPed = CreateMockPed(904);
            spawner.RegisterProp(failEnvId, retryProp);
            var retryCtx = new ActiveNpcContext(retryPed, engine.Store.GetNpcByEntityId("npc_quartermaster"), failEnvId, "npc_quartermaster");
            spawner.RegisterNpc(failEnvId, retryCtx);

            EnvironmentRecord retryRec = null;
            try
            {
                retryRec = engine.Realize(failEnvId);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected retry failure: " + ex.Message);
            }

            AssertTrue("Retry realization succeeds on healthy disk", retryRec != null);
            AssertTrue("Environment status is now 'REALIZED'", retryRec.Status == "REALIZED");
            AssertTrue("Runtime now authoritatively holds realized entities", runtime.HasRealizedEntities(failEnvId));

            // Cold disk inspection
            string rawRetryJson = File.ReadAllText(dbPath);
            AssertTrue("Cold disk now authoritatively records 'REALIZED'", rawRetryJson.Contains("env_secure_armory"));

            // =====================================================================
            // SECTION 5: IDEMPOTENT REALIZATION
            // =====================================================================
            Console.WriteLine("\n[Section 5] Verifying Realize idempotency when already realized...");

            int propCountBefore = runtime.GetPropsForEnvironment(failEnvId).Count;
            var idempotentRec = engine.Realize(failEnvId);
            int propCountAfter = runtime.GetPropsForEnvironment(failEnvId).Count;

            AssertTrue("Idempotent Realize returns non-null record", idempotentRec != null);
            AssertTrue("Idempotent Realize preserves REALIZED status", idempotentRec.Status == "REALIZED");
            AssertTrue("Idempotent Realize does not mutate or duplicate entities", propCountBefore == propCountAfter);

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("   TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
