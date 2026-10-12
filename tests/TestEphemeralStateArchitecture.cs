using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Tests
{
    class TestEphemeralStateArchitecture
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
            Console.WriteLine("  EPHEMERAL STATE ARCHITECTURE TEST SUITE (Phase 5)      ");
            Console.WriteLine("==========================================================\n");

            string testDir = Path.Combine(Path.GetTempPath(), "dwe_ephemeral_test_" + Guid.NewGuid().ToString().Substring(0, 8));
            Directory.CreateDirectory(testDir);

            try
            {
                var recomputer = new EphemeralStateRecomputer();

                // ========================================================
                // TEST 1: Acute Panic & Trauma Decay (Laws 2 & 7)
                // ========================================================
                Console.WriteLine("--- TEST 1: Acute Panic & Trauma Temporal Decay ---");
                {
                    DateTime t0 = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

                    var npc = new NpcRecord
                    {
                        NpcId = 101,
                        Name = "CivBystander",
                        Fear = 0,
                        Trust = 0,
                        Respect = 0,
                        CurrentState = "CALM"
                    };

                    // Violent assault event occurred at t0
                    var assaultEvt = new EventRecord
                    {
                        EventId = 1,
                        NpcId = 101,
                        Actor = "Player",
                        Target = "CivBystander",
                        EventType = "ASSAULT",
                        Severity = 1.0f,
                        Description = "Player attacked bystander with blunt weapon",
                        Timestamp = t0
                    };
                    npc.History.Add(assaultEvt);

                    // Immediate evaluation at t0 (elapsed = 0s)
                    var pImmediate = recomputer.RecomputeNpcPosture(npc, t0);
                    AssertTrue("Immediate posture has high effective fear (>= 50)", pImmediate.EffectiveFear >= 50f);
                    AssertTrue("Immediate posture has recent trauma flag", pImmediate.HasRecentTrauma);
                    AssertTrue("Immediate state is ALERT or PANICKED", pImmediate.EffectiveState == "ALERT" || pImmediate.EffectiveState == "PANICKED");

                    // Evaluation at t0 + 90s (1 fear half-life)
                    DateTime t90 = t0.AddSeconds(90);
                    var p90 = recomputer.RecomputeNpcPosture(npc, t90);
                    AssertTrue("Posture fear decayed after 90s", p90.EffectiveFear < pImmediate.EffectiveFear);
                    AssertTrue("Trauma flag cleared after 90s (> 45s threshold)", !p90.HasRecentTrauma);

                    // Evaluation at t0 + 360s (4 fear half-lives)
                    DateTime t360 = t0.AddSeconds(360);
                    var p360 = recomputer.RecomputeNpcPosture(npc, t360);
                    AssertTrue("Posture fear decayed to near baseline (< 10)", p360.EffectiveFear < 10f);
                    AssertTrue("Effective state recovered to CALM", p360.EffectiveState == "CALM");
                }

                // ========================================================
                // TEST 2: Epistemic Suspicion & Trust Decay
                // ========================================================
                Console.WriteLine("\n--- TEST 2: Epistemic Suspicion & Trust Temporal Decay ---");
                {
                    DateTime t0 = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

                    var npc = new NpcRecord
                    {
                        NpcId = 202,
                        Name = "CorruptOfficial",
                        Trust = 10,
                        Fear = 0,
                        CurrentState = "CALM"
                    };

                    var secretEvt = new EventRecord
                    {
                        EventId = 2,
                        NpcId = 202,
                        Actor = "Player",
                        Target = "CorruptOfficial",
                        EventType = "EXPOSITION_SECRET",
                        Severity = 0.90f,
                        Description = "Player revealed evidence of offshore bribery",
                        Timestamp = t0
                    };
                    npc.History.Add(secretEvt);

                    var pImmediate = recomputer.RecomputeNpcPosture(npc, t0);
                    AssertTrue("Immediate suspicion level is high (>= 0.70)", pImmediate.SuspicionLevel >= 0.70f);
                    AssertTrue("Immediate effective trust shifted negative", pImmediate.EffectiveTrust < 0f);
                    AssertTrue("Immediate state is ALERT", pImmediate.EffectiveState == "ALERT");

                    // 15 minutes later (900s)
                    DateTime t900 = t0.AddSeconds(900);
                    var p900 = recomputer.RecomputeNpcPosture(npc, t900);
                    AssertTrue("Suspicion decayed significantly after 15m", p900.SuspicionLevel < 0.20f);
                }

                // ========================================================
                // TEST 3: Dynamic Location Heat & Danger Recomputation
                // ========================================================
                Console.WriteLine("\n--- TEST 3: Dynamic Location Heat & Danger Recomputation ---");
                {
                    DateTime t0 = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

                    var events = new List<EventRecord>
                    {
                        new EventRecord
                        {
                            EventId = 10,
                            EventType = "SHOOTING",
                            Severity = 0.9f,
                            PositionX = 100f,
                            PositionY = 200f,
                            PositionZ = 15f,
                            HasPosition = true,
                            Timestamp = t0.AddSeconds(-20)
                        },
                        new EventRecord
                        {
                            EventId = 11,
                            EventType = "EXPLOSION",
                            Severity = 1.0f,
                            PositionX = 110f,
                            PositionY = 205f,
                            PositionZ = 15f,
                            HasPosition = true,
                            Timestamp = t0.AddSeconds(-15)
                        }
                    };

                    // Epicenter: (105, 202, 15) within 10 meters of both events
                    var heatEpicenter = recomputer.RecomputeLocationHeat(105f, 202f, 15f, events, radiusMeters: 50f, asOf: t0);
                    AssertTrue("Epicenter danger score is high (>= 0.50)", heatEpicenter.DangerScore >= 0.50f);
                    AssertTrue("Epicenter has 2 violent events", heatEpicenter.RecentViolentEventCount == 2);
                    AssertTrue("Epicenter closest threat is very close (<= 15m)", heatEpicenter.DistanceToClosestThreat <= 15f);

                    // Distant location: (600, 600, 15) > 500 meters away
                    var heatDistant = recomputer.RecomputeLocationHeat(600f, 600f, 15f, events, radiusMeters: 50f, asOf: t0);
                    AssertTrue("Distant location danger score is 0", heatDistant.DangerScore == 0f);
                    AssertTrue("Distant location has 0 violent events", heatDistant.RecentViolentEventCount == 0);
                    AssertTrue("Distant location summary is peaceful", heatDistant.Summary == "Peaceful area");

                    // Temporal decay of epicenter: 12 minutes later (720 seconds > 600s window)
                    DateTime t720 = t0.AddSeconds(720);
                    var heatExpired = recomputer.RecomputeLocationHeat(105f, 202f, 15f, events, radiusMeters: 50f, asOf: t720);
                    AssertTrue("Expired events drop danger score to 0", heatExpired.DangerScore == 0f);
                }

                // ========================================================
                // TEST 4: Cross-Faction Hostility & War Status Derivation
                // ========================================================
                Console.WriteLine("\n--- TEST 4: Cross-Faction Hostility & War Status Derivation ---");
                {
                    DateTime t0 = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

                    var gangEvents = new List<EventRecord>
                    {
                        new EventRecord
                        {
                            EventId = 20,
                            Actor = "Ballas",
                            Target = "Families",
                            EventType = "DRIVEBY_SHOOTING",
                            Severity = 0.95f,
                            Timestamp = t0.AddSeconds(-120)
                        },
                        new EventRecord
                        {
                            EventId = 21,
                            Actor = "Families",
                            Target = "Ballas",
                            EventType = "FACTION_RAID",
                            Severity = 0.90f,
                            Timestamp = t0.AddSeconds(-60)
                        },
                        new EventRecord
                        {
                            EventId = 22,
                            Actor = "Ballas",
                            Target = "Families",
                            EventType = "FATAL_RETALIATION",
                            Severity = 1.0f,
                            Timestamp = t0.AddSeconds(-10)
                        }
                    };

                    var tension = recomputer.RecomputeFactionHostility("Ballas", "Families", gangEvents, t0);
                    AssertTrue("Faction tension report is not null", tension != null);
                    AssertTrue("Recent clash count is 3", tension.RecentClashCount == 3);
                    AssertTrue("Hostility score is high (>= 0.70)", tension.HostilityScore >= 0.70f);
                    AssertTrue("Factions are declared at active war", tension.IsAtWar);
                    AssertTrue("Status is ACTIVE_WAR", tension.Status == "ACTIVE_WAR");

                    // Unrelated factions (e.g. Vagos vs Police with no events)
                    var peaceful = recomputer.RecomputeFactionHostility("Vagos", "Police", gangEvents, t0);
                    AssertTrue("Unrelated factions have 0 hostility", peaceful.HostilityScore == 0f);
                    AssertTrue("Unrelated factions are peaceful", peaceful.Status == "PEACEFUL");
                }

                // ========================================================
                // TEST 5: History Compaction (Law 7: Bounded Replay)
                // ========================================================
                Console.WriteLine("\n--- TEST 5: Event History Compaction (Law 7) ---");
                {
                    DateTime baseTime = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
                    var history = new List<EventRecord>();

                    // 1 Critical death event
                    history.Add(new EventRecord { EventId = 1, EventType = "NPC_DEATH", Severity = 1.0f, Timestamp = baseTime.AddMinutes(1) });

                    // 1 Critical secret event
                    history.Add(new EventRecord { EventId = 2, EventType = "EXPOSITION_SECRET", Severity = 0.9f, Timestamp = baseTime.AddMinutes(2) });

                    // 1 Critical betrayal
                    history.Add(new EventRecord { EventId = 3, EventType = "BETRAYAL", Severity = 0.85f, Timestamp = baseTime.AddMinutes(3) });

                    // 1 Police alert
                    history.Add(new EventRecord { EventId = 4, EventType = "POLICE_ALERT", Severity = 0.8f, Timestamp = baseTime.AddMinutes(4) });

                    // 16 Ambient chatter and wandering events
                    for (int i = 5; i <= 20; i++)
                    {
                        history.Add(new EventRecord
                        {
                            EventId = i,
                            EventType = (i % 2 == 0) ? "CHAT_IDLE" : "WANDER_PATROL",
                            Severity = 0.1f,
                            Timestamp = baseTime.AddMinutes(i)
                        });
                    }

                    AssertTrue("Original history count is 20", history.Count == 20);

                    // Compact to bounded window of 6 events
                    var compacted = recomputer.CompactEventHistory(history, maxRetained: 6);
                    AssertTrue("Compacted history count is exactly 6", compacted.Count == 6);

                    // Check that all 4 high-salience milestones survived compaction
                    AssertTrue("NPC_DEATH milestone survived compaction", compacted.Exists(e => e.EventType == "NPC_DEATH"));
                    AssertTrue("EXPOSITION_SECRET milestone survived compaction", compacted.Exists(e => e.EventType == "EXPOSITION_SECRET"));
                    AssertTrue("BETRAYAL milestone survived compaction", compacted.Exists(e => e.EventType == "BETRAYAL"));
                    AssertTrue("POLICE_ALERT milestone survived compaction", compacted.Exists(e => e.EventType == "POLICE_ALERT"));

                    // Verify chronological order was preserved
                    bool inOrder = true;
                    for (int i = 1; i < compacted.Count; i++)
                    {
                        if (compacted[i].Timestamp < compacted[i - 1].Timestamp)
                        {
                            inOrder = false;
                            break;
                        }
                    }
                    AssertTrue("Compacted history preserved chronological ordering", inOrder);
                }

                // ========================================================
                // TEST 6: EngineCore & WorldSimulationEngine Integration
                // ========================================================
                Console.WriteLine("\n--- TEST 6: EngineCore & Simulation Integration ---");
                {
                    var engine = new DynamicWorldEngine.EngineCore(testDir);
                    AssertTrue("EngineCore Recomputer is exposed", engine.Recomputer != null);

                    var npc = new NpcRecord
                    {
                        NpcId = 601,
                        Name = "StoreNpc",
                        Fear = 10,
                        Trust = 20,
                        CurrentState = "CALM"
                    };
                    engine.Store.EnrollNpc(npc);

                    var posture = engine.GetEffectiveNpcPosture(601);
                    AssertTrue("GetEffectiveNpcPosture returns valid posture", posture != null);
                    AssertTrue("Effective posture matches NPC id", posture.NpcId == 601);
                    AssertTrue("Initial effective state is CALM", posture.EffectiveState == "CALM");

                    // Query location heat via EngineCore
                    var heat = engine.GetLocationHeat(0f, 0f, 72f);
                    AssertTrue("GetLocationHeat returns valid assessment", heat != null);
                    AssertTrue("Empty world has zero danger score", heat.DangerScore == 0f);

                    // Query faction hostility via EngineCore
                    var faction = engine.GetFactionHostility("Cops", "Robbers");
                    AssertTrue("GetFactionHostility returns valid report", faction != null);
                    AssertTrue("Empty world has peaceful faction status", faction.Status == "PEACEFUL");

                    // Seed bloated history and compact via simulation
                    for (int i = 0; i < 20; i++)
                    {
                        npc.History.Add(new EventRecord
                        {
                            EventId = 100 + i,
                            EventType = "CHAT_IDLE",
                            Severity = 0.1f,
                            Timestamp = DateTime.UtcNow.AddMinutes(-i)
                        });
                    }
                    AssertTrue("Npc history bloated to > 15 events", npc.History.Count > 15);

                    engine.Simulation.CompactNpcHistories(maxRetained: 10);
                    AssertTrue("CompactNpcHistories compacted history to <= 10", npc.History.Count <= 10);
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
