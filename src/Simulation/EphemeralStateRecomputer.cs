using System;
using System.Collections.Generic;
using System.Linq;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Simulation
{
    public class EphemeralNpcPosture
    {
        public int NpcId { get; set; }
        public float EffectiveFear { get; set; }       // 0.0 to 100.0
        public float EffectiveTrust { get; set; }      // -100.0 to +100.0
        public float EffectiveRespect { get; set; }    // -100.0 to +100.0
        public string EffectiveState { get; set; }     // "CALM", "ALERT", "PANICKED", "HOSTILE", "SUBMISSIVE", "DEAD"
        public float SuspicionLevel { get; set; }      // 0.0 to 1.0
        public bool HasRecentTrauma { get; set; }
        public string DominantRecentEventType { get; set; }
        public int BoundedEventCount { get; set; }
        public DateTime RecomputedAt { get; set; }

        public EphemeralNpcPosture()
        {
            EffectiveState = "CALM";
            RecomputedAt = DateTime.UtcNow;
            DominantRecentEventType = "NONE";
        }

        public override string ToString()
        {
            return string.Format("[Posture NPC_{0}] State={1}, Fear={2:F1}, Trust={3:F1}, Suspicion={4:F2}, Trauma={5}",
                NpcId, EffectiveState, EffectiveFear, EffectiveTrust, SuspicionLevel, HasRecentTrauma);
        }
    }

    public class LocationHeatAssessment
    {
        public float DangerScore { get; set; }          // 0.0 (peaceful) to 1.0 (extreme active combat zone)
        public int RecentViolentEventCount { get; set; }
        public bool ActivePoliceAlert { get; set; }
        public string DominantThreat { get; set; }
        public float DistanceToClosestThreat { get; set; }
        public DateTime RecomputedAt { get; set; }
        public string Summary { get; set; }

        public LocationHeatAssessment()
        {
            DominantThreat = "None";
            RecomputedAt = DateTime.UtcNow;
            Summary = "Peaceful area";
            DistanceToClosestThreat = float.MaxValue;
        }

        public override string ToString()
        {
            return string.Format("[Location Heat {0:F2}] {1} (Violent Events: {2}, Police Alert: {3})",
                DangerScore, Summary, RecentViolentEventCount, ActivePoliceAlert);
        }
    }

    public class FactionTensionReport
    {
        public string FactionA { get; set; }
        public string FactionB { get; set; }
        public float HostilityScore { get; set; }       // 0.0 to 1.0
        public int RecentClashCount { get; set; }
        public bool IsAtWar { get; set; }
        public string Status { get; set; }              // "PEACEFUL", "TENSE", "HOSTILE", "ACTIVE_WAR"
        public DateTime RecomputedAt { get; set; }
        public string Summary { get; set; }

        public FactionTensionReport()
        {
            Status = "PEACEFUL";
            RecomputedAt = DateTime.UtcNow;
            Summary = "No active tension";
        }

        public override string ToString()
        {
            return string.Format("[Faction Tension: {0} vs {1}] {2} ({3:F2}, Clashes: {4}, War: {5})",
                FactionA, FactionB, Status, HostilityScore, RecentClashCount, IsAtWar);
        }
    }

    /// <summary>
    /// Bounded Replay & Ephemeral State Recomputer (Laws 2 & 7).
    /// Intermediate state should be recomputed, not persisted forever.
    /// Bounded replay of the recent event stream beats infinite state growth.
    /// </summary>
    public class EphemeralStateRecomputer
    {
        public const double PanicHalfLifeSeconds = 30.0;
        public const double FearHalfLifeSeconds = 90.0;
        public const double HostilityHalfLifeSeconds = 180.0;
        public const double TrustHalfLifeSeconds = 600.0;
        public const double SuspicionHalfLifeSeconds = 300.0;
        public const int DefaultBoundedWindowSize = 15;

        public static float ComputeDecayFactor(double elapsedSeconds, double halfLifeSeconds)
        {
            if (elapsedSeconds <= 0.0) return 1.0f;
            if (halfLifeSeconds <= 0.0) return 0.0f;
            return (float)Math.Exp(-0.69314718056 * (elapsedSeconds / halfLifeSeconds));
        }

        /// <summary>
        /// Recomputes an NPC's effective operational posture by replaying recent bounded events
        /// with exponential half-life decay on top of baseline traits.
        /// </summary>
        public EphemeralNpcPosture RecomputeNpcPosture(NpcRecord npc, DateTime? asOf = null)
        {
            if (npc == null) return null;

            DateTime now = asOf ?? DateTime.UtcNow;
            var posture = new EphemeralNpcPosture
            {
                NpcId = npc.NpcId,
                RecomputedAt = now
            };

            // 1. Mortality is terminal and irreversible
            if (!npc.IsAlive || string.Equals(npc.CurrentState, "DEAD", StringComparison.OrdinalIgnoreCase))
            {
                posture.EffectiveState = "DEAD";
                posture.EffectiveFear = 0f;
                posture.EffectiveTrust = 0f;
                posture.EffectiveRespect = 0f;
                posture.SuspicionLevel = 0f;
                posture.HasRecentTrauma = false;
                posture.DominantRecentEventType = "DEATH";
                return posture;
            }

            // 2. Start from baseline personality traits
            float fearAcc = npc.Fear;
            float trustAcc = npc.Trust;
            float respectAcc = npc.Respect;
            float suspicionAcc = 0.0f;
            float maxSeverity = 0f;
            string dominantType = "NONE";

            // 3. Replay bounded event slice with temporal decay
            var history = npc.History;
            int count = 0;
            if (history != null && history.Count > 0)
            {
                // Take bounded window (up to DefaultBoundedWindowSize recent events)
                var boundedSlice = history.OrderByDescending(e => e.Timestamp).Take(DefaultBoundedWindowSize).ToList();
                count = boundedSlice.Count;

                foreach (var evt in boundedSlice)
                {
                    double elapsed = Math.Max(0.0, (now - evt.Timestamp).TotalSeconds);
                    string type = (evt.EventType ?? "").ToUpperInvariant();

                    if (evt.Severity > maxSeverity)
                    {
                        maxSeverity = evt.Severity;
                        dominantType = evt.EventType;
                    }

                    if (type.Contains("ASSAULT") || type.Contains("DAMAGE") || type.Contains("SHOOT") || type.Contains("SHOT_NEARBY") || type.Contains("GUN_AIM"))
                    {
                        float decayPanic = ComputeDecayFactor(elapsed, PanicHalfLifeSeconds);
                        float decayFear = ComputeDecayFactor(elapsed, FearHalfLifeSeconds);

                        fearAcc += (evt.Severity * 60f) * decayFear;
                        trustAcc -= (evt.Severity * 50f) * decayPanic;

                        if (elapsed <= 45.0)
                        {
                            posture.HasRecentTrauma = true;
                        }
                    }
                    else if (type.Contains("SECRET") || type.Contains("ACCUSATION") || type.Contains("EXPOSITION") || type.Contains("BETRAY"))
                    {
                        float decaySuspicion = ComputeDecayFactor(elapsed, SuspicionHalfLifeSeconds);
                        float decayTrust = ComputeDecayFactor(elapsed, TrustHalfLifeSeconds);

                        suspicionAcc += (evt.Severity * 0.85f) * decaySuspicion;
                        trustAcc -= (evt.Severity * 40f) * decayTrust;
                        fearAcc += (evt.Severity * 25f) * decaySuspicion;
                    }
                    else if (type.Contains("BRIBE") || type.Contains("GIFT") || type.Contains("COOPERAT"))
                    {
                        float decayTrust = ComputeDecayFactor(elapsed, TrustHalfLifeSeconds);
                        float decayFear = ComputeDecayFactor(elapsed, FearHalfLifeSeconds);

                        trustAcc += (evt.Severity * 50f) * decayTrust;
                        fearAcc -= (evt.Severity * 30f) * decayFear;
                    }
                    else if (type.Contains("ALERT") || type.Contains("DISPATCH") || type.Contains("BACKUP"))
                    {
                        float decayFear = ComputeDecayFactor(elapsed, FearHalfLifeSeconds);
                        fearAcc += (evt.Severity * 35f) * decayFear;
                        suspicionAcc += 0.40f * decayFear;
                    }
                }
            }

            // 4. Clamp normalized emotional values
            posture.EffectiveFear = Math.Max(0f, Math.Min(100f, fearAcc));
            posture.EffectiveTrust = Math.Max(-100f, Math.Min(100f, trustAcc));
            posture.EffectiveRespect = Math.Max(-100f, Math.Min(100f, respectAcc));
            posture.SuspicionLevel = Math.Max(0f, Math.Min(1.0f, suspicionAcc));
            posture.DominantRecentEventType = dominantType;
            posture.BoundedEventCount = count;

            // 5. Dynamic State Derivation from effective decayed posture
            if (posture.EffectiveFear >= 70f)
            {
                posture.EffectiveState = "PANICKED";
            }
            else if (posture.EffectiveFear >= 45f || posture.SuspicionLevel >= 0.60f)
            {
                posture.EffectiveState = "ALERT";
            }
            else if (posture.EffectiveTrust <= -50f)
            {
                posture.EffectiveState = "HOSTILE";
            }
            else if (posture.EffectiveTrust >= 50f)
            {
                posture.EffectiveState = "ALLIED";
            }
            else
            {
                posture.EffectiveState = "CALM";
            }

            return posture;
        }

        /// <summary>
        /// Dynamically recomputes neighborhood heat and threat level around specific 3D coordinates.
        /// Replaces bloated permanent environmental heat database properties with on-demand derived state.
        /// </summary>
        public LocationHeatAssessment RecomputeLocationHeat(float x, float y, float z, IEnumerable<EventRecord> events, float radiusMeters = 75f, DateTime? asOf = null)
        {
            DateTime now = asOf ?? DateTime.UtcNow;
            var assessment = new LocationHeatAssessment { RecomputedAt = now };

            if (events == null) return assessment;

            float heatScore = 0f;
            int violentCount = 0;
            bool policeAlert = false;
            float closestDist = float.MaxValue;
            string dominantThreat = "None";
            float highestThreatWeight = 0f;

            foreach (var evt in events)
            {
                if (evt == null) continue;

                double elapsed = Math.Max(0.0, (now - evt.Timestamp).TotalSeconds);
                // Discard events older than 10 minutes (600 seconds)
                if (elapsed > 600.0) continue;

                float spatialMultiplier = 1.0f;
                if (evt.HasPosition)
                {
                    float dx = x - evt.PositionX;
                    float dy = y - evt.PositionY;
                    float dz = z - evt.PositionZ;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                    if (dist > radiusMeters) continue;

                    if (dist < closestDist) closestDist = dist;
                    spatialMultiplier = Math.Max(0.1f, 1.0f - (dist / radiusMeters));
                }
                else
                {
                    // Global non-positional events receive modest ambient weight
                    spatialMultiplier = 0.35f;
                }

                string type = (evt.EventType ?? "").ToUpperInvariant();
                float temporalDecay = ComputeDecayFactor(elapsed, HostilityHalfLifeSeconds);
                float threatWeight = 0f;

                if (type.Contains("DEATH") || type.Contains("FATAL") || type.Contains("KILL"))
                {
                    threatWeight = 0.50f * evt.Severity;
                    violentCount++;
                }
                else if (type.Contains("SHOOT") || type.Contains("GUN_AIM") || type.Contains("ASSAULT") || type.Contains("DAMAGE"))
                {
                    threatWeight = 0.35f * evt.Severity;
                    violentCount++;
                }
                else if (type.Contains("ALERT") || type.Contains("DISPATCH") || type.Contains("BACKUP") || type.Contains("RAID"))
                {
                    threatWeight = 0.30f * evt.Severity;
                    policeAlert = true;
                }
                else if (type.Contains("EXPLOSION") || type.Contains("FIRE"))
                {
                    threatWeight = 0.40f * evt.Severity;
                    violentCount++;
                }

                float eventContribution = threatWeight * spatialMultiplier * temporalDecay;
                heatScore += eventContribution;

                if (eventContribution > highestThreatWeight)
                {
                    highestThreatWeight = eventContribution;
                    dominantThreat = evt.EventType;
                }
            }

            assessment.DangerScore = Math.Max(0f, Math.Min(1.0f, heatScore));
            assessment.RecentViolentEventCount = violentCount;
            assessment.ActivePoliceAlert = policeAlert;
            assessment.DominantThreat = dominantThreat;
            assessment.DistanceToClosestThreat = closestDist;

            if (assessment.DangerScore >= 0.70f)
            {
                assessment.Summary = "Active lethal confrontation zone";
            }
            else if (assessment.DangerScore >= 0.40f)
            {
                assessment.Summary = "Heightened hostility / recent gunfire";
            }
            else if (assessment.DangerScore >= 0.15f)
            {
                assessment.Summary = "Elevated tension";
            }
            else
            {
                assessment.Summary = "Peaceful area";
            }

            return assessment;
        }

        /// <summary>
        /// Dynamically recomputes hostility and war status between two factions from recent event history.
        /// </summary>
        public FactionTensionReport RecomputeFactionHostility(string factionA, string factionB, IEnumerable<EventRecord> events, DateTime? asOf = null)
        {
            DateTime now = asOf ?? DateTime.UtcNow;
            var report = new FactionTensionReport
            {
                FactionA = factionA ?? "Unknown",
                FactionB = factionB ?? "Unknown",
                RecomputedAt = now
            };

            if (string.Equals(factionA, factionB, StringComparison.OrdinalIgnoreCase) || events == null)
            {
                return report;
            }

            float tensionAcc = 0f;
            int clashCount = 0;

            foreach (var evt in events)
            {
                if (evt == null) continue;

                double elapsed = Math.Max(0.0, (now - evt.Timestamp).TotalSeconds);
                if (elapsed > 900.0) continue; // 15 minute window

                bool involvesBoth = (string.Equals(evt.Actor, factionA, StringComparison.OrdinalIgnoreCase) && string.Equals(evt.Target, factionB, StringComparison.OrdinalIgnoreCase)) ||
                                    (string.Equals(evt.Actor, factionB, StringComparison.OrdinalIgnoreCase) && string.Equals(evt.Target, factionA, StringComparison.OrdinalIgnoreCase)) ||
                                    ((evt.Description ?? "").IndexOf(factionA, StringComparison.OrdinalIgnoreCase) >= 0 && (evt.Description ?? "").IndexOf(factionB, StringComparison.OrdinalIgnoreCase) >= 0);

                if (!involvesBoth) continue;

                string type = (evt.EventType ?? "").ToUpperInvariant();
                float decay = ComputeDecayFactor(elapsed, TrustHalfLifeSeconds);

                if (type.Contains("DEATH") || type.Contains("FATAL") || type.Contains("KILL") || type.Contains("ASSAULT") || type.Contains("SHOOT") || type.Contains("RAID") || type.Contains("RETALIAT") || type.Contains("COMBAT"))
                {
                    tensionAcc += (evt.Severity * 0.45f) * decay;
                    clashCount++;
                }
                else if (type.Contains("BETRAY") || type.Contains("THREAT"))
                {
                    tensionAcc += (evt.Severity * 0.30f) * decay;
                    clashCount++;
                }
                else if (type.Contains("PEACE") || type.Contains("TRUCE") || type.Contains("COOPERAT"))
                {
                    tensionAcc -= (evt.Severity * 0.40f) * decay;
                }
            }

            report.HostilityScore = Math.Max(0f, Math.Min(1.0f, tensionAcc));
            report.RecentClashCount = clashCount;
            report.IsAtWar = report.HostilityScore >= 0.70f || clashCount >= 2;

            if (report.IsAtWar)
            {
                report.Status = "ACTIVE_WAR";
                report.Summary = string.Format("Active war between {0} and {1} ({2} recent clashes)", factionA, factionB, clashCount);
            }
            else if (report.HostilityScore >= 0.40f)
            {
                report.Status = "HOSTILE";
                report.Summary = string.Format("High hostility between {0} and {1}", factionA, factionB);
            }
            else if (report.HostilityScore >= 0.15f)
            {
                report.Status = "TENSE";
                report.Summary = string.Format("Simmering tension between {0} and {1}", factionA, factionB);
            }
            else
            {
                report.Status = "PEACEFUL";
                report.Summary = "No active hostility";
            }

            return report;
        }

        /// <summary>
        /// Law 7: Bounded Replay Beats Infinite State Growth.
        /// Compacts an event list by purging low-salience noise (chat, wandering)
        /// while strictly preserving high-salience milestones (death, betrayal, secrets)
        /// to maintain a strict upper bound on memory and serialization footprint.
        /// </summary>
        public List<EventRecord> CompactEventHistory(List<EventRecord> history, int maxRetained = DefaultBoundedWindowSize)
        {
            if (history == null || history.Count <= maxRetained)
            {
                return history != null ? new List<EventRecord>(history) : new List<EventRecord>();
            }

            // Score each event by intrinsic milestone significance + recency
            var scoredEvents = history.Select((evt, index) =>
            {
                float score = 0f;
                string type = (evt.EventType ?? "").ToUpperInvariant();

                // Critical milestones receive heavy preservation scores
                if (type.Contains("DEATH") || type.Contains("FATAL") || type.Contains("KILL"))
                {
                    score += 100.0f;
                }
                else if (type.Contains("SECRET") || type.Contains("ACCUSATION") || type.Contains("EXPOSITION"))
                {
                    score += 60.0f;
                }
                else if (type.Contains("BETRAY") || type.Contains("WAR"))
                {
                    score += 50.0f;
                }
                else if (type.Contains("ALERT") || type.Contains("DISPATCH") || type.Contains("RAID"))
                {
                    score += 40.0f;
                }
                else if (type.Contains("ASSAULT") || type.Contains("SHOOT") || type.Contains("DAMAGE"))
                {
                    score += 30.0f;
                }
                else if (type.Contains("CHAT") || type.Contains("IDLE") || type.Contains("WANDER"))
                {
                    score += 0.0f;
                }
                else
                {
                    score += 10.0f;
                }

                // Severity contribution
                score += evt.Severity * 20.0f;

                // Recency bonus (more recent events in index get higher weight)
                score += (float)index / Math.Max(1, history.Count) * 15.0f;

                return new { Event = evt, Score = score, OriginalIndex = index };
            }).ToList();

            // Retain top `maxRetained` scored events, and restore original chronological order
            var compacted = scoredEvents
                .OrderByDescending(s => s.Score)
                .Take(maxRetained)
                .OrderBy(s => s.OriginalIndex)
                .Select(s => s.Event)
                .ToList();

            return compacted;
        }
    }
}
