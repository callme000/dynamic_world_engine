using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Database
{
    public enum PerceptionType
    {
        DirectSight,    // Full visual observation (high confidence 0.9 - 1.0)
        AuditoryOnly,   // Heard gunshots / commotion but no visual (confidence 0.4 - 0.7)
        Inferred,       // Inferred from aftermath / blood / sirens (confidence 0.3 - 0.5)
        Rumor           // Second-hand information passed between NPCs (confidence 0.2 - 0.6)
    }

    public class WitnessRecord
    {
        public int NpcId { get; set; }
        public PerceptionType Perception { get; set; }
        public float Confidence { get; set; }
        public float DistanceToEvent { get; set; }

        public WitnessRecord()
        {
            Perception = PerceptionType.DirectSight;
            Confidence = 1.0f;
            DistanceToEvent = 0f;
        }

        public WitnessRecord(int npcId, PerceptionType perception, float confidence, float distance)
        {
            NpcId = npcId;
            Perception = perception;
            Confidence = confidence;
            DistanceToEvent = distance;
        }
    }

    public class BeliefEntry
    {
        public string Subject { get; set; }      // e.g. "Marco", "Player"
        public string Predicate { get; set; }    // e.g. "IS_INFORMANT", "IS_DANGEROUS", "OWES_MONEY"
        public string ObjectValue { get; set; }  // e.g. "POLICE", "HIGH", "TRUE"
        public float Confidence { get; set; }    // 0.0 to 1.0
        public string Source { get; set; }       // "Direct_Observation", "Rumor_From_Boris", "Inference"
        public DateTime Timestamp { get; set; }

        public BeliefEntry()
        {
            Confidence = 0.5f;
            Timestamp = DateTime.UtcNow;
        }

        public BeliefEntry(string subject, string predicate, string objectValue, float confidence, string source)
        {
            Subject = subject;
            Predicate = predicate;
            ObjectValue = objectValue;
            Confidence = confidence;
            Source = source;
            Timestamp = DateTime.UtcNow;
        }
    }

    public class KnowledgeEntry
    {
        public string FactKey { get; set; }      // e.g. "MARCO_INFORMANT", "PLAYER_HAS_MICROSMG"
        public string Description { get; set; }
        public string SourceEventId { get; set; }
        public DateTime AcquiredAt { get; set; }

        public KnowledgeEntry()
        {
            AcquiredAt = DateTime.UtcNow;
        }

        public KnowledgeEntry(string factKey, string description, string sourceEventId = "")
        {
            FactKey = factKey;
            Description = description;
            SourceEventId = sourceEventId;
            AcquiredAt = DateTime.UtcNow;
        }
    }

    public class NpcGoal
    {
        public string GoalId { get; set; }
        public string GoalType { get; set; }     // e.g. "PROTECT_BUSINESS", "ALERT_CONTACT", "INVESTIGATE_PLAYER", "FLEE_CITY"
        public string TargetEntity { get; set; } // e.g. "player", "police_contact", "nightclub"
        public int Priority { get; set; }        // 1 to 10
        public string Status { get; set; }       // "ACTIVE", "SUSPENDED", "COMPLETED", "FAILED"
        public string Description { get; set; }

        public NpcGoal()
        {
            GoalId = System.Guid.NewGuid().ToString().Substring(0, 8);
            GoalType = "IDLE";
            Priority = 1;
            Status = "ACTIVE";
            Description = "Observe surroundings";
        }

        public NpcGoal(string goalType, string targetEntity, int priority, string description)
        {
            GoalId = System.Guid.NewGuid().ToString().Substring(0, 8);
            GoalType = goalType;
            TargetEntity = targetEntity;
            Priority = priority;
            Status = "ACTIVE";
            Description = description;
        }
    }

    public class NpcRecord
    {
        public int NpcId { get; set; }
        public string EntityId { get; set; }
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Occupation { get; set; }
        public string PublicFaction { get; set; }
        public string HiddenRole { get; set; }
        public string PedModel { get; set; }
        public bool IsAlive { get; set; }
        public DateTime CreatedAt { get; set; }

        // Dynamic Emotional & Behavioral State
        public int Trust { get; set; }      // -100 (complete hatred) to +100 (absolute devotion)
        public int Fear { get; set; }       // 0 (fearless) to 100 (terrorized panic)
        public int Respect { get; set; }    // -100 to +100
        public string CurrentState { get; set; } // "CALM", "ALERT", "THREATENED", "PANICKED", "HOSTILE", "SUBMISSIVE", "FLEEING", "ALLIED"

        public Dictionary<string, int> Relationships { get; set; }
        public List<EventRecord> History { get; set; }

        // --- EPISTEMIC LAYER ---
        public List<KnowledgeEntry> Knowledge { get; set; }  // Verified facts this NPC knows
        public List<BeliefEntry> Beliefs { get; set; }        // Subjective beliefs / suspicions
        public List<string> Secrets { get; set; }             // Hidden truths this NPC actively conceals
        
        // --- AGENCY LAYER ---
        public NpcGoal ActiveGoal { get; set; }               // Current operational agenda
        public NpcGoal LongTermGoal { get; set; }             // Long term aspiration

        public NpcRecord()
        {
            Guid = System.Guid.NewGuid().ToString();
            IsAlive = true;
            CreatedAt = DateTime.UtcNow;
            Trust = 0;
            Fear = 0;
            Respect = 0;
            CurrentState = "CALM";
            Relationships = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            History = new List<EventRecord>();
            Knowledge = new List<KnowledgeEntry>();
            Beliefs = new List<BeliefEntry>();
            Secrets = new List<string>();
            ActiveGoal = new NpcGoal("MAINTAIN_ROUTINE", "self", 1, "Conduct daily routine");
            LongTermGoal = new NpcGoal("PROSPER", "self", 5, "Maintain position and avoid trouble");
        }
    }

    public class EventRecord
    {
        public int EventId { get; set; }
        public int NpcId { get; set; }
        public string Actor { get; set; }       // "Player", "Marco", "Officer_Davis"
        public string Target { get; set; }      // "Marco", "Player", "Environment"
        public string EventType { get; set; }   // "THREAT", "BRIBE", "ASSAULT", "ACCUSATION", "SECRET_REVEAL", "POLICE_ALERT"
        public float Severity { get; set; }     // 0.0 to 1.0
        public string Description { get; set; }
        public string SentimentTag { get; set; }
        public List<string> Consequences { get; set; }
        public List<WitnessRecord> Witnesses { get; set; } // First-class witness propagation
        public DateTime Timestamp { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public bool HasPosition { get; set; }

        public EventRecord()
        {
            Actor = "Player";
            Target = "npc";
            EventType = "INTERACTION";
            Severity = 0.5f;
            Consequences = new List<string>();
            Witnesses = new List<WitnessRecord>();
            Timestamp = DateTime.UtcNow;
            HasPosition = false;
        }
    }

    public class PropRecord
    {
        public int PropId { get; set; }
        public string ModelName { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float Heading { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }
        public bool IsDynamic { get; set; }
        public string EnvironmentId { get; set; }
        public DateTime CreatedAt { get; set; }

        public PropRecord()
        {
            IsDynamic = false;
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class EnvironmentRecord
    {
        public string EnvironmentId { get; set; }
        public string PlanId { get; set; }
        public string Name { get; set; }
        public string Archetype { get; set; }
        public string Description { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CenterZ { get; set; }
        public float Heading { get; set; }

        // Runtime lifecycle status: COMMISSIONED, REALIZED, DEREALIZED, ARCHIVED
        public string Status { get; set; }

        // Resident living identities (semantic string IDs)
        public List<string> ResidentEntityIds { get; set; }

        // Database integer IDs in WorldMemoryStore
        public List<int> NpcIds { get; set; }

        // Dynamic world state deltas (mutations relative to the plan)
        public Dictionary<string, bool> PortalStates { get; set; } // portalId -> isLocked
        public Dictionary<string, string> ContainerStates { get; set; } // containerId/propId -> state ("looted", "unlocked", etc.)
        public string ScenarioState { get; set; } // ongoing ambient scenario or scene tag

        public DateTime CreatedAt { get; set; }
        public DateTime? LastRealizedAt { get; set; }
        public DateTime? LastDerealizedAt { get; set; }

        // Backwards compatibility for legacy tests and callers
        [Obsolete("Props now belong to the CanonicalPlan. Query engine.Plans.GetPlan(env.PlanId).ResolvedProps instead.")]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int> PropIds { get; set; }

        public EnvironmentRecord()
        {
            Status = "COMMISSIONED";
            ResidentEntityIds = new List<string>();
            NpcIds = new List<int>();
            PropIds = new List<int>();
            PortalStates = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            ContainerStates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CreatedAt = DateTime.UtcNow;
        }
    }
}
