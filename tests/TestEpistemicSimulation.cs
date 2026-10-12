using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Tests
{
    class EpistemicSimulationTester
    {
        static int Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  EPISTEMIC ONTOLOGY & INDIRECT CAUSALITY TEST SUITE     ");
            Console.WriteLine("=========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Epistemic_Data");
            if (Directory.Exists(testDir)) Directory.Delete(testDir, true);
            Directory.CreateDirectory(testDir);

            EngineCore engine = new EngineCore(testDir);
            int passed = 0;
            int total = 0;

            // --- TEST 1: MULTI-AGENT SETUP (MARCO, BORIS, OFFICER DAVIS) ---
            total++;
            Console.WriteLine("[Test 1] Creating multi-agent scenario with distinct secrets and roles...");
            NpcRecord marco = engine.Store.CreateNpc("Marco Bellini", "Nightclub Owner", "Cartel", "a_m_y_business_01", "Police Informant");
            NpcRecord boris = engine.Store.CreateNpc("Boris 'The Anvil'", "Cartel Enforcer", "Cartel", "g_m_m_armboss_01", "Cartel Loyal Hitman");
            NpcRecord officer = engine.Store.CreateNpc("Officer Davis", "Detective", "Police", "s_m_y_cop_01", "LSPD Narcotics Detective");

            if (marco.Secrets.Contains("Police Informant") && boris.Secrets.Contains("Cartel Loyal Hitman") && officer.PublicFaction == "Police")
            {
                Console.WriteLine("  ✅ PASS: Multi-agent entities initialized with distinct secrets and factions.");
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Entity initialization failed.");
            }

            // --- TEST 2: SEMANTIC INTENT INTERPRETATION ---
            total++;
            Console.WriteLine("\n[Test 2] Testing Semantic Intent Interpreter (Touching Marco's Secret)...");
            string playerStatement = "I know you're a police informant, Marco.";
            SemanticIntent intent = IntentInterpreter.Interpret(playerStatement, marco);

            if (intent.Type == SemanticIntentType.SecretExposition && intent.SecretMatched == "Police Informant")
            {
                Console.WriteLine("  ✅ PASS: Intent correctly interpreted as 'SecretExposition' (Confidence: {0:F2}, Topic: '{1}')", intent.Confidence, intent.ExtractedTopic);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Intent interpretation failed. Type={0}", intent.Type);
            }

            // --- TEST 3: EVENT PROPAGATION WITH WITNESSES (DIRECT SIGHT VS AUDITORY) ---
            total++;
            Console.WriteLine("\n[Test 3] Simulating Player confronting Marco with Boris (DirectSight) and Officer (Auditory) nearby...");
            List<WitnessRecord> witnesses = new List<WitnessRecord>
            {
                new WitnessRecord(boris.NpcId, PerceptionType.DirectSight, 1.0f, 4.5f),
                new WitnessRecord(officer.NpcId, PerceptionType.AuditoryOnly, 0.6f, 22.0f)
            };

            NpcRecord updatedMarco = engine.Simulation.ProcessInteractionWithWitnesses(
                marco.NpcId,
                "Player",
                intent.Type.ToString().ToUpperInvariant(),
                intent.EmotionalImpact,
                "Player exposed Marco's police informant identity directly",
                "Secret_Exposition",
                witnesses
            );

            NpcRecord updatedBoris = engine.Store.GetNpcById(boris.NpcId);
            NpcRecord updatedOfficer = engine.Store.GetNpcById(officer.NpcId);

            bool marcoOk = updatedMarco.Fear >= 40 && updatedMarco.ActiveGoal.GoalType == "ALERT_POLICE_CONTACT";
            bool borisOk = updatedBoris.Knowledge.Count > 0 && updatedBoris.Beliefs.Exists(b => b.Predicate == "IS_HOSTILE_TO");
            bool officerOk = updatedOfficer.Beliefs.Exists(b => b.Predicate == "HEARD_COMMOTION_NEAR");

            if (marcoOk && borisOk && officerOk)
            {
                Console.WriteLine("  ✅ PASS: Epistemic state propagated accurately across all 3 agents:");
                Console.WriteLine("     • Marco Goal: {0} (Fear: {1}%, Trust: {2})", updatedMarco.ActiveGoal.GoalType, updatedMarco.Fear, updatedMarco.Trust);
                Console.WriteLine("     • Boris Knowledge: \"{0}\"", updatedBoris.Knowledge[0].Description);
                Console.WriteLine("     • Officer Belief: \"{0} {1}\"", updatedOfficer.Beliefs[0].Subject, updatedOfficer.Beliefs[0].Predicate);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Witness propagation failed. MarcoOk={0}, BorisOk={1}, OfficerOk={2}", marcoOk, borisOk, officerOk);
            }

            // --- TEST 4: AUTONOMOUS SIMULATION TICK & INDIRECT CAUSALITY ---
            total++;
            Console.WriteLine("\n[Test 4] Executing Autonomous Simulation Tick (Marco acts on goal without Player intervention)...");
            var autoEvents = engine.Simulation.PerformAutonomousWorldTick();

            NpcRecord copAfterTick = engine.Store.GetNpcById(officer.NpcId);
            bool indirectInvestigationTriggered = autoEvents.Exists(e => e.EventType == "POLICE_ALERT") &&
                                                   copAfterTick.ActiveGoal.GoalType == "INVESTIGATE_PLAYER" &&
                                                   copAfterTick.CurrentState == "ALERT";

            if (indirectInvestigationTriggered)
            {
                Console.WriteLine("  ✅ PASS: Indirect causality loop emerged autonomously!");
                Console.WriteLine("     • Generated World Event: \"{0}\"", autoEvents[0].Description);
                Console.WriteLine("     • Officer Davis Goal transitioned to: {0} ({1})", copAfterTick.ActiveGoal.GoalType, copAfterTick.ActiveGoal.Description);
                Console.WriteLine("     • Officer Davis State: {0}", copAfterTick.CurrentState);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Autonomous indirect causality failed.");
            }

            // --- TEST 5: LIVINGLSAIS FACT OUTPUT VERIFICATION ---
            total++;
            Console.WriteLine("\n[Test 5] Verifying LivingLSAIs epistemic fact serialization...");
            string marcoFact = engine.GetNpcFactForLivingLSAIs(marco.NpcId);
            string copFact = engine.GetNpcFactForLivingLSAIs(officer.NpcId);

            bool factsOk = marcoFact.Contains("Goal=ALERT_POLICE_CONTACT") && copFact.Contains("Goal=INVESTIGATE_PLAYER");
            if (factsOk)
            {
                Console.WriteLine("  ✅ PASS: LivingLSAIs facts reflect full epistemic agency:\n     Marco: {0}\n     Cop: {1}", marcoFact, copFact);
                passed++;
            }
            else
            {
                Console.WriteLine("  ❌ FAIL: Fact formatting missing epistemic goals.");
            }

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  EPISTEMIC TEST SUMMARY: {0}/{1} TESTS PASSED", passed, total);
            Console.WriteLine("=========================================================");

            return (passed == total) ? 0 : 1;
        }
    }
}
