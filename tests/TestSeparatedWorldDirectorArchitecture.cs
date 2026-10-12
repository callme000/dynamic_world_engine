using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Runtime;
using DynamicWorldEngine.Services;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA.Math;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Tests
{
    class TestSeparatedWorldDirectorArchitecture
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
            Console.WriteLine("  DECOUPLED WORLD DIRECTOR & SCENE GENERATOR TEST SUITE   ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_DecoupledDirector_Data");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            try
            {
                var service = new WorldGeneratorService(testDir);
                var parser = new ActionParser();
                var planner = new SpatialWorldPlanner();
                var timeline = new DirectorTimelineManager();
                var spawner = new EntitySpawnerService();
                var runtime = new RuntimeExecutor(spawner, null, null, null, null, null, null, timeline);
                var engine = new EngineCore(testDir, null, null, null, null, null, runtime);

                // ========================================================
                // TEST 1: Pure Scene Generation (t = 0, No Forced Script Beats)
                // ========================================================
                Console.WriteLine("[Phase 1] Pure Scene Generation (AI World Architect)...");

                string sceneJson = service.SynthesizeEnvironmentLocally("luxury beach party with cocktails and music", false);
                AssertTrue("SynthesizeEnvironmentLocally(..., false) produces non-empty JSON", !string.IsNullOrEmpty(sceneJson));

                JObject sceneObj = JObject.Parse(sceneJson);
                AssertTrue("Action is BUILD_ENVIRONMENT", string.Equals((string)sceneObj["action"], "BUILD_ENVIRONMENT", StringComparison.OrdinalIgnoreCase));
                AssertTrue("Pure scene JSON has NO scenario_director", sceneObj["data"]["scenario_director"] == null);

                BuildEnvironmentPayload scenePayload = parser.ParseBuildEnvironment(sceneJson);
                AssertTrue("Parsed payload has valid environment_id", !string.IsNullOrEmpty(scenePayload.Data.EnvironmentId));
                AssertTrue("Parsed payload has ScenarioDirector == null", scenePayload.Data.ScenarioDirector == null);

                SpatialExecutionPlan plan = planner.PlanEnvironment(scenePayload.Data, new Vector3(100f, 200f, 30f), 0f);
                AssertTrue("Plan generated successfully", plan != null);
                AssertTrue("Plan has ScenarioDirector == null", plan.ScenarioDirector == null);
                AssertTrue("Plan has props assigned", plan.ResolvedProps != null && plan.ResolvedProps.Count > 0);

                EnvironmentRecord env = engine.CommissionEnvironment(plan, scenePayload.Data);
                AssertTrue("Environment commissioned into memory store", env != null && env.Status == "COMMISSIONED");

                // ========================================================
                // TEST 2: Dedicated World Director Payload (DIRECT_SCENARIO)
                // ========================================================
                Console.WriteLine("\n[Phase 2] Dedicated World Director Payload (DIRECT_SCENARIO)...");

                string directorJson = service.SynthesizeScenarioOrEventLocally("create a storyline scenario with escalating cartel raid");
                AssertTrue("SynthesizeScenarioOrEventLocally produces non-empty JSON", !string.IsNullOrEmpty(directorJson));

                JObject directorObj = JObject.Parse(directorJson);
                AssertTrue("Director action is DIRECT_SCENARIO", string.Equals((string)directorObj["action"], "DIRECT_SCENARIO", StringComparison.OrdinalIgnoreCase));
                AssertTrue("Director payload has beats", directorObj["data"]["beats"] != null);

                DirectScenarioPayload directPayload = parser.ParseDirectScenario(directorJson);
                AssertTrue("ParseDirectScenario succeeds", directPayload != null && directPayload.Data != null);
                AssertTrue("Parsed ScenarioDirectorData has at least 1 beat", directPayload.Data.Beats.Count > 0);
                AssertTrue("Beat has valid trigger and delay", directPayload.Data.Beats[0].DelaySeconds > 0);

                // Test invalid action rejection in ParseDirectScenario
                bool exceptionThrown = false;
                try
                {
                    parser.ParseDirectScenario(sceneJson); // Passing BUILD_ENVIRONMENT to ParseDirectScenario
                }
                catch (InvalidOperationException)
                {
                    exceptionThrown = true;
                }
                AssertTrue("ParseDirectScenario rejects non-DIRECT_SCENARIO payload", exceptionThrown);

                // ========================================================
                // TEST 3: Dynamic Scenario Binding onto Active Environment
                // ========================================================
                Console.WriteLine("\n[Phase 3] Dynamic Scenario Binding & Timeline Execution...");

                bool loaded = engine.DirectScenario(directPayload.Data, env.EnvironmentId);
                AssertTrue("EngineCore.DirectScenario successfully loads scenario", loaded);
                AssertTrue("Timeline manager is active", timeline.IsScenarioActive);
                AssertTrue("ActiveScenarioTitle matches payload", !string.IsNullOrEmpty(timeline.ActiveScenarioTitle));

                // Verify that scene props in PlanRepository and EnvironmentRecord are unaffected
                EnvironmentRecord storedEnv = engine.Store.GetEnvironment(env.EnvironmentId);
                AssertTrue("Stored environment record remains intact", storedEnv != null && storedEnv.EnvironmentId == env.EnvironmentId);
                SpatialExecutionPlan storedPlan = engine.Plans.GetPlan(env.EnvironmentId);
                AssertTrue("Physical plan in PlanRepository remains unmutated", storedPlan != null && storedPlan.ResolvedProps.Count == plan.ResolvedProps.Count);

                // ========================================================
                // TEST 4: Timeline Processing & Beat Execution
                // ========================================================
                Console.WriteLine("\n[Phase 4] Timeline Processing & Event Dispatch...");

                int eventsFired = 0;
                int mutationsFired = 0;

                // Create a 0-second beat scenario to test immediate execution
                var instantScenario = new ScenarioDirectorData
                {
                    ScenarioTitle = "Instant Action Escalation",
                    NarrativeIntro = "Hostile confrontation erupts immediately.",
                    Beats = new List<ScenarioBeatData>
                    {
                        new ScenarioBeatData
                        {
                            BeatId = "beat_instant",
                            DelaySeconds = 0.0f,
                            Trigger = "timer",
                            Announcement = "Ambush unfolds!",
                            WorldEvent = new TriggerWorldEventData
                            {
                                EventType = "AMBUSH",
                                Title = "Cartel Ambush",
                                Description = "Hostile cartel operatives engage."
                            },
                            Mutations = new List<MutateEntityData>
                            {
                                new MutateEntityData
                                {
                                    TargetEntityId = "npc_1",
                                    Hostility = "hostile_attack",
                                    GiveWeapon = "WEAPON_PISTOL"
                                }
                            }
                        }
                    }
                };

                runtime.DirectScenario(instantScenario, env.EnvironmentId, new Vector3(100f, 200f, 30f));
                AssertTrue("Instant scenario loaded", timeline.IsScenarioActive);

                timeline.ProcessTimeline(null,
                    (evt) => { eventsFired++; },
                    (mut) => { mutationsFired++; });

                AssertTrue("World event fired during timeline processing", eventsFired == 1);
                AssertTrue("Mutation fired during timeline processing", mutationsFired == 1);
                AssertTrue("Timeline completes after all beats execute", !timeline.IsScenarioActive);

                // ========================================================
                // TEST 5: Backward Compatibility for Legacy Embedded Director
                // ========================================================
                Console.WriteLine("\n[Phase 5] Backward Compatibility (Legacy Embedded Directors)...");

                string legacyJson = service.SynthesizeEnvironmentLocally("bank heist vault robbery", true);
                JObject legacyObj = JObject.Parse(legacyJson);
                AssertTrue("SynthesizeEnvironmentLocally(..., true) includes scenario_director", legacyObj["data"]["scenario_director"] != null);

                BuildEnvironmentPayload legacyPayload = parser.ParseBuildEnvironment(legacyJson);
                AssertTrue("ParseBuildEnvironment parses legacy embedded director", legacyPayload.Data.ScenarioDirector != null);

                SpatialExecutionPlan legacyPlan = planner.PlanEnvironment(legacyPayload.Data, new Vector3(50f, 50f, 10f), 90f);
                AssertTrue("PlanEnvironment preserves legacy ScenarioDirector", legacyPlan.ScenarioDirector != null);
                AssertTrue("Legacy plan has beats", legacyPlan.ScenarioDirector.Beats != null && legacyPlan.ScenarioDirector.Beats.Count > 0);

                // Verify alias methods
                string aliasScene = engine.GenerateEnvironmentJsonFromNaturalLanguage("street taco vendor");
                AssertTrue("GenerateEnvironmentJsonFromNaturalLanguage alias produces valid scene JSON", !string.IsNullOrEmpty(aliasScene));

                string aliasDirector = engine.GenerateMutationOrEventFromNaturalLanguage("trigger sudden betrayal");
                AssertTrue("GenerateMutationOrEventFromNaturalLanguage alias produces valid director JSON", !string.IsNullOrEmpty(aliasDirector));

                // System prompts distinction
                string scenePrompt = service.GetAstraSceneArchitectSystemPrompt();
                string directorPrompt = service.GetAstraWorldDirectorSystemPrompt("test context");
                AssertTrue("Scene prompt instructs BUILD_ENVIRONMENT", scenePrompt.Contains("BUILD_ENVIRONMENT"));
                AssertTrue("Director prompt instructs DIRECT_SCENARIO", directorPrompt.Contains("DIRECT_SCENARIO"));
                AssertTrue("Director prompt instructs TRIGGER_WORLD_EVENT", directorPrompt.Contains("TRIGGER_WORLD_EVENT"));
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    try { Directory.Delete(testDir, true); } catch { }
                }
            }

            Console.WriteLine(string.Format("\nRESULTS: {0}/{1} tests passed.", passed, total));
            return passed == total ? 0 : 1;
        }
    }
}
