using System;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Archetypes;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  MILESTONE 2: UNIVERSAL WORLD SPAWNER TEST SUITE        ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_M2_Data");
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
            Directory.CreateDirectory(testDir);

            int totalTests = 0;
            int passedTests = 0;

            // --- TEST 1: SPATIAL COORDINATE TRANSFORM ---
            totalTests++;
            Console.WriteLine("[Test 1] Testing PropPlacementEngine.LocalToWorld()...");
            CalculatedTransform tf1 = PropPlacementEngine.LocalToWorld(100f, 200f, 10f, 0f, 1f, 2f, 0.5f, 90f);
            // With heading 0 (facing North/Y): local X(+1) = East/X(+1), local Y(+2) = North/Y(+2)
            bool t1Ok = Math.Abs(tf1.X - 101f) < 0.01f && Math.Abs(tf1.Y - 202f) < 0.01f && Math.Abs(tf1.Z - 10.5f) < 0.01f && Math.Abs(tf1.Heading - 90f) < 0.01f;
            if (t1Ok)
            {
                Console.WriteLine("  ✅ PASS: LocalToWorld calculated exact world coords (X=101, Y=202, Z=10.5, H=90)");
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Coordinates mismatched. Got X={0}, Y={1}, Z={2}, H={3}", tf1.X, tf1.Y, tf1.Z, tf1.Heading);
            }

            // --- TEST 2: GAMBLING DEN ARCHETYPE ---
            totalTests++;
            Console.WriteLine("\n[Test 2] Testing Gambling Den Archetype JSON...");
            EngineCore engine = new EngineCore(testDir);
            string gamblingJson = EnvironmentCatalog.GetGamblingDenJson();
            EnvironmentRecord gEnv = engine.ExecuteBuildEnvironmentCommand(gamblingJson, 50f, 50f, 15f, 180f);
            var gPlan = engine.Plans.GetPlan(gEnv.PlanId);

            bool gOk = gEnv != null && gPlan != null && gPlan.ResolvedProps.Count == 6 && gEnv.NpcIds.Count >= 2;
            if (gOk)
            {
                Console.WriteLine("  ✅ PASS: Spawned Gambling Den with 6 Props (in PlanRepository) and {0} NPCs (Dealer Marcus + Guard Bruno + Crowd)", gEnv.NpcIds.Count);
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Gambling Den props/NPCs count mismatch. (props={0}, npcs={1})",
                    gPlan != null ? gPlan.ResolvedProps.Count : -1, gEnv != null ? gEnv.NpcIds.Count : -1);
            }

            // --- TEST 3: STREET VENDOR ARCHETYPE ---
            totalTests++;
            Console.WriteLine("\n[Test 3] Testing Street Vendor Archetype JSON...");
            string vendorJson = EnvironmentCatalog.GetStreetVendorJson();
            EnvironmentRecord vEnv = engine.ExecuteBuildEnvironmentCommand(vendorJson, 60f, 60f, 15f, 90f);
            var vPlan = engine.Plans.GetPlan(vEnv.PlanId);
            bool vOk = vEnv != null && vPlan != null && vPlan.ResolvedProps.Count == 3 && vEnv.NpcIds.Count == 1;
            if (vOk)
            {
                Console.WriteLine("  ✅ PASS: Spawned Street Vendor with Hotdog Stand, Cooler, Trash Bin, and Vendor Sal");
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Street Vendor count mismatch.");
            }

            // --- TEST 4: CHOP SHOP ARCHETYPE ---
            totalTests++;
            Console.WriteLine("\n[Test 4] Testing Chop Shop Archetype JSON...");
            string chopJson = EnvironmentCatalog.GetChopShopJson();
            EnvironmentRecord cEnv = engine.ExecuteBuildEnvironmentCommand(chopJson, 70f, 70f, 15f, 0f);
            var cPlan = engine.Plans.GetPlan(cEnv.PlanId);
            bool cOk = cEnv != null && cPlan != null && cPlan.ResolvedProps.Count == 4 && cEnv.NpcIds.Count == 1;
            if (cOk)
            {
                Console.WriteLine("  ✅ PASS: Spawned Chop Shop with Toolchest, Engine, Barrel, Wheel, and Mechanic Tork");
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Chop Shop count mismatch.");
            }

            // --- TEST 5: POLICE ROADBLOCK ARCHETYPE ---
            totalTests++;
            Console.WriteLine("\n[Test 5] Testing Police Roadblock Archetype JSON...");
            string copJson = EnvironmentCatalog.GetPoliceRoadblockJson();
            EnvironmentRecord copEnv = engine.ExecuteBuildEnvironmentCommand(copJson, 80f, 80f, 15f, 270f);
            var copPlan = engine.Plans.GetPlan(copEnv.PlanId);
            bool copOk = copEnv != null && copPlan != null && copPlan.ResolvedProps.Count == 4 && copEnv.NpcIds.Count >= 2;
            if (copOk)
            {
                Console.WriteLine("  ✅ PASS: Spawned Police Checkpoint with 4 Barricades/Cones and {0} Officers/Perimeter Units", copEnv.NpcIds.Count);
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Police Roadblock count mismatch. (props={0}, npcs={1})",
                    copPlan != null ? copPlan.ResolvedProps.Count : -1, copEnv != null ? copEnv.NpcIds.Count : -1);
            }

            // --- TEST 6: BBQ PARTY ARCHETYPE ---
            totalTests++;
            Console.WriteLine("\n[Test 6] Testing BBQ Party Archetype JSON...");
            string bbqJson = EnvironmentCatalog.GetBbqPartyJson();
            EnvironmentRecord bbqEnv = engine.ExecuteBuildEnvironmentCommand(bbqJson, 90f, 90f, 15f, 45f);
            var bbqPlan = engine.Plans.GetPlan(bbqEnv.PlanId);
            bool bbqOk = bbqEnv != null && bbqPlan != null && bbqPlan.ResolvedProps.Count == 4 && bbqEnv.NpcIds.Count >= 1;
            if (bbqOk)
            {
                Console.WriteLine("  ✅ PASS: Spawned BBQ Party with Grill, Cooler, Boombox, Chair, Tyrone Green and {0} Guests", bbqEnv.NpcIds.Count - 1);
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: BBQ Party count mismatch. (props={0}, npcs={1})",
                    bbqPlan != null ? bbqPlan.ResolvedProps.Count : -1, bbqEnv != null ? bbqEnv.NpcIds.Count : -1);
            }

            // --- TEST 7: LIVINGLSAIS FACT FORMATTING ---
            totalTests++;
            Console.WriteLine("\n[Test 7] Testing LivingLSAIs Fact Injection across multi-NPC environments...");
            string dealerFact = engine.GetNpcFactForLivingLSAIs(gEnv.NpcIds[0]);
            string guardFact = engine.GetNpcFactForLivingLSAIs(gEnv.NpcIds[1]);
            bool factsOk = dealerFact.Contains("Marcus") && dealerFact.Contains("Blackjack & Poker Dealer") &&
                            guardFact.Contains("Bruno") && guardFact.Contains("Enforcer");
            if (factsOk)
            {
                Console.WriteLine("  ✅ PASS: LivingLSAIs context correctly generated:");
                Console.WriteLine("     • {0}", dealerFact);
                Console.WriteLine("     • {0}", guardFact);
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Fact string content incorrect.");
            }

            // --- TEST 8: ENVIRONMENT DELETION / CLEANUP ---
            totalTests++;
            Console.WriteLine("\n[Test 8] Testing Environment Cleanup / CLEAR_ENVIRONMENT...");
            bool clearOk = engine.ClearEnvironment(gEnv.EnvironmentId);
            bool gNotFound = engine.Store.GetEnvironmentById(gEnv.EnvironmentId) == null;
            bool gPropsDeleted = engine.Store.GetPropsByEnvironment(gEnv.EnvironmentId).Count == 0;
            if (clearOk && gNotFound && gPropsDeleted)
            {
                Console.WriteLine("  ✅ PASS: Successfully deleted Gambling Den and all associated props from world memory");
                passedTests++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Environment cleanup failed.");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  TEST SUMMARY: {0}/{1} TESTS PASSED", passedTests, totalTests);
            Console.WriteLine("=========================================================");

            return (passedTests == totalTests) ? 0 : 1;
        }
    }
}
