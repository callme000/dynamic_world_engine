using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Database;
using GTA.Math;

namespace DynamicWorldEngine.Retrieval
{
    public class ScopedContextResult
    {
        public List<NpcRecord> CandidateNpcs { get; set; }
        public EnvironmentRecord ActiveEnvironment { get; set; }
        public List<EventRecord> RelevantEvents { get; set; }
        public string FormattedContext { get; set; }
        public int TotalWorldNpcs { get; set; }
        public int FilteredNpcsCount { get; set; }

        public ScopedContextResult()
        {
            CandidateNpcs = new List<NpcRecord>();
            RelevantEvents = new List<EventRecord>();
            FormattedContext = string.Empty;
        }
    }

    public class ContextRetriever
    {
        private static bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
            return Regex.IsMatch(text, @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase);
        }

        public ScopedContextResult RetrieveScopedContext(
            WorldMemoryStore store,
            string userPrompt,
            string activeEnvironmentId = null,
            Vector3? playerPosition = null,
            int maxCandidates = 4,
            int maxRecentEvents = 3)
        {
            var result = new ScopedContextResult();
            if (store == null) return result;

            List<NpcRecord> allNpcs = store.GetAllNpcs();
            result.TotalWorldNpcs = allNpcs.Count;

            string cleanPrompt = (userPrompt ?? "").ToLowerInvariant().Trim();

            // 1. Resolve Active Environment if any
            EnvironmentRecord activeEnv = null;
            if (!string.IsNullOrEmpty(activeEnvironmentId))
            {
                activeEnv = store.GetEnvironment(activeEnvironmentId);
            }
            if (activeEnv == null && allNpcs.Count > 0)
            {
                var allEnvs = store.GetAllEnvironments();
                activeEnv = allEnvs.OrderByDescending(e => e.LastRealizedAt ?? e.CreatedAt).FirstOrDefault();
            }
            result.ActiveEnvironment = activeEnv;

            // 2. Score Candidate NPCs
            var scoredNpcs = new List<KeyValuePair<NpcRecord, int>>();

            foreach (var npc in allNpcs)
            {
                if (npc == null || !npc.IsAlive) continue;

                int score = 0;

                // A. Direct Name Matching (+100)
                if (!string.IsNullOrEmpty(npc.Name))
                {
                    string[] nameTokens = npc.Name.Split(new char[] { ' ', '\'', '"' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in nameTokens)
                    {
                        if (token.Length >= 3 && ContainsWord(cleanPrompt, token))
                        {
                            score += 100;
                        }
                    }
                }

                // B. EntityId Matching (+100)
                if (!string.IsNullOrEmpty(npc.EntityId) && ContainsWord(cleanPrompt, npc.EntityId))
                {
                    score += 100;
                }

                // C. Occupation / Role Matching (+60)
                if (!string.IsNullOrEmpty(npc.Occupation))
                {
                    string[] occTokens = npc.Occupation.Split(new char[] { ' ', '/', '&', '-' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in occTokens)
                    {
                        if (token.Length >= 3 && ContainsWord(cleanPrompt, token))
                        {
                            score += 60;
                        }
                    }
                }

                // D. Public Faction Matching (+40)
                if (!string.IsNullOrEmpty(npc.PublicFaction))
                {
                    string[] factionTokens = npc.PublicFaction.Split(new char[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in factionTokens)
                    {
                        if (token.Length >= 3 && ContainsWord(cleanPrompt, token))
                        {
                            score += 40;
                        }
                    }
                }

                // E. Hidden Role / Secret Keywords (+40)
                if (!string.IsNullOrEmpty(npc.HiddenRole) && !npc.HiddenRole.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    string[] secretTokens = npc.HiddenRole.Split(new char[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in secretTokens)
                    {
                        if (token.Length >= 4 && ContainsWord(cleanPrompt, token))
                        {
                            score += 40;
                        }
                    }
                }

                // F. Active Environment Residency Bonus (+30)
                if (activeEnv != null && activeEnv.NpcIds != null && activeEnv.NpcIds.Contains(npc.NpcId))
                {
                    score += 30;
                }
                else if (activeEnv != null && activeEnv.ResidentEntityIds != null && !string.IsNullOrEmpty(npc.EntityId) && activeEnv.ResidentEntityIds.Contains(npc.EntityId))
                {
                    score += 30;
                }

                scoredNpcs.Add(new KeyValuePair<NpcRecord, int>(npc, score));
            }

            // Order candidates by score descending
            var topCandidates = scoredNpcs
                .OrderByDescending(kvp => kvp.Value)
                .ThenByDescending(kvp => kvp.Key.CreatedAt)
                .Take(maxCandidates)
                .Select(kvp => kvp.Key)
                .ToList();

            // If prompt didn't match any names/roles specifically and all scores are 0,
            // fall back to resident NPCs of the active environment
            if (topCandidates.Count == 0 && activeEnv != null && activeEnv.NpcIds != null && activeEnv.NpcIds.Count > 0)
            {
                foreach (var id in activeEnv.NpcIds.Take(maxCandidates))
                {
                    var npc = store.GetNpc(id);
                    if (npc != null && npc.IsAlive) topCandidates.Add(npc);
                }
            }

            result.CandidateNpcs = topCandidates;
            result.FilteredNpcsCount = topCandidates.Count;

            // 3. Collect Relevant Event History for Candidates
            var recentEvents = new List<EventRecord>();
            var candidateIds = new HashSet<int>(topCandidates.Select(c => c.NpcId));
            var candidateNames = new HashSet<string>(topCandidates.Select(c => c.Name.ToLowerInvariant()));

            var worldEvents = store.GetRecentWorldEvents(15);
            if (worldEvents != null)
            {
                foreach (var evt in worldEvents)
                {
                    if (evt == null) continue;
                    bool isRelevant = false;
                    if (candidateIds.Contains(evt.NpcId))
                    {
                        isRelevant = true;
                    }
                    else if (!string.IsNullOrEmpty(evt.Actor) && candidateNames.Contains(evt.Actor.ToLowerInvariant()))
                    {
                        isRelevant = true;
                    }
                    else if (!string.IsNullOrEmpty(evt.Target) && candidateNames.Contains(evt.Target.ToLowerInvariant()))
                    {
                        isRelevant = true;
                    }

                    if (isRelevant && !recentEvents.Contains(evt))
                    {
                        recentEvents.Add(evt);
                        if (recentEvents.Count >= maxRecentEvents) break;
                    }
                }
            }
            result.RelevantEvents = recentEvents;

            // 4. Synthesize Compact Scoped Context
            StringBuilder sb = new StringBuilder();

            if (activeEnv != null)
            {
                sb.AppendLine(string.Format("[ACTIVE VENUE]: {0} ({1}) - Status: {2}", 
                    activeEnv.Name, activeEnv.Archetype, activeEnv.Status));
            }

            sb.AppendLine("[RELEVANT ENTITIES]:");
            if (topCandidates.Count == 0)
            {
                sb.AppendLine("  (No specific resident NPCs active nearby)");
            }
            else
            {
                foreach (var npc in topCandidates)
                {
                    // Relational Pruning: Only keep relationships with Player or other candidates!
                    var prunedRels = new List<string>();
                    if (npc.Relationships != null)
                    {
                        foreach (var kvp in npc.Relationships)
                        {
                            if (kvp.Key.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
                                candidateNames.Contains(kvp.Key.ToLowerInvariant()))
                            {
                                prunedRels.Add(string.Format("{0}:{1}{2}", kvp.Key, kvp.Value >= 0 ? "+" : "", kvp.Value));
                            }
                        }
                    }
                    string relStr = prunedRels.Count > 0 ? string.Join(",", prunedRels.ToArray()) : "Player:0";

                    string goalStr = npc.ActiveGoal != null ? npc.ActiveGoal.GoalType : "ROUTINE";
                    string secretStr = (!string.IsNullOrEmpty(npc.HiddenRole) && !npc.HiddenRole.Equals("None", StringComparison.OrdinalIgnoreCase))
                        ? string.Format(" | Secret: {0}", npc.HiddenRole)
                        : "";

                    sb.AppendLine(string.Format("  - [{0}] {1} ({2}/{3}) | State: {4} | Goal: {5} | Rel: {6}{7}",
                        npc.EntityId, npc.Name, npc.PublicFaction, npc.Occupation, npc.CurrentState, goalStr, relStr, secretStr));
                }
            }

            if (recentEvents.Count > 0)
            {
                sb.AppendLine("[RECENT SALIENT EVENTS]:");
                foreach (var evt in recentEvents)
                {
                    sb.AppendLine(string.Format("  - {0}: {1}", evt.EventType, evt.Description));
                }
            }

            result.FormattedContext = sb.ToString().TrimEnd();
            return result;
        }
    }
}
