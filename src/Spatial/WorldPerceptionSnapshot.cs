using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Structured World Perception Snapshot (Structured Vision Layer).
    /// Represents the deterministic physical, spatial, and societal reality of a 50-100m zone
    /// around a candidate location, eliminating "spawning while blind".
    /// </summary>
    public class WorldPerceptionSnapshot
    {
        // 1. Observation Telemetry
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public string ZoneName { get; set; }
        public string StreetName { get; set; }
        public DateTime Timestamp { get; set; }

        // 2. Ground & Terrain Assessment
        public float Elevation { get; set; }
        public float SlopeDegrees { get; set; }
        public string TerrainCategory { get; set; }
        public float WalkableAreaPercent { get; set; }
        public float RoadDistance { get; set; }
        public float SidewalkDistance { get; set; }
        public bool HasWaterNearby { get; set; }
        public float WaterDistance { get; set; }

        // 3. Spatial Boundaries & Enclosure (8-Compass Raycast)
        public float NorthClearance { get; set; }
        public float SouthClearance { get; set; }
        public float EastClearance { get; set; }
        public float WestClearance { get; set; }
        public float AvailableWidth { get; set; }
        public float AvailableLength { get; set; }
        public bool IsAlleyOrCorridor { get; set; }
        public float CorridorWidth { get; set; }
        public List<string> BoundaryStructures { get; set; }

        // 4. Vertical Space & Overhead
        public bool HasCeiling { get; set; }
        public float CeilingHeight { get; set; }

        // 5. Living Entities & Population Census
        public int NearbyPedCount { get; set; }
        public int NearbyVehicleCount { get; set; }
        public string DominantFaction { get; set; }
        public string PoliceActivityLevel { get; set; }
        public List<string> EntityHighlights { get; set; }

        // 6. Spatial Constraints & Feasibility
        public bool NavMeshAvailable { get; set; }
        public string ObstructionDensity { get; set; }
        public bool InteriorCandidate { get; set; }
        public string InteriorCandidateName { get; set; }
        public string PlacementFeasibility { get; set; }

        // 7. Nearby Registered Environments
        public List<string> NearbyEnvironments { get; set; }

        // 8. Grounded Candidate Areas (Detected Usable Regions)
        public List<CandidateArea> CandidateAreas { get; set; }

        public WorldPerceptionSnapshot()
        {
            BoundaryStructures = new List<string>();
            EntityHighlights = new List<string>();
            NearbyEnvironments = new List<string>();
            CandidateAreas = new List<CandidateArea>();
            ZoneName = "Los Santos";
            StreetName = "Open Area";
            TerrainCategory = "Flat Urban Ground";
            ObstructionDensity = "Low";
            PlacementFeasibility = "Optimal";
            DominantFaction = "None";
            PoliceActivityLevel = "None";
            Timestamp = DateTime.UtcNow;
            NorthClearance = 50.0f;
            SouthClearance = 50.0f;
            EastClearance = 50.0f;
            WestClearance = 50.0f;
            AvailableWidth = 50.0f;
            AvailableLength = 50.0f;
            WalkableAreaPercent = 100.0f;
            RoadDistance = 15.0f;
            SidewalkDistance = 5.0f;
            NavMeshAvailable = true;
        }

        /// <summary>
        /// Formats this snapshot into a concise, token-efficient markdown brief (< 300 tokens)
        /// designed for injection into Astra World Director or Scene Architect prompts.
        /// </summary>
        public string FormatForPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("### SPATIAL PERCEPTION SNAPSHOT (LOCAL REALITY)");

            // 1. Location & Ground
            sb.AppendFormat("- **Location**: {0} ({1}) | Elevation: {2:F1}m | Slope: {3:F1}° ({4})\n",
                ZoneName, StreetName, Elevation, SlopeDegrees, TerrainCategory);

            // 2. Usable Footprint & Overhead
            string sky = HasCeiling ? string.Format("Covered/Ceiling ({0:F1}m)", CeilingHeight) : "Open Sky";
            sb.AppendFormat("- **Usable Footprint**: {0:F1}m × {1:F1}m clear ground | Walkable: {2:F0}% | Overhead: {3}\n",
                AvailableWidth, AvailableLength, WalkableAreaPercent, sky);

            // 3. Boundaries & Enclosure
            if (IsAlleyOrCorridor)
            {
                sb.AppendFormat("- **Enclosure**: Alley/Corridor (Width: {0:F1}m) | Boundaries: {1}\n",
                    CorridorWidth, BoundaryStructures.Count > 0 ? string.Join("; ", BoundaryStructures) : "Constrained on both flanks");
            }
            else if (BoundaryStructures.Count > 0)
            {
                sb.AppendFormat("- **Boundaries**: {0}\n", string.Join("; ", BoundaryStructures));
            }
            else
            {
                sb.AppendFormat("- **Boundaries**: Open expanse (Clearance: N:{0:F0}m, S:{1:F0}m, E:{2:F0}m, W:{3:F0}m)\n",
                    NorthClearance, SouthClearance, EastClearance, WestClearance);
            }

            // 4. Access & Constraints
            sb.AppendFormat("- **Access & NavMesh**: Road: {0:F1}m | Sidewalk: {1:F1}m | NavMesh: {2} | Obstructions: {3}\n",
                RoadDistance, SidewalkDistance, NavMeshAvailable ? "Available" : "Restricted", ObstructionDensity);

            // 5. Living Census
            string factionStr = !string.IsNullOrEmpty(DominantFaction) && DominantFaction != "None"
                ? string.Format(" (Faction: {0})", DominantFaction)
                : "";
            sb.AppendFormat("- **Living Census**: {0} Peds{1}, {2} Vehicles | Police Alert: {3}\n",
                NearbyPedCount, factionStr, NearbyVehicleCount, PoliceActivityLevel);

            if (EntityHighlights.Count > 0)
            {
                sb.AppendFormat("- **Entity Notes**: {0}\n", string.Join("; ", EntityHighlights));
            }

            // 6. Feasibility & Existing Venues
            if (NearbyEnvironments.Count > 0)
            {
                sb.AppendFormat("- **Nearby Venues**: {0}\n", string.Join(", ", NearbyEnvironments));
            }

            sb.AppendFormat("- **Spatial Feasibility**: {0}\n", PlacementFeasibility);

            // 7. Grounded Candidate Spatial Regions
            if (CandidateAreas != null && CandidateAreas.Count > 0)
            {
                sb.AppendLine("- **Candidate Usable Regions**:");
                foreach (var area in CandidateAreas)
                {
                    string tagsStr = area.SuitabilityTags.Count > 0 ? string.Join(", ", area.SuitabilityTags) : "general";
                    sb.AppendFormat("  • [{0}] {1} ({2:F0}m×{3:F0}m, {4}, Slope: {5:F1}°, Seclusion: {6:F2}) -> Best for: {7}\n",
                        area.AreaId, area.Label, area.Dimensions.X, area.Dimensions.Y,
                        area.SurfaceType, area.SlopeDegrees, area.SeclusionScore, tagsStr);
                }
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Represents a scored candidate location when evaluating multiple candidate sites for placement.
    /// </summary>
    public class CandidatePerceptionScore
    {
        public GTA.Math.Vector3 Position { get; set; }
        public string CandidateLabel { get; set; }
        public float FitnessScore { get; set; } // 0.0 to 1.0
        public string Reason { get; set; }
        public WorldPerceptionSnapshot Snapshot { get; set; }

        public CandidatePerceptionScore()
        {
            CandidateLabel = "Candidate";
            FitnessScore = 0.5f;
            Reason = "Unscored";
        }
    }
}
