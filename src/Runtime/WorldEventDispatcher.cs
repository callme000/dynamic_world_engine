using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Spatial;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Orchestrates dynamic in-game world events (police raids, rival gang ambushes, pursuits).
    /// </summary>
    public static class WorldEventDispatcher
    {
        public static void ExecuteWorldEventDirect(
            TriggerWorldEventData data,
            EntitySpawnerService spawner,
            BlipManager blips)
        {
            if (data == null || spawner == null) return;

            Ped player = Game.Player.Character;
            Vector3 playerPos = player.Position;
            float heading = player.Heading;

            // Spawn vehicle 45 meters down the road
            Vector3 forward = new Vector3(-(float)Math.Sin(heading * Math.PI / 180f), (float)Math.Cos(heading * Math.PI / 180f), 0f);
            Vector3 spawnPos = playerPos + (forward * 45.0f);

            float gZ = World.GetGroundHeight(spawnPos);
            if (gZ > 0) spawnPos.Z = gZ;

            string vehModelName = string.IsNullOrEmpty(data.VehicleModel) ? "POLICE" : data.VehicleModel;
            Model model = spawner.SafeLoadVehicleModel(vehModelName);

            if (model.IsLoaded)
            {
                float vehHeading = (heading + 180.0f) % 360.0f;
                Vehicle veh = World.CreateVehicle(model, spawnPos, vehHeading);
                model.MarkAsNoLongerNeeded();

                if (veh != null && veh.Exists())
                {
                    veh.IsPersistent = true;
                    spawner.SpawnedVehicles.Add(veh);

                    if (data.Sirens && veh.HasSiren)
                    {
                        veh.IsSirenActive = true;
                        veh.IsSirenSilent = false;
                    }

                    if (blips != null)
                    {
                        blips.CreateCoordBlip(spawnPos, data.Title ?? "World Event", BlipColor.Red, BlipSprite.PoliceCar);
                    }

                    Ped driver = null;
                    bool isAnyHostileToPlayer = false;

                    if (data.Occupants != null && data.Occupants.Count > 0)
                    {
                        for (int i = 0; i < data.Occupants.Count; i++)
                        {
                            var occ = data.Occupants[i];
                            VehicleSeat seat = (i == 0) ? VehicleSeat.Driver : (VehicleSeat)(i - 1);

                            string pedModelName = string.IsNullOrEmpty(occ.PedModel) ? "s_m_y_cop_01" : occ.PedModel;
                            Model pModel = spawner.SafeLoadModel(pedModelName, true);

                            if (pModel.IsLoaded)
                            {
                                Ped occPed = veh.CreatePedOnSeat(seat, pModel);
                                pModel.MarkAsNoLongerNeeded();

                                if (occPed != null && occPed.Exists())
                                {
                                    occPed.IsPersistent = true;
                                    occPed.BlockPermanentEvents = false;

                                    if (seat == VehicleSeat.Driver) driver = occPed;

                                    if (!string.IsNullOrEmpty(occ.Weapon) && !string.Equals(occ.Weapon, "none", StringComparison.OrdinalIgnoreCase))
                                    {
                                        WeaponHash wHash;
                                        if (Enum.TryParse<WeaponHash>(occ.Weapon.Replace("WEAPON_", ""), true, out wHash))
                                        {
                                            occPed.Weapons.Give(wHash, 200, true, true);
                                        }
                                    }

                                    bool isHostile = string.Equals(occ.HostilityToPlayer, "hostile", StringComparison.OrdinalIgnoreCase);
                                    if (isHostile)
                                    {
                                        isAnyHostileToPlayer = true;
                                        int mercGroup = Game.GenerateHash("CIVMERCENARY");
                                        occPed.RelationshipGroup = mercGroup;
                                        Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, mercGroup, player.RelationshipGroup);
                                        Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, player.RelationshipGroup, mercGroup);

                                        if (seat != VehicleSeat.Driver)
                                        {
                                            occPed.Task.Combat(player);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Direct driver to approach or intercept player
                    if (driver != null && driver.Exists())
                    {
                        float speed = data.ApproachSpeed > 0 ? data.ApproachSpeed : 25.0f;
                        if (isAnyHostileToPlayer)
                        {
                            driver.Task.DriveTo(veh, player.Position, 8.0f, speed, DrivingStyle.Rushed);
                            Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 2);
                        }
                        else
                        {
                            driver.Task.DriveTo(veh, player.Position, 10.0f, speed, DrivingStyle.Normal);
                        }
                    }

                    Notification.Show(string.Format("~r~[World Event: {0}]~w~~n~{1}", data.Title ?? "Emergency Alert", data.Description ?? "Units are responding to the area."));
                }
            }
        }
    }
}
