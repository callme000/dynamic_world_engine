using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;
using GTA;
using GTA.Math;
using GTA.UI;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Executes live in-game behavioral mutations on active NPCs.
    /// Handles combat aggression, weapon equips, fleeing, and relationship shifts.
    /// </summary>
    public static class EntityMutationHandler
    {
        public static void ExecuteEntityMutationDirect(MutateEntityData data, List<ActiveNpcContext> activeNpcContexts)
        {
            if (data == null || activeNpcContexts == null) return;

            ActiveNpcContext targetContext = null;
            Vector3 playerPos = Game.Player.Character.Position;

            if (!string.IsNullOrEmpty(data.TargetEntityId))
            {
                targetContext = activeNpcContexts.Find(ctx =>
                    ctx != null && ctx.IsValid && (
                        string.Equals(ctx.Record.Name, data.TargetEntityId, StringComparison.OrdinalIgnoreCase) ||
                        ctx.Record.NpcId.ToString() == data.TargetEntityId
                    )
                );
            }

            if (targetContext == null)
            {
                float minDist = float.MaxValue;
                foreach (var ctx in activeNpcContexts)
                {
                    if (ctx != null && ctx.IsValid)
                    {
                        float dist = ctx.PedInstance.Position.DistanceTo(playerPos);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            targetContext = ctx;
                        }
                    }
                }
            }

            if (targetContext != null && targetContext.IsValid)
            {
                Ped targetPed = targetContext.PedInstance;
                NpcRecord targetRecord = targetContext.Record;

                if (!string.IsNullOrEmpty(data.GiveWeapon))
                {
                    WeaponHash wHash;
                    if (Enum.TryParse<WeaponHash>(data.GiveWeapon.Replace("WEAPON_", ""), true, out wHash))
                    {
                        targetPed.Weapons.Give(wHash, 100, true, true);
                    }
                }

                if (targetRecord != null)
                {
                    if (!string.IsNullOrEmpty(data.Hostility) && data.Hostility.Contains("hostile"))
                    {
                        targetRecord.CurrentState = "HOSTILE";
                    }
                    else if (!string.IsNullOrEmpty(data.Hostility) && data.Hostility.Contains("flee"))
                    {
                        targetRecord.CurrentState = "PANIC";
                    }

                    if (!string.IsNullOrEmpty(data.NewHiddenRole))
                    {
                        targetRecord.HiddenRole = data.NewHiddenRole;
                    }
                }

                NpcStateMachine.ApplyPhysicalBehavior(targetPed, targetRecord, Game.Player.Character, targetContext);

                Notification.Show(string.Format("~y~[World Mutation]~w~~n~{0}", data.NarrativeReason ?? "Character behavior changed!"));
            }
        }
    }
}
