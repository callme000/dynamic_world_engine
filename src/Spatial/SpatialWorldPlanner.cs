using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Retrieval;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Pure 3D spatial layout planner.
    /// Orchestrates venue resolution, feasibility verification, transform hierarchies, and crowd direction.
    /// </summary>
    public class SpatialWorldPlanner
    {
        public SpatialWorldPlanner()
        {
        }

        [Obsolete("SpatialWorldPlanner no longer takes runtime managers. Use parameterless constructor.")]
        public SpatialWorldPlanner(
            NavMeshBlockerManager navBlockerManager,
            BlipManager blipManager,
            TeleportPocketManager pocketManager,
            InteriorManager interiorManager)
        {
        }

        public SpatialExecutionPlan PlanEnvironment(
            BuildEnvironmentData data,
            Vector3 playerOrigin,
            float playerHeading)
        {
            if (data == null) throw new ArgumentNullException("data");

            SpatialExecutionPlan plan = new SpatialExecutionPlan();
            plan.EnvironmentId = string.IsNullOrEmpty(data.EnvironmentId) ? ("env_" + Guid.NewGuid().ToString().Substring(0, 8)) : data.EnvironmentId;
            plan.Name = string.IsNullOrEmpty(data.Name) ? "Planned Environment" : data.Name;
            plan.Archetype = string.IsNullOrEmpty(data.Archetype) ? "custom" : data.Archetype;
            plan.Heading = playerHeading;
            plan.ScenarioDirector = data.ScenarioDirector;

            // 0. Pass Audits from payload if present
            string fullQuery = ((data.OriginalPrompt ?? "") + " " + data.Name + " " + data.Archetype + " " + (data.Description ?? "")).ToLowerInvariant();
            if (data.SpatialFeasibilityAudit != null) plan.FeasibilityAudit = data.SpatialFeasibilityAudit;
            if (data.CharacterResolutionAudit != null) plan.CharacterAudit = data.CharacterResolutionAudit;
            if (data.AssetTransferAudit != null) plan.AssetTransferAudit = data.AssetTransferAudit;

            // 1. Resolve Location & Typology via dedicated VenueSelectionResolver
            var venueRes = VenueSelectionResolver.ResolveVenue(data, playerOrigin, playerHeading, fullQuery);
            Vector3 centerPos = venueRes.CenterPosition;
            plan.CenterPosition = centerPos;
            plan.Heading = venueRes.Heading;
            plan.IsSkyPocket = venueRes.IsSkyPocket;
            plan.MatchedInterior = venueRes.MatchedInterior;
            plan.PlannedPocket = venueRes.PlannedPocket;
            plan.ConnectedPockets = venueRes.ConnectedPockets;
            plan.ArchitecturalLayers = venueRes.ArchitecturalLayers;

            // 2. Generic Volumetric Feasibility Verification via SpatialFeasibilityEvaluator
            SpatialFeasibilityEvaluator.EvaluateFeasibility(plan, data, centerPos);

            // 2.5. Deterministic Engram Asset Resolution (Law 3: Static catalogs are queried, not reasoned about)
            plan.EngramReport = EngramResolver.ResolveAllAssets(data, fullQuery);

            // 2.7. Orientation-First Spatial Placement Solving (Level 4 Spatial World Model & Solver)
            string detectedPattern = null;
            if (fullQuery.Contains("meeting") || fullQuery.Contains("interrogat") || fullQuery.Contains("deal") || fullQuery.Contains("president"))
            {
                detectedPattern = "FaceToFaceMeeting";
            }
            else if (fullQuery.Contains("bonfire") || fullQuery.Contains("campfire") || fullQuery.Contains("party"))
            {
                detectedPattern = "CircularGathering";
            }

            if (detectedPattern != null)
            {
                try
                {
                    var sampler = new SpatialGridSampler();
                    var model = sampler.SampleArea(centerPos, 50.0f, SpatialGridSampler.DefaultCellSize, null, data.Name);
                    plan.WorldModel = model;

                    var solver = new SpatialPlacementSolver();
                    var req = new SceneInteractionRequirements
                    {
                        Pattern = detectedPattern,
                        ParticipantCount = data.Npcs != null ? data.Npcs.Count : 2
                    };
                    var solution = solver.Solve(model, req);
                    plan.SceneSolution = solution;

                    if (solution != null && solution.AnchorCenter != Vector3.Zero && plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        centerPos = solution.AnchorCenter;
                        plan.CenterPosition = centerPos;
                        plan.Heading = solution.AnchorHeading;
                    }

                        // Auto-populate props from solver if scene data has no props
                        if (solution != null && solution.Props.Count > 0 && (data.Props == null || data.Props.Count == 0))
                        {
                            if (data.Props == null) data.Props = new List<PropPlacementData>();
                            foreach (var sp in solution.Props)
                            {
                                data.Props.Add(new PropPlacementData
                                {
                                    PropId = sp.PropId,
                                    Model = sp.Model,
                                    Offset = new Vector3Offset(sp.Position.X - centerPos.X, sp.Position.Y - centerPos.Y, sp.Position.Z - centerPos.Z),
                                    Heading = sp.Rotation.Z
                                });
                            }
                        }
                    }
                    catch {}
                }

            // 3. Resolve Props (Pass 1: Root Props, Pass 2: Child & Surface Attached Props)
            Dictionary<string, CalculatedTransform> computedTransforms = new Dictionary<string, CalculatedTransform>(StringComparer.OrdinalIgnoreCase);

            if (plan.IsSkyPocket && plan.ArchitecturalLayers.Count == 0)
            {
                string floorModel = "prop_metal_plates01";
                var floorAsset = SpatialAssetRegistry.Instance.GetAsset(floorModel);
                if (floorAsset != null)
                {
                    data.Props.Insert(0, new PropPlacementData
                    {
                        PropId = plan.EnvironmentId + "_sky_floor",
                        Model = floorModel,
                        Offset = new Vector3Offset(0, 0, -0.05f),
                        Heading = 0f,
                        IsDynamic = false
                    });
                }
            }

            if (data.Props != null && data.Props.Count > 0)
            {
                // Pass 1: Root Props
                foreach (var p in data.Props)
                {
                    if (string.IsNullOrEmpty(p.ParentPropId))
                    {
                        float lx = p.Offset != null ? p.Offset.X : 0f;
                        float ly = p.Offset != null ? p.Offset.Y : 0f;
                        float lz = p.Offset != null ? p.Offset.Z : 0f;

                        CalculatedTransform tf = PropPlacementEngine.LocalToWorldFullTransform(
                            centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                            lx, ly, lz,
                            p.Heading, p.Pitch, p.Roll
                        );

                        if (!string.IsNullOrEmpty(p.PropId))
                        {
                            computedTransforms[p.PropId] = tf;
                        }

                        var assetDef = ModularAssetCatalog.GetAllAssets().Find(a => string.Equals(a.Model, p.Model, StringComparison.OrdinalIgnoreCase));
                        bool blocksNav = p.IsNavBlocker || (assetDef != null && assetDef.BlocksNavMesh);
                        Vector3 dim = assetDef != null ? assetDef.Dimensions : new Vector3(1f, 1f, 1f);

                        plan.ResolvedProps.Add(new ResolvedPropPlacement
                        {
                            PropId = p.PropId,
                            ParentPropId = "",
                            ModelName = p.Model,
                            WorldPosition = new Vector3(tf.X, tf.Y, tf.Z),
                            WorldRotation = new Vector3(tf.Pitch, tf.Roll, tf.Heading),
                            IsDynamic = p.IsDynamic,
                            BlocksNavMesh = blocksNav,
                            Dimensions = dim
                        });

                        if (blocksNav)
                        {
                            plan.NavBlockers.Add(new WallNavBlockerDefinition(
                                new Vector3(tf.X, tf.Y, tf.Z),
                                new Vector3(dim.Y, dim.X, dim.Z),
                                tf.Heading
                            ));
                        }
                    }
                }

                // Pass 2: Child & Surface Attached Props
                foreach (var p in data.Props)
                {
                    if (!string.IsNullOrEmpty(p.ParentPropId))
                    {
                        CalculatedTransform parentTf = null;
                        computedTransforms.TryGetValue(p.ParentPropId, out parentTf);

                        float lx = p.Offset != null ? p.Offset.X : 0f;
                        float ly = p.Offset != null ? p.Offset.Y : 0f;
                        float lz = p.Offset != null ? p.Offset.Z : 0f;

                        CalculatedTransform tf = (parentTf != null)
                            ? HierarchicalTransformEngine.ComputeChildWorldTransform(parentTf, lx, ly, lz, p.Heading, p.Pitch, p.Roll)
                            : PropPlacementEngine.LocalToWorldFullTransform(centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f, lx, ly, lz, p.Heading, p.Pitch, p.Roll);

                        if (!string.IsNullOrEmpty(p.PropId))
                        {
                            computedTransforms[p.PropId] = tf;
                        }

                        var assetDef = ModularAssetCatalog.GetAllAssets().Find(a => string.Equals(a.Model, p.Model, StringComparison.OrdinalIgnoreCase));
                        bool blocksNav = p.IsNavBlocker || (assetDef != null && assetDef.BlocksNavMesh);
                        Vector3 dim = assetDef != null ? assetDef.Dimensions : new Vector3(0.5f, 0.5f, 0.5f);

                        plan.ResolvedProps.Add(new ResolvedPropPlacement
                        {
                            PropId = p.PropId,
                            ParentPropId = p.ParentPropId,
                            ModelName = p.Model,
                            WorldPosition = new Vector3(tf.X, tf.Y, tf.Z),
                            WorldRotation = new Vector3(tf.Pitch, tf.Roll, tf.Heading),
                            IsDynamic = p.IsDynamic,
                            BlocksNavMesh = blocksNav,
                            Dimensions = dim
                        });
                    }
                }
            }

            // 4. Resolve NPCs
            if (data.Npcs != null)
            {
                foreach (var n in data.Npcs)
                {
                    float lx = n.Offset != null ? n.Offset.X : 0f;
                    float ly = n.Offset != null ? n.Offset.Y : 0f;
                    float lz = n.Offset != null ? n.Offset.Z : 0f;

                    CalculatedTransform tf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, lz,
                        n.Heading, 0f, 0f
                    );

                    Vector3 npcPos = new Vector3(tf.X, tf.Y, tf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        npcPos = SpatialQueryService.FindGroundPosition(npcPos);
                    }

                    string pedModel = string.IsNullOrEmpty(n.PedModel) ? "a_m_y_beach_01" : n.PedModel;

                    // Dynamic Character & Ped Model Resolution via SpatialAssetRegistry
                    var charQuery = new AssetQuery
                    {
                        Type = AssetType.Ped,
                        Identity = n.Name,
                        Category = n.Occupation,
                        RequiredCapabilities = new List<string> { n.Occupation, n.HiddenRole },
                        RawQuery = (n.Name ?? "") + " " + (n.Occupation ?? "") + " " + (n.HiddenRole ?? "") + " " + (n.PedModel ?? "") + " " + fullQuery
                    };

                    var charResult = SpatialAssetRegistry.Instance.Resolve(charQuery);
                    if (charResult != null && charResult.PrimaryAsset != null && charResult.MatchScore > 30f)
                    {
                        bool isGenericPlaceholder = string.IsNullOrEmpty(n.PedModel) || n.PedModel == "a_m_y_beach_01" || n.PedModel.StartsWith("generic") || n.PedModel.Contains("dj");
                        if (isGenericPlaceholder || charResult.Confidence == KnowledgeConfidence.CuratedCanonical)
                        {
                            pedModel = charResult.PrimaryAsset.ModelName;
                            if ((string.IsNullOrEmpty(n.Name) || n.Name.StartsWith("npc_") || n.Name.Equals("DJ", StringComparison.OrdinalIgnoreCase)) && charResult.PrimaryAsset.Identities.Count > 0)
                            {
                                n.Name = charResult.PrimaryAsset.Identities[0];
                            }
                            if ((string.IsNullOrEmpty(n.Occupation) || n.Occupation.Equals("DJ", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(charResult.PrimaryAsset.Category))
                            {
                                n.Occupation = charResult.PrimaryAsset.Category;
                            }

                            if (plan.CharacterAudit == null && charResult.Confidence == KnowledgeConfidence.CuratedCanonical && charResult.PrimaryAsset.Identities.Count > 0)
                            {
                                plan.CharacterAudit = new CharacterResolutionAudit
                                {
                                    RequestedQuery = data.OriginalPrompt ?? fullQuery,
                                    RequestedRole = n.Occupation ?? "Canonical Lore Character",
                                    ResolvedCharacterName = charResult.PrimaryAsset.Identities[0],
                                    ResolvedPedModel = charResult.PrimaryAsset.ModelName,
                                    CanonicalDlcOrigin = charResult.PrimaryAsset.DlcPackOrigin,
                                    RejectionReason = "Generic pedestrian rejected. Resolved verified canonical asset " + charResult.PrimaryAsset.Identities[0] + ".",
                                    VerificationSummary = "AUTHENTIC GTA ONLINE CANONICAL ASSET CONFIRMED"
                                };
                            }
                        }
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = n.EntityId,
                        Name = n.Name,
                        Occupation = n.Occupation,
                        PublicFaction = n.PublicFaction,
                        HiddenRole = n.HiddenRole,
                        PedModel = pedModel,
                        WorldPosition = npcPos,
                        Heading = tf.Heading,
                        Scenario = n.Scenario,
                        Weapon = n.Weapon,
                        InitialTask = n.InitialTask ?? "",
                        InitialState = string.IsNullOrEmpty(n.InitialState) ? "CALM" : n.InitialState,
                        Relationships = n.Relationships ?? new Dictionary<string, int>(),
                        InitialEvents = n.InitialEvents ?? new List<InitialEventData>()
                    });
                }

                // Affordance-Aware Living Crowd Scaling via ProceduralCrowdDirector
                ProceduralCrowdDirector.PopulateCrowd(plan, data, centerPos, fullQuery);
            }

            // 5. Configure Epistemic Access Gating & Pockets
            bool isLocked = false;
            bool isKnown = true;
            string lockReason = "Locked";
            string reqSecret = "";
            string reqFaction = "";

            if (data.SpatialRequirements != null && data.SpatialRequirements.AccessGating != null)
            {
                var gating = data.SpatialRequirements.AccessGating;
                isLocked = gating.IsLocked;
                isKnown = gating.IsKnownToPlayer;
                lockReason = gating.LockReason ?? "Locked";
                reqSecret = gating.RequiredSecret ?? "";
                reqFaction = gating.RequiredFaction ?? "";
            }

            if (data.IsSkyPocket && plan.PlannedPocket == null)
            {
                Vector3 streetCoords = playerOrigin;
                Vector3 interiorCoords = centerPos;
                Vector3 interiorExitCoords = centerPos + new Vector3(2.5f, 0, 0);

                plan.PlannedPocket = new PocketTransition(
                    plan.EnvironmentId,
                    plan.EnvironmentId,
                    streetCoords,
                    interiorCoords,
                    interiorExitCoords,
                    string.IsNullOrEmpty(data.EntranceTitle) ? plan.Name : data.EntranceTitle,
                    isLocked,
                    isKnown,
                    lockReason
                );
                plan.PlannedPocket.RequiredSecret = reqSecret;
                plan.PlannedPocket.RequiredFaction = reqFaction;
            }

            // 6. Resolve Vehicles
            if (data.Vehicles != null && data.Vehicles.Count > 0)
            {
                foreach (var v in data.Vehicles)
                {
                    float lx = v.Offset != null ? v.Offset.X : 0f;
                    float ly = v.Offset != null ? v.Offset.Y : 0f;
                    float lz = v.Offset != null ? v.Offset.Z : 0f;

                    CalculatedTransform tf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, lz,
                        v.Heading, 0f, 0f
                    );

                    Vector3 vehPos = new Vector3(tf.X, tf.Y, tf.Z);
                    string vModel = (v.Model ?? "").ToLowerInvariant();
                    bool isWaterVehicle = IsBoatOrWatercraft(vModel);
                    bool isCenterpieceDisplay = (v.VehicleId ?? "").ToLowerInvariant().Contains("centerpiece") || 
                                                (plan.FeasibilityAudit != null && plan.FeasibilityAudit.ResolutionStrategy.Contains("CENTERPIECE"));

                    if (isCenterpieceDisplay && vModel.Equals("kosatka"))
                    {
                        vModel = "avisa";
                    }

                    bool vehiclesStagedOutside = fullQuery.Contains("outside") || fullQuery.Contains("valet") || fullQuery.Contains("perimeter") || fullQuery.Contains("entrance");

                    if (plan.MatchedInterior != null)
                    {
                        if (isCenterpieceDisplay)
                        {
                            vehPos = new Vector3(tf.X, tf.Y, tf.Z);
                        }
                        else if (plan.MatchedInterior.SupportsVehicles && !isWaterVehicle && !vehiclesStagedOutside)
                        {
                            vehPos = new Vector3(tf.X, tf.Y, tf.Z);
                        }
                        else if (isWaterVehicle && plan.MatchedInterior.WaterArrivalCoords.HasValue)
                        {
                            vehPos = plan.MatchedInterior.WaterArrivalCoords.Value + new Vector3(lx, ly, 0f);
                        }
                        else
                        {
                            vehPos = plan.MatchedInterior.EntranceCoords + new Vector3(lx, ly, 0f);
                            vehPos = SpatialQueryService.FindGroundPosition(vehPos);
                        }
                    }
                    else if (!data.IsSkyPocket)
                    {
                        vehPos = SpatialQueryService.FindGroundPosition(vehPos);
                    }

                    plan.ResolvedVehicles.Add(new ResolvedVehiclePlacement
                    {
                        VehicleId = v.VehicleId,
                        ModelName = string.IsNullOrEmpty(vModel) ? "police" : vModel,
                        WorldPosition = vehPos,
                        Heading = tf.Heading,
                        PrimaryColor = v.PrimaryColor,
                        IsEngineRunning = v.IsEngineRunning,
                        Sirens = v.Sirens,
                        LightsOn = v.LightsOn,
                        DriverEntityId = v.DriverEntityId
                    });
                }
            }

            plan.Atmosphere = data.Atmosphere;

            // 7. Ontological Provenance: Ensure Asset Transfer Audit is fully populated
            if (plan.AssetTransferAudit == null)
            {
                var donorCheck = Regex.Match(data.OriginalPrompt ?? "", @"\b(?:from|stolen from|taken from|borrowed from)\s+(?:the\s+)?([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (donorCheck.Success)
                {
                    string donor = donorCheck.Groups[1].Value.ToUpperInvariant();
                    plan.AssetTransferAudit = new AssetTransferAudit
                    {
                        HostEnvironment = plan.MatchedInterior != null ? plan.MatchedInterior.Name : (!string.IsNullOrEmpty(data.Name) ? data.Name : "Host Venue"),
                        DonorAssetOrigin = donor,
                        RequestedAssetFamily = "Transferred " + donor + " Asset Family",
                        OntologicalDistinction = string.Format("Ontology Separation Verified: Location ownership is strictly '{0}', while asset origin '{1}' informs props.", plan.MatchedInterior != null ? plan.MatchedInterior.Name : "Open Ground", donor),
                        ResolvedVenueInterior = plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "open_world"
                    };
                }
            }

            if (plan.AssetTransferAudit != null)
            {
                if (string.IsNullOrEmpty(plan.AssetTransferAudit.ResolvedVenueInterior))
                {
                    plan.AssetTransferAudit.ResolvedVenueInterior = plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "open_world";
                }
                if (plan.AssetTransferAudit.TransferredAssetModels.Count == 0 && plan.ResolvedProps != null)
                {
                    foreach (var p in plan.ResolvedProps)
                    {
                        if (!plan.AssetTransferAudit.TransferredAssetModels.Contains(p.ModelName))
                        {
                            plan.AssetTransferAudit.TransferredAssetModels.Add(p.ModelName);
                        }
                    }
                }
            }

            // 8. Hard Spatial Validation Gate (Stage 9: Surface contact, clearances, facing coherence)
            try
            {
                var validator = new SpatialPlanValidator();
                SceneInteractionRequirements valReq = null;
                if (detectedPattern != null)
                {
                    valReq = new SceneInteractionRequirements
                    {
                        Pattern = detectedPattern,
                        ParticipantCount = data.Npcs != null ? data.Npcs.Count : 2
                    };
                }
                plan.ValidationResult = validator.ValidatePlan(plan, plan.WorldModel, valReq, true);
            }
            catch {}

            return plan;
        }

        public static bool IsBoatOrWatercraft(string model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            var asset = SpatialAssetRegistry.Instance.GetAsset(model);
            if (asset != null)
            {
                if (asset.HasAffordance("amphibious") ||
                    asset.HasTag("boat") ||
                    asset.HasTag("watercraft") ||
                    asset.HasTag("submersible") ||
                    asset.HasTag("marine"))
                    return true;
            }
            string m = model.ToLowerInvariant();
            return m.Contains("boat") || m.Contains("dinghy") || m.Contains("speeder") ||
                   m.Contains("toro") || m.Contains("longfin") || m.Contains("tropic") ||
                   m.Contains("squalo") || m.Contains("marquis") || m.Contains("seashark") ||
                   m.Contains("jetmax") || m.Contains("predator") || m.Contains("tug") ||
                   m.Contains("submersible") || m.Contains("avisa") || m.Contains("kosatka");
        }

        [Obsolete("Runtime lifecycle cleanup has moved to RuntimeExecutor. This method is maintained as an obsolete stub.")]
        public void CleanUpEnvironmentSpatial(string environmentId)
        {
        }
    }
}
