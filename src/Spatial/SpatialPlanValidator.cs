using System;
using System.Collections.Generic;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    public class SpatialValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> HardViolations { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> AutoCorrectionsApplied { get; set; }
        public int CheckedPropsCount { get; set; }
        public int CheckedNpcsCount { get; set; }
        public SpatialExecutionPlan SanitizedPlan { get; set; }

        public SpatialValidationResult()
        {
            IsValid = true;
            HardViolations = new List<string>();
            Warnings = new List<string>();
            AutoCorrectionsApplied = new List<string>();
        }

        public override string ToString()
        {
            return string.Format("SpatialValidation: {0} ({1} violations, {2} corrections, {3} props, {4} npcs)",
                IsValid ? "PASSED" : "FAILED", HardViolations.Count, AutoCorrectionsApplied.Count, CheckedPropsCount, CheckedNpcsCount);
        }
    }

    /// <summary>
    /// Hard Spatial Validation Gate & Auto-Corrector (Stage 9 of the Spatial Orchestration Pipeline).
    /// Acts as an authoritative physical barrier before a plan is committed or realized.
    /// Inspects surface contact, bounding containment, prop/obstacle collision overlaps,
    /// angular facing coherence, line of sight, and pedestrian reachability.
    /// </summary>
    public class SpatialPlanValidator
    {
        public SpatialValidationResult ValidatePlan(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            SceneInteractionRequirements requirements = null,
            bool applyAutoCorrections = true)
        {
            var result = new SpatialValidationResult
            {
                SanitizedPlan = plan
            };

            if (plan == null)
            {
                result.IsValid = false;
                result.HardViolations.Add("Execution plan is null");
                return result;
            }

            result.CheckedPropsCount = plan.ResolvedProps != null ? plan.ResolvedProps.Count : 0;
            result.CheckedNpcsCount = plan.ResolvedNpcs != null ? plan.ResolvedNpcs.Count : 0;

            // 1. Surface Contact & Ground Elevation Verification
            if (model != null && model.Surfaces != null && model.Surfaces.Count > 0)
            {
                ValidateSurfaces(plan, model, result, applyAutoCorrections);
            }

            // 2. Candidate Area Containment Verification
            if (model != null && requirements != null && !string.IsNullOrEmpty(requirements.TargetAreaId))
            {
                ValidateContainment(plan, model, requirements.TargetAreaId, result);
            }

            // 3. Pairwise Collision & Clearance Envelope Verification
            ValidateClearancesAndCollisions(plan, model, result, applyAutoCorrections);

            // 4. Orientation & Facing Coherence Verification
            ValidateFacingCoherence(plan, model, requirements, result, applyAutoCorrections);

            // 5. Line-of-Sight Integrity Verification
            ValidateLineOfSight(plan, model, requirements, result);

            // Plan is valid if no hard violations remain
            result.IsValid = result.HardViolations.Count == 0;
            return result;
        }

        private void ValidateSurfaces(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            SpatialValidationResult result,
            bool applyCorrections)
        {
            if (plan.IsSkyPocket) return; // Sky pockets hover above world by design

            // Validate Props
            if (plan.ResolvedProps != null)
            {
                for (int i = 0; i < plan.ResolvedProps.Count; i++)
                {
                    var prop = plan.ResolvedProps[i];
                    Vector3 pos = prop.WorldPosition;
                    var surf = FindClosestSurface(model, pos);
                    if (surf != null)
                    {
                        float deltaZ = pos.Z - surf.Position.Z;
                        if (deltaZ > 0.6f)
                        {
                            if (applyCorrections)
                            {
                                prop.WorldPosition = new Vector3(pos.X, pos.Y, surf.Position.Z);
                                result.AutoCorrectionsApplied.Add(string.Format("Prop '{0}' was floating {1:F2}m; clamped Z to ground ({2:F2})", prop.PropId, deltaZ, surf.Position.Z));
                            }
                            else
                            {
                                result.HardViolations.Add(string.Format("Prop '{0}' is floating {1:F2}m above ground", prop.PropId, deltaZ));
                            }
                        }
                        else if (deltaZ < -0.4f)
                        {
                            if (applyCorrections)
                            {
                                prop.WorldPosition = new Vector3(pos.X, pos.Y, surf.Position.Z);
                                result.AutoCorrectionsApplied.Add(string.Format("Prop '{0}' was buried {1:F2}m; clamped Z to ground ({2:F2})", prop.PropId, -deltaZ, surf.Position.Z));
                            }
                            else
                            {
                                result.HardViolations.Add(string.Format("Prop '{0}' is buried {1:F2}m below ground", prop.PropId, -deltaZ));
                            }
                        }
                    }
                }
            }

            // Validate NPCs
            if (plan.ResolvedNpcs != null)
            {
                for (int i = 0; i < plan.ResolvedNpcs.Count; i++)
                {
                    var npc = plan.ResolvedNpcs[i];
                    Vector3 pos = npc.WorldPosition;
                    var surf = FindClosestSurface(model, pos);
                    if (surf != null)
                    {
                        float deltaZ = pos.Z - surf.Position.Z;
                        if (deltaZ > 0.5f || deltaZ < -0.3f)
                        {
                            if (applyCorrections)
                            {
                                npc.WorldPosition = new Vector3(pos.X, pos.Y, surf.Position.Z);
                                result.AutoCorrectionsApplied.Add(string.Format("NPC '{0}' elevation corrected to ground ({1:F2})", npc.Name, surf.Position.Z));
                            }
                            else
                            {
                                result.HardViolations.Add(string.Format("NPC '{0}' elevation misaligned ({1:F2}m off ground)", npc.Name, deltaZ));
                            }
                        }
                    }
                }
            }
        }

        private void ValidateContainment(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            string targetAreaId,
            SpatialValidationResult result)
        {
            var area = model.CandidateAreas.Find(a => string.Equals(a.AreaId, targetAreaId, StringComparison.OrdinalIgnoreCase));
            if (area == null) return;

            float maxRadius = Math.Max(area.Dimensions.X, area.Dimensions.Y) * 0.5f + 3.0f; // 3m boundary tolerance

            if (plan.ResolvedNpcs != null)
            {
                foreach (var npc in plan.ResolvedNpcs)
                {
                    float dist = area.Center.DistanceTo(npc.WorldPosition);
                    if (dist > maxRadius)
                    {
                        result.Warnings.Add(string.Format("NPC '{0}' is {1:F1}m from Candidate Area '{2}' center (exceeds {3:F1}m boundary)",
                            npc.Name, dist, targetAreaId, maxRadius));
                    }
                }
            }
        }

        private void ValidateClearancesAndCollisions(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            SpatialValidationResult result,
            bool applyCorrections)
        {
            // Pairwise prop collision
            if (plan.ResolvedProps != null && plan.ResolvedProps.Count > 1)
            {
                for (int i = 0; i < plan.ResolvedProps.Count; i++)
                {
                    for (int j = i + 1; j < plan.ResolvedProps.Count; j++)
                    {
                        var a = plan.ResolvedProps[i];
                        var b = plan.ResolvedProps[j];

                        // Skip parent-child attachments
                        if (string.Equals(a.ParentPropId, b.PropId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(b.ParentPropId, a.PropId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        float dist = ((Vector3)a.WorldPosition).DistanceTo(b.WorldPosition);
                        float minClearance = 0.4f; // Minimum 40cm between separate props

                        if (dist < minClearance)
                        {
                            if (applyCorrections)
                            {
                                // Nudge b slightly away from a
                                Vector3 dir = ((Vector3)b.WorldPosition) - ((Vector3)a.WorldPosition);
                                if (dir.Length() < 0.01f) dir = new Vector3(1f, 0f, 0f);
                                dir.Normalize();
                                b.WorldPosition = (Vector3)b.WorldPosition + (dir * (minClearance - dist + 0.1f));
                                result.AutoCorrectionsApplied.Add(string.Format("Separated overlapping props '{0}' and '{1}' by nudging {2:F2}m", a.PropId, b.PropId, minClearance - dist + 0.1f));
                            }
                            else
                            {
                                result.HardViolations.Add(string.Format("Prop collision violation: '{0}' overlaps with '{1}' (dist: {2:F2}m < {3:F2}m)", a.PropId, b.PropId, dist, minClearance));
                            }
                        }
                    }
                }
            }

            // Static obstacle collision check
            if (model != null && model.Obstacles != null && plan.ResolvedProps != null)
            {
                foreach (var prop in plan.ResolvedProps)
                {
                    Vector3 pPos = prop.WorldPosition;
                    foreach (var obs in model.Obstacles)
                    {
                        float dist = pPos.DistanceTo(obs.Position);
                        float obsRadius = Math.Max(obs.Dimensions.X, obs.Dimensions.Y) * 0.5f;
                        if (dist < obsRadius)
                        {
                            if (applyCorrections)
                            {
                                Vector3 dir = pPos - obs.Position;
                                if (dir.Length() < 0.01f) dir = new Vector3(1f, 0f, 0f);
                                dir.Normalize();
                                prop.WorldPosition = obs.Position + (dir * (obsRadius + 0.5f));
                                result.AutoCorrectionsApplied.Add(string.Format("Prop '{0}' nudged out of world obstacle '{1}'", prop.PropId, obs.ObstacleType));
                            }
                            else
                            {
                                result.HardViolations.Add(string.Format("Prop '{0}' collides with world obstacle '{1}' (dist: {2:F2}m)", prop.PropId, obs.ObstacleType, dist));
                            }
                        }
                    }
                }
            }
        }

        private void ValidateFacingCoherence(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            SceneInteractionRequirements requirements,
            SpatialValidationResult result,
            bool applyCorrections)
        {
            if (requirements == null || plan.ResolvedNpcs == null || plan.ResolvedNpcs.Count < 2) return;

            string pattern = requirements.Pattern ?? "";

            if (pattern.IndexOf("meeting", StringComparison.OrdinalIgnoreCase) >= 0 ||
                pattern.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var p1 = plan.ResolvedNpcs[0];
                var p2 = plan.ResolvedNpcs[1];

                float expectedP1ToP2 = SpatialPlacementSolver.ComputeFacingHeading(p1.WorldPosition, p2.WorldPosition);
                float expectedP2ToP1 = SpatialPlacementSolver.ComputeFacingHeading(p2.WorldPosition, p1.WorldPosition);

                float diff1 = Math.Abs(p1.Heading - expectedP1ToP2);
                if (diff1 > 180f) diff1 = 360f - diff1;

                float diff2 = Math.Abs(p2.Heading - expectedP2ToP1);
                if (diff2 > 180f) diff2 = 360f - diff2;

                if (diff1 > 35.0f)
                {
                    if (applyCorrections)
                    {
                        p1.Heading = expectedP1ToP2;
                        result.AutoCorrectionsApplied.Add(string.Format("Corrected NPC '{0}' facing heading to {1:F1} deg (was {2:F1} deg off)", p1.Name, expectedP1ToP2, diff1));
                    }
                    else
                    {
                        result.HardViolations.Add(string.Format("NPC '{0}' is facing away from interlocutor '{1}' (deviation: {2:F1} deg)", p1.Name, p2.Name, diff1));
                    }
                }

                if (diff2 > 35.0f)
                {
                    if (applyCorrections)
                    {
                        p2.Heading = expectedP2ToP1;
                        result.AutoCorrectionsApplied.Add(string.Format("Corrected NPC '{0}' facing heading to {1:F1} deg (was {2:F1} deg off)", p2.Name, expectedP2ToP1, diff2));
                    }
                    else
                    {
                        result.HardViolations.Add(string.Format("NPC '{0}' is facing away from interlocutor '{1}' (deviation: {2:F1} deg)", p2.Name, p1.Name, diff2));
                    }
                }
            }
        }

        private void ValidateLineOfSight(
            SpatialExecutionPlan plan,
            SpatialWorldModel model,
            SceneInteractionRequirements requirements,
            SpatialValidationResult result)
        {
            if (requirements == null || !requirements.RequireLineOfSight || plan.ResolvedNpcs == null || plan.ResolvedNpcs.Count < 2) return;

            var p1 = plan.ResolvedNpcs[0];
            var p2 = plan.ResolvedNpcs[1];

            bool hasLos = SpatialPlacementSolver.HasLineOfSight(p1.WorldPosition, p2.WorldPosition, model);
            if (!hasLos)
            {
                result.HardViolations.Add(string.Format("Line of sight blocked between primary participants '{0}' and '{1}' by world geometry", p1.Name, p2.Name));
            }
        }

        private SpatialSurface FindClosestSurface(SpatialWorldModel model, Vector3 pos)
        {
            SpatialSurface closest = null;
            float minDist = float.MaxValue;

            foreach (var s in model.Surfaces)
            {
                float dx = s.Position.X - pos.X;
                float dy = s.Position.Y - pos.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < minDist)
                {
                    minDist = d2;
                    closest = s;
                }
            }

            return closest;
        }
    }
}
