using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Simulation
{
    public class ActiveNpcContext
    {
        public Ped PedInstance { get; set; }
        public NpcRecord Record { get; set; }
        public string LastAppliedBehaviorState { get; set; }
        public string EnvironmentId { get; set; }
        public string EntityId { get; set; }

        public ActiveNpcContext()
        {
            LastAppliedBehaviorState = string.Empty;
            EnvironmentId = string.Empty;
            EntityId = string.Empty;
        }

        public ActiveNpcContext(Ped ped, NpcRecord record, string environmentId = null, string entityId = null)
        {
            PedInstance = ped;
            Record = record;
            LastAppliedBehaviorState = string.Empty;
            EnvironmentId = environmentId ?? string.Empty;
            EntityId = entityId ?? (record != null ? record.EntityId : string.Empty);
        }

        public bool? MockIsDead { get; set; }
        public bool? MockExists { get; set; }

        public bool CheckIsDead()
        {
            if (MockIsDead.HasValue) return MockIsDead.Value;
            try
            {
                return PedInstance != null && PedInstance.Exists() && PedInstance.IsDead;
            }
            catch
            {
                return false;
            }
        }

        public bool CheckExists()
        {
            if (MockExists.HasValue) return MockExists.Value;
            try
            {
                return PedInstance != null && PedInstance.Exists();
            }
            catch
            {
                return PedInstance != null;
            }
        }

        public bool IsValid
        {
            get
            {
                if (Record == null) return false;
                if (!CheckExists()) return false;
                if (CheckIsDead()) return false;
                return true;
            }
        }

        public void ExecuteInitialChoreography(string initialTask, List<ActiveNpcContext> allContexts)
        {
            if (!IsValid || string.IsNullOrWhiteSpace(initialTask)) return;

            string task = initialTask.Trim();

            try
            {
                if (task.StartsWith("aim_at:", StringComparison.OrdinalIgnoreCase))
                {
                    string targetId = task.Substring(7).Trim();
                    ActiveNpcContext target = FindTargetContext(targetId, allContexts);
                    if (target != null && target.IsValid)
                    {
                        PedInstance.Task.AimAt(target.PedInstance, -1);
                    }
                }
                else if (task.StartsWith("shoot_at:", StringComparison.OrdinalIgnoreCase))
                {
                    string targetId = task.Substring(9).Trim();
                    ActiveNpcContext target = FindTargetContext(targetId, allContexts);
                    if (target != null && target.IsValid)
                    {
                        PedInstance.Task.FightAgainst(target.PedInstance);
                    }
                }
                else if (task.StartsWith("talk_to:", StringComparison.OrdinalIgnoreCase))
                {
                    string targetId = task.Substring(8).Trim();
                    ActiveNpcContext target = FindTargetContext(targetId, allContexts);
                    if (target != null && target.IsValid)
                    {
                        PedInstance.Task.TurnTo(target.PedInstance);
                    }
                }
                else if (string.Equals(task, "hostage_hands_up", StringComparison.OrdinalIgnoreCase) || string.Equals(task, "hands_up", StringComparison.OrdinalIgnoreCase))
                {
                    PedInstance.Task.HandsUp(-1);
                }
                else if (string.Equals(task, "cower", StringComparison.OrdinalIgnoreCase))
                {
                    PedInstance.Task.Cower(-1);
                }
                else if (string.Equals(task, "guard_post", StringComparison.OrdinalIgnoreCase) || string.Equals(task, "stand_guard", StringComparison.OrdinalIgnoreCase))
                {
                    PedInstance.Task.StandStill(-1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ActiveNpcContext] Choreography exception: " + ex.Message);
            }
        }

        private ActiveNpcContext FindTargetContext(string targetId, List<ActiveNpcContext> allContexts)
        {
            if (allContexts == null) return null;

            return allContexts.Find(ctx =>
                ctx != null && ctx.IsValid && (
                    string.Equals(ctx.Record.Name, targetId, StringComparison.OrdinalIgnoreCase) ||
                    ctx.Record.NpcId.ToString() == targetId
                )
            );
        }
    }
}
