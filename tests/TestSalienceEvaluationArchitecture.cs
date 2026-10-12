using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Tests
{
    class TestSalienceEvaluationArchitecture
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
            Console.WriteLine("  SALIENCE EVALUATION ARCHITECTURE TEST SUITE (Phase 4)  ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(Path.GetTempPath(), "dwe_salience_test_" + Guid.NewGuid().ToString().Substring(0, 8));
            Directory.CreateDirectory(testDir);

            try
            {
                var evaluator = new SalienceEvaluator(threshold: 0.70f, cooldownSeconds: 2.0);

                // ========================================================
                // TEST 1: Mortality Event Cognitive Triggering
                // ========================================================
                Console.WriteLine("--- TEST 1: Mortality Event Salience (Death) ---");
                {
                    var deathEvt = new EventRecord
                    {
                        NpcId = 101,
                        Actor = "Player",
                        Target = "Target_NPC",
                        EventType = "NPC_DEATH",
                        Severity = 1.0f,
                        Description = "Target_NPC died of fatal gunshot wounds.",
                        SentimentTag = "Fatal_Mortality"
                    };

                    var assessment = evaluator.EvaluateEvent(deathEvt);
                    AssertTrue("Mortality assessment is not null", assessment != null);
                    AssertTrue("Mortality salience score is high (>= 0.85)", assessment.SalienceScore >= 0.85f);
                    AssertTrue("Mortality event triggers cognitive wake-up", assessment.IsCognitiveTrigger);
                    AssertTrue("Mortality factors include mortality transition", assessment.SalienceFactors.Exists(f => f.Contains("Mortality")));
                    AssertTrue("Mortality factors include player involvement", assessment.SalienceFactors.Exists(f => f.Contains("Player")));

                    evaluator.ClearCooldowns();
                }

                // ========================================================
                // TEST 2: Epistemic Secret Disclosure Salience
                // ========================================================
                Console.WriteLine("\n--- TEST 2: Epistemic Secret Disclosure Salience ---");
                {
                    var npc = new NpcRecord
                    {
                        NpcId = 202,
                        Name = "CorruptAgent",
                        HiddenRole = "FIB_Undercover",
                        Secrets = new List<string> { "Embezzling cartel wire transfers", "Undercover agent file #44" }
                    };

                    var secretEvt = new EventRecord
                    {
                        NpcId = 202,
                        Actor = "Player",
                        Target = "CorruptAgent",
                        EventType = "EXPOSITION_SECRET",
                        Severity = 0.90f,
                        Description = "Player confronted CorruptAgent with stolen wire transfers.",
                        SentimentTag = "Accusation"
                    };

                    var assessment = evaluator.EvaluateEvent(secretEvt, npc);
                    AssertTrue("Secret disclosure assessment is not null", assessment != null);
                    AssertTrue("Secret disclosure salience score >= 0.80", assessment.SalienceScore >= 0.80f);
                    AssertTrue("Secret disclosure triggers cognitive wake-up", assessment.IsCognitiveTrigger);
                    AssertTrue("Secret factors include epistemic disclosure", assessment.SalienceFactors.Exists(f => f.Contains("Epistemic")));
                    AssertTrue("Secret factors include hidden role bonus", assessment.SalienceFactors.Exists(f => f.Contains("Hidden Role")));
                    AssertTrue("Secret factors include active secrets bonus", assessment.SalienceFactors.Exists(f => f.Contains("active secrets")));

                    evaluator.ClearCooldowns();
                }

                // ========================================================
                // TEST 3: Ambient Noise Rejection (Cheap Local State Machine)
                // ========================================================
                Console.WriteLine("\n--- TEST 3: Ambient Noise Rejection ($0, 0ms Local Simulation) ---");
                {
                    var idleEvt = new EventRecord
                    {
                        NpcId = 303,
                        Actor = "NPC_303",
                        Target = "NPC_304",
                        EventType = "CHAT_IDLE",
                        Severity = 0.15f,
                        Description = "NPC_303 made small talk about the weather.",
                        SentimentTag = "neutral"
                    };

                    var assessment = evaluator.EvaluateEvent(idleEvt);
                    AssertTrue("Idle chat salience score is low (< 0.40)", assessment.SalienceScore < 0.40f);
                    AssertTrue("Idle chat is suppressed (NOT cognitive trigger)", !assessment.IsCognitiveTrigger);
                    AssertTrue("Reason mentions cheap local state machine", assessment.TriggerReason.Contains("cheap local state machine"));

                    var wanderEvt = new EventRecord
                    {
                        NpcId = 303,
                        Actor = "NPC_303",
                        Target = "Street",
                        EventType = "WANDER_PATROL",
                        Severity = 0.10f,
                        Description = "NPC_303 strolled down the sidewalk.",
                        SentimentTag = "neutral"
                    };

                    var wanderAssessment = evaluator.EvaluateEvent(wanderEvt);
                    AssertTrue("Wander event salience < 0.20", wanderAssessment.SalienceScore < 0.20f);
                    AssertTrue("Wander event suppressed from LLM trigger", !wanderAssessment.IsCognitiveTrigger);

                    evaluator.ClearCooldowns();
                }

                // ========================================================
                // TEST 4: Proximity Attenuation & Spatial Attenuation
                // ========================================================
                Console.WriteLine("\n--- TEST 4: Proximity Attenuation ---");
                {
                    var combatEvt = new EventRecord
                    {
                        NpcId = 404,
                        Actor = "Enemy_Thug",
                        Target = "Civilian",
                        EventType = "GUN_AIM",
                        Severity = 0.70f,
                        Description = "Thug aimed firearm at civilian bystander.",
                        SentimentTag = "Threat"
                    };

                    // Close proximity (10 meters)
                    var closeAssessment = evaluator.EvaluateEvent(combatEvt, null, distanceToPlayer: 10.0f);
                    evaluator.ClearCooldowns();

                    // Distant proximity (85 meters)
                    var distantAssessment = evaluator.EvaluateEvent(combatEvt, null, distanceToPlayer: 85.0f);
                    evaluator.ClearCooldowns();

                    AssertTrue("Close event salience > distant event salience", closeAssessment.SalienceScore > distantAssessment.SalienceScore);
                    AssertTrue("Close event contains proximity bonus (+0.10)", closeAssessment.SalienceFactors.Exists(f => f.Contains("Close Proximity")));
                    AssertTrue("Distant event contains attenuation penalty (-0.25)", distantAssessment.SalienceFactors.Exists(f => f.Contains("Distant Event")));
                    AssertTrue("Distant combat suppressed from triggering LLM", !distantAssessment.IsCognitiveTrigger);
                }

                // ========================================================
                // TEST 5: Anti-Oscillation Cooldown Enforcement
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Anti-Oscillation Cooldown Enforcement ---");
                {
                    evaluator.ClearCooldowns();

                    var assaultEvt = new EventRecord
                    {
                        NpcId = 505,
                        Actor = "Player",
                        Target = "Guard",
                        EventType = "ASSAULT",
                        Severity = 0.95f,
                        Description = "Player struck guard in melee combat.",
                        SentimentTag = "Combat"
                    };

                    // First trigger: should fire
                    var firstAssessment = evaluator.EvaluateEvent(assaultEvt);
                    AssertTrue("First high-salience event fires cognitive trigger", firstAssessment.IsCognitiveTrigger);

                    // Rapid repeated trigger within 2s cooldown: should be suppressed
                    var rapidAssessment = evaluator.EvaluateEvent(assaultEvt);
                    AssertTrue("Rapid repeated event within cooldown is suppressed", !rapidAssessment.IsCognitiveTrigger);
                    AssertTrue("Suppression reason indicates cooldown", rapidAssessment.TriggerReason.Contains("cooldown"));

                    // Clear cooldowns: should immediately fire again
                    evaluator.ClearCooldowns();
                    var resetAssessment = evaluator.EvaluateEvent(assaultEvt);
                    AssertTrue("After clearing cooldown, trigger fires again", resetAssessment.IsCognitiveTrigger);

                    evaluator.ClearCooldowns();
                }

                // ========================================================
                // TEST 6: WorldSimulationEngine Cognitive Emergence Event
                // ========================================================
                Console.WriteLine("\n--- TEST 6: WorldSimulationEngine Event Emergence ---");
                {
                    var store = new WorldMemoryStore(testDir);
                    var bridge = new DynamicWorldEngine.Bridge.LivingLsAiBridge(store);
                    var sim = new WorldSimulationEngine(store, bridge);

                    // Seed an informant NPC
                    var informant = new NpcRecord
                    {
                        NpcId = 601,
                        Name = "SecretInformant",
                        HiddenRole = "Police_Informant",
                        Secrets = new List<string> { "Informing on local warehouse syndicate" },
                        IsAlive = true,
                        PublicFaction = "Bystander"
                    };
                    store.EnrollNpc(informant);

                    int triggersFired = 0;
                    SalienceAssessment lastAssessment = null;
                    EventRecord lastEvent = null;

                    sim.OnCognitiveTriggerFired += (assessment, evt) =>
                    {
                        triggersFired++;
                        lastAssessment = assessment;
                        lastEvent = evt;
                    };

                    // Direct interaction with secret revelation (accusing informant)
                    sim.ProcessInteraction(601, "Player", "EXPOSITION_SECRET", 0.90f, "Player tells informant they know about the syndicate leak", "Secret");

                    AssertTrue("Interaction fired OnCognitiveTriggerFired", triggersFired > 0);
                    AssertTrue("Last assessment is cognitive trigger", lastAssessment != null && lastAssessment.IsCognitiveTrigger);
                    AssertTrue("Recorded event target matches informant name", lastEvent != null && lastEvent.Target == "SecretInformant");

                    // Verify autonomous world tick emergence: informant goal is now ALERT_POLICE_CONTACT
                    var currentInformant = store.GetNpcById(601);
                    AssertTrue("Informant set goal to ALERT_POLICE_CONTACT", currentInformant.ActiveGoal != null && currentInformant.ActiveGoal.GoalType == "ALERT_POLICE_CONTACT");

                    int triggersBeforeTick = triggersFired;
                    sim.Salience.ClearCooldowns();

                    var generatedEvents = sim.PerformAutonomousWorldTick();
                    AssertTrue("Autonomous tick generated emergence events", generatedEvents.Count > 0);
                    AssertTrue("Emergence event triggered cognitive wake-up", triggersFired > triggersBeforeTick);
                    AssertTrue("Last event was POLICE_ALERT", lastEvent != null && lastEvent.EventType == "POLICE_ALERT");
                }

                // ========================================================
                // TEST 7: EngineCore Integration & Mortality Notification
                // ========================================================
                Console.WriteLine("\n--- TEST 7: EngineCore Salience Integration ---");
                {
                    var engine = new DynamicWorldEngine.EngineCore(testDir);
                    AssertTrue("EngineCore Salience property is exposed", engine.Salience != null);

                    // Seed an NPC
                    var victim = new NpcRecord
                    {
                        NpcId = 701,
                        Name = "VictimNPC",
                        IsAlive = true
                    };
                    engine.Store.EnrollNpc(victim);

                    int deathTriggers = 0;
                    engine.Simulation.OnCognitiveTriggerFired += (assessment, evt) =>
                    {
                        if (assessment.EventType.Contains("DIED") || assessment.EventType.Contains("DEATH"))
                        {
                            deathTriggers++;
                        }
                    };

                    var deathEvt = engine.Simulation.RecordNpcDeath(701, "Player", "Direct hit from sniper rifle", distanceToPlayer: 5.0f);
                    AssertTrue("RecordNpcDeath returns event record", deathEvt != null);
                    AssertTrue("Victim is marked dead in store", !engine.Store.GetNpcById(701).IsAlive);
                    AssertTrue("Mortality triggered cognitive trigger via EngineCore", deathTriggers == 1);

                    // Direct evaluation helper on EngineCore
                    var directAssessment = engine.EvaluateEventSalience(deathEvt, victim, 5.0f);
                    AssertTrue("EvaluateEventSalience helper works on EngineCore", directAssessment != null && directAssessment.SalienceScore >= 0.70f);
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
