using System;
using System.IO;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Tests
{
    public class TestStep2ActionParser
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE - STEP 2 UNIT TEST RUNNER  ");
            Console.WriteLine("=================================================");

            string testDbDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_output_step2");
            if (Directory.Exists(testDbDir))
            {
                Directory.Delete(testDbDir, true);
            }

            var store = new WorldMemoryStore(testDbDir);
            var parser = new ActionParser();
            var executor = new WorldActionExecutor(store);

            // Sample JSON payload representing LLM response for "Create a drug dealer here"
            string sampleJson = @"
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
                        },
                        {
                            ""description"": ""Owed money by local mechanic Marcus"",
                            ""sentiment_tag"": ""Owed_Money""
                        }
                    ]
                }
            }";

            Console.WriteLine("\n[Step 2 - Test 1] Parsing SPAWN_PERSISTENT_NPC JSON payload...");
            ActionPayload payload = parser.ParseAndValidate(sampleJson);
            Console.WriteLine(string.Format(" -> Action Parsed: {0}", payload.Action));
            Console.WriteLine(string.Format(" -> Target Name: {0}, Occupation: {1}, Model: {2}", payload.Data.Name, payload.Data.Occupation, payload.Data.PedModel));

            Console.WriteLine("\n[Step 2 - Test 2] Executing Action into World DB...");
            NpcRecord darnell = executor.ExecuteSpawnNpcAction(payload);
            Console.WriteLine(string.Format(" -> Created NPC #{0}: Name={1}, Faction={2}", darnell.NpcId, darnell.Name, darnell.PublicFaction));

            Console.WriteLine("\n[Step 2 - Test 3] Checking Relationships...");
            foreach (var rel in darnell.Relationships)
            {
                Console.WriteLine(string.Format("    • {0}: {1}{2}", rel.Key, rel.Value >= 0 ? "+" : "", rel.Value));
            }

            Console.WriteLine("\n[Step 2 - Test 4] Checking LivingLSAIs Fact Payload Formatting...");
            string factPayload = store.FormatNpcContextForLivingLSAIs(darnell.NpcId);
            Console.WriteLine(string.Format(" -> Payload:\n    \"{0}\"", factPayload));

            // Assertion verification
            if (darnell.Name.Contains("Darnell") && darnell.Occupation == "Drug Dealer" && darnell.Relationships["Police"] == -50)
            {
                Console.WriteLine("\n=================================================");
                Console.WriteLine("  SUCCESS: ALL STEP 2 UNIT TESTS PASSED CLEANLY!  ");
                Console.WriteLine("=================================================");
            }
            else
            {
                Console.WriteLine("\nFAILURE: Step 2 assertions failed!");
                Environment.Exit(1);
            }
        }
    }
}
