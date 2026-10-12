using System;
using System.Collections.Generic;
using DynamicWorldEngine.Audio;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using DynamicWorldEngine.Utils;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Handles native GTA entity streaming, spawning, atmosphere manipulation, and lifecycle disposal.
    /// Decouples native World.Create* operations from the main script loop.
    /// </summary>
    public class EntitySpawnerService
    {
        private readonly List<Prop> _spawnedProps = new List<Prop>();
        private readonly List<ActiveNpcContext> _activeNpcContexts = new List<ActiveNpcContext>();
        private readonly List<Vehicle> _spawnedVehicles = new List<Vehicle>();
        private readonly List<int> _spawnedScriptFires = new List<int>();

        private readonly Dictionary<string, List<Prop>> _propsByEnvironment = new Dictionary<string, List<Prop>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ActiveNpcContext>> _npcsByEnvironment = new Dictionary<string, List<ActiveNpcContext>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Vehicle>> _vehiclesByEnvironment = new Dictionary<string, List<Vehicle>>(StringComparer.OrdinalIgnoreCase);

        private Weather _originalWeather = Weather.ExtraSunny;
        private int _originalHour = 12;
        private int _originalMinute = 0;
        private bool _atmosphereModified = false;

        public List<Prop> SpawnedProps { get { return _spawnedProps; } }
        public List<ActiveNpcContext> ActiveNpcContexts { get { return _activeNpcContexts; } }
        public List<Vehicle> SpawnedVehicles { get { return _spawnedVehicles; } }

        public void RegisterProp(string environmentId, Prop prop)
        {
            if (prop == null) return;

            if (!_spawnedProps.Contains(prop))
            {
                _spawnedProps.Add(prop);
            }

            if (!string.IsNullOrEmpty(environmentId))
            {
                List<Prop> list;
                if (!_propsByEnvironment.TryGetValue(environmentId, out list))
                {
                    list = new List<Prop>();
                    _propsByEnvironment[environmentId] = list;
                }
                if (!list.Contains(prop))
                {
                    list.Add(prop);
                }
            }
        }

        public void RegisterNpc(string environmentId, ActiveNpcContext context)
        {
            if (context == null) return;

            if (!string.IsNullOrEmpty(environmentId))
            {
                context.EnvironmentId = environmentId;
            }

            if (!_activeNpcContexts.Contains(context))
            {
                _activeNpcContexts.Add(context);
            }

            if (!string.IsNullOrEmpty(environmentId))
            {
                List<ActiveNpcContext> list;
                if (!_npcsByEnvironment.TryGetValue(environmentId, out list))
                {
                    list = new List<ActiveNpcContext>();
                    _npcsByEnvironment[environmentId] = list;
                }
                if (!list.Contains(context))
                {
                    list.Add(context);
                }
            }
        }

        public void RegisterVehicle(string environmentId, Vehicle vehicle)
        {
            if (vehicle == null) return;

            if (!_spawnedVehicles.Contains(vehicle))
            {
                _spawnedVehicles.Add(vehicle);
            }

            if (!string.IsNullOrEmpty(environmentId))
            {
                List<Vehicle> list;
                if (!_vehiclesByEnvironment.TryGetValue(environmentId, out list))
                {
                    list = new List<Vehicle>();
                    _vehiclesByEnvironment[environmentId] = list;
                }
                if (!list.Contains(vehicle))
                {
                    list.Add(vehicle);
                }
            }
        }

        public List<Prop> GetPropsForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return new List<Prop>();
            List<Prop> list;
            if (_propsByEnvironment.TryGetValue(environmentId, out list))
            {
                return new List<Prop>(list);
            }
            return new List<Prop>();
        }

        public List<ActiveNpcContext> GetNpcsForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return new List<ActiveNpcContext>();
            List<ActiveNpcContext> list;
            if (_npcsByEnvironment.TryGetValue(environmentId, out list))
            {
                return new List<ActiveNpcContext>(list);
            }
            return new List<ActiveNpcContext>();
        }

        public List<Vehicle> GetVehiclesForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return new List<Vehicle>();
            List<Vehicle> list;
            if (_vehiclesByEnvironment.TryGetValue(environmentId, out list))
            {
                return new List<Vehicle>(list);
            }
            return new List<Vehicle>();
        }

        public bool HasRealizedEntities(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return false;
            List<Prop> props;
            if (_propsByEnvironment.TryGetValue(environmentId, out props) && props.Count > 0) return true;
            List<ActiveNpcContext> npcs;
            if (_npcsByEnvironment.TryGetValue(environmentId, out npcs) && npcs.Count > 0) return true;
            List<Vehicle> vehs;
            if (_vehiclesByEnvironment.TryGetValue(environmentId, out vehs) && vehs.Count > 0) return true;
            return false;
        }

        public void RemoveEntitiesForEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return;

            // 1. Remove Props
            List<Prop> props;
            if (_propsByEnvironment.TryGetValue(environmentId, out props))
            {
                foreach (var prop in props)
                {
                    try
                    {
                        if (prop != null && prop.Exists()) prop.Delete();
                    }
                    catch { }
                    _spawnedProps.Remove(prop);
                }
                _propsByEnvironment.Remove(environmentId);
            }

            // 2. Remove NPCs
            List<ActiveNpcContext> npcs;
            if (_npcsByEnvironment.TryGetValue(environmentId, out npcs))
            {
                foreach (var ctx in npcs)
                {
                    try
                    {
                        if (ctx != null && ctx.IsValid && ctx.PedInstance != null && ctx.PedInstance.Exists())
                        {
                            ctx.PedInstance.Delete();
                        }
                    }
                    catch { }
                    _activeNpcContexts.Remove(ctx);
                }
                _npcsByEnvironment.Remove(environmentId);
            }

            // 3. Remove Vehicles
            List<Vehicle> vehs;
            if (_vehiclesByEnvironment.TryGetValue(environmentId, out vehs))
            {
                foreach (var veh in vehs)
                {
                    try
                    {
                        if (veh != null && veh.Exists()) veh.Delete();
                    }
                    catch { }
                    _spawnedVehicles.Remove(veh);
                }
                _vehiclesByEnvironment.Remove(environmentId);
            }
        }

        public bool SafeLoadModel(string modelName, out Model model, bool isPed = false)
        {
            model = new Model(0);
            try
            {
                model = new Model(modelName);
                model.Request(1000);
                int attempts = 0;
                while (!model.IsLoaded && attempts < 50)
                {
                    Script.Wait(20);
                    attempts++;
                }

                if (!model.IsLoaded)
                {
                    Log.Warn("Spawner", "Failed to load model \"{0}\" (isPed={1}) after {2} attempts", modelName, isPed, attempts);
                    model.MarkAsNoLongerNeeded();
                    if (isPed)
                    {
                        string fallback = "a_m_y_beach_01";
                        Log.Info("Spawner", "Falling back to default ped model: {0}", fallback);
                        model = new Model(fallback);
                        model.Request(1000);
                        attempts = 0;
                        while (!model.IsLoaded && attempts < 50)
                        {
                            Script.Wait(20);
                            attempts++;
                        }
                        return model.IsLoaded;
                    }
                    // For props, fail gracefully rather than spawning an out-of-place wooden supply crate!
                    return false;
                }

                return model.IsLoaded;
            }
            catch (Exception ex)
            {
                Log.Error("Spawner", ex, "Exception loading model \"{0}\"", modelName);
                return false;
            }
        }

        public Model SafeLoadModel(string modelName, bool isPed = false)
        {
            Model m;
            SafeLoadModel(modelName, out m, isPed);
            return m;
        }

        public bool SafeLoadVehicleModel(string modelName, out Model model)
        {
            model = new Model(0);
            try
            {
                model = new Model(modelName);
                model.Request(1000);
                int attempts = 0;
                while (!model.IsLoaded && attempts < 50)
                {
                    Script.Wait(20);
                    attempts++;
                }

                if (!model.IsLoaded)
                {
                    Log.Warn("Spawner", "Failed to load vehicle model \"{0}\" after {1} attempts. Falling back to police vehicle.", modelName, attempts);
                    model.MarkAsNoLongerNeeded();
                    model = new Model(VehicleHash.Police);
                    model.Request(1000);
                    attempts = 0;
                    while (!model.IsLoaded && attempts < 50)
                    {
                        Script.Wait(20);
                        attempts++;
                    }
                }

                return model.IsLoaded;
            }
            catch (Exception ex)
            {
                Log.Error("Spawner", ex, "Exception loading vehicle model \"{0}\"", modelName);
                return false;
            }
        }

        public Model SafeLoadVehicleModel(string modelName)
        {
            Model m;
            SafeLoadVehicleModel(modelName, out m);
            return m;
        }

        public void SetVehicleColorByName(Vehicle veh, string colorName)
        {
            if (veh == null || !veh.Exists() || string.IsNullOrEmpty(colorName)) return;
            string c = colorName.ToLowerInvariant();
            try
            {
                if (c.Contains("matte") && c.Contains("black")) veh.Mods.PrimaryColor = VehicleColor.MatteBlack;
                else if (c.Contains("black")) veh.Mods.PrimaryColor = VehicleColor.MetallicBlack;
                else if (c.Contains("red")) veh.Mods.PrimaryColor = VehicleColor.MetallicRed;
                else if (c.Contains("blue")) veh.Mods.PrimaryColor = VehicleColor.MetallicBlue;
                else if (c.Contains("white")) veh.Mods.PrimaryColor = VehicleColor.PureWhite;
                else if (c.Contains("yellow")) veh.Mods.PrimaryColor = VehicleColor.MatteYellow;
                else if (c.Contains("green")) veh.Mods.PrimaryColor = VehicleColor.MetallicDarkGreen;
                else if (c.Contains("silver") || c.Contains("grey") || c.Contains("gray")) veh.Mods.PrimaryColor = VehicleColor.MetallicSilver;
            }
            catch { }
        }

        public void ApplyAtmosphere(EnvironmentAtmosphereData atmo, Vector3 centerPos)
        {
            if (atmo == null) return;

            try
            {
                if (!_atmosphereModified)
                {
                    _originalWeather = World.Weather;
                    _originalHour = Function.Call<int>(Hash.GET_CLOCK_HOURS);
                    _originalMinute = Function.Call<int>(Hash.GET_CLOCK_MINUTES);
                }

                if (!string.IsNullOrEmpty(atmo.Weather))
                {
                    Weather w;
                    if (Enum.TryParse<Weather>(atmo.Weather, true, out w))
                    {
                        World.Weather = w;
                        _atmosphereModified = true;
                    }
                }

                if (atmo.TimeHours >= 0 && atmo.TimeHours <= 23)
                {
                    int mins = Math.Max(0, Math.Min(59, atmo.TimeMinutes));
                    Function.Call(Hash.SET_CLOCK_TIME, atmo.TimeHours, mins, 0);
                    _atmosphereModified = true;
                }

                if (!string.IsNullOrEmpty(atmo.AmbientFx))
                {
                    string fx = atmo.AmbientFx.ToLowerInvariant();
                    if (fx.Contains("bonfire") || fx.Contains("fire"))
                    {
                        int fHandle = Function.Call<int>(Hash.START_SCRIPT_FIRE, centerPos.X, centerPos.Y, centerPos.Z, 1, false);
                        if (fHandle > 0) _spawnedScriptFires.Add(fHandle);
                    }
                }
            }
            catch { }
        }

        public void ResetAtmosphere()
        {
            try
            {
                if (_atmosphereModified)
                {
                    World.Weather = _originalWeather;
                    Function.Call(Hash.SET_CLOCK_TIME, _originalHour, _originalMinute, 0);
                    _atmosphereModified = false;
                }

                foreach (var fHandle in _spawnedScriptFires)
                {
                    try
                    {
                        Function.Call(Hash.REMOVE_SCRIPT_FIRE, fHandle);
                    }
                    catch { }
                }
                _spawnedScriptFires.Clear();
            }
            catch { }
        }

        public void SpawnEnvironmentFromPlanDirect(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Ped playerPed,
            EnvironmentRecord envRecord,
            InteriorManager interiorManager,
            NavMeshBlockerManager navBlockers,
            BlipManager blips,
            NpcActivityDirector activityDirector,
            out int propsCount,
            out int npcsCount,
            out int vehsCount)
        {
            SpawnEnvironmentFromPlanDirect(plan, data, playerPed, envRecord, interiorManager, navBlockers, blips, activityDirector, out propsCount, out npcsCount, out vehsCount, null);
        }

        public void SpawnEnvironmentFromPlanDirect(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Ped playerPed,
            EnvironmentRecord envRecord,
            InteriorManager interiorManager,
            NavMeshBlockerManager navBlockers,
            BlipManager blips,
            NpcActivityDirector activityDirector,
            out int propsCount,
            out int npcsCount,
            out int vehsCount,
            HashSet<string> deadEntityIds)
        {
            propsCount = 0;
            npcsCount = 0;
            vehsCount = 0;

            if (plan == null) return;

            Log.Breadcrumb("SpawnEnvironmentFromPlanDirect: envId=\"{0}\", planId=\"{1}\"", plan.EnvironmentId, plan.PlanId);
            Log.Info("Spawner", "Beginning physical realization for \"{0}\" (PlanId: {1}). Props: {2}, Vehs: {3}, NPCs: {4}",
                plan.EnvironmentId, plan.PlanId,
                plan.ResolvedProps != null ? plan.ResolvedProps.Count : 0,
                plan.ResolvedVehicles != null ? plan.ResolvedVehicles.Count : 0,
                plan.ResolvedNpcs != null ? plan.ResolvedNpcs.Count : 0);

            // 1. Apply Atmosphere
            try
            {
                ApplyAtmosphere(plan.Atmosphere, plan.CenterPosition);
            }
            catch (Exception ex)
            {
                Log.Warn("Spawner", "Failed applying atmosphere: {0}", ex.Message);
            }

            // 2. Spawn Resolved Props with 3D Hierarchical Transforms
            Dictionary<string, Prop> spawnedPropsById = new Dictionary<string, Prop>(StringComparer.OrdinalIgnoreCase);

            if (plan.ResolvedProps != null && plan.ResolvedProps.Count > 0)
            {
                foreach (var propPlan in plan.ResolvedProps)
                {
                    try
                    {
                        Model model;
                        if (SafeLoadModel(propPlan.ModelName, out model, false))
                        {
                            Vector3 rot = propPlan.WorldRotation;
                            Vector3 spawnPos = propPlan.WorldPosition;

                            if (!string.IsNullOrEmpty(propPlan.ParentPropId) && spawnedPropsById.ContainsKey(propPlan.ParentPropId))
                            {
                                Prop parentProp = spawnedPropsById[propPlan.ParentPropId];
                                if (parentProp != null && parentProp.Exists())
                                {
                                    spawnPos = new Vector3(propPlan.WorldPosition.X, propPlan.WorldPosition.Y, parentProp.Position.Z + propPlan.Dimensions.Z);
                                }
                            }

                            Prop spawnedProp = World.CreateProp(model, spawnPos, rot, propPlan.IsDynamic, false);
                            model.MarkAsNoLongerNeeded();

                            if (spawnedProp != null && spawnedProp.Exists())
                            {
                                spawnedProp.IsPersistent = true;

                                if (string.IsNullOrEmpty(propPlan.ParentPropId))
                                {
                                    Function.Call(Hash.PLACE_OBJECT_ON_GROUND_PROPERLY, spawnedProp.Handle);
                                }
                                spawnedProp.IsPositionFrozen = !propPlan.IsDynamic;

                                if (interiorManager != null)
                                {
                                    interiorManager.BindEntityToInteriorRoom(spawnedProp, playerPed);
                                }
                                RegisterProp(plan.EnvironmentId, spawnedProp);
                                if (!string.IsNullOrEmpty(propPlan.PropId))
                                {
                                    spawnedPropsById[propPlan.PropId] = spawnedProp;
                                }
                                propsCount++;

                                if (propPlan.BlocksNavMesh && navBlockers != null)
                                {
                                    navBlockers.AddWallBlocker(plan.EnvironmentId, spawnedProp.Position, propPlan.Dimensions.X, propPlan.Dimensions.Y, propPlan.Dimensions.Z, rot.Z);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Spawner", ex, "Failed to spawn prop {0}", propPlan != null ? propPlan.ModelName : "unknown");
                    }
                }
            }

            // 3. Spawn Resolved Vehicles
            Dictionary<string, Vehicle> spawnedVehiclesMap = new Dictionary<string, Vehicle>(StringComparer.OrdinalIgnoreCase);

            if (plan.ResolvedVehicles != null && plan.ResolvedVehicles.Count > 0)
            {
                foreach (var vPlan in plan.ResolvedVehicles)
                {
                    try
                    {
                        Model vModel;
                        if (SafeLoadVehicleModel(vPlan.ModelName, out vModel))
                        {
                            Vehicle veh = World.CreateVehicle(vModel, vPlan.WorldPosition, vPlan.Heading);
                            vModel.MarkAsNoLongerNeeded();

                            if (veh != null && veh.Exists())
                            {
                                veh.IsPersistent = true;
                                veh.PlaceOnGround();
                                SetVehicleColorByName(veh, vPlan.PrimaryColor);

                                if (vPlan.IsEngineRunning)
                                {
                                    veh.IsEngineRunning = true;
                                }
                                if (vPlan.Sirens && veh.HasSiren)
                                {
                                    veh.IsSirenActive = true;
                                    veh.IsSirenSilent = false;
                                }
                                if (vPlan.LightsOn)
                                {
                                    veh.AreLightsOn = true;
                                }

                                if (interiorManager != null)
                                {
                                    interiorManager.BindEntityToInteriorRoom(veh, playerPed);
                                }
                                RegisterVehicle(plan.EnvironmentId, veh);
                                if (!string.IsNullOrEmpty(vPlan.VehicleId))
                                {
                                    spawnedVehiclesMap[vPlan.VehicleId] = veh;
                                }
                                vehsCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Spawner", ex, "Failed to spawn vehicle {0}", vPlan != null ? vPlan.ModelName : "unknown");
                    }
                }
            }

            // 4. Initialize Environmental Affordances & Spawn Resolved Living NPCs
            string venueModel = plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "";
            string venueName = plan.MatchedInterior != null ? plan.MatchedInterior.Name : "";
            var affordancePoints = SpatialAffordanceScanner.ScanInterestPoints(plan.CenterPosition, 35.0f, venueModel, venueName);
            if (activityDirector != null)
            {
                activityDirector.InitializeVenueAffordances(affordancePoints);
            }

            List<ActiveNpcContext> newlySpawnedNpcs = new List<ActiveNpcContext>();
            if (plan.ResolvedNpcs != null && plan.ResolvedNpcs.Count > 0)
            {
                int nIdx = 0;
                foreach (var npcPlan in plan.ResolvedNpcs)
                {
                    try
                    {
                        if (deadEntityIds != null && (
                            (!string.IsNullOrEmpty(npcPlan.EntityId) && deadEntityIds.Contains(npcPlan.EntityId)) ||
                            (!string.IsNullOrEmpty(npcPlan.Name) && deadEntityIds.Contains(npcPlan.Name)) ||
                            (envRecord != null && envRecord.NpcIds != null && nIdx < envRecord.NpcIds.Count && deadEntityIds.Contains(envRecord.NpcIds[nIdx].ToString()))))
                        {
                            nIdx++;
                            continue;
                        }

                        Model pModel;
                        if (SafeLoadModel(npcPlan.PedModel, out pModel, true))
                        {
                            Ped spawnedPed = World.CreatePed(pModel, npcPlan.WorldPosition, npcPlan.Heading);
                            pModel.MarkAsNoLongerNeeded();

                            if (spawnedPed != null && spawnedPed.Exists())
                            {
                                spawnedPed.IsPersistent = true;

                                if (interiorManager != null)
                                {
                                    interiorManager.BindEntityToInteriorRoom(spawnedPed, playerPed);
                                }

                                if (!string.IsNullOrEmpty(npcPlan.Weapon))
                                {
                                    WeaponHash wHash;
                                    if (Enum.TryParse<WeaponHash>(npcPlan.Weapon.Replace("WEAPON_", ""), true, out wHash))
                                    {
                                        spawnedPed.Weapons.Give(wHash, 100, false, true);
                                    }
                                }

                                string taskLower = (npcPlan.InitialTask ?? "").ToLowerInvariant();
                                string roleLower = (npcPlan.HiddenRole ?? npcPlan.Occupation ?? "").ToLowerInvariant();

                                if (activityDirector != null)
                                {
                                    if (taskLower == "swim_in_pool" || roleLower.Contains("swim"))
                                    {
                                        activityDirector.RegisterNpc(spawnedPed, "Pool Swimmer", "SWIMMING");
                                    }
                                    else if (taskLower == "sunbathe" || roleLower.Contains("sunbathe"))
                                    {
                                        activityDirector.RegisterNpc(spawnedPed, "Pool Sunbather", "WORLD_HUMAN_SUNBATHE");
                                    }
                                    else if (taskLower == "art_tour" || (!roleLower.Contains("cartel") && System.Text.RegularExpressions.Regex.IsMatch(roleLower, @"\b(art|gallery|exhibit|connoisseur)\b")))
                                    {
                                        activityDirector.RegisterNpc(spawnedPed, "Art Connoisseur", npcPlan.Scenario);
                                    }
                                    else if (taskLower == "party_roam" || roleLower.Contains("party") || roleLower.Contains("guest") || roleLower.Contains("vip") || roleLower.Contains("dancer"))
                                    {
                                        activityDirector.RegisterNpc(spawnedPed, "Party Guest", npcPlan.Scenario);
                                    }
                                    else if (!string.IsNullOrEmpty(npcPlan.Scenario))
                                    {
                                        spawnedPed.BlockPermanentEvents = true;
                                        spawnedPed.Task.StartScenarioInPlace(npcPlan.Scenario, 0, false);
                                    }
                                    else
                                    {
                                        spawnedPed.BlockPermanentEvents = false;
                                    }
                                }

                                var record = new NpcRecord
                                {
                                    NpcId = (envRecord != null && envRecord.NpcIds != null && nIdx < envRecord.NpcIds.Count) ? envRecord.NpcIds[nIdx] : (1000 + nIdx),
                                    EntityId = !string.IsNullOrEmpty(npcPlan.EntityId) ? npcPlan.EntityId : (envRecord != null && envRecord.ResidentEntityIds != null && nIdx < envRecord.ResidentEntityIds.Count ? envRecord.ResidentEntityIds[nIdx] : string.Format("npc_{0}", 1000 + nIdx)),
                                    Name = npcPlan.Name,
                                    Occupation = npcPlan.Occupation,
                                    PublicFaction = npcPlan.PublicFaction,
                                    PedModel = npcPlan.PedModel,
                                    HiddenRole = npcPlan.HiddenRole,
                                    CurrentState = npcPlan.InitialState,
                                    Relationships = npcPlan.Relationships
                                };

                                var context = new ActiveNpcContext(spawnedPed, record, plan.EnvironmentId, record.EntityId);
                                RegisterNpc(plan.EnvironmentId, context);
                                newlySpawnedNpcs.Add(context);
                                nIdx++;

                                if (blips != null)
                                {
                                    blips.CreateNpcBlip(spawnedPed, npcPlan.Name, plan.EnvironmentId, BlipColor.Green, BlipSprite.Standard);
                                }
                                npcsCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Spawner", ex, "Failed to spawn NPC {0} (model: {1})", npcPlan != null ? npcPlan.Name : "unknown", npcPlan != null ? npcPlan.PedModel : "unknown");
                    }
                }
            }

            // 5. Seat Designated Driver NPCs into Vehicles
            if (plan.ResolvedVehicles != null)
            {
                foreach (var vPlan in plan.ResolvedVehicles)
                {
                    if (!string.IsNullOrEmpty(vPlan.DriverEntityId) && !string.IsNullOrEmpty(vPlan.VehicleId))
                    {
                        Vehicle targetVeh;
                        if (spawnedVehiclesMap.TryGetValue(vPlan.VehicleId, out targetVeh) && targetVeh != null && targetVeh.Exists())
                        {
                            ActiveNpcContext driverCtx = newlySpawnedNpcs.Find(c =>
                                c != null && c.IsValid && (
                                    string.Equals(c.Record.Name, vPlan.DriverEntityId, StringComparison.OrdinalIgnoreCase) ||
                                    c.Record.NpcId.ToString() == vPlan.DriverEntityId
                                )
                            );

                            if (driverCtx != null && driverCtx.IsValid)
                            {
                                driverCtx.PedInstance.SetIntoVehicle(targetVeh, VehicleSeat.Driver);
                            }
                        }
                    }
                }
            }

            // 6. Execute Initial Staged Choreography
            for (int i = 0; i < plan.ResolvedNpcs.Count && i < newlySpawnedNpcs.Count; i++)
            {
                var npcPlan = plan.ResolvedNpcs[i];
                var ctx = newlySpawnedNpcs[i];
                if (ctx != null && !string.IsNullOrEmpty(npcPlan.InitialTask))
                {
                    ctx.ExecuteInitialChoreography(npcPlan.InitialTask, _activeNpcContexts);
                }
            }
        }

        public void SpawnStash(
            CreateStashData data,
            Ped player,
            BlipManager blips,
            WorldMemoryStore store = null)
        {
            if (data == null || player == null) return;

            Vector3 playerPos = player.Position;
            float heading = player.Heading;

            float localX = data.Offset != null ? data.Offset.X : 2.0f;
            float localY = data.Offset != null ? data.Offset.Y : 3.0f;
            float localZ = data.Offset != null ? data.Offset.Z : 0.0f;

            CalculatedTransform tf = PropPlacementEngine.LocalToWorld(playerPos.X, playerPos.Y, playerPos.Z, heading, localX, localY, localZ);
            Vector3 stashPos = new Vector3(tf.X, tf.Y, tf.Z);
            float gZ = World.GetGroundHeight(stashPos);
            if (gZ > 0) stashPos.Z = gZ;

            string propModelName = string.IsNullOrEmpty(data.ContainerProp) ? "prop_box_ammo04a" : data.ContainerProp;
            Model model;
            if (SafeLoadModel(propModelName, out model, false))
            {
                Prop stashProp = World.CreateProp(model, stashPos, new Vector3(0, 0, tf.Heading), false, false);
                model.MarkAsNoLongerNeeded();

                if (stashProp != null && stashProp.Exists())
                {
                    stashProp.IsPersistent = true;
                    _spawnedProps.Add(stashProp);

                    if (store != null)
                    {
                        store.CreateProp(propModelName, stashPos.X, stashPos.Y, stashPos.Z, tf.Heading, 0f, 0f, false, "stash");
                    }

                    if (blips != null)
                    {
                        blips.CreateCoordBlip(stashPos, data.Title ?? "Contraband Stash", BlipColor.Yellow, BlipSprite.DollarSign);
                    }

                    Notification.Show(string.Format("~g~[Secret Stash: {0}]~w~~n~{1}~n~~y~Marked on Radar!", data.Title ?? "Hidden Cache", data.Description ?? "Contraband found."));
                }
            }
        }

        public void ClearAllSpawnedEntities(
            NavMeshBlockerManager navBlockers,
            TeleportPocketManager teleportPockets,
            InteriorManager interiorManager,
            BlipManager blips,
            PartyAudioManager partyAudio,
            NpcActivityDirector activityDirector,
            DirectorTimelineManager timelineManager)
        {
            if (partyAudio != null) partyAudio.StopAudio();
            if (activityDirector != null) activityDirector.ClearAll();
            if (blips != null) blips.ClearAllBlips();
            if (navBlockers != null) navBlockers.ClearAllBlockers();
            if (teleportPockets != null) teleportPockets.ClearAllPockets();
            if (timelineManager != null) timelineManager.Reset();
            if (interiorManager != null) interiorManager.ClearCurrentInteriorBlip();

            foreach (var prop in _spawnedProps)
            {
                try
                {
                    if (prop != null && prop.Exists()) prop.Delete();
                }
                catch { }
            }
            _spawnedProps.Clear();
            _propsByEnvironment.Clear();

            foreach (var ctx in _activeNpcContexts)
            {
                try
                {
                    if (ctx != null && ctx.IsValid && ctx.PedInstance != null && ctx.PedInstance.Exists())
                    {
                        ctx.PedInstance.Delete();
                    }
                }
                catch { }
            }
            _activeNpcContexts.Clear();
            _npcsByEnvironment.Clear();

            foreach (var veh in _spawnedVehicles)
            {
                try
                {
                    if (veh != null && veh.Exists()) veh.Delete();
                }
                catch { }
            }
            _spawnedVehicles.Clear();
            _vehiclesByEnvironment.Clear();

            ResetAtmosphere();
        }
    }
}
