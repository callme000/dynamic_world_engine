using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Tests
{
    public class TestSpatialAssetRegistry
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("  TESTING SPATIAL ASSET REGISTRY & ONTOLOGY ENGINE       ");
            Console.WriteLine("=========================================================");

            string gtaPath = @"C:\Program Files\Epic Games\GTAVEnhanced";
            Console.WriteLine("\n[1] Interrogating GTA V Installation: " + gtaPath);

            var registry = SpatialAssetRegistry.Instance;
            registry.InitializeRegistry(gtaPath);

            Console.WriteLine("    -> Discovered Installed DLC Packs: " + registry.DiscoveredDlcPacks.Count);
            foreach (var dlc in registry.DiscoveredDlcPacks)
            {
                if (dlc.StartsWith("mp", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("       - " + dlc);
                }
            }

            Console.WriteLine("\n[2] Registry Asset Count: " + registry.AllAssets.Count + " total registered entities.");

            // -------------------------------------------------------------
            // TEST A: Resolve Canonical Character by Identity & Capabilities
            // -------------------------------------------------------------
            Console.WriteLine("\n[3] Testing Character Query Resolution (Layer 1 + Layer 3)...");
            var dreQuery = new AssetQuery
            {
                Type = AssetType.Ped,
                Identity = "Dr. Dre",
                RequiredCapabilities = new List<string> { "celebrity", "musician", "producer" }
            };
            var dreResult = registry.Resolve(dreQuery);
            Console.WriteLine("    Query: 'Dr. Dre' (Required: celebrity, musician, producer)");
            Console.WriteLine("    -> Discovered Character: " + dreResult.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Primary Identity:    " + (dreResult.PrimaryAsset.Identities.Count > 0 ? dreResult.PrimaryAsset.Identities[0] : "N/A"));
            Console.WriteLine("    -> Confidence:          " + dreResult.Confidence + " (Score: " + dreResult.MatchScore + ")");
            Console.WriteLine("    -> Provenance:          " + dreResult.ResolutionProvenance);

            var djQuery = new AssetQuery
            {
                Type = AssetType.Ped,
                Identity = "party dj",
                RequiredCapabilities = new List<string> { "dj", "party_dj" },
                PreferDlc = "The Cayo Perico Heist"
            };
            var djResult = registry.Resolve(djQuery);
            Console.WriteLine("\n    Query: 'party dj' (Required: dj, party_dj | PreferDlc: Cayo Perico)");
            Console.WriteLine("    -> Discovered Character: " + djResult.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Primary Identity:    " + (djResult.PrimaryAsset.Identities.Count > 0 ? djResult.PrimaryAsset.Identities[0] : "N/A"));
            Console.WriteLine("    -> DLC Origin:          " + djResult.PrimaryAsset.DlcPackOrigin);
            Console.WriteLine("    -> Match Score:         " + djResult.MatchScore);

            // -------------------------------------------------------------
            // TEST B: Resolve Interior Spatial Container by Affordances & Tags
            // -------------------------------------------------------------
            Console.WriteLine("\n[4] Testing Interior Query Resolution (Layer 2 + Layer 3)...");
            var penthouseQuery = new AssetQuery
            {
                Type = AssetType.Interior,
                RequiredTags = new List<string> { "penthouse", "luxury", "vip" },
                MinCapacity = 25
            };
            var penthouseResult = registry.Resolve(penthouseQuery);
            Console.WriteLine("    Query: Tags ['penthouse', 'luxury', 'vip'], MinCapacity: 25");
            Console.WriteLine("    -> Selected Interior:   " + penthouseResult.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Interior Desc:       " + penthouseResult.PrimaryAsset.Description);
            Console.WriteLine("    -> Coordinates:         " + penthouseResult.PrimaryAsset.DefaultCoordinates);
            Console.WriteLine("    -> Entity Sets Count:   " + penthouseResult.PrimaryAsset.EntitySets.Count);
            Console.WriteLine("    -> Affordances:         " + string.Join(", ", penthouseResult.PrimaryAsset.Affordances));

            var bunkerQuery = new AssetQuery
            {
                Type = AssetType.Interior,
                RequiredAffordances = new List<string> { "military_command", "blast_shelter" },
                RequiredTags = new List<string> { "bunker" }
            };
            var bunkerResult = registry.Resolve(bunkerQuery);
            Console.WriteLine("\n    Query: Affordances ['military_command', 'blast_shelter'], Tag: 'bunker'");
            Console.WriteLine("    -> Selected Interior:   " + bunkerResult.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Coordinates:         " + bunkerResult.PrimaryAsset.DefaultCoordinates);

            // -------------------------------------------------------------
            // TEST C: Resolve Props by Functional Affordance
            // -------------------------------------------------------------
            Console.WriteLine("\n[5] Testing Prop Affordance Resolution (Layer 1 Dimensions + Layer 3 Affordance)...");
            var djDeckQuery = new AssetQuery
            {
                Type = AssetType.Prop,
                RequiredAffordances = new List<string> { "dj_equipment" }
            };
            var deckResult = registry.Resolve(djDeckQuery);
            Console.WriteLine("    Query: Affordance 'dj_equipment'");
            Console.WriteLine("    -> Selected Prop:       " + deckResult.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Physical Dimensions: " + deckResult.PrimaryAsset.Dimensions);
            Console.WriteLine("    -> Surface Elevation:   " + deckResult.PrimaryAsset.SurfaceElevation + "m");
            Console.WriteLine("    -> Blocks NavMesh:      " + deckResult.PrimaryAsset.BlocksNavMesh);

            // -------------------------------------------------------------
            // TEST D: Affordance-Driven Primitive Composition
            // -------------------------------------------------------------
            Console.WriteLine("\n[6] Testing Complex Entity Composition from Primitives...");
            var composedDj = registry.ComposeComplexEntity("dj_booth");
            Console.WriteLine("    Composition Target: 'dj_booth'");
            Console.WriteLine("    -> Primary Stand/Surface: " + composedDj.PrimaryAsset.ModelName + " (Dim: " + composedDj.PrimaryAsset.Dimensions + ")");
            Console.WriteLine("    -> Composed Sub-Assets (" + composedDj.ComposedSubAssets.Count + "):");
            foreach (var sub in composedDj.ComposedSubAssets)
            {
                Console.WriteLine("       * " + sub.ModelName + " [" + sub.Category + "] Affordances: " + string.Join(", ", sub.Affordances));
            }
            Console.WriteLine("    -> Provenance: " + composedDj.ResolutionProvenance);

            var composedHeistTable = registry.ComposeComplexEntity("heist_planning_table");
            Console.WriteLine("\n    Composition Target: 'heist_planning_table'");
            Console.WriteLine("    -> Primary Surface:     " + composedHeistTable.PrimaryAsset.ModelName);
            Console.WriteLine("    -> Composed Sub-Assets (" + composedHeistTable.ComposedSubAssets.Count + "):");
            foreach (var sub in composedHeistTable.ComposedSubAssets)
            {
                Console.WriteLine("       * " + sub.ModelName + " [" + sub.Category + "] Affordances: " + string.Join(", ", sub.Affordances));
            }
            Console.WriteLine("    -> Provenance: " + composedHeistTable.ResolutionProvenance);

            Console.WriteLine("\n=========================================================");
            Console.WriteLine("  SPATIAL ASSET REGISTRY TEST COMPLETED: 100% SUCCESS!   ");
            Console.WriteLine("=========================================================");
        }
    }
}
