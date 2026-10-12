using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Audio;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;
using DynamicWorldEngine.Spatial;
using GTA;

namespace DynamicWorldEngine.Runtime
{
    /// <summary>
    /// Authoritative live GTA runtime execution engine.
    /// Realizes a canonical SpatialExecutionPlan into physical GTA entities,
    /// IPL interiors, minimap blips, navigation blockers, and teleport pockets.
    /// Manages both environment-scoped and global runtime lifecycle deallocation.
    /// </summary>
    public class RuntimeExecutor
    {
        private readonly EntitySpawnerService _spawner;
        private readonly NavMeshBlockerManager _navBlockers;
        private readonly BlipManager _blips;
        private readonly TeleportPocketManager _teleportPockets;
        private readonly InteriorManager _interiorManager;
        private readonly PartyAudioManager _partyAudio;
        private readonly NpcActivityDirector _activityDirector;
        private readonly DirectorTimelineManager _timelineManager;

        public RuntimeExecutor(
            EntitySpawnerService spawner,
            NavMeshBlockerManager navBlockers,
            BlipManager blips,
            TeleportPocketManager teleportPockets,
            InteriorManager interiorManager,
            PartyAudioManager partyAudio = null,
            NpcActivityDirector activityDirector = null,
            DirectorTimelineManager timelineManager = null)
        {
            _spawner = spawner ?? new EntitySpawnerService();
            _navBlockers = navBlockers ?? new NavMeshBlockerManager();
            _blips = blips ?? new BlipManager();
            _teleportPockets = teleportPockets ?? new TeleportPocketManager();
            _interiorManager = interiorManager ?? new InteriorManager(_teleportPockets, _blips);
            _partyAudio = partyAudio ?? new PartyAudioManager();
            _activityDirector = activityDirector ?? new NpcActivityDirector();
            _timelineManager = timelineManager ?? new DirectorTimelineManager();
        }

        public EntitySpawnerService Spawner { get { return _spawner; } }
        public NavMeshBlockerManager NavBlockers { get { return _navBlockers; } }
        public BlipManager Blips { get { return _blips; } }
        public TeleportPocketManager TeleportPockets { get { return _teleportPockets; } }
        public InteriorManager InteriorManager { get { return _interiorManager; } }
        public PartyAudioManager PartyAudio { get { return _partyAudio; } }
        public NpcActivityDirector ActivityDirector { get { return _activityDirector; } }
        public DirectorTimelineManager TimelineManager { get { return _timelineManager; } }

        /// <summary>
        /// Begins a transactional realization scope for an environment.
        /// Follows Stage-Before-Commit: entities staged during this scope will be
        /// automatically rolled back via CleanUpEnvironment if the scope is disposed without being committed.
        /// </summary>
        public RealizationScope BeginRealizationScope(string environmentId)
        {
            return new RealizationScope(this, environmentId);
        }

        /// <summary>
        /// Realizes a canonical SpatialExecutionPlan in the live GTA V engine.
        /// Activates IPLs, registers teleport pockets, streams and spawns props/peds/vehicles,
        /// and initializes scenario timelines and ambient audio.
        /// </summary>
        public void RealizeEnvironmentFromPlan(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Ped playerPed,
            EnvironmentRecord envRecord,
            out int propsCount,
            out int npcsCount,
            out int vehsCount)
        {
            RealizeEnvironmentFromPlan(plan, data, playerPed, envRecord, out propsCount, out npcsCount, out vehsCount, null);
        }

        public void RealizeEnvironmentFromPlan(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Ped playerPed,
            EnvironmentRecord envRecord,
            out int propsCount,
            out int npcsCount,
            out int vehsCount,
            HashSet<string> deadEntityIds)
        {
            if (plan == null) throw new ArgumentNullException("plan");

            // 1. Activate Interior or Register Teleport Pockets
            if (plan.MatchedInterior != null)
            {
                _interiorManager.ActivateInterior(plan.MatchedInterior, playerPed, true);
            }
            else if (plan.ConnectedPockets != null && plan.ConnectedPockets.Count > 0)
            {
                foreach (var cp in plan.ConnectedPockets)
                {
                    _teleportPockets.RegisterPocket(
                        cp.PocketId,
                        cp.EnvironmentId,
                        cp.StreetCoords,
                        cp.InteriorCoords,
                        cp.InteriorExitCoords,
                        cp.EntranceTitle,
                        cp.IsLocked,
                        cp.IsKnownToPlayer
                    );
                }

                if (plan.IsSkyPocket || (data != null && data.IsSkyPocket))
                {
                    TeleportPocketManager.SafeTeleportPlayer(playerPed, plan.CenterPosition, plan.Heading);
                }
            }
            else if (plan.PlannedPocket != null)
            {
                _teleportPockets.RegisterPocket(
                    plan.PlannedPocket.PocketId,
                    plan.PlannedPocket.EnvironmentId,
                    plan.PlannedPocket.StreetCoords,
                    plan.PlannedPocket.InteriorCoords,
                    plan.PlannedPocket.InteriorExitCoords,
                    plan.PlannedPocket.EntranceTitle,
                    plan.PlannedPocket.IsLocked,
                    plan.PlannedPocket.IsKnownToPlayer
                );

                if (plan.IsSkyPocket || (data != null && data.IsSkyPocket))
                {
                    TeleportPocketManager.SafeTeleportPlayer(playerPed, plan.CenterPosition, plan.Heading);
                }
            }

            // 2. Delegate entity instantiation and navmesh registration to EntitySpawnerService
            _spawner.SpawnEnvironmentFromPlanDirect(
                plan,
                data,
                playerPed,
                envRecord,
                _interiorManager,
                _navBlockers,
                _blips,
                _activityDirector,
                out propsCount,
                out npcsCount,
                out vehsCount,
                deadEntityIds
            );

            // 3. Bind Scenario Director Timeline Beats
            if (plan.ScenarioDirector != null && plan.ScenarioDirector.Beats != null && plan.ScenarioDirector.Beats.Count > 0)
            {
                _timelineManager.LoadScenario(plan.ScenarioDirector, plan.EnvironmentId, plan.CenterPosition, _spawner.ActiveNpcContexts);
            }

            // 4. 3D Spatial Audio & Music Streaming
            string venueModel = plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "";
            string venueName = plan.MatchedInterior != null ? plan.MatchedInterior.Name : "";
            string fullThemeText = (plan.Name + " " + plan.Archetype + " " + venueName + " " + ((data != null) ? data.Description ?? "" : "")).ToLowerInvariant();

            if (fullThemeText.Contains("party") || fullThemeText.Contains("club") || fullThemeText.Contains("mansion") || fullThemeText.Contains("celebrat") || fullThemeText.Contains("dj") || fullThemeText.Contains("dance"))
            {
                _partyAudio.StartPartyAudio(venueModel, venueName);
            }
            else if (Regex.IsMatch(fullThemeText, @"\b(art|museum|gallery|kortz)\b") && !fullThemeText.Contains("cartel") && !fullThemeText.Contains("interrogat") && !fullThemeText.Contains("torture"))
            {
                _partyAudio.StartGalleryAudio(venueModel, venueName);
            }
        }

        /// <summary>
        /// Realizes a stash container in the live GTA world, attaches a radar blip, and persists to WorldMemoryStore.
        /// </summary>
        public void RealizeStash(CreateStashData data, Ped player, WorldMemoryStore store = null)
        {
            if (data == null || player == null) return;
            _spawner.SpawnStash(data, player, _blips, store);
        }

        /// <summary>
        /// Directs an independent scenario timeline on top of an active living environment (t > 0).
        /// Binds to existing active NPCs and spatial origin without respawning or altering physical scene props.
        /// </summary>
        public bool DirectScenario(ScenarioDirectorData scenario, string targetEnvironmentId = null, GTA.Math.Vector3? customOrigin = null)
        {
            if (scenario == null || scenario.Beats == null || scenario.Beats.Count == 0) return false;

            string envId = targetEnvironmentId;
            if (string.IsNullOrEmpty(envId))
            {
                envId = !string.IsNullOrEmpty(scenario.TargetEnvironmentId) ? scenario.TargetEnvironmentId : scenario.EnvironmentId;
            }

            // Resolve origin position
            GTA.Math.Vector3 origin = customOrigin.HasValue ? customOrigin.Value : GTA.Math.Vector3.Zero;
            if (!customOrigin.HasValue)
            {
                try
                {
                    if (Game.Player != null && Game.Player.Character != null)
                    {
                        origin = Game.Player.Character.Position;
                    }
                }
                catch { }
            }

            // Collect target NPCs
            List<ActiveNpcContext> targetNpcs = null;
            if (!string.IsNullOrEmpty(envId))
            {
                targetNpcs = GetNpcsForEnvironment(envId);
            }

            if (targetNpcs == null || targetNpcs.Count == 0)
            {
                targetNpcs = _spawner.ActiveNpcContexts;
            }

            _timelineManager.LoadScenario(scenario, envId, origin, targetNpcs);
            return true;
        }

        public System.Collections.Generic.List<Prop> GetPropsForEnvironment(string environmentId)
        {
            return _spawner.GetPropsForEnvironment(environmentId);
        }

        public System.Collections.Generic.List<ActiveNpcContext> GetNpcsForEnvironment(string environmentId)
        {
            return _spawner.GetNpcsForEnvironment(environmentId);
        }

        public System.Collections.Generic.List<Vehicle> GetVehiclesForEnvironment(string environmentId)
        {
            return _spawner.GetVehiclesForEnvironment(environmentId);
        }

        public bool HasRealizedEntities(string environmentId)
        {
            return _spawner.HasRealizedEntities(environmentId);
        }

        /// <summary>
        /// Cleans up all live GTA runtime objects for a specific environment.
        /// Evicts native GTA entities (props, npcs, vehicles), removes environment-scoped
        /// nav blockers, map blips, teleport pockets, and resets active scenario timeline if matched.
        /// </summary>
        public void CleanUpEnvironment(string environmentId)
        {
            if (string.IsNullOrEmpty(environmentId)) return;

            _spawner.RemoveEntitiesForEnvironment(environmentId);
            _navBlockers.RemoveBlockersForEnvironment(environmentId);
            _blips.RemoveBlipsForEnvironment(environmentId);
            _teleportPockets.RemovePocketsForEnvironment(environmentId);
            if (_timelineManager != null)
            {
                _timelineManager.ResetIfEnvironment(environmentId);
            }
        }

        /// <summary>
        /// Cleans up all live GTA runtime entities, resets atmosphere, and clears all managers.
        /// </summary>
        public void ClearAll()
        {
            _spawner.ClearAllSpawnedEntities(
                _navBlockers,
                _teleportPockets,
                _interiorManager,
                _blips,
                _partyAudio,
                _activityDirector,
                _timelineManager
            );
        }
    }
}
