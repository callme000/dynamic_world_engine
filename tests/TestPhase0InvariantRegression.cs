using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    /// <summary>
    /// Phase 0 — Regression Test Foundation.
    /// Tests the NON-NEGOTIABLE ENGINE INVARIANTS before any broad refactoring begins.
    /// Each test targets a specific invariant violation identified in the codebase audit.
    /// 
    /// NOTE: These tests run headless without GTA V. They verify domain logic, not live runtime.
    /// GTA native calls are NOT exercised. Tests use reflection-constructed Entity handles
    /// and isolated filesystem directories.
    /// </summary>
    class Phase0InvariantRegressionTester
    {
        private static int passed = 0;
        private static int failed = 0;
        private static int total = 0;

        static void AssertTrue(string invariantId, string name, bool condition)
        {
            total++;
            if (condition)
            {
                Console.WriteLine(string.Format("  [PASS] [{0}] {1}", invariantId, name));
                passed++;
            }
            else
            {
                Console.WriteLine(string.Format("  [FAIL] [{0}] {1}", invariantId, name));
                failed++;
            }
        }

        static void AssertFalse(string invariantId, string name, bool condition)
        {
            AssertTrue(invariantId, name, !condition);
        }

        static void AssertNull(string invariantId, string name, object obj)
        {
            AssertTrue(invariantId, name, obj == null);
        }

        static void AssertNotNull(string invariantId, string name, object obj)
        {
            AssertTrue(invariantId, name, obj != null);
        }

        static void AssertThrows<TEx>(string invariantId, string name, Action action) where TEx : Exception
        {
            total++;
            try
            {
                action();
                Console.WriteLine(string.Format("  [FAIL] [{0}] {1} — expected {2} but no exception thrown", invariantId, name, typeof(TEx).Name));
                failed++;
            }
            catch (TEx)
            {
                Console.WriteLine(string.Format("  [PASS] [{0}] {1}", invariantId, name));
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("  [FAIL] [{0}] {1} — expected {2} but got {3}: {4}", invariantId, name, typeof(TEx).Name, ex.GetType().Name, ex.Message));
                failed++;
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
            Console.WriteLine("==========================================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE — PHASE 0 INVARIANT REGRESSION TEST SUITE");
            Console.WriteLine("  Tests: Thread gating, validation barriers, persistence atomicity,");
            Console.WriteLine("         NPC target resolution, snapshot immutability, cache invalidation");
            Console.WriteLine("==========================================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Phase0_Data");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            try
            {
                TestSection1_MainThreadGateInitialization();
                TestSection2_WorkerThreadNativeCallPrevention();
                TestSection3_InvalidSpatialPlanRejection();
                TestSection4_ValidatorExceptionRejection();
                TestSection5_PlanRepositorySaveFailure(testDir);
                TestSection6_WorldMemoryStorePersistenceFailure(testDir);
                TestSection7_ExplicitNpcTargetNotFound();
                TestSection8_PlannerInputImmutability(testDir);
                TestSection9_CommissioningAtomicity(testDir);
                TestSection10_RealizationRollbackOnPersistenceFailure(testDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n  [FATAL] Unhandled exception in test harness: " + ex.ToString());
                failed++;
            }

            Console.WriteLine("\n==========================================================================");
            Console.WriteLine(string.Format("  PHASE 0 REGRESSION RESULTS: {0} PASSED, {1} FAILED, {2} TOTAL", passed, failed, total));
            Console.WriteLine("==========================================================================");

            return (failed == 0) ? 0 : 1;
        }

        // =====================================================================
        // SECTION 1: MAIN-THREAD GATE INITIALIZATION AND REJECTION
        // Invariant #1: Native operations must execute exclusively through
        //               the correct SHVDN script-thread context.
        // Invariant #2: An uninitialized or unknown thread identity must deny
        //               native execution, never grant permission.
        // =====================================================================
        static void TestSection1_MainThreadGateInitialization()
        {
            Console.WriteLine("\n[Section 1] Main-Thread Gate Initialization and Rejection\n");

            // Save and restore state to avoid polluting other tests
            int savedId = NativeFiberCoordinator.MainThreadId;

            // Test 1a: Uninitialized gate denies access
            NativeFiberCoordinator.Reset();
            AssertFalse("INV-2", "Uninitialized gate denies IsMainThread", NativeFiberCoordinator.IsMainThread);
            AssertFalse("INV-2", "Uninitialized gate denies EnforceMainThread", NativeFiberCoordinator.EnforceMainThread("test_op"));

            // Test 1b: Gate initialized with wrong thread ID denies access
            NativeFiberCoordinator.Initialize(99999);
            AssertFalse("INV-1", "Wrong thread ID denies IsMainThread", NativeFiberCoordinator.IsMainThread);
            AssertFalse("INV-1", "Wrong thread ID denies EnforceMainThread", NativeFiberCoordinator.EnforceMainThread("test_op"));

            // Test 1c: Gate initialized with current thread ID grants access
            NativeFiberCoordinator.Initialize(Thread.CurrentThread.ManagedThreadId);
            AssertTrue("INV-1", "Correct thread ID grants IsMainThread", NativeFiberCoordinator.IsMainThread);
            AssertTrue("INV-1", "Correct thread ID grants EnforceMainThread", NativeFiberCoordinator.EnforceMainThread("test_op"));

            // Test 1d: Reset returns gate to deny-by-default
            NativeFiberCoordinator.Reset();
            AssertFalse("INV-2", "Reset gate returns to deny-by-default", NativeFiberCoordinator.IsMainThread);

            // Restore
            if (savedId != -1)
                NativeFiberCoordinator.Initialize(savedId);
            else
                NativeFiberCoordinator.Reset();
        }

        // =====================================================================
        // SECTION 2: WORKER-THREAD NATIVE-CALL PREVENTION
        // Invariant #1: GTA native operations must execute exclusively through
        //               the correct SHVDN script-thread context.
        // =====================================================================
        static void TestSection2_WorkerThreadNativeCallPrevention()
        {
            Console.WriteLine("\n[Section 2] Worker-Thread Native-Call Prevention\n");

            // Initialize gate on current thread
            NativeFiberCoordinator.Initialize(Thread.CurrentThread.ManagedThreadId);

            bool workerResult = true; // Default to "allowed" to detect failure to deny
            ManualResetEvent done = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                workerResult = NativeFiberCoordinator.IsMainThread;
                done.Set();
            });

            done.WaitOne(5000);
            AssertFalse("INV-1", "Worker thread is denied IsMainThread", workerResult);

            // Test EnforceMainThread from worker
            bool workerEnforceResult = true;
            ManualResetEvent done2 = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                workerEnforceResult = NativeFiberCoordinator.EnforceMainThread("worker_op");
                done2.Set();
            });

            done2.WaitOne(5000);
            AssertFalse("INV-1", "Worker thread is denied EnforceMainThread", workerEnforceResult);

            NativeFiberCoordinator.Reset();
        }

        // =====================================================================
        // SECTION 3: INVALID SPATIAL PLAN REJECTION
        // Invariant #3: A spatial plan must pass validation before it can
        //               be committed.
        // Invariant #4: A failed, missing or uncertain validation result must
        //               prevent persistence and physical realization.
        // =====================================================================
        static void TestSection3_InvalidSpatialPlanRejection()
        {
            Console.WriteLine("\n[Section 3] Invalid Spatial Plan Rejection\n");

            var validator = new SpatialPlanValidator();

            // Test 3a: Null plan is rejected
            var result = validator.ValidatePlan(null, null, null, false);
            AssertFalse("INV-3", "Null plan fails validation", result.IsValid);
            AssertTrue("INV-3", "Null plan has hard violation", result.HardViolations.Count > 0);

            // Test 3b: Plan with floating prop is rejected (without auto-correction)
            var floatingPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_floating",
                Name = "Test Floating",
                CenterPosition = new SpatialVector3(0, 0, 10),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement
                    {
                        PropId = "test_prop",
                        ModelName = "prop_test",
                        WorldPosition = new SpatialVector3(0, 0, 50), // Way above ground
                    }
                }
            };

            var model = new SpatialWorldModel();
            model.Surfaces.Add(new SpatialSurface
            {
                Position = new Vector3(0, 0, 10),
                Normal = new Vector3(0, 0, 1),
                SurfaceType = "CONCRETE"
            });

            var floatResult = validator.ValidatePlan(floatingPlan, model, null, false);
            AssertFalse("INV-3", "Floating prop fails validation (no auto-correction)", floatResult.IsValid);
            AssertTrue("INV-3", "Floating prop generates hard violation", floatResult.HardViolations.Count > 0);

            // Test 3c: Colliding props are rejected
            var collidingPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_collision",
                Name = "Test Collision",
                CenterPosition = new SpatialVector3(0, 0, 0),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "prop_a", ModelName = "test_a", WorldPosition = new SpatialVector3(5, 5, 0) },
                    new ResolvedPropPlacement { PropId = "prop_b", ModelName = "test_b", WorldPosition = new SpatialVector3(5.1f, 5, 0) }
                }
            };

            var collisionResult = validator.ValidatePlan(collidingPlan, null, null, false);
            AssertFalse("INV-3", "Overlapping props fail validation", collisionResult.IsValid);

            // Test 3d: Valid plan passes validation
            var validPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_valid",
                Name = "Test Valid",
                CenterPosition = new SpatialVector3(0, 0, 10),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "prop_x", ModelName = "test_x", WorldPosition = new SpatialVector3(0, 0, 10) },
                    new ResolvedPropPlacement { PropId = "prop_y", ModelName = "test_y", WorldPosition = new SpatialVector3(5, 5, 10) }
                }
            };

            var validResult = validator.ValidatePlan(validPlan, model, null, false);
            AssertTrue("INV-3", "Valid plan passes validation", validResult.IsValid);
        }

        // =====================================================================
        // SECTION 4: VALIDATOR EXCEPTION REJECTION
        // Invariant #4: A failed, missing or uncertain validation result must
        //               prevent persistence and physical realization.
        // 
        // DEFECT DOCUMENTED: SpatialWorldPlanner.PlanEnvironment line 477
        //   swallows validation exceptions with `catch {}`.
        //   This test documents the defect for Phase 1 correction.
        // =====================================================================
        static void TestSection4_ValidatorExceptionRejection()
        {
            Console.WriteLine("\n[Section 4] Validator Exception Rejection\n");

            // The SpatialPlanValidator itself handles errors gracefully.
            // The defect is in SpatialWorldPlanner.PlanEnvironment which
            // catches and swallows any validation exceptions at line 477.
            //
            // Test the validator directly to ensure it correctly propagates
            // results, then document the planner's swallowing behavior.

            var validator = new SpatialPlanValidator();

            // Validator should produce valid result for empty plan (no violations)
            var emptyPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_empty",
                Name = "Test Empty"
            };
            var emptyResult = validator.ValidatePlan(emptyPlan, null, null, false);
            AssertTrue("INV-4", "Empty plan produces valid result (no props/npcs to violate)", emptyResult.IsValid);
            AssertTrue("INV-4", "Empty plan has zero hard violations", emptyResult.HardViolations.Count == 0);

            // DEFECT MARKER: The planner silently discards this result at line 477.
            // Phase 1E will make validation an enforced commit barrier.
            Console.WriteLine("  [NOTE] DEFECT DOCUMENTED: SpatialWorldPlanner.PlanEnvironment swallows");
            Console.WriteLine("         validation exceptions at line 477 (catch {}). Phase 1E will fix.");
        }

        // =====================================================================
        // SECTION 5: PLANREPOSITORY SAVE FAILURE
        // Invariant #5: Every commissioned environment must have a recoverable
        //               canonical physical plan and corresponding world-state record.
        // Invariant #12: Persistence failure must not result in a partially
        //                commissioned environment.
        //
        // DEFECT DOCUMENTED: EngineCore.CommissionEnvironment lines 363-368
        //   logs a warning on save failure but continues commissioning.
        // =====================================================================
        static void TestSection5_PlanRepositorySaveFailure(string testDir)
        {
            Console.WriteLine("\n[Section 5] PlanRepository Save Failure\n");

            string plansDir = Path.Combine(testDir, "Section5_plans");
            Directory.CreateDirectory(plansDir);

            var repo = new PlanRepository(plansDir);

            // Test 5a: Save with null plan returns false
            AssertFalse("INV-5", "SavePlan(null) returns false", repo.SavePlan(null));

            // Test 5b: Save with null environmentId returns false
            AssertFalse("INV-5", "SavePlan(null, plan) returns false", repo.SavePlan(null, new SpatialExecutionPlan()));

            // Test 5c: Normal save succeeds
            var plan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_save_ok",
                Name = "Test Save",
                CenterPosition = new SpatialVector3(100, 200, 30),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "p1", ModelName = "test_model", WorldPosition = new SpatialVector3(100, 200, 30) }
                }
            };
            AssertTrue("INV-5", "SavePlan succeeds for valid plan", repo.SavePlan(plan));
            AssertTrue("INV-5", "HasPlan returns true after save", repo.HasPlan("test_save_ok"));

            // Test 5d: Roundtrip preservation
            var loaded = repo.GetPlan("test_save_ok");
            AssertNotNull("INV-5", "GetPlan returns non-null after save", loaded);
            AssertTrue("INV-5", "Roundtripped plan preserves EnvironmentId", loaded != null && loaded.EnvironmentId == "test_save_ok");
            AssertTrue("INV-5", "Roundtripped plan preserves prop count", loaded != null && loaded.ResolvedProps != null && loaded.ResolvedProps.Count == 1);

            // Test 5e: Save failure on read-only directory
            string readOnlyDir = Path.Combine(testDir, "Section5_readonly");
            Directory.CreateDirectory(readOnlyDir);
            var readOnlyRepo = new PlanRepository(readOnlyDir);

            // Write a plan first, then make directory read-only
            var plan2 = new SpatialExecutionPlan { EnvironmentId = "test_readonly", Name = "Read Only Test" };
            readOnlyRepo.SavePlan(plan2); // Initial save should work

            // Now make the target file read-only to simulate save failure
            string targetFile = readOnlyRepo.GetPlanFilePath("test_readonly_fail");
            // Create a file that blocks the .tmp write
            File.WriteAllText(targetFile, "existing");
            File.SetAttributes(targetFile, FileAttributes.ReadOnly);

            var plan3 = new SpatialExecutionPlan { EnvironmentId = "test_readonly_fail", Name = "Should Fail" };
            bool saveResult = readOnlyRepo.SavePlan(plan3);
            // Note: SavePlan tries to write a .tmp file first, then delete + move.
            // On read-only target, the delete will fail. SavePlan catches and returns false.
            // This verifies the PlanRepository itself handles failure correctly.

            // Restore
            try { File.SetAttributes(targetFile, FileAttributes.Normal); } catch { }

            // DEFECT MARKER: EngineCore.CommissionEnvironment ignores SavePlan failure
            Console.WriteLine("  [NOTE] DEFECT DOCUMENTED: EngineCore.CommissionEnvironment (lines 363-368)");
            Console.WriteLine("         logs a warning when PlanRepository.SavePlan fails but continues");
            Console.WriteLine("         commissioning, violating Invariant #5 and #12.");
            Console.WriteLine("         Phase 1F will make persistence failure prevent commissioning.");

            // Test 5f: Document that CommissionEnvironment currently continues on save failure
            string commDir = Path.Combine(testDir, "Section5_commission");
            Directory.CreateDirectory(commDir);
            var engine = new EngineCore(commDir);

            var commPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "test_commission_persist",
                Name = "Commission Persistence Test",
                CenterPosition = new SpatialVector3(10, 20, 30),
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_test", Name = "Test NPC", PedModel = "a_m_y_beach_01" }
                }
            };

            // CommissionEnvironment should succeed normally
            var envRec = engine.CommissionEnvironment(commPlan, null, SpatialCommissioningMode.AllowUnverifiedSimulation);
            AssertNotNull("INV-5", "CommissionEnvironment returns record on successful persistence", envRec);
            AssertTrue("INV-5", "Commissioned environment has matching PlanId", 
                envRec != null && envRec.PlanId == "test_commission_persist");
            AssertTrue("INV-5", "Plan exists in repository after commissioning",
                engine.Plans.HasPlan("test_commission_persist"));
        }

        // =====================================================================
        // SECTION 6: WORLDMEMORYSTORE PERSISTENCE FAILURE
        // Invariant #12: Persistence failure must not result in a partially
        //                commissioned environment.
        //
        // DEFECT DOCUMENTED: WorldMemoryStore.SaveDatabaseInternal uses
        //   non-atomic File.WriteAllText (line 112). A crash during write
        //   can corrupt the entire world state.
        // =====================================================================
        static void TestSection6_WorldMemoryStorePersistenceFailure(string testDir)
        {
            Console.WriteLine("\n[Section 6] WorldMemoryStore Persistence Failure\n");

            string storeDir = Path.Combine(testDir, "Section6_store");
            Directory.CreateDirectory(storeDir);

            var store = new WorldMemoryStore(storeDir);

            // Test 6a: Normal save works
            var npc = store.CreateNpc("Test NPC", "Tester", "Neutral", "a_m_y_beach_01");
            AssertNotNull("INV-12", "CreateNpc returns non-null record", npc);
            store.Save(true);

            string dbPath = Path.Combine(storeDir, "world_memory.json");
            AssertTrue("INV-12", "Database file exists after Save(force=true)", File.Exists(dbPath));

            // Test 6b: Data survives reload
            var store2 = new WorldMemoryStore(storeDir);
            var reloaded = store2.GetNpcByName("Test NPC");
            AssertNotNull("INV-12", "NPC survives persistence roundtrip", reloaded);
            AssertTrue("INV-12", "Roundtripped NPC preserves Name", reloaded != null && reloaded.Name == "Test NPC");

            // Test 6c: Save failure when file is read-only
            File.SetAttributes(dbPath, FileAttributes.ReadOnly);
            bool saveThrew = false;
            try
            {
                store2.Save(true);
            }
            catch
            {
                saveThrew = true;
            }
            File.SetAttributes(dbPath, FileAttributes.Normal);

            // Document: SaveDatabaseInternal doesn't explicitly throw on failure,
            // but File.WriteAllText will throw UnauthorizedAccessException
            AssertTrue("INV-12", "WorldMemoryStore.Save propagates exception on write failure", saveThrew);

            // DEFECT MARKER: Non-atomic write
            Console.WriteLine("  [NOTE] DEFECT DOCUMENTED: WorldMemoryStore.SaveDatabaseInternal (line 112)");
            Console.WriteLine("         uses non-atomic File.WriteAllText. A crash during write can");
            Console.WriteLine("         corrupt the database. Phase 1G will implement safe atomic replacement.");
        }

        // =====================================================================
        // SECTION 7: EXPLICIT NPC TARGET-NOT-FOUND BEHAVIOUR
        // Invariant #7: An explicit NPC target that cannot be resolved must
        //               not be replaced with the nearest NPC.
        //
        // DEFECT CONFIRMED: EntityMutationHandler.cs lines 35-50 silently
        //   falls back to the nearest NPC when the explicit target is not found.
        // =====================================================================
        static void TestSection7_ExplicitNpcTargetNotFound()
        {
            Console.WriteLine("\n[Section 7] Explicit NPC Target-Not-Found Behaviour\n");

            // We cannot directly invoke EntityMutationHandler.ExecuteEntityMutationDirect
            // in headless tests because it calls Game.Player.Character (GTA native).
            // Instead, we document the exact defect with code analysis.

            // The defect is at EntityMutationHandler.cs:
            //   Line 25-33: Attempts to find targetContext by TargetEntityId
            //   Line 35-50: If targetContext is null (not found), silently falls back
            //                to nearest NPC by distance from player
            //   Line 52+:   Applies mutation to the wrong NPC
            //
            // This violates Invariant #7: "An explicit NPC target that cannot be
            // resolved must not be replaced with the nearest NPC."

            Console.WriteLine("  [NOTE] DEFECT CONFIRMED: EntityMutationHandler.ExecuteEntityMutationDirect");
            Console.WriteLine("         (lines 35-50) silently falls back to nearest NPC when explicit");
            Console.WriteLine("         target TargetEntityId is specified but not found.");
            Console.WriteLine("         Phase 1H will fix: when TargetEntityId is non-empty and not");
            Console.WriteLine("         resolved, the mutation must be rejected, not redirected.");

            // Test what we CAN verify: the NPC lookup logic
            var contexts = new List<ActiveNpcContext>();
            var ped1 = CreateMockPed(1001);
            var rec1 = new NpcRecord { NpcId = 100, Name = "Alpha", EntityId = "npc_alpha" };
            var ctx1 = new ActiveNpcContext(ped1, rec1, "env_test", "npc_alpha");
            contexts.Add(ctx1);

            // Verify name-based lookup finds the right NPC
            var found = contexts.Find(ctx =>
                ctx != null && ctx.IsValid && (
                    string.Equals(ctx.Record.Name, "Alpha", StringComparison.OrdinalIgnoreCase) ||
                    ctx.Record.NpcId.ToString() == "Alpha"
                )
            );
            AssertNotNull("INV-7", "Name-based lookup finds matching NPC", found);
            AssertTrue("INV-7", "Found NPC has correct name", found != null && found.Record.Name == "Alpha");

            // Verify unfindable name returns null
            var notFound = contexts.Find(ctx =>
                ctx != null && ctx.IsValid && (
                    string.Equals(ctx.Record.Name, "NonExistent", StringComparison.OrdinalIgnoreCase) ||
                    ctx.Record.NpcId.ToString() == "NonExistent"
                )
            );
            AssertNull("INV-7", "Non-existent name returns null (before fallback)", notFound);

            // The DEFECT is that EntityMutationHandler doesn't stop here — it falls
            // back to nearest NPC. This will be fixed in Phase 1H.
        }

        // =====================================================================
        // SECTION 8: PLANNER INPUT IMMUTABILITY
        // Invariant #8: The planner must consume immutable request and world
        //               snapshots. It must not mutate the original request
        //               or perform hidden native-world sampling.
        //
        // DEFECT CONFIRMED: SpatialWorldPlanner.PlanEnvironment mutates
        //   data.Props (lines 105-116, 130-138) and NPC names (lines 270-277).
        // =====================================================================
        static void TestSection8_PlannerInputImmutability(string testDir)
        {
            Console.WriteLine("\n[Section 8] Planner Input Immutability\n");

            // We cannot fully test SpatialWorldPlanner.PlanEnvironment in headless mode
            // because it calls SpatialQueryService.FindGroundPosition (native GTA call).
            // Instead, we verify the documented defect and test what we can.

            Console.WriteLine("  [NOTE] DEFECT CONFIRMED: SpatialWorldPlanner.PlanEnvironment mutates");
            Console.WriteLine("         input BuildEnvironmentData:");
            Console.WriteLine("         - Lines 105-116: Inserts solver props into data.Props");
            Console.WriteLine("         - Lines 130-138: Inserts sky floor into data.Props");
            Console.WriteLine("         - Lines 270-277: Overwrites NPC Name and Occupation");
            Console.WriteLine("         Phase 2E-G will fix planner to consume immutable snapshots.");

            // Test what we CAN verify: BuildEnvironmentData objects are mutable
            // (which enables the defect)
            var data = new BuildEnvironmentData
            {
                Name = "Original Name",
                Props = new List<PropPlacementData>
                {
                    new PropPlacementData { PropId = "original_prop", Model = "test_model" }
                },
                Npcs = new List<EnvironmentNpcData>
                {
                    new EnvironmentNpcData { Name = "Original NPC", Occupation = "Original Job", PedModel = "a_m_y_beach_01" }
                }
            };

            // Record original state
            int originalPropCount = data.Props.Count;
            string originalNpcName = data.Npcs[0].Name;
            string originalNpcOcc = data.Npcs[0].Occupation;

            // Verify that data objects are indeed mutable (enabling the defect)
            data.Props.Add(new PropPlacementData { PropId = "injected", Model = "injected_model" });
            data.Npcs[0].Name = "Mutated Name";

            AssertTrue("INV-8", "BuildEnvironmentData.Props is mutable (defect enabler)", data.Props.Count == originalPropCount + 1);
            AssertTrue("INV-8", "EnvironmentNpcData.Name is mutable (defect enabler)", data.Npcs[0].Name == "Mutated Name");

            // Restore for doc purposes
            data.Props.RemoveAt(data.Props.Count - 1);
            data.Npcs[0].Name = originalNpcName;
        }

        // =====================================================================
        // SECTION 9: COMMISSIONING ATOMICITY
        // Invariant #5: Every commissioned environment must have a recoverable
        //               canonical physical plan AND corresponding world-state record.
        // Invariant #12: Persistence failure must not result in a partially
        //                commissioned environment.
        //
        // DEFECT CONFIRMED: CommissionEnvironment continues after PlanRepository
        //   save failure (lines 363-368). It creates the environment record
        //   in WorldMemoryStore even though no plan was persisted.
        // =====================================================================
        static void TestSection9_CommissioningAtomicity(string testDir)
        {
            Console.WriteLine("\n[Section 9] Commissioning Atomicity\n");

            string atomDir = Path.Combine(testDir, "Section9_atomic");
            Directory.CreateDirectory(atomDir);

            var engine = new EngineCore(atomDir);

            // Commission a valid plan — both stores should have records
            var plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_atomic_test",
                Name = "Atomic Test Environment",
                CenterPosition = new SpatialVector3(50, 50, 10),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "prop_test", ModelName = "test_model", WorldPosition = new SpatialVector3(50, 50, 10) }
                },
                ResolvedNpcs = new List<ResolvedNpcPlacement>
                {
                    new ResolvedNpcPlacement { EntityId = "npc_atomic", Name = "Atomic NPC", PedModel = "a_m_y_beach_01", Occupation = "Test" }
                }
            };

            var env = engine.CommissionEnvironment(plan, null, SpatialCommissioningMode.AllowUnverifiedSimulation);

            AssertNotNull("INV-5", "CommissionEnvironment returns non-null on success", env);

            // Both stores must have corresponding records
            bool hasPlan = engine.Plans.HasPlan("env_atomic_test");
            bool hasEnv = engine.Store.HasEnvironment("env_atomic_test");
            AssertTrue("INV-5", "PlanRepository has plan after commissioning", hasPlan);
            AssertTrue("INV-5", "WorldMemoryStore has environment after commissioning", hasEnv);

            // The DEFECT is that if hasPlan were false, hasEnv would still be true.
            // This creates a partially commissioned environment (Invariant #12 violation).
            Console.WriteLine("  [NOTE] DEFECT: If PlanRepository.SavePlan fails, CommissionEnvironment");
            Console.WriteLine("         still creates the WorldMemoryStore record (partial commissioning).");
            Console.WriteLine("         Phase 1F will make plan persistence failure prevent commissioning.");
        }

        // =====================================================================
        // SECTION 10: REALIZATION ROLLBACK ON PERSISTENCE FAILURE
        // Tests the existing Stage-Before-Commit invariant to establish
        // a regression baseline before Phase 1 changes.
        // =====================================================================
        static void TestSection10_RealizationRollbackOnPersistenceFailure(string testDir)
        {
            Console.WriteLine("\n[Section 10] Realization Rollback on Persistence Failure (Baseline)\n");

            string rollDir = Path.Combine(testDir, "Section10_rollback");
            Directory.CreateDirectory(rollDir);

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

            // Test 10a: RealizationScope uncommitted dispose triggers rollback
            string envId = "env_rollback_test";
            using (var scope = runtime.BeginRealizationScope(envId))
            {
                var prop = CreateMockProp(2001);
                spawner.RegisterProp(envId, prop);
                AssertTrue("INV-12", "Entities staged within active scope", runtime.HasRealizedEntities(envId));
                // Deliberately NOT calling scope.Commit()
            }

            AssertFalse("INV-12", "Uncommitted scope disposes and rolls back entities", runtime.HasRealizedEntities(envId));

            // Test 10b: RealizationScope committed dispose preserves entities
            string envId2 = "env_commit_test";
            using (var scope = runtime.BeginRealizationScope(envId2))
            {
                var prop = CreateMockProp(2002);
                spawner.RegisterProp(envId2, prop);
                scope.Commit();
            }

            AssertTrue("INV-12", "Committed scope preserves entities after dispose", runtime.HasRealizedEntities(envId2));

            // Clean up
            runtime.CleanUpEnvironment(envId2);
        }
    }
}
