using System;

namespace DynamicWorldEngine.Spatial
{
    public class CalculatedTransform
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }
        public float Pitch { get; set; }
        public float Roll { get; set; }

        public CalculatedTransform(float x, float y, float z, float heading, float pitch = 0f, float roll = 0f)
        {
            X = x;
            Y = y;
            Z = z;
            Heading = heading;
            Pitch = pitch;
            Roll = roll;
        }
    }

    public static class PropPlacementEngine
    {
        private const float Deg2Rad = (float)Math.PI / 180.0f;
        private const float Rad2Deg = 180.0f / (float)Math.PI;

        /// <summary>
        /// Fast 2D Yaw-only transform (for level surfaces and standard outdoor/indoor placements)
        /// </summary>
        public static CalculatedTransform LocalToWorldYawOnly(
            float originX, float originY, float originZ, float originHeading,
            float localOffsetX, float localOffsetY, float localOffsetZ,
            float localHeading = 0f, float localPitch = 0f, float localRoll = 0f)
        {
            float radYaw = originHeading * Deg2Rad;
            float sinYaw = (float)Math.Sin(radYaw);
            float cosYaw = (float)Math.Cos(radYaw);

            // GTA coordinate frame: +Y is North/Forward, +X is East/Right, +Z is Up
            float worldX = originX - (sinYaw * localOffsetY) + (cosYaw * localOffsetX);
            float worldY = originY + (cosYaw * localOffsetY) + (sinYaw * localOffsetX);
            float worldZ = originZ + localOffsetZ;

            float worldHeading = (originHeading + localHeading) % 360.0f;
            if (worldHeading < 0) worldHeading += 360.0f;

            return new CalculatedTransform(worldX, worldY, worldZ, worldHeading, localPitch, localRoll);
        }

        /// <summary>
        /// Full 3D rotation transform applying 3D Euler rotation (Yaw, Pitch, Roll) to both the local offset vector and orientation
        /// </summary>
        public static CalculatedTransform LocalToWorldFullTransform(
            float originX, float originY, float originZ, float originHeading, float originPitch, float originRoll,
            float localOffsetX, float localOffsetY, float localOffsetZ,
            float localHeading = 0f, float localPitch = 0f, float localRoll = 0f)
        {
            // If parent has no pitch or roll, use the fast yaw path
            if (Math.Abs(originPitch) < 0.001f && Math.Abs(originRoll) < 0.001f)
            {
                return LocalToWorldYawOnly(originX, originY, originZ, originHeading, localOffsetX, localOffsetY, localOffsetZ, localHeading, localPitch, localRoll);
            }

            // Convert Euler angles to Radians
            float yawRad = originHeading * Deg2Rad;
            float pitchRad = originPitch * Deg2Rad;
            float rollRad = originRoll * Deg2Rad;

            float cosY = (float)Math.Cos(yawRad);
            float sinY = (float)Math.Sin(yawRad);
            float cosP = (float)Math.Cos(pitchRad);
            float sinP = (float)Math.Sin(pitchRad);
            float cosR = (float)Math.Cos(rollRad);
            float sinR = (float)Math.Sin(rollRad);

            // 3D Rotation Matrix R = Rz(yaw) * Rx(pitch) * Ry(roll)
            // R00, R01, R02 (X row)
            float r00 = cosY * cosR - sinY * sinP * sinR;
            float r01 = -sinY * cosP;
            float r02 = cosY * sinR + sinY * sinP * cosR;

            // R10, R11, R12 (Y row)
            float r10 = sinY * cosR + cosY * sinP * sinR;
            float r11 = cosY * cosP;
            float r12 = sinY * sinR - cosY * sinP * cosR;

            // R20, R21, R22 (Z row)
            float r20 = -cosP * sinR;
            float r21 = sinP;
            float r22 = cosP * cosR;

            // Transform the local offset vector: (localOffsetX, localOffsetY, localOffsetZ)
            // Note: In GTA local coords, localOffsetX is right, localOffsetY is forward, localOffsetZ is up
            float rotX = (r00 * localOffsetX) - (r01 * localOffsetY) + (r02 * localOffsetZ);
            float rotY = (r10 * localOffsetX) + (r11 * localOffsetY) + (r12 * localOffsetZ);
            float rotZ = (r20 * localOffsetX) + (r21 * localOffsetY) + (r22 * localOffsetZ);

            float worldX = originX + rotX;
            float worldY = originY + rotY;
            float worldZ = originZ + rotZ;

            // Compound rotation angles
            float worldHeading = (originHeading + localHeading) % 360.0f;
            if (worldHeading < 0) worldHeading += 360.0f;

            float worldPitch = originPitch + localPitch;
            float worldRoll = originRoll + localRoll;

            return new CalculatedTransform(worldX, worldY, worldZ, worldHeading, worldPitch, worldRoll);
        }

        public static CalculatedTransform LocalToWorld(
            float originX, float originY, float originZ, float originHeading,
            float localOffsetX, float localOffsetY, float localOffsetZ,
            float localHeading = 0f, float localPitch = 0f, float localRoll = 0f,
            float originPitch = 0f, float originRoll = 0f)
        {
            return LocalToWorldFullTransform(
                originX, originY, originZ, originHeading, originPitch, originRoll,
                localOffsetX, localOffsetY, localOffsetZ,
                localHeading, localPitch, localRoll
            );
        }
    }
}
