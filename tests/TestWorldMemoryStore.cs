using System;
using System.IO;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Tests
{
    public class TestWorldMemoryStore
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE - STEP 1 UNIT TEST RUNNER  ");
            Console.WriteLine("=================================================");

            string testDbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_output");
            if (Directory.Exists(testDbDir))
            {
                Directory.Delete(testDbDir, true);
            }

            var store = new WorldMemoryStore(testDbDir);

            // Test 1: Create Marcus (NPC #1842 concept)
            Console.WriteLine("\n[Test 1] Creating Marcus (Mechanic)...");
            var marcus = store.CreateNpc("Marcus", "Mechanic", "Civilian", "s_m_y_barman_01");
            Console.WriteLine(string.Format(" -> Created NPC #{0}: Name={1}, Guid={2}", marcus.NpcId, marcus.Name, marcus.Guid));

            // Test 2: Relationships (Player: +72, Police: -20, Gang A: +40)
            Console.WriteLine("\n[Test 2] Setting Relationships for Marcus...");
            store.UpdateRelationship(marcus.NpcId, "Player", 72);
            store.UpdateRelationship(marcus.NpcId, "Police", -20);
            store.UpdateRelationship(marcus.NpcId, "Gang A", 40);

            // Test 3: Event History
            Console.WriteLine("\n[Test 3] Adding Event History...");
            store.AddEvent(marcus.NpcId, "Player saved Marcus from gang ambush", "Saved_Life");
            store.AddEvent(marcus.NpcId, "Marcus gave player a modified sports car", "Gave_Vehicle");
            store.AddEvent(marcus.NpcId, "Marcus witnessed player committing robbery at Fleeca Bank", "Shared_Secret");

            // Test 4: Dynamic Role Mutation ("Make the bartender secretly work for the police")
            Console.WriteLine("\n[Test 4] Mutating Marcus's Role...");
            store.MutateRole(marcus.NpcId, "Undercover Police Informant", "Blackmailed by LSPD detective");

            // Test 5: Semantic Favor Search ("Find someone who owes me a favor")
            Console.WriteLine("\n[Test 5] Querying World History: 'Find someone who owes me a favor'...");
            var debtors = store.FindNpcsWhoOweFavor(30);
            Console.WriteLine(string.Format(" -> Debtors Found: {0}", debtors.Count));
            foreach (var d in debtors)
            {
                Console.WriteLine(string.Format("    • #{0} {1} ({2}) - Role: {3}", d.NpcId, d.Name, d.Occupation, d.HiddenRole));
            }

            // Test 6: LivingLSAIs Context Injection Format
            Console.WriteLine("\n[Test 6] Formatting LivingLSAIs Fact Payload...");
            string factPayload = store.FormatNpcContextForLivingLSAIs(marcus.NpcId);
            Console.WriteLine(string.Format(" -> Payload:\n    \"{0}\"", factPayload));

            // Verification assertion
            if (debtors.Count == 1 && debtors[0].Name == "Marcus" && marcus.HiddenRole == "Undercover Police Informant")
            {
                Console.WriteLine("\n=================================================");
                Console.WriteLine("  SUCCESS: ALL STEP 1 UNIT TESTS PASSED CLEANLY!  ");
                Console.WriteLine("=================================================");
            }
            else
            {
                Console.WriteLine("\nFAILURE: Unit test assertions failed!");
                Environment.Exit(1);
            }
        }
    }
}
