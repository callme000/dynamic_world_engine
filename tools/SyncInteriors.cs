using System;
using System.Collections.Generic;
using System.IO;
using GTA.Math;
using Newtonsoft.Json;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Discovery;

namespace DynamicWorldEngine.Tools
{
    /// <summary>
    /// Standalone utility to discover all GTA V interiors dynamically from the game installation,
    /// classify them autonomously via SemanticClassificationEngine, and export or sync
    /// the results to DynamicWorldEngineData/interiors.json as a readable offline snapshot.
    /// </summary>
    public class SyncInteriors
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  DYNAMIC WORLD ENGINE - INTERIOR SYNC & DISCOVERY TOOL  ");
            Console.WriteLine("=========================================================");

            string gtaPath = args.Length > 0 ? args[0] : null;
            var install = GtaInstallationLocator.LocateInstallation(gtaPath);

            if (install == null || !install.IsInstalled)
            {
                Console.WriteLine("[ERROR] Could not locate GTA V installation!");
                return;
            }

            Console.WriteLine("[INFO] Discovered GTA V installation: {0}", install.RootPath);

            var provider = new DynamicInteriorDiscoveryProvider();
            provider.Initialize(SpatialAssetRegistry.Instance, install.RootPath);

            var classifier = new SemanticClassificationEngine();
            var interiors = new List<GtaAssetDefinition>();

            var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in provider.ProvideAssets())
            {
                // Run semantic classification to derive categories, tags, and affordances
                classifier.Enrich(asset);
                interiors.Add(asset);

                string cat = !string.IsNullOrEmpty(asset.Category) ? asset.Category : "uncategorized";
                if (!categoryCounts.ContainsKey(cat)) categoryCounts[cat] = 0;
                categoryCounts[cat]++;
            }

            Console.WriteLine("\n[SUCCESS] Dynamically Discovered {0} Interiors from GTA V installation!", interiors.Count);
            Console.WriteLine("\nBreakdown by Category:");
            foreach (var kvp in categoryCounts)
            {
                Console.WriteLine("  - {0,-15}: {1}", kvp.Key, kvp.Value);
            }

            // Export to DynamicWorldEngineData/interiors.json
            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DynamicWorldEngineData");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, "interiors.json");

            // Convert to human-readable JSON format
            var jsonList = new List<Dictionary<string, object>>();
            foreach (var a in interiors)
            {
                var entry = new Dictionary<string, object>
                {
                    { "InteriorId", a.ModelName },
                    { "Name", a.Name },
                    { "Category", a.Category },
                    { "Description", !string.IsNullOrEmpty(a.Description) ? a.Description : string.Format("Dynamically discovered {0} venue.", a.Category) },
                    { "CapacityNpcs", a.Capacity },
                    { "SupportsVehicles", a.SupportsVehicles },
                    { "SemanticTags", a.Tags },
                    { "Keywords", a.Identities },
                    { "IplNames", a.IplDependencies },
                    { "EntranceCoords", new { X = a.EntranceCoords.HasValue ? a.EntranceCoords.Value.X : a.DefaultCoordinates.X, Y = a.EntranceCoords.HasValue ? a.EntranceCoords.Value.Y : a.DefaultCoordinates.Y, Z = a.EntranceCoords.HasValue ? a.EntranceCoords.Value.Z : a.DefaultCoordinates.Z } },
                    { "InteriorCoords", new { X = a.DefaultCoordinates.X, Y = a.DefaultCoordinates.Y, Z = a.DefaultCoordinates.Z } },
                    { "InteriorHeading", a.DefaultHeading }
                };
                jsonList.Add(entry);
            }

            string jsonOutput = JsonConvert.SerializeObject(jsonList, Formatting.Indented);
            File.WriteAllText(targetPath, jsonOutput);

            Console.WriteLine("\n[INFO] Synchronized snapshot written to: {0}", targetPath);
            Console.WriteLine("=========================================================\n");
        }
    }
}
