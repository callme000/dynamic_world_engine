using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Services;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA.Math;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Tests
{
    public class TestUnifiedScenarioDirector
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  UNIFIED SCENE GENERATOR & SCENARIO DIRECTOR TEST SUITE ");
            Console.WriteLine("=========================================================\n");

            int passed = 0;
            int total = 3;

            // [Test 1] Schema & JSON Validation of Unified Scene + Director Beats
            Console.WriteLine("[Test 1] Testing Unified Scenario JSON Schema Parsing...");
            string sampleJson = @"{
              ""action"": ""BUILD_ENVIRONMENT"",
              ""data"": {
                ""environment_id"": ""env_hostage_deal"",
                ""name"": ""Warehouse Hostage Negotiation"",
                ""archetype"": ""criminal_den"",
                ""description"": ""A cartel boss holding an informant at gunpoint when SWAT raids the warehouse."",
                ""props"": [
                  { ""prop_id"": ""table_1"", ""model"": ""prop_table_03b"", ""offset"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 } },
                  { ""prop_id"": ""cash_1"", ""parent_prop_id"": ""table_1"", ""model"": ""prop_cash_pile_02"", ""offset"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.76 } }
                ],
                ""npcs"": [
                  {
                    ""entity_id"": ""boss_1"",
                    ""name"": ""Don Salvatore"",
                    ""occupation"": ""Cartel Boss"",
                    ""public_faction"": ""Cartel"",
                    ""hidden_role"": ""Cartel Kingpin"",
                    ""ped_model"": ""g_m_y_famca_01"",
                    ""weapon"": ""WEAPON_PISTOL"",
                    ""initial_task"": ""aim_at:informant_1"",
                    ""initial_state"": ""AGGRESSIVE""
                  },
                  {
                    ""entity_id"": ""informant_1"",
                    ""name"": ""Jimmy Valenti"",
                    ""occupation"": ""Accountant"",
                    ""public_faction"": ""Civilians"",
                    ""hidden_role"": ""FIB Informant"",
                    ""ped_model"": ""a_m_y_business_01"",
                    ""weapon"": """",
                    ""initial_task"": ""hostage_hands_up"",
                    ""initial_state"": ""PANIC""
                  }
                ],
                ""scenario_director"": {
                  ""scenario_title"": ""The Informant Trap"",
                  ""narrative_intro"": ""Don Salvatore has Jimmy pinned against the table, interrogating him about leaked files."",
                  ""beats"": [
                    {
                      ""beat_id"": ""beat_1"",
                      ""delay_seconds"": 0.0,
                      ""trigger"": ""timer"",
                      ""announcement"": ""SWAT cruisers skid to a halt outside the warehouse with sirens blazing!"",
                      ""world_event"": {
                        ""event_type"": ""POLICE_RAID"",
                        ""title"": ""SWAT Breach"",
                        ""description"": ""Tactical officers breach the warehouse entrance."",
                        ""vehicle_model"": ""POLICE"",
                        ""sirens"": true,
                        ""occupants"": [
                          { ""name"": ""Officer Davis"", ""ped_model"": ""s_m_y_cop_01"", ""weapon"": ""WEAPON_CARBINERIFLE"", ""role"": ""officer"", ""hostility_to_player"": ""attack"" }
                        ]
                      },
                      ""mutations"": [
                        { ""target_entity_id"": ""boss_1"", ""hostility"": ""hostile_attack"", ""give_weapon"": ""WEAPON_SMG"", ""narrative_reason"": ""Don Salvatore turns and opens fire on SWAT!"" },
                        { ""target_entity_id"": ""informant_1"", ""hostility"": ""flee_panic"", ""narrative_reason"": ""Jimmy dives under cover to escape the crossfire!"" }
                      ]
                    }
                  ]
                }
              }
            }";

            string healed = JsonRepairUtility.RepairAndExtractJson(sampleJson);
            ActionParser parser = new ActionParser();
            BuildEnvironmentPayload payload = parser.ParseBuildEnvironment(healed);

            if (payload.Data != null && payload.Data.ScenarioDirector != null && payload.Data.ScenarioDirector.Beats.Count == 1)
            {
                Console.WriteLine("  ✓ PASS: Successfully parsed Unified Scene + Director schema:");
                Console.WriteLine("       Title: " + payload.Data.ScenarioDirector.ScenarioTitle);
                Console.WriteLine("       Intro: " + payload.Data.ScenarioDirector.NarrativeIntro);
                Console.WriteLine("       Beats: " + payload.Data.ScenarioDirector.Beats.Count + " beat defined");
                passed++;
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Parsing failed.");
            }

            // [Test 2] Testing SpatialWorldPlanner Staged Choreography Cascade
            Console.WriteLine("\n[Test 2] Testing SpatialWorldPlanner Staged Tasks & Director Binding...");
            SpatialWorldPlanner planner = new SpatialWorldPlanner(new NavMeshBlockerManager(), new BlipManager(), new TeleportPocketManager(), new InteriorManager(new TeleportPocketManager()));
            SpatialExecutionPlan plan = planner.PlanEnvironment(payload.Data, new Vector3(100, 200, 30), 45.0f);

            if (plan.ResolvedNpcs.Count == 2 && 
                plan.ResolvedNpcs[0].InitialTask == "aim_at:informant_1" &&
                plan.ResolvedNpcs[1].InitialTask == "hostage_hands_up" &&
                plan.ScenarioDirector != null)
            {
                Console.WriteLine("  ✓ PASS: SpatialExecutionPlan successfully captured actor choreographies and director timeline:");
                Console.WriteLine("       Actor 1: " + plan.ResolvedNpcs[0].Name + " -> Task: " + plan.ResolvedNpcs[0].InitialTask + " (" + plan.ResolvedNpcs[0].InitialState + ")");
                Console.WriteLine("       Actor 2: " + plan.ResolvedNpcs[1].Name + " -> Task: " + plan.ResolvedNpcs[1].InitialTask + " (" + plan.ResolvedNpcs[1].InitialState + ")");
                passed++;
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Spatial plan did not capture choreographies.");
            }

            // [Test 3] Testing DirectorTimelineManager Event & Mutation Execution
            Console.WriteLine("\n[Test 3] Testing DirectorTimelineManager Beat Triggering & Mutations...");
            DirectorTimelineManager timeline = new DirectorTimelineManager();
            timeline.LoadScenario(plan.ScenarioDirector, plan.EnvironmentId, plan.CenterPosition, new List<ActiveNpcContext>());

            bool worldEventTriggered = false;
            int mutationsTriggered = 0;

            timeline.ProcessTimeline(null, 
                we => { worldEventTriggered = true; Console.WriteLine("       Fired World Event: " + we.Title + " (" + we.EventType + ")"); },
                mut => { mutationsTriggered++; Console.WriteLine("       Applied Mutation to: " + mut.TargetEntityId + " -> " + mut.Hostility + " (" + mut.NarrativeReason + ")"); }
            );

            if (worldEventTriggered && mutationsTriggered == 2)
            {
                Console.WriteLine("  ✓ PASS: Scenario Director successfully dispatched world events and NPC mutations on schedule!");
                passed++;
            }
            else
            {
                Console.WriteLine("  ✗ FAIL: Director beats did not execute.");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine(string.Format("  DIRECTOR TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total));
            Console.WriteLine("=========================================================");
        }
    }
}
