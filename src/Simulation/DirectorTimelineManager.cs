using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using GTA;
using GTA.Math;
using GTA.UI;

namespace DynamicWorldEngine.Simulation
{
    public class RuntimeScenarioBeat
    {
        public ScenarioBeatData Data { get; set; }
        public bool IsExecuted { get; set; }

        public RuntimeScenarioBeat(ScenarioBeatData data)
        {
            Data = data;
            IsExecuted = false;
        }
    }

    public class DirectorTimelineManager
    {
        private ScenarioDirectorData _currentScenario = null;
        private readonly List<RuntimeScenarioBeat> _runtimeBeats = new List<RuntimeScenarioBeat>();
        private DateTime _scenarioStartTime = DateTime.MinValue;
        private Vector3 _sceneOrigin = Vector3.Zero;
        private string _activeEnvironmentId = string.Empty;
        private readonly List<ActiveNpcContext> _boundNpcs = new List<ActiveNpcContext>();

        public bool IsScenarioActive
        {
            get { return _currentScenario != null && _runtimeBeats.Exists(b => !b.IsExecuted); }
        }

        public string ActiveScenarioTitle
        {
            get { return _currentScenario != null ? _currentScenario.ScenarioTitle : string.Empty; }
        }

        public void LoadScenario(
            ScenarioDirectorData scenario,
            string environmentId,
            Vector3 sceneOrigin,
            List<ActiveNpcContext> npcs)
        {
            Reset();

            if (scenario == null || scenario.Beats == null || scenario.Beats.Count == 0) return;

            _currentScenario = scenario;
            _activeEnvironmentId = environmentId;
            _sceneOrigin = sceneOrigin;
            _scenarioStartTime = DateTime.UtcNow;

            _boundNpcs.Clear();
            if (npcs != null)
            {
                _boundNpcs.AddRange(npcs);
            }

            foreach (var beat in scenario.Beats)
            {
                _runtimeBeats.Add(new RuntimeScenarioBeat(beat));
            }

            if (!string.IsNullOrEmpty(scenario.NarrativeIntro))
            {
                try
                {
                    Notification.Show(string.Format("~y~[Director: {0}]~w~~n~{1}", 
                        string.IsNullOrEmpty(scenario.ScenarioTitle) ? "Active Scenario" : scenario.ScenarioTitle, 
                        scenario.NarrativeIntro));
                }
                catch { }
            }
        }

        public void ProcessTimeline(
            Ped playerPed,
            Action<TriggerWorldEventData> onFireWorldEvent,
            Action<MutateEntityData> onFireMutation)
        {
            if (!IsScenarioActive) return;

            DateTime now = DateTime.UtcNow;
            double elapsedSeconds = (now - _scenarioStartTime).TotalSeconds;

            foreach (var rBeat in _runtimeBeats)
            {
                if (rBeat.IsExecuted) continue;

                var beat = rBeat.Data;
                bool shouldTrigger = false;

                string trig = (beat.Trigger ?? "timer").ToLowerInvariant();

                if (trig == "timer")
                {
                    if (elapsedSeconds >= beat.DelaySeconds)
                    {
                        shouldTrigger = true;
                    }
                }
                else if (trig == "on_player_approach" && playerPed != null && playerPed.Exists())
                {
                    float dist = playerPed.Position.DistanceTo(_sceneOrigin);
                    if (dist < 5.5f)
                    {
                        shouldTrigger = true;
                    }
                }
                else if (trig == "on_weapon_drawn" && playerPed != null && playerPed.Exists())
                {
                    if (playerPed.IsAiming || (playerPed.Weapons.Current != null && playerPed.Weapons.Current.Hash != WeaponHash.Unarmed))
                    {
                        shouldTrigger = true;
                    }
                }
                else if (trig == "on_combat")
                {
                    if (playerPed != null && playerPed.Exists() && playerPed.IsInCombat)
                    {
                        shouldTrigger = true;
                    }
                    else
                    {
                        foreach (var npc in _boundNpcs)
                        {
                            if (npc != null && npc.IsValid && (npc.PedInstance.IsInCombat || npc.PedInstance.IsDead))
                            {
                                shouldTrigger = true;
                                break;
                            }
                        }
                    }
                }

                if (shouldTrigger)
                {
                    rBeat.IsExecuted = true;
                    ExecuteBeat(beat, onFireWorldEvent, onFireMutation);
                }
            }
        }

        private void ExecuteBeat(
            ScenarioBeatData beat,
            Action<TriggerWorldEventData> onFireWorldEvent,
            Action<MutateEntityData> onFireMutation)
        {
            if (!string.IsNullOrEmpty(beat.Announcement))
            {
                try
                {
                    Notification.Show(string.Format("~r~[Scenario Event]~w~~n~{0}", beat.Announcement));
                }
                catch { }
            }

            // 1. Fire World Event (e.g. Vehicle Raid / Police Backup / Ambush)
            if (beat.WorldEvent != null && onFireWorldEvent != null)
            {
                onFireWorldEvent(beat.WorldEvent);
            }

            // 2. Fire Entity Mutations (e.g. Combat, Flee, Draw Weapon)
            if (beat.Mutations != null && beat.Mutations.Count > 0 && onFireMutation != null)
            {
                foreach (var mutation in beat.Mutations)
                {
                    onFireMutation(mutation);
                }
            }
        }

        public void Reset()
        {
            _currentScenario = null;
            _runtimeBeats.Clear();
            _boundNpcs.Clear();
            _scenarioStartTime = DateTime.MinValue;
            _sceneOrigin = Vector3.Zero;
            _activeEnvironmentId = string.Empty;
        }

        public void ResetIfEnvironment(string environmentId)
        {
            if (!string.IsNullOrEmpty(environmentId) && string.Equals(_activeEnvironmentId, environmentId, StringComparison.OrdinalIgnoreCase))
            {
                Reset();
            }
        }
    }
}
