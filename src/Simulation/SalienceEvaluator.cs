using System;
using System.Collections.Generic;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Simulation
{
    public class SalienceAssessment
    {
        public string EventType { get; set; }
        public float SalienceScore { get; set; }
        public bool IsCognitiveTrigger { get; set; }
        public string TriggerReason { get; set; }
        public int NpcId { get; set; }
        public string Actor { get; set; }
        public string Target { get; set; }
        public List<string> SalienceFactors { get; set; }
        public DateTime Timestamp { get; set; }

        public SalienceAssessment()
        {
            SalienceFactors = new List<string>();
            Timestamp = DateTime.UtcNow;
            EventType = "UNKNOWN";
            TriggerReason = string.Empty;
            Actor = string.Empty;
            Target = string.Empty;
        }

        public override string ToString()
        {
            return string.Format("[Salience {0:F2} - {1}] {2}: {3}",
                SalienceScore, IsCognitiveTrigger ? "TRIGGER" : "SUPPRESSED",
                EventType, TriggerReason);
        }
    }

    /// <summary>
    /// Event-Driven Cognitive Salience Evaluator (Laws 6 & 8).
    /// Enforces the principle that the physical world runs cheaply ($0, 0ms),
    /// invoking expensive LLM cognition only when state transitions cross
    /// a critical salience threshold (default >= 0.70).
    /// </summary>
    public class SalienceEvaluator
    {
        public const float DefaultThreshold = 0.70f;
        private readonly float _threshold;
        private readonly double _cooldownSeconds;
        private readonly Dictionary<string, DateTime> _lastTriggerTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();

        public float Threshold { get { return _threshold; } }
        public double CooldownSeconds { get { return _cooldownSeconds; } }

        public SalienceEvaluator(float threshold = DefaultThreshold, double cooldownSeconds = 3.0)
        {
            _threshold = threshold;
            _cooldownSeconds = cooldownSeconds;
        }

        public void ClearCooldowns()
        {
            lock (_lock)
            {
                _lastTriggerTimes.Clear();
            }
        }

        private bool IsCooldownActive(string key, DateTime now)
        {
            lock (_lock)
            {
                DateTime last;
                if (_lastTriggerTimes.TryGetValue(key, out last))
                {
                    if ((now - last).TotalSeconds < _cooldownSeconds)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void RecordTriggerTime(string key, DateTime now)
        {
            lock (_lock)
            {
                _lastTriggerTimes[key] = now;
            }
        }

        /// <summary>
        /// Evaluates an EventRecord generated in the world for cognitive triggering.
        /// </summary>
        public SalienceAssessment EvaluateEvent(EventRecord evt, NpcRecord npc = null, float distanceToPlayer = 0f)
        {
            var assessment = new SalienceAssessment();
            if (evt == null)
            {
                assessment.SalienceScore = 0f;
                assessment.IsCognitiveTrigger = false;
                assessment.TriggerReason = "Null event record";
                return assessment;
            }

            assessment.EventType = evt.EventType ?? "UNKNOWN";
            assessment.NpcId = evt.NpcId;
            assessment.Actor = evt.Actor ?? "Unknown";
            assessment.Target = evt.Target ?? "Unknown";

            string typeUpper = assessment.EventType.ToUpperInvariant();
            float score = 0f;

            // 1. Base Severity contribution (up to 0.35)
            float baseContribution = Math.Max(0f, Math.Min(1f, evt.Severity)) * 0.35f;
            score += baseContribution;
            assessment.SalienceFactors.Add(string.Format("Base Severity ({0:F2}) -> +{1:F2}", evt.Severity, baseContribution));

            // 2. Event Type Specific Multiplier
            float typeContribution = 0f;
            if (typeUpper.Contains("DEATH") || typeUpper.Contains("FATAL") || typeUpper.Contains("KILL"))
            {
                typeContribution = 0.45f;
                assessment.SalienceFactors.Add("Event Type: Mortality State Transition -> +0.45");
            }
            else if (typeUpper.Contains("SECRET") || typeUpper.Contains("ACCUSATION") || typeUpper.Contains("EXPOSITION") || typeUpper.Contains("BLACKMAIL"))
            {
                typeContribution = 0.35f;
                assessment.SalienceFactors.Add("Event Type: Epistemic Secret Disclosure -> +0.35");
            }
            else if (typeUpper.Contains("BETRAY") || typeUpper.Contains("FACTION") || typeUpper.Contains("WAR") || typeUpper.Contains("HOSTIL"))
            {
                typeContribution = 0.30f;
                assessment.SalienceFactors.Add("Event Type: Allegiance / Hostility Shift -> +0.30");
            }
            else if (typeUpper.Contains("ALERT") || typeUpper.Contains("DISPATCH") || typeUpper.Contains("BACKUP") || typeUpper.Contains("RAID"))
            {
                typeContribution = 0.30f;
                assessment.SalienceFactors.Add("Event Type: Tactical Reinforcement / Escalation -> +0.30");
            }
            else if (typeUpper.Contains("ASSAULT") || typeUpper.Contains("DAMAGE") || typeUpper.Contains("GUN_AIM") || typeUpper.Contains("SHOOT"))
            {
                typeContribution = 0.20f;
                assessment.SalienceFactors.Add("Event Type: Physical Weapon Confrontation -> +0.20");
            }
            else if (typeUpper.Contains("BRIBE") || typeUpper.Contains("GIFT") || typeUpper.Contains("COOPERAT"))
            {
                typeContribution = 0.20f;
                assessment.SalienceFactors.Add("Event Type: Strategic Cooperation -> +0.20");
            }
            else if (typeUpper.Contains("CHAT") || typeUpper.Contains("AMBIENT") || typeUpper.Contains("WANDER") || typeUpper.Contains("IDLE"))
            {
                typeContribution = 0.0f;
                assessment.SalienceFactors.Add("Event Type: Ambient / Trivial -> +0.00");
            }
            else
            {
                typeContribution = 0.10f;
                assessment.SalienceFactors.Add("Event Type: General Event -> +0.10");
            }
            score += typeContribution;

            // 3. Player Direct Involvement (+0.25)
            bool isPlayerInvolved = string.Equals(evt.Actor, "player", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(evt.Target, "player", StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrEmpty(evt.Description) && evt.Description.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0);
            if (isPlayerInvolved)
            {
                score += 0.25f;
                assessment.SalienceFactors.Add("Player Direct Involvement -> +0.25");
            }

            // 4. Character Significance (Named character, faction leadership, or secret role)
            if (npc != null)
            {
                if (!string.IsNullOrEmpty(npc.HiddenRole) && !string.Equals(npc.HiddenRole, "None", StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.10f;
                    assessment.SalienceFactors.Add(string.Format("Target has Hidden Role '{0}' -> +0.10", npc.HiddenRole));
                }
                if (npc.Secrets != null && npc.Secrets.Count > 0)
                {
                    score += 0.05f;
                    assessment.SalienceFactors.Add("Target holds active secrets -> +0.05");
                }
            }

            // 5. Proximity Attenuation
            if (distanceToPlayer > 0f)
            {
                if (distanceToPlayer <= 15.0f)
                {
                    score += 0.10f;
                    assessment.SalienceFactors.Add(string.Format("Close Proximity ({0:F1}m <= 15m) -> +0.10", distanceToPlayer));
                }
                else if (distanceToPlayer > 60.0f)
                {
                    score -= 0.25f;
                    assessment.SalienceFactors.Add(string.Format("Distant Event ({0:F1}m > 60m) -> -0.25", distanceToPlayer));
                }
            }

            // Clamp normalized salience score
            assessment.SalienceScore = Math.Max(0.0f, Math.Min(1.0f, score));

            // 6. Anti-Oscillation Cooldown Check
            string cooldownKey = string.Format("{0}_{1}", assessment.EventType, assessment.NpcId);
            DateTime now = DateTime.UtcNow;

            if (assessment.SalienceScore >= _threshold)
            {
                if (IsCooldownActive(cooldownKey, now))
                {
                    assessment.IsCognitiveTrigger = false;
                    assessment.TriggerReason = string.Format("Salience threshold exceeded ({0:F2} >= {1:F2}) but suppressed by cooldown",
                        assessment.SalienceScore, _threshold);
                }
                else
                {
                    assessment.IsCognitiveTrigger = true;
                    RecordTriggerTime(cooldownKey, now);
                    assessment.TriggerReason = string.Format("Salience threshold exceeded ({0:F2} >= {1:F2}): Cognitive Trigger Fired",
                        assessment.SalienceScore, _threshold);
                }
            }
            else
            {
                assessment.IsCognitiveTrigger = false;
                assessment.TriggerReason = string.Format("Salience below threshold ({0:F2} < {1:F2}): Handled by cheap local state machine",
                    assessment.SalienceScore, _threshold);
            }

            return assessment;
        }

        /// <summary>
        /// Evaluates an NPC death event specifically.
        /// </summary>
        public SalienceAssessment EvaluateDeath(int npcId, string killer, string cause, NpcRecord npc = null, float distanceToPlayer = 0f)
        {
            var evt = new EventRecord
            {
                NpcId = npcId,
                Actor = killer ?? "Unknown",
                Target = npc != null ? npc.Name : string.Format("NPC_{0}", npcId),
                EventType = "NPC_DEATH",
                Severity = 1.0f,
                Description = string.Format("{0} was killed by {1} ({2})", npc != null ? npc.Name : "NPC", killer, cause),
                SentimentTag = "Fatal_Mortality"
            };

            return EvaluateEvent(evt, npc, distanceToPlayer);
        }

        /// <summary>
        /// Evaluates a spoken player utterance through semantic intent.
        /// </summary>
        public SalienceAssessment EvaluateUtterance(int npcId, SemanticIntent intent, NpcRecord npc = null, float distanceToPlayer = 0f)
        {
            if (intent == null)
            {
                return new SalienceAssessment { SalienceScore = 0f, IsCognitiveTrigger = false, TriggerReason = "Null intent" };
            }

            var evt = new EventRecord
            {
                NpcId = npcId,
                Actor = "Player",
                Target = npc != null ? npc.Name : string.Format("NPC_{0}", npcId),
                EventType = intent.Type.ToString().ToUpperInvariant(),
                Severity = intent.EmotionalImpact,
                Description = intent.RawUtterance ?? "",
                SentimentTag = intent.Type.ToString()
            };

            return EvaluateEvent(evt, npc, distanceToPlayer);
        }

        public bool ShouldTriggerCognition(SalienceAssessment assessment)
        {
            return assessment != null && assessment.IsCognitiveTrigger;
        }
    }
}
