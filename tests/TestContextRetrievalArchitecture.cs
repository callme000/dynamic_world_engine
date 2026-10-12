using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;

namespace DynamicWorldEngine.Tests
{
    class TestContextRetrievalArchitecture
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
            Console.WriteLine("  CONTEXT RETRIEVAL ARCHITECTURE TEST SUITE (Law 1 & 4)   ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_ContextRetrieval_Data");
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
            Directory.CreateDirectory(testDir);

            try
            {
                var store = new WorldMemoryStore(testDir);
                var retriever = new ContextRetriever();

                // ========================================================
                // Populate a world with 20 NPCs across two factions
                // ========================================================

                // Cartel NPCs (5)
                var cartelBoss = store.CreateNpc("El Diablo", "Drug Lord", "Madrazo Cartel", "ig_mp_agent14", "Cartel Kingpin", "npc_diablo");
                var cartelLt = store.CreateNpc("Marcus Rivera", "Lieutenant", "Madrazo Cartel", "s_m_y_xmech_01", "Enforcer", "npc_marcus");
                var cartelSoldier1 = store.CreateNpc("Javier Morales", "Soldier", "Madrazo Cartel", "s_m_y_xmech_02", "None", "npc_javier");
                var cartelSoldier2 = store.CreateNpc("Carlos Fuentes", "Soldier", "Madrazo Cartel", "s_m_y_armymech_01", "None", "npc_carlos");
                var cartelCook = store.CreateNpc("Rosa Delgado", "Cook", "Madrazo Cartel", "a_f_y_topless_01", "Chemist", "npc_rosa");

                // Biker Gang NPCs (5)
                var bikerPres = store.CreateNpc("Iron Mike", "Club President", "Lost MC", "g_m_y_lost_01", "Arms Dealer", "npc_ironmike");
                var bikerVP = store.CreateNpc("Dusty Rhodes", "Vice President", "Lost MC", "g_m_y_lost_02", "None", "npc_dusty");
                var bikerProspect1 = store.CreateNpc("Snake Wilson", "Prospect", "Lost MC", "g_m_y_lost_03", "None", "npc_snake");
                var bikerProspect2 = store.CreateNpc("Tiny Baker", "Prospect", "Lost MC", "g_m_y_ballaeast_01", "Undercover Agent", "npc_tiny");
                var bikerMechanic = store.CreateNpc("Wrench", "Mechanic", "Lost MC", "g_m_y_ballaorig_01", "None", "npc_wrench");

                // Civilian NPCs (10)
                var civ1 = store.CreateNpc("Nancy Hernandez", "Bartender", "Civilian", "a_f_m_bevhills_01", "None", "npc_nancy");
                var civ2 = store.CreateNpc("Officer Davis", "Detective", "LSPD", "s_m_y_cop_01", "None", "npc_davis");
                var civ3 = store.CreateNpc("Maria Santos", "Waitress", "Civilian", "a_f_y_business_01", "None", "npc_maria");
                var civ4 = store.CreateNpc("Bobby Chen", "Street Vendor", "Civilian", "a_m_y_stbla_01", "None", "npc_bobby");
                var civ5 = store.CreateNpc("Lena Popov", "Nurse", "Civilian", "s_f_y_scrubs_01", "None", "npc_lena");
                var civ6 = store.CreateNpc("Rex Thompson", "Bouncer", "Civilian", "s_m_m_bouncer_01", "None", "npc_rex");
                var civ7 = store.CreateNpc("DJ Spark", "DJ", "Civilian", "u_m_y_party_01", "None", "npc_djspark");
                var civ8 = store.CreateNpc("Vince Moretti", "Loan Shark", "Civilian", "ig_claypain", "Money Launderer", "npc_vince");
                var civ9 = store.CreateNpc("Dr. Sarah Kim", "Surgeon", "Civilian", "s_f_y_scrubs_01", "None", "npc_sarahkim");
                var civ10 = store.CreateNpc("Frank Owens", "Taxi Driver", "Civilian", "a_m_m_soucent_03", "None", "npc_frank");

                // Set up relationships
                cartelBoss.Relationships["Player"] = -30;
                cartelBoss.Relationships["Marcus Rivera"] = 80;
                cartelLt.Relationships["Player"] = -50;
                cartelLt.Relationships["El Diablo"] = 90;
                cartelLt.Relationships["Rosa Delgado"] = 40;
                bikerPres.Relationships["Player"] = 10;
                bikerPres.Relationships["Dusty Rhodes"] = 70;
                civ2.Relationships["Player"] = 20;
                civ8.Relationships["Player"] = -40;
                civ8.Relationships["El Diablo"] = 60;

                // Create an environment with cartel NPCs
                var env = store.CreateEnvironment("env_safehouse", "El Diablo's Safehouse", "COMPOUND",
                    "A fortified compound in the Blaine County hills", 1500f, 3200f, 35f, 180f);
                env.NpcIds.Add(cartelBoss.NpcId);
                env.NpcIds.Add(cartelLt.NpcId);
                env.NpcIds.Add(cartelSoldier1.NpcId);
                env.NpcIds.Add(cartelSoldier2.NpcId);
                env.NpcIds.Add(cartelCook.NpcId);
                env.ResidentEntityIds.Add("npc_diablo");
                env.ResidentEntityIds.Add("npc_marcus");
                env.Status = "REALIZED";
                env.LastRealizedAt = DateTime.UtcNow;

                // Record some world events involving cartel
                store.RecordEventGraphEvent(cartelBoss.NpcId, "Player", "El Diablo", "THREAT", 0.8f,
                    "Player threatened El Diablo at the compound gate", "HOSTILE", null);
                store.RecordEventGraphEvent(cartelLt.NpcId, "Marcus Rivera", "Player", "ASSAULT", 0.9f,
                    "Marcus opened fire on the player after the threat", "AGGRESSIVE", null);
                // Unrelated event
                store.RecordEventGraphEvent(civ1.NpcId, "Nancy Hernandez", "Bobby Chen", "GOSSIP", 0.1f,
                    "Nancy told Bobby about a sale at the clothing store", "NEUTRAL", null);


                // ========================================================
                // TEST 1: Direct Entity Name Scoring
                // ========================================================
                Console.WriteLine("--- TEST 1: Direct Entity Name Scoring ---");
                {
                    var result = retriever.RetrieveScopedContext(store, "Bribe Marcus to betray El Diablo");
                    AssertTrue("Candidates include Marcus Rivera",
                        result.CandidateNpcs.Exists(n => n.Name == "Marcus Rivera"));
                    AssertTrue("Candidates include El Diablo",
                        result.CandidateNpcs.Exists(n => n.Name == "El Diablo"));
                    AssertTrue("Candidates do NOT include Frank Owens (random civilian)",
                        !result.CandidateNpcs.Exists(n => n.Name == "Frank Owens"));
                    AssertTrue("FilteredNpcsCount <= 4 (maxCandidates)",
                        result.FilteredNpcsCount <= 4);
                }

                // ========================================================
                // TEST 2: Role & Faction Scoring
                // ========================================================
                Console.WriteLine("\n--- TEST 2: Role & Faction Scoring ---");
                {
                    var result = retriever.RetrieveScopedContext(store, "Get the Cartel backup, call the soldiers");
                    bool hasCartel = result.CandidateNpcs.Exists(n => n.PublicFaction == "Madrazo Cartel");
                    AssertTrue("At least one Cartel member selected", hasCartel);

                    // Verify that bikers and random civilians are NOT picked
                    bool hasBiker = result.CandidateNpcs.Exists(n => n.PublicFaction == "Lost MC");
                    // Bikers shouldn't be in the top candidates for a "cartel" prompt
                    // (they might appear if they share env residency, but they don't here)
                    AssertTrue("Top candidates do NOT contain Lost MC bikers", !hasBiker);
                }

                // ========================================================
                // TEST 3: Relational Pruning
                // ========================================================
                Console.WriteLine("\n--- TEST 3: Relational Pruning ---");
                {
                    var result = retriever.RetrieveScopedContext(store, "Talk to El Diablo about his operation");
                    string ctx = result.FormattedContext;

                    // El Diablo's relationship with Marcus should appear (both are candidates)
                    // El Diablo's relationship with Player should appear
                    // But a random third-party relationship should NOT appear
                    AssertTrue("Context string is not empty", !string.IsNullOrEmpty(ctx));
                    AssertTrue("Context includes Player relationship reference",
                        ctx.Contains("Player"));
                    AssertTrue("Context contains El Diablo entity",
                        ctx.Contains("El Diablo"));
                }

                // ========================================================
                // TEST 4: Spatial Residency Fallback
                // ========================================================
                Console.WriteLine("\n--- TEST 4: Spatial Residency Fallback ---");
                {
                    // Ambient prompt with no specific names or roles
                    var result = retriever.RetrieveScopedContext(store, "Something strange is happening around here", "env_safehouse");
                    // Should fall back to environment residents since no name/role matches
                    bool hasResident = result.CandidateNpcs.Count > 0;
                    AssertTrue("Ambient prompt still produces candidates via env residency", hasResident);

                    if (result.ActiveEnvironment != null)
                    {
                        AssertTrue("Active environment is the safehouse",
                            result.ActiveEnvironment.EnvironmentId == "env_safehouse");
                    }
                    else
                    {
                        AssertTrue("Active environment should be resolved", false);
                    }
                }

                // ========================================================
                // TEST 5: Token Reduction Verification
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Token Reduction Verification ---");
                {
                    // Full world dump
                    string fullDump = "";
                    foreach (var npc in store.GetAllNpcs())
                    {
                        fullDump += store.FormatNpcContextForLivingLSAIs(npc.NpcId) + "\n";
                    }

                    // Scoped context
                    var result = retriever.RetrieveScopedContext(store, "Bribe Marcus to betray El Diablo");
                    string scoped = result.FormattedContext;

                    int fullLen = fullDump.Length;
                    int scopedLen = scoped.Length;

                    double reductionPercent = fullLen > 0 ? ((double)(fullLen - scopedLen) / fullLen) * 100.0 : 0;

                    Console.WriteLine(string.Format("    Full dump: {0} chars", fullLen));
                    Console.WriteLine(string.Format("    Scoped:    {0} chars", scopedLen));
                    Console.WriteLine(string.Format("    Reduction: {0:F1}%", reductionPercent));

                    AssertTrue("Scoped context is >60% smaller than full dump", reductionPercent > 60.0);
                    AssertTrue("Scoped context is non-empty", scopedLen > 10);
                }

                // ========================================================
                // TEST 6: Event Windowing (Relevant Events Only)
                // ========================================================
                Console.WriteLine("\n--- TEST 6: Event Windowing ---");
                {
                    var result = retriever.RetrieveScopedContext(store, "What happened with Marcus?");
                    // Marcus is NPC primary on the assault event, so that event should be relevant
                    bool hasRelevantEvent = result.RelevantEvents.Count > 0;
                    AssertTrue("Relevant events list is non-empty for named-NPC prompt", hasRelevantEvent);

                    // The gossip event between Nancy and Bobby should NOT be included
                    bool hasGossip = result.RelevantEvents.Exists(e =>
                        e.EventType == "GOSSIP" && e.Actor == "Nancy Hernandez");
                    AssertTrue("Irrelevant gossip event is excluded from windowed results", !hasGossip);
                }

                // ========================================================
                // TEST 7: Dead NPCs Are Excluded
                // ========================================================
                Console.WriteLine("\n--- TEST 7: Dead NPC Exclusion ---");
                {
                    cartelSoldier1.IsAlive = false;
                    var result = retriever.RetrieveScopedContext(store, "Where is Javier?");
                    bool hasJavier = result.CandidateNpcs.Exists(n => n.Name == "Javier Morales");
                    AssertTrue("Dead NPC Javier is NOT in candidates even when named", !hasJavier);
                    // Restore for subsequent tests
                    cartelSoldier1.IsAlive = true;
                }

                // ========================================================
                // TEST 8: Hidden Role / Secret Keyword Scoring
                // ========================================================
                Console.WriteLine("\n--- TEST 8: Hidden Role Scoring ---");
                {
                    var result = retriever.RetrieveScopedContext(store, "Find the undercover agent in the biker gang");
                    // Tiny Baker has HiddenRole = "Undercover Agent"
                    bool hasTiny = result.CandidateNpcs.Exists(n => n.Name == "Tiny Baker");
                    AssertTrue("Undercover Agent Tiny Baker selected via hidden role match", hasTiny);
                }

                // ========================================================
                // TEST 9: EngineCore Integration
                // ========================================================
                Console.WriteLine("\n--- TEST 9: EngineCore.GetScopedWorldFactSummary Integration ---");
                {
                    string engineDir = Path.Combine(testDir, "engine_integration");
                    Directory.CreateDirectory(engineDir);
                    var engine = new EngineCore(engineDir, null, null, null, null, null, null);
                    // Populate the engine's own store with a test NPC
                    engine.Store.CreateNpc("Marcus Test", "Lieutenant", "TestFaction", "s_m_y_xmech_01", "None", "npc_marcus_test");
                    string scopedCtx = engine.GetScopedWorldFactSummary("Talk to Marcus about the deal");
                    AssertTrue("EngineCore.GetScopedWorldFactSummary returns non-empty string",
                        !string.IsNullOrEmpty(scopedCtx));
                    AssertTrue("Scoped context mentions Marcus",
                        scopedCtx.Contains("Marcus"));
                }

                // ========================================================
                // TEST 10: Null/Edge Case Safety
                // ========================================================
                Console.WriteLine("\n--- TEST 10: Null Safety ---");
                {
                    var result1 = retriever.RetrieveScopedContext(null, "test prompt");
                    AssertTrue("Null store returns empty result", result1.CandidateNpcs.Count == 0);

                    var result2 = retriever.RetrieveScopedContext(store, null);
                    AssertTrue("Null prompt doesn't crash", result2 != null);

                    var result3 = retriever.RetrieveScopedContext(store, "");
                    AssertTrue("Empty prompt doesn't crash", result3 != null);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\n  [FATAL] Unhandled exception: {0}", ex));
            }
            finally
            {
                try
                {
                    if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_ContextRetrieval_Data")))
                    {
                        Directory.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_ContextRetrieval_Data"), true);
                    }
                }
                catch { }
            }

            Console.WriteLine(string.Format("\n=========================================================="));
            Console.WriteLine(string.Format("  RESULTS: {0}/{1} PASSED", passed, total));
            Console.WriteLine(string.Format("=========================================================="));

            return (passed == total) ? 0 : 1;
        }
    }
}
