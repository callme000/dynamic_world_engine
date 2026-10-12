using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Retrieval
{
    public class KairosNpcDossier
    {
        public int NpcId { get; set; }
        public string Name { get; set; }
        public string Occupation { get; set; }
        public string PublicFaction { get; set; }
        public string HiddenRole { get; set; }
        public string EffectiveState { get; set; }
        public float EffectiveFear { get; set; }
        public float EffectiveTrust { get; set; }
        public float SuspicionLevel { get; set; }
        public bool HasRecentTrauma { get; set; }
        public string PostureSummary { get; set; }
        public List<string> ActiveGoals { get; set; }
        public List<string> KnownSecrets { get; set; }
        public List<string> KeyBeliefs { get; set; }

        public KairosNpcDossier()
        {
            ActiveGoals = new List<string>();
            KnownSecrets = new List<string>();
            KeyBeliefs = new List<string>();
            EffectiveState = "CALM";
            PostureSummary = string.Empty;
        }

        public override string ToString()
        {
            return string.Format("{0} [{1} / {2}]: {3}", Name, PublicFaction, HiddenRole, PostureSummary);
        }
    }

    public class KairosSituationBrief
    {
        public string LocationContext { get; set; }
        public float DangerScore { get; set; }
        public string ThreatSummary { get; set; }
        public bool ActivePoliceAlert { get; set; }
        public List<string> ActiveFactionWars { get; set; }
        public List<KairosNpcDossier> KeyCharacters { get; set; }
        public List<string> RecentCriticalMilestones { get; set; }
        public List<string> DramaticOpportunities { get; set; }
        public DateTime CompiledAt { get; set; }

        public KairosSituationBrief()
        {
            LocationContext = "Los Santos Metropolitan Area";
            ThreatSummary = "Peaceful area";
            ActiveFactionWars = new List<string>();
            KeyCharacters = new List<KairosNpcDossier>();
            RecentCriticalMilestones = new List<string>();
            DramaticOpportunities = new List<string>();
            CompiledAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Formats this brief into a compact, token-efficient markdown block (< 350 tokens)
        /// designed for injection into Astra World Director or Scene Architect prompts.
        /// </summary>
        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("### SITUATION BRIEF (KAIROS COGNITIVE INSIGHT)");
            sb.AppendFormat("- **Location Threat**: {0} (Danger: {1:F2}, Police Alert: {2})\n",
                ThreatSummary, DangerScore, ActivePoliceAlert ? "ACTIVE" : "None");

            if (ActiveFactionWars.Count > 0)
            {
                sb.AppendLine("- **Active Faction Conflicts**:");
                foreach (var war in ActiveFactionWars)
                {
                    sb.AppendFormat("  * {0}\n", war);
                }
            }

            if (KeyCharacters.Count > 0)
            {
                sb.AppendLine("- **Key Characters & Effective Postures**:");
                foreach (var c in KeyCharacters)
                {
                    string goals = c.ActiveGoals.Count > 0 ? string.Join(", ", c.ActiveGoals) : "Routine";
                    string secrets = c.KnownSecrets.Count > 0 ? string.Format(" [Secrets: {0}]", string.Join(";", c.KnownSecrets)) : "";
                    sb.AppendFormat("  * {0} ({1}/{2}): {3} | Goal: {4}{5}\n",
                        c.Name, c.PublicFaction, c.HiddenRole, c.PostureSummary, goals, secrets);
                }
            }

            if (RecentCriticalMilestones.Count > 0)
            {
                sb.AppendLine("- **Recent Critical Milestones**:");
                foreach (var m in RecentCriticalMilestones)
                {
                    sb.AppendFormat("  * {0}\n", m);
                }
            }

            if (DramaticOpportunities.Count > 0)
            {
                sb.AppendLine("- **Dramatic Narrative Opportunities**:");
                foreach (var opp in DramaticOpportunities)
                {
                    sb.AppendFormat("  * {0}\n", opp);
                }
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Kairos Insight Query Pipeline (Laws 5, 9 & 10).
    /// Compiles raw physical state and decayed ephemeral postures into
    /// structured, task-specific situation briefs rather than dumping raw logs.
    /// </summary>
    public class KairosInsightCompiler
    {
        private readonly WorldMemoryStore _store;
        private readonly EphemeralStateRecomputer _recomputer;
        private readonly SalienceEvaluator _salience;
        private readonly ContextRetriever _retriever;

        public KairosInsightCompiler(
            WorldMemoryStore store,
            EphemeralStateRecomputer recomputer = null,
            SalienceEvaluator salience = null,
            ContextRetriever retriever = null)
        {
            _store = store ?? new WorldMemoryStore();
            _recomputer = recomputer ?? new EphemeralStateRecomputer();
            _salience = salience ?? new SalienceEvaluator();
            _retriever = retriever ?? new ContextRetriever();
        }

        public WorldMemoryStore Store { get { return _store; } }
        public EphemeralStateRecomputer Recomputer { get { return _recomputer; } }
        public SalienceEvaluator Salience { get { return _salience; } }
        public ContextRetriever Retriever { get { return _retriever; } }

        /// <summary>
        /// Compiles a complete, high-order situation brief for a given location or character focus.
        /// </summary>
        public KairosSituationBrief CompileSituationBrief(
            float? x = null,
            float? y = null,
            float? z = null,
            int? focusNpcId = null,
            string targetFaction = null,
            int maxCharacters = 5,
            int maxMilestones = 4,
            DateTime? asOf = null)
        {
            DateTime now = asOf ?? DateTime.UtcNow;
            var brief = new KairosSituationBrief { CompiledAt = now };
            var recentEvents = _store.GetRecentWorldEvents(50);

            // 1. Location Heat Assessment
            if (x.HasValue && y.HasValue && z.HasValue)
            {
                var heat = _recomputer.RecomputeLocationHeat(x.Value, y.Value, z.Value, recentEvents, 75f, now);
                brief.DangerScore = heat.DangerScore;
                brief.ThreatSummary = heat.Summary;
                brief.ActivePoliceAlert = heat.ActivePoliceAlert;
                brief.LocationContext = string.Format("Coordinates ({0:F0}, {1:F0}, {2:F0})", x.Value, y.Value, z.Value);
            }
            else
            {
                var heat = _recomputer.RecomputeLocationHeat(0f, 0f, 0f, recentEvents, 5000f, now);
                brief.DangerScore = heat.DangerScore;
                brief.ThreatSummary = heat.Summary;
                brief.ActivePoliceAlert = heat.ActivePoliceAlert;
                brief.LocationContext = "Los Santos Metropolitan Area";
            }

            // 2. High-Salience Milestones (Law 9: compiled, not raw dump)
            var salientEvents = new List<EventRecord>();
            foreach (var evt in recentEvents)
            {
                var assessment = _salience.EvaluateEvent(evt);
                if (assessment.IsCognitiveTrigger || assessment.SalienceScore >= 0.65f)
                {
                    salientEvents.Add(evt);
                }
            }

            foreach (var evt in salientEvents.Take(maxMilestones))
            {
                brief.RecentCriticalMilestones.Add(string.Format("[{0}] {1} (Severity: {2:F2})",
                    evt.EventType, evt.Description, evt.Severity));
            }

            // 3. Active Faction Wars
            var allNpcs = _store.GetAllNpcs();
            var knownFactions = allNpcs
                .Select(n => n.PublicFaction)
                .Concat(recentEvents.Select(e => e.Actor))
                .Concat(recentEvents.Select(e => e.Target))
                .Where(f => !string.IsNullOrEmpty(f) &&
                            !string.Equals(f, "Bystander", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(f, "Civilians", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(f, "Player", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(f, "npc", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(f, "Unknown", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < knownFactions.Count; i++)
            {
                for (int j = i + 1; j < knownFactions.Count; j++)
                {
                    var tension = _recomputer.RecomputeFactionHostility(knownFactions[i], knownFactions[j], recentEvents, now);
                    if (tension.IsAtWar || tension.HostilityScore >= 0.40f)
                    {
                        brief.ActiveFactionWars.Add(tension.Summary);
                    }
                }
            }

            // 4. Key Characters (Task-Specific Scoping - Law 5)
            var candidates = new List<NpcRecord>();
            if (focusNpcId.HasValue)
            {
                var focusNpc = _store.GetNpcById(focusNpcId.Value);
                if (focusNpc != null) candidates.Add(focusNpc);
            }

            if (!string.IsNullOrEmpty(targetFaction))
            {
                var factionNpcs = allNpcs.Where(n => string.Equals(n.PublicFaction, targetFaction, StringComparison.OrdinalIgnoreCase) && !candidates.Contains(n));
                candidates.AddRange(factionNpcs);
            }

            // Add other salient / active NPCs
            foreach (var npc in allNpcs)
            {
                if (candidates.Contains(npc) || !npc.IsAlive) continue;

                bool isRelevant = (npc.ActiveGoal != null && npc.ActiveGoal.GoalType != "IDLE" && npc.ActiveGoal.GoalType != "MAINTAIN_ROUTINE") ||
                                  (!string.IsNullOrEmpty(npc.HiddenRole) && !string.Equals(npc.HiddenRole, "None", StringComparison.OrdinalIgnoreCase)) ||
                                  (npc.Fear >= 40 || npc.Trust <= -30);

                if (isRelevant)
                {
                    candidates.Add(npc);
                }
            }

            foreach (var npc in candidates.Take(maxCharacters))
            {
                brief.KeyCharacters.Add(CompileNpcDossier(npc.NpcId, now));
            }

            // 5. High-Order Dramatic Opportunities
            foreach (var dossier in brief.KeyCharacters)
            {
                if (dossier.ActiveGoals.Exists(g => g.Contains("ALERT_POLICE")))
                {
                    brief.DramaticOpportunities.Add(string.Format("{0} is actively alerting police; intercept or extraction recommended", dossier.Name));
                }
                else if (dossier.ActiveGoals.Exists(g => g.Contains("SEEK_PROTECTION")))
                {
                    brief.DramaticOpportunities.Add(string.Format("{0} is seeking armed reinforcement; ambush window open", dossier.Name));
                }
                if (dossier.HasRecentTrauma || dossier.EffectiveFear >= 70f)
                {
                    brief.DramaticOpportunities.Add(string.Format("{0} is in severe acute panic; highly compliant to intimidation or interrogation", dossier.Name));
                }
                if (dossier.KnownSecrets.Count > 0)
                {
                    brief.DramaticOpportunities.Add(string.Format("{0} holds critical secrets ({1}); blackmail or trade leverage available",
                        dossier.Name, string.Join(", ", dossier.KnownSecrets)));
                }
            }

            if (brief.ActiveFactionWars.Count > 0)
            {
                brief.DramaticOpportunities.Add("Active open faction conflict provides situational cover for covert actions");
            }

            if (brief.DangerScore >= 0.70f)
            {
                brief.DramaticOpportunities.Add("Local high-threat combat environment: law enforcement sirens active, high chaos index");
            }

            return brief;
        }

        /// <summary>
        /// Compiles an individual NPC dossier including effective decayed posture and epistemic data.
        /// </summary>
        public KairosNpcDossier CompileNpcDossier(int npcId, DateTime? asOf = null)
        {
            var npc = _store.GetNpcById(npcId);
            if (npc == null) return null;

            DateTime now = asOf ?? DateTime.UtcNow;
            var posture = _recomputer.RecomputeNpcPosture(npc, now);

            var dossier = new KairosNpcDossier
            {
                NpcId = npc.NpcId,
                Name = npc.Name,
                Occupation = npc.Occupation,
                PublicFaction = npc.PublicFaction,
                HiddenRole = npc.HiddenRole,
                EffectiveState = posture.EffectiveState,
                EffectiveFear = posture.EffectiveFear,
                EffectiveTrust = posture.EffectiveTrust,
                SuspicionLevel = posture.SuspicionLevel,
                HasRecentTrauma = posture.HasRecentTrauma,
                PostureSummary = string.Format("{0} (Fear: {1:F0}, Trust: {2:F0}, Suspicion: {3:F2}{4})",
                    posture.EffectiveState, posture.EffectiveFear, posture.EffectiveTrust, posture.SuspicionLevel,
                    posture.HasRecentTrauma ? ", Recent Trauma" : "")
            };

            if (npc.ActiveGoal != null && npc.ActiveGoal.GoalType != "IDLE")
            {
                dossier.ActiveGoals.Add(string.Format("{0} ({1})", npc.ActiveGoal.GoalType, npc.ActiveGoal.Description));
            }

            if (npc.Secrets != null)
            {
                dossier.KnownSecrets.AddRange(npc.Secrets);
            }

            if (npc.Beliefs != null)
            {
                foreach (var b in npc.Beliefs.Take(3))
                {
                    dossier.KeyBeliefs.Add(string.Format("{0} {1} {2}", b.Subject, b.Predicate, b.ObjectValue));
                }
            }

            return dossier;
        }
    }
}
