using System;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    public static class SpatialQueryService
    {
        public static SimpleVector3 CalculateSpawnPositionAhead(float playerX, float playerY, float playerZ, float forwardX, float forwardY, float distanceAhead = 2.0f, float playerHeading = 0f)
        {
            return GroundRaycaster.CalculateSpawnPositionAhead(playerX, playerY, playerZ, forwardX, forwardY, distanceAhead, playerHeading);
        }

        public static Vector3 FindGroundPosition(Vector3 searchPos)
        {
            return GroundRaycaster.FindGroundPosition(searchPos);
        }

        public static Vector3 FindClearSpawnPosition(Vector3 origin, Vector3 forward, float distanceAhead, float requiredRadius = 2.0f)
        {
            return GroundRaycaster.FindClearSpawnPosition(origin, forward, distanceAhead, requiredRadius);
        }
    }
}
