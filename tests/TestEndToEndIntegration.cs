using System;
using System.IO;
using DynamicWorldEngine;

namespace DynamicWorldEngine.Tests
{
    public class TestEndToEndIntegration
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE - END-TO-END INTEGRATION TEST      ");
            Console.WriteLine("==========================================================");

            string testDbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_output_e2e");
            if (Directory.Exists(testDbDir))
            {
                Directory.Delete(testDbDir, true);
            }

            string mockLlaiDir = Path.Combine(testDbDir, "LivingLSAIs_Host");
            var engine = new EngineCore(testDbDir, mockLlaiDir);

            // Step 1: Simulate receiving LLM action for "Create a drug dealer here"
            Console.WriteLine("\n[E2E Step 1] Processing 'Create a drug dealer here' Payload...");
            string sampleDealerJson = @"
            {
                ""action"": ""SPAWN_PERSISTENT_NPC"",
                ""data"": {
                    ""entity_id"": ""dealer_darnell_01"",
                    ""name"": ""Darnell 'D-Lance' Jenkins"",
                    ""occupation"": ""Drug Dealer"",
                    ""public_faction"": ""Families"",
                    ""hidden_role"": ""None"",
                    ""ped_model"": ""g_m_y_famca_01"",
                    ""position_offset"": { ""x"": 2.0, ""y"": 1.5, ""z"": 0.0 },
                    ""relationships"": {
                        ""Player"": 10,
                        ""Police"": -50,
                        ""Families"": 60,
                        ""Ballas"": -30
                    },
                    ""initial_events"": [
                        {
                            ""description"": ""Operates street corner in Chamberlain Hills Families turf"",
                            ""sentiment_tag"": ""Territory_Active""
                        }
                    ]
                }
            }";

            var darnell = engine.ExecuteSpawnNpcCommand(sampleDealerJson);
            Console.WriteLine(string.Format(" -> Created NPC #{0}: Name={1}, Occupation={2}", darnell.NpcId, darnell.Name, darnell.Occupation));

            // Step 2: Simulate receiving LLM action for "Make the bartender secretly work for police"
            Console.WriteLine("\n[E2E Step 2] Processing 'Make the bartender an undercover cop' Payload...");
            string sampleCopJson = @"
            {
                ""action"": ""SPAWN_PERSISTENT_NPC"",
                ""data"": {
                    ""entity_id"": ""barman_marcus_01"",
                    ""name"": ""Marcus"",
                    ""occupation"": ""Mechanic / Bartender"",
                    ""public_faction"": ""Civilian"",
                    ""hidden_role"": ""Undercover Police Informant"",
                    ""ped_model"": ""s_m_y_barman_01"",
                    ""position_offset"": { ""x"": 5.0, ""y"": 0.0, ""z"": 0.0 },
                    ""relationships"": {
                        ""Player"": 72,
                        ""Police"": -20,
                        ""Gang A"": 40
                    },
                    ""initial_events"": [
                        {
                            ""description"": ""Player saved Marcus from gang ambush"",
                            ""sentiment_tag"": ""Saved_Life""
                        },
                        {
                            ""description"": ""Blackmailed by LSPD detective to leak cartel info"",
                            ""sentiment_tag"": ""Role_Mutation""
                        }
                    ]
                }
            }";

            var marcus = engine.ExecuteSpawnNpcCommand(sampleCopJson);
            Console.WriteLine(string.Format(" -> Created NPC #{0}: Name={1}, HiddenRole={2}", marcus.NpcId, marcus.Name, marcus.HiddenRole));

            // Step 3: Verify LLAI CUSTOM RULES.txt contains both NPC facts
            Console.WriteLine("\n[E2E Step 3] Inspecting Auto-Injected LivingLSAIs Rules File...");
            string rulesPath = engine.Bridge.GetCustomRulesFilePath();
            if (File.Exists(rulesPath))
            {
                string rulesContent = File.ReadAllText(rulesPath);
                Console.WriteLine(string.Format("\n[Rules File Content Preview]:\n{0}", rulesContent));

                bool hasDarnell = rulesContent.Contains("Darnell") && rulesContent.Contains("Drug Dealer");
                bool hasMarcus = rulesContent.Contains("Marcus") && rulesContent.Contains("Undercover Police Informant");

                if (hasDarnell && hasMarcus)
                {
                    Console.WriteLine("==========================================================");
                    Console.WriteLine("  SUCCESS: END-TO-END INTEGRATION TEST PASSED PERFECTLY!  ");
                    Console.WriteLine("==========================================================");
                    return;
                }
            }

            Console.WriteLine("\nFAILURE: End-to-end assertions failed!");
            Environment.Exit(1);
        }
    }
}
