using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Simulation;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Autonomous Spatial Perception Scanner.
    /// Physically probes a 50-100m radius around candidate coordinates using native GTA V geometry,
    /// raycasts, road/sidewalk nodes, and living entity censuses to eliminate "spawning while blind".
    /// Fully resilient with headless fallbacks for testing outside the GTA V runtime.
    /// </summary>
    public class SpatialPerceptionScanner
    {
        public static int MainThreadId = -1;
        public static bool IsMainThread
        {
            get { return MainThreadId == -1 || System.Threading.Thread.CurrentThread.ManagedThreadId == MainThreadId; }
        }

        private static readonly Vector3[] CompassDirections = new Vector3[]
        {
            new Vector3(0f, 1f, 0f),       // 0: North
            new Vector3(0.707f, 0.707f, 0f),// 1: North-East
            new Vector3(1f, 0f, 0f),       // 2: East
            new Vector3(0.707f, -0.707f, 0f),// 3: South-East
            new Vector3(0f, -1f, 0f),      // 4: South
            new Vector3(-0.707f, -0.707f, 0f),// 5: South-West
            new Vector3(-1f, 0f, 0f),      // 6: West
            new Vector3(-0.707f, 0.707f, 0f)// 7: North-West
        };

        private readonly SpatialGridSampler _gridSampler = new SpatialGridSampler();

        /// <summary>
        /// Scans and constructs a machine-readable 3D Spatial World Model around target coordinates,
        /// including surfaces, obstacles, paths, candidate areas, and a 3D spatial graph.
        /// </summary>
        public SpatialWorldModel ScanSpatialWorldModel(
            Vector3 origin,
            float radius = 50.0f,
            WorldMemoryStore store = null,
            EphemeralStateRecomputer recomputer = null)
        {
            var snapshot = ScanLocation(origin, radius, store, recomputer);
            var model = _gridSampler.SampleArea(origin, radius, SpatialGridSampler.DefaultCellSize, store, snapshot.ZoneName);
            model.SemanticSnapshot = snapshot;
            if (model.CandidateAreas != null && model.CandidateAreas.Count > 0)
            {
                snapshot.CandidateAreas = model.CandidateAreas;
            }
            return model;
        }

        /// <summary>
        /// Scans a 50-100m radius around target coordinates and returns a structured perception snapshot.
        /// </summary>
        public WorldPerceptionSnapshot ScanLocation(
            Vector3 origin,
            float radius = 50.0f,
            WorldMemoryStore store = null,
            EphemeralStateRecomputer recomputer = null)
        {
            var snapshot = new WorldPerceptionSnapshot
            {
                X = origin.X,
                Y = origin.Y,
                Z = origin.Z,
                Elevation = origin.Z,
                Timestamp = DateTime.UtcNow
            };

            // 1. Ground, Elevation & Slope Assessment
            ProbeGroundAndSlope(origin, radius, snapshot);

            // 2. Roadway, Sidewalk, Street & Zone Assessment
            ProbeRoadAndAddress(origin, snapshot);

            // 3. 8-Compass Horizontal Raycast Enclosure & Alley Detection
            ProbeCompassBoundaries(origin, radius, snapshot);

            // 4. Vertical Space & Ceiling (via SpatialVisionEngine)
            ProbeOverhead(origin, radius, snapshot);

            // 5. Living Entity Census (Peds, Cops, Vehicles)
            ProbeLivingEntities(origin, radius, snapshot);

            // 6. Memory Store, Factions & Location Heat Integration
            ProbeMemoryAndThreats(origin, radius, store, recomputer, snapshot);

            // 7. Feasibility Synthesis
            EvaluatePlacementFeasibility(snapshot);

            // 8. Grounded Candidate Areas via Grid Sampling
            try
            {
                var model = _gridSampler.SampleArea(origin, radius, SpatialGridSampler.DefaultCellSize, store, snapshot.ZoneName);
                if (model != null && model.CandidateAreas != null && model.CandidateAreas.Count > 0)
                {
                    snapshot.CandidateAreas = model.CandidateAreas;
                }
            }
            catch {}

            return snapshot;
        }

        private void ProbeGroundAndSlope(Vector3 origin, float radius, WorldPerceptionSnapshot snapshot)
        {
            if (!IsMainThread)
            {
                snapshot.Elevation = origin.Z;
                snapshot.SlopeDegrees = 1.5f;
                snapshot.TerrainCategory = "Flat Urban Ground";
                snapshot.WalkableAreaPercent = 90.0f;
                return;
            }

            try
            {
                float groundHeight;
                Vector3 normal;
                bool gotNormal = World.GetGroundHeightAndNormal(origin, out groundHeight, out normal);

                if (gotNormal)
                {
                    snapshot.Elevation = groundHeight;
                    float nz = Math.Max(-1.0f, Math.Min(1.0f, normal.Z));
                    float slopeRad = (float)Math.Acos(nz);
                    snapshot.SlopeDegrees = slopeRad * (180.0f / (float)Math.PI);

                    if (snapshot.SlopeDegrees < 4.0f) snapshot.TerrainCategory = "Flat Urban Ground";
                    else if (snapshot.SlopeDegrees < 12.0f) snapshot.TerrainCategory = "Gentle Grade";
                    else if (snapshot.SlopeDegrees < 25.0f) snapshot.TerrainCategory = "Moderate Incline";
                    else snapshot.TerrainCategory = "Steep Slope / Embankment";

                    snapshot.WalkableAreaPercent = snapshot.SlopeDegrees < 20.0f ? 90.0f : 45.0f;
                }
                else
                {
                    float fallbackZ;
                    if (SpatialVisionEngine.ProbeGroundElevation(origin, out fallbackZ))
                    {
                        snapshot.Elevation = fallbackZ;
                    }
                    snapshot.SlopeDegrees = 1.5f;
                    snapshot.TerrainCategory = "Flat Urban Ground";
                    snapshot.WalkableAreaPercent = 90.0f;
                }
            }
            catch
            {
                // Headless/Test Fallback
                snapshot.Elevation = origin.Z;
                snapshot.SlopeDegrees = 1.5f;
                snapshot.TerrainCategory = "Flat Urban Ground";
                snapshot.WalkableAreaPercent = 90.0f;
            }
        }

        private void ProbeRoadAndAddress(Vector3 origin, WorldPerceptionSnapshot snapshot)
        {
            if (!IsMainThread) return;
            try
            {
                Vector3 roadPos = World.GetNextPositionOnStreet(origin);
                if (roadPos != Vector3.Zero)
                {
                    snapshot.RoadDistance = origin.DistanceTo(roadPos);
                }
            }
            catch
            {
                snapshot.RoadDistance = 18.0f;
            }

            try
            {
                Vector3 sidewalkPos = World.GetNextPositionOnSidewalk(origin);
                if (sidewalkPos != Vector3.Zero)
                {
                    snapshot.SidewalkDistance = origin.DistanceTo(sidewalkPos);
                }
            }
            catch
            {
                snapshot.SidewalkDistance = 6.0f;
            }

            try
            {
                string st = World.GetStreetName(origin);
                if (!string.IsNullOrEmpty(st)) snapshot.StreetName = st;
            }
            catch
            {
                snapshot.StreetName = "Metropolitan Street";
            }

            try
            {
                string zn = World.GetZoneLocalizedName(origin);
                if (string.IsNullOrEmpty(zn)) zn = World.GetZoneDisplayName(origin);
                if (!string.IsNullOrEmpty(zn)) snapshot.ZoneName = zn;
            }
            catch
            {
                snapshot.ZoneName = "Los Santos";
            }
        }

        private void ProbeCompassBoundaries(Vector3 origin, float radius, WorldPerceptionSnapshot snapshot)
        {
            if (!IsMainThread) return;
            float[] clearances = new float[8];
            for (int i = 0; i < 8; i++) clearances[i] = radius;

            Vector3 eyePos = new Vector3(origin.X, origin.Y, snapshot.Elevation + 1.0f);

            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = CompassDirections[i];
                Vector3 rayEnd = eyePos + (dir * radius);

                try
                {
                    RaycastResult hit = World.Raycast(eyePos, rayEnd, IntersectFlags.Map | IntersectFlags.Objects);
                    if (hit.DidHit)
                    {
                        clearances[i] = Math.Max(1.0f, eyePos.DistanceTo(hit.HitPosition));
                    }
                }
                catch
                {
                    // Headless default
                    clearances[i] = radius;
                }
            }

            snapshot.NorthClearance = clearances[0];
            snapshot.EastClearance = clearances[2];
            snapshot.SouthClearance = clearances[4];
            snapshot.WestClearance = clearances[6];

            snapshot.AvailableLength = Math.Min(radius * 2, snapshot.NorthClearance + snapshot.SouthClearance);
            snapshot.AvailableWidth = Math.Min(radius * 2, snapshot.EastClearance + snapshot.WestClearance);

            // Record significant boundaries
            if (snapshot.NorthClearance < radius * 0.8f)
                snapshot.BoundaryStructures.Add(string.Format("Building/Wall {0:F0}m North", snapshot.NorthClearance));
            if (snapshot.EastClearance < radius * 0.8f)
                snapshot.BoundaryStructures.Add(string.Format("Structure {0:F0}m East", snapshot.EastClearance));
            if (snapshot.SouthClearance < radius * 0.8f)
                snapshot.BoundaryStructures.Add(string.Format("Structure {0:F0}m South", snapshot.SouthClearance));
            if (snapshot.WestClearance < radius * 0.8f)
                snapshot.BoundaryStructures.Add(string.Format("Wall/Fence {0:F0}m West", snapshot.WestClearance));

            // Alley & Corridor Detection (narrow on one axis, elongated on the other)
            float ewSpan = snapshot.AvailableWidth;
            float nsSpan = snapshot.AvailableLength;

            if (ewSpan <= 9.0f && nsSpan >= 18.0f)
            {
                snapshot.IsAlleyOrCorridor = true;
                snapshot.CorridorWidth = ewSpan;
            }
            else if (nsSpan <= 9.0f && ewSpan >= 18.0f)
            {
                snapshot.IsAlleyOrCorridor = true;
                snapshot.CorridorWidth = nsSpan;
            }

            // Obstruction Density
            float avgClearance = (snapshot.NorthClearance + snapshot.EastClearance + snapshot.SouthClearance + snapshot.WestClearance) / 4.0f;
            if (avgClearance <= 8.0f) snapshot.ObstructionDensity = "Dense";
            else if (avgClearance <= 18.0f) snapshot.ObstructionDensity = "Medium";
            else if (avgClearance <= 35.0f) snapshot.ObstructionDensity = "Low";
            else snapshot.ObstructionDensity = "Clear";
        }

        private void ProbeOverhead(Vector3 origin, float radius, WorldPerceptionSnapshot snapshot)
        {
            if (!IsMainThread) return;
            try
            {
                var clearance = SpatialVisionEngine.SenseRoomClearance(origin, Math.Min(radius, 25.0f));
                snapshot.HasCeiling = clearance.HasCeiling;
                snapshot.CeilingHeight = clearance.CeilingClearance;
            }
            catch
            {
                snapshot.HasCeiling = false;
                snapshot.CeilingHeight = 99.0f;
            }
        }

        private void ProbeLivingEntities(Vector3 origin, float radius, WorldPerceptionSnapshot snapshot)
        {
            if (!IsMainThread) return;
            try
            {
                Ped[] nearbyPeds = World.GetNearbyPeds(origin, radius);
                if (nearbyPeds != null)
                {
                    snapshot.NearbyPedCount = nearbyPeds.Length;
                    int cops = 0;
                    foreach (var ped in nearbyPeds)
                    {
                        if (ped != null && ped.Exists())
                        {
                            try
                            {
                                bool isCop = ped.PedType == PedType.Cop ||
                                             ped.PedType == PedType.Swat ||
                                             ped.PedType == PedType.Army ||
                                             ped.RelationshipGroup == Game.GenerateHash("COP") ||
                                             ped.IsInPoliceVehicle;
                                if (isCop) cops++;
                            }
                            catch {}
                        }
                    }

                    if (cops > 0)
                    {
                        snapshot.PoliceActivityLevel = cops >= 3 ? "Active Alert" : "Elevated";
                        snapshot.EntityHighlights.Add(string.Format("{0} Police Officers in immediate vicinity", cops));
                    }
                }
            }
            catch
            {
                snapshot.NearbyPedCount = 0;
            }

            try
            {
                Vehicle[] nearbyVehicles = World.GetNearbyVehicles(origin, radius);
                if (nearbyVehicles != null)
                {
                    snapshot.NearbyVehicleCount = nearbyVehicles.Length;
                }
            }
            catch
            {
                snapshot.NearbyVehicleCount = 0;
            }
        }

        private void ProbeMemoryAndThreats(
            Vector3 origin,
            float radius,
            WorldMemoryStore store,
            EphemeralStateRecomputer recomputer,
            WorldPerceptionSnapshot snapshot)
        {
            if (store != null)
            {
                // 1. Nearby registered environments
                var envs = store.GetAllEnvironments();
                var factionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var env in envs)
                {
                    float dist = new Vector2(origin.X - env.CenterX, origin.Y - env.CenterY).Length();
                    if (dist <= 130.0f && dist > 2.0f)
                    {
                        snapshot.NearbyEnvironments.Add(string.Format("{0} ({1:F0}m)", env.Name ?? env.EnvironmentId, dist));
                    }

                    // Associate environment NPCs if within radius
                    if (dist <= radius * 1.5f && env.NpcIds != null)
                    {
                        foreach (int nid in env.NpcIds)
                        {
                            var n = store.GetNpc(nid);
                            if (n != null && !string.IsNullOrEmpty(n.PublicFaction))
                            {
                                string f = n.PublicFaction;
                                if (!f.Equals("Bystander", StringComparison.OrdinalIgnoreCase) &&
                                    !f.Equals("Civilians", StringComparison.OrdinalIgnoreCase))
                                {
                                    factionCounts[f] = factionCounts.ContainsKey(f) ? factionCounts[f] + 1 : 1;
                                }
                            }
                        }
                    }
                }

                // 2. Nearby faction inference from NPC event history positions
                var npcs = store.GetAllNpcs();
                foreach (var npc in npcs)
                {
                    if (string.IsNullOrEmpty(npc.PublicFaction) ||
                        npc.PublicFaction.Equals("Bystander", StringComparison.OrdinalIgnoreCase) ||
                        npc.PublicFaction.Equals("Civilians", StringComparison.OrdinalIgnoreCase) ||
                        npc.PublicFaction.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
                        npc.PublicFaction.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool isNearby = false;
                    if (npc.History != null)
                    {
                        foreach (var evt in npc.History)
                        {
                            if (evt.HasPosition)
                            {
                                float d = new Vector2(origin.X - evt.PositionX, origin.Y - evt.PositionY).Length();
                                if (d <= radius * 1.5f) { isNearby = true; break; }
                            }
                        }
                    }

                    if (isNearby)
                    {
                        string f = npc.PublicFaction;
                        factionCounts[f] = factionCounts.ContainsKey(f) ? factionCounts[f] + 1 : 1;
                    }
                }

                string topFaction = null;
                int maxF = 0;
                foreach (var kvp in factionCounts)
                {
                    if (kvp.Value > maxF)
                    {
                        maxF = kvp.Value;
                        topFaction = kvp.Key;
                    }
                }

                if (topFaction != null)
                {
                    snapshot.DominantFaction = topFaction;
                }
            }

            if (recomputer != null)
            {
                try
                {
                    var events = store != null ? store.GetAllEvents() : new List<EventRecord>();
                    var heat = recomputer.RecomputeLocationHeat(origin.X, origin.Y, origin.Z, events);
                    if (heat.ActivePoliceAlert)
                    {
                        snapshot.PoliceActivityLevel = "Active Alert";
                        snapshot.EntityHighlights.Add(string.Format("Police Tactical Alert ({0})", heat.Summary));
                    }
                    else if (heat.DangerScore >= 0.60f && snapshot.PoliceActivityLevel == "None")
                    {
                        snapshot.PoliceActivityLevel = "Elevated";
                    }
                }
                catch {}
            }
        }

        private void EvaluatePlacementFeasibility(WorldPerceptionSnapshot snapshot)
        {
            if (snapshot.SlopeDegrees > 25.0f)
            {
                snapshot.PlacementFeasibility = "Restricted (Steep Terrain)";
            }
            else if (snapshot.IsAlleyOrCorridor && snapshot.CorridorWidth < 3.5f)
            {
                snapshot.PlacementFeasibility = "Constrained (Narrow Alley)";
            }
            else if (snapshot.AvailableWidth < 7.0f || snapshot.AvailableLength < 7.0f)
            {
                snapshot.PlacementFeasibility = "Constrained (Tight Footprint)";
            }
            else if (snapshot.WalkableAreaPercent < 50.0f)
            {
                snapshot.PlacementFeasibility = "Restricted (Uneven Ground)";
            }
            else
            {
                snapshot.PlacementFeasibility = "Optimal";
            }
        }

        /// <summary>
        /// Evaluates and ranks multiple candidate locations for a specific venue archetype
        /// (e.g. "warehouse", "gang_hideout", "street_vendor", "drag_strip").
        /// </summary>
        public List<CandidatePerceptionScore> RankCandidates(
            List<Vector3> candidatePositions,
            string requiredArchetype,
            WorldMemoryStore store = null,
            EphemeralStateRecomputer recomputer = null)
        {
            var results = new List<CandidatePerceptionScore>();
            string arch = (requiredArchetype ?? "").ToLowerInvariant();

            int idx = 1;
            foreach (var pos in candidatePositions)
            {
                var snapshot = ScanLocation(pos, 50.0f, store, recomputer);
                var score = new CandidatePerceptionScore
                {
                    Position = pos,
                    CandidateLabel = string.Format("Site {0} ({1})", idx++, snapshot.ZoneName),
                    Snapshot = snapshot
                };

                float fitness = 0.5f;
                var reasons = new List<string>();

                // A. Large compounds / drag strips / hangars
                if (arch.Contains("hangar") || arch.Contains("warehouse") || arch.Contains("compound") || arch.Contains("drag_strip") || arch.Contains("staging"))
                {
                    if (snapshot.AvailableWidth >= 30.0f && snapshot.AvailableLength >= 30.0f)
                    {
                        fitness += 0.35f;
                        reasons.Add("Ample ground span");
                    }
                    else
                    {
                        fitness -= 0.30f;
                        reasons.Add("Footprint too constrained");
                    }

                    if (snapshot.SlopeDegrees < 5.0f)
                    {
                        fitness += 0.15f;
                        reasons.Add("Flat terrain");
                    }
                    else
                    {
                        fitness -= 0.20f;
                        reasons.Add("Uneven slope");
                    }
                }
                // B. Secluded illicit labs / gang hideouts / interrogations
                else if (arch.Contains("hideout") || arch.Contains("lab") || arch.Contains("chop_shop") || arch.Contains("interrogat") || arch.Contains("gang"))
                {
                    if (snapshot.IsAlleyOrCorridor || snapshot.ObstructionDensity == "Medium" || snapshot.ObstructionDensity == "Dense")
                    {
                        fitness += 0.30f;
                        reasons.Add("Secluded enclosure/cover");
                    }
                    if (!string.IsNullOrEmpty(snapshot.DominantFaction) && snapshot.DominantFaction != "None")
                    {
                        fitness += 0.20f;
                        reasons.Add("Established faction presence");
                    }
                    if (snapshot.PoliceActivityLevel == "None")
                    {
                        fitness += 0.10f;
                        reasons.Add("Low law enforcement visibility");
                    }
                    else
                    {
                        fitness -= 0.25f;
                        reasons.Add("Active police scrutiny");
                    }
                }
                // C. Street vendors / market stalls / bbq parties
                else if (arch.Contains("vendor") || arch.Contains("bbq") || arch.Contains("market") || arch.Contains("party"))
                {
                    if (snapshot.SidewalkDistance <= 10.0f)
                    {
                        fitness += 0.25f;
                        reasons.Add("Direct pedestrian access");
                    }
                    if (snapshot.WalkableAreaPercent >= 75.0f && snapshot.SlopeDegrees < 6.0f)
                    {
                        fitness += 0.20f;
                        reasons.Add("Walkable paved area");
                    }
                }
                else
                {
                    if (snapshot.PlacementFeasibility == "Optimal") fitness += 0.30f;
                }

                score.FitnessScore = Math.Max(0.05f, Math.Min(0.99f, fitness));
                score.Reason = reasons.Count > 0 ? string.Join(", ", reasons) : "Standard feasibility";
                results.Add(score);
            }

            // Sort descending by fitness score
            results.Sort((a, b) => b.FitnessScore.CompareTo(a.FitnessScore));
            return results;
        }

        /// <summary>
        /// Utility to construct a synthetic perception snapshot for offline test scenarios.
        /// </summary>
        public static WorldPerceptionSnapshot CreateSyntheticSnapshot(
            float x, float y, float z,
            string zone, string street,
            float slopeDegrees, float availableWidth, float availableLength,
            bool isAlley, float corridorWidth,
            int nearbyPeds, int nearbyVehicles,
            string dominantFaction, string policeAlert = "None")
        {
            var s = new WorldPerceptionSnapshot
            {
                X = x, Y = y, Z = z,
                Elevation = z,
                ZoneName = zone,
                StreetName = street,
                SlopeDegrees = slopeDegrees,
                TerrainCategory = slopeDegrees < 4.0f ? "Flat Urban Ground" : (slopeDegrees < 12.0f ? "Gentle Grade" : "Moderate Incline"),
                AvailableWidth = availableWidth,
                AvailableLength = availableLength,
                IsAlleyOrCorridor = isAlley,
                CorridorWidth = corridorWidth,
                NearbyPedCount = nearbyPeds,
                NearbyVehicleCount = nearbyVehicles,
                DominantFaction = dominantFaction,
                PoliceActivityLevel = policeAlert,
                WalkableAreaPercent = slopeDegrees < 20.0f ? 92.0f : 40.0f,
                RoadDistance = isAlley ? 22.0f : 12.0f,
                SidewalkDistance = isAlley ? 15.0f : 4.0f,
                ObstructionDensity = isAlley ? "Medium" : (availableWidth > 30f ? "Low" : "Medium"),
                PlacementFeasibility = isAlley && corridorWidth < 3.5f ? "Constrained (Narrow Alley)" : (slopeDegrees > 25f ? "Restricted (Steep Terrain)" : "Optimal")
            };

            if (isAlley)
            {
                s.BoundaryStructures.Add(string.Format("Building Wall {0:F0}m North", availableLength * 0.5f));
                s.BoundaryStructures.Add(string.Format("Building Wall {0:F0}m South", availableLength * 0.5f));
            }

            return s;
        }
    }
}
