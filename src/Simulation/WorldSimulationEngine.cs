using System;
using System.Collections.Generic;
using DynamicWorldEngine.Bridge;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Simulation
{
    public class WorldSimulationEngine
    {
        private readonly WorldMemoryStore _store;
        private readonly LivingLsAiBridge _bridge;
        private readonly TeleportPocketManager _pocketManager;
        private readonly BlipManager _blipManager;
        private readonly SalienceEvaluator _salienceEvaluator;
        private readonly EphemeralStateRecomputer _recomputer;

        public event Action<SalienceAssessment, EventRecord> OnCognitiveTriggerFired;

        public WorldSimulationEngine(
            WorldMemoryStore store,
            LivingLsAiBridge bridge,
            TeleportPocketManager pockets = null,
            BlipManager blips = null,
            SalienceEvaluator salience = null,
            EphemeralStateRecomputer recomputer = null)
        {
            _store = store ?? new WorldMemoryStore();
            _bridge = bridge ?? new LivingLsAiBridge(_store);
            _pocketManager = pockets;
            _blipManager = blips;
            _salienceEvaluator = salience ?? new SalienceEvaluator();
            _recomputer = recomputer ?? new EphemeralStateRecomputer();
        }

        public WorldMemoryStore Store { get { return _store; } }
        public LivingLsAiBridge Bridge { get { return _bridge; } }
        public SalienceEvaluator Salience { get { return _salienceEvaluator; } }
        public EphemeralStateRecomputer Recomputer { get { return _recomputer; } }

        public NpcRecord ProcessInteractionWithWitnesses(int npcId, string actor, string eventType, float severity, string description, string sentimentTag, List<WitnessRecord> witnesses)
        {
            var npc = _store.GetNpcById(npcId);
            if (npc == null) return null;

            int deltaTrust = 0;
            int deltaFear = 0;
            int deltaRespect = 0;

            string type = (eventType ?? "").ToUpperInvariant();

            if (type.Contains("SECRET") || type.Contains("EXPOSITION") || type.Contains("ACCUSATION"))
            {
                deltaFear = (int)(severity * 50f);
                deltaTrust = -(int)(severity * 60f);
                deltaRespect = (int)(severity * 10f);

                // Epistemic belief update: NPC realizes Player knows their secret
                _store.AddOrUpdateBelief(npcId, "Player", "KNOWS_SECRET", npc.HiddenRole ?? "Secret", 0.95f, "Direct_Accusation");

                // If NPC is an informant or spy, transition goal to alert contact
                if (npc.HiddenRole.ToLowerInvariant().Contains("informant") || npc.Secrets.Exists(s => s.ToLowerInvariant().Contains("informant")))
                {
                    _store.SetNpcGoal(npcId, "ALERT_POLICE_CONTACT", "police_contact", 9, "Quietly alert police detective that player knows secret identity");
                }
                else
                {
                    _store.SetNpcGoal(npcId, "CONCEAL_EVIDENCE", "stash", 7, "Attempt to hide incriminating records");
                }

                // Dynamically reveal secret hideouts / stashes connected to this secret
                RevealSecretsInWorld(npc);
            }
            else if (type.Contains("THREAT") || type.Contains("INTIMIDATION") || type.Contains("GUN_AIM"))
            {
                deltaFear = (int)(severity * 60f);
                deltaTrust = -(int)(severity * 50f);
                deltaRespect = (int)(severity * 20f);

                _store.AddOrUpdateBelief(npcId, "Player", "IS_DANGEROUS", "High", 0.90f, "Direct_Threat");

                if (npc.Fear >= 60)
                {
                    _store.SetNpcGoal(npcId, "SEEK_PROTECTION", "allies", 8, "Seek protection from cartel enforcers or law enforcement");
                }
            }
            else if (type.Contains("BRIBE") || type.Contains("FAVOR") || type.Contains("GIFT"))
            {
                deltaTrust = (int)(severity * 70f);
                deltaFear = -(int)(severity * 20f);
                deltaRespect = (int)(severity * 30f);

                _store.AddOrUpdateBelief(npcId, "Player", "IS_RELIABLE_PARTNER", "True", 0.85f, "Bribe_Accepted");
                _store.SetNpcGoal(npcId, "COOPERATE_WITH_PLAYER", "Player", 6, "Assist player in current operation");

                // Bribe unlocks cooperation and secret entrances
                RevealSecretsInWorld(npc);
            }
            else if (type.Contains("ASSAULT") || type.Contains("DAMAGE"))
            {
                deltaFear = (int)(severity * 80f);
                deltaTrust = -(int)(severity * 90f);
                _store.AddOrUpdateBelief(npcId, "Player", "IS_HOSTILE_ENEMY", "Lethal", 1.0f, "Physical_Assault");
            }
            else if (type.Contains("INSULT"))
            {
                deltaTrust = -30;
                deltaRespect = -40;
            }

            var nextState = NpcStateMachine.EvaluateNewState(npc, eventType, severity);
            string stateStr = nextState.ToString().ToUpperInvariant();

            _store.UpdateNpcEmotionalState(npcId, deltaTrust, deltaFear, deltaRespect, stateStr);

            List<string> consequences = new List<string>();
            consequences.Add(string.Format("State changed to {0}", stateStr));
            if (deltaFear > 0) consequences.Add(string.Format("Fear increased by {0}%", deltaFear));
            if (deltaTrust != 0) consequences.Add(string.Format("Trust shifted by {0}", deltaTrust));
            if (npc.ActiveGoal != null) consequences.Add(string.Format("New Goal: {0}", npc.ActiveGoal.GoalType));

            string normalizedActor = string.Equals(actor, "player", StringComparison.OrdinalIgnoreCase) ? "Player" : actor;
            var recordedEvent = _store.RecordEventGraphEventWithWitnesses(npcId, normalizedActor, npc.Name, eventType, severity, description, sentimentTag, consequences, witnesses);

            if (_salienceEvaluator != null && recordedEvent != null)
            {
                var assessment = _salienceEvaluator.EvaluateEvent(recordedEvent, npc);
                if (assessment.IsCognitiveTrigger && OnCognitiveTriggerFired != null)
                {
                    OnCognitiveTriggerFired(assessment, recordedEvent);
                }
            }

            _bridge.SyncAllNpcFactsToCustomRules(new int[] { npcId }, false);

            return _store.GetNpcById(npcId);
        }

        private void RevealSecretsInWorld(NpcRecord npc)
        {
            if (npc == null) return;

            if (_pocketManager != null)
            {
                if (npc.Secrets != null)
                {
                    foreach (var s in npc.Secrets)
                    {
                        _pocketManager.RevealPocketToPlayer(s);
                        _pocketManager.UnlockPocket(s);
                        if (_blipManager != null) _blipManager.RevealSecretBlip("global", s);
                    }
                }
                if (!string.IsNullOrEmpty(npc.HiddenRole) && !string.Equals(npc.HiddenRole, "None", StringComparison.OrdinalIgnoreCase))
                {
                    _pocketManager.RevealPocketToPlayer(npc.HiddenRole);
                    _pocketManager.UnlockPocket(npc.HiddenRole);
                    if (_blipManager != null) _blipManager.RevealSecretBlip("global", npc.HiddenRole);
                }
            }
        }

        public NpcRecord ProcessInteraction(int npcId, string actor, string eventType, float severity, string description, string sentimentTag = "neutral")
        {
            return ProcessInteractionWithWitnesses(npcId, actor, eventType, severity, description, sentimentTag, null);
        }

        public List<EventRecord> PerformAutonomousWorldTick()
        {
            List<EventRecord> generatedEvents = new List<EventRecord>();
            var allNpcs = _store.GetAllNpcs();

            foreach (var npc in allNpcs)
            {
                if (npc == null || !npc.IsAlive || npc.ActiveGoal == null) continue;

                // Agent Goal Resolution
                if (npc.ActiveGoal.GoalType == "ALERT_POLICE_CONTACT" && npc.ActiveGoal.Status == "ACTIVE")
                {
                    npc.ActiveGoal.Status = "COMPLETED";
                    _store.Save(false);

                    var evt = _store.RecordEventGraphEvent(
                        npc.NpcId,
                        npc.Name,
                        "LSPD Detective",
                        "POLICE_ALERT",
                        0.85f,
                        string.Format("{0} sent an encrypted dispatch message to police regarding Player's threats", npc.Name),
                        "Alert_Dispatch",
                        new List<string> { "Police alerted to player actions", "Investigation initiated" }
                    );
                    generatedEvents.Add(evt);

                    if (_salienceEvaluator != null && evt != null)
                    {
                        var assessment = _salienceEvaluator.EvaluateEvent(evt, npc);
                        if (assessment.IsCognitiveTrigger && OnCognitiveTriggerFired != null)
                        {
                            OnCognitiveTriggerFired(assessment, evt);
                        }
                    }

                    var policeNpcs = allNpcs.FindAll(n => n != null && (n.PublicFaction == "Police" || (!string.IsNullOrEmpty(n.Occupation) && (n.Occupation.Contains("Police") || n.Occupation.Contains("Officer")))));
                    foreach (var cop in policeNpcs)
                    {
                        cop.Knowledge.Add(new KnowledgeEntry(
                            "PLAYER_SUSPECT",
                            string.Format("Informant {0} reported that Player is uncovering covert operations", npc.Name)
                        ));
                        _store.AddOrUpdateBelief(cop.NpcId, "Player", "IS_WANTED_SUSPECT", "High", 0.90f, "Informant_Tip");
                        _store.SetNpcGoal(cop.NpcId, "INVESTIGATE_PLAYER", "Player", 9, "Intercept and question Player");
                        _store.UpdateNpcEmotionalState(cop.NpcId, -20, 0, 10, "ALERT");
                    }
                }
                else if (npc.ActiveGoal.GoalType == "SEEK_PROTECTION" && npc.ActiveGoal.Status == "ACTIVE")
                {
                    npc.ActiveGoal.Status = "COMPLETED";
                    _store.Save(false);

                    var evt = _store.RecordEventGraphEvent(
                        npc.NpcId,
                        npc.Name,
                        "Faction Enforcers",
                        "CALL_BACKUP",
                        0.75f,
                        string.Format("{0} placed a call to {1} enforcers requesting armed backup", npc.Name, npc.PublicFaction),
                        "Reinforcements",
                        new List<string> { "Enforcers notified of hostility" }
                    );
                    generatedEvents.Add(evt);

                    if (_salienceEvaluator != null && evt != null)
                    {
                        var assessment = _salienceEvaluator.EvaluateEvent(evt, npc);
                        if (assessment.IsCognitiveTrigger && OnCognitiveTriggerFired != null)
                        {
                            OnCognitiveTriggerFired(assessment, evt);
                        }
                    }
                }
            }

            return generatedEvents;
        }

        public EventRecord RecordNpcDeath(int npcId, string killer = "Unknown", string cause = "Fatal injuries", float distanceToPlayer = 0f)
        {
            var npc = _store.GetNpcById(npcId);
            if (npc == null) return null;

            _store.MarkNpcDead(npcId, killer, cause);

            var events = _store.GetRecentWorldEvents(1);
            EventRecord evt = events.Count > 0 ? events[0] : null;
            if (evt != null && _salienceEvaluator != null)
            {
                var assessment = _salienceEvaluator.EvaluateEvent(evt, npc, distanceToPlayer);
                if (assessment.IsCognitiveTrigger && OnCognitiveTriggerFired != null)
                {
                    OnCognitiveTriggerFired(assessment, evt);
                }
            }
            return evt;
        }

        public bool MutateNpcRole(int npcId, string newRole, string reason)
        {
            var npc = _store.GetNpcById(npcId);
            if (npc == null) return false;

            _store.MutateRole(npcId, newRole, reason);
            _bridge.SyncAllNpcFactsToCustomRules(new int[] { npcId }, false);
            return true;
        }

        public EphemeralNpcPosture GetEffectiveNpcPosture(int npcId, DateTime? asOf = null)
        {
            var npc = _store.GetNpcById(npcId);
            if (npc == null) return null;
            return _recomputer.RecomputeNpcPosture(npc, asOf);
        }

        public LocationHeatAssessment GetLocationHeat(float x, float y, float z, float radiusMeters = 75f, DateTime? asOf = null)
        {
            var events = _store.GetRecentWorldEvents(50);
            return _recomputer.RecomputeLocationHeat(x, y, z, events, radiusMeters, asOf);
        }

        public FactionTensionReport GetFactionHostility(string factionA, string factionB, DateTime? asOf = null)
        {
            var events = _store.GetRecentWorldEvents(50);
            return _recomputer.RecomputeFactionHostility(factionA, factionB, events, asOf);
        }

        public void CompactNpcHistories(int maxRetained = 15)
        {
            var npcs = _store.GetAllNpcs();
            foreach (var npc in npcs)
            {
                if (npc != null && npc.History != null && npc.History.Count > maxRetained)
                {
                    npc.History = _recomputer.CompactEventHistory(npc.History, maxRetained);
                }
            }
        }
    }
}
