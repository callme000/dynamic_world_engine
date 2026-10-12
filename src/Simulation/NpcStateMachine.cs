using System;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Simulation
{
    public enum NpcBehaviorState
    {
        Calm,
        Alert,
        Threatened,
        Panicked,
        Hostile,
        Submissive,
        Fleeing,
        Allied
    }

    public class NpcStateMachine
    {
        public static NpcBehaviorState EvaluateNewState(NpcRecord record, string triggerEvent, float severity)
        {
            if (record == null) return NpcBehaviorState.Calm;

            string trig = (triggerEvent ?? "").ToUpperInvariant();

            if (trig.Contains("THREAT") || trig.Contains("GUN_AIM") || trig.Contains("INTIMIDATION"))
            {
                if (severity >= 0.75f || record.Fear >= 70)
                {
                    return (record.PublicFaction == "Police" || record.Occupation.Contains("Guard") || record.Occupation.Contains("Enforcer"))
                        ? NpcBehaviorState.Hostile
                        : NpcBehaviorState.Submissive;
                }
                else if (severity >= 0.4f || record.Fear >= 40)
                {
                    return NpcBehaviorState.Threatened;
                }
                return NpcBehaviorState.Alert;
            }
            else if (trig.Contains("ASSAULT") || trig.Contains("DAMAGE") || trig.Contains("SHOT_NEARBY"))
            {
                if (record.Fear >= 80)
                {
                    return NpcBehaviorState.Fleeing;
                }
                return NpcBehaviorState.Hostile;
            }
            else if (trig.Contains("BRIBE") || trig.Contains("ALLIANCE") || trig.Contains("FAVOR"))
            {
                return (record.Trust >= 10 || severity >= 0.75f) ? NpcBehaviorState.Allied : NpcBehaviorState.Calm;
            }
            else if (trig.Contains("PANIC") || trig.Contains("EXPLOSION"))
            {
                return NpcBehaviorState.Fleeing;
            }

            return NpcBehaviorState.Calm;
        }

        public static void ApplyPhysicalBehavior(Ped ped, NpcRecord record, Ped playerPed, ActiveNpcContext context = null)
        {
            if (ped == null || !ped.Exists() || playerPed == null || !playerPed.Exists()) return;

            string state = (record != null && !string.IsNullOrEmpty(record.CurrentState)) ? record.CurrentState.ToUpperInvariant() : "CALM";

            // Guard against task overwrite thrashing
            if (context != null && string.Equals(context.LastAppliedBehaviorState, state, StringComparison.OrdinalIgnoreCase))
            {
                if (state == "HOSTILE" && ped.IsInCombatAgainst(playerPed)) return;
                if (state == "SUBMISSIVE" || state == "HANDS_UP") return;
                if ((state == "FLEEING" || state == "PANICKED") && ped.IsFleeing) return;
                if (state == "ALLIED") return;
            }

            if (context != null)
            {
                context.LastAppliedBehaviorState = state;
            }

            switch (state)
            {
                case "SUBMISSIVE":
                case "HANDS_UP":
                    ped.Task.HandsUp(20000);
                    break;

                case "FLEEING":
                case "PANICKED":
                    if (!ped.IsFleeing)
                    {
                        ped.Task.FleeFrom(playerPed);
                    }
                    break;

                case "HOSTILE":
                    if (!ped.Weapons.HasWeapon(WeaponHash.Pistol) && !ped.Weapons.HasWeapon(WeaponHash.MicroSMG) && !ped.Weapons.HasWeapon(WeaponHash.PumpShotgun))
                    {
                        ped.Weapons.Give(WeaponHash.Pistol, 100, true, true);
                    }
                    if (!ped.IsInCombatAgainst(playerPed))
                    {
                        ped.Task.FightAgainst(playerPed);
                    }
                    break;

                case "ALLIED":
                    ped.Task.FollowToOffsetFromEntity(playerPed, new Vector3(0, -1.5f, 0), 2.0f, -1, 1.0f, true);
                    break;

                case "THREATENED":
                case "ALERT":
                    ped.Task.LookAt(playerPed, 8000);
                    break;

                case "CALM":
                default:
                    break;
            }
        }
    }
}
