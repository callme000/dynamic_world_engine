using System;
using System.IO;
using DynamicWorldEngine.Bridge;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Tests
{
    public class TestStep4Bridge
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE - STEP 4 UNIT TEST RUNNER  ");
            Console.WriteLine("=================================================");

            string testDbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_output_step4");
            if (Directory.Exists(testDbDir))
            {
                Directory.Delete(testDbDir, true);
            }

            var store = new WorldMemoryStore(testDbDir);

            // Populate DB with test peds
            var darnell = store.CreateNpc("Darnell 'D-Lance' Jenkins", "Drug Dealer", "Families", "g_m_y_famca_01");
            store.UpdateRelationship(darnell.NpcId, "Player", 10);
            store.UpdateRelationship(darnell.NpcId, "Police", -50);
            store.AddEvent(darnell.NpcId, "Operates street corner in Chamberlain Hills Families turf", "Territory_Active");

            var marcus = store.CreateNpc("Marcus", "Mechanic", "Civilian", "s_m_y_barman_01", "Undercover Police Informant");
            store.UpdateRelationship(marcus.NpcId, "Player", 72);
            store.UpdateRelationship(marcus.NpcId, "Police", -20);
            store.AddEvent(marcus.NpcId, "Player saved Marcus from gang ambush", "Saved_Life");

            // Initialize Bridge targeting local mock folder
            string mockLlaiDir = Path.Combine(testDbDir, "mock_LivingLSAIs");
            var bridge = new LivingLsAiBridge(store, mockLlaiDir);

            Console.WriteLine("\n[Step 4 - Test 1] Syncing NPC Facts to LLAI CUSTOM RULES.txt...");
            bool success = bridge.SyncAllNpcFactsToCustomRules();
            Console.WriteLine(string.Format(" -> Sync Result: {0}", success));

            string rulesPath = bridge.GetCustomRulesFilePath();
            Console.WriteLine(string.Format(" -> Target Path: {0}", rulesPath));

            if (File.Exists(rulesPath))
            {
                string rulesContent = File.ReadAllText(rulesPath);
                Console.WriteLine(string.Format("\n[Rules File Content Preview]:\n{0}", rulesContent));

                if (rulesContent.Contains("Darnell") && rulesContent.Contains("Marcus") && rulesContent.Contains("Undercover Police Informant"))
                {
                    Console.WriteLine("\n=================================================");
                    Console.WriteLine("  SUCCESS: ALL STEP 4 UNIT TESTS PASSED CLEANLY!  ");
                    Console.WriteLine("=================================================");
                    return;
                }
            }

            Console.WriteLine("\nFAILURE: Step 4 assertions failed!");
            Environment.Exit(1);
        }
    }
}
