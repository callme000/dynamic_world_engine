using System;
using System.Collections.Generic;

namespace DynamicWorldEngine.Spatial
{
    public class HierarchicalTransformEngine
    {
        public static CalculatedTransform ComputeChildWorldTransform(
            CalculatedTransform parentTransform,
            float childLocalX, float childLocalY, float childLocalZ,
            float childLocalHeading = 0f, float childLocalPitch = 0f, float childLocalRoll = 0f)
        {
            if (parentTransform == null)
            {
                return new CalculatedTransform(childLocalX, childLocalY, childLocalZ, childLocalHeading, childLocalPitch, childLocalRoll);
            }

            return PropPlacementEngine.LocalToWorldFullTransform(
                parentTransform.X, parentTransform.Y, parentTransform.Z, parentTransform.Heading, parentTransform.Pitch, parentTransform.Roll,
                childLocalX, childLocalY, childLocalZ,
                childLocalHeading, childLocalPitch, childLocalRoll
            );
        }

        public static CalculatedTransform ComputeSurfaceSnapTransform(
            CalculatedTransform parentTransform,
            float surfaceThickness,
            float childLocalX, float childLocalY,
            float childLocalHeading = 0f, float childLocalPitch = 0f, float childLocalRoll = 0f)
        {
            if (parentTransform == null)
            {
                return new CalculatedTransform(childLocalX, childLocalY, surfaceThickness, childLocalHeading, childLocalPitch, childLocalRoll);
            }

            // Snap cleanly on top of the parent prop (+surfaceThickness along the parent's local upward normal)
            return ComputeChildWorldTransform(
                parentTransform,
                childLocalX, childLocalY, surfaceThickness,
                childLocalHeading, childLocalPitch, childLocalRoll
            );
        }
    }
}
