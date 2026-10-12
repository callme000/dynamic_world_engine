using System;
using System.Collections.Generic;
using System.Text;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Scoped, lean world context packet assembled for AI scene generation (Stage 6 of the Spatial Orchestration Pipeline).
    /// Solves the "spawning while blind" architectural gap by packaging grounded physical terrain,
    /// candidate areas, and environmental constraints into an information-dense prompt (< 400 tokens).
    /// Adheres to Laws 1 & 4 of the 10 Laws of Information Architecture.
    /// </summary>
    public class WorldContextPacket
    {
        public LocationCandidate Location { get; set; }
        public WorldPerceptionSnapshot PerceptionSnapshot { get; set; }
        public List<CandidateArea> CandidateAreas { get; set; }
        public List<string> AmbientCensus { get; set; }
        public List<string> RecommendedAssets { get; set; }
        public List<string> HardConstraints { get; set; }
        public string AsciiMapPreview { get; set; }

        public WorldContextPacket()
        {
            CandidateAreas = new List<CandidateArea>();
            AmbientCensus = new List<string>();
            RecommendedAssets = new List<string>();
            HardConstraints = new List<string>();
            AsciiMapPreview = "";
        }

        public static WorldContextPacket Build(
            LocationCandidate location,
            SpatialWorldModel model,
            WorldMemoryStore store = null,
            string userQuery = "")
        {
            var packet = new WorldContextPacket
            {
                Location = location
            };

            if (model != null)
            {
                if (model.CandidateAreas != null)
                {
                    packet.CandidateAreas.AddRange(model.CandidateAreas);
                }

                if (model.SemanticSnapshot != null)
                {
                    packet.PerceptionSnapshot = model.SemanticSnapshot;
                }

                // Render ASCII preview
                try
                {
                    packet.AsciiMapPreview = SpatialWorldModelRenderer.RenderAsciiMap(model, (location != null ? location.DisplayName : "SPATIAL MAP"));
                }
                catch { }

                // Compile hard constraints from model
                if (model.Obstacles != null && model.Obstacles.Count > 0)
                {
                    packet.HardConstraints.Add(string.Format("{0} physical obstacles detected in local perimeter (walls/fences/props).", model.Obstacles.Count));
                }

                if (packet.PerceptionSnapshot != null)
                {
                    if (packet.PerceptionSnapshot.HasWaterNearby && packet.PerceptionSnapshot.WaterDistance >= 0 && packet.PerceptionSnapshot.WaterDistance < 30f)
                    {
                        packet.HardConstraints.Add(string.Format("Water shoreline at {0:F1}m distance (keep non-amphibious entities away from water).", packet.PerceptionSnapshot.WaterDistance));
                    }
                    if (packet.PerceptionSnapshot.RoadDistance >= 0 && packet.PerceptionSnapshot.RoadDistance < 20f)
                    {
                        packet.HardConstraints.Add(string.Format("Active roadway at {0:F1}m distance (ensure vehicle staging connects to roadway).", packet.PerceptionSnapshot.RoadDistance));
                    }
                    if (packet.PerceptionSnapshot.SlopeDegrees > 15f)
                    {
                        packet.HardConstraints.Add(string.Format("Steep incline ({0:F1} deg slope). Prioritize flat candidate regions.", packet.PerceptionSnapshot.SlopeDegrees));
                    }
                }
            }

            // Compile ambient entity census
            if (store != null)
            {
                var npcs = store.GetAllNpcs();
                if (npcs != null)
                {
                    foreach (var n in npcs)
                    {
                        if (n.IsAlive)
                        {
                            packet.AmbientCensus.Add(string.Format("{0} ({1}, {2})", n.Name, n.Occupation, n.CurrentState));
                        }
                    }
                }
            }

            // Curated authentic assets
            if (!string.IsNullOrEmpty(userQuery))
            {
                var cat = location != null ? location.Category : "general";
                if (cat.Contains("beach") || userQuery.Contains("beach") || userQuery.Contains("bonfire"))
                {
                    packet.RecommendedAssets.Add("props: prop_beach_fire, prop_chair_02, prop_table_01, prop_cooler_01a");
                    packet.RecommendedAssets.Add("vehicles: rebel2, surfer, dinghy");
                }
                else if (cat.Contains("airfield") || userQuery.Contains("airfield") || userQuery.Contains("plane"))
                {
                    packet.RecommendedAssets.Add("props: prop_toolchest_01, prop_barrel_exp_01a, prop_barrier_work05");
                    packet.RecommendedAssets.Add("vehicles: dodo, velum, titan, dubsta");
                }
                else if (cat.Contains("hood") || cat.Contains("street") || userQuery.Contains("gang"))
                {
                    packet.RecommendedAssets.Add("props: prop_chair_05, prop_bbq_3, prop_crate_01a");
                    packet.RecommendedAssets.Add("vehicles: buccaneer, voodoo, chino");
                }
                else
                {
                    packet.RecommendedAssets.Add("props: prop_table_03b, prop_chair_04a, prop_barrier_work05");
                    packet.RecommendedAssets.Add("vehicles: fbi, insurgent, granger");
                }
            }

            return packet;
        }

        /// <summary>
        /// Compiles an information-dense, highly-structured prompt brief for the AI Director (< 400 tokens).
        /// </summary>
        public string FormatForPrompt(bool includeAsciiMap = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== GROUNDED SPATIAL CONTEXT BRIEF ===");

            if (Location != null)
            {
                sb.AppendLine(string.Format("LOCATION: {0} ({1}) @ ({2:F1}, {3:F1}, {4:F1})",
                    Location.DisplayName, Location.Category,
                    Location.Coordinates.X, Location.Coordinates.Y, Location.Coordinates.Z));
            }

            if (PerceptionSnapshot != null)
            {
                sb.AppendLine(string.Format("TERRAIN: Surface={0} | Slope={1:F1} deg | Walkable={2:F0}% | RoadDist={3:F1}m | WaterDist={4:F1}m",
                    PerceptionSnapshot.TerrainCategory,
                    PerceptionSnapshot.SlopeDegrees,
                    PerceptionSnapshot.WalkableAreaPercent,
                    PerceptionSnapshot.RoadDistance >= 0 ? PerceptionSnapshot.RoadDistance.ToString("F1") : "none",
                    PerceptionSnapshot.WaterDistance >= 0 ? PerceptionSnapshot.WaterDistance.ToString("F1") : "none"));
            }

            if (CandidateAreas.Count > 0)
            {
                sb.AppendLine("CANDIDATE SPATIAL REGIONS (Choose one AreaId for scene anchor):");
                foreach (var ca in CandidateAreas)
                {
                    string tags = (ca.SuitabilityTags != null && ca.SuitabilityTags.Count > 0)
                        ? string.Join(", ", ca.SuitabilityTags.ToArray())
                        : "general";

                    sb.AppendLine(string.Format("  - [{0}] {1}: {2:F1}m x {3:F1}m footprint, Surface: {4}, Seclusion: {5:F2}, Suitable: [{6}]",
                        ca.AreaId, ca.Label, ca.Dimensions.X, ca.Dimensions.Y, ca.SurfaceType, ca.SeclusionScore, tags));
                }
            }

            if (HardConstraints.Count > 0)
            {
                sb.AppendLine("PHYSICAL CONSTRAINTS:");
                foreach (var c in HardConstraints)
                {
                    sb.AppendLine(string.Format("  * {0}", c));
                }
            }

            if (RecommendedAssets.Count > 0)
            {
                sb.AppendLine("RECOMMENDED AUTHENTIC GTA ASSETS:");
                foreach (var a in RecommendedAssets)
                {
                    sb.AppendLine(string.Format("  * {0}", a));
                }
            }

            if (AmbientCensus.Count > 0)
            {
                sb.AppendLine(string.Format("AMBIENT RESIDENTS: {0} nearby entities detected.", AmbientCensus.Count));
            }

            if (includeAsciiMap && !string.IsNullOrEmpty(AsciiMapPreview))
            {
                sb.AppendLine();
                sb.AppendLine(AsciiMapPreview);
            }

            sb.AppendLine("========================================");
            return sb.ToString();
        }
    }
}
