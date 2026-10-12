using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Database
{
    public class WorldMemoryStore
    {
        private readonly string _filePath;
        private readonly object _lock = new object();
        private DatabaseState _state;

        private class DatabaseState
        {
            public int NextNpcId { get; set; }
            public int NextEventId { get; set; }
            public int NextPropId { get; set; }
            public List<NpcRecord> Npcs { get; set; }
            public List<PropRecord> Props { get; set; }
            public List<EnvironmentRecord> Environments { get; set; }
            public List<EventRecord> WorldEvents { get; set; }

            public DatabaseState()
            {
                NextNpcId = 1000;
                NextEventId = 1;
                NextPropId = 5000;
                Npcs = new List<NpcRecord>();
                Props = new List<PropRecord>();
                Environments = new List<EnvironmentRecord>();
                WorldEvents = new List<EventRecord>();
            }
        }

        public WorldMemoryStore(string dbDirectory)
        {
            if (string.IsNullOrEmpty(dbDirectory))
            {
                dbDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }

            Directory.CreateDirectory(dbDirectory);
            _filePath = Path.Combine(dbDirectory, "world_memory.json");
            LoadDatabase();
        }

        public WorldMemoryStore() : this(null)
        {
        }

        private void LoadDatabase()
        {
            lock (_lock)
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(_filePath);
                        _state = JsonConvert.DeserializeObject<DatabaseState>(json) ?? new DatabaseState();
                        if (_state.Npcs == null) _state.Npcs = new List<NpcRecord>();
                        if (_state.Props == null) _state.Props = new List<PropRecord>();
                        if (_state.Environments == null) _state.Environments = new List<EnvironmentRecord>();
                        if (_state.WorldEvents == null) _state.WorldEvents = new List<EventRecord>();
                        if (_state.NextPropId <= 0) _state.NextPropId = 5000;

                        // Ensure nested collections are initialized for backwards compatibility
                        foreach (var npc in _state.Npcs)
                        {
                            if (npc.Knowledge == null) npc.Knowledge = new List<KnowledgeEntry>();
                            if (npc.Beliefs == null) npc.Beliefs = new List<BeliefEntry>();
                            if (npc.Secrets == null) npc.Secrets = new List<string>();
                            if (npc.History == null) npc.History = new List<EventRecord>();
                            if (npc.Relationships == null) npc.Relationships = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            if (npc.ActiveGoal == null) npc.ActiveGoal = new NpcGoal("MAINTAIN_ROUTINE", "self", 1, "Conduct daily routine");
                        }

                        foreach (var env in _state.Environments)
                        {
                            if (env.ResidentEntityIds == null) env.ResidentEntityIds = new List<string>();
                            if (env.NpcIds == null) env.NpcIds = new List<int>();
                            if (env.PropIds == null) env.PropIds = new List<int>();
                            if (env.PortalStates == null) env.PortalStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                            if (env.ContainerStates == null) env.ContainerStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            if (string.IsNullOrEmpty(env.Status)) env.Status = "COMMISSIONED";
                            if (string.IsNullOrEmpty(env.PlanId)) env.PlanId = env.EnvironmentId;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[WorldMemoryStore] Warning: Failed to load DB ({0}). Creating fresh state.", ex.Message);
                        _state = new DatabaseState();
                    }
                }
                else
                {
                    _state = new DatabaseState();
                    SaveDatabaseInternal();
                }
            }
        }

        private DateTime _lastDbSaveTime = DateTime.MinValue;
        private readonly TimeSpan _minDbSaveInterval = TimeSpan.FromMilliseconds(1000);
        private bool _isDbDirty = false;

        private void SaveDatabaseInternal()
        {
            string json = JsonConvert.SerializeObject(_state, Formatting.Indented);
            File.WriteAllText(_filePath, json);
            _isDbDirty = false;
            _lastDbSaveTime = DateTime.UtcNow;
        }

        public void Save(bool force = false)
        {
            lock (_lock)
            {
                DateTime now = DateTime.UtcNow;
                if (!force && (now - _lastDbSaveTime) < _minDbSaveInterval)
                {
                    _isDbDirty = true;
                    return;
                }
                SaveDatabaseInternal();
            }
        }

        public void FlushIfDirty()
        {
            lock (_lock)
            {
                if (_isDbDirty && (DateTime.UtcNow - _lastDbSaveTime) >= _minDbSaveInterval)
                {
                    SaveDatabaseInternal();
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                if (_isDbDirty)
                {
                    SaveDatabaseInternal();
                }
            }
        }

        // --- NPC OPERATIONS ---

        public NpcRecord CreateNpc(string name, string occupation, string publicFaction, string pedModel, string hiddenRole, string entityId = null)
        {
            lock (_lock)
            {
                var npc = new NpcRecord
                {
                    NpcId = _state.NextNpcId++,
                    EntityId = entityId ?? "",
                    Name = name,
                    Occupation = occupation,
                    PublicFaction = publicFaction,
                    PedModel = pedModel,
                    HiddenRole = string.IsNullOrEmpty(hiddenRole) ? "None" : hiddenRole,
                    CurrentState = "CALM"
                };

                if (!string.IsNullOrEmpty(hiddenRole) && !string.Equals(hiddenRole, "None", StringComparison.OrdinalIgnoreCase))
                {
                    npc.Secrets.Add(hiddenRole);
                }

                _state.Npcs.Add(npc);
                Save(false);
                return npc;
            }
        }

        public NpcRecord CreateNpc(string name, string occupation, string publicFaction, string pedModel)
        {
            return CreateNpc(name, occupation, publicFaction, pedModel, "None", null);
        }

        public NpcRecord EnrollNpc(NpcRecord record)
        {
            if (record == null) return null;
            lock (_lock)
            {
                if (record.NpcId <= 0)
                {
                    record.NpcId = _state.NextNpcId++;
                }
                _state.Npcs.Add(record);
                Save(false);
                return record;
            }
        }

        public NpcRecord GetNpcById(int npcId)
        {
            lock (_lock)
            {
                return _state.Npcs.FirstOrDefault(n => n.NpcId == npcId);
            }
        }

        public NpcRecord GetNpc(int npcId)
        {
            return GetNpcById(npcId);
        }

        public NpcRecord GetNpcByEntityId(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return null;
            lock (_lock)
            {
                return _state.Npcs.FirstOrDefault(n => 
                    string.Equals(n.EntityId, entityId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n.Guid, entityId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public NpcRecord GetNpcByName(string name)
        {
            lock (_lock)
            {
                return _state.Npcs.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public List<NpcRecord> GetAllNpcs()
        {
            lock (_lock)
            {
                return new List<NpcRecord>(_state.Npcs);
            }
        }

        public void UpdateRelationship(int npcId, string targetEntity, int standingValue)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    npc.Relationships[targetEntity] = standingValue;
                    Save(false);
                }
            }
        }

        public void UpdateNpcEmotionalState(int npcId, int deltaTrust, int deltaFear, int deltaRespect, string newState = null)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    npc.Trust = Math.Max(-100, Math.Min(100, npc.Trust + deltaTrust));
                    npc.Fear = Math.Max(0, Math.Min(100, npc.Fear + deltaFear));
                    npc.Respect = Math.Max(-100, Math.Min(100, npc.Respect + deltaRespect));

                    if (!string.IsNullOrEmpty(newState))
                    {
                        npc.CurrentState = newState;
                    }

                    // Update Player relationship derived from Trust & Fear
                    int calculatedPlayerScore = npc.Trust - (npc.Fear / 3);
                    npc.Relationships["Player"] = calculatedPlayerScore;

                    Save(false);
                }
            }
        }

        public void MutateRole(int npcId, string newHiddenRole, string eventReason)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    npc.HiddenRole = newHiddenRole;
                    if (!npc.Secrets.Contains(newHiddenRole))
                    {
                        npc.Secrets.Add(newHiddenRole);
                    }

                    if (!string.IsNullOrEmpty(eventReason))
                    {
                        AddEvent(npcId, eventReason, "Role_Mutation");
                    }
                    else
                    {
                        Save(false);
                    }
                }
            }
        }

        public void MutateRole(int npcId, string newHiddenRole)
        {
            MutateRole(npcId, newHiddenRole, null);
        }

        public bool MarkNpcDead(int npcId, string killer = "Unknown", string cause = "Fatal injuries")
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc == null || !npc.IsAlive) return false;

                npc.IsAlive = false;
                npc.CurrentState = "DEAD";
                npc.ActiveGoal = new NpcGoal("DEAD", "none", 0, "Deceased");

                var consequences = new List<string> { "Deceased", "Permanent_Casualty" };
                RecordEventGraphEvent(
                    npcId,
                    killer ?? "Unknown",
                    npc.Name,
                    "ENTITY_DIED",
                    1.0f,
                    string.Format("{0} died ({1}).", npc.Name, cause ?? "Fatal injuries"),
                    "tragedy",
                    consequences
                );

                // Irrevocable terminal semantic transition: force durable synchronous persistence to disk immediately
                Save(true);
                return true;
            }
        }

        public bool MarkNpcDeadByEntityId(string entityId, string killer = "Unknown", string cause = "Fatal injuries")
        {
            if (string.IsNullOrEmpty(entityId)) return false;
            var npc = GetNpcByEntityId(entityId);
            if (npc != null)
            {
                return MarkNpcDead(npc.NpcId, killer, cause);
            }
            return false;
        }

        // --- EPISTEMIC & GOAL OPERATIONS ---

        public void AddKnowledge(int npcId, string factKey, string description, string sourceEventId = "")
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    if (!npc.Knowledge.Any(k => string.Equals(k.FactKey, factKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        npc.Knowledge.Add(new KnowledgeEntry(factKey, description, sourceEventId));
                        Save(false);
                    }
                }
            }
        }

        public void AddOrUpdateBelief(int npcId, string subject, string predicate, string objectValue, float confidence, string source)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    var existing = npc.Beliefs.FirstOrDefault(b => 
                        string.Equals(b.Subject, subject, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(b.Predicate, predicate, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        existing.ObjectValue = objectValue;
                        existing.Confidence = confidence;
                        existing.Source = source;
                        existing.Timestamp = DateTime.UtcNow;
                    }
                    else
                    {
                        npc.Beliefs.Add(new BeliefEntry(subject, predicate, objectValue, confidence, source));
                    }
                    Save(false);
                }
            }
        }

        public void SetNpcGoal(int npcId, string goalType, string targetEntity, int priority, string description)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc != null)
                {
                    npc.ActiveGoal = new NpcGoal(goalType, targetEntity, priority, description);
                    Save(false);
                }
            }
        }

        // --- FIRST-CLASS EVENT OPERATIONS ---

        public EventRecord AddEvent(int npcId, string description, string sentimentTag)
        {
            return RecordEventGraphEvent(npcId, "Player", "npc", "INTERACTION", 0.5f, description, sentimentTag, null);
        }

        public EventRecord RecordEventGraphEvent(int npcId, string actor, string target, string eventType, float severity, string description, string sentimentTag, List<string> consequences)
        {
            return RecordEventGraphEventWithWitnesses(npcId, actor, target, eventType, severity, description, sentimentTag, consequences, null);
        }

        public EventRecord RecordEventGraphEventWithWitnesses(int primaryNpcId, string actor, string target, string eventType, float severity, string description, string sentimentTag, List<string> consequences, List<WitnessRecord> witnesses)
        {
            lock (_lock)
            {
                var evt = new EventRecord
                {
                    EventId = _state.NextEventId++,
                    NpcId = primaryNpcId,
                    Actor = actor,
                    Target = target,
                    EventType = eventType,
                    Severity = severity,
                    Description = description,
                    SentimentTag = sentimentTag,
                    Consequences = consequences ?? new List<string>(),
                    Witnesses = witnesses ?? new List<WitnessRecord>(),
                    Timestamp = DateTime.UtcNow
                };

                // Add to central WorldEvents list (capped to last 50)
                _state.WorldEvents.Add(evt);
                while (_state.WorldEvents.Count > 50)
                {
                    _state.WorldEvents.RemoveAt(0);
                }

                // Add to Primary NPC
                var primaryNpc = GetNpcById(primaryNpcId);
                if (primaryNpc != null)
                {
                    primaryNpc.History.Add(evt);
                    while (primaryNpc.History.Count > 15)
                    {
                        primaryNpc.History.RemoveAt(0);
                    }
                }

                // Propagate to Witnesses
                if (witnesses != null)
                {
                    foreach (var w in witnesses)
                    {
                        if (w.NpcId == primaryNpcId) continue;
                        var wNpc = GetNpcById(w.NpcId);
                        if (wNpc != null)
                        {
                            wNpc.History.Add(evt);
                            while (wNpc.History.Count > 15)
                            {
                                wNpc.History.RemoveAt(0);
                            }

                            // Epistemic update for witness based on perception level
                            if (w.Perception == PerceptionType.DirectSight)
                            {
                                wNpc.Knowledge.Add(new KnowledgeEntry(
                                    string.Format("{0}_{1}_{2}", actor, eventType, target),
                                    string.Format("Witnessed {0} commit {1} against {2}", actor, eventType, target),
                                    evt.EventId.ToString()
                                ));
                                AddOrUpdateBelief(wNpc.NpcId, actor, "IS_HOSTILE_TO", target, 0.95f, "Direct_Witness");
                            }
                            else if (w.Perception == PerceptionType.AuditoryOnly)
                            {
                                AddOrUpdateBelief(wNpc.NpcId, target, "HEARD_COMMOTION_NEAR", "Nearby", 0.60f, "Auditory_Witness");
                            }
                        }
                    }
                }

                Save(false);
                return evt;
            }
        }

        public EventRecord RecordEventGraphEventWithWitnesses(int primaryNpcId, string actor, string target, string eventType, float severity, string description, string sentimentTag, List<string> consequences, List<WitnessRecord> witnesses, float posX, float posY, float posZ)
        {
            var evt = RecordEventGraphEventWithWitnesses(primaryNpcId, actor, target, eventType, severity, description, sentimentTag, consequences, witnesses);
            if (evt != null)
            {
                evt.PositionX = posX;
                evt.PositionY = posY;
                evt.PositionZ = posZ;
                evt.HasPosition = true;
            }
            return evt;
        }

        public List<EventRecord> GetRelevantEventHistorySlice(int npcId, int maxCount = 4)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc == null || npc.History == null) return new List<EventRecord>();
                return npc.History.OrderByDescending(h => h.Timestamp).Take(maxCount).ToList();
            }
        }

        public List<EventRecord> GetRecentWorldEvents(int maxCount = 10)
        {
            lock (_lock)
            {
                return _state.WorldEvents.OrderByDescending(e => e.Timestamp).Take(maxCount).ToList();
            }
        }

        public List<EventRecord> GetAllEvents()
        {
            lock (_lock)
            {
                return new List<EventRecord>(_state.WorldEvents);
            }
        }

        // --- PROP OPERATIONS ---

        public PropRecord CreateProp(string modelName, float x, float y, float z, float heading, float pitch, float roll, bool isDynamic, string envId)
        {
            lock (_lock)
            {
                var prop = new PropRecord
                {
                    PropId = _state.NextPropId++,
                    ModelName = modelName,
                    PositionX = x,
                    PositionY = y,
                    PositionZ = z,
                    Heading = heading,
                    Pitch = pitch,
                    Roll = roll,
                    IsDynamic = isDynamic,
                    EnvironmentId = envId
                };

                _state.Props.Add(prop);
                Save(false);
                return prop;
            }
        }

        public PropRecord GetPropById(int propId)
        {
            lock (_lock)
            {
                return _state.Props.FirstOrDefault(p => p.PropId == propId);
            }
        }

        public List<PropRecord> GetAllProps()
        {
            lock (_lock)
            {
                return new List<PropRecord>(_state.Props);
            }
        }

        public List<PropRecord> GetPropsByEnvironment(string envId)
        {
            lock (_lock)
            {
                return _state.Props.Where(p => string.Equals(p.EnvironmentId, envId, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public bool DeleteProp(int propId)
        {
            lock (_lock)
            {
                var prop = GetPropById(propId);
                if (prop != null)
                {
                    _state.Props.Remove(prop);
                    Save(false);
                    return true;
                }
                return false;
            }
        }

        // --- ENVIRONMENT OPERATIONS ---

        public EnvironmentRecord CreateEnvironment(
            string envId,
            string name,
            string archetype,
            string desc,
            float cx,
            float cy,
            float cz,
            float heading,
            string planId = null,
            string status = "COMMISSIONED")
        {
            lock (_lock)
            {
                var env = new EnvironmentRecord
                {
                    EnvironmentId = string.IsNullOrEmpty(envId) ? ("env_" + Guid.NewGuid().ToString().Substring(0, 8)) : envId,
                    PlanId = string.IsNullOrEmpty(planId) ? envId : planId,
                    Name = name,
                    Archetype = archetype,
                    Description = desc,
                    CenterX = cx,
                    CenterY = cy,
                    CenterZ = cz,
                    Heading = heading,
                    Status = string.IsNullOrEmpty(status) ? "COMMISSIONED" : status
                };

                _state.Environments.Add(env);
                Save(false);
                return env;
            }
        }

        public EnvironmentRecord EnrollEnvironment(EnvironmentRecord env)
        {
            if (env == null) return null;
            lock (_lock)
            {
                if (string.IsNullOrEmpty(env.EnvironmentId))
                {
                    env.EnvironmentId = "env_" + Guid.NewGuid().ToString().Substring(0, 8);
                }
                _state.Environments.Add(env);
                Save(false);
                return env;
            }
        }

        public EnvironmentRecord GetEnvironmentById(string envId)
        {
            lock (_lock)
            {
                return _state.Environments.FirstOrDefault(e => string.Equals(e.EnvironmentId, envId, StringComparison.OrdinalIgnoreCase));
            }
        }

        public EnvironmentRecord GetEnvironment(string envId)
        {
            return GetEnvironmentById(envId);
        }

        public bool HasEnvironment(string envId)
        {
            if (string.IsNullOrEmpty(envId)) return false;
            return GetEnvironmentById(envId) != null;
        }

        public List<EnvironmentRecord> GetAllEnvironments()
        {
            lock (_lock)
            {
                return new List<EnvironmentRecord>(_state.Environments);
            }
        }

        public bool UpdateEnvironmentStatus(string envId, string status, bool forceSave = false)
        {
            lock (_lock)
            {
                var env = GetEnvironmentById(envId);
                if (env != null)
                {
                    env.Status = status;
                    if (string.Equals(status, "REALIZED", StringComparison.OrdinalIgnoreCase))
                    {
                        env.LastRealizedAt = DateTime.UtcNow;
                    }
                    else if (string.Equals(status, "DEREALIZED", StringComparison.OrdinalIgnoreCase))
                    {
                        env.LastDerealizedAt = DateTime.UtcNow;
                    }
                    Save(forceSave);
                    return true;
                }
                return false;
            }
        }

        public bool UpdatePortalState(string envId, string portalId, bool isLocked, bool forceSave = false)
        {
            lock (_lock)
            {
                var env = GetEnvironmentById(envId);
                if (env != null)
                {
                    if (env.PortalStates == null)
                    {
                        env.PortalStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    }
                    env.PortalStates[portalId] = isLocked;
                    Save(forceSave);
                    return true;
                }
                return false;
            }
        }

        public bool UpdateContainerState(string envId, string containerId, string state, bool forceSave = false)
        {
            lock (_lock)
            {
                var env = GetEnvironmentById(envId);
                if (env != null)
                {
                    if (env.ContainerStates == null)
                    {
                        env.ContainerStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    env.ContainerStates[containerId] = state;
                    Save(forceSave);
                    return true;
                }
                return false;
            }
        }

        public bool UpdateScenarioState(string envId, string scenarioState, bool forceSave = false)
        {
            lock (_lock)
            {
                var env = GetEnvironmentById(envId);
                if (env != null)
                {
                    env.ScenarioState = scenarioState;
                    Save(forceSave);
                    return true;
                }
                return false;
            }
        }

        public bool DeleteEnvironment(string envId)
        {
            lock (_lock)
            {
                var env = GetEnvironmentById(envId);
                if (env != null)
                {
                    _state.Props.RemoveAll(p => string.Equals(p.EnvironmentId, envId, StringComparison.OrdinalIgnoreCase));
                    _state.Environments.Remove(env);
                    Save(false);
                    return true;
                }
                return false;
            }
        }

        public string FormatNpcContextForLivingLSAIs(int npcId)
        {
            lock (_lock)
            {
                var npc = GetNpcById(npcId);
                if (npc == null) return string.Empty;

                var relList = new List<string>();
                foreach (var kvp in npc.Relationships)
                {
                    relList.Add(string.Format("{0}:{1}{2}", kvp.Key, kvp.Value >= 0 ? "+" : "", kvp.Value));
                }
                string rels = string.Join(",", relList.ToArray());

                string goalStr = (npc.ActiveGoal != null) ? string.Format("Goal={0}", npc.ActiveGoal.GoalType) : "Goal=IDLE";

                string context = string.Format("[NPC #{0} {1} ({2}/{3})] {4} | {5} | Fear:{6}% Trust:{7}", 
                    npc.NpcId, npc.Name, npc.PublicFaction, npc.Occupation, npc.CurrentState, goalStr, npc.Fear, npc.Trust);

                if (npc.Secrets != null && npc.Secrets.Count > 0)
                {
                    context += string.Format(" | Secret:{0}", string.Join(";", npc.Secrets.ToArray()));
                }
                else if (!string.Equals(npc.HiddenRole, "None", StringComparison.OrdinalIgnoreCase))
                {
                    context += string.Format(" | Secret:{0}", npc.HiddenRole);
                }

                if (npc.Beliefs != null && npc.Beliefs.Count > 0)
                {
                    var beliefList = new List<string>();
                    foreach (var b in npc.Beliefs.Take(2))
                    {
                        beliefList.Add(string.Format("{0} {1}", b.Subject, b.Predicate));
                    }
                    context += string.Format(" | Beliefs:[{0}]", string.Join(";", beliefList.ToArray()));
                }

                if (!string.IsNullOrEmpty(rels))
                {
                    context += string.Format(" | Rel:{0}", rels);
                }

                var recentEvents = GetRelevantEventHistorySlice(npcId, 1);
                if (recentEvents.Count > 0)
                {
                    context += string.Format(" | Last:\"{0}\"", recentEvents[0].Description);
                }

                return context.Trim();
            }
        }
    }
}
