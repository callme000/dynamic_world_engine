using System;
using System.Collections.Generic;
using System.IO;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Native interior activation, IPL streaming, and memory pinning manager.
    /// Delegates map radar blip creation to BlipManager.
    /// </summary>
    public class InteriorManager
    {
        private readonly TeleportPocketManager _pocketManager;
        private readonly BlipManager _blipManager;
        private int _currentActiveInteriorId = 0;
        private InteriorDefinition _currentActiveInterior = null;
        private Blip _currentInteriorBlip = null;

        public InteriorManager(TeleportPocketManager pocketManager, BlipManager blipManager = null)
        {
            _pocketManager = pocketManager;
            _blipManager = blipManager;
        }

        public int CurrentActiveInteriorId { get { return _currentActiveInteriorId; } }
        public InteriorDefinition CurrentActiveInterior { get { return _currentActiveInterior; } }

        public InteriorDefinition ResolveInteriorForPrompt(string prompt)
        {
            return InteriorCatalog.FindBestMatch(prompt);
        }

        private static bool? _isEnableAllInteriorsPresent = null;
        public static bool IsEnableAllInteriorsPresent()
        {
            if (!_isEnableAllInteriorsPresent.HasValue)
            {
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string scriptsDir = Path.Combine(baseDir, "scripts");
                    _isEnableAllInteriorsPresent = 
                        File.Exists(Path.Combine(scriptsDir, "EnableAllinteriors.dll")) ||
                        File.Exists(Path.Combine(scriptsDir, "EnableAllInteriors.ini")) ||
                        File.Exists(Path.Combine(baseDir, "EnableAllinteriors.dll")) ||
                        File.Exists(Path.Combine(baseDir, "EnableAllInteriors.ini"));
                }
                catch
                {
                    _isEnableAllInteriorsPresent = false;
                }
            }
            return _isEnableAllInteriorsPresent.Value;
        }

        public bool ActivateInterior(InteriorDefinition interior, Ped playerPed, bool teleportPlayer = false)
        {
            if (interior == null) return false;

            try
            {
                // Disambiguate Open-World / Outdoor locations (e.g. Del Perro Beach, Pier, Alamo Sea)
                // Outdoor locations do not have interior airlocks, entity sets, or indoor pockets.
                // We must NOT register teleport pockets or create entrance blips on open-world ground.
                bool isOutdoor = (interior.SemanticTags != null && (interior.SemanticTags.Contains("outdoor") || interior.SemanticTags.Contains("open_world") || interior.SemanticTags.Contains("landmark")))
                    || string.Equals(interior.Category, "open_world", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(interior.Category, "beach", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(interior.Category, "pier", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(interior.Category, "park", StringComparison.OrdinalIgnoreCase);

                if (isOutdoor)
                {
                    ClearCurrentInteriorBlip();

                    // Pre-load collision at destination
                    Function.Call(Hash.REQUEST_COLLISION_AT_COORD, 
                        interior.InteriorCoords.X, interior.InteriorCoords.Y, interior.InteriorCoords.Z);

                    if (teleportPlayer && playerPed != null && playerPed.Exists())
                    {
                        TeleportPocketManager.SafeTeleportPlayer(playerPed, interior.InteriorCoords, interior.InteriorHeading);
                    }

                    _currentActiveInteriorId = 0;
                    _currentActiveInterior = interior;
                    Notification.Show(string.Format("~g~[Location Active]~w~~n~~y~{0}~w~", interior.Name));
                    return true;
                }

                if (!DynamicWorldEngine.Runtime.NativeFiberCoordinator.IsMainThread)
                {
                    Console.WriteLine(string.Format(
                        "[InteriorManager] Indoor activation BLOCKED for '{0}' (InteriorId={1}). CurrentThreadId={2}, MainThreadId={3}, Coords={4}.",
                        interior.Name,
                        interior.InteriorId,
                        System.Threading.Thread.CurrentThread.ManagedThreadId,
                        DynamicWorldEngine.Runtime.NativeFiberCoordinator.MainThreadId,
                        interior.InteriorCoords));
                    return false;
                }

                Console.WriteLine(string.Format(
                    "[InteriorManager] Starting indoor activation for '{0}' (InteriorId={1}, Coords={2}, Heading={3}, CurrentThreadId={4}, MainThreadId={5}, Teleport={6}).",
                    interior.Name,
                    interior.InteriorId,
                    interior.InteriorCoords,
                    interior.InteriorHeading,
                    System.Threading.Thread.CurrentThread.ManagedThreadId,
                    DynamicWorldEngine.Runtime.NativeFiberCoordinator.MainThreadId,
                    teleportPlayer));

                // --- INDOOR IPL INTERIOR PIPELINE ---
                // 1. Enable Online Multiplayer DLC Map Streaming in Singleplayer
                try
                {
                    Function.Call(Hash.ON_ENTER_MP);
                }
                catch { }

                // 2. Remove any blocking offmission IPLs
                if (interior.RemoveIplNames != null)
                {
                    foreach (var remIpl in interior.RemoveIplNames)
                    {
                        if (Function.Call<bool>(Hash.IS_IPL_ACTIVE, remIpl))
                        {
                            Function.Call(Hash.REMOVE_IPL, remIpl);
                        }
                    }
                }

                // 3. Request and Load All Required IPLs (Rockstar Oracle: REQUEST_IPL -> IS_IPL_ACTIVE)
                if (interior.IplNames != null && interior.IplNames.Count > 0)
                {
                    foreach (var ipl in interior.IplNames)
                    {
                        if (!Function.Call<bool>(Hash.IS_IPL_ACTIVE, ipl))
                        {
                            Function.Call(Hash.REQUEST_IPL, ipl);
                            int iplAttempts = 0;
                            while (!Function.Call<bool>(Hash.IS_IPL_ACTIVE, ipl) && iplAttempts < 25)
                            {
                                Script.Wait(10);
                                iplAttempts++;
                            }
                        }
                    }
                }

                // 4. Discover Interior ID at Coordinates
                int intId = Function.Call<int>(Hash.GET_INTERIOR_AT_COORDS, 
                    interior.InteriorCoords.X, interior.InteriorCoords.Y, interior.InteriorCoords.Z);
                
                if (intId != 0 && Function.Call<bool>(Hash.IS_VALID_INTERIOR, intId))
                {
                    // 5. Activate Built-in Rockstar Entity Sets (Rockstar Oracle: ENABLE_INTERIOR_PROP / ACTIVATE_INTERIOR_ENTITY_SET)
                    if (interior.EntitySets != null)
                    {
                        foreach (var set in interior.EntitySets)
                        {
                            Function.Call(Hash.ACTIVATE_INTERIOR_ENTITY_SET, intId, set);
                        }
                    }

                    // 6. Force Redraw & Wait until Ready (Rockstar Oracle: REFRESH_INTERIOR -> IS_INTERIOR_READY)
                    Function.Call(Hash.REFRESH_INTERIOR, intId);
                    int readyAttempts = 0;
                    while (!Function.Call<bool>(Hash.IS_INTERIOR_READY, intId) && readyAttempts < 30)
                    {
                        Script.Wait(20);
                        readyAttempts++;
                    }

                    // 7. Pin Interior in Memory AFTER Entity Sets & Refresh (Rockstar Oracle: PIN_INTERIOR_IN_MEMORY)
                    Function.Call(Hash.PIN_INTERIOR_IN_MEMORY, intId);

                    _currentActiveInteriorId = intId;
                    _currentActiveInterior = interior;
                }

                // 8. Pre-load Collision Meshes at destination
                Function.Call(Hash.REQUEST_COLLISION_AT_COORD, 
                    interior.InteriorCoords.X, interior.InteriorCoords.Y, interior.InteriorCoords.Z);
                Function.Call(Hash.REQUEST_ADDITIONAL_COLLISION_AT_COORD,
                    interior.InteriorCoords.X, interior.InteriorCoords.Y, interior.InteriorCoords.Z);

                // 7b. Autonomous Spatial Vision Abyss Prevention Guard
                Vector3 safeCoords = SpatialVisionEngine.ResolveSafePlacementPoint(interior.InteriorCoords, interior.EntranceCoords, interior.Name);
                interior.InteriorCoords = safeCoords;

                // 8. Register Entrance / Exit markers in TeleportPocketManager (Exit offset to prevent spawn loop)
                // Suppress safehouse blip creation (createBlip: false) because entrance blips are either handled
                // by EnableAllInteriors (blue blip) or our dedicated _currentInteriorBlip below.
                Vector3 exitMarkerPos = interior.InteriorCoords + new Vector3(2.5f, 0, 0);
                _pocketManager.RegisterPocket(
                    interior.InteriorId,
                    interior.EntranceCoords,
                    interior.InteriorCoords,
                    exitMarkerPos,
                    interior.Name,
                    createBlip: false
                );

                // 9. Manage Entrance GPS Route Blip
                ClearCurrentInteriorBlip();

                // If EnableAllInteriors is installed, it already marks all interior doors with its own entrance blips (blue).
                // Avoid creating a duplicate yellow blip over the exact same entrance!
                bool eaiInstalled = IsEnableAllInteriorsPresent();
                if (!eaiInstalled && !teleportPlayer)
                {
                    string blipLabel = interior.Name + " (Entrance)";
                    if (_blipManager != null)
                    {
                        _currentInteriorBlip = _blipManager.CreateCoordBlip(interior.EntranceCoords, blipLabel, BlipColor.Yellow, BlipSprite.Key);
                    }
                    else
                    {
                        _currentInteriorBlip = World.CreateBlip(interior.EntranceCoords);
                        if (_currentInteriorBlip != null && _currentInteriorBlip.Exists())
                        {
                            _currentInteriorBlip.Sprite = BlipSprite.Key;
                            _currentInteriorBlip.Color = BlipColor.Yellow;
                            _currentInteriorBlip.Name = blipLabel;
                        }
                    }

                    if (_currentInteriorBlip != null && _currentInteriorBlip.Exists())
                    {
                        _currentInteriorBlip.ShowRoute = true;
                    }
                }

                // 10. Safe Teleport Player with freeze and collision streaming
                if (teleportPlayer && playerPed != null && playerPed.Exists())
                {
                    Console.WriteLine(string.Format(
                        "[InteriorManager] Calling SafeTeleportPlayer for '{0}' at {1}, heading={2}.",
                        interior.Name, interior.InteriorCoords, interior.InteriorHeading));
                    TeleportPocketManager.SafeTeleportPlayer(playerPed, interior.InteriorCoords, interior.InteriorHeading);
                    Console.WriteLine(string.Format(
                        "[InteriorManager] SafeTeleportPlayer returned for '{0}'. Player position now {1}.",
                        interior.Name, playerPed.Position));
                }
                else if (teleportPlayer)
                {
                    Console.WriteLine(string.Format(
                        "[InteriorManager] Teleport requested for '{0}' but player ped is unavailable (null={1}, exists={2}).",
                        interior.Name,
                        playerPed == null,
                        playerPed != null && playerPed.Exists()));
                }

                Notification.Show(string.Format("~g~[Interior Active]~w~~n~~y~{0}~w~", interior.Name));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format(
                    "[InteriorManager] Error activating interior '{0}' (InteriorId={1}, CurrentThreadId={2}, MainThreadId={3}): {4}",
                    interior.Name,
                    interior.InteriorId,
                    System.Threading.Thread.CurrentThread.ManagedThreadId,
                    DynamicWorldEngine.Runtime.NativeFiberCoordinator.MainThreadId,
                    ex));
                return false;
            }
        }

        public void BindEntityToInteriorRoom(Entity entity, Ped playerPed)
        {
            if (entity == null || !entity.Exists() || _currentActiveInteriorId == 0) return;

            try
            {
                int roomKey = 0;
                if (playerPed != null && playerPed.Exists())
                {
                    roomKey = Function.Call<int>(Hash.GET_ROOM_KEY_FROM_ENTITY, playerPed.Handle);
                }

                if (roomKey != 0)
                {
                    Function.Call(Hash.FORCE_ROOM_FOR_ENTITY, entity.Handle, _currentActiveInteriorId, roomKey);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[InteriorManager] Room portal binding warning: " + ex.Message);
            }
        }

        public void DeactivateCurrentInterior()
        {
            if (_currentActiveInteriorId != 0)
            {
                try
                {
                    Function.Call(Hash.UNPIN_INTERIOR, _currentActiveInteriorId);
                }
                catch { }
                _currentActiveInteriorId = 0;
                _currentActiveInterior = null;
            }

            ClearCurrentInteriorBlip();
        }

        public void ClearCurrentInteriorBlip()
        {
            if (_currentInteriorBlip != null && _currentInteriorBlip.Exists())
            {
                _currentInteriorBlip.Delete();
                _currentInteriorBlip = null;
            }
        }
    }
}
