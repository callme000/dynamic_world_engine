using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Spatial Vision & Clearance Data for an active environment center.
    /// </summary>
    public class RoomClearanceData
    {
        public float FloorZ { get; set; }
        public float CeilingZ { get; set; }
        public bool HasCeiling { get; set; }
        public float CeilingClearance { get; set; }
        public float MaxClearRadius { get; set; }
        public bool IsAbyss { get; set; }
        public bool IsGroundSolid { get; set; }
        public string DiagnosticSummary { get; set; }
    }

    /// <summary>
    /// Autonomous Spatial Vision Engine.
    /// Provides active spatial vision, ground awareness, and collision verification
    /// to prevent players, props, and NPCs from being spawned into unstreamed voids or the abyss.
    /// Uses raycasting and native elevation queries to inspect the physical 3D world in real-time.
    /// </summary>
    public static class SpatialVisionEngine
    {
        /// <summary>
        /// Probes the physical 3D world at the target coordinates to find the true solid ground surface.
        /// </summary>
        public static bool ProbeGroundElevation(Vector3 targetPos, out float validGroundZ)
        {
            validGroundZ = targetPos.Z;

            if (!DynamicWorldEngine.Runtime.NativeFiberCoordinator.IsMainThread)
            {
                return false;
            }

            try
            {
                // 1. First probe via raycast directly downwards from 3.0m above
                Vector3 rayStart = new Vector3(targetPos.X, targetPos.Y, targetPos.Z + 3.0f);
                Vector3 rayEnd = new Vector3(targetPos.X, targetPos.Y, targetPos.Z - 12.0f);

                RaycastResult hit = World.Raycast(rayStart, rayEnd, IntersectFlags.Map | IntersectFlags.Objects);
                if (hit.DidHit)
                {
                    validGroundZ = hit.HitPosition.Z;
                    return true;
                }

                // 2. Native ground height query
                OutputArgument outZ = new OutputArgument();
                bool found = Function.Call<bool>(Hash.GET_GROUND_Z_FOR_3D_COORD, targetPos.X, targetPos.Y, targetPos.Z + 1.0f, outZ, false);
                if (found)
                {
                    float nativeGroundZ = outZ.GetResult<float>();
                    if (Math.Abs(nativeGroundZ) > 0.01f)
                    {
                        validGroundZ = nativeGroundZ;
                        return true;
                    }
                }

                // 3. ScriptHookV World fallback
                float shvdnGround = World.GetGroundHeight(targetPos);
                if (Math.Abs(shvdnGround) > 0.01f && Math.Abs(shvdnGround - targetPos.Z) < 15.0f)
                {
                    validGroundZ = shvdnGround;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SpatialVisionEngine] ProbeGroundElevation warning: " + ex.Message);
            }

            return false;
        }

        /// <summary>
        /// Detects whether the specified coordinate is floating in an unloaded abyss or void.
        /// </summary>
        public static bool IsAbyss(Vector3 targetPos, float groundZ, bool groundFound)
        {
            // If ground was completely undetected
            if (!groundFound)
            {
                // Underground shells (Z < -10) with no collision are definitely an abyss
                if (targetPos.Z < -10.0f) return true;
                return false;
            }

            // If the ground is more than 8.0m below the target Z in an underground location, it's a void
            if (targetPos.Z < 0f && (targetPos.Z - groundZ) > 8.0f)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Casts 3D rays in 6 directions from the center origin to sense real room boundaries,
        /// floor elevation, ceiling height, and wall clearance.
        /// </summary>
        public static RoomClearanceData SenseRoomClearance(Vector3 centerPos, float maxRadius = 20.0f)
        {
            var data = new RoomClearanceData
            {
                FloorZ = centerPos.Z,
                CeilingZ = centerPos.Z + 10.0f,
                HasCeiling = false,
                CeilingClearance = 10.0f,
                MaxClearRadius = maxRadius,
                IsAbyss = false,
                IsGroundSolid = false
            };

            if (!DynamicWorldEngine.Runtime.NativeFiberCoordinator.IsMainThread)
            {
                return data;
            }

            try
            {
                // 1. Raycast Down (Floor)
                float groundZ;
                bool groundFound = ProbeGroundElevation(centerPos, out groundZ);
                data.IsGroundSolid = groundFound;
                data.FloorZ = groundZ;
                data.IsAbyss = IsAbyss(centerPos, groundZ, groundFound);

                // 2. Raycast Up (Ceiling)
                Vector3 ceilStart = new Vector3(centerPos.X, centerPos.Y, groundZ + 0.5f);
                Vector3 ceilEnd = new Vector3(centerPos.X, centerPos.Y, groundZ + 35.0f);
                RaycastResult ceilHit = World.Raycast(ceilStart, ceilEnd, IntersectFlags.Map | IntersectFlags.Objects);

                if (ceilHit.DidHit && ceilHit.HitPosition.Z > groundZ + 1.5f)
                {
                    data.HasCeiling = true;
                    data.CeilingZ = ceilHit.HitPosition.Z;
                    data.CeilingClearance = ceilHit.HitPosition.Z - groundZ;
                }
                else
                {
                    data.HasCeiling = false;
                    data.CeilingClearance = 99.0f; // Open sky (e.g. estate grounds / terrace / pool)
                }

                // 3. Raycast 4 Horizontal Compass Directions for wall boundaries
                Vector3[] directions = new Vector3[]
                {
                    new Vector3(1f, 0f, 0f),  // East
                    new Vector3(-1f, 0f, 0f), // West
                    new Vector3(0f, 1f, 0f),  // North
                    new Vector3(0f, -1f, 0f)  // South
                };

                float minWallDist = maxRadius;
                Vector3 eyePos = new Vector3(centerPos.X, centerPos.Y, groundZ + 1.0f);

                foreach (var dir in directions)
                {
                    RaycastResult wallHit = World.Raycast(eyePos, eyePos + (dir * maxRadius), IntersectFlags.Map | IntersectFlags.Objects);
                    if (wallHit.DidHit)
                    {
                        float dist = eyePos.DistanceTo(wallHit.HitPosition);
                        if (dist < minWallDist) minWallDist = dist;
                    }
                }

                data.MaxClearRadius = Math.Max(2.5f, minWallDist - 0.5f);
                data.DiagnosticSummary = string.Format(
                    "SpatialVision: Floor={0:F1}m, Ceiling={1}, MaxRadius={2:F1}m, Solid={3}, Abyss={4}",
                    data.FloorZ,
                    data.HasCeiling ? (data.CeilingClearance.ToString("F1") + "m") : "OpenSky",
                    data.MaxClearRadius,
                    data.IsGroundSolid,
                    data.IsAbyss
                );
            }
            catch (Exception ex)
            {
                data.DiagnosticSummary = "SpatialVision error: " + ex.Message;
            }

            return data;
        }

        /// <summary>
        /// Verifies whether the requested placement is safe. If an abyss is detected (unloaded shell / void),
        /// automatically redirects the target coordinates to a verified solid fallback position.
        /// </summary>
        public static Vector3 ResolveSafePlacementPoint(Vector3 desiredCoords, Vector3 fallbackEntrance, string venueTitle)
        {
            float groundZ;
            bool groundFound = ProbeGroundElevation(desiredCoords, out groundZ);

            if (IsAbyss(desiredCoords, groundZ, groundFound))
            {
                Console.WriteLine("[SpatialVisionEngine] ABYSS GUARD TRIGGERED for '{0}' at ({1:F1}, {2:F1}, {3:F1})! Falling back to safe surface ground: ({4:F1}, {5:F1}, {6:F1})",
                    venueTitle, desiredCoords.X, desiredCoords.Y, desiredCoords.Z, fallbackEntrance.X, fallbackEntrance.Y, fallbackEntrance.Z);

                float fallbackGroundZ;
                if (ProbeGroundElevation(fallbackEntrance, out fallbackGroundZ))
                {
                    return new Vector3(fallbackEntrance.X, fallbackEntrance.Y, fallbackGroundZ);
                }

                return fallbackEntrance;
            }

            if (groundFound)
            {
                return new Vector3(desiredCoords.X, desiredCoords.Y, groundZ);
            }

            return desiredCoords;
        }

        /// <summary>
        /// Probes for water bodies (swimming pools, oceans, lakes) within a radius of the center.
        /// Returns true if a water surface is detected with swimming depth.
        /// </summary>
        public static bool ProbeWaterSurface(Vector3 centerPos, float radius, out Vector3 waterCenter, out float waterElevation, out float waterRadius)
        {
            waterCenter = centerPos;
            waterElevation = centerPos.Z;
            waterRadius = 0.0f;

            if (!DynamicWorldEngine.Runtime.NativeFiberCoordinator.IsMainThread)
            {
                return false;
            }

            try
            {
                // Multi-point radial grid probe for water height
                List<Vector3> waterHits = new List<Vector3>();

                float[] probeDists = new float[] { 4f, 8f, 14f, 20f, 28f };
                for (int d = 0; d < probeDists.Length; d++)
                {
                    float dist = probeDists[d];
                    if (dist > radius) break;

                    for (int a = 0; a < 8; a++)
                    {
                        float angle = a * (float)Math.PI * 0.25f;
                        float px = centerPos.X + (float)Math.Cos(angle) * dist;
                        float py = centerPos.Y + (float)Math.Sin(angle) * dist;

                        OutputArgument outHeight = new OutputArgument();
                        // Native Hash 0xF68273F11CC26227: GET_WATER_HEIGHT
                        bool hasWater = Function.Call<bool>(Hash.GET_WATER_HEIGHT, px, py, centerPos.Z + 1.5f, outHeight);
                        if (hasWater)
                        {
                            float wZ = outHeight.GetResult<float>();
                            waterHits.Add(new Vector3(px, py, wZ));
                        }
                    }
                }

                if (waterHits.Count >= 3)
                {
                    float sumX = 0f, sumY = 0f, sumZ = 0f;
                    foreach (var pt in waterHits)
                    {
                        sumX += pt.X;
                        sumY += pt.Y;
                        sumZ += pt.Z;
                    }
                    waterCenter = new Vector3(sumX / waterHits.Count, sumY / waterHits.Count, sumZ / waterHits.Count);
                    waterElevation = waterCenter.Z;

                    float maxDist = 0f;
                    foreach (var pt in waterHits)
                    {
                        float d = new Vector2(pt.X - waterCenter.X, pt.Y - waterCenter.Y).Length();
                        if (d > maxDist) maxDist = d;
                    }
                    waterRadius = Math.Max(3.0f, maxDist);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SpatialVisionEngine] ProbeWaterSurface warning: " + ex.Message);
            }

            return false;
        }
    }
}
