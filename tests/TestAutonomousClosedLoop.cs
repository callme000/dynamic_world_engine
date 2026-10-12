using System;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Tests
{
    class ClosedLoopTester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  AUTONOMOUS CLOSED-LOOP SIMULATION TEST SUITE           ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Loop_Data");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            EngineCore engine = new EngineCore(testDir);
            int passed = 0;
            int total = 0;

            // --- TEST 1: CREATE NPC & INITIAL STATE ---
            total++;
            Console.WriteLine("[Test 1] Creating NPC (Marco Bellini, Cartel Owner)...");
            NpcRecord marco = engine.Store.CreateNpc("Marco Bellini", "Nightclub Owner", "Cartel", "a_m_y_business_01", "Money laundering boss");
            if (marco != null && marco.CurrentState == "CALM" && marco.Fear == 0 && marco.Trust == 0)
            {
                Console.WriteLine("  ✅ PASS: Marco created in default state (State: CALM, Fear: 0%, Trust: 0)");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: NPC creation state failed.");
            }

            // --- TEST 2: THREAT / GUN AIM EVENT ---
            total++;
            Console.WriteLine("\n[Test 2] Simulating Observer detecting Player aiming gun at Marco (GUN_AIM, severity=0.85)...");
            NpcRecord afterAim = engine.Simulation.ProcessInteraction(
                marco.NpcId,
                "player",
                "GUN_AIM",
                0.85f,
                "Player pointed a lethal weapon directly at Marco Bellini",
                "Threat_Physical"
            );

            bool aimOk = afterAim.Fear >= 50 && afterAim.CurrentState == "SUBMISSIVE" && afterAim.History.Count == 1;
            if (aimOk)
            {
                Console.WriteLine("  ✅ PASS: State Machine transitioned Marco -> State: {0}, Fear: {1}%, Trust: {2}", 
                    afterAim.CurrentState, afterAim.Fear, afterAim.Trust);
                Console.WriteLine("     Recorded Event: \"{0}\"", afterAim.History[0].Description);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Gun aim state transition failed. State={0}, Fear={1}", afterAim.CurrentState, afterAim.Fear);
            }

            // --- TEST 3: BRIBE EVENT TRANSITION ---
            total++;
            Console.WriteLine("\n[Test 3] Simulating Player offering bribe (BRIBE, severity=0.8)...");
            NpcRecord afterBribe = engine.Simulation.ProcessInteraction(
                marco.NpcId,
                "player",
                "BRIBE",
                0.80f,
                "Player offered Marco a $20,000 cash cut to partner up",
                "Alliance_Proposal"
            );

            bool bribeOk = afterBribe.Trust > 0 && afterBribe.CurrentState == "ALLIED" && afterBribe.History.Count == 2;
            if (bribeOk)
            {
                Console.WriteLine("  ✅ PASS: State Machine transitioned Marco -> State: {0}, Trust: {1}, Fear: {2}%", 
                    afterBribe.CurrentState, afterBribe.Trust, afterBribe.Fear);
                Console.WriteLine("     Derived Player Relationship: {0}", afterBribe.Relationships["Player"]);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Bribe state transition failed.");
            }

            // --- TEST 4: EVENT GRAPH CONTEXT SLICING ---
            total++;
            Console.WriteLine("\n[Test 4] Testing Event Graph history slicing...");
            var historySlice = engine.Store.GetRelevantEventHistorySlice(marco.NpcId, 2);
            if (historySlice.Count == 2 && historySlice[0].EventType == "BRIBE" && historySlice[1].EventType == "GUN_AIM")
            {
                Console.WriteLine("  ✅ PASS: Sliced recent history chronologically:");
                foreach (var h in historySlice)
                {
                    Console.WriteLine("     • [{0}] Severity {1:F2}: {2}", h.EventType, h.Severity, h.Description);
                }
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: History slicing failed.");
            }

            // --- TEST 5: LIVINGLSAIS FACT SYNC WITH STATE & EMOTIONS ---
            total++;
            Console.WriteLine("\n[Test 5] Verifying LivingLSAIs Fact Hot-Sync contains emotional metrics...");
            string fact = engine.GetNpcFactForLivingLSAIs(marco.NpcId);
            bool factOk = (fact.Contains("State=ALLIED") || fact.Contains("ALLIED")) && fact.Contains("Fear:") && fact.Contains("Trust:");
            if (factOk)
            {
                Console.WriteLine("  ✅ PASS: LivingLSAIs fact formatted perfectly with real-time state:\n     {0}", fact);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: LivingLSAIs fact missing emotional metrics.");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("=========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
