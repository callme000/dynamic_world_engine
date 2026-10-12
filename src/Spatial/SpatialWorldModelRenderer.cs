using System;
using System.Collections.Generic;
using System.Text;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Spatial World Model Visualizer & Inspector.
    /// Generates top-down 2D ASCII spatial maps and detailed object inspection reports
    /// to make the spatial reality human-inspectable and LLM-comprehensible.
    /// </summary>
    public static class SpatialWorldModelRenderer
    {
        /// <summary>
        /// Renders a top-down 2D ASCII spatial map of the SpatialWorldModel.
        /// </summary>
        public static string RenderAsciiMap(
            SpatialWorldModel model,
            string title = "SPATIAL PERCEPTION MAP",
            int gridCols = 40,
            int gridRows = 20)
        {
            if (model == null) return "[Empty Spatial World Model]";

            float minX = model.BoundsMin.X;
            float maxX = model.BoundsMax.X;
            float minY = model.BoundsMin.Y;
            float maxY = model.BoundsMax.Y;

            float spanX = Math.Max(10f, maxX - minX);
            float spanY = Math.Max(10f, maxY - minY);

            char[,] grid = new char[gridRows, gridCols];

            // 1. Base initialization with default ground
            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    grid[r, c] = ' ';
                }
            }

            // 2. Plot Surfaces
            foreach (var surf in model.Surfaces)
            {
                int c = (int)(((surf.Position.X - minX) / spanX) * (gridCols - 1));
                int r = (int)(((maxY - surf.Position.Y) / spanY) * (gridRows - 1)); // Invert Y for top-down (North is Up)

                if (c >= 0 && c < gridCols && r >= 0 && r < gridRows)
                {
                    if (surf.SurfaceType == "WATER") grid[r, c] = '~';
                    else if (surf.SurfaceType == "SAND") grid[r, c] = '.';
                    else if (surf.SurfaceType == "CONCRETE") grid[r, c] = '=';
                    else if (surf.SurfaceType == "ASPHALT") grid[r, c] = '#';
                    else if (surf.SurfaceType == "GRASS") grid[r, c] = ',';
                    else grid[r, c] = '.';
                }
            }

            // 3. Plot Paths
            foreach (var p in model.Paths)
            {
                char pathChar = p.PathType == "ROAD" ? '#' : '=';
                foreach (var wp in p.Waypoints)
                {
                    int c = (int)(((wp.X - minX) / spanX) * (gridCols - 1));
                    int r = (int)(((maxY - wp.Y) / spanY) * (gridRows - 1));
                    if (c >= 0 && c < gridCols && r >= 0 && r < gridRows)
                    {
                        grid[r, c] = pathChar;
                    }
                }
            }

            // 4. Plot Obstacles
            foreach (var obs in model.Obstacles)
            {
                int c = (int)(((obs.Position.X - minX) / spanX) * (gridCols - 1));
                int r = (int)(((maxY - obs.Position.Y) / spanY) * (gridRows - 1));
                if (c >= 0 && c < gridCols && r >= 0 && r < gridRows)
                {
                    grid[r, c] = obs.ObstacleType == "TREE" ? 'T' : 'X';
                }
            }

            // 5. Plot Entities
            foreach (var ent in model.ExistingEntities)
            {
                int c = (int)(((ent.Position.X - minX) / spanX) * (gridCols - 1));
                int r = (int)(((maxY - ent.Position.Y) / spanY) * (gridRows - 1));
                if (c >= 0 && c < gridCols && r >= 0 && r < gridRows)
                {
                    grid[r, c] = ent.EntityType == "VEHICLE" ? 'V' : 'P';
                }
            }

            // 6. Plot Candidate Area Markers (e.g. [A], [B])
            foreach (var area in model.CandidateAreas)
            {
                int c = (int)(((area.Center.X - minX) / spanX) * (gridCols - 1));
                int r = (int)(((maxY - area.Center.Y) / spanY) * (gridRows - 1));

                string marker = string.Format("[{0}]", area.AreaId.Replace("Area_", ""));
                if (c >= 1 && c + marker.Length < gridCols && r >= 0 && r < gridRows)
                {
                    for (int i = 0; i < marker.Length; i++)
                    {
                        grid[r, c - 1 + i] = marker[i];
                    }
                }
            }

            // Build output string
            var sb = new StringBuilder();
            sb.AppendLine("┌────────────────────────────────────────────────────────┐");
            sb.AppendFormat("│  {0,-52}  │\n", title.ToUpperInvariant());
            sb.AppendLine("│  [NORTH ^]  Legend: ~ Water  . Sand  = Walkway  # Road  │");
            sb.AppendLine("│             T Tree   X Obstacle  P Ped  V Veh  [A] Area│");
            sb.AppendLine("├────────────────────────────────────────────────────────┤");

            for (int r = 0; r < gridRows; r++)
            {
                sb.Append("│  ");
                for (int c = 0; c < gridCols; c++)
                {
                    sb.Append(grid[r, c]);
                }
                sb.AppendLine("  │");
            }

            sb.AppendLine("└────────────────────────────────────────────────────────┘");

            // Candidate Area Summary
            if (model.CandidateAreas.Count > 0)
            {
                sb.AppendLine("CANDIDATE REGIONS:");
                foreach (var area in model.CandidateAreas)
                {
                    sb.AppendLine(" • " + area.FormatBrief());
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Detailed inspector report for a specific scene prop or placement.
        /// </summary>
        public static string InspectObject(
            string objectId,
            string modelName,
            Vector3 position,
            Vector3 rotation,
            string surfaceType,
            float clearanceFront,
            float clearanceBack,
            float clearanceLeft,
            float clearanceRight,
            string facingTarget = null,
            List<string> relationships = null)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("OBJECT INSPECTOR: {0} ({1})\n", objectId, modelName);
            sb.AppendFormat(" • Position:    ({0:F2}, {1:F2}, {2:F2})\n", position.X, position.Y, position.Z);
            sb.AppendFormat(" • Rotation:    Heading: {0:F1}° | Pitch: {1:F1}° | Roll: {2:F1}°\n", rotation.Z, rotation.X, rotation.Y);
            sb.AppendFormat(" • Surface:     {0}\n", surfaceType ?? "Unknown");
            sb.AppendFormat(" • Clearance:   Front: {0:F1}m | Back: {1:F1}m | Left: {2:F1}m | Right: {3:F1}m\n",
                clearanceFront, clearanceBack, clearanceLeft, clearanceRight);

            if (!string.IsNullOrEmpty(facingTarget))
            {
                sb.AppendFormat(" • Facing:      {0}\n", facingTarget);
            }

            if (relationships != null && relationships.Count > 0)
            {
                sb.AppendFormat(" • Relations:   {0}\n", string.Join("; ", relationships));
            }

            return sb.ToString().TrimEnd();
        }
    }
}
