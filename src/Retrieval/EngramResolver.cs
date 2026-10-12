using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Spatial;
using GTA.Math;

namespace DynamicWorldEngine.Retrieval
{
    public class EngramResolutionItem
    {
        public string AssetType { get; set; } // "Prop", "Vehicle", "Ped"
        public string OriginalDescriptor { get; set; }
        public string ResolvedModelName { get; set; }
        public bool IsExactMatch { get; set; }
        public float Confidence { get; set; }
        public float MatchScore { get; set; }
        public string Provenance { get; set; }

        public EngramResolutionItem()
        {
            AssetType = "Prop";
            OriginalDescriptor = string.Empty;
            ResolvedModelName = string.Empty;
            Provenance = string.Empty;
        }

        public override string ToString()
        {
            return string.Format("[{0}] \"{1}\" -> \"{2}\" ({3}, Conf: {4:F2})",
                AssetType, OriginalDescriptor, ResolvedModelName, 
                IsExactMatch ? "Passthrough" : "Resolved", Confidence);
        }
    }

    public class EngramResolutionReport
    {
        public int TotalProcessed { get; set; }
        public int ResolvedCount { get; set; }
        public int PassthroughCount { get; set; }
        public int FallbackCount { get; set; }
        public List<EngramResolutionItem> Items { get; set; }
        public List<string> UnresolvedAssets { get; set; }

        public EngramResolutionReport()
        {
            Items = new List<EngramResolutionItem>();
            UnresolvedAssets = new List<string>();
        }

        public void AddItem(EngramResolutionItem item)
        {
            if (item == null) return;
            Items.Add(item);
            TotalProcessed++;
            if (item.IsExactMatch) PassthroughCount++;
            else if (item.Confidence > 0.4f) ResolvedCount++;
            else FallbackCount++;
        }
    }

    /// <summary>
    /// The Engram Resolver (Law 3: Static catalogs are queried, not reasoned about).
    /// Decouples LLM cognition from static GTA asset dictionaries and model hashes.
    /// Intercepts semantic descriptors from AI generation (or hallucinated model codes)
    /// and deterministically resolves them against SpatialAssetRegistry and ModularAssetCatalog.
    /// </summary>
    public static class EngramResolver
    {
        private static readonly HashSet<string> KnownCommonVehicles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "police", "police2", "police3", "police4", "polmav", "sheriff", "sheriff2",
            "fbi", "fbi2", "pranger", "riot", "firetruk", "ambulance",
            "adder", "zentorno", "osiris", "t20", "turismor", "comet2", "banshee", "elegy2", "jester", "sultan",
            "baller", "baller2", "cavalcade", "dubsta", "dubsta2", "granger", "huntley", "landstalker", "rocoto", "seminole",
            "cognoscenti", "cognoscenti2", "superd", "windsor", "windsor2", "stretch",
            "buzzard", "buzzard2", "annihilator", "savage", "cargobob", "valkyrie", "maverick", "frogger",
            "titan", "cargoplane", "luxor", "shamal", "cuban800", "mammatus", "duster", "velum", "lazer", "hydra",
            "submersible", "submersible2", "avisa", "kosatka",
            "dinghy", "dinghy2", "marquis", "jetmax", "speeder", "squalo", "tropic", "tug",
            "daemon", "bagger", "hexer", "innovation", "sovereign", "sanchez", "sanchez2", "bati", "akuma", "ruffian",
            "insurgent", "insurgent2", "barracks", "barracks2", "crusader", "halftrack", "rhino",
            "mule", "mule2", "burrito", "burrito2", "burrito3", "pony", "rumpo", "speedo", "speedo2", "youga"
        };

        private static string[] TokenizeDescriptor(string descriptor)
        {
            if (string.IsNullOrEmpty(descriptor)) return new string[0];
            string clean = descriptor.Trim().ToLowerInvariant();

            // Strip typical GTA model prefixes
            string[] prefixes = new string[] { "prop_", "bkr_prop_", "xm_prop_", "ba_prop_", "h4_prop_", "vw_prop_", "gr_prop_", "hei_prop_", "v_", "v_serv_", "v_res_" };
            foreach (var pfx in prefixes)
            {
                if (clean.StartsWith(pfx))
                {
                    clean = clean.Substring(pfx.Length);
                    break;
                }
            }

            // Strip common version/variant suffixes like _01, _01a, 01, etc.
            clean = Regex.Replace(clean, @"_0[1-9][a-z]?$", "");
            clean = Regex.Replace(clean, @"_0[1-9]$", "");

            // Split by separator characters
            string[] rawTokens = clean.Split(new char[] { '_', '-', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return rawTokens;
        }

        /// <summary>
        /// Checks whether a model string represents a known valid prop in GTA V manifests.
        /// </summary>
        public static bool IsKnownPropModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return false;
            var reg = SpatialAssetRegistry.Instance;
            if (reg != null)
            {
                var asset = reg.GetAsset(modelName);
                if (asset != null && asset.Type == AssetType.Prop) return true;
            }

            var allModular = ModularAssetCatalog.GetAllAssets();
            if (allModular != null && allModular.Exists(a => string.Equals(a.Model, modelName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a model string represents a known valid vehicle in GTA V manifests.
        /// </summary>
        public static bool IsKnownVehicleModel(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return false;
            if (KnownCommonVehicles.Contains(modelName)) return true;

            var reg = SpatialAssetRegistry.Instance;
            if (reg != null)
            {
                var asset = reg.GetAsset(modelName);
                if (asset != null && asset.Type == AssetType.Vehicle) return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a single prop model or semantic descriptor to an authentic GTA V prop model.
        /// </summary>
        public static EngramResolutionItem ResolvePropModel(string rawModel, string contextPrompt = "")
        {
            var item = new EngramResolutionItem
            {
                AssetType = "Prop",
                OriginalDescriptor = rawModel ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(rawModel))
            {
                item.ResolvedModelName = "prop_box_wood02a";
                item.Confidence = 0.1f;
                item.Provenance = "Default fallback (empty input)";
                return item;
            }

            string clean = rawModel.Trim();

            // 1. Check if model is ALREADY a valid, registered GTA asset (Passthrough)
            if (IsKnownPropModel(clean))
            {
                item.ResolvedModelName = clean;
                item.IsExactMatch = true;
                item.Confidence = 1.0f;
                item.MatchScore = 100f;
                item.Provenance = "Exact Passthrough (Curated Asset)";
                return item;
            }

            // 2. Semantic Resolution via SpatialAssetRegistry
            string[] tokens = TokenizeDescriptor(clean);
            var reg = SpatialAssetRegistry.Instance;
            if (reg != null && tokens.Length > 0)
            {
                var query = new AssetQuery
                {
                    Type = AssetType.Prop,
                    Identity = clean,
                    RawQuery = clean + " " + (contextPrompt ?? ""),
                    RequiredTags = new List<string>(tokens)
                };

                // Semantic Affordance & Category hints
                bool isTable = tokens.Any(t => t.Contains("table") || t.Contains("desk") || t.Contains("counter") || t.Contains("surface"));
                bool isSeat = tokens.Any(t => t.Contains("chair") || t.Contains("seat") || t.Contains("stool") || t.Contains("bench") || t.Contains("couch") || t.Contains("sofa"));
                bool isContainer = tokens.Any(t => t.Contains("crate") || t.Contains("box") || t.Contains("case") || t.Contains("barrel") || t.Contains("container") || t.Contains("ammo"));
                bool isLight = tokens.Any(t => t.Contains("light") || t.Contains("lamp") || t.Contains("spotlight") || t.Contains("worklight"));
                bool isLoot = tokens.Any(t => t.Contains("cash") || t.Contains("gold") || t.Contains("money") || t.Contains("briefcase") || t.Contains("duffel") || t.Contains("loot"));
                bool isElectronics = tokens.Any(t => t.Contains("laptop") || t.Contains("computer") || t.Contains("screen") || t.Contains("monitor") || t.Contains("radio") || t.Contains("server"));

                if (isTable)
                {
                    query.Category = "Table";
                    query.RequiredAffordances.Add("can_be_surface");
                }
                else if (isSeat)
                {
                    query.Category = "Seat";
                    query.RequiredAffordances.Add("supports_npc_seating");
                }
                else if (isContainer)
                {
                    query.Category = "Container";
                }
                else if (isLight)
                {
                    query.Category = "Lighting";
                }

                if (isLoot)
                {
                    query.RequiredAffordances.Add("heist_loot");
                }
                if (isElectronics)
                {
                    query.RequiredTags.Add("electronics");
                }

                var res = reg.Resolve(query);
                if (res != null && res.PrimaryAsset != null && res.MatchScore > 10f)
                {
                    item.ResolvedModelName = res.PrimaryAsset.ModelName;
                    item.Confidence = res.PrimaryAsset.SemanticConfidenceScore;
                    item.MatchScore = res.MatchScore;
                    item.Provenance = string.Format("Engram Resolution: {0}", res.ResolutionProvenance);
                    return item;
                }

                // 3. Fallback Heuristics based on dominant category token
                if (isTable)
                {
                    item.ResolvedModelName = "prop_table_03b";
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Table";
                    return item;
                }
                if (isSeat)
                {
                    item.ResolvedModelName = "bkr_prop_biker_chair_01";
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Seat";
                    return item;
                }
                if (isContainer)
                {
                    item.ResolvedModelName = clean.Contains("ammo") ? "prop_box_ammo04a" : (clean.Contains("weapon") ? "prop_box_guncase_01a" : "prop_box_wood02a");
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Container";
                    return item;
                }
                if (isLight)
                {
                    item.ResolvedModelName = "prop_worklight_03a";
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Lighting";
                    return item;
                }
                if (isLoot)
                {
                    item.ResolvedModelName = clean.Contains("gold") ? "prop_gold_vault_pile" : (clean.Contains("duffel") ? "prop_cash_case_01" : "prop_cash_pile_02");
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Loot";
                    return item;
                }
                if (isElectronics)
                {
                    item.ResolvedModelName = clean.Contains("laptop") ? "prop_laptop_01a" : "xm_prop_x17_screens_01a";
                    item.Confidence = 0.7f;
                    item.Provenance = "Category Fallback: Electronics";
                    return item;
                }
            }

            // 4. Ultimate graceful fallback: safe wooden crate
            item.ResolvedModelName = "prop_box_wood02a";
            item.Confidence = 0.3f;
            item.Provenance = "Safe Fallback: Generic Prop";
            return item;
        }

        /// <summary>
        /// Resolves a single vehicle model or semantic vehicle class into an authentic GTA V vehicle model.
        /// </summary>
        public static EngramResolutionItem ResolveVehicleModel(string rawModel, string contextPrompt = "")
        {
            var item = new EngramResolutionItem
            {
                AssetType = "Vehicle",
                OriginalDescriptor = rawModel ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(rawModel))
            {
                item.ResolvedModelName = "police";
                item.Confidence = 0.2f;
                item.Provenance = "Default Fallback (empty input)";
                return item;
            }

            string clean = rawModel.Trim().ToLowerInvariant();

            // 1. Check if model is ALREADY a valid, known GTA vehicle (Passthrough)
            if (IsKnownVehicleModel(clean))
            {
                item.ResolvedModelName = clean;
                item.IsExactMatch = true;
                item.Confidence = 1.0f;
                item.MatchScore = 100f;
                item.Provenance = "Exact Passthrough (Known GTA Vehicle)";
                return item;
            }

            // 2. Semantic Class Resolution
            string[] tokens = clean.Split(new char[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Any(t => t.Contains("police") || t.Equals("cop") || t.Equals("cops") || t.Contains("cruiser") || t.Contains("patrol") || t.Contains("enforcement")))
            {
                item.ResolvedModelName = "police";
                item.Confidence = 0.9f;
                item.Provenance = "Semantic Class: Law Enforcement";
                return item;
            }

            if (tokens.Any(t => t.Contains("ambulance") || t.Contains("paramedic") || t.Contains("medic") || t.Contains("ems")))
            {
                item.ResolvedModelName = "ambulance";
                item.Confidence = 0.9f;
                item.Provenance = "Semantic Class: Emergency Medical";
                return item;
            }

            if (tokens.Any(t => t.Contains("fire") || t.Contains("firetruck")))
            {
                item.ResolvedModelName = "firetruk";
                item.Confidence = 0.9f;
                item.Provenance = "Semantic Class: Fire Department";
                return item;
            }

            if (tokens.Any(t => t.Contains("helicopter") || t.Contains("chopper") || t.Contains("helo") || t.Contains("rotorcraft")))
            {
                item.ResolvedModelName = "buzzard";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Helicopter";
                return item;
            }

            if (tokens.Any(t => t.Contains("plane") || t.Contains("airplane") || t.Contains("jet") || t.Contains("aircraft") || t.Contains("aviation")))
            {
                item.ResolvedModelName = clean.Contains("jet") ? "luxor" : "cuban800";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Airplane";
                return item;
            }

            if (tokens.Any(t => t.Contains("sub") || t.Contains("submarine") || t.Contains("submersible") || t.Contains("underwater")))
            {
                item.ResolvedModelName = clean.Contains("kosatka") ? "kosatka" : "submersible";
                item.Confidence = 0.9f;
                item.Provenance = "Semantic Class: Submersible";
                return item;
            }

            if (tokens.Any(t => t.Contains("boat") || t.Contains("yacht") || t.Contains("dinghy") || t.Contains("speedboat") || t.Contains("ship") || t.Contains("maritime")))
            {
                item.ResolvedModelName = clean.Contains("yacht") ? "marquis" : "dinghy";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Watercraft";
                return item;
            }

            if (tokens.Any(t => t.Contains("motorcycle") || t.Contains("bike") || t.Contains("biker") || t.Contains("moto")))
            {
                item.ResolvedModelName = "daemon";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Motorcycle";
                return item;
            }

            if (tokens.Any(t => t.Contains("supercar") || t.Contains("sports") || t.Contains("race") || t.Contains("racing") || t.Contains("tuner") || t.Contains("drift")))
            {
                item.ResolvedModelName = "comet2";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Sports/Supercar";
                return item;
            }

            if (tokens.Any(t => t.Contains("luxury") || t.Contains("limo") || t.Contains("limousine") || t.Contains("vip") || t.Contains("executive") || t.Contains("sedan")))
            {
                item.ResolvedModelName = "cognoscenti";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Luxury Sedan";
                return item;
            }

            if (tokens.Any(t => t.Contains("military") || t.Contains("army") || t.Contains("tank") || t.Contains("armored") || t.Contains("swat") || t.Contains("insurgent")))
            {
                item.ResolvedModelName = clean.Contains("tank") ? "rhino" : "insurgent";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Military/Armored";
                return item;
            }

            if (tokens.Any(t => t.Contains("truck") || t.Contains("van") || t.Contains("cargo") || t.Contains("delivery") || t.Contains("transport")))
            {
                item.ResolvedModelName = clean.Contains("van") ? "burrito3" : "mule";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: Commercial Transport";
                return item;
            }

            if (tokens.Any(t => t.Contains("suv") || t.Contains("offroad") || t.Contains("4x4") || t.Contains("jeep")))
            {
                item.ResolvedModelName = "baller";
                item.Confidence = 0.85f;
                item.Provenance = "Semantic Class: SUV/Off-Road";
                return item;
            }

            // 3. Fallback: standard city sedan/police
            item.ResolvedModelName = "police";
            item.Confidence = 0.4f;
            item.Provenance = "Default Vehicle Fallback";
            return item;
        }

        /// <summary>
        /// Intercepts a parsed BuildEnvironmentData object and resolves all prop and vehicle model
        /// fields from semantic descriptors into validated, canonical GTA V asset models.
        /// </summary>
        public static EngramResolutionReport ResolveAllAssets(BuildEnvironmentData data, string contextPrompt = "")
        {
            var report = new EngramResolutionReport();
            if (data == null) return report;

            // 1. Resolve Props
            if (data.Props != null && data.Props.Count > 0)
            {
                foreach (var prop in data.Props)
                {
                    if (prop == null) continue;
                    var res = ResolvePropModel(prop.Model, contextPrompt);
                    prop.Model = res.ResolvedModelName;
                    report.AddItem(res);
                    if (res.Confidence < 0.4f)
                    {
                        report.UnresolvedAssets.Add(string.Format("Prop: {0}", res.OriginalDescriptor));
                    }
                }
            }

            // 2. Resolve Vehicles
            if (data.Vehicles != null && data.Vehicles.Count > 0)
            {
                foreach (var veh in data.Vehicles)
                {
                    if (veh == null) continue;
                    var res = ResolveVehicleModel(veh.Model, contextPrompt);
                    veh.Model = res.ResolvedModelName;
                    report.AddItem(res);
                    if (res.Confidence < 0.4f)
                    {
                        report.UnresolvedAssets.Add(string.Format("Vehicle: {0}", res.OriginalDescriptor));
                    }
                }
            }

            return report;
        }
    }
}
