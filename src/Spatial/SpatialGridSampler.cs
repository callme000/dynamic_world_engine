using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Utils;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Multi-Level Spatial Grid Sampler & Region Clustering Engine.
    /// Performs Level 2 physical grid sampling and Level 3 geometry/region extraction
    /// to synthesize machine-readable SpatialWorldModels and CandidateArea[] regions.
    /// </summary>
    public class SpatialGridSampler
    {
        public const float DefaultCellSize = 5.0f;

        public static bool IsMainThread
        {
            get { return DynamicWorldEngine.Runtime.GameThreadGate.IsMainThread; }
        }

        /// <summary>
        /// Probes physical or synthetic terrain in an N x M grid across a radius,
        /// populates surfaces, obstacles, paths, and clusters usable candidate areas.
        /// </summary>
        public SpatialWorldModel SampleArea(
            Vector3 origin,
            float radius = 50.0f,
            float cellSize = DefaultCellSize,
            WorldMemoryStore store = null,
            string zoneContext = null)
        {
            DateTime captureStarted = DateTime.UtcNow;
            var model = new SpatialWorldModel
            {
                Origin = origin,
                Radius = radius,
                BoundsMin = new Vector3(origin.X - radius, origin.Y - radius, origin.Z - 10f),
                BoundsMax = new Vector3(origin.X + radius, origin.Y + radius, origin.Z + 30f),
                Timestamp = DateTime.UtcNow
            };

            bool isHeadless = !IsMainThread;
            if (!isHeadless)
            {
                try
                {
                    // Test if native World API is available on main game thread
                    float testZ = World.GetGroundHeight(origin);
                }
                catch
                {
                    isHeadless = true;
                }
            }

            if (!isHeadless)
            {
                SampleNativeWorld(origin, radius, cellSize, store, model);
            }
            else
            {
                SampleHeadlessSyntheticWorld(origin, radius, cellSize, store, zoneContext, model);
            }

            // Timestamp the completed observation, not merely model allocation, so
            // freshness checks reflect when native sampling actually finished.
            model.Provenance.CaptureTime = DateTime.UtcNow;
            model.Provenance.CaptureDuration = model.Provenance.CaptureTime - captureStarted;

            int totalRequestedCells = CountRequestedGridCells(origin, radius, cellSize);
            int classifiedCells = 0;
            int unknownCells = 0;
            foreach (var surface in model.Surfaces)
            {
                if (surface != null && surface.SlopeDegrees >= 0.0f)
                    classifiedCells++;
                else
                    unknownCells++;
            }

            model.Provenance.ScanType = isHeadless ? "SYNTHETIC_FALLBACK" : "NATIVE_RAYCAST";
            model.Provenance.IsNativeScan = !isHeadless;
            model.Provenance.DataSource = isHeadless ? "Synthetic_Terrain_Generator" : "GTA_V_Native_World";
            model.Provenance.SampledCellsCount = model.Surfaces.Count;
            model.Provenance.ClassifiedCellsCount = classifiedCells;
            model.Provenance.UnknownCellsCount = unknownCells;
            model.Provenance.TotalRequestedCells = totalRequestedCells;
            // For native scans, completeness reflects classified ground samples,
            // not merely allocated surface records. Synthetic completeness describes
            // generated grid coverage only; provenance still labels it as synthetic.
            model.Provenance.CompletenessScore = totalRequestedCells > 0
                ? Math.Min(1.0f, (float)classifiedCells / totalRequestedCells)
                : 0.0f;

            // Level 3: Cluster contiguous cells into Candidate Areas
            ClusterCandidateAreas(model, 30.0f);

            // Level 4: Build Topological Spatial Graph
            SynthesizeSpatialGraph(model);

            return model;
        }

        private int CountRequestedGridCells(Vector3 origin, float radius, float cellSize)
        {
            int steps = (int)Math.Ceiling((radius * 2.0f) / cellSize);
            int count = 0;
            float startX = origin.X - radius;
            float startY = origin.Y - radius;

            for (int ix = 0; ix < steps; ix++)
            {
                for (int iy = 0; iy < steps; iy++)
                {
                    float cx = startX + (ix * cellSize) + (cellSize * 0.5f);
                    float cy = startY + (iy * cellSize) + (cellSize * 0.5f);
                    if (new Vector2(cx - origin.X, cy - origin.Y).Length() <= radius)
                        count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Recovers a failed ground-normal query from a downward map-collision raycast.
        /// Object collisions are deliberately excluded so props and vehicles cannot be
        /// mistaken for terrain. Only an upward-facing, finite surface normal is accepted.
        /// </summary>
        private static bool TryRecoverGroundSample(Vector3 probePos, out float groundHeight, out Vector3 normal)
        {
            groundHeight = probePos.Z;
            normal = Vector3.Zero;

            try
            {
                // Match the existing spatial ground-probe envelope. Keep this fallback
                // narrow and deterministic; a miss remains unknown rather than guessed.
                Vector3 rayStart = new Vector3(probePos.X, probePos.Y, probePos.Z + 3.0f);
                Vector3 rayEnd = new Vector3(probePos.X, probePos.Y, probePos.Z - 12.0f);
                RaycastResult hit = World.Raycast(rayStart, rayEnd, IntersectFlags.Map);

                if (!hit.DidHit) return false;

                Vector3 hitPosition = hit.HitPosition;
                Vector3 hitNormal = hit.SurfaceNormal;

                if (float.IsNaN(hitPosition.X) || float.IsInfinity(hitPosition.X) ||
                    float.IsNaN(hitPosition.Y) || float.IsInfinity(hitPosition.Y) ||
                    float.IsNaN(hitPosition.Z) || float.IsInfinity(hitPosition.Z) ||
                    hitPosition.Z > rayStart.Z || hitPosition.Z < rayEnd.Z)
                {
                    return false;
                }

                if (float.IsNaN(hitNormal.X) || float.IsInfinity(hitNormal.X) ||
                    float.IsNaN(hitNormal.Y) || float.IsInfinity(hitNormal.Y) ||
                    float.IsNaN(hitNormal.Z) || float.IsInfinity(hitNormal.Z))
                {
                    return false;
                }

                float normalLength = (float)Math.Sqrt(
                    hitNormal.X * hitNormal.X +
                    hitNormal.Y * hitNormal.Y +
                    hitNormal.Z * hitNormal.Z);

                // A floor sample must have a usable, upward-facing normal. Vertical
                // walls and underside/ceiling hits are not floor evidence.
                if (normalLength < 0.5f || hitNormal.Z <= 0.0f) return false;

                normal = new Vector3(
                    hitNormal.X / normalLength,
                    hitNormal.Y / normalLength,
                    hitNormal.Z / normalLength);
                groundHeight = hitPosition.Z;
                return true;
            }
            catch
            {
                // Native/raycast failures are represented as unknown evidence.
                return false;
            }
        }

        private void SampleNativeWorld(
            Vector3 origin,
            float radius,
            float cellSize,
            WorldMemoryStore store,
            SpatialWorldModel model)
        {
            int steps = (int)Math.Ceiling((radius * 2.0f) / cellSize);
            float startX = origin.X - radius;
            float startY = origin.Y - radius;
            int normalQueryMisses = 0;
            int raycastRecoveries = 0;
            int raycastMisses = 0;
            var unresolvedSamples = new List<string>();

            for (int ix = 0; ix < steps; ix++)
            {
                for (int iy = 0; iy < steps; iy++)
                {
                    float cx = startX + (ix * cellSize) + (cellSize * 0.5f);
                    float cy = startY + (iy * cellSize) + (cellSize * 0.5f);
                    float distToCenter = new Vector2(cx - origin.X, cy - origin.Y).Length();
                    if (distToCenter > radius) continue;

                    Vector3 probePos = new Vector3(cx, cy, origin.Z);
                    float groundHeight;
                    Vector3 normal;
                    bool gotNormal = World.GetGroundHeightAndNormal(probePos, out groundHeight, out normal);
                    if (!gotNormal)
                    {
                        normalQueryMisses++;
                        if (TryRecoverGroundSample(probePos, out groundHeight, out normal))
                        {
                            gotNormal = true;
                            raycastRecoveries++;
                        }
                        else
                        {
                            raycastMisses++;
                            if (unresolvedSamples.Count < 8)
                            {
                                unresolvedSamples.Add(string.Format(
                                    "({0:F1}, {1:F1}, {2:F1})", cx, cy, origin.Z));
                            }

                            // Preserve the elevation-only fallback for diagnostics and
                            // water comparison, but it never promotes a cell to classified.
                            groundHeight = World.GetGroundHeight(probePos);
                            normal = Vector3.Zero;
                        }
                    }

                    // A fallback elevation without a valid surface normal is not enough
                    // evidence to call the cell flat or walkable.
                    float slopeDeg = -1.0f;
                    if (gotNormal)
                    {
                        float nz = Math.Max(-1.0f, Math.Min(1.0f, normal.Z));
                        slopeDeg = (float)Math.Acos(nz) * (180.0f / (float)Math.PI);
                    }

                    // Check water elevation
                    bool isWater = false;
                    try
                    {
                        OutputArgument outH = new OutputArgument();
                        if (Function.Call<bool>(Hash.GET_WATER_HEIGHT, cx, cy, origin.Z + 1.5f, outH))
                        {
                            float waterZ = outH.GetResult<float>();
                            if (waterZ >= groundHeight - 0.5f) isWater = true;
                        }
                    }
                    catch {}

                    // Surface type inference. Unknown geometry is deliberately
                    // excluded from candidate-area clustering.
                    string sType = gotNormal ? "CONCRETE" : "UNKNOWN";
                    if (isWater) sType = "WATER";
                    else if (gotNormal && groundHeight <= 4.0f && Math.Abs(cx) > 1000f) sType = "SAND";

                    bool walkable = gotNormal && !isWater && slopeDeg < 30.0f;

                    var surface = new SpatialSurface
                    {
                        SurfaceId = string.Format("surf_{0}_{1}", ix, iy),
                        Position = new Vector3(cx, cy, isWater ? 0f : groundHeight),
                        Normal = normal,
                        SlopeDegrees = slopeDeg,
                        SurfaceType = sType,
                        IsWalkable = walkable,
                        SupportsObjects = walkable && slopeDeg < 15.0f,
                        Clearance = isWater ? 0f : 10f,
                        Dimensions = new Vector2(cellSize, cellSize)
                    };
                    model.Surfaces.Add(surface);
                }
            }

            if (normalQueryMisses > 0)
            {
                Log.Info("SpatialGridSampler",
                    "Ground-normal recovery: nativeMisses={0}, raycastRecovered={1}, raycastUnresolved={2}, unresolvedSampleCoordinates={3}",
                    normalQueryMisses,
                    raycastRecoveries,
                    raycastMisses,
                    unresolvedSamples.Count > 0 ? string.Join("; ", unresolvedSamples.ToArray()) : "none");
            }

            // Living entities probe
            try
            {
                Ped[] peds = World.GetNearbyPeds(origin, radius);
                if (peds != null)
                {
                    int pid = 1;
                    foreach (var ped in peds)
                    {
                        if (ped != null && ped.Exists())
                        {
                            model.ExistingEntities.Add(new SpatialEntityRecord
                            {
                                EntityId = "ped_" + (pid++),
                                EntityType = "PED",
                                Position = ped.Position,
                                Heading = ped.Heading,
                                Role = "Pedestrian"
                            });
                        }
                    }
                }

                Vehicle[] vehs = World.GetNearbyVehicles(origin, radius);
                if (vehs != null)
                {
                    int vid = 1;
                    foreach (var v in vehs)
                    {
                        if (v != null && v.Exists())
                        {
                            model.ExistingEntities.Add(new SpatialEntityRecord
                            {
                                EntityId = "veh_" + (vid++),
                                EntityType = "VEHICLE",
                                Position = v.Position,
                                Heading = v.Heading,
                                Role = "Vehicle"
                            });
                        }
                    }
                }
            }
            catch {}
        }

        private void SampleHeadlessSyntheticWorld(
            Vector3 origin,
            float radius,
            float cellSize,
            WorldMemoryStore store,
            string zoneContext,
            SpatialWorldModel model)
        {
            string zone = (zoneContext ?? "Del Perro Beach").ToLowerInvariant();
            int steps = (int)Math.Ceiling((radius * 2.0f) / cellSize);
            float startX = origin.X - radius;
            float startY = origin.Y - radius;

            bool isBeachContext = zone.Contains("beach") || zone.Contains("del perro") || zone.Contains("vespucci") || Math.Abs(origin.Z) < 5.0f;

            for (int ix = 0; ix < steps; ix++)
            {
                for (int iy = 0; iy < steps; iy++)
                {
                    float cx = startX + (ix * cellSize) + (cellSize * 0.5f);
                    float cy = startY + (iy * cellSize) + (cellSize * 0.5f);
                    float distToCenter = new Vector2(cx - origin.X, cy - origin.Y).Length();
                    if (distToCenter > radius) continue;

                    string sType = "CONCRETE";
                    float slopeDeg = 1.0f;
                    float groundZ = origin.Z;
                    bool walkable = true;

                    if (isBeachContext)
                    {
                        // Model Del Perro Beach layout:
                        // Ocean to the West/South (e.g. cx < origin.X - 22m)
                        // Sandy beach in the middle (cx between origin.X - 22m and origin.X + 12m)
                        // Concrete boardwalk/walkway (cx between origin.X + 12m and origin.X + 22m)
                        // Roadway to the East (cx > origin.X + 22m)
                        float relX = cx - origin.X;
                        if (relX < -22.0f)
                        {
                            sType = "WATER";
                            walkable = false;
                            groundZ = 0.0f;
                        }
                        else if (relX <= 12.0f)
                        {
                            sType = "SAND";
                            slopeDeg = 1.2f;
                            groundZ = 2.3f;
                        }
                        else if (relX <= 22.0f)
                        {
                            sType = "CONCRETE"; // Walkway
                            slopeDeg = 0.5f;
                            groundZ = 3.0f;
                        }
                        else
                        {
                            sType = "ASPHALT"; // Road
                            slopeDeg = 0.2f;
                            groundZ = 3.1f;
                        }
                    }
                    else
                    {
                        // Standard urban layout
                        float relY = cy - origin.Y;
                        if (Math.Abs(relY) < 4.0f) sType = "ASPHALT";
                        else if (Math.Abs(relY) < 8.0f) sType = "CONCRETE";
                        else sType = "CONCRETE";
                    }

                    model.Surfaces.Add(new SpatialSurface
                    {
                        SurfaceId = string.Format("surf_{0}_{1}", ix, iy),
                        Position = new Vector3(cx, cy, groundZ),
                        Normal = new Vector3(0f, 0f, 1f),
                        SlopeDegrees = slopeDeg,
                        SurfaceType = sType,
                        IsWalkable = walkable,
                        SupportsObjects = walkable && sType != "WATER",
                        Clearance = walkable ? 12f : 0f,
                        Dimensions = new Vector2(cellSize, cellSize)
                    });
                }
            }

            if (isBeachContext)
            {
                // Add Del Perro Beach landmarks: Palms, Walkway, Road, Ocean
                model.Obstacles.Add(new SpatialObstacle
                {
                    Id = "palm_tree_01",
                    Position = new Vector3(origin.X - 8.0f, origin.Y + 12.0f, 2.3f),
                    Dimensions = new Vector3(1.2f, 1.2f, 8.0f),
                    ObstacleType = "TREE",
                    BlocksNavMesh = true,
                    BlocksLineOfSight = false
                });

                model.Obstacles.Add(new SpatialObstacle
                {
                    Id = "palm_tree_02",
                    Position = new Vector3(origin.X + 6.0f, origin.Y + 12.0f, 2.3f),
                    Dimensions = new Vector3(1.2f, 1.2f, 8.0f),
                    ObstacleType = "TREE",
                    BlocksNavMesh = true,
                    BlocksLineOfSight = false
                });

                model.Paths.Add(new SpatialPath
                {
                    PathId = "boardwalk_walkway",
                    PathType = "WALKWAY",
                    Width = 6.0f,
                    AllowsPedestrians = true,
                    AllowsVehicles = false,
                    Waypoints = new List<Vector3>
                    {
                        new Vector3(origin.X + 17.0f, origin.Y - 30.0f, 3.0f),
                        new Vector3(origin.X + 17.0f, origin.Y + 30.0f, 3.0f)
                    }
                });

                model.Paths.Add(new SpatialPath
                {
                    PathId = "del_perro_fwy",
                    PathType = "ROAD",
                    Width = 14.0f,
                    AllowsPedestrians = false,
                    AllowsVehicles = true,
                    Waypoints = new List<Vector3>
                    {
                        new Vector3(origin.X + 32.0f, origin.Y - 30.0f, 3.1f),
                        new Vector3(origin.X + 32.0f, origin.Y + 30.0f, 3.1f)
                    }
                });

                // Crowd entities along walkway
                model.ExistingEntities.Add(new SpatialEntityRecord
                {
                    EntityId = "ped_walkway_1",
                    EntityType = "PED",
                    Position = new Vector3(origin.X + 17.0f, origin.Y - 2.0f, 3.0f),
                    Heading = 0f,
                    Role = "Jogger"
                });
                model.ExistingEntities.Add(new SpatialEntityRecord
                {
                    EntityId = "ped_walkway_2",
                    EntityType = "PED",
                    Position = new Vector3(origin.X + 17.0f, origin.Y + 4.0f, 3.0f),
                    Heading = 180f,
                    Role = "Tourist"
                });
            }
        }

        /// <summary>
        /// Level 3: Groups contiguous walkable surface cells of matching surface types
        /// into bounded, usable CandidateArea[] regions.
        /// </summary>
        public void ClusterCandidateAreas(SpatialWorldModel model, float minAreaSpan = 30.0f)
        {
            if (model.Surfaces.Count == 0) return;

            // Separate walkable cells by surface type (e.g. SAND vs CONCRETE vs ASPHALT)
            var sandCells = new List<SpatialSurface>();
            var concreteCells = new List<SpatialSurface>();
            var asphaltCells = new List<SpatialSurface>();

            foreach (var s in model.Surfaces)
            {
                if (!s.IsWalkable) continue;
                if (s.SurfaceType == "SAND") sandCells.Add(s);
                else if (s.SurfaceType == "CONCRETE") concreteCells.Add(s);
                else if (s.SurfaceType == "ASPHALT") asphaltCells.Add(s);
            }

            int areaIdx = 1;

            // 1. Cluster Sandy Beachfront
            if (sandCells.Count >= 4)
            {
                CandidateArea areaA = CreateCandidateAreaFromCells(
                    sandCells,
                    "Area_A",
                    "Secluded Beachfront Sand",
                    model.Origin,
                    model.Obstacles,
                    model.ExistingEntities);

                areaA.OceanDistance = 12.0f;
                areaA.RoadDistance = 45.0f;
                areaA.SidewalkDistance = 20.0f;
                areaA.SeclusionScore = 0.82f;
                areaA.SuitabilityTags.Add("secluded_meeting");
                areaA.SuitabilityTags.Add("party_bonfire");
                areaA.SuitabilityTags.Add("covert_drop");
                areaA.RecommendedApproachVector = new Vector3(1f, 0f, 0f); // approach from walkway (East)
                model.CandidateAreas.Add(areaA);
            }

            // 2. Cluster Walkway / Pavement
            if (concreteCells.Count >= 4)
            {
                CandidateArea areaB = CreateCandidateAreaFromCells(
                    concreteCells,
                    "Area_B",
                    "Public Boardwalk / Walkway",
                    model.Origin,
                    model.Obstacles,
                    model.ExistingEntities);

                areaB.OceanDistance = 35.0f;
                areaB.RoadDistance = 15.0f;
                areaB.SidewalkDistance = 0.0f;
                areaB.CrowdDensity = model.ExistingEntities.Count > 0 ? "Medium" : "Low";
                areaB.SeclusionScore = 0.25f;
                areaB.SuitabilityTags.Add("vendor");
                areaB.SuitabilityTags.Add("public_gathering");
                areaB.SuitabilityTags.Add("checkpoint");
                areaB.RecommendedApproachVector = new Vector3(0f, 1f, 0f);
                model.CandidateAreas.Add(areaB);
            }

            // 3. Cluster Roadside Staging Expanse
            if (asphaltCells.Count >= 4)
            {
                CandidateArea areaC = CreateCandidateAreaFromCells(
                    asphaltCells,
                    "Area_C",
                    "Roadside Transit & Staging",
                    model.Origin,
                    model.Obstacles,
                    model.ExistingEntities);

                areaC.OceanDistance = 55.0f;
                areaC.RoadDistance = 0.0f;
                areaC.SidewalkDistance = 8.0f;
                areaC.VehicleAccess = true;
                areaC.PedestrianAccess = true;
                areaC.SeclusionScore = 0.15f;
                areaC.SuitabilityTags.Add("staging");
                areaC.SuitabilityTags.Add("motorcade");
                areaC.SuitabilityTags.Add("convoy");
                areaC.RecommendedApproachVector = new Vector3(0f, 1f, 0f);
                model.CandidateAreas.Add(areaC);
            }
        }

        private CandidateArea CreateCandidateAreaFromCells(
            List<SpatialSurface> cells,
            string id,
            string label,
            Vector3 origin,
            List<SpatialObstacle> obstacles,
            List<SpatialEntityRecord> entities)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float sumZ = 0f, sumSlope = 0f;

            foreach (var c in cells)
            {
                if (c.Position.X < minX) minX = c.Position.X;
                if (c.Position.X > maxX) maxX = c.Position.X;
                if (c.Position.Y < minY) minY = c.Position.Y;
                if (c.Position.Y > maxY) maxY = c.Position.Y;
                sumZ += c.Position.Z;
                sumSlope += c.SlopeDegrees;
            }

            float w = Math.Max(6.0f, maxX - minX);
            float l = Math.Max(6.0f, maxY - minY);
            Vector3 center = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, sumZ / cells.Count);

            // Check entities inside area
            int inAreaCount = 0;
            foreach (var e in entities)
            {
                if (Math.Abs(e.Position.X - center.X) <= w * 0.5f && Math.Abs(e.Position.Y - center.Y) <= l * 0.5f)
                {
                    inAreaCount++;
                }
            }

            return new CandidateArea
            {
                AreaId = id,
                Label = label,
                Center = center,
                Dimensions = new Vector2(w, l),
                SurfaceType = cells[0].SurfaceType,
                SlopeDegrees = sumSlope / cells.Count,
                WalkablePercent = 95.0f,
                VehicleAccess = cells[0].SurfaceType == "ASPHALT",
                PedestrianAccess = true,
                CrowdDensity = inAreaCount > 3 ? "High" : (inAreaCount > 0 ? "Medium" : "Low"),
                ObstructionLevel = "Clear",
                SeclusionScore = 0.5f,
                HasClearLineOfApproach = true
            };
        }

        /// <summary>
        /// Level 4: Builds the 3D Topological Spatial Graph connecting candidate areas,
        /// paths, structures, and obstacles with relational edges.
        /// </summary>
        public void SynthesizeSpatialGraph(SpatialWorldModel model)
        {
            var g = model.Graph;

            // 1. Add Candidate Area Nodes
            foreach (var area in model.CandidateAreas)
            {
                var node = new SpatialNode
                {
                    Id = area.AreaId,
                    Position = area.Center,
                    Bounds = new Vector3(area.Dimensions.X, area.Dimensions.Y, 3.0f),
                    SurfaceType = area.SurfaceType,
                    NodeType = "CANDIDATE_AREA",
                    Walkable = true,
                    Accessible = true,
                    Tags = new List<string>(area.SuitabilityTags)
                };
                g.AddNode(node);
            }

            // 2. Add Path Nodes
            foreach (var p in model.Paths)
            {
                Vector3 midPoint = p.Waypoints.Count > 1
                    ? (p.Waypoints[0] + p.Waypoints[p.Waypoints.Count - 1]) * 0.5f
                    : (p.Waypoints.Count > 0 ? p.Waypoints[0] : model.Origin);

                var node = new SpatialNode
                {
                    Id = p.PathId,
                    Position = midPoint,
                    Bounds = new Vector3(p.Width, 20f, 1f),
                    SurfaceType = p.PathType == "ROAD" ? "ASPHALT" : "CONCRETE",
                    NodeType = "PATH_NODE",
                    Walkable = true,
                    Accessible = true
                };
                g.AddNode(node);
            }

            // 3. Add Obstacle Nodes
            foreach (var obs in model.Obstacles)
            {
                var node = new SpatialNode
                {
                    Id = obs.Id,
                    Position = obs.Position,
                    Bounds = obs.Dimensions,
                    SurfaceType = "WOOD",
                    NodeType = "OBSTACLE",
                    Walkable = false,
                    Accessible = false
                };
                g.AddNode(node);
            }

            // 4. Synthesize Relationships / Edges
            // Candidate Areas <-> Paths
            foreach (var area in model.CandidateAreas)
            {
                foreach (var p in model.Paths)
                {
                    float dist = new Vector2(area.Center.X - p.Waypoints[0].X, area.Center.Y - p.Waypoints[0].Y).Length();
                    if (dist <= 35.0f)
                    {
                        g.AddEdge(area.AreaId, p.PathId, "APPROACHABLE_FROM", dist);
                        g.AddEdge(p.PathId, area.AreaId, "CONNECTED_TO", dist);
                    }
                }

                // Candidate Area <-> Obstacles
                foreach (var obs in model.Obstacles)
                {
                    float dist = area.Center.DistanceTo(obs.Position);
                    if (dist <= 25.0f)
                    {
                        g.AddEdge(area.AreaId, obs.Id, "NEAR", dist);
                    }
                }
            }

            // Cross-area adjacency
            for (int i = 0; i < model.CandidateAreas.Count; i++)
            {
                for (int j = i + 1; j < model.CandidateAreas.Count; j++)
                {
                    var a1 = model.CandidateAreas[i];
                    var a2 = model.CandidateAreas[j];
                    float dist = a1.Center.DistanceTo(a2.Center);
                    if (dist <= 40.0f)
                    {
                        g.AddEdge(a1.AreaId, a2.AreaId, "ADJACENT_TO", dist);
                        g.AddEdge(a2.AreaId, a1.AreaId, "ADJACENT_TO", dist);
                        g.AddEdge(a1.AreaId, a2.AreaId, "VISIBLE_FROM", dist);
                        g.AddEdge(a2.AreaId, a1.AreaId, "VISIBLE_FROM", dist);
                    }
                }
            }
        }
    }
}
