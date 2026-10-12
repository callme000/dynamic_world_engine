using System;
using System.Collections.Generic;
using GTA.Math;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Machine-Readable 3D Spatial World Model.
    /// Represents the deterministic geometric, topological, and environmental reality
    /// of a 50-100m zone in Los Santos.
    /// </summary>
    public class SpatialWorldModel
    {
        public Vector3 Origin { get; set; }
        public float Radius { get; set; }
        public Vector3 BoundsMin { get; set; }
        public Vector3 BoundsMax { get; set; }
        public DateTime Timestamp { get; set; }

        public List<SpatialSurface> Surfaces { get; set; }
        public List<SpatialObstacle> Obstacles { get; set; }
        public List<SpatialStructure> Structures { get; set; }
        public List<SpatialPath> Paths { get; set; }
        public List<SpatialEntityRecord> ExistingEntities { get; set; }
        public List<CandidateArea> CandidateAreas { get; set; }
        public SpatialGraph Graph { get; set; }

        [JsonIgnore]
        public WorldPerceptionSnapshot SemanticSnapshot { get; set; }

        public SpatialWorldModel()
        {
            Surfaces = new List<SpatialSurface>();
            Obstacles = new List<SpatialObstacle>();
            Structures = new List<SpatialStructure>();
            Paths = new List<SpatialPath>();
            ExistingEntities = new List<SpatialEntityRecord>();
            CandidateAreas = new List<CandidateArea>();
            Graph = new SpatialGraph();
            Timestamp = DateTime.UtcNow;
        }

        public CandidateArea FindCandidateArea(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return null;
            return CandidateAreas.Find(a => string.Equals(a.AreaId, areaId, StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(a.Label, areaId, StringComparison.OrdinalIgnoreCase));
        }

        public CandidateArea GetBestCandidate(string requiredArchetype)
        {
            if (CandidateAreas.Count == 0) return null;
            string arch = (requiredArchetype ?? "").ToLowerInvariant();

            CandidateArea best = CandidateAreas[0];
            float bestScore = -1f;

            foreach (var area in CandidateAreas)
            {
                float score = 0.5f;
                if (area.SuitabilityTags.Contains(arch)) score += 0.4f;

                if (arch.Contains("meeting") || arch.Contains("interrogat") || arch.Contains("deal"))
                {
                    score += area.SeclusionScore * 0.4f;
                    if (area.PedestrianAccess) score += 0.2f;
                    if (area.CrowdDensity == "Low" || area.CrowdDensity == "None") score += 0.2f;
                }
                else if (arch.Contains("party") || arch.Contains("bonfire") || arch.Contains("bbq"))
                {
                    if (area.SurfaceType.Equals("SAND", StringComparison.OrdinalIgnoreCase)) score += 0.3f;
                    if (area.Dimensions.X >= 15f && area.Dimensions.Y >= 12f) score += 0.3f;
                    if (area.SlopeDegrees < 5.0f) score += 0.2f;
                }
                else if (arch.Contains("staging") || arch.Contains("compound") || arch.Contains("motorcade"))
                {
                    if (area.VehicleAccess) score += 0.4f;
                    if (area.Dimensions.X >= 25f && area.Dimensions.Y >= 20f) score += 0.4f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = area;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Geometric surface patch in the spatial world model.
    /// </summary>
    public class SpatialSurface
    {
        public string SurfaceId { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public float SlopeDegrees { get; set; }
        public string SurfaceType { get; set; } // SAND, CONCRETE, ASPHALT, GRASS, DIRT, WOOD, METAL, WATER
        public bool IsWalkable { get; set; }
        public bool SupportsObjects { get; set; }
        public float Clearance { get; set; }
        public Vector2 Dimensions { get; set; }

        public SpatialSurface()
        {
            Normal = new Vector3(0f, 0f, 1f);
            SurfaceType = "CONCRETE";
            IsWalkable = true;
            SupportsObjects = true;
            Clearance = 10f;
            Dimensions = new Vector2(5f, 5f);
        }
    }

    /// <summary>
    /// Physical 3D obstruction in the spatial world model.
    /// </summary>
    public class SpatialObstacle
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Dimensions { get; set; } // Width, Length, Height
        public float Heading { get; set; }
        public string ObstacleType { get; set; } // WALL, BUILDING, FENCE, TREE, PROP, VEHICLE, WATER_BODY
        public bool BlocksNavMesh { get; set; }
        public bool BlocksLineOfSight { get; set; }

        public SpatialObstacle()
        {
            Dimensions = new Vector3(1f, 1f, 2f);
            ObstacleType = "WALL";
            BlocksNavMesh = true;
            BlocksLineOfSight = true;
        }
    }

    /// <summary>
    /// Architectural structure or building enclosure.
    /// </summary>
    public class SpatialStructure
    {
        public string Id { get; set; }
        public string StructureType { get; set; } // BUILDING, WALL, CANOPY, FENCE
        public Vector3 Center { get; set; }
        public Vector3 Dimensions { get; set; }
        public List<Vector3> PerimeterPoints { get; set; }

        public SpatialStructure()
        {
            StructureType = "BUILDING";
            Dimensions = new Vector3(10f, 10f, 6f);
            PerimeterPoints = new List<Vector3>();
        }
    }

    /// <summary>
    /// Walkway, road, sidewalk, or alley corridor.
    /// </summary>
    public class SpatialPath
    {
        public string PathId { get; set; }
        public string PathType { get; set; } // ROAD, SIDEWALK, WALKWAY, ALLEY
        public List<Vector3> Waypoints { get; set; }
        public float Width { get; set; }
        public bool AllowsVehicles { get; set; }
        public bool AllowsPedestrians { get; set; }

        public SpatialPath()
        {
            PathType = "WALKWAY";
            Waypoints = new List<Vector3>();
            Width = 3f;
            AllowsPedestrians = true;
            AllowsVehicles = false;
        }
    }

    /// <summary>
    /// Living or static entity observed during spatial perception scan.
    /// </summary>
    public class SpatialEntityRecord
    {
        public string EntityId { get; set; }
        public string EntityType { get; set; } // PED, VEHICLE, COP
        public Vector3 Position { get; set; }
        public float Heading { get; set; }
        public string Faction { get; set; }
        public string Role { get; set; }

        public SpatialEntityRecord()
        {
            EntityType = "PED";
            Faction = "Civilian";
            Role = "Bystander";
        }
    }

    /// <summary>
    /// Usable spatial region detected through grid sampling and geometry clustering.
    /// Enables the AI Director to select grounded spaces rather than hallucinating coordinates.
    /// </summary>
    public class CandidateArea
    {
        public string AreaId { get; set; }
        public string Label { get; set; }
        public Vector3 Center { get; set; }
        public Vector2 Dimensions { get; set; } // Width (X span), Length (Y span)
        public string SurfaceType { get; set; }
        public float SlopeDegrees { get; set; }
        public float WalkablePercent { get; set; }
        public bool VehicleAccess { get; set; }
        public bool PedestrianAccess { get; set; }
        public string CrowdDensity { get; set; } // None, Low, Medium, High
        public float OceanDistance { get; set; }
        public float RoadDistance { get; set; }
        public float SidewalkDistance { get; set; }
        public string ObstructionLevel { get; set; } // Clear, Low, Medium, Dense
        public float SeclusionScore { get; set; } // 0.0 to 1.0
        public List<string> SuitabilityTags { get; set; }
        public bool HasClearLineOfApproach { get; set; }
        public Vector3 RecommendedApproachVector { get; set; }

        public CandidateArea()
        {
            AreaId = "Area_A";
            Label = "Candidate Area";
            Dimensions = new Vector2(15f, 15f);
            SurfaceType = "CONCRETE";
            SlopeDegrees = 1.0f;
            WalkablePercent = 95f;
            PedestrianAccess = true;
            CrowdDensity = "Low";
            ObstructionLevel = "Clear";
            SeclusionScore = 0.5f;
            SuitabilityTags = new List<string>();
            HasClearLineOfApproach = true;
            RecommendedApproachVector = new Vector3(0f, 1f, 0f);
        }

        public bool ContainsPoint(Vector3 p)
        {
            float halfW = Dimensions.X * 0.5f;
            float halfL = Dimensions.Y * 0.5f;
            return p.X >= Center.X - halfW && p.X <= Center.X + halfW &&
                   p.Y >= Center.Y - halfL && p.Y <= Center.Y + halfL;
        }

        public string FormatBrief()
        {
            return string.Format("[{0}] {1}: {2:F0}m×{3:F0}m, {4}, Slope: {5:F1}°, Crowd: {6}, Seclusion: {7:F2}, Access: {8}{9}",
                AreaId, Label, Dimensions.X, Dimensions.Y, SurfaceType, SlopeDegrees,
                CrowdDensity, SeclusionScore,
                PedestrianAccess ? "Peds" : "None",
                VehicleAccess ? "+Vehicles" : "");
        }
    }

    /// <summary>
    /// Node in the 3D spatial graph.
    /// </summary>
    public class SpatialNode
    {
        public string Id { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Bounds { get; set; }
        public string SurfaceType { get; set; }
        public string NodeType { get; set; } // CANDIDATE_AREA, SURFACE, STRUCTURE, OBSTACLE, ENTRANCE, PATH_NODE, ENTITY
        public bool Walkable { get; set; }
        public bool Accessible { get; set; }
        public List<string> Tags { get; set; }

        public SpatialNode()
        {
            NodeType = "SURFACE";
            SurfaceType = "CONCRETE";
            Walkable = true;
            Accessible = true;
            Tags = new List<string>();
        }
    }

    /// <summary>
    /// Directed edge representing spatial/topological relationships between nodes.
    /// </summary>
    public class SpatialEdge
    {
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public string RelationshipType { get; set; } // CONNECTED_TO, ADJACENT_TO, INSIDE, NEAR, BLOCKED_BY, VISIBLE_FROM, APPROACHABLE_FROM
        public float Distance { get; set; }
        public float Weight { get; set; }

        public SpatialEdge()
        {
            RelationshipType = "CONNECTED_TO";
            Weight = 1.0f;
        }

        public SpatialEdge(string src, string tgt, string rel, float dist = 1.0f, float weight = 1.0f)
        {
            SourceNodeId = src;
            TargetNodeId = tgt;
            RelationshipType = rel;
            Distance = dist;
            Weight = weight;
        }
    }

    /// <summary>
    /// 3D Spatial Relationship Graph.
    /// Enables rich relational queries: connectivity, adjacency, visibility, and approachability.
    /// </summary>
    public class SpatialGraph
    {
        public Dictionary<string, SpatialNode> Nodes { get; set; }
        public List<SpatialEdge> Edges { get; set; }

        public SpatialGraph()
        {
            Nodes = new Dictionary<string, SpatialNode>(StringComparer.OrdinalIgnoreCase);
            Edges = new List<SpatialEdge>();
        }

        public void AddNode(SpatialNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id)) return;
            Nodes[node.Id] = node;
        }

        public void AddEdge(string src, string tgt, string relationship, float dist = 1f, float weight = 1f)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) return;
            Edges.Add(new SpatialEdge(src, tgt, relationship, dist, weight));
        }

        public List<SpatialNode> GetNeighbors(string nodeId, string relationship = null)
        {
            var result = new List<SpatialNode>();
            foreach (var edge in Edges)
            {
                if (string.Equals(edge.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    if (relationship == null || string.Equals(edge.RelationshipType, relationship, StringComparison.OrdinalIgnoreCase))
                    {
                        SpatialNode target;
                        if (Nodes.TryGetValue(edge.TargetNodeId, out target))
                        {
                            result.Add(target);
                        }
                    }
                }
            }
            return result;
        }

        public List<SpatialEdge> GetEdges(string nodeId)
        {
            var result = new List<SpatialEdge>();
            foreach (var edge in Edges)
            {
                if (string.Equals(edge.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(edge.TargetNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(edge);
                }
            }
            return result;
        }

        public List<SpatialNode> GetVisibleNodesFrom(string nodeId)
        {
            return GetNeighbors(nodeId, "VISIBLE_FROM");
        }

        public List<SpatialNode> GetNodesByType(string nodeType)
        {
            var result = new List<SpatialNode>();
            foreach (var node in Nodes.Values)
            {
                if (string.Equals(node.NodeType, nodeType, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(node);
                }
            }
            return result;
        }

        public bool AreConnected(string nodeA, string nodeB)
        {
            foreach (var edge in Edges)
            {
                if ((string.Equals(edge.SourceNodeId, nodeA, StringComparison.OrdinalIgnoreCase) && string.Equals(edge.TargetNodeId, nodeB, StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(edge.SourceNodeId, nodeB, StringComparison.OrdinalIgnoreCase) && string.Equals(edge.TargetNodeId, nodeA, StringComparison.OrdinalIgnoreCase)))
                {
                    if (edge.RelationshipType == "CONNECTED_TO" || edge.RelationshipType == "APPROACHABLE_FROM" || edge.RelationshipType == "ADJACENT_TO")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
