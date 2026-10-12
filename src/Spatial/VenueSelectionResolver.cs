using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    public class VenueResolutionResult
    {
        public Vector3 CenterPosition { get; set; }
        public float Heading { get; set; }
        public bool IsSkyPocket { get; set; }
        public InteriorDefinition MatchedInterior { get; set; }
        public PocketTransition PlannedPocket { get; set; }
        public List<PocketTransition> ConnectedPockets { get; set; }
        public List<ArchitecturalLayerDecomposition> ArchitecturalLayers { get; set; }

        public VenueResolutionResult()
        {
            ConnectedPockets = new List<PocketTransition>();
            ArchitecturalLayers = new List<ArchitecturalLayerDecomposition>();
        }
    }

    /// <summary>
    /// Dedicated service for interior venue matching, multi-tier sky pocket stack resolution,
    /// and ontological separation of Location Ownership vs. Asset Origin.
    /// </summary>
    public static class VenueSelectionResolver
    {
        public static VenueResolutionResult ResolveVenue(
            BuildEnvironmentData data,
            Vector3 playerOrigin,
            float playerHeading,
            string fullQuery)
        {
            var result = new VenueResolutionResult();
            result.Heading = playerHeading;

            bool isMultiTierStack = (data.ArchitecturalLayers != null && data.ArchitecturalLayers.Count > 0);

            if (data.IsSkyPocket || isMultiTierStack)
            {
                result.IsSkyPocket = true;
                float alt = data.SkyPocketAltitude > 0 ? data.SkyPocketAltitude : 1500.0f;
                result.CenterPosition = new Vector3(playerOrigin.X, playerOrigin.Y, alt);

                if (isMultiTierStack)
                {
                    result.ArchitecturalLayers = new List<ArchitecturalLayerDecomposition>(data.ArchitecturalLayers);

                    for (int i = 0; i < result.ArchitecturalLayers.Count; i++)
                    {
                        var layer = result.ArchitecturalLayers[i];
                        if (layer.WorldCoordinates == null) layer.WorldCoordinates = new Vector3Offset();

                        if (layer.ResolutionType == "sky_pocket" || layer.LayerIndex == 1)
                        {
                            if (layer.WorldCoordinates.X == 0f && layer.WorldCoordinates.Y == 0f)
                            {
                                layer.WorldCoordinates.X = result.CenterPosition.X;
                                layer.WorldCoordinates.Y = result.CenterPosition.Y;
                                layer.WorldCoordinates.Z = result.CenterPosition.Z;
                            }
                        }
                        else
                        {
                            GtaAssetDefinition targetInt = null;
                            if (!string.IsNullOrEmpty(layer.AssignedInteriorId))
                            {
                                targetInt = SpatialAssetRegistry.Instance.GetAsset(layer.AssignedInteriorId);
                            }

                            if (targetInt == null && !string.IsNullOrEmpty(layer.TargetConcept))
                            {
                                var matchRes = SpatialAssetRegistry.Instance.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = layer.TargetConcept });
                                if (matchRes != null && matchRes.PrimaryAsset != null)
                                {
                                    targetInt = matchRes.PrimaryAsset;
                                    layer.AssignedInteriorId = targetInt.ModelName;
                                }
                            }

                            if (targetInt != null && layer.WorldCoordinates.X == 0f && layer.WorldCoordinates.Y == 0f)
                            {
                                layer.WorldCoordinates.X = targetInt.DefaultCoordinates.X;
                                layer.WorldCoordinates.Y = targetInt.DefaultCoordinates.Y;
                                layer.WorldCoordinates.Z = targetInt.DefaultCoordinates.Z;
                            }
                        }
                    }

                    // Dynamically generate connecting pocket transitions linking each layer to the next
                    string envId = string.IsNullOrEmpty(data.EnvironmentId) ? "env_pocket" : data.EnvironmentId;
                    for (int i = 0; i < result.ArchitecturalLayers.Count - 1; i++)
                    {
                        var layerA = result.ArchitecturalLayers[i];
                        var layerB = result.ArchitecturalLayers[i + 1];

                        Vector3 entrance = new Vector3(layerA.WorldCoordinates.X - 3.0f, layerA.WorldCoordinates.Y, layerA.WorldCoordinates.Z);
                        Vector3 exit = new Vector3(layerB.WorldCoordinates.X, layerB.WorldCoordinates.Y, layerB.WorldCoordinates.Z);
                        Vector3 ret = new Vector3(layerA.WorldCoordinates.X, layerA.WorldCoordinates.Y, layerA.WorldCoordinates.Z);

                        string portalTitle = !string.IsNullOrEmpty(layerA.TransitionPortalTitle) ? layerA.TransitionPortalTitle : ("Airlock Transition to " + layerB.LayerName);
                        var pocket = new PocketTransition(
                            string.Format("{0}_p{1}", envId, i + 1),
                            envId,
                            entrance,
                            exit,
                            ret,
                            portalTitle,
                            false, true, "Security Clearance Alpha"
                        );
                        result.ConnectedPockets.Add(pocket);
                        if (i == 0) result.PlannedPocket = pocket;
                    }
                }
            }
            else
            {
                // Generic Interior Resolution via SpatialAssetRegistry
                var tags = new List<string>();
                if (data.SpatialRequirements != null)
                {
                    if (data.SpatialRequirements.RequiredTags != null) tags.AddRange(data.SpatialRequirements.RequiredTags);
                    if (!string.IsNullOrEmpty(data.SpatialRequirements.Atmosphere)) tags.Add(data.SpatialRequirements.Atmosphere);
                    if (!string.IsNullOrEmpty(data.SpatialRequirements.Privacy)) tags.Add(data.SpatialRequirements.Privacy);
                }

                bool vehiclesStagedOutside = fullQuery.Contains("outside") || fullQuery.Contains("valet") || fullQuery.Contains("perimeter") || fullQuery.Contains("entrance");
                bool reqVehiclesInside = !vehiclesStagedOutside && ((data.SpatialRequirements != null && data.SpatialRequirements.RequiredTags != null && (data.SpatialRequirements.RequiredTags.Contains("vehicle_staging") || data.SpatialRequirements.RequiredTags.Contains("supports_vehicles") || data.SpatialRequirements.RequiredTags.Contains("exhibition") || data.SpatialRequirements.RequiredTags.Contains("garage") || data.SpatialRequirements.RequiredTags.Contains("chop_shop"))) || fullQuery.Contains("vehicle exhibition") || fullQuery.Contains("car meet"));

                // Venue Location Query: Distinguish Location Ownership from Asset Origin!
                // If the prompt transfers assets "from <donor>" into a host environment,
                // query the HOST venue and NOT the donor origin.
                string venueQuery = (data.Name + " " + data.Archetype + " " + (data.Description ?? "") + " " + (data.OriginalPrompt ?? "") + " " + (fullQuery ?? "")).ToLowerInvariant();
                if (data.AssetTransferAudit != null && !string.IsNullOrEmpty(data.AssetTransferAudit.HostEnvironment))
                {
                    venueQuery = (data.AssetTransferAudit.HostEnvironment + " " + venueQuery).ToLowerInvariant();
                }
                else
                {
                    var donorMatch = Regex.Match(venueQuery, @"\b(?:from|borrowed from|stolen from|originating from)\s+(?:the\s+)?([a-z0-9_ -]+)", RegexOptions.IgnoreCase);
                    if (donorMatch.Success)
                    {
                        string donorPhrase = donorMatch.Groups[0].Value;
                        venueQuery = venueQuery.Replace(donorPhrase, "");
                    }
                }

                var intQuery = new AssetQuery
                {
                    Type = AssetType.Interior,
                    RawQuery = venueQuery,
                    RequiredTags = tags,
                    MinCapacity = data.SpatialRequirements != null ? data.SpatialRequirements.MinCapacity : 0,
                    RequiresVehicleSupport = reqVehiclesInside ? (bool?)true : null
                };

                var resolvedInt = SpatialAssetRegistry.Instance.Resolve(intQuery);
                bool isOpenWorldPrivacy = (data.SpatialRequirements != null && (data.SpatialRequirements.Privacy == "open_public" || data.SpatialRequirements.Privacy == "open_world"));

                if (resolvedInt != null && resolvedInt.PrimaryAsset != null)
                {
                    bool queryExplicitlyRequestsIndoor = fullQuery.Contains("interior") || fullQuery.Contains("indoor") || fullQuery.Contains("inside") ||
                                                         fullQuery.Contains("bunker") || fullQuery.Contains("vault") || fullQuery.Contains("hangar") ||
                                                         fullQuery.Contains("basement") || fullQuery.Contains("cellar") || fullQuery.Contains("warehouse") ||
                                                         fullQuery.Contains("laboratory") || fullQuery.Contains("facility") || fullQuery.Contains("office") ||
                                                         fullQuery.Contains("penthouse") || fullQuery.Contains("station") || fullQuery.Contains("precinct");

                    if (isOpenWorldPrivacy)
                    {
                        bool isExplicitIdentityMatch = resolvedInt.PrimaryAsset.MatchesIdentity(fullQuery) && resolvedInt.MatchScore >= 50f;
                        bool isOutdoorLocation = resolvedInt.PrimaryAsset.HasTag("outdoor") ||
                                                 resolvedInt.PrimaryAsset.HasTag("open_world") ||
                                                 resolvedInt.PrimaryAsset.HasTag("landmark");
                        bool isOutdoorLocationRequested = isOutdoorLocation && resolvedInt.MatchScore >= 40f && resolvedInt.PrimaryAsset.MatchesIdentity(fullQuery);

                        if (queryExplicitlyRequestsIndoor || isExplicitIdentityMatch || isOutdoorLocationRequested)
                        {
                            result.MatchedInterior = InteriorCatalog.GetInteriorById(resolvedInt.PrimaryAsset.ModelName);
                        }
                    }
                    else
                    {
                        if (resolvedInt.MatchScore >= 20f)
                        {
                            result.MatchedInterior = InteriorCatalog.GetInteriorById(resolvedInt.PrimaryAsset.ModelName);
                        }
                    }
                }

                if (result.MatchedInterior != null)
                {
                    result.CenterPosition = result.MatchedInterior.InteriorCoords;
                    result.Heading = result.MatchedInterior.InteriorHeading;
                }
                else
                {
                    float fwdDist = (data.CenterOffset != null && data.CenterOffset.Y > 0) ? data.CenterOffset.Y : 3.5f;
                    Vector3 forward = new Vector3(-(float)Math.Sin(playerHeading * Math.PI / 180f), (float)Math.Cos(playerHeading * Math.PI / 180f), 0f);
                    result.CenterPosition = SpatialQueryService.FindClearSpawnPosition(playerOrigin, forward, fwdDist, 3.0f);
                }
            }

            return result;
        }
    }
}
