using System;
using System.IO;
using DynamicWorldEngine.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Tests
{
    class TestModularGeneratorArchitecture
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
            Console.WriteLine("  MODULAR GENERATOR ARCHITECTURE TEST SUITE (Phase 3)    ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(Path.GetTempPath(), "dynamic_world_modular_test_" + Guid.NewGuid().ToString().Substring(0, 8));
            Directory.CreateDirectory(testDir);

            try
            {
                // ========================================================
                // TEST 1: LlmTransportClient Isolation
                // ========================================================
                Console.WriteLine("--- TEST 1: LlmTransportClient Isolation ---");
                {
                    var client = new LlmTransportClient();
                    AssertTrue("LlmTransportClient initializes cleanly", client != null);
                    AssertTrue("GeminiKeyCount is non-negative", client.GeminiKeyCount >= 0);
                    AssertTrue("GroqKeyCount is non-negative", client.GroqKeyCount >= 0);

                    // Test key manipulation
                    client.AddGeminiKey("AIzaSyMockKeyForTestingOnly1234567");
                    AssertTrue("AddGeminiKey increases key count", client.GeminiKeyCount > 0);

                    client.PreferredGeminiModel = "gemini-test-custom";
                    AssertTrue("PreferredGeminiModel setter works", client.PreferredGeminiModel == "gemini-test-custom");

                    // Clear keys to test failover behavior
                    client.ClearKeys();
                    AssertTrue("ClearKeys resets key counts to zero", client.GeminiKeyCount == 0 && client.GroqKeyCount == 0);

                    string nullResult = client.ExecuteGenerationWithFailover("System Prompt", "User Prompt");
                    AssertTrue("ExecuteGenerationWithFailover returns null when no keys configured", nullResult == null);
                }

                // ========================================================
                // TEST 2: LocalWorldSynthesizer Isolation (Pure Offline)
                // ========================================================
                Console.WriteLine("\n--- TEST 2: LocalWorldSynthesizer Isolation (Pure Offline) ---");
                {
                    var synth = new LocalWorldSynthesizer();
                    AssertTrue("LocalWorldSynthesizer initializes cleanly", synth != null);

                    // Bank heist
                    string heistJson = synth.SynthesizeEnvironmentLocally("robbery at the pacific standard bank vault with thermal drillers");
                    AssertTrue("Bank heist produces non-empty JSON", !string.IsNullOrEmpty(heistJson));
                    JObject heistRoot = JObject.Parse(heistJson);
                    AssertTrue("Bank heist action is BUILD_ENVIRONMENT", (string)heistRoot["action"] == "BUILD_ENVIRONMENT");
                    AssertTrue("Bank heist contains vault or cash props",
                        heistJson.Contains("vault") || heistJson.Contains("cash") || heistJson.Contains("drill"));

                    // Covert interrogation
                    string interJson = synth.SynthesizeEnvironmentLocally("clandestine interrogation in secret underground basement");
                    AssertTrue("Interrogation produces non-empty JSON", !string.IsNullOrEmpty(interJson));
                    JObject interRoot = JObject.Parse(interJson);
                    AssertTrue("Interrogation action is BUILD_ENVIRONMENT", (string)interRoot["action"] == "BUILD_ENVIRONMENT");
                    AssertTrue("Interrogation archetype is covert_interrogation",
                        (string)interRoot["data"]["archetype"] == "covert_interrogation");

                    // Military compound
                    string milJson = synth.SynthesizeEnvironmentLocally("fort zancudo military base armed checkpoint with guards and tank");
                    AssertTrue("Military base produces non-empty JSON", !string.IsNullOrEmpty(milJson));
                    JObject milRoot = JObject.Parse(milJson);
                    AssertTrue("Military action is BUILD_ENVIRONMENT", (string)milRoot["action"] == "BUILD_ENVIRONMENT");
                }

                // ========================================================
                // TEST 3: Local Scenario & Event Synthesis
                // ========================================================
                Console.WriteLine("\n--- TEST 3: Local Scenario & Event Synthesis ---");
                {
                    var synth = new LocalWorldSynthesizer();

                    // Scenario with timeline beats
                    string scenarioJson = synth.SynthesizeScenarioOrEventLocally("direct a scenario with timeline beats and tactical raid");
                    AssertTrue("Scenario director produces non-empty JSON", !string.IsNullOrEmpty(scenarioJson));
                    JObject scenRoot = JObject.Parse(scenarioJson);
                    AssertTrue("Scenario action is DIRECT_SCENARIO", (string)scenRoot["action"] == "DIRECT_SCENARIO");
                    AssertTrue("Scenario has beats array", scenRoot["data"]["beats"] != null);

                    // Mutation / Hostility
                    string mutateJson = synth.SynthesizeScenarioOrEventLocally("betray the boss and open fire with hostile attack");
                    AssertTrue("Mutation produces non-empty JSON", !string.IsNullOrEmpty(mutateJson));
                    JObject mutRoot = JObject.Parse(mutateJson);
                    AssertTrue("Mutation action is MUTATE_ENTITY_STATE", (string)mutRoot["action"] == "MUTATE_ENTITY_STATE");

                    // Quest / Stash
                    string stashJson = synth.SynthesizeScenarioOrEventLocally("hidden weapons stash with contraband cash");
                    AssertTrue("Stash produces non-empty JSON", !string.IsNullOrEmpty(stashJson));
                    JObject stashRoot = JObject.Parse(stashJson);
                    AssertTrue("Stash action is ATTACH_QUEST_OR_STASH", (string)stashRoot["action"] == "ATTACH_QUEST_OR_STASH");

                    // Ambient World Event (Default)
                    string eventJson = synth.SynthesizeScenarioOrEventLocally("tactical police raid response");
                    AssertTrue("Event produces non-empty JSON", !string.IsNullOrEmpty(eventJson));
                    JObject evtRoot = JObject.Parse(eventJson);
                    AssertTrue("Event action is TRIGGER_WORLD_EVENT", (string)evtRoot["action"] == "TRIGGER_WORLD_EVENT");
                }

                // ========================================================
                // TEST 4: WorldGeneratorService Coordinator
                // ========================================================
                Console.WriteLine("\n--- TEST 4: WorldGeneratorService Coordinator ---");
                {
                    var coordinator = new WorldGeneratorService(testDir);
                    AssertTrue("Coordinator exposes TransportClient", coordinator.TransportClient != null);
                    AssertTrue("Coordinator exposes LocalSynthesizer", coordinator.LocalSynthesizer != null);
                    AssertTrue("Coordinator exposes Cache", coordinator.Cache != null);

                    // Preset routing
                    string presetJson = coordinator.GenerateSceneJsonFromPrompt("preset:chopshop");
                    AssertTrue("Preset chopshop returns non-empty JSON", !string.IsNullOrEmpty(presetJson));
                    JObject presetRoot = JObject.Parse(presetJson);
                    AssertTrue("Preset action is BUILD_ENVIRONMENT", (string)presetRoot["action"] == "BUILD_ENVIRONMENT");

                    // Fallback to local synthesizer when generating without network/keys
                    string sceneJson = coordinator.GenerateSceneJsonFromPrompt("underground poker gambling den");
                    AssertTrue("Scene generation produces valid JSON", !string.IsNullOrEmpty(sceneJson));
                    JObject sceneRoot = JObject.Parse(sceneJson);
                    AssertTrue("Scene action is BUILD_ENVIRONMENT", (string)sceneRoot["action"] == "BUILD_ENVIRONMENT");

                    // Cache check: second call should return cached response
                    string cachedJson = coordinator.GenerateSceneJsonFromPrompt("underground poker gambling den");
                    AssertTrue("Cached call returns identical JSON", cachedJson == sceneJson);
                }

                // ========================================================
                // TEST 5: Backward Compatibility Facade
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Backward Compatibility Facade ---");
                {
                    var service = new WorldGeneratorService(testDir);

                    // System prompts
                    string scenePrompt = service.GetAstraSceneArchitectSystemPrompt();
                    AssertTrue("GetAstraSceneArchitectSystemPrompt returns non-empty prompt", !string.IsNullOrEmpty(scenePrompt));
                    AssertTrue("Scene prompt instructs BUILD_ENVIRONMENT", scenePrompt.Contains("BUILD_ENVIRONMENT"));
                    AssertTrue("Scene prompt includes Engram principle", scenePrompt.Contains("ENGRAM PRINCIPLE"));

                    string directorPrompt = service.GetAstraWorldDirectorSystemPrompt("NPC Marcus is alive");
                    AssertTrue("GetAstraWorldDirectorSystemPrompt returns non-empty prompt", !string.IsNullOrEmpty(directorPrompt));
                    AssertTrue("Director prompt includes active world context", directorPrompt.Contains("NPC Marcus is alive"));

                    // Aliases
                    string envJson = service.GenerateEnvironmentJsonFromPrompt("luxury yacht party");
                    AssertTrue("GenerateEnvironmentJsonFromPrompt returns valid JSON", !string.IsNullOrEmpty(envJson));

                    string mutJson = service.GenerateMutationOrEventJsonFromPrompt("cartel ambush");
                    AssertTrue("GenerateMutationOrEventJsonFromPrompt returns valid JSON", !string.IsNullOrEmpty(mutJson));

                    string localDirect = service.SynthesizeEnvironmentLocally("beach bonfire bbq");
                    AssertTrue("SynthesizeEnvironmentLocally returns valid JSON", !string.IsNullOrEmpty(localDirect));

                    string localEvent = service.SynthesizeScenarioOrEventLocally("emergency response");
                    AssertTrue("SynthesizeScenarioOrEventLocally returns valid JSON", !string.IsNullOrEmpty(localEvent));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\n  [FATAL] Unhandled exception: {0}", ex));
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }

            Console.WriteLine(string.Format("\n=========================================================="));
            Console.WriteLine(string.Format("  RESULTS: {0}/{1} PASSED", passed, total));
            Console.WriteLine(string.Format("=========================================================="));

            return (passed == total) ? 0 : 1;
        }
    }
}
