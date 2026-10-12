using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Commands;
using GTA.Math;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Spatial
{
    public class InteriorDefinition
    {
        public string InteriorId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Privacy { get; set; }
        public string Atmosphere { get; set; }
        public string SecurityLevel { get; set; }
        public int CapacityNpcs { get; set; }
        public List<string> SemanticTags { get; set; }
        public string[] Keywords { get; set; }
        public List<string> IplNames { get; set; }
        public List<string> RemoveIplNames { get; set; }
        public SpatialVector3 EntranceCoords { get; set; }
        public SpatialVector3 InteriorCoords { get; set; }
        public float InteriorHeading { get; set; }
        public List<string> EntitySets { get; set; }
        public bool SupportsVehicles { get; set; }
        public SpatialVector3? WaterArrivalCoords { get; set; }

        public InteriorDefinition()
        {
            SemanticTags = new List<string>();
            Keywords = new string[0];
            IplNames = new List<string>();
            RemoveIplNames = new List<string>();
            EntitySets = new List<string>();
        }

        public InteriorDefinition(
            string id, string name, string category, string desc,
            string privacy, string atmosphere, string security, int capacity,
            string[] tags, string[] keywords,
            List<string> ipls, SpatialVector3 entrance, SpatialVector3 interior, float heading,
            List<string> entitySets = null, List<string> removeIpls = null, bool supportsVehicles = false,
            SpatialVector3? waterArrival = null)
        {
            InteriorId = id;
            Name = name;
            Category = category;
            Description = desc;
            Privacy = privacy;
            Atmosphere = atmosphere;
            SecurityLevel = security;
            CapacityNpcs = capacity;
            SemanticTags = new List<string>(tags ?? new string[0]);
            Keywords = keywords ?? new string[0];
            IplNames = ipls ?? new List<string>();
            RemoveIplNames = removeIpls ?? new List<string>();
            EntranceCoords = entrance;
            InteriorCoords = interior;
            InteriorHeading = heading;
            EntitySets = entitySets ?? new List<string>();
            SupportsVehicles = supportsVehicles;
            WaterArrivalCoords = waterArrival;
        }
    }

    /// <summary>
    /// Data-driven Interior Provider. Loads interior definitions from external JSON manifests
    /// (DynamicWorldEngineData/interiors.json), ensuring zero hardcoded interior knowledge in C# code.
    /// </summary>
    /// <summary>
    /// Unified Interior Catalog Adapter.
    /// Delegates all interior queries directly to the unified SpatialAssetRegistry,
    /// ensuring zero duplicated catalogs or separate authority sources in the engine.
    /// </summary>
    public static class InteriorCatalog
    {
        public static void LoadManifest()
        {
            // No-op: SpatialAssetRegistry is the single source of truth
        }

        public static List<InteriorDefinition> GetAllInteriors()
        {
            var list = new List<InteriorDefinition>();
            foreach (var asset in DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.AllAssets)
            {
                if (asset.Type == DynamicWorldEngine.Database.AssetType.Interior)
                {
                    list.Add(ToInteriorDefinition(asset));
                }
            }
            return list;
        }

        public static InteriorDefinition GetInteriorById(string interiorId)
        {
            if (string.IsNullOrEmpty(interiorId)) return null;
            var asset = DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.GetAsset(interiorId);
            if (asset != null && asset.Type == DynamicWorldEngine.Database.AssetType.Interior)
            {
                return ToInteriorDefinition(asset);
            }
            return null;
        }

        public static InteriorDefinition FindBestMatch(string query, SpatialRequirementsData req = null)
        {
            var tags = new List<string>();
            if (req != null)
            {
                if (req.RequiredTags != null) tags.AddRange(req.RequiredTags);
                if (!string.IsNullOrEmpty(req.Atmosphere)) tags.Add(req.Atmosphere);
                if (!string.IsNullOrEmpty(req.Privacy)) tags.Add(req.Privacy);
                if (!string.IsNullOrEmpty(req.SecurityLevel)) tags.Add(req.SecurityLevel);
            }

            var assetQuery = new DynamicWorldEngine.Database.AssetQuery
            {
                Type = DynamicWorldEngine.Database.AssetType.Interior,
                RawQuery = query ?? "",
                RequiredTags = tags,
                MinCapacity = req != null ? req.MinCapacity : 0
            };

            var res = DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.Resolve(assetQuery);
            if (res != null && res.PrimaryAsset != null && res.MatchScore >= 15f)
            {
                return ToInteriorDefinition(res.PrimaryAsset);
            }

            return null;
        }

        public static InteriorDefinition ToInteriorDefinition(DynamicWorldEngine.Database.GtaAssetDefinition asset)
        {
            if (asset == null) return null;

            var def = new InteriorDefinition
            {
                InteriorId = asset.ModelName,
                Name = !string.IsNullOrEmpty(asset.Name) ? asset.Name : asset.ModelName,
                Category = asset.Category,
                Description = asset.Description,
                CapacityNpcs = asset.Capacity,
                SupportsVehicles = asset.SupportsVehicles,
                InteriorCoords = asset.DefaultCoordinates,
                InteriorHeading = asset.DefaultHeading,
                EntranceCoords = asset.EntranceCoords.HasValue ? asset.EntranceCoords.Value : asset.DefaultCoordinates,
                WaterArrivalCoords = asset.WaterArrivalCoords,
                IplNames = new List<string>(asset.IplDependencies ?? new List<string>()),
                RemoveIplNames = new List<string>(asset.RemoveIplDependencies ?? new List<string>()),
                EntitySets = new List<string>(asset.EntitySets ?? new List<string>()),
                SemanticTags = new List<string>(asset.Tags ?? new List<string>())
            };

            return def;
        }
    }
}
