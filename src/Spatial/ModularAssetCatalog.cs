using System;
using System.Collections.Generic;
using System.IO;
using GTA.Math;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Spatial
{
    public class ModularAssetDefinition
    {
        public string Model { get; set; }
        public string Category { get; set; } // Wall, Floor, Door, Ceiling, Table, Seat, Container, Lighting, Prop, VehicleTool, Food, Tech
        public Vector3 Dimensions { get; set; } // Length, Width, Height
        public bool Walkable { get; set; }
        public bool BlocksNavMesh { get; set; }
        public bool CanAttachChildren { get; set; }
        public float SurfaceElevation { get; set; } // e.g. 0.76 for standard tables
        public List<string> Tags { get; set; }

        public ModularAssetDefinition()
        {
            Dimensions = new Vector3(1f, 1f, 1f);
            Tags = new List<string>();
        }

        public ModularAssetDefinition(
            string model, string category, Vector3 dimensions,
            bool walkable, bool blocksNavMesh, bool canAttachChildren, float surfaceElev,
            params string[] tags)
        {
            Model = model;
            Category = category;
            Dimensions = dimensions;
            Walkable = walkable;
            BlocksNavMesh = blocksNavMesh;
            CanAttachChildren = canAttachChildren;
            SurfaceElevation = surfaceElev;
            Tags = new List<string>(tags ?? new string[0]);
        }
    }

    /// <summary>
    /// Data-driven Prop Provider. Loads prop definitions from external JSON manifests
    /// (DynamicWorldEngineData/props.json), ensuring zero hardcoded prop lists in C# code.
    /// </summary>
    /// <summary>
    /// Unified Modular Asset Catalog Adapter.
    /// Delegates all prop queries directly to the unified SpatialAssetRegistry,
    /// ensuring zero duplicated catalogs or separate authority sources in the engine.
    /// </summary>
    public static class ModularAssetCatalog
    {
        public static void LoadManifest()
        {
            // No-op: SpatialAssetRegistry is the single source of truth
        }

        public static List<ModularAssetDefinition> GetAllAssets()
        {
            var list = new List<ModularAssetDefinition>();
            foreach (var asset in DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.AllAssets)
            {
                if (asset.Type == DynamicWorldEngine.Database.AssetType.Prop)
                {
                    list.Add(ToModularAssetDefinition(asset));
                }
            }
            return list;
        }

        public static ModularAssetDefinition GetAssetByName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName)) return null;
            var asset = DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.GetAsset(modelName);
            if (asset != null)
            {
                return ToModularAssetDefinition(asset);
            }
            return null;
        }

        public static ModularAssetDefinition FindBestAsset(string category, List<string> styleTags)
        {
            var query = new DynamicWorldEngine.Database.AssetQuery
            {
                Type = DynamicWorldEngine.Database.AssetType.Prop,
                Category = category,
                RequiredTags = styleTags ?? new List<string>()
            };

            var res = DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.Resolve(query);
            if (res != null && res.PrimaryAsset != null)
            {
                return ToModularAssetDefinition(res.PrimaryAsset);
            }

            // Fallback to any prop in category if available, otherwise first prop in registry
            foreach (var asset in DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.AllAssets)
            {
                if (asset.Type == DynamicWorldEngine.Database.AssetType.Prop &&
                    string.Equals(asset.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    return ToModularAssetDefinition(asset);
                }
            }

            foreach (var asset in DynamicWorldEngine.Database.SpatialAssetRegistry.Instance.AllAssets)
            {
                if (asset.Type == DynamicWorldEngine.Database.AssetType.Prop)
                {
                    return ToModularAssetDefinition(asset);
                }
            }

            return null;
        }

        public static ModularAssetDefinition ToModularAssetDefinition(DynamicWorldEngine.Database.GtaAssetDefinition asset)
        {
            if (asset == null) return null;

            return new ModularAssetDefinition(
                asset.ModelName,
                asset.Category ?? "Prop",
                asset.Dimensions,
                asset.Walkable,
                asset.BlocksNavMesh,
                asset.CanAttachChildren,
                asset.SurfaceElevation,
                asset.Tags != null ? asset.Tags.ToArray() : new string[0]
            );
        }
    }
}
