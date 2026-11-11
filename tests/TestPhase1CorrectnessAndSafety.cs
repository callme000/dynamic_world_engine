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
    class Phase1CorrectnessAndSafetyTester
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

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE — PHASE 1 CORRECTNESS AND RUNTIME SAFETY TESTS");
            Console.WriteLine("==========================================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Phase1_Data");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            try
            {
                TestA_GameThreadGateAuthority();
                TestB_SpatialPlanValidatorCommitBarrier(testDir);
                TestC_PlanPersistenceFailurePreventsCommissioning(testDir);
                TestD_SafeAtomicFileReplacementAndRecovery(testDir);
                TestE_NpcTargetResolutionWithoutFallback();
                TestF_StoredNpcObjectImmutability(testDir);
                TestG_TeleportSyntheticFallbackReporting();
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n  [FATAL] Unhandled exception in Phase 1 test suite: " + ex.ToString());
                failed++;
            }

            Console.WriteLine("\n==========================================================================");
            Console.WriteLine(string.Format("  PHASE 1 TEST SUMMARY: {0} PASSED, {1} FAILED, {2} TOTAL", passed, failed, total));
            Console.WriteLine("==========================================================================");

            return (failed == 0) ? 0 : 1;
        }

        // =====================================================================
        // PART A: GameThreadGate Authority & Zero Native Off-Thread Execution
        // Invariants #1 & #2
        // =====================================================================
        static void TestA_GameThreadGateAuthority()
        {
            Console.WriteLine("[Part A] GameThreadGate Authority & Zero Native Off-Thread Execution\n");

            GameThreadGate.Reset();
            AssertFalse("INV-2", "Uninitialized GameThreadGate denies IsMainThread", GameThreadGate.IsMainThread);
            AssertFalse("INV-2", "Uninitialized GameThreadGate reports IsInitialized == false", GameThreadGate.IsInitialized);
            AssertFalse("INV-2", "Uninitialized GameThreadGate denies EnforceMainThread", GameThreadGate.EnforceMainThread("test"));

            AssertThrows<InvalidOperationException>("INV-2", "RequireMainThread throws when uninitialized", () =>
            {
                GameThreadGate.RequireMainThread("test_uninit");
            });

            // Initialize to current thread
            GameThreadGate.Initialize(Thread.CurrentThread.ManagedThreadId);
            AssertTrue("INV-1", "Initialized GameThreadGate reports IsInitialized == true", GameThreadGate.IsInitialized);
            AssertTrue("INV-1", "Current thread matches MainThreadId", GameThreadGate.IsMainThread);
            AssertTrue("INV-1", "EnforceMainThread grants permission on main thread", GameThreadGate.EnforceMainThread("test_init"));

            // Execute on main thread
            int execVal = GameThreadGate.Execute("calc", () => 42, 0);
            AssertTrue("INV-1", "GameThreadGate.Execute succeeds on main thread", execVal == 42);

            // Execute on worker thread must be denied
            int workerVal = -1;
            bool workerThrew = false;
            ManualResetEvent done = new ManualResetEvent(false);

            ThreadPool.QueueUserWorkItem(delegate
            {
                workerVal = GameThreadGate.Execute("worker_calc", () => 999, -1);
                try
                {
                    GameThreadGate.RequireMainThread("worker_require");
                }
                catch (InvalidOperationException)
                {
                    workerThrew = true;
                }
                done.Set();
            });

            done.WaitOne(5000);
            AssertTrue("INV-1", "GameThreadGate.Execute denies worker thread and returns fallback", workerVal == -1);
            AssertTrue("INV-1", "GameThreadGate.RequireMainThread throws on worker thread", workerThrew);

            // Check PlayerTeleportCoordinator and SpatialPerceptionScanner forward to GameThreadGate
            AssertTrue("INV-1", "PlayerTeleportCoordinator forwards to GameThreadGate.IsMainThread", PlayerTeleportCoordinator.IsMainThread == GameThreadGate.IsMainThread);
            AssertTrue("INV-1", "SpatialPerceptionScanner forwards to GameThreadGate.IsMainThread", SpatialPerceptionScanner.IsMainThread == GameThreadGate.IsMainThread);
            AssertTrue("INV-1", "SpatialGridSampler forwards to GameThreadGate.IsMainThread", SpatialGridSampler.IsMainThread == GameThreadGate.IsMainThread);

            GameThreadGate.Reset();
        }

        // =====================================================================
        // PART B: SpatialPlanValidator Enforced Commit Barrier
        // Invariants #3 & #4
        // =====================================================================
        static void TestB_SpatialPlanValidatorCommitBarrier(string testDir)
        {
            Console.WriteLine("\n[Part B] SpatialPlanValidator Enforced Commit Barrier\n");

            string barrierDir = Path.Combine(testDir, "PartB_barrier");
            Directory.CreateDirectory(barrierDir);
            var engine = new EngineCore(barrierDir);

            // Create an invalid plan with hard collisions that cannot be auto-corrected or auto-correction disabled
            var invalidPlan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_invalid_barrier",
                Name = "Invalid Colliding Plan",
                CenterPosition = new SpatialVector3(0, 0, 0),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "p1", ModelName = "test_a", WorldPosition = new SpatialVector3(0, 0, 0) },
                    new ResolvedPropPlacement { PropId = "p2", ModelName = "test_b", WorldPosition = new SpatialVector3(0.01f, 0, 0) }
                }
            };

            // Pre-validate with applyAutoCorrections: false so it has hard violations
            var valResult = engine.PlanValidator.ValidatePlan(invalidPlan, null, null, applyAutoCorrections: false);
            invalidPlan.ValidationResult = valResult;
            AssertFalse("INV-3", "Invalid plan fails validation", invalidPlan.ValidationResult.IsValid);

            // Attempting to commission this invalid plan MUST throw InvalidOperationException
            AssertThrows<InvalidOperationException>("INV-4", "CommissionEnvironment rejects plan failing validation barrier", () =>
            {
                engine.CommissionEnvironment(invalidPlan, null, SpatialCommissioningMode.AllowUnverifiedSimulation);
            });

            // Ensure plan was NOT persisted
            AssertFalse("INV-4", "Rejected plan is not present in PlanRepository", engine.Plans.HasPlan("env_invalid_barrier"));
            AssertFalse("INV-4", "Rejected plan is not present in WorldMemoryStore", engine.Store.HasEnvironment("env_invalid_barrier"));
        }

        // =====================================================================
        // PART C: Plan Persistence Failure Prevents World Commissioning
        // Invariants #5 & #12
        // =====================================================================
        static void TestC_PlanPersistenceFailurePreventsCommissioning(string testDir)
        {
            Console.WriteLine("\n[Part C] Plan Persistence Failure Prevents World Commissioning\n");

            string commDir = Path.Combine(testDir, "PartC_comm");
            Directory.CreateDirectory(commDir);
            var engine = new EngineCore(commDir);

            // Lock target plan file before commissioning to force PlanRepository.SavePlan to fail
            string targetPlanPath = engine.Plans.GetPlanFilePath("env_force_plan_fail");
            File.WriteAllText(targetPlanPath, "blocked");
            File.SetAttributes(targetPlanPath, FileAttributes.ReadOnly);

            var plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_force_plan_fail",
                Name = "Forced Plan Save Failure",
                CenterPosition = new SpatialVector3(10, 20, 30),
                ResolvedProps = new List<ResolvedPropPlacement>
                {
                    new ResolvedPropPlacement { PropId = "p1", ModelName = "model1", WorldPosition = new SpatialVector3(10, 20, 30) }
                }
            };

            // Commissioning MUST fail and throw InvalidOperationException because plan could not be persisted
            AssertThrows<InvalidOperationException>("INV-12", "CommissionEnvironment aborts when PlanRepository fails", () =>
            {
                engine.CommissionEnvironment(plan, null, SpatialCommissioningMode.AllowUnverifiedSimulation);
            });

            // Clean up attributes
            try { File.SetAttributes(targetPlanPath, FileAttributes.Normal); File.Delete(targetPlanPath); } catch { }

            // Ensure no partial commissioning in WorldMemoryStore
            AssertFalse("INV-12", "Partial environment NOT enrolled in WorldMemoryStore on plan save failure",
                engine.Store.HasEnvironment("env_force_plan_fail"));
        }

        // =====================================================================
        // PART D: Safe Atomic File Replacement and Recovery
        // Invariant #12
        // =====================================================================
        static void TestD_SafeAtomicFileReplacementAndRecovery(string testDir)
        {
            Console.WriteLine("\n[Part D] Safe Atomic File Replacement and Recovery\n");

            string atomicDir = Path.Combine(testDir, "PartD_atomic");
            Directory.CreateDirectory(atomicDir);

            // Test WorldMemoryStore atomic save and backup creation
            var store = new WorldMemoryStore(atomicDir);
            store.CreateNpc("Agent Smith", "Enforcer", "Matrix", "s_m_m_highsec_01");
            store.Save(true);

            string dbPath = Path.Combine(atomicDir, "world_memory.json");
            string bakPath = dbPath + ".bak";

            AssertTrue("INV-12", "Database file exists after save", File.Exists(dbPath));

            // A second save should create/update the .bak file
            store.CreateNpc("Neo", "The One", "Zion", "mp_m_freemode_01");
            store.Save(true);
            AssertTrue("INV-12", "Backup file (.bak) exists after subsequent save", File.Exists(bakPath));

            // Simulate database file corruption by writing invalid bytes
            File.WriteAllText(dbPath, "{ CORRUPTED INVALID JSON DATA ??? ");

            // Loading from store should recover from backup
            var storeRecovered = new WorldMemoryStore(atomicDir);
            var recoveredNpc = storeRecovered.GetNpcByName("Agent Smith");
            AssertNotNull("INV-12", "WorldMemoryStore successfully recovered state from .bak backup", recoveredNpc);

            // Test PlanRepository atomic save and backup
            string plansDir = Path.Combine(atomicDir, "plans");
            var planRepo = new PlanRepository(plansDir);
            var plan = new SpatialExecutionPlan
            {
                EnvironmentId = "env_atomic_plan",
                Name = "Atomic Plan Test",
                CenterPosition = new SpatialVector3(10, 10, 10)
            };
            planRepo.SavePlan(plan);

            // Second save generates .bak
            plan.Name = "Atomic Plan Test v2";
            planRepo.SavePlan(plan);

            string planPath = planRepo.GetPlanFilePath("env_atomic_plan");
            string planBakPath = planPath + ".bak";
            AssertTrue("INV-12", "Plan backup file (.bak) exists", File.Exists(planBakPath));

            // Corrupt primary plan file
            File.WriteAllText(planPath, "CORRUPTED");

            // PlanRepository TryGetPlan should recover from .bak
            SpatialExecutionPlan recoveredPlan;
            bool getOk = planRepo.TryGetPlan("env_atomic_plan", out recoveredPlan);
            AssertTrue("INV-12", "PlanRepository recovered plan from .bak backup", getOk && recoveredPlan != null);
        }

        // =====================================================================
        // PART E: NPC Target Resolution Without Nearest-Fallback
        // Invariant #7: An explicit NPC target that cannot be resolved must
        //               not be replaced with the nearest NPC.
        // =====================================================================
        static void TestE_NpcTargetResolutionWithoutFallback()
        {
            Console.WriteLine("\n[Part E] NPC Target Resolution Without Nearest-Fallback\n");

            var contexts = new List<ActiveNpcContext>();
            var ped1 = CreateMockPed(3001);
            var rec1 = new NpcRecord { NpcId = 201, Name = "Alice", CurrentState = "CALM" };
            var ctx1 = new ActiveNpcContext(ped1, rec1, "env_test", "npc_alice");
            contexts.Add(ctx1);

            // Target a non-existent NPC explicitly
            var mutData = new MutateEntityData
            {
                TargetEntityId = "NonExistentBob",
                Hostility = "hostile_attack"
            };

            // Execute mutation — MUST NOT fall back to Alice!
            EntityMutationHandler.ExecuteEntityMutationDirect(mutData, contexts);

            AssertTrue("INV-7", "Explicit unresolvable target does NOT mutate nearest NPC (Alice state remains CALM)",
                rec1.CurrentState == "CALM");

            // Now target Alice explicitly
            var mutAlice = new MutateEntityData
            {
                TargetEntityId = "Alice",
                Hostility = "hostile_attack"
            };
            EntityMutationHandler.ExecuteEntityMutationDirect(mutAlice, contexts);

            AssertTrue("INV-7", "Explicit resolvable target mutates correctly (Alice state transitioned to HOSTILE)",
                rec1.CurrentState == "HOSTILE");
        }

        // =====================================================================
        // PART F: Stored NPC Object Immutability
        // Invariants #9 & #11: External callers cannot mutate stored NPC records directly
        // =====================================================================
        static void TestF_StoredNpcObjectImmutability(string testDir)
        {
            Console.WriteLine("\n[Part F] Stored NPC Object Immutability\n");

            string storeDir = Path.Combine(testDir, "PartF_immutability");
            Directory.CreateDirectory(storeDir);
            var store = new WorldMemoryStore(storeDir);

            var original = store.CreateNpc("Senator Davis", "Politician", "Government", "s_m_m_highsec_01", "Corrupt Official");
            int id = original.NpcId;

            // Retrieve NPC via GetNpcById
            var retrieved = store.GetNpcById(id);
            AssertNotNull("INV-9", "GetNpcById returns record", retrieved);
            AssertTrue("INV-9", "Initial Trust is 0", retrieved.Trust == 0);

            // Attempt to maliciously or accidentally mutate the retrieved object directly
            retrieved.Trust = 999;
            retrieved.CurrentState = "COMPROMISED";
            retrieved.Secrets.Add("LEAKED_SECRET");

            // Retrieve again from store — stored record must be 100% UNTOUCHED!
            var fresh = store.GetNpcById(id);
            AssertTrue("INV-9", "Stored NPC Trust is unmutated by external caller", fresh.Trust == 0);
            AssertTrue("INV-9", "Stored NPC CurrentState is unmutated by external caller", fresh.CurrentState == "CALM");
            AssertFalse("INV-11", "Stored NPC Secrets list is unpolluted by external caller", fresh.Secrets.Contains("LEAKED_SECRET"));

            // Check GetAllNpcs defensive copying as well
            var all = store.GetAllNpcs();
            AssertTrue("INV-9", "GetAllNpcs returns non-empty list", all.Count > 0);
            all[0].Trust = -888;

            var checkAfterAll = store.GetNpcById(id);
            AssertTrue("INV-9", "Stored NPC unaffected by GetAllNpcs element mutation", checkAfterAll.Trust == 0);
        }

        // =====================================================================
        // PART G: Real vs Synthetic Spatial Snapshot & Fallback Reporting
        // Invariant #6: A physical teleport, collision load or runtime operation
        //               must never be reported as successful when only a synthetic
        //               fallback occurred.
        // =====================================================================
        static void TestG_TeleportSyntheticFallbackReporting()
        {
            Console.WriteLine("\n[Part G] Real vs Synthetic Spatial Snapshot & Fallback Reporting\n");

            var coordinator = new PlayerTeleportCoordinator();

            // Headless / uninitialized gate run
            GameThreadGate.Reset();
            var result = coordinator.AnchorPhysicalContext(new Vector3(100, 200, 30), 90f);

            AssertTrue("INV-6", "Synthetic fallback reports IsNativeGtaRuntime == false", !result.IsNativeGtaRuntime);
            AssertTrue("INV-6", "Synthetic fallback reports CollisionLoaded == false (never claims loaded collision)", !result.CollisionLoaded);
            AssertTrue("INV-6", "Synthetic fallback reports IsSyntheticFallback == true", result.IsSyntheticFallback);
        }
    }
}
