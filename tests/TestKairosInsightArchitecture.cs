using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Services;
using DynamicWorldEngine.Simulation;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Tests
{
    class TestKairosInsightArchitecture
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

        static int Main(string[] args)
        {
            Console.WriteLine("==========================================================");
            Console.WriteLine("  KAIROS INSIGHT ARCHITECTURE TEST SUITE (Phase 6)       ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(Path.GetTempPath(), "dwe_kairos_test_" + Guid.NewGuid().ToString().Substring(0, 8));
            Directory.CreateDirectory(testDir);

            try
            {
                var store = new WorldMemoryStore(testDir);
                var recomputer = new EphemeralStateRecomputer();
                var salience = new SalienceEvaluator();
                var compiler = new KairosInsightCompiler(store, recomputer, salience);

                // Seed world facts
                var informant = new NpcRecord
                {
                    NpcId = 101,
                    Name = "Marco_Reyes",
                    Occupation = "Valet",
                    PublicFaction = "Civilians",
                    HiddenRole = "Police_Informant",
                    Secrets = new List<string> { "Smuggling ledger hidden in car trunk" },
                    Fear = 50,
                    Trust = -20,
                    IsAlive = true,
                    ActiveGoal = new NpcGoal("ALERT_POLICE_CONTACT", "Detective_Miller", 9, "Alert detective to cartel presence")
                };
                store.EnrollNpc(informant);

                var boss = new NpcRecord
                {
                    NpcId = 102,
                    Name = "Don_Salieri",
                    Occupation = "Cartel Boss",
                    PublicFaction = "Salieri_Cartel",
                    HiddenRole = "Syndicate_Leader",
                    Fear = 0,
                    Trust = 0,
                    IsAlive = true
                };
                store.EnrollNpc(boss);

                DateTime now = DateTime.UtcNow;

                // Seed recent events
                store.RecordEventGraphEventWithWitnesses(
                    101, "Player", "Marco_Reyes", "EXPOSITION_SECRET", 0.90f,
                    "Player accused Marco of being a police mole", "Accusation",
                    new List<string> { "Marco panicked" }, null,
                    120f, 240f, 20f
                );

                store.RecordEventGraphEventWithWitnesses(
                    102, "Salieri_Cartel", "Families", "DRIVEBY_SHOOTING", 0.95f,
                    "Cartel hitmen ambushed Families gang on the corner", "Violence",
                    new List<string> { "Casualties sustained" }, null,
                    125f, 245f, 20f
                );

                store.RecordEventGraphEventWithWitnesses(
                    102, "Families", "Salieri_Cartel", "FATAL_RETALIATION", 1.0f,
                    "Families returned fire killing 1 cartel guard", "Mortality",
                    new List<string> { "Guard dead" }, null,
                    122f, 242f, 20f
                );

                store.Save(true);

                // ========================================================
                // TEST 1: Situation Brief Compilation (Law 10: Stratification)
                // ========================================================
                Console.WriteLine("--- TEST 1: Situation Brief Compilation ---");
                {
                    var brief = compiler.CompileSituationBrief(120f, 240f, 20f, asOf: now);
                    AssertTrue("Situation brief is not null", brief != null);
                    AssertTrue("Local danger score reflects gunfire (> 0.40)", brief.DangerScore > 0.40f);
                    AssertTrue("Threat summary reflects active conflict", brief.ThreatSummary.Contains("hostility") || brief.ThreatSummary.Contains("confrontation") || brief.ThreatSummary.Contains("tension"));
                    AssertTrue("Active faction wars detected", brief.ActiveFactionWars.Count > 0);
                    AssertTrue("Recent critical milestones captured", brief.RecentCriticalMilestones.Count > 0);
                    AssertTrue("Key characters include relevant NPCs", brief.KeyCharacters.Count >= 2);
                }

                // ========================================================
                // TEST 2: Token-Efficient Formatting (Law 9: Compiled Artifact)
                // ========================================================
                Console.WriteLine("\n--- TEST 2: Token-Efficient Prompt Formatting ---");
                {
                    var brief = compiler.CompileSituationBrief(120f, 240f, 20f);
                    string formatted = brief.FormatForPrompt();

                    AssertTrue("Formatted text contains header", formatted.Contains("### SITUATION BRIEF"));
                    AssertTrue("Formatted text contains Location Threat", formatted.Contains("- **Location Threat**:"));
                    AssertTrue("Formatted text contains Key Characters", formatted.Contains("- **Key Characters & Effective Postures**:"));
                    AssertTrue("Formatted text contains Critical Milestones", formatted.Contains("- **Recent Critical Milestones**:"));
                    AssertTrue("Formatted text contains Dramatic Opportunities", formatted.Contains("- **Dramatic Narrative Opportunities**:"));

                    // Word count under 250 words (far smaller than raw 50-event JSON dumps)
                    int wordCount = formatted.Split(new char[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    AssertTrue("Brief is compact and token-efficient (< 250 words)", wordCount < 250);
                }

                // ========================================================
                // TEST 3: High-Order Dramatic Opportunities
                // ========================================================
                Console.WriteLine("\n--- TEST 3: High-Order Dramatic Opportunities ---");
                {
                    var brief = compiler.CompileSituationBrief(120f, 240f, 20f);
                    AssertTrue("Dramatic opportunities list is not empty", brief.DramaticOpportunities.Count > 0);
                    AssertTrue("Opportunity recognizes informant alerting police", brief.DramaticOpportunities.Exists(o => o.Contains("alerting police") || o.Contains("extraction")));
                    AssertTrue("Opportunity recognizes active faction war cover", brief.DramaticOpportunities.Exists(o => o.Contains("faction conflict") || o.Contains("cover")));
                    AssertTrue("Opportunity recognizes secret leverage", brief.DramaticOpportunities.Exists(o => o.Contains("secrets") || o.Contains("leverage")));
                }

                // ========================================================
                // TEST 4: NPC Dossier Compilation
                // ========================================================
                Console.WriteLine("\n--- TEST 4: NPC Dossier Compilation ---");
                {
                    var dossier = compiler.CompileNpcDossier(101);
                    AssertTrue("NPC dossier is not null", dossier != null);
                    AssertTrue("Dossier has correct name", dossier.Name == "Marco_Reyes");
                    AssertTrue("Dossier has hidden role", dossier.HiddenRole == "Police_Informant");
                    AssertTrue("Dossier posture summary reflects alert/panic", dossier.PostureSummary.Contains("ALERT") || dossier.PostureSummary.Contains("PANICKED"));
                    AssertTrue("Dossier contains active goals", dossier.ActiveGoals.Count > 0);
                    AssertTrue("Dossier contains known secrets", dossier.KnownSecrets.Contains("Smuggling ledger hidden in car trunk"));
                }

                // ========================================================
                // TEST 5: Task-Specific Scoping (Law 5)
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Task-Specific Scoping ---");
                {
                    // Focus specifically on Don_Salieri (102)
                    var scopedBrief = compiler.CompileSituationBrief(focusNpcId: 102);
                    AssertTrue("Scoped brief first character is Don_Salieri", scopedBrief.KeyCharacters.Count > 0 && scopedBrief.KeyCharacters[0].NpcId == 102);

                    // Scope to faction "Civilians"
                    var civilianBrief = compiler.CompileSituationBrief(targetFaction: "Civilians");
                    AssertTrue("Civilian brief contains Marco_Reyes", civilianBrief.KeyCharacters.Exists(c => c.Name == "Marco_Reyes"));
                }

                // ========================================================
                // TEST 6: EngineCore & WorldGeneratorService Integration
                // ========================================================
                Console.WriteLine("\n--- TEST 6: EngineCore & WorldGeneratorService Integration ---");
                {
                    var engine = new EngineCore(testDir);
                    AssertTrue("EngineCore InsightCompiler is exposed", engine.InsightCompiler != null);

                    var brief = engine.GetKairosSituationBrief(120f, 240f, 20f, focusNpcId: 101);
                    AssertTrue("EngineCore GetKairosSituationBrief works", brief != null);

                    var dossier = engine.GetKairosNpcDossier(101);
                    AssertTrue("EngineCore GetKairosNpcDossier works", dossier != null && dossier.Name == "Marco_Reyes");

                    // Director synthesis with Kairos brief
                    string directorJson = engine.Generator.GenerateScenarioDirectorJsonFromPrompt("ambush informant near motel", brief);
                    AssertTrue("GenerateScenarioDirectorJsonFromPrompt with brief returned JSON", !string.IsNullOrEmpty(directorJson));

                    JObject parsed = JObject.Parse(directorJson);
                    AssertTrue("Generated director JSON has valid action or event", parsed["action"] != null);
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir))
                    {
                        Directory.Delete(testDir, true);
                    }
                }
                catch { }
            }

            Console.WriteLine("\n==========================================================");
            Console.WriteLine(string.Format("  TEST RESULTS: {0}/{1} ASSERTIONS PASSED", passed, total));
            Console.WriteLine("==========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
