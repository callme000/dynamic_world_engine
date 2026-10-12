using System;
using System.Collections.Generic;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Evaluates physical feasibility and volumetric constraints for requested assets in target venues.
    /// Prevents oversized entities (submarines, aircraft) from clipping through indoor environments.
    /// </summary>
    public static class SpatialFeasibilityEvaluator
    {
        public static void EvaluateFeasibility(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Vector3 centerPos)
        {
            if (plan == null || data == null) return;

            // Generic Volumetric Feasibility Verification
            if (plan.FeasibilityAudit == null && plan.MatchedInterior != null && !plan.MatchedInterior.SupportsVehicles && data.Vehicles != null && data.Vehicles.Count > 0)
            {
                var veh = data.Vehicles[0];
                Vector3 dim = SpatialAssetRegistry.Instance.QueryModelDimensions(veh.Model);
                float maxLen = Math.Max(dim.X, dim.Y);
                if (maxLen > 10f || dim.Z > 4.5f)
                {
                    plan.FeasibilityAudit = new SpatialFeasibilityAudit
                    {
                        RequestedAsset = veh.Model,
                        AssetClassification = string.Format("Oversized Entity ({0:F1}m Length, {1:F1}m Height)", maxLen, dim.Z),
                        PhysicalDimensions = string.Format("{0:F1}m x {1:F1}m x {2:F1}m", maxLen, Math.Min(dim.X, dim.Y), dim.Z),
                        EnvironmentClearance = "Interior Envelope (3.5m Ceiling, Pedestrian Doorways Only)",
                        SpatialConflictDetected = true,
                        ConflictReason = "Volumetric Context Mismatch: The requested vehicle exceeds the physical boundary dimensions and clearance ceiling of this interior.",
                        ResolutionStrategy = "CENTERPIECE_SUBMERSIBLE_ADAPTATION_AND_NEARBY_REALIZATION",
                        SuggestedRealization = "Centerpiece Auxiliary Submersible (Avisa) deployed inside venue, with full-scale vessel moored at nearby coastal deep-water coordinates."
                    };
                }
            }

            // Synthesize Ocean Shuttle Pocket if nearby realization is triggered
            if (plan.FeasibilityAudit != null && plan.FeasibilityAudit.ResolutionStrategy != null && plan.FeasibilityAudit.ResolutionStrategy.Contains("NEARBY") && plan.ConnectedPockets.Count == 0)
            {
                var oceanPocket = new PocketTransition(
                    plan.EnvironmentId + "_ocean_kosatka",
                    plan.EnvironmentId,
                    centerPos + new Vector3(-6.0f, 2.0f, 0.0f),
                    new Vector3(-1605.0f, -2820.0f, 0.5f),
                    centerPos,
                    "VIP Harbor Shuttle to Moored Full-Scale Kosatka Submarine",
                    false, true, "VIP Maritime Access"
                );
                plan.ConnectedPockets.Add(oceanPocket);
            }
        }
    }
}
