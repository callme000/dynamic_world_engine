using System;
using System.Collections.Generic;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Commands
{
    /// <summary>
    /// Executes validated action payloads against WorldMemoryStore.
    /// Decoupled from ActionParser (which handles pure schema deserialization).
    /// </summary>
    public class WorldActionExecutor
    {
        private readonly WorldMemoryStore _store;

        public WorldActionExecutor(WorldMemoryStore store)
        {
            _store = store ?? new WorldMemoryStore();
        }

        public WorldMemoryStore Store { get { return _store; } }

        public NpcRecord ExecuteSpawnNpcAction(ActionPayload payload)
        {
            if (payload == null || !string.Equals(payload.Action, "SPAWN_PERSISTENT_NPC", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid action type. Expected 'SPAWN_PERSISTENT_NPC'.");
            }

            var data = payload.Data;
            if (data == null)
            {
                throw new InvalidOperationException("Payload data block is missing.");
            }

            string name = string.IsNullOrEmpty(data.Name) ? "Unknown Ped" : data.Name;
            string occupation = string.IsNullOrEmpty(data.Occupation) ? "Civilian" : data.Occupation;
            string faction = string.IsNullOrEmpty(data.PublicFaction) ? "Civilian" : data.PublicFaction;
            string pedModel = data.PedModel;
            if (string.IsNullOrEmpty(pedModel))
            {
                var defaultPedQuery = new DynamicWorldEngine.Database.AssetQuery
                {
                    Type = DynamicWorldEngine.Database.AssetType.Ped,
                    Category = "CivilianPed"
                };
                var resolvedPed = DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.Resolve(defaultPedQuery);
                pedModel = (resolvedPed != null && resolvedPed.PrimaryAsset != null)
                    ? resolvedPed.PrimaryAsset.ModelName
                    : "a_m_y_beach_01";
            }
            string hiddenRole = string.IsNullOrEmpty(data.HiddenRole) ? "None" : data.HiddenRole;

            NpcRecord npc = _store.CreateNpc(name, occupation, faction, pedModel, hiddenRole, data.EntityId);

            if (data.Relationships != null)
            {
                foreach (var kvp in data.Relationships)
                {
                    _store.UpdateRelationship(npc.NpcId, kvp.Key, kvp.Value);
                }
            }

            if (data.InitialEvents != null)
            {
                foreach (var evt in data.InitialEvents)
                {
                    _store.AddEvent(npc.NpcId, evt.Description, evt.SentimentTag);
                }
            }

            return _store.GetNpcById(npc.NpcId);
        }

        public EnvironmentRecord ExecuteSpatialExecutionPlan(SpatialExecutionPlan plan, BuildEnvironmentData data = null)
        {
            if (plan == null) throw new ArgumentNullException("plan");

            string envId = plan.EnvironmentId;
            string name = plan.Name;
            string archetype = plan.Archetype;
            string desc = (data != null && !string.IsNullOrEmpty(data.Description)) ? data.Description : "";

            EnvironmentRecord env = _store.CreateEnvironment(
                envId, name, archetype, desc,
                plan.CenterPosition.X, plan.CenterPosition.Y, plan.CenterPosition.Z, plan.Heading,
                plan.EnvironmentId, "COMMISSIONED");

            // Register initial portal state from planned pockets if present
            if (plan.PlannedPocket != null)
            {
                env.PortalStates[plan.PlannedPocket.PocketId] = plan.PlannedPocket.IsLocked;
            }
            if (plan.ConnectedPockets != null)
            {
                foreach (var cp in plan.ConnectedPockets)
                {
                    if (!string.IsNullOrEmpty(cp.PocketId))
                    {
                        env.PortalStates[cp.PocketId] = cp.IsLocked;
                    }
                }
            }

            // NOTE: Physical geometry / props belong authoritatively to CanonicalPlan in PlanRepository!
            // They are no longer dumped into world_memory.json.

            if (plan.ResolvedNpcs != null)
            {
                foreach (var n in plan.ResolvedNpcs)
                {
                    NpcRecord npc = _store.CreateNpc(n.Name, n.Occupation, n.PublicFaction, n.PedModel, n.HiddenRole, n.EntityId);

                    if (n.Relationships != null)
                    {
                        foreach (var kvp in n.Relationships)
                        {
                            _store.UpdateRelationship(npc.NpcId, kvp.Key, kvp.Value);
                        }
                    }

                    if (n.InitialEvents != null)
                    {
                        foreach (var evt in n.InitialEvents)
                        {
                            _store.AddEvent(npc.NpcId, evt.Description, evt.SentimentTag);
                        }
                    }

                    env.NpcIds.Add(npc.NpcId);
                    if (!string.IsNullOrEmpty(n.EntityId))
                    {
                        env.ResidentEntityIds.Add(n.EntityId);
                    }
                }
            }

            _store.Save(true);
            return env;
        }

        public bool ExecuteClearEnvironmentAction(ClearEnvironmentPayload payload)
        {
            if (payload == null || payload.Data == null || string.IsNullOrEmpty(payload.Data.EnvironmentId))
            {
                return false;
            }

            return _store.DeleteEnvironment(payload.Data.EnvironmentId);
        }

        public bool ExecuteMutateEntityAction(MutateEntityPayload payload, out NpcRecord modifiedNpc)
        {
            modifiedNpc = null;
            if (payload == null || payload.Data == null) return false;

            var data = payload.Data;
            NpcRecord npc = null;

            int parsedId;
            if (int.TryParse(data.TargetEntityId, out parsedId))
            {
                npc = _store.GetNpcById(parsedId);
            }
            if (npc == null && !string.IsNullOrEmpty(data.TargetEntityId))
            {
                npc = _store.GetNpcByEntityId(data.TargetEntityId);
            }
            if (npc == null && !string.IsNullOrEmpty(data.TargetEntityId))
            {
                npc = _store.GetNpcByName(data.TargetEntityId);
            }

            if (npc == null)
            {
                Console.WriteLine(string.Format("[WorldActionExecutor] Warning: TargetEntityId '{0}' could not be matched.", data.TargetEntityId));
                return false;
            }

            if (data.RelationshipDelta != 0)
            {
                int current = npc.Relationships.ContainsKey("Player") ? npc.Relationships["Player"] : 0;
                _store.UpdateRelationship(npc.NpcId, "Player", current + data.RelationshipDelta);
            }

            if (!string.IsNullOrEmpty(data.NewHiddenRole))
            {
                _store.MutateRole(npc.NpcId, data.NewHiddenRole, data.NarrativeReason);
            }
            else if (!string.IsNullOrEmpty(data.NarrativeReason))
            {
                _store.AddEvent(npc.NpcId, data.NarrativeReason, "State_Mutation");
            }

            modifiedNpc = _store.GetNpcById(npc.NpcId);
            return true;
        }
    }
}
