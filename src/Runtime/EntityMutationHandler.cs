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
            Vector3 playerPos = Vector3.Zero;
            try
            {
                if (Game.Player != null && Game.Player.Character != null)
                {
                    playerPos = Game.Player.Character.Position;
                }
            }
            catch { }

            bool hasExplicitTarget = !string.IsNullOrEmpty(data.TargetEntityId);
            if (hasExplicitTarget)
            {
                targetContext = activeNpcContexts.Find(ctx =>
                    ctx != null && ctx.IsValid && (
                        string.Equals(ctx.Record.Name, data.TargetEntityId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ctx.EntityId, data.TargetEntityId, StringComparison.OrdinalIgnoreCase) ||
                        ctx.Record.NpcId.ToString() == data.TargetEntityId
                    )
                );

                // Invariant #7: An explicit NPC target that cannot be resolved must not be replaced with the nearest NPC.
                if (targetContext == null)
                {
                    Console.WriteLine(string.Format("[EntityMutationHandler] Explicit target NPC '{0}' could not be resolved. Aborting mutation without fallback to nearest NPC (Invariant #7).", data.TargetEntityId));
                    return;
                }
            }
            else
            {
                // Ambient / unaddressed mutation: resolve to nearest valid NPC
                float minDist = float.MaxValue;
                foreach (var ctx in activeNpcContexts)
                {
                    if (ctx != null && ctx.IsValid && ctx.PedInstance != null)
                    {
                        try
                        {
                            float dist = ctx.PedInstance.Position.DistanceTo(playerPos);
                            if (dist < minDist)
                            {
                                minDist = dist;
                                targetContext = ctx;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (targetContext != null && targetContext.IsValid)
            {
                Ped targetPed = targetContext.PedInstance;
                NpcRecord targetRecord = targetContext.Record;

                if (!string.IsNullOrEmpty(data.GiveWeapon) && targetPed != null)
                {
                    WeaponHash wHash;
                    if (Enum.TryParse<WeaponHash>(data.GiveWeapon.Replace("WEAPON_", ""), true, out wHash))
                    {
                        try { targetPed.Weapons.Give(wHash, 100, true, true); } catch { }
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

                try
                {
                    Ped playerPed = null;
                    try { if (Game.Player != null) playerPed = Game.Player.Character; } catch { }
                    NpcStateMachine.ApplyPhysicalBehavior(targetPed, targetRecord, playerPed, targetContext);
                }
                catch { }

                try
                {
                    Notification.Show(string.Format("~y~[World Mutation]~w~~n~{0}", data.NarrativeReason ?? "Character behavior changed!"));
                }
                catch { }
            }
        }
    }
}
