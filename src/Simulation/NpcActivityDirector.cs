using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Spatial;

namespace DynamicWorldEngine.Simulation
{
    public enum NpcActivityState
    {
        NavigatingToPoint,
        InteractingAtPoint,
        SwimmingInWater,
        SunbathingOnDeck,
        FreeRoam
    }

    public class ActiveNpcActivity
    {
        public Ped PedInstance { get; set; }
        public string ActivityRole { get; set; }
        public NpcActivityState State { get; set; }
        public EnvironmentalInterestPoint CurrentTargetPoint { get; set; }
        public int StateTimer { get; set; }
        public int NavigationTimeout { get; set; }
        public List<string> VisitedPointIds { get; set; }

        public ActiveNpcActivity(Ped ped, string role)
        {
            PedInstance = ped;
            ActivityRole = role ?? "Visitor";
            State = NpcActivityState.FreeRoam;
            VisitedPointIds = new List<string>();
        }

        public bool IsValid
        {
            get { return PedInstance != null && PedInstance.Exists() && !PedInstance.IsDead; }
        }
    }

    /// <summary>
    /// Autonomous Multi-Stop NPC Activity Director.
    /// Directs living NPCs to exploit physical venue affordances dynamically:
    /// walking from painting to painting in art galleries, moving between the pool and bar at parties,
    /// and executing authentic contextual interactions without freezing in place.
    /// </summary>
    public class NpcActivityDirector
    {
        private readonly List<ActiveNpcActivity> _activities = new List<ActiveNpcActivity>();
        private readonly Random _rnd = new Random();
        private List<EnvironmentalInterestPoint> _activeInterestPoints = new List<EnvironmentalInterestPoint>();
        private int _lastTickTime = 0;

        public void InitializeVenueAffordances(List<EnvironmentalInterestPoint> points)
        {
            _activeInterestPoints = points ?? new List<EnvironmentalInterestPoint>();
            Console.WriteLine("[NpcActivityDirector] Initialized with {0} active environmental interest points.", _activeInterestPoints.Count);
        }

        public void RegisterNpc(Ped ped, string role, string requestedScenario = "")
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return;

            string rLower = (role ?? "").ToLowerInvariant();
            string scenLower = (requestedScenario ?? "").ToLowerInvariant();

            var activity = new ActiveNpcActivity(ped, role);

            // A. Swimmer in Water Body
            if (rLower.Contains("swim") || scenLower.Contains("swim"))
            {
                activity.State = NpcActivityState.SwimmingInWater;
                var poolPoint = _activeInterestPoints.Find(p => p.Type == AffordanceType.WaterSwimming);
                if (poolPoint != null)
                {
                    activity.CurrentTargetPoint = poolPoint;
                    CommandSwimTo(ped, poolPoint.Position);
                }
                activity.StateTimer = Game.GameTime + _rnd.Next(20000, 40000);
            }
            // B. Sunbather on Pool Deck / Beach
            else if (rLower.Contains("sunbathe") || scenLower.Contains("sunbathe"))
            {
                activity.State = NpcActivityState.SunbathingOnDeck;
                var sunPoint = _activeInterestPoints.Find(p => p.Type == AffordanceType.WaterSunbathing && p.CurrentOccupants < p.MaxOccupants);
                if (sunPoint != null)
                {
                    sunPoint.CurrentOccupants++;
                    activity.CurrentTargetPoint = sunPoint;
                    ped.Position = sunPoint.Position;
                    ped.Heading = sunPoint.Heading;
                }
                ped.Task.StartScenarioInPlace("WORLD_HUMAN_SUNBATHE", 0, false);
                activity.StateTimer = Game.GameTime + _rnd.Next(30000, 60000);
            }
            // C. Tactical Guard on Post
            else if (rLower.Contains("guard") || scenLower.Contains("guard"))
            {
                activity.State = NpcActivityState.InteractingAtPoint;
                ped.Task.StartScenarioInPlace("WORLD_HUMAN_GUARD_STAND", 0, false);
                activity.StateTimer = Game.GameTime + 9999999; // Retains guard post
            }
            // D. Interrogation Subject, Hostage or Prisoner
            else if (rLower.Contains("hostage") || rLower.Contains("subject") || rLower.Contains("prisoner") || rLower.Contains("undercover") || scenLower.Contains("cower") || scenLower.Contains("chair"))
            {
                activity.State = NpcActivityState.InteractingAtPoint;
                if (scenLower.Contains("cower")) ped.Task.StartScenarioInPlace("WORLD_HUMAN_STAND_COWER", 0, false);
                else ped.Task.StartScenarioInPlace("WORLD_HUMAN_SEAT_CHAIR", 0, false);
                activity.StateTimer = Game.GameTime + 9999999; // Retains interrogation posture
            }
            // E. Interrogator confronting subject
            else if (rLower.Contains("interrogat") || rLower.Contains("inquisitor"))
            {
                activity.State = NpcActivityState.InteractingAtPoint;
                activity.StateTimer = Game.GameTime + 9999999;
            }
            // F. Multi-Stop Exploratory Tour (Art Lovers, Connoisseurs, Partygoers, Bar Patrons)
            else if (_activeInterestPoints.Count > 0)
            {
                // Unblock permanent events so navmesh tasks and walking function smoothly
                ped.BlockPermanentEvents = false;

                // Pick initial interest point matching their archetype
                EnvironmentalInterestPoint target = SelectNextInterestPoint(activity, true);
                if (target != null)
                {
                    activity.CurrentTargetPoint = target;
                    activity.VisitedPointIds.Add(target.PointId);
                    activity.State = NpcActivityState.NavigatingToPoint;
                    activity.NavigationTimeout = Game.GameTime + 30000; // 30s timeout

                    CommandNavigateTo(ped, target.ApproachPosition);
                }
                else
                {
                    ped.Task.WanderAround(ped.Position, 15.0f);
                    activity.State = NpcActivityState.FreeRoam;
                }
            }
            else
            {
                ped.Task.WanderAround(ped.Position, 15.0f);
                activity.State = NpcActivityState.FreeRoam;
            }

            _activities.Add(activity);
        }

        public void OnTick()
        {
            int now = Game.GameTime;
            if (now - _lastTickTime < 500) return; // Process every 500ms
            _lastTickTime = now;

            for (int i = _activities.Count - 1; i >= 0; i--)
            {
                var act = _activities[i];
                if (!act.IsValid)
                {
                    _activities.RemoveAt(i);
                    continue;
                }

                Ped ped = act.PedInstance;
                Vector3 currentPos = ped.Position;

                switch (act.State)
                {
                    case NpcActivityState.NavigatingToPoint:
                        if (act.CurrentTargetPoint != null)
                        {
                            Vector3 targetPos = act.CurrentTargetPoint.ApproachPosition;
                            float dist = currentPos.DistanceTo(targetPos);

                            // Arrived at the affordance point!
                            if (dist < 1.8f || now > act.NavigationTimeout)
                            {
                                act.State = NpcActivityState.InteractingAtPoint;
                                act.StateTimer = now + _rnd.Next(14000, 24000); // Admire for 14-24 seconds

                                // Smoothly orient towards the affordance
                                float targetHeading = act.CurrentTargetPoint.Heading;
                                ped.Heading = targetHeading;

                                // Select authentic scenario
                                string scenario = act.CurrentTargetPoint.RecommendedScenario;
                                if (act.CurrentTargetPoint.Type == AffordanceType.ArtExhibit)
                                {
                                    // Randomize art viewing actions: taking photos, reading brochure, or contemplating
                                    int pick = _rnd.Next(3);
                                    if (pick == 0) scenario = "WORLD_HUMAN_PAPARAZZI";
                                    else if (pick == 1) scenario = "WORLD_HUMAN_TOURIST_MAP";
                                    else scenario = "WORLD_HUMAN_STAND_IMPARTIAL";
                                }

                                ped.Task.StartScenarioInPlace(scenario, 0, false);
                            }
                        }
                        break;

                    case NpcActivityState.InteractingAtPoint:
                        // Interaction duration elapsed: Transition to the NEXT interest point!
                        if (now > act.StateTimer)
                        {
                            EnvironmentalInterestPoint nextPoint = SelectNextInterestPoint(act, false);
                            if (nextPoint != null)
                            {
                                act.CurrentTargetPoint = nextPoint;
                                act.VisitedPointIds.Add(nextPoint.PointId);
                                if (act.VisitedPointIds.Count > 6) act.VisitedPointIds.RemoveAt(0);

                                act.State = NpcActivityState.NavigatingToPoint;
                                act.NavigationTimeout = now + 35000;

                                ped.Task.ClearAll();
                                CommandNavigateTo(ped, nextPoint.ApproachPosition);
                            }
                            else
                            {
                                ped.Task.WanderAround(ped.Position, 20.0f);
                                act.State = NpcActivityState.FreeRoam;
                                act.StateTimer = now + 25000;
                            }
                        }
                        break;

                    case NpcActivityState.SwimmingInWater:
                        // Swimming wander loop inside the pool
                        if (now > act.StateTimer && act.CurrentTargetPoint != null)
                        {
                            act.StateTimer = now + _rnd.Next(15000, 30000);
                            float ox = (float)(_rnd.NextDouble() * 5.0 - 2.5);
                            float oy = (float)(_rnd.NextDouble() * 5.0 - 2.5);
                            Vector3 swimDest = new Vector3(act.CurrentTargetPoint.Position.X + ox, act.CurrentTargetPoint.Position.Y + oy, act.CurrentTargetPoint.Position.Z);
                            CommandSwimTo(ped, swimDest);
                        }
                        break;

                    case NpcActivityState.SunbathingOnDeck:
                        // Sunbathers periodically adjust or get up after long duration
                        if (now > act.StateTimer)
                        {
                            act.StateTimer = now + 60000;
                            ped.Task.StartScenarioInPlace("WORLD_HUMAN_SUNBATHE", 0, false);
                        }
                        break;

                    case NpcActivityState.FreeRoam:
                        if (now > act.StateTimer && _activeInterestPoints.Count > 0)
                        {
                            EnvironmentalInterestPoint target = SelectNextInterestPoint(act, false);
                            if (target != null)
                            {
                                act.CurrentTargetPoint = target;
                                act.VisitedPointIds.Add(target.PointId);
                                act.State = NpcActivityState.NavigatingToPoint;
                                act.NavigationTimeout = now + 30000;
                                ped.Task.ClearAll();
                                CommandNavigateTo(ped, target.ApproachPosition);
                            }
                        }
                        break;
                }
            }
        }

        private EnvironmentalInterestPoint SelectNextInterestPoint(ActiveNpcActivity act, bool isInitial)
        {
            if (_activeInterestPoints == null || _activeInterestPoints.Count == 0) return null;

            string rLower = act.ActivityRole.ToLowerInvariant();
            var candidates = new List<EnvironmentalInterestPoint>();

            // Prioritize points matching their specific role
            if (!rLower.Contains("cartel") && (System.Text.RegularExpressions.Regex.IsMatch(rLower, @"\b(art|visitor|tourist|connoisseur|photo)\b") || rLower.Contains("gallery") || rLower.Contains("exhibit")))
            {
                foreach (var p in _activeInterestPoints)
                {
                    if (p.Type == AffordanceType.ArtExhibit || p.Type == AffordanceType.BalconyScenic) candidates.Add(p);
                }
            }
            else if (rLower.Contains("party") || rLower.Contains("vip") || rLower.Contains("guest"))
            {
                foreach (var p in _activeInterestPoints)
                {
                    if (p.Type == AffordanceType.BarService || p.Type == AffordanceType.DanceFloor || p.Type == AffordanceType.BalconyScenic || p.Type == AffordanceType.SeatingLounge)
                    {
                        candidates.Add(p);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                candidates.AddRange(_activeInterestPoints);
            }

            // Filter out recently visited points to encourage diverse multi-stop exploration
            var unvisited = candidates.FindAll(c => !act.VisitedPointIds.Contains(c.PointId));
            var pool = (unvisited.Count > 0) ? unvisited : candidates;

            return pool[_rnd.Next(pool.Count)];
        }

        private void CommandNavigateTo(Ped ped, Vector3 destination)
        {
            try
            {
                ped.Task.FollowNavMeshTo(destination, PedMoveBlendRatio.Walk, -1, 0.25f, FollowNavMeshFlags.Default, 0f);
            }
            catch
            {
                try
                {
                    Function.Call(Hash.TASK_GO_TO_COORD_ANY_MEANS, ped.Handle, destination.X, destination.Y, destination.Z, 1.0f, 0, 0, 786603, -1.0f);
                }
                catch { }
            }
        }

        private void CommandSwimTo(Ped ped, Vector3 destination)
        {
            try
            {
                ped.Task.FollowNavMeshTo(destination, PedMoveBlendRatio.Walk, -1, 0.25f, FollowNavMeshFlags.AllowSwimmingUnderwater, 0f);
            }
            catch
            {
                try
                {
                    Function.Call(Hash.TASK_GO_TO_COORD_ANY_MEANS, ped.Handle, destination.X, destination.Y, destination.Z, 1.0f, 0, 0, 786603, -1.0f);
                }
                catch { }
            }
        }

        public void ClearAll()
        {
            foreach (var act in _activities)
            {
                if (act != null && act.IsValid)
                {
                    try { act.PedInstance.Task.ClearAll(); } catch { }
                }
            }
            _activities.Clear();
            _activeInterestPoints.Clear();
        }
    }
}
