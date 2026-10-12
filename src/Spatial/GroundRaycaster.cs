using System;
using DynamicWorldEngine.Runtime;
using GTA;
using GTA.Math;
using GTA.Native;

namespace DynamicWorldEngine.Spatial
{
    public class SimpleVector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }

        public SimpleVector3(float x, float y, float z, float heading = 0f)
        {
            X = x;
            Y = y;
            Z = z;
            Heading = heading;
        }
    }

    /// <summary>
    /// Authoritative Raycast Probes and Collision Safety Service.
    /// Strictly fiber-confined: enforces Inviolable Rule #2 so that zero native GTA raycasts
    /// or elevation checks execute on background threads.
    /// </summary>
    public static class GroundRaycaster
    {
        public static SimpleVector3 CalculateSpawnPositionAhead(
            float playerX, float playerY, float playerZ,
            float forwardX, float forwardY,
            float distanceAhead = 2.0f, float playerHeading = 0f)
        {
            float targetX = playerX + (forwardX * distanceAhead);
            float targetY = playerY + (forwardY * distanceAhead);
            float targetZ = playerZ;

            // Facing opposite to player so NPC looks at player
            float spawnHeading = (playerHeading + 180f) % 360f;

            return new SimpleVector3(targetX, targetY, targetZ, spawnHeading);
        }

        /// <summary>
        /// Probes ground height at the search coordinates.
        /// Confinable: returns searchPos immediately if running off-thread or headless.
        /// </summary>
        public static Vector3 FindGroundPosition(Vector3 searchPos)
        {
            if (!NativeFiberCoordinator.IsMainThread)
            {
                return searchPos;
            }

            try
            {
                float groundZ = World.GetGroundHeight(searchPos);
                if (groundZ > -100.0f && groundZ < 1000.0f)
                {
                    return new Vector3(searchPos.X, searchPos.Y, groundZ);
                }
            }
            catch { }

            return searchPos;
        }

        /// <summary>
        /// Casts a raycast ahead to find an unobstructed ground position.
        /// If obstacle is hit, pulls back safely. Returns candidate ground if off-thread.
        /// </summary>
        public static Vector3 FindClearSpawnPosition(Vector3 origin, Vector3 forward, float distanceAhead, float requiredRadius = 2.0f)
        {
            Vector3 candidate = origin + (forward * distanceAhead);

            if (!NativeFiberCoordinator.IsMainThread)
            {
                return candidate;
            }

            Vector3 withGround = FindGroundPosition(candidate);

            try
            {
                RaycastResult hit = World.Raycast(
                    origin + new Vector3(0, 0, 0.5f),
                    candidate + new Vector3(0, 0, 0.5f),
                    IntersectFlags.Map | IntersectFlags.Objects
                );

                if (hit.DidHit && hit.HitPosition.DistanceTo(origin) < distanceAhead)
                {
                    Vector3 adjusted = origin + (forward * Math.Max(1.0f, hit.HitPosition.DistanceTo(origin) - 1.0f));
                    return FindGroundPosition(adjusted);
                }
            }
            catch { }

            return withGround;
        }

        /// <summary>
        /// Performs downward vertical raycast and native elevation query to find true solid ground.
        /// </summary>
        public static bool ProbeGroundElevation(Vector3 targetPos, out float validGroundZ)
        {
            validGroundZ = targetPos.Z;

            if (!NativeFiberCoordinator.IsMainThread)
            {
                return false;
            }

            try
            {
                // 1. Raycast directly downwards from 3.0m above
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
            catch { }

            return false;
        }
    }
}
