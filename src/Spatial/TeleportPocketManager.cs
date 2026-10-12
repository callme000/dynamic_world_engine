using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Spatial
{
    public class PocketTransition
    {
        public string PocketId { get; set; }
        public string EnvironmentId { get; set; }
        public SpatialVector3 StreetCoords { get; set; }
        public SpatialVector3 InteriorCoords { get; set; }
        public SpatialVector3 InteriorExitCoords { get; set; }
        public string EntranceTitle { get; set; }

        [JsonIgnore]
        public Blip EntranceBlip { get; set; }

        // Epistemic Access Gating
        public bool IsLocked { get; set; }
        public bool IsKnownToPlayer { get; set; }
        public string RequiredSecret { get; set; }
        public string RequiredFaction { get; set; }
        public int RequiredRelationship { get; set; }
        public string LockReason { get; set; }

        public PocketTransition()
        {
            PocketId = "";
            EnvironmentId = "";
            EntranceTitle = "Entrance";
            IsLocked = false;
            IsKnownToPlayer = true;
            LockReason = "Locked";
            RequiredSecret = "";
            RequiredFaction = "";
            RequiredRelationship = -100;
        }

        public PocketTransition(string id, string envId, SpatialVector3 street, SpatialVector3 interior, SpatialVector3 exit, string title, bool isLocked = false, bool isKnown = true, string lockReason = "Locked")
        {
            PocketId = id;
            EnvironmentId = envId;
            StreetCoords = street;
            InteriorCoords = interior;
            InteriorExitCoords = exit;
            EntranceTitle = title;
            IsLocked = isLocked;
            IsKnownToPlayer = isKnown;
            LockReason = lockReason;
            RequiredSecret = "";
            RequiredFaction = "";
            RequiredRelationship = -100;
        }
    }

    public class TeleportPocketManager
    {
        private readonly List<PocketTransition> _activePockets = new List<PocketTransition>();
        private DateTime _lastTeleportTime = DateTime.MinValue;

        public void RegisterPocket(
            string pocketId, string envId, Vector3 streetCoords, Vector3 interiorCoords, Vector3 interiorExitCoords,
            string title = "Underground Bunker", bool isLocked = false, bool isKnown = true, string lockReason = "Locked",
            bool createBlip = true)
        {
            PocketTransition pocket = new PocketTransition(pocketId, envId, streetCoords, interiorCoords, interiorExitCoords, title, isLocked, isKnown, lockReason);
            
            // Create Street Blip only if known to player and createBlip is true
            if (isKnown && createBlip)
            {
                Blip b = World.CreateBlip(streetCoords);
                if (b != null && b.Exists())
                {
                    b.Sprite = BlipSprite.Safehouse;
                    b.Color = isLocked ? BlipColor.Red : BlipColor.Yellow;
                    b.Name = title;
                    b.IsShortRange = false;
                    pocket.EntranceBlip = b;
                }
            }

            _activePockets.Add(pocket);
        }

        public void RegisterPocket(string pocketId, Vector3 streetCoords, Vector3 interiorCoords, Vector3 interiorExitCoords, string title = "Underground Bunker", bool createBlip = true)
        {
            RegisterPocket(pocketId, "global", streetCoords, interiorCoords, interiorExitCoords, title, false, true, "Locked", createBlip);
        }

        public void UnlockPocket(string pocketId)
        {
            var p = _activePockets.Find(x => string.Equals(x.PocketId, pocketId, StringComparison.OrdinalIgnoreCase));
            if (p != null)
            {
                p.IsLocked = false;
                if (p.EntranceBlip != null && p.EntranceBlip.Exists())
                {
                    p.EntranceBlip.Color = BlipColor.Yellow;
                }
            }
        }

        public void RevealPocketToPlayer(string pocketId)
        {
            var p = _activePockets.Find(x => string.Equals(x.PocketId, pocketId, StringComparison.OrdinalIgnoreCase));
            if (p != null && !p.IsKnownToPlayer)
            {
                p.IsKnownToPlayer = true;
                if (p.EntranceBlip == null || !p.EntranceBlip.Exists())
                {
                    Blip b = World.CreateBlip(p.StreetCoords);
                    if (b != null && b.Exists())
                    {
                        b.Sprite = BlipSprite.Safehouse;
                        b.Color = p.IsLocked ? BlipColor.Red : BlipColor.Yellow;
                        b.Name = p.EntranceTitle;
                        b.IsShortRange = false;
                        p.EntranceBlip = b;
                    }
                }
            }
        }

        public void ProcessPocketTransitions(Ped playerPed)
        {
            if (playerPed == null || !playerPed.Exists() || playerPed.IsDead || _activePockets.Count == 0) return;

            DateTime now = DateTime.UtcNow;
            if ((now - _lastTeleportTime).TotalSeconds < 3.0) return;

            Vector3 pPos = playerPed.Position;

            foreach (var pocket in _activePockets)
            {
                // If the pocket is a secret and player has not discovered it yet, skip street marker rendering
                if (!pocket.IsKnownToPlayer) continue;

                // Check Street Entrance Proximity (< 10.0m)
                float distStreet = pPos.DistanceTo(pocket.StreetCoords);
                if (distStreet < 10.0f)
                {
                    Color markerColor = pocket.IsLocked ? Color.FromArgb(180, 220, 20, 60) : Color.FromArgb(180, 255, 215, 0);

                    // Draw 3D Ground Cylinder Marker at street entrance
                    World.DrawMarker(
                        MarkerType.Cylinder,
                        pocket.StreetCoords - new Vector3(0, 0, 0.9f),
                        Vector3.Zero, Vector3.Zero,
                        new Vector3(1.2f, 1.2f, 0.5f),
                        markerColor
                    );

                    if (distStreet < 1.6f)
                    {
                        if (pocket.IsLocked)
                        {
                            DisplayHelpText(string.Format("~r~[LOCKED]~w~ {0} ({1})", pocket.EntranceTitle, pocket.LockReason));
                        }
                        else
                        {
                            DisplayHelpText("Press ~INPUT_CONTEXT~ (E) to Enter " + pocket.EntranceTitle);
                            if (Game.IsControlJustPressed(Control.Context))
                            {
                                _lastTeleportTime = now;
                                SafeTeleportPlayer(playerPed, pocket.InteriorCoords, 0f);
                                return;
                            }
                        }
                    }
                }

                // Check Interior Exit Proximity (< 10.0m) - Ensure exit is distinct from entrance spawn
                float distInterior = pPos.DistanceTo(pocket.InteriorExitCoords);
                if (distInterior < 10.0f)
                {
                    World.DrawMarker(
                        MarkerType.Cylinder,
                        pocket.InteriorExitCoords - new Vector3(0, 0, 0.9f),
                        Vector3.Zero, Vector3.Zero,
                        new Vector3(1.2f, 1.2f, 0.5f),
                        Color.FromArgb(180, 0, 200, 255)
                    );

                    if (distInterior < 1.6f)
                    {
                        DisplayHelpText("Press ~INPUT_CONTEXT~ (E) to Exit to Street");
                        if (Game.IsControlJustPressed(Control.Context))
                        {
                            _lastTeleportTime = now;
                            SafeTeleportPlayer(playerPed, pocket.StreetCoords + (playerPed.ForwardVector * 1.5f), 0f);
                            return;
                        }
                    }
                }
            }
        }

        public static void SafeTeleportPlayer(Ped player, Vector3 destination, float heading = 0f)
        {
            if (player == null || !player.Exists()) return;

            try
            {
                Screen.FadeOut(500);
                Script.Wait(550);

                // 1. Freeze physics immediately to prevent gravity from pulling ped into void before collision loads
                player.IsPositionFrozen = true;

                // 2. Request collision streaming at target coordinates
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);
                Function.Call(Hash.REQUEST_ADDITIONAL_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);

                // 3. Move player position and heading
                player.Position = destination + new Vector3(0, 0, 0.2f);
                if (heading != 0f) player.Heading = heading;

                // 4. Force load all objects in the area
                Function.Call(Hash.LOAD_ALL_OBJECTS_NOW);

                // 5. Wait for collision to stream (poll up to 40 frames / 800ms)
                int timeout = 0;
                while (!Function.Call<bool>(Hash.HAS_COLLISION_LOADED_AROUND_ENTITY, player.Handle) && timeout < 40)
                {
                    Function.Call(Hash.REQUEST_COLLISION_AT_COORD, destination.X, destination.Y, destination.Z);
                    Script.Wait(20);
                    timeout++;
                }

                // Extra stabilization pause
                Script.Wait(200);

                // Spatial Vision: Probe real solid ground before unfreezing
                float validGroundZ;
                if (SpatialVisionEngine.ProbeGroundElevation(player.Position, out validGroundZ))
                {
                    player.Position = new Vector3(destination.X, destination.Y, validGroundZ + 0.1f);
                }

                // 6. Unfreeze player once solid ground is guaranteed
                player.IsPositionFrozen = false;

                Screen.FadeIn(500);
            }
            catch (Exception ex)
            {
                if (player != null && player.Exists()) player.IsPositionFrozen = false;
                Console.WriteLine("[SafeTeleportPlayer] Teleport exception: " + ex.Message);
            }
        }

        private void DisplayHelpText(string text)
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_HELP, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_HELP, 0, false, true, -1);
        }

        public void RemovePocketsForEnvironment(string envId)
        {
            if (string.IsNullOrEmpty(envId)) return;

            for (int i = _activePockets.Count - 1; i >= 0; i--)
            {
                var p = _activePockets[i];
                if (string.Equals(p.EnvironmentId, envId, StringComparison.OrdinalIgnoreCase))
                {
                    if (p.EntranceBlip != null && p.EntranceBlip.Exists())
                    {
                        p.EntranceBlip.Delete();
                    }
                    _activePockets.RemoveAt(i);
                }
            }
        }

        public void ClearAllPockets()
        {
            foreach (var p in _activePockets)
            {
                if (p.EntranceBlip != null && p.EntranceBlip.Exists())
                {
                    p.EntranceBlip.Delete();
                }
            }
            _activePockets.Clear();
        }
    }
}
