using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Candidate macro-location discovered from world models, open-world landmarks, or dynamic interiors.
    /// Represents Stage 1 & 2 of the Spatial Orchestration Pipeline.
    /// </summary>
    public class LocationCandidate
    {
        public string LocationId { get; set; }
        public string DisplayName { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public Vector3 Coordinates { get; set; }
        public float Heading { get; set; }
        public float DefaultRadius { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Affordances { get; set; }
        public bool SupportsVehicles { get; set; }
        public int CapacityNpcs { get; set; }
        public bool IsInterior { get; set; }
        public InteriorDefinition MatchedInterior { get; set; }
        public float MatchScore { get; set; }
        public string MatchReason { get; set; }

        public LocationCandidate()
        {
            LocationId = "";
            DisplayName = "";
            Category = "open_world";
            Description = "";
            Coordinates = Vector3.Zero;
            Heading = 0f;
            DefaultRadius = 50.0f;
            Tags = new List<string>();
            Affordances = new List<string>();
            SupportsVehicles = true;
            CapacityNpcs = 30;
            IsInterior = false;
            MatchedInterior = null;
            MatchScore = 0f;
            MatchReason = "";
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}) @ {2} [Score: {3:F1}]", DisplayName, Category, Coordinates, MatchScore);
        }
    }

    /// <summary>
    /// Result of macro-location discovery and selection.
    /// </summary>
    public class LocationSelectionResult
    {
        public LocationCandidate SelectedLocation { get; set; }
        public List<LocationCandidate> RankedCandidates { get; set; }
        public string DetectedArchetype { get; set; }
        public bool IsExplicitTarget { get; set; }

        public LocationSelectionResult()
        {
            RankedCandidates = new List<LocationCandidate>();
            DetectedArchetype = "custom";
            IsExplicitTarget = false;
        }
    }

    /// <summary>
    /// Macro-Location Selection Resolver (Stages 1 & 2 of the Spatial Orchestration Pipeline).
    /// Discovers candidate macro-locations from authentic open-world landmarks, airfields,
    /// cul-de-sacs, and dynamic interiors before any AI scene intent is generated.
    /// Solves the "spawning while blind" architectural gap.
    /// </summary>
    public class LocationSelectionResolver
    {
        public LocationSelectionResult ResolveLocation(
            string prompt,
            Vector3? playerCurrentPosition = null,
            float playerCurrentHeading = 0f,
            BuildEnvironmentData data = null)
        {
            var result = new LocationSelectionResult();
            string fullQuery = (prompt ?? "").ToLowerInvariant();
            if (data != null)
            {
                fullQuery = ((data.OriginalPrompt ?? "") + " " + (data.Name ?? "") + " " + (data.Archetype ?? "") + " " + (data.Description ?? "") + " " + fullQuery).ToLowerInvariant();
            }

            bool explicitIndoor = fullQuery.Contains("indoor") || fullQuery.Contains("interior") || fullQuery.Contains("inside") ||
                                 fullQuery.Contains("bunker") || fullQuery.Contains("vault") || fullQuery.Contains("office") ||
                                 fullQuery.Contains("basement") || fullQuery.Contains("penthouse") || fullQuery.Contains("lab") ||
                                 fullQuery.Contains("cellar") || fullQuery.Contains("warehouse");

            bool explicitOutdoor = fullQuery.Contains("beach") || fullQuery.Contains("outdoor") || fullQuery.Contains("outside") ||
                                  fullQuery.Contains("open air") || fullQuery.Contains("runway") || fullQuery.Contains("airstrip") ||
                                  fullQuery.Contains("alley") || fullQuery.Contains("street") || fullQuery.Contains("quarry") ||
                                  fullQuery.Contains("vineyard") || fullQuery.Contains("dam") || fullQuery.Contains("track");

            List<LocationCandidate> candidates = new List<LocationCandidate>();

            // 1. Interrogate Open-World Landmarks from OpenWorldLocationProvider directly
            try
            {
                var openWorldProvider = new DynamicWorldEngine.Discovery.OpenWorldLocationProvider();
                openWorldProvider.Initialize(SpatialAssetRegistry.Instance, null);
                var locAssets = openWorldProvider.ProvideAssets();
                if (locAssets != null)
                {
                    foreach (var asset in locAssets)
                    {
                        bool isOutdoorLandmark = asset.HasTag("outdoor") || asset.HasTag("open_world") || asset.HasTag("landmark");
                        var cand = new LocationCandidate
                        {
                            LocationId = asset.ModelName,
                            DisplayName = asset.Name,
                            Category = asset.Category,
                            Description = asset.Description,
                            Coordinates = asset.DefaultCoordinates,
                            Heading = asset.DefaultHeading,
                            DefaultRadius = isOutdoorLandmark ? 60.0f : 30.0f,
                            SupportsVehicles = asset.SupportsVehicles,
                            CapacityNpcs = asset.Capacity,
                            IsInterior = !isOutdoorLandmark,
                            Tags = new List<string>(asset.Tags),
                            Affordances = new List<string>(asset.Affordances)
                        };

                        float score = ScoreCandidate(cand, asset.Identities, fullQuery, explicitIndoor, explicitOutdoor, playerCurrentPosition);
                        if (score > 10.0f)
                        {
                            cand.MatchScore = score;
                            candidates.Add(cand);
                        }
                    }
                }
            }
            catch { }

            // 2. Interrogate Open-World Landmarks from SpatialAssetRegistry
            try
            {
                var assets = SpatialAssetRegistry.Instance.AllAssets;
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset.Type == AssetType.Interior)
                        {
                            bool alreadyAdded = false;
                            for (int i = 0; i < candidates.Count; i++)
                            {
                                if (string.Equals(candidates[i].LocationId, asset.ModelName, StringComparison.OrdinalIgnoreCase))
                                {
                                    alreadyAdded = true;
                                    break;
                                }
                            }
                            if (alreadyAdded) continue;

                            bool isOutdoorLandmark = asset.HasTag("outdoor") || asset.HasTag("open_world") || asset.HasTag("landmark");
                            
                            var cand = new LocationCandidate
                            {
                                LocationId = asset.ModelName,
                                DisplayName = asset.Name,
                                Category = asset.Category,
                                Description = asset.Description,
                                Coordinates = asset.DefaultCoordinates,
                                Heading = asset.DefaultHeading,
                                DefaultRadius = isOutdoorLandmark ? 60.0f : 30.0f,
                                SupportsVehicles = asset.SupportsVehicles,
                                CapacityNpcs = asset.Capacity,
                                IsInterior = !isOutdoorLandmark,
                                Tags = new List<string>(asset.Tags),
                                Affordances = new List<string>(asset.Affordances)
                            };

                            float score = ScoreCandidate(cand, asset.Identities, fullQuery, explicitIndoor, explicitOutdoor, playerCurrentPosition);
                            if (score > 10.0f)
                            {
                                cand.MatchScore = score;
                                candidates.Add(cand);
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Interrogate Dynamic Interiors Catalog
            try
            {
                var allInteriors = InteriorCatalog.GetAllInteriors();
                if (allInteriors != null)
                {
                    foreach (var interior in allInteriors)
                    {
                        if (interior == null) continue;
                        bool alreadyAdded = false;
                        for (int i = 0; i < candidates.Count; i++)
                        {
                            if (string.Equals(candidates[i].LocationId, interior.InteriorId, StringComparison.OrdinalIgnoreCase))
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }

                        if (!alreadyAdded)
                        {
                            var cand = new LocationCandidate
                            {
                                LocationId = interior.InteriorId,
                                DisplayName = interior.Name,
                                Category = interior.Category,
                                Description = interior.Atmosphere,
                                Coordinates = interior.InteriorCoords,
                                Heading = interior.InteriorHeading,
                                DefaultRadius = 25.0f,
                                SupportsVehicles = interior.SupportsVehicles,
                                CapacityNpcs = interior.CapacityNpcs,
                                IsInterior = true,
                                MatchedInterior = interior,
                                Tags = new List<string>(interior.SemanticTags)
                            };

                            List<string> ids = new List<string> { interior.Name, interior.InteriorId };
                            float score = ScoreCandidate(cand, ids, fullQuery, explicitIndoor, explicitOutdoor, playerCurrentPosition);
                            if (score > 10.0f)
                            {
                                cand.MatchScore = score;
                                candidates.Add(cand);
                            }
                        }
                    }
                }
            }
            catch {}

            // Sort candidates descending by match score
            candidates.Sort((a, b) => b.MatchScore.CompareTo(a.MatchScore));
            result.RankedCandidates = candidates;

            if (candidates.Count > 0 && candidates[0].MatchScore >= 35.0f)
            {
                result.SelectedLocation = candidates[0];
                result.IsExplicitTarget = candidates[0].MatchScore >= 60.0f;
            }
            else
            {
                // Fallback: If player location is provided, anchor to player's current vicinity
                if (playerCurrentPosition.HasValue && playerCurrentPosition.Value != Vector3.Zero)
                {
                    result.SelectedLocation = new LocationCandidate
                    {
                        LocationId = "loc_player_vicinity",
                        DisplayName = "Player Current Vicinity",
                        Category = "open_world",
                        Description = "Locally anchored context around the active player.",
                        Coordinates = playerCurrentPosition.Value,
                        Heading = playerCurrentHeading,
                        DefaultRadius = 50.0f,
                        SupportsVehicles = true,
                        CapacityNpcs = 20,
                        IsInterior = false,
                        MatchScore = 30.0f,
                        MatchReason = "Default player vicinity anchor"
                    };
                }
                else if (candidates.Count > 0)
                {
                    result.SelectedLocation = candidates[0];
                }
                else
                {
                    // Canonical default: Del Perro Beach
                    result.SelectedLocation = new LocationCandidate
                    {
                        LocationId = "loc_del_perro_beach",
                        DisplayName = "Del Perro Beach Sands",
                        Category = "beach",
                        Description = "Expansive Pacific beachfront sands.",
                        Coordinates = new Vector3(-1500.0f, -1190.0f, 2.5f),
                        Heading = 140.0f,
                        DefaultRadius = 60.0f,
                        SupportsVehicles = true,
                        CapacityNpcs = 40,
                        IsInterior = false,
                        MatchScore = 25.0f,
                        MatchReason = "Canonical open-world baseline"
                    };
                }
            }

            // Populate detected archetype
            if (result.SelectedLocation != null)
            {
                result.DetectedArchetype = result.SelectedLocation.Category;
            }

            return result;
        }

        private float ScoreCandidate(
            LocationCandidate cand,
            List<string> identities,
            string fullQuery,
            bool explicitIndoor,
            bool explicitOutdoor,
            Vector3? playerPosition)
        {
            float score = 0f;
            string locNameLower = cand.DisplayName.ToLowerInvariant();
            string locIdLower = cand.LocationId.ToLowerInvariant();
            string catLower = cand.Category.ToLowerInvariant();

            // 1. Direct Identity Matching (Highest priority: +80)
            if (identities != null)
            {
                foreach (var id in identities)
                {
                    string idLower = id.ToLowerInvariant();
                    if (idLower.Length >= 4 && fullQuery.Contains(idLower))
                    {
                        score += 80.0f;
                        cand.MatchReason = "Direct identity match: " + id;
                        break;
                    }
                }
            }

            if (score < 80.0f)
            {
                if (fullQuery.Contains(locNameLower))
                {
                    score += 75.0f;
                    cand.MatchReason = "Display name match: " + cand.DisplayName;
                }
                else if (fullQuery.Contains(locIdLower.Replace("loc_", "").Replace("_", " ")))
                {
                    score += 65.0f;
                    cand.MatchReason = "Location ID token match";
                }
            }

            // 2. Keyword & Category Matching (+30 to +45)
            if (!string.IsNullOrEmpty(catLower) && fullQuery.Contains(catLower))
            {
                score += 35.0f;
                if (string.IsNullOrEmpty(cand.MatchReason)) cand.MatchReason = "Category match: " + cand.Category;
            }

            if (cand.Tags != null)
            {
                foreach (var tag in cand.Tags)
                {
                    string tagLower = tag.ToLowerInvariant();
                    if (tagLower.Length >= 3 && fullQuery.Contains(tagLower))
                    {
                        score += 15.0f;
                    }
                }
            }

            // 3. Environmental Preference (Indoor vs Outdoor)
            if (cand.IsInterior)
            {
                if (explicitIndoor) score += 30.0f;
                if (explicitOutdoor) score -= 40.0f;
            }
            else
            {
                if (explicitOutdoor) score += 30.0f;
                if (explicitIndoor) score -= 40.0f;
            }

            // 4. Proximity scoring (if player position given and score > 0)
            if (playerPosition.HasValue && playerPosition.Value != Vector3.Zero && cand.Coordinates != Vector3.Zero)
            {
                float dist = playerPosition.Value.DistanceTo(cand.Coordinates);
                if (dist < 150.0f)
                {
                    score += 20.0f; // Player is practically right there
                }
                else if (dist < 500.0f)
                {
                    score += 10.0f;
                }
            }

            return score;
        }
    }
}
