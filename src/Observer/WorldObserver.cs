using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Observer
{
    public class WorldObserver
    {
        private readonly WorldSimulationEngine _simEngine;
        private readonly Dictionary<int, DateTime> _lastPedReaction = new Dictionary<int, DateTime>();

        public WorldObserver(WorldSimulationEngine simEngine)
        {
            _simEngine = simEngine;
        }

        public void ClearTracking()
        {
            _lastPedReaction.Clear();
        }

        public void ObserveWorld(Ped playerPed, List<ActiveNpcContext> activeContexts)
        {
            if (activeContexts == null) return;

            bool playerValid = false;
            try
            {
                playerValid = playerPed != null && playerPed.Exists();
            }
            catch
            {
                playerValid = false;
            }

            DateTime now = DateTime.UtcNow;

            for (int i = activeContexts.Count - 1; i >= 0; i--)
            {
                ActiveNpcContext ctx = activeContexts[i];
                if (ctx == null) continue;

                // Close #1 & #2: Disambiguate Mortality (Ped.IsDead == true) from Virtualization (!Ped.Exists())
                bool exists = ctx.CheckExists();
                bool isDead = ctx.CheckIsDead();

                if (exists && isDead)
                {
                    if (ctx.Record != null)
                    {
                        _lastPedReaction.Remove(ctx.Record.NpcId);

                        if (ctx.Record.IsAlive)
                        {
                            string killer = "Unknown";
                            try
                            {
                                if (playerPed != null && playerPed.Exists() && ctx.PedInstance != null && ctx.PedInstance.HasBeenDamagedBy(playerPed))
                                {
                                    killer = "Player";
                                }
                            }
                            catch { }

                            if (_simEngine != null)
                            {
                                float dist = 0f;
                                try
                                {
                                    if (playerPed != null && playerPed.Exists() && ctx.PedInstance != null)
                                    {
                                        dist = playerPed.Position.DistanceTo(ctx.PedInstance.Position);
                                    }
                                }
                                catch { }
                                _simEngine.RecordNpcDeath(ctx.Record.NpcId, killer, "Fatal injuries", dist);
                            }
                            ctx.Record.IsAlive = false;
                            ctx.Record.CurrentState = "DEAD";
                        }
                    }
                    continue;
                }

                // Virtualization / Engine Culling: PedInstance does not exist while not observed dead
                if (!exists)
                {
                    // Do NOT mutate IsAlive; this is ambient streaming/culling, not death.
                    continue;
                }

                // If record was already marked dead, skip behavior evaluation
                if (ctx.Record != null && !ctx.Record.IsAlive)
                {
                    continue;
                }

                Ped ped = ctx.PedInstance;
                NpcRecord record = ctx.Record;
                if (record == null) continue;

                // Rate-limit physical reactions per NPC (1.5 seconds)
                if (_lastPedReaction.ContainsKey(record.NpcId))
                {
                    if ((now - _lastPedReaction[record.NpcId]).TotalSeconds < 1.5)
                    {
                        continue;
                    }
                }

                if (!playerValid) continue;

                // Collect Witnesses for any physical interaction
                List<WitnessRecord> witnesses = CollectWitnesses(ped.Position, activeContexts, record.NpcId);

                // 1. Check if Player is Aiming a Gun directly at this Ped
                bool isAimingAtPed = Game.Player.IsAiming && Game.Player.TargetedEntity == ped;
                if (isAimingAtPed)
                {
                    _lastPedReaction[record.NpcId] = now;
                    _simEngine.ProcessInteractionWithWitnesses(
                        record.NpcId,
                        "Player",
                        "GUN_AIM",
                        0.85f,
                        "Player pointed a lethal weapon directly at " + record.Name,
                        "Threat_Physical",
                        witnesses
                    );
                    NpcStateMachine.ApplyPhysicalBehavior(ped, record, playerPed, ctx);
                    continue;
                }

                // 2. Check if Player has Damaged or Hit this Ped
                if (ped.HasBeenDamagedBy(playerPed))
                {
                    _lastPedReaction[record.NpcId] = now;
                    ped.ClearLastWeaponDamage();

                    _simEngine.ProcessInteractionWithWitnesses(
                        record.NpcId,
                        "Player",
                        "ASSAULT",
                        0.95f,
                        "Player physically attacked " + record.Name,
                        "Hostility_Extreme",
                        witnesses
                    );
                    NpcStateMachine.ApplyPhysicalBehavior(ped, record, playerPed, ctx);
                    continue;
                }

                // 3. Check if Player Fired a Weapon Close to this Ped (< 12 meters)
                if (playerPed.IsShooting && playerPed.Position.DistanceTo(ped.Position) < 12.0f)
                {
                    _lastPedReaction[record.NpcId] = now;
                    _simEngine.ProcessInteractionWithWitnesses(
                        record.NpcId,
                        "Player",
                        "SHOT_NEARBY",
                        0.75f,
                        "Gunshots discharged within close proximity of " + record.Name,
                        "Panic_Environment",
                        witnesses
                    );
                    NpcStateMachine.ApplyPhysicalBehavior(ped, record, playerPed, ctx);
                    continue;
                }
            }
        }

        private List<WitnessRecord> CollectWitnesses(Vector3 eventPosition, List<ActiveNpcContext> activeContexts, int primaryNpcId)
        {
            List<WitnessRecord> witnesses = new List<WitnessRecord>();
            if (activeContexts == null) return witnesses;

            foreach (var ctx in activeContexts)
            {
                if (ctx == null || !ctx.IsValid || ctx.Record.NpcId == primaryNpcId) continue;

                float dist = ctx.PedInstance.Position.DistanceTo(eventPosition);
                if (dist < 15.0f)
                {
                    witnesses.Add(new WitnessRecord(ctx.Record.NpcId, PerceptionType.DirectSight, 1.0f, dist));
                }
                else if (dist < 30.0f)
                {
                    witnesses.Add(new WitnessRecord(ctx.Record.NpcId, PerceptionType.AuditoryOnly, 0.6f, dist));
                }
            }

            return witnesses;
        }

        public void ProcessSpokenUtterance(int npcId, string playerUtterance, Ped ped, Ped playerPed, List<ActiveNpcContext> activeContexts = null)
        {
            if (string.IsNullOrEmpty(playerUtterance)) return;

            NpcRecord targetNpc = _simEngine.Store.GetNpcById(npcId);
            SemanticIntent intent = IntentInterpreter.Interpret(playerUtterance, targetNpc);

            string eventType = intent.Type.ToString().ToUpperInvariant();
            float severity = intent.EmotionalImpact;

            List<WitnessRecord> witnesses = (ped != null) ? CollectWitnesses(ped.Position, activeContexts, npcId) : null;

            var updated = _simEngine.ProcessInteractionWithWitnesses(
                npcId,
                "Player",
                eventType,
                severity,
                string.Format("Player stated (Intent={0}): \"{1}\"", intent.Type, playerUtterance),
                eventType,
                witnesses
            );

            if (ped != null && ped.Exists() && updated != null)
            {
                NpcStateMachine.ApplyPhysicalBehavior(ped, updated, playerPed);
            }
        }
    }
}
