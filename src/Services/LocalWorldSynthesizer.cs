using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Archetypes;
using DynamicWorldEngine.Commands;
using DynamicWorldEngine.Database;
using DynamicWorldEngine.Discovery;
using DynamicWorldEngine.Spatial;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Services
{
    /// <summary>
    /// Pure Offline Heuristic & Procedural World Synthesizer (Astra Local Engine).
    /// Generates complex environments, compound ensembles, motorcades, crowd rules,
    /// and scenario events entirely offline with zero network or external API dependencies.
    /// </summary>
    public class LocalWorldSynthesizer
    {
        private static bool ContainsWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
            return Regex.IsMatch(text, @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase);
        }

        private static bool ContainsAnyWord(string text, params string[] words)
        {
            if (string.IsNullOrEmpty(text) || words == null || words.Length == 0) return false;
            foreach (var w in words)
            {
                if (Regex.IsMatch(text, @"\b" + Regex.Escape(w) + @"\b", RegexOptions.IgnoreCase)) return true;
            }
            return false;
        }

        public string SynthesizeEnvironmentLocally(string prompt)
        {
            return SynthesizeEnvironmentLocally(prompt, false);
        }

        public string SynthesizeEnvironmentLocally(string prompt, bool includeScenarioDirector)
        {
            string rawPrompt = prompt ?? "";
            string p = rawPrompt.ToLowerInvariant();

            string envId = "env_" + Guid.NewGuid().ToString().Substring(0, 8);
            var registry = SpatialAssetRegistry.Instance;
            var decomposer = new SemanticDecomposer(registry);
            var decomposition = decomposer.Decompose(rawPrompt);

            // 1. Identify Atmospheric & Archetype Clues
            string title = "Dynamic Living World";
            string archetype = "custom_scenario";
            string desc = "Custom scenario generated dynamically from prompt: " + rawPrompt;
            string weather = "CLEAR";
            int hour = 14;
            string ambientFx = "none";
            string privacy = "open_public";
            string atmosphereTag = "street_gritty";
            List<string> requiredTags = new List<string>();

            // Thematic Categorization from semantic cues with strict word boundary protection
            bool isBankHeist = ContainsAnyWord(p, "heist", "robbery", "vault", "safecracking", "driller", "drillers");
            bool isOutdoor = ContainsAnyWord(p, "camp", "campers", "camping", "bonfire", "beach", "bbq", "barbecue", "cookout", "forest");
            bool isSciFi = ContainsAnyWord(p, "alien", "ufo", "autopsy", "extraterrestrial", "biohazard", "anomalous", "science");
            bool isRace = ContainsAnyWord(p, "race", "racing", "tuner", "drag", "drift", "speedway") || p.Contains("car meet") || p.Contains("drag strip");
            bool isSmugglerHangar = ContainsAnyWord(p, "hangar", "airstrip", "airfield") || (ContainsWord(p, "smuggler") && (p.Contains("plane") || p.Contains("cargo") || p.Contains("crate") || p.Contains("air")));

            bool isInterrogation = !isBankHeist && (ContainsAnyWord(p, "interrogation", "interogation", "torture", "captive", "detain", "questioning", "inquisition")
                || (ContainsAnyWord(p, "hostage", "hostages") && !isBankHeist)
                || (ContainsAnyWord(p, "undercover", "safehouse", "hideout", "lockup") && (p.Contains("cartel") || p.Contains("gang") || p.Contains("mob") || p.Contains("syndicate") || p.Contains("police") || p.Contains("agent") || p.Contains("investigation") || p.Contains("interrogat"))));

            bool isDrugLab = !isInterrogation && !isSciFi && (ContainsAnyWord(p, "meth", "cocaine", "weed farm", "drug lab", "chemical", "cooks", "narcotics") || (p.Contains("lab") && (p.Contains("cook") || p.Contains("boil") || p.Contains("drug"))));
            bool isEmergency = !isInterrogation && (ContainsAnyWord(p, "medical", "hospital", "ambulance", "paramedic", "paramedics", "clinic", "patient", "patients", "triage", "doctor", "doctors", "surgeon", "surgery", "surgical", "infirmary", "coroner", "emergency", "trauma", "gunshot") || p.Contains("operating room"));
            bool isMilitary = !isDrugLab && !isEmergency && !isRace && (ContainsAnyWord(p, "military", "army", "marine", "checkpoint", "tank", "ordnance") || ContainsWord(p, "war") || (ContainsWord(p, "bunker") && !ContainsAnyWord(p, "meth", "drug", "coke", "weed", "narcotic", "cook", "boil")));
            bool isBiker = !isInterrogation && (ContainsAnyWord(p, "biker", "bikers", "motorcycle", "motorcycles", "chopper", "choppers", "lost mc", "lostmc", "clubhouse", "biker gang"));
            bool isChopShop = !isInterrogation && !isRace && (ContainsAnyWord(p, "chop shop", "salvage", "mechanic", "mechanics", "stripping", "dismantle", "dismantling", "scrapyard", "autoshop") || (p.Contains("welding") && p.Contains("car")));
            bool isPolice = !isInterrogation && !isEmergency && !isMilitary && (ContainsAnyWord(p, "police station", "precinct", "booking desk", "jail", "lockup", "sheriff", "patrol") || (ContainsAnyWord(p, "police", "cop", "cops", "detective", "detectives") && !p.Contains("raid")) || (ContainsAnyWord(p, "officer", "officers") && !isMilitary));
            bool isDockyard = !isInterrogation && !isSmugglerHangar && (ContainsAnyWord(p, "dockyard", "docks", "cargo ship", "longshoreman", "freighter", "harbor", "seaport") || (p.Contains("container") && (p.Contains("shipment") || p.Contains("crane") || p.Contains("smuggl"))));
            bool isGambling = !isInterrogation && !isBiker && !isOutdoor && (ContainsAnyWord(p, "gambling", "poker", "cards", "blackjack", "roulette", "bookmaker", "croupier", "chips") || (p.Contains("high-stakes") && (p.Contains("table") || p.Contains("game") || p.Contains("tournament") || p.Contains("gamble") || p.Contains("bet"))));
            bool isParty = !isInterrogation && !isBiker && !isOutdoor && !isGambling && (ContainsAnyWord(p, "party", "club", "rave", "dance") || ContainsWord(p, "dj") || p.Contains("celebrat"));
            bool isHeist = isBankHeist || ContainsAnyWord(p, "blueprint", "planning");
            bool isCrime = !isInterrogation && !isBiker && !isDrugLab && !isChopShop && !isDockyard && !isMilitary && !isSmugglerHangar && !isGambling && (ContainsAnyWord(p, "cartel", "drug", "coke", "dealer", "gang", "syndicate", "mob", "mafia") || p.Contains("arms deal"));
            bool isLuxury = !isInterrogation && !isBiker && !isOutdoor && !isGambling && (ContainsAnyWord(p, "luxury", "penthouse", "casino", "auction", "mansion", "estate", "villa", "yacht") || ContainsWord(p, "vip") || p.Contains("high-end"));
            bool isArtOrMuseum = !isInterrogation && (ContainsAnyWord(p, "art", "museum", "gallery", "painting", "paintings", "exhibit", "exhibits", "exhibition", "kortz", "sculpture", "sculptures"));

            if (isInterrogation)
            {
                hour = 2;
                weather = "OVERCAST";
                privacy = "hidden_basement";
                atmosphereTag = "dark_interrogation";
                archetype = "covert_interrogation";
                title = "Clandestine Safehouse Interrogation";
                desc = "High-stakes covert interrogation conducted in an underground clandestine room.";
                requiredTags.Add("interrogation");
                requiredTags.Add("torture");
                requiredTags.Add("safehouse");
                requiredTags.Add("basement");
                requiredTags.Add("cartel");
                requiredTags.Add("undercover");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isParty || isLuxury)
            {
                hour = 23;
                weather = "CLEAR";
                ambientFx = isParty ? "club_lights" : "none";
                privacy = "commercial_indoor";
                atmosphereTag = "luxury_vip";

                // Registry-driven venue resolution: discover privacy, atmosphere and tags from matched interior definition
                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 20f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (matchedAsset.HasAffordance("luxury_estate") || matchedAsset.Category == "mansion" || matchedAsset.Category == "yacht") privacy = "luxury_estate";
                    else if (matchedAsset.HasAffordance("subterranean_bunker") || matchedAsset.Category == "bunker") privacy = "subterranean_bunker";
                    else if (matchedAsset.HasAffordance("open_public")) privacy = "open_public";

                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
                else
                {
                    if (p.Contains("penthouse")) requiredTags.Add("penthouse");
                    if (p.Contains("casino")) requiredTags.Add("casino");
                    if (p.Contains("music locker") || p.Contains("underground")) requiredTags.Add("music locker");
                    if (p.Contains("club") || p.Contains("nightclub")) requiredTags.Add("nightclub");
                }

                archetype = (p.Contains("conference") || p.Contains("meeting")) ? "syndicate_summit" : "luxury_celebration";
                title = (p.Contains("conference") || p.Contains("summit")) ? "High-Stakes Criminal Summit" : "Exclusive VIP Gathering";
                desc = (p.Contains("conference") || p.Contains("summit")) ? "High-level syndicate conference convened at an exclusive luxury venue." : "An exclusive luxury gathering with VIP guests, music, and celebration.";
            }
            else if (isArtOrMuseum)
            {
                hour = 14;
                weather = "CLEAR";
                privacy = "commercial_indoor";
                atmosphereTag = "cultural_gallery";
                requiredTags.Add("gallery");
                requiredTags.Add("art");
                requiredTags.Add("museum");
                requiredTags.Add("kortz");
                archetype = "art_exhibition";
                title = "Kortz Center Fine Art Exhibition";
                desc = "Distinguished cultural gallery exhibition with patrons admiring contemporary artworks and sculpture.";

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 20f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isGambling)
            {
                hour = 22;
                weather = "CLEAR";
                privacy = p.Contains("casino") ? "commercial_indoor" : "hidden_basement";
                atmosphereTag = "gambling_den";
                archetype = "underground_gambling";
                title = p.Contains("casino") ? "Diamond Casino High-Roller Suite" : "Underground High-Stakes Gambling Den";
                desc = "High-stakes poker tournament with card tables, piles of chips, and luxury drinks in a clandestine backroom.";
                requiredTags.Add("casino");
                requiredTags.Add("gambling");
                requiredTags.Add("table");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isMilitary)
            {
                hour = 20;
                weather = "OVERCAST";
                privacy = p.Contains("bunker") ? "subterranean_bunker" : "open_public";
                atmosphereTag = "military_secure";
                if (p.Contains("bunker")) requiredTags.Add("bunker");
                if (p.Contains("checkpoint")) requiredTags.Add("checkpoint");
                if (p.Contains("facility")) requiredTags.Add("facility");
                archetype = "military_operations";
                title = "Armed Defense Tactical Operation";
                desc = "Tactical military presence with defensive hardware and security escorts.";
            }
            else if (isHeist)
            {
                bool isBankVault = p.Contains("bank") || p.Contains("vault") || p.Contains("deposit") || p.Contains("gold");
                bool isPlanning = p.Contains("plan") || p.Contains("blueprint") || p.Contains("board");

                if (isBankVault && !isPlanning)
                {
                    hour = 13;
                    weather = "CLEAR";
                    privacy = "secluded_underground";
                    atmosphereTag = "luxury_vip";
                    if (p.Contains("casino"))
                    {
                        requiredTags.Add("casino_vault");
                        title = "The Diamond Casino Vault Strike";
                    }
                    else
                    {
                        requiredTags.Add("bank");
                        requiredTags.Add("bank vault");
                        title = "Pacific Standard Bank Vault Heist";
                    }
                    archetype = "bank_heist";
                    desc = "High-stakes armed tactical bank vault breach with heavy safecracking and cash extraction.";

                    var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                    if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 20f)
                    {
                        var matchedAsset = matchedVenueRes.PrimaryAsset;
                        if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    }
                }
                else
                {
                    hour = 21;
                    weather = "CLEAR";
                    privacy = "commercial_indoor";
                    atmosphereTag = "covert_planning";
                    requiredTags.Add("planning");
                    archetype = "clandestine_planning";
                    title = "Clandestine Syndicate Strategy Operation";
                    desc = "High-stakes tactical planning session coordinating logistics and assets for an upcoming heist.";
                }
            }
            else if (isCrime)
            {
                hour = 22;
                weather = "SMOG";
                privacy = "open_public";
                atmosphereTag = "street_gritty";
                archetype = "contraband_exchange";
                bool isGangDeal = (p.Contains("families") && p.Contains("ballas")) || (p.Contains("arms deal") && (p.Contains("gang") || p.Contains("families") || p.Contains("ballas")));
                title = isGangDeal ? "Families & Ballas Street Contraband Exchange" : "Underground Contraband Syndicate";
                desc = isGangDeal ? "High-tension street-level arms deal between rival gangs under street cover." : "Clandestine illicit transactions taking place under street cover.";
            }
            else if (isRace)
            {
                hour = 23;
                weather = "CLEAR";
                privacy = (p.Contains("car meet") || p.Contains("warehouse")) ? "commercial_indoor" : "open_public";
                atmosphereTag = "street_gritty";
                if (p.Contains("car meet")) requiredTags.Add("car meet");
                archetype = "street_racing_meet";
                title = "Underground High-Octane Meet";
                desc = "High-performance motor enthusiasts and tuned machinery staged for adrenaline battles.";
            }
            else if (isSciFi)
            {
                hour = 23;
                weather = "FOGGY";
                ambientFx = "sparks";
                privacy = "hidden_basement";
                atmosphereTag = "high_tech";
                archetype = "classified_research";
                title = "Classified Anomalous Research Outpost";
                desc = "Covert scientific installation investigating anomalous specimens and technology.";
                requiredTags.Add("laboratory");
                requiredTags.Add("research");
                requiredTags.Add("biohazard");
                requiredTags.Add("science");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 20f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                }
            }
            else if (isEmergency)
            {
                hour = 14;
                weather = "CLEAR";
                privacy = "commercial_indoor";
                atmosphereTag = "emergency_triage";
                archetype = "medical_emergency";
                title = "Emergency Hospital Trauma Center";
                desc = "Urgent medical triage and resuscitation ongoing in the hospital emergency unit.";
                requiredTags.Add("hospital");
                requiredTags.Add("medical");
                requiredTags.Add("triage");
                requiredTags.Add("clinic");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isChopShop)
            {
                hour = 18;
                weather = "OVERCAST";
                privacy = "commercial_indoor";
                atmosphereTag = "industrial_gritty";
                archetype = "chop_shop_salvage";
                title = "Illicit Vehicle Salvage & Chop Shop";
                desc = "Active vehicle dismantling, torch welding, and parts extraction operation in an industrial garage.";
                requiredTags.Add("chop_shop");
                requiredTags.Add("salvage");
                requiredTags.Add("mechanic");
                requiredTags.Add("workshop");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isBiker)
            {
                hour = 22;
                weather = "CLEAR";
                privacy = "commercial_indoor";
                atmosphereTag = "biker_gritty";
                archetype = "biker_clubhouse";
                title = "Lost MC Biker Clubhouse & Bar";
                desc = "Biker brotherhood gathering around billiards, cold beer, and roaring choppers.";
                requiredTags.Add("biker");
                requiredTags.Add("clubhouse");
                requiredTags.Add("motorcycle");
                requiredTags.Add("bar");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isDrugLab)
            {
                hour = 1;
                weather = "OVERCAST";
                privacy = p.Contains("bunker") ? "subterranean_bunker" : (p.Contains("warehouse") ? "commercial_indoor" : "hidden_basement");
                atmosphereTag = "chemical_hazard";
                archetype = "illicit_manufacturing";
                title = "Subterranean Chemical Narcotics Lab";
                desc = "Clandestine chemical synthesis, cook vats, and high-purity contraband packaging under armed guard.";
                requiredTags.Add("meth");
                requiredTags.Add("drug_lab");
                requiredTags.Add("chemical");
                requiredTags.Add("lab");
                if (p.Contains("bunker")) requiredTags.Add("bunker");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isPolice)
            {
                hour = 11;
                weather = "CLEAR";
                privacy = "commercial_indoor";
                atmosphereTag = "police_operations";
                archetype = "police_precinct";
                title = "LSPD Police Station & Booking Desk";
                desc = "Law enforcement officers processing intake suspects and reviewing field intelligence files.";
                requiredTags.Add("police");
                requiredTags.Add("police_station");
                requiredTags.Add("precinct");
                requiredTags.Add("law_enforcement");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isDockyard)
            {
                hour = 23;
                weather = "FOGGY";
                privacy = "open_public";
                atmosphereTag = "harbor_industrial";
                archetype = "dockyard_smuggling";
                title = "Port Terminal Contraband Shipping Operation";
                desc = "Maritime shipping containers and heavy cargo crates staged for clandestine logistics transfer.";
                requiredTags.Add("cargo_ship");
                requiredTags.Add("dockyard");
                requiredTags.Add("shipping");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 20f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                }
            }
            else if (isSmugglerHangar)
            {
                hour = 19;
                weather = "CLEAR";
                privacy = p.Contains("hangar") ? "commercial_indoor" : "open_public";
                atmosphereTag = "airfield_contraband";
                archetype = "smuggler_hangar";
                title = "Cartel Smuggling Airfield & Hangar";
                desc = "Clandestine aviation smuggling operation staging contraband crates and air cargo.";
                requiredTags.Add("hangar");
                requiredTags.Add("airstrip");
                requiredTags.Add("cargo");
                requiredTags.Add("airplane");

                var matchedVenueRes = registry.Resolve(new AssetQuery { Type = AssetType.Interior, RawQuery = rawPrompt });
                if (matchedVenueRes != null && matchedVenueRes.PrimaryAsset != null && matchedVenueRes.MatchScore >= 15f)
                {
                    var matchedAsset = matchedVenueRes.PrimaryAsset;
                    if (!string.IsNullOrEmpty(matchedAsset.Category) && !requiredTags.Contains(matchedAsset.Category)) requiredTags.Add(matchedAsset.Category);
                    if (matchedAsset.Tags != null && matchedAsset.Tags.Count > 0)
                    {
                        foreach (var tag in matchedAsset.Tags)
                        {
                            if (p.Contains(tag.ToLowerInvariant()) && !requiredTags.Contains(tag))
                            {
                                requiredTags.Add(tag);
                            }
                        }
                    }
                }
            }
            else if (isOutdoor)
            {
                hour = 21;
                weather = "EXTRASUNNY";
                ambientFx = "bonfire";
                privacy = "open_public";
                atmosphereTag = "outdoor_leisure";
                archetype = "outdoor_gathering";
                title = "Outdoor Community Social Gathering";
                desc = "Relaxed open-air social gathering around food and warm fires.";
            }

            List<object> props = new List<object>();
            List<object> vehicles = new List<object>();
            List<object> npcs = new List<object>();
            List<object> beats = new List<object>();

            // 2. Discover / Resolve Complex Functional Primitives
            if (isInterrogation)
            {
                // Clandestine Interrogation Setup: Central interrogation chair, examination table, audio tape recorder, weapon case, harsh spotlight
                var chairRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "chair", "seat" } });
                string chairModel = (chairRes != null && chairRes.PrimaryAsset != null) ? chairRes.PrimaryAsset.ModelName : "bkr_prop_biker_chair_01";
                props.Add(new { prop_id = "interrogation_chair", parent_prop_id = "", model = chairModel, offset = new { x = 0.0, y = 2.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });

                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tableModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "interrogation_table", parent_prop_id = "", model = tableModel, offset = new { x = -1.8, y = 3.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var tapeRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "recorder", "electronics" } });
                string tapeModel = (tapeRes != null && tapeRes.PrimaryAsset != null) ? tapeRes.PrimaryAsset.ModelName : "prop_tapeplayer_01";
                props.Add(new { prop_id = "interrogation_recorder", parent_prop_id = "interrogation_table", model = tapeModel, offset = new { x = 0.0, y = -0.2, z = 0.76 }, heading = 0.0, is_dynamic = false });

                var gunCaseRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "weapons", "case", "box" } });
                string gunCaseModel = (gunCaseRes != null && gunCaseRes.PrimaryAsset != null) ? gunCaseRes.PrimaryAsset.ModelName : "prop_box_guncase_01a";
                props.Add(new { prop_id = "interrogation_guncase", parent_prop_id = "interrogation_table", model = gunCaseModel, offset = new { x = 0.0, y = 0.25, z = 0.76 }, heading = 180.0, is_dynamic = false });

                var lightRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "light", "lamp", "worklight" } });
                string lightModel = (lightRes != null && lightRes.PrimaryAsset != null) ? lightRes.PrimaryAsset.ModelName : "prop_worklight_03a";
                props.Add(new { prop_id = "interrogation_spotlight", parent_prop_id = "", model = lightModel, offset = new { x = 0.0, y = 4.2, z = 0.0 }, heading = 0.0, is_dynamic = false });
            }
            else if (p.Contains("dj") || p.Contains("turntable") || p.Contains("music") || (isParty && !isHeist))
            {
                var djGroup = registry.ComposeComplexEntity("dj_booth");
                if (djGroup != null && djGroup.PrimaryAsset != null)
                {
                    string rootId = "dj_booth_stand";
                    props.Add(new { prop_id = rootId, parent_prop_id = "", model = djGroup.PrimaryAsset.ModelName, offset = new { x = 0.0, y = 5.0, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });

                    for (int i = 0; i < djGroup.ComposedSubAssets.Count; i++)
                    {
                        var sub = djGroup.ComposedSubAssets[i];
                        if (sub.HasAffordance("dj_equipment"))
                        {
                            props.Add(new { prop_id = "dj_turntables", parent_prop_id = rootId, model = sub.ModelName, offset = new { x = 0.0, y = 0.0, z = 0.95 }, heading = 0.0, is_dynamic = false });
                        }
                        else if (sub.HasAffordance("audio_projection"))
                        {
                            float sideX = (i % 2 == 0) ? -2.2f : 2.2f;
                            float sideH = (i % 2 == 0) ? 160.0f : 200.0f;
                            props.Add(new { prop_id = "speaker_" + (i + 1), parent_prop_id = "", model = sub.ModelName, offset = new { x = sideX, y = 5.0, z = 0.0 }, heading = sideH, is_dynamic = false, is_nav_blocker = true });
                        }
                    }
                }
            }

            if (p.Contains("doomsday") || p.Contains("command center") || p.Contains("war room"))
            {
                // Dynamic resolution of Doomsday / high-tech command center equipment
                var deskRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "doomsday_desk", "war_room", "command_desk" } });
                string dModel = (deskRes != null && deskRes.PrimaryAsset != null) ? deskRes.PrimaryAsset.ModelName : "xm_prop_x17_desk_cover_01a";
                props.Add(new { prop_id = "doomsday_cmd_desk", parent_prop_id = "", model = dModel, offset = new { x = 0.0, y = 3.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var screenRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "screens", "monitors", "surveillance_wall" } });
                string sModel = (screenRes != null && screenRes.PrimaryAsset != null) ? screenRes.PrimaryAsset.ModelName : "xm_prop_x17_screens_01a";
                props.Add(new { prop_id = "doomsday_screens", parent_prop_id = "doomsday_cmd_desk", model = sModel, offset = new { x = 0.0, y = 0.4, z = 0.85 }, heading = 0.0, is_dynamic = false });
            }
            if (isHeist && (p.Contains("bank") || p.Contains("vault") || p.Contains("deposit") || p.Contains("gold")) && !p.Contains("planning"))
            {
                // Authentic Bank Vault Loot, Cash Crates, Gold Stacks, and Breach Hardware dynamically resolved from registry
                var crateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "cash", "crate" }, RawQuery = rawPrompt });
                string crateModel = (crateRes != null && crateRes.PrimaryAsset != null) ? crateRes.PrimaryAsset.ModelName : "prop_cash_crate_01";

                var goldRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "gold", "vault" }, RawQuery = rawPrompt });
                string goldModel = (goldRes != null && goldRes.PrimaryAsset != null) ? goldRes.PrimaryAsset.ModelName : "prop_gold_vault_pile";

                var duffelRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "heist_loot" }, RawQuery = rawPrompt });
                string duffelModel = (duffelRes != null && duffelRes.PrimaryAsset != null) ? duffelRes.PrimaryAsset.ModelName : "prop_cash_case_01";

                var stackRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "cash_pile", "cash" }, RawQuery = rawPrompt });
                string stackModel = (stackRes != null && stackRes.PrimaryAsset != null) ? stackRes.PrimaryAsset.ModelName : "prop_cash_pile_02";

                var drillRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "welding", "gas", "torch" } });
                string drillModel = (drillRes != null && drillRes.PrimaryAsset != null) ? drillRes.PrimaryAsset.ModelName : "prop_weld_torch";

                // Primary Vault Extraction Core
                props.Add(new { prop_id = "vault_cash_crate", parent_prop_id = "", model = crateModel, offset = new { x = -1.0, y = 3.0, z = 0.0 }, heading = 15.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "vault_gold_pile", parent_prop_id = "", model = goldModel, offset = new { x = 1.0, y = 3.0, z = 0.0 }, heading = 45.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "vault_heist_duffel", parent_prop_id = "vault_cash_crate", model = duffelModel, offset = new { x = 0.0, y = 0.0, z = 0.8 }, heading = 30.0, is_dynamic = false });
                props.Add(new { prop_id = "vault_cash_stack", parent_prop_id = "", model = stackModel, offset = new { x = 0.0, y = 2.5, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Compound Enrichment: Secondary loot crates, floor duffels, and thermal breach equipment
                props.Add(new { prop_id = "vault_reserve_crate", parent_prop_id = "", model = crateModel, offset = new { x = -2.2, y = 3.8, z = 0.0 }, heading = 20.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "vault_floor_duffel", parent_prop_id = "", model = duffelModel, offset = new { x = -0.5, y = 2.2, z = 0.0 }, heading = 75.0, is_dynamic = false });
                props.Add(new { prop_id = "vault_extra_cash", parent_prop_id = "", model = stackModel, offset = new { x = 0.8, y = 2.2, z = 0.0 }, heading = 15.0, is_dynamic = false });
                props.Add(new { prop_id = "vault_thermal_drill_rig", parent_prop_id = "", model = drillModel, offset = new { x = 0.0, y = 4.0, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });
            }
            else if (isHeist || p.Contains("table") || p.Contains("planning"))
            {
                var heistGroup = registry.ComposeComplexEntity("heist_planning_table");
                if (heistGroup != null && heistGroup.PrimaryAsset != null)
                {
                    string rootId = "planning_table";
                    props.Add(new { prop_id = rootId, parent_prop_id = "", model = heistGroup.PrimaryAsset.ModelName, offset = new { x = 0.0, y = 3.0, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                    for (int i = 0; i < heistGroup.ComposedSubAssets.Count; i++)
                    {
                        var sub = heistGroup.ComposedSubAssets[i];
                        float sx = (i == 0) ? 0.0f : (i == 1 ? -0.4f : 0.4f);
                        float sy = (i == 0) ? 0.0f : (i == 1 ? 0.1f : -0.1f);
                        props.Add(new { prop_id = "table_item_" + (i + 1), parent_prop_id = rootId, model = sub.ModelName, offset = new { x = sx, y = sy, z = 0.76 }, heading = (i == 1 ? 90.0 : 0.0), is_dynamic = false });
                    }
                }

                // Add planning whiteboard / map stand and tactical chairs
                var chairRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "chair", "office" } });
                string chairModel = (chairRes != null && chairRes.PrimaryAsset != null) ? chairRes.PrimaryAsset.ModelName : "bkr_prop_biker_chair_01";
                props.Add(new { prop_id = "planning_chair_1", parent_prop_id = "", model = chairModel, offset = new { x = -1.2, y = 3.0, z = 0.0 }, heading = 90.0, is_dynamic = false });
                props.Add(new { prop_id = "planning_chair_2", parent_prop_id = "", model = chairModel, offset = new { x = 1.2, y = 3.0, z = 0.0 }, heading = 270.0, is_dynamic = false });
            }
            else if (!isInterrogation && isCrime && props.Count == 0)
            {
                // Street Contraband Exchange Compound: Deal table, contraband weapon cases, payment stacks, lookouts & pallets
                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "deal_table", parent_prop_id = "", model = tModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var caseRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "weapons", "crate", "box" } });
                string cModel = (caseRes != null && caseRes.PrimaryAsset != null) ? caseRes.PrimaryAsset.ModelName : "prop_box_guncase_01a";
                props.Add(new { prop_id = "contraband_case", parent_prop_id = "deal_table", model = cModel, offset = new { x = -0.3, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                var cashRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "heist_loot" } });
                string cashModel = (cashRes != null && cashRes.PrimaryAsset != null) ? cashRes.PrimaryAsset.ModelName : "prop_cash_case_01";
                props.Add(new { prop_id = "loot_payment", parent_prop_id = "deal_table", model = cashModel, offset = new { x = 0.3, y = 0.0, z = 0.76 }, heading = 30.0, is_dynamic = false });

                var palletRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "pallet", "industrial" } });
                string palletModel = (palletRes != null && palletRes.PrimaryAsset != null) ? palletRes.PrimaryAsset.ModelName : "prop_pallet_01a";
                props.Add(new { prop_id = "deal_pallet", parent_prop_id = "", model = palletModel, offset = new { x = -2.2, y = 2.2, z = 0.0 }, heading = 15.0, is_dynamic = false });

                var crateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "crate", "wood" } });
                string crateModel = (crateRes != null && crateRes.PrimaryAsset != null) ? crateRes.PrimaryAsset.ModelName : "prop_box_wood02a";
                props.Add(new { prop_id = "deal_ammo_crate", parent_prop_id = "", model = crateModel, offset = new { x = 2.2, y = 2.2, z = 0.0 }, heading = 330.0, is_dynamic = false, is_nav_blocker = true });
            }
            else if (isMilitary && !p.Contains("race"))
            {
                // Fortified Military Tactical Compound: 4 Sandbag Revetments, Checkpoint Security Gate, Floodlight Tower, Ammo Depot & Comms Desk
                var sandbagRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "sandbag", "military" } });
                string sModel = (sandbagRes != null && sandbagRes.PrimaryAsset != null) ? sandbagRes.PrimaryAsset.ModelName : "prop_sandbag_01";
                props.Add(new { prop_id = "defense_barrier_l", parent_prop_id = "", model = sModel, offset = new { x = -2.5, y = 2.0, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "defense_barrier_r", parent_prop_id = "", model = sModel, offset = new { x = 2.5, y = 2.0, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "defense_barrier_outer_l", parent_prop_id = "", model = sModel, offset = new { x = -4.5, y = 2.5, z = 0.0 }, heading = 15.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "defense_barrier_outer_r", parent_prop_id = "", model = sModel, offset = new { x = 4.5, y = 2.5, z = 0.0 }, heading = 345.0, is_dynamic = false, is_nav_blocker = true });

                var gateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "security_gate", "military" } });
                string gModel = (gateRes != null && gateRes.PrimaryAsset != null) ? gateRes.PrimaryAsset.ModelName : "prop_sec_barrier_01a";
                props.Add(new { prop_id = "checkpoint_security_gate", parent_prop_id = "", model = gModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                // High-Intensity Tactical Floodlight Tower
                var lightRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "worklight", "generator", "military" } });
                string lightModel = (lightRes != null && lightRes.PrimaryAsset != null) ? lightRes.PrimaryAsset.ModelName : "prop_worklight_02a";
                props.Add(new { prop_id = "military_floodlight_tower", parent_prop_id = "", model = lightModel, offset = new { x = -5.0, y = 4.5, z = 0.0 }, heading = 45.0, is_dynamic = false, is_nav_blocker = true });

                // Military Ordnance & Ammo Depot
                var ammoBoxRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "ammo", "military", "crate" } });
                string ammoModel = (ammoBoxRes != null && ammoBoxRes.PrimaryAsset != null) ? ammoBoxRes.PrimaryAsset.ModelName : "prop_box_ammo04a";
                props.Add(new { prop_id = "military_ammo_crate_1", parent_prop_id = "", model = ammoModel, offset = new { x = 4.0, y = 4.2, z = 0.0 }, heading = 30.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "military_ammo_crate_2", parent_prop_id = "", model = ammoModel, offset = new { x = 4.8, y = 4.0, z = 0.0 }, heading = 45.0, is_dynamic = false });

                // Tactical Field Communications Desk
                var deskRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string deskModel = (deskRes != null && deskRes.PrimaryAsset != null) ? deskRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "military_comms_desk", parent_prop_id = "", model = deskModel, offset = new { x = -3.5, y = 4.2, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var radioRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "radio", "electronics", "comms" } });
                string radioModel = (radioRes != null && radioRes.PrimaryAsset != null) ? radioRes.PrimaryAsset.ModelName : "prop_tapeplayer_01";
                props.Add(new { prop_id = "military_comms_radio", parent_prop_id = "military_comms_desk", model = radioModel, offset = new { x = 0.0, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });
            }

            // Stanchions / Hospitality tables for VIP or Party
            if (!isInterrogation && (isLuxury || isParty) && !isHeist)
            {
                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table", "picnic" } });
                string tModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_picnictable_01";
                props.Add(new { prop_id = "vip_table", parent_prop_id = "", model = tModel, offset = new { x = -4.0, y = 2.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var drinkRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "drinks_service" } });
                string dModel = (drinkRes != null && drinkRes.PrimaryAsset != null) ? drinkRes.PrimaryAsset.ModelName : "ba_prop_club_champ_open";
                props.Add(new { prop_id = "vip_drinks", parent_prop_id = "vip_table", model = dModel, offset = new { x = 0.0, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                // Poolside Loungers & Sun Deck Comfort
                var loungerRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "lounger", "chair", "sun" } });
                string lModel = (loungerRes != null && loungerRes.PrimaryAsset != null) ? loungerRes.PrimaryAsset.ModelName : "prop_chair_05";
                props.Add(new { prop_id = "patio_sun_lounger_1", parent_prop_id = "", model = lModel, offset = new { x = 3.5, y = 3.0, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "patio_sun_lounger_2", parent_prop_id = "", model = lModel, offset = new { x = 3.5, y = 4.5, z = 0.0 }, heading = 270.0, is_dynamic = false });

                // Outdoor Patio Heater
                var heaterRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "heater", "patio", "lamp" } });
                string hModel = (heaterRes != null && heaterRes.PrimaryAsset != null) ? heaterRes.PrimaryAsset.ModelName : "prop_patio_heater_01";
                props.Add(new { prop_id = "patio_heater", parent_prop_id = "", model = hModel, offset = new { x = -4.0, y = 4.0, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Secondary Cocktail Stand with Ice Cooler
                var coolerRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "cooler", "drinks" } });
                string cModel = (coolerRes != null && coolerRes.PrimaryAsset != null) ? coolerRes.PrimaryAsset.ModelName : "prop_cooler_01a";
                props.Add(new { prop_id = "vip_ice_cooler", parent_prop_id = "", model = cModel, offset = new { x = -5.0, y = 2.0, z = 0.0 }, heading = 90.0, is_dynamic = false });

                // If BBQ/Cookout is requested, place BBQ grill ON GROUND near patio, never on table!
                if (p.Contains("bbq") || p.Contains("grill") || p.Contains("cookout"))
                {
                    var bbqRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "bbq_grill" } });
                    string bbqModel = (bbqRes != null && bbqRes.PrimaryAsset != null) ? bbqRes.PrimaryAsset.ModelName : "prop_bbq_5";
                    props.Add(new { prop_id = "patio_bbq_grill", parent_prop_id = "", model = bbqModel, offset = new { x = -4.5, y = 5.5, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });
                }
            }

            // Art & Museum Gallery Easels, Sculptures & Benches
            if (!isInterrogation && isArtOrMuseum)
            {
                var easelRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "easel", "art" } });
                string eModel = (easelRes != null && easelRes.PrimaryAsset != null && (easelRes.PrimaryAsset.HasTag("easel") || easelRes.PrimaryAsset.ModelName.IndexOf("easel", StringComparison.OrdinalIgnoreCase) >= 0)) ? easelRes.PrimaryAsset.ModelName : "prop_easel_01";
                props.Add(new { prop_id = "gallery_easel_1", parent_prop_id = "", model = eModel, offset = new { x = -2.5, y = 4.0, z = 0.0 }, heading = 160.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "gallery_easel_2", parent_prop_id = "", model = eModel, offset = new { x = 2.5, y = 4.0, z = 0.0 }, heading = 200.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "gallery_easel_3", parent_prop_id = "", model = eModel, offset = new { x = -4.0, y = 5.0, z = 0.0 }, heading = 140.0, is_dynamic = false, is_nav_blocker = true });

                var statueRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "statue", "sculpture", "art" } });
                string sModel = (statueRes != null && statueRes.PrimaryAsset != null && (statueRes.PrimaryAsset.HasTag("statue") || statueRes.PrimaryAsset.HasTag("sculpture") || statueRes.PrimaryAsset.ModelName.IndexOf("statue", StringComparison.OrdinalIgnoreCase) >= 0 || statueRes.PrimaryAsset.ModelName.IndexOf("sculpt", StringComparison.OrdinalIgnoreCase) >= 0)) ? statueRes.PrimaryAsset.ModelName : "prop_statue_01";
                props.Add(new { prop_id = "gallery_sculpture_center", parent_prop_id = "", model = sModel, offset = new { x = 0.0, y = 5.5, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var benchRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "bench", "seat" } });
                string bModel = (benchRes != null && benchRes.PrimaryAsset != null && (benchRes.PrimaryAsset.HasTag("bench") || benchRes.PrimaryAsset.ModelName.IndexOf("bench", StringComparison.OrdinalIgnoreCase) >= 0)) ? benchRes.PrimaryAsset.ModelName : "prop_bench_01a";
                props.Add(new { prop_id = "gallery_bench_1", parent_prop_id = "", model = bModel, offset = new { x = 0.0, y = 1.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "gallery_bench_2", parent_prop_id = "", model = bModel, offset = new { x = 4.0, y = 2.5, z = 0.0 }, heading = 270.0, is_dynamic = false });
            }

            // Medical Triage Equipment: Examination bed/gurney, IV stand, monitor desk, defibrillator
            if (!isInterrogation && isEmergency)
            {
                var gurneyRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "gurney", "bed", "medical" } });
                string gurneyModel = (gurneyRes != null && gurneyRes.PrimaryAsset != null) ? gurneyRes.PrimaryAsset.ModelName : "v_med_bed1";
                props.Add(new { prop_id = "triage_gurney", parent_prop_id = "", model = gurneyModel, offset = new { x = 0.0, y = 2.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "triage_gurney_secondary", parent_prop_id = "", model = gurneyModel, offset = new { x = 2.4, y = 2.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });

                var standRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "ivstand", "medical" } });
                string standModel = (standRes != null && standRes.PrimaryAsset != null) ? standRes.PrimaryAsset.ModelName : "v_med_ivstand";
                props.Add(new { prop_id = "medical_ivstand_1", parent_prop_id = "", model = standModel, offset = new { x = 1.0, y = 2.8, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "medical_ivstand_2", parent_prop_id = "", model = standModel, offset = new { x = 3.4, y = 2.8, z = 0.0 }, heading = 0.0, is_dynamic = false });

                var medTableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "medical", "table" } });
                string medTableModel = (medTableRes != null && medTableRes.PrimaryAsset != null) ? medTableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "triage_table", parent_prop_id = "", model = medTableModel, offset = new { x = -1.6, y = 2.8, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var defibRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "defibrillator", "medical", "electronics" } });
                string defibModel = (defibRes != null && defibRes.PrimaryAsset != null) ? defibRes.PrimaryAsset.ModelName : "prop_defibrillator_01";
                props.Add(new { prop_id = "medical_defibrillator", parent_prop_id = "triage_table", model = defibModel, offset = new { x = 0.0, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                // Surgical Examination Spotlight & Privacy Room Divider
                var lightRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "worklight", "lamp", "medical" } });
                string lightModel = (lightRes != null && lightRes.PrimaryAsset != null) ? lightRes.PrimaryAsset.ModelName : "prop_worklight_03a";
                props.Add(new { prop_id = "triage_exam_light", parent_prop_id = "", model = lightModel, offset = new { x = 0.0, y = 4.0, z = 0.0 }, heading = 180.0, is_dynamic = false });

                var screenRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "screen", "office", "divider" } });
                string screenModel = (screenRes != null && screenRes.PrimaryAsset != null) ? screenRes.PrimaryAsset.ModelName : "prop_screen_01";
                props.Add(new { prop_id = "medical_privacy_screen", parent_prop_id = "", model = screenModel, offset = new { x = -2.8, y = 3.2, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });
            }

            // Chop Shop & Auto Salvage Equipment: Engine hoist, toolchest, welding cart, wheel rack, jack & scrap parts
            if (!isInterrogation && isChopShop)
            {
                var hoistRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "hoist", "engine", "industrial" } });
                string hoistModel = (hoistRes != null && hoistRes.PrimaryAsset != null) ? hoistRes.PrimaryAsset.ModelName : "prop_engine_hoist";
                props.Add(new { prop_id = "chop_engine_hoist", parent_prop_id = "", model = hoistModel, offset = new { x = -2.2, y = 3.5, z = 0.0 }, heading = 45.0, is_dynamic = false, is_nav_blocker = true });

                var chestRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "toolchest", "tools", "workshop" } });
                string chestModel = (chestRes != null && chestRes.PrimaryAsset != null) ? chestRes.PrimaryAsset.ModelName : "prop_toolchest_01";
                props.Add(new { prop_id = "chop_toolchest", parent_prop_id = "", model = chestModel, offset = new { x = 2.4, y = 3.2, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });

                var torchRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "welding", "gas", "tank" } });
                string torchModel = (torchRes != null && torchRes.PrimaryAsset != null) ? torchRes.PrimaryAsset.ModelName : "prop_weld_torch";
                props.Add(new { prop_id = "chop_welding_cart", parent_prop_id = "", model = torchModel, offset = new { x = -1.8, y = 1.8, z = 0.0 }, heading = 90.0, is_dynamic = false });

                var rackRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "wheel", "tire", "rack" } });
                string rackModel = (rackRes != null && rackRes.PrimaryAsset != null) ? rackRes.PrimaryAsset.ModelName : "prop_wheel_rack_01";
                props.Add(new { prop_id = "chop_wheel_rack", parent_prop_id = "", model = rackModel, offset = new { x = 2.5, y = 1.8, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                // Floor Trolley Jack & Waste Oil Drums
                var jackRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "carjack", "tools", "workshop" } });
                string jackModel = (jackRes != null && jackRes.PrimaryAsset != null) ? jackRes.PrimaryAsset.ModelName : "prop_carjack";
                props.Add(new { prop_id = "chop_floor_jack", parent_prop_id = "", model = jackModel, offset = new { x = -1.2, y = 3.0, z = 0.0 }, heading = 180.0, is_dynamic = false });

                var drumRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrel", "oil", "industrial" } });
                string drumModel = (drumRes != null && drumRes.PrimaryAsset != null) ? drumRes.PrimaryAsset.ModelName : "prop_barrel_02a";
                props.Add(new { prop_id = "chop_oil_drums", parent_prop_id = "", model = drumModel, offset = new { x = 3.2, y = 4.2, z = 0.0 }, heading = 0.0, is_dynamic = false });
            }

            // Lost MC Biker Clubhouse Equipment: Billiards pool table, beer fridge, crates, bar chairs, jukebox & card table
            if (!isInterrogation && isBiker)
            {
                var poolRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "pooltable", "billiards", "table" } });
                string poolModel = (poolRes != null && poolRes.PrimaryAsset != null) ? poolRes.PrimaryAsset.ModelName : "prop_pooltable_02";
                props.Add(new { prop_id = "clubhouse_pool_table", parent_prop_id = "", model = poolModel, offset = new { x = 0.0, y = 3.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var fridgeRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "beer", "fridge", "bar" } });
                string fridgeModel = (fridgeRes != null && fridgeRes.PrimaryAsset != null) ? fridgeRes.PrimaryAsset.ModelName : "prop_bar_beerfridge";
                props.Add(new { prop_id = "clubhouse_beer_fridge", parent_prop_id = "", model = fridgeModel, offset = new { x = -2.8, y = 1.8, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var crateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "crate", "beer" } });
                string crateModel = (crateRes != null && crateRes.PrimaryAsset != null) ? crateRes.PrimaryAsset.ModelName : "prop_crate_beer_01";
                props.Add(new { prop_id = "clubhouse_beer_crate_1", parent_prop_id = "", model = crateModel, offset = new { x = -2.8, y = 2.6, z = 0.0 }, heading = 45.0, is_dynamic = false });
                props.Add(new { prop_id = "clubhouse_beer_crate_2", parent_prop_id = "", model = crateModel, offset = new { x = -2.8, y = 3.4, z = 0.0 }, heading = 60.0, is_dynamic = false });

                var chairRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "biker", "chair", "seat" } });
                string chairModel = (chairRes != null && chairRes.PrimaryAsset != null) ? chairRes.PrimaryAsset.ModelName : "bkr_prop_biker_chair_01";
                props.Add(new { prop_id = "clubhouse_chair_1", parent_prop_id = "", model = chairModel, offset = new { x = 2.4, y = 2.0, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "clubhouse_chair_2", parent_prop_id = "", model = chairModel, offset = new { x = 2.4, y = 3.2, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "clubhouse_chair_3", parent_prop_id = "", model = chairModel, offset = new { x = 1.5, y = 4.2, z = 0.0 }, heading = 180.0, is_dynamic = false });
                props.Add(new { prop_id = "clubhouse_chair_4", parent_prop_id = "", model = chairModel, offset = new { x = 3.2, y = 4.2, z = 0.0 }, heading = 180.0, is_dynamic = false });

                // Card Table & Vintage Clubhouse Jukebox
                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tableModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "clubhouse_card_table", parent_prop_id = "", model = tableModel, offset = new { x = 2.4, y = 4.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var radioRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "radio", "electronics", "music" } });
                string radioModel = (radioRes != null && radioRes.PrimaryAsset != null) ? radioRes.PrimaryAsset.ModelName : "prop_radio_01";
                props.Add(new { prop_id = "clubhouse_jukebox", parent_prop_id = "", model = radioModel, offset = new { x = -2.8, y = 4.2, z = 0.0 }, heading = 90.0, is_dynamic = false });
            }

            // Chemical Narcotics Lab Equipment: Dual chemical reaction vats, heating table, precision scales, acetone drums & ventilation
            if (!isInterrogation && isDrugLab)
            {
                var vatRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "vat", "meth", "chemical" } });
                string vatModel = (vatRes != null && vatRes.PrimaryAsset != null) ? vatRes.PrimaryAsset.ModelName : "bkr_prop_meth_chckvat";
                props.Add(new { prop_id = "druglab_primary_vat", parent_prop_id = "", model = vatModel, offset = new { x = -1.8, y = 3.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "druglab_secondary_vat", parent_prop_id = "", model = vatModel, offset = new { x = -3.4, y = 3.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var labTableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "meth", "table", "chemical" } });
                string labTableModel = (labTableRes != null && labTableRes.PrimaryAsset != null) ? labTableRes.PrimaryAsset.ModelName : "bkr_prop_meth_table01a";
                props.Add(new { prop_id = "druglab_work_table", parent_prop_id = "", model = labTableModel, offset = new { x = 1.8, y = 3.2, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });

                var scaleRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "scale", "meth", "contraband" } });
                string scaleModel = (scaleRes != null && scaleRes.PrimaryAsset != null) ? scaleRes.PrimaryAsset.ModelName : "bkr_prop_meth_scale_01a";
                props.Add(new { prop_id = "druglab_scale", parent_prop_id = "druglab_work_table", model = scaleModel, offset = new { x = 0.0, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                // Precursor Chemical Drums & Acetone Barrels
                var drumRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrel", "chemical", "acetone" } });
                string drumModel = (drumRes != null && drumRes.PrimaryAsset != null) ? drumRes.PrimaryAsset.ModelName : "bkr_prop_meth_acetone";
                props.Add(new { prop_id = "druglab_acetone_drum_1", parent_prop_id = "", model = drumModel, offset = new { x = -2.6, y = 1.8, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "druglab_acetone_drum_2", parent_prop_id = "", model = drumModel, offset = new { x = -3.4, y = 1.8, z = 0.0 }, heading = 30.0, is_dynamic = false });

                var chemDrumRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrel", "chemical" } });
                string chemDrumModel = (chemDrumRes != null && chemDrumRes.PrimaryAsset != null) ? chemDrumRes.PrimaryAsset.ModelName : "prop_barrel_02a";
                props.Add(new { prop_id = "druglab_precursor_barrel", parent_prop_id = "", model = chemDrumModel, offset = new { x = -4.2, y = 1.8, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Industrial Ventilation Generator & Contraband Boxes
                var genRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "generator", "industrial" } });
                string genModel = (genRes != null && genRes.PrimaryAsset != null) ? genRes.PrimaryAsset.ModelName : "prop_generator_01a";
                props.Add(new { prop_id = "druglab_vent_generator", parent_prop_id = "", model = genModel, offset = new { x = 3.6, y = 3.5, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });

                var boxRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "box", "crate" } });
                string boxModel = (boxRes != null && boxRes.PrimaryAsset != null) ? boxRes.PrimaryAsset.ModelName : "prop_box_tea01a";
                props.Add(new { prop_id = "druglab_packaged_boxes", parent_prop_id = "", model = boxModel, offset = new { x = 1.8, y = 2.0, z = 0.0 }, heading = 15.0, is_dynamic = false });
            }

            // Police Station / Precinct Equipment: Intake booking desk, case files, armory guncase, holding benches, watercooler & archives
            if (!isInterrogation && isPolice)
            {
                var deskRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "desk", "office", "table" } });
                string deskModel = (deskRes != null && deskRes.PrimaryAsset != null) ? deskRes.PrimaryAsset.ModelName : "prop_fib_office_desk_01";
                props.Add(new { prop_id = "police_booking_desk", parent_prop_id = "", model = deskModel, offset = new { x = -1.5, y = 2.5, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var fileRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "file", "box", "office" } });
                string fileModel = (fileRes != null && fileRes.PrimaryAsset != null) ? fileRes.PrimaryAsset.ModelName : "prop_file_box_01";
                props.Add(new { prop_id = "police_case_files", parent_prop_id = "police_booking_desk", model = fileModel, offset = new { x = 0.3, y = 0.0, z = 0.76 }, heading = 15.0, is_dynamic = false });

                var gunBoxRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "guncase", "weapons", "box" } });
                string gunBoxModel = (gunBoxRes != null && gunBoxRes.PrimaryAsset != null) ? gunBoxRes.PrimaryAsset.ModelName : "prop_box_guncase_01a";
                props.Add(new { prop_id = "police_armory_box", parent_prop_id = "police_booking_desk", model = gunBoxModel, offset = new { x = -0.3, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                var benchRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "bench", "seat", "chair" } });
                string benchModel = (benchRes != null && benchRes.PrimaryAsset != null) ? benchRes.PrimaryAsset.ModelName : "prop_bench_01a";
                props.Add(new { prop_id = "police_holding_bench_1", parent_prop_id = "", model = benchModel, offset = new { x = 2.2, y = 2.5, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "police_holding_bench_2", parent_prop_id = "", model = benchModel, offset = new { x = 3.6, y = 2.5, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });

                // Officer Briefing Table & Water Cooler
                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tableModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "police_briefing_table", parent_prop_id = "", model = tableModel, offset = new { x = -1.5, y = 4.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var coolerRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "water", "cooler", "office" } });
                string coolerModel = (coolerRes != null && coolerRes.PrimaryAsset != null) ? coolerRes.PrimaryAsset.ModelName : "prop_cooler_01a";
                props.Add(new { prop_id = "police_water_cooler", parent_prop_id = "", model = coolerModel, offset = new { x = -3.5, y = 2.5, z = 0.0 }, heading = 90.0, is_dynamic = false });
            }

            // Port Terminal & Dockyard Equipment: Shipping containers, heavy wooden crates, forklift pallets, worklights & fuel drums
            if (!isInterrogation && isDockyard)
            {
                var contRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "container", "shipping", "cargo" } });
                string contModel = (contRes != null && contRes.PrimaryAsset != null) ? contRes.PrimaryAsset.ModelName : "prop_container_01a";
                props.Add(new { prop_id = "dock_cargo_container_1", parent_prop_id = "", model = contModel, offset = new { x = -3.5, y = 5.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "dock_cargo_container_2", parent_prop_id = "", model = contModel, offset = new { x = -3.5, y = 8.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var crateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "crate", "wood", "shipping" } });
                string crateModel = (crateRes != null && crateRes.PrimaryAsset != null) ? crateRes.PrimaryAsset.ModelName : "prop_box_wood02a";
                props.Add(new { prop_id = "dock_weapon_crate_1", parent_prop_id = "", model = crateModel, offset = new { x = 1.8, y = 3.0, z = 0.0 }, heading = 15.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "dock_weapon_crate_2", parent_prop_id = "", model = crateModel, offset = new { x = 2.8, y = 3.0, z = 0.0 }, heading = 30.0, is_dynamic = false });

                var palletRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "pallet", "industrial" } });
                string palletModel = (palletRes != null && palletRes.PrimaryAsset != null) ? palletRes.PrimaryAsset.ModelName : "prop_pallet_01a";
                props.Add(new { prop_id = "dock_cargo_pallet_1", parent_prop_id = "", model = palletModel, offset = new { x = 0.0, y = 2.5, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "dock_cargo_pallet_2", parent_prop_id = "", model = palletModel, offset = new { x = 0.0, y = 3.8, z = 0.0 }, heading = 10.0, is_dynamic = false });

                // Industrial Harbor Floodlight Tower & Fuel Drums
                var lightRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "worklight", "industrial" } });
                string lightModel = (lightRes != null && lightRes.PrimaryAsset != null) ? lightRes.PrimaryAsset.ModelName : "prop_worklight_02a";
                props.Add(new { prop_id = "dock_floodlight_tower", parent_prop_id = "", model = lightModel, offset = new { x = 4.0, y = 5.0, z = 0.0 }, heading = 220.0, is_dynamic = false, is_nav_blocker = true });

                var drumRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrel", "oil", "industrial" } });
                string drumModel = (drumRes != null && drumRes.PrimaryAsset != null) ? drumRes.PrimaryAsset.ModelName : "prop_barrel_02a";
                props.Add(new { prop_id = "dock_fuel_barrels", parent_prop_id = "", model = drumModel, offset = new { x = 4.2, y = 3.0, z = 0.0 }, heading = 0.0, is_dynamic = false });
            }

            // Classified Sci-Fi / Biohazard Research: Examination/autopsy bed, specimen containers, lab monitors & cleanroom screens
            if (!isInterrogation && isSciFi)
            {
                var bedRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "bed", "medical" } });
                string bedModel = (bedRes != null && bedRes.PrimaryAsset != null) ? bedRes.PrimaryAsset.ModelName : "v_med_bed1";
                props.Add(new { prop_id = "scifi_autopsy_bed", parent_prop_id = "", model = bedModel, offset = new { x = 0.0, y = 2.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });

                var screenRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "screens", "monitors", "electronics" } });
                string sModel = (screenRes != null && screenRes.PrimaryAsset != null) ? screenRes.PrimaryAsset.ModelName : "xm_prop_x17_screens_01a";
                props.Add(new { prop_id = "scifi_monitoring_station", parent_prop_id = "", model = sModel, offset = new { x = -2.2, y = 3.5, z = 0.0 }, heading = 45.0, is_dynamic = false, is_nav_blocker = true });

                var specRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "alien", "crate", "box", "specimen" } });
                string specModel = (specRes != null && specRes.PrimaryAsset != null) ? specRes.PrimaryAsset.ModelName : "prop_alien_egg_01";
                props.Add(new { prop_id = "scifi_specimen_pod_1", parent_prop_id = "", model = specModel, offset = new { x = 2.0, y = 3.2, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "scifi_specimen_pod_2", parent_prop_id = "", model = specModel, offset = new { x = 2.8, y = 3.2, z = 0.0 }, heading = 15.0, is_dynamic = false, is_nav_blocker = true });

                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tableModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "scifi_instrument_table", parent_prop_id = "", model = tableModel, offset = new { x = -1.2, y = 2.8, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var divRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "screen", "office", "divider" } });
                string divModel = (divRes != null && divRes.PrimaryAsset != null) ? divRes.PrimaryAsset.ModelName : "prop_screen_01";
                props.Add(new { prop_id = "scifi_cleanroom_divider", parent_prop_id = "", model = divModel, offset = new { x = -3.2, y = 2.5, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });
            }

            // Outdoor Bonfire & Barbecue Social Gathering: Campfire/bonfire, bbq grill, coolers, camping chairs & picnic table
            if (!isInterrogation && isOutdoor)
            {
                var fireRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "fire", "bonfire" } });
                string fireModel = (fireRes != null && fireRes.PrimaryAsset != null) ? fireRes.PrimaryAsset.ModelName : "prop_beach_fire";
                props.Add(new { prop_id = "outdoor_central_bonfire", parent_prop_id = "", model = fireModel, offset = new { x = 0.0, y = 3.0, z = 0.0 }, heading = 0.0, is_dynamic = false });

                var bbqRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "bbq", "grill" } });
                string bbqModel = (bbqRes != null && bbqRes.PrimaryAsset != null) ? bbqRes.PrimaryAsset.ModelName : "prop_bbq_5";
                props.Add(new { prop_id = "outdoor_bbq_grill", parent_prop_id = "", model = bbqModel, offset = new { x = -2.5, y = 3.5, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var coolerRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "cooler", "drinks" } });
                string coolerModel = (coolerRes != null && coolerRes.PrimaryAsset != null) ? coolerRes.PrimaryAsset.ModelName : "prop_cooler_01a";
                props.Add(new { prop_id = "outdoor_drink_cooler", parent_prop_id = "", model = coolerModel, offset = new { x = -2.5, y = 2.5, z = 0.0 }, heading = 0.0, is_dynamic = false });

                var chairRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "chair", "seat" } });
                string chairModel = (chairRes != null && chairRes.PrimaryAsset != null) ? chairRes.PrimaryAsset.ModelName : "prop_chair_05";
                props.Add(new { prop_id = "outdoor_camp_chair_1", parent_prop_id = "", model = chairModel, offset = new { x = 2.0, y = 2.4, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "outdoor_camp_chair_2", parent_prop_id = "", model = chairModel, offset = new { x = 2.0, y = 3.6, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "outdoor_camp_chair_3", parent_prop_id = "", model = chairModel, offset = new { x = 0.0, y = 4.5, z = 0.0 }, heading = 180.0, is_dynamic = false });
                props.Add(new { prop_id = "outdoor_camp_chair_4", parent_prop_id = "", model = chairModel, offset = new { x = -1.5, y = 2.0, z = 0.0 }, heading = 45.0, is_dynamic = false });

                var picTableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table", "picnic" } });
                string picTableModel = (picTableRes != null && picTableRes.PrimaryAsset != null) ? picTableRes.PrimaryAsset.ModelName : "prop_picnictable_01";
                props.Add(new { prop_id = "outdoor_picnic_table", parent_prop_id = "", model = picTableModel, offset = new { x = 3.5, y = 3.0, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });
            }

            // Street Race & Drag Meet Equipment: Pit toolchest, tire racks, car jack, gas cans & track barriers
            if (!isInterrogation && isRace)
            {
                var chestRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "toolchest", "workshop" } });
                string chestModel = (chestRes != null && chestRes.PrimaryAsset != null) ? chestRes.PrimaryAsset.ModelName : "prop_toolchest_01";
                props.Add(new { prop_id = "drag_pit_toolchest", parent_prop_id = "", model = chestModel, offset = new { x = -4.0, y = 3.5, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var rackRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "wheel", "tire", "rack" } });
                string rackModel = (rackRes != null && rackRes.PrimaryAsset != null) ? rackRes.PrimaryAsset.ModelName : "prop_wheel_rack_01";
                props.Add(new { prop_id = "drag_racing_tires_1", parent_prop_id = "", model = rackModel, offset = new { x = 4.0, y = 3.5, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "drag_racing_tires_2", parent_prop_id = "", model = rackModel, offset = new { x = 5.2, y = 3.5, z = 0.0 }, heading = 270.0, is_dynamic = false, is_nav_blocker = true });

                var jackRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "carjack", "tools" } });
                string jackModel = (jackRes != null && jackRes.PrimaryAsset != null) ? jackRes.PrimaryAsset.ModelName : "prop_carjack";
                props.Add(new { prop_id = "drag_pit_carjack", parent_prop_id = "", model = jackModel, offset = new { x = -3.0, y = 3.5, z = 0.0 }, heading = 180.0, is_dynamic = false });

                var gasCanRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "gas", "fuel", "can" } });
                string gasCanModel = (gasCanRes != null && gasCanRes.PrimaryAsset != null) ? gasCanRes.PrimaryAsset.ModelName : "prop_gas_can_01a";
                props.Add(new { prop_id = "drag_fuel_canister", parent_prop_id = "", model = gasCanModel, offset = new { x = -4.8, y = 3.5, z = 0.0 }, heading = 0.0, is_dynamic = false });

                var coneRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrier", "traffic" } });
                string coneModel = (coneRes != null && coneRes.PrimaryAsset != null) ? coneRes.PrimaryAsset.ModelName : "prop_barrier_work05";
                props.Add(new { prop_id = "drag_track_barrier_l", parent_prop_id = "", model = coneModel, offset = new { x = -6.0, y = 1.0, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "drag_track_barrier_r", parent_prop_id = "", model = coneModel, offset = new { x = 6.0, y = 1.0, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "drag_track_barrier_l2", parent_prop_id = "", model = coneModel, offset = new { x = -6.0, y = 5.0, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "drag_track_barrier_r2", parent_prop_id = "", model = coneModel, offset = new { x = 6.0, y = 5.0, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Starting Grid Beacon / Timing Post
                var beaconRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "speaker", "audio" } });
                string beaconModel = (beaconRes != null && beaconRes.PrimaryAsset != null) ? beaconRes.PrimaryAsset.ModelName : "prop_speaker_05";
                props.Add(new { prop_id = "drag_start_beacon", parent_prop_id = "", model = beaconModel, offset = new { x = 0.0, y = 1.0, z = 0.0 }, heading = 180.0, is_dynamic = false });
            }

            // Cartel Smuggler Hangar Equipment: Heavy cargo crates, shipping container, weapon cases, industrial pallets, floodlight tower & flight desk
            if (!isInterrogation && isSmugglerHangar)
            {
                var crateRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "crate", "cargo", "shipping" } });
                string crateModel = (crateRes != null && crateRes.PrimaryAsset != null) ? crateRes.PrimaryAsset.ModelName : "prop_box_wood02a";
                props.Add(new { prop_id = "smuggler_cargo_crate_1", parent_prop_id = "", model = crateModel, offset = new { x = -2.5, y = 3.0, z = 0.0 }, heading = 15.0, is_dynamic = false, is_nav_blocker = true });
                props.Add(new { prop_id = "smuggler_cargo_crate_2", parent_prop_id = "", model = crateModel, offset = new { x = -3.5, y = 3.0, z = 0.0 }, heading = 30.0, is_dynamic = false });
                props.Add(new { prop_id = "smuggler_cargo_crate_3", parent_prop_id = "", model = crateModel, offset = new { x = -4.5, y = 3.0, z = 0.0 }, heading = 45.0, is_dynamic = false });

                var contRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "container", "shipping", "cargo" } });
                string contModel = (contRes != null && contRes.PrimaryAsset != null) ? contRes.PrimaryAsset.ModelName : "prop_container_01a";
                props.Add(new { prop_id = "smuggler_container", parent_prop_id = "", model = contModel, offset = new { x = -6.0, y = 5.5, z = 0.0 }, heading = 90.0, is_dynamic = false, is_nav_blocker = true });

                var gunBoxRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "guncase", "weapons", "box" } });
                string gunBoxModel = (gunBoxRes != null && gunBoxRes.PrimaryAsset != null) ? gunBoxRes.PrimaryAsset.ModelName : "prop_box_guncase_01a";
                props.Add(new { prop_id = "smuggler_weapon_case", parent_prop_id = "", model = gunBoxModel, offset = new { x = 2.0, y = 2.5, z = 0.0 }, heading = 0.0, is_dynamic = false });

                var palletRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "pallet", "industrial" } });
                string palletModel = (palletRes != null && palletRes.PrimaryAsset != null) ? palletRes.PrimaryAsset.ModelName : "prop_pallet_01a";
                props.Add(new { prop_id = "smuggler_cargo_pallet", parent_prop_id = "", model = palletModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Tarmac Floodlight & Fuel Barrel Staging
                var lightRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "worklight", "industrial" } });
                string lightModel = (lightRes != null && lightRes.PrimaryAsset != null) ? lightRes.PrimaryAsset.ModelName : "prop_worklight_02a";
                props.Add(new { prop_id = "smuggler_hangar_floodlight", parent_prop_id = "", model = lightModel, offset = new { x = 5.0, y = 4.5, z = 0.0 }, heading = 220.0, is_dynamic = false, is_nav_blocker = true });

                var drumRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "barrel", "fuel", "industrial" } });
                string drumModel = (drumRes != null && drumRes.PrimaryAsset != null) ? drumRes.PrimaryAsset.ModelName : "prop_barrel_02a";
                props.Add(new { prop_id = "smuggler_fuel_drums", parent_prop_id = "", model = drumModel, offset = new { x = 3.5, y = 3.5, z = 0.0 }, heading = 0.0, is_dynamic = false });

                // Flight Manifest Planning Desk
                var deskRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string deskModel = (deskRes != null && deskRes.PrimaryAsset != null) ? deskRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "smuggler_flight_desk", parent_prop_id = "", model = deskModel, offset = new { x = 1.5, y = 3.8, z = 0.0 }, heading = 180.0, is_dynamic = false, is_nav_blocker = true });
            }

            // Clandestine Gambling Den & Poker Tournament Equipment: Card/poker table, chips, cash cases, drinks & seated club chairs
            if (!isInterrogation && isGambling)
            {
                var tableRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "can_be_surface" }, RequiredTags = new List<string> { "table" } });
                string tableModel = (tableRes != null && tableRes.PrimaryAsset != null) ? tableRes.PrimaryAsset.ModelName : "prop_table_03b";
                props.Add(new { prop_id = "poker_table", parent_prop_id = "", model = tableModel, offset = new { x = 0.0, y = 2.8, z = 0.0 }, heading = 0.0, is_dynamic = false, is_nav_blocker = true });

                var cashRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "cash", "money" } });
                string cashModel = (cashRes != null && cashRes.PrimaryAsset != null) ? cashRes.PrimaryAsset.ModelName : "prop_cash_pile_02";
                props.Add(new { prop_id = "poker_pot_cash", parent_prop_id = "poker_table", model = cashModel, offset = new { x = 0.0, y = 0.0, z = 0.76 }, heading = 0.0, is_dynamic = false });

                var caseRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "heist_loot" } });
                string caseModel = (caseRes != null && caseRes.PrimaryAsset != null) ? caseRes.PrimaryAsset.ModelName : "prop_cash_case_01";
                props.Add(new { prop_id = "high_roller_briefcase_1", parent_prop_id = "poker_table", model = caseModel, offset = new { x = -0.4, y = 0.0, z = 0.76 }, heading = 20.0, is_dynamic = false });
                props.Add(new { prop_id = "high_roller_briefcase_2", parent_prop_id = "poker_table", model = caseModel, offset = new { x = 0.4, y = 0.0, z = 0.76 }, heading = 340.0, is_dynamic = false });

                var drinkRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredAffordances = new List<string> { "drinks_service" } });
                string drinkModel = (drinkRes != null && drinkRes.PrimaryAsset != null) ? drinkRes.PrimaryAsset.ModelName : "prop_cooler_01a";
                props.Add(new { prop_id = "gambling_whiskey_service", parent_prop_id = "poker_table", model = drinkModel, offset = new { x = 0.0, y = 0.25, z = 0.76 }, heading = 0.0, is_dynamic = false });

                var chairRes = registry.Resolve(new AssetQuery { Type = AssetType.Prop, RequiredTags = new List<string> { "chair", "seat" } });
                string chairModel = (chairRes != null && chairRes.PrimaryAsset != null) ? chairRes.PrimaryAsset.ModelName : "bkr_prop_biker_chair_01";
                props.Add(new { prop_id = "poker_chair_1", parent_prop_id = "", model = chairModel, offset = new { x = -1.2, y = 2.8, z = 0.0 }, heading = 90.0, is_dynamic = false });
                props.Add(new { prop_id = "poker_chair_2", parent_prop_id = "", model = chairModel, offset = new { x = 1.2, y = 2.8, z = 0.0 }, heading = 270.0, is_dynamic = false });
                props.Add(new { prop_id = "poker_chair_3", parent_prop_id = "", model = chairModel, offset = new { x = 0.0, y = 1.6, z = 0.0 }, heading = 0.0, is_dynamic = false });
                props.Add(new { prop_id = "poker_chair_4", parent_prop_id = "", model = chairModel, offset = new { x = 0.0, y = 4.0, z = 0.0 }, heading = 180.0, is_dynamic = false });
            }

            // 3. Discover / Resolve Canonical Lore Characters & NPCs via Registry Evidence
            CharacterResolutionAudit charAudit = null;
            List<string> namedCandidates = new List<string>();

            // Dynamic scan of registered canonical character definitions from characters manifest (zero hardcoded names in C#)
            var canonicalChars = CanonicalCharacterRegistry.GetAllCharacters();
            foreach (var c in canonicalChars)
            {
                if (string.IsNullOrEmpty(c.FullName)) continue;
                string fLower = c.FullName.ToLowerInvariant();
                bool matches = p.IndexOf(fLower, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches && c.Keywords != null)
                {
                    foreach (var kw in c.Keywords)
                    {
                        if (!string.IsNullOrEmpty(kw) && kw.Length >= 3 && p.IndexOf(kw.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            matches = true;
                            break;
                        }
                    }
                }
                if (matches && !namedCandidates.Contains(c.FullName))
                {
                    namedCandidates.Add(c.FullName);
                }
            }

            // Celebrity Resident DJ check if prompt asks for authentic DJ without specific name
            if (namedCandidates.Count == 0 && (p.Contains("dj") && (p.Contains("resident") || p.Contains("celebrity") || p.Contains("gta online") || p.Contains("character"))))
            {
                namedCandidates.Add("party_dj");
            }

            int actorIndex = 1;
            foreach (string nameQuery in namedCandidates)
            {
                var identityRes = registry.IdentityResolver.ResolveIdentity(nameQuery);
                if (identityRes.IsSuccess)
                {
                    string actorId = "npc_canonical_" + actorIndex;
                    float nx = (actorIndex == 1) ? 0.0f : (actorIndex == 2 ? -1.2f : 1.2f);
                    float ny = (actorIndex == 1) ? 4.2f : 3.0f;
                    float nh = (actorIndex == 1) ? 180.0f : (actorIndex == 2 ? 90.0f : 270.0f);
                    string scenario = isHeist ? "PROP_HUMAN_SEAT_CHAIR" : (isParty ? "WORLD_HUMAN_PARTYING" : "WORLD_HUMAN_STAND_IMPARTIAL");

                    npcs.Add(new
                    {
                        entity_id = actorId,
                        name = identityRes.ResolvedCharacterName,
                        occupation = identityRes.ResolvedCharacterName + " (Canonical)",
                        public_faction = "The Syndicate",
                        hidden_role = "Discovered authentic canonical character: " + identityRes.ResolvedCharacterName,
                        ped_model = identityRes.ResolvedPedModel,
                        offset = new { x = nx, y = ny, z = 0.0 },
                        heading = nh,
                        scenario = scenario,
                        weapon = "none",
                        initial_task = (actorIndex > 1) ? "talk_to:npc_canonical_1" : "none",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 50 } },
                        initial_events = new[] { new { description = "Present at the scene as canonical asset " + identityRes.ResolvedCharacterName, sentiment_tag = "canonical" } }
                    });

                    if (charAudit == null)
                    {
                        charAudit = new CharacterResolutionAudit
                        {
                            RequestedQuery = rawPrompt,
                            RequestedRole = nameQuery,
                            ResolvedCharacterName = identityRes.ResolvedCharacterName,
                            ResolvedPedModel = identityRes.ResolvedPedModel,
                            CanonicalDlcOrigin = identityRes.CanonicalDlcOrigin ?? "Discovered DLC Content",
                            RejectionReason = "Generic pedestrian rejected. Resolved verified canonical asset " + identityRes.ResolvedCharacterName + " from characters manifest.",
                            VerificationSummary = "AUTHENTIC GTA ONLINE CANONICAL ASSET CONFIRMED"
                        };
                    }

                    actorIndex++;
                }
            }

            // Interrogation Cast: Subject, Inquisitor, and Guard Enforcers
            if (isInterrogation)
            {
                var captiveRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "agent", "detective", "smart", "civilian" } });
                string captiveModel = (captiveRes != null && captiveRes.PrimaryAsset != null) ? captiveRes.PrimaryAsset.ModelName : "u_m_m_fibarchitect";
                npcs.Add(new
                {
                    entity_id = "npc_interrogation_subject",
                    name = "Compromised Undercover Agent",
                    occupation = "Undercover Field Operative",
                    public_faction = "Law Enforcement",
                    hidden_role = "Detained undercover agent undergoing hostile questioning",
                    ped_model = captiveModel,
                    offset = new { x = 0.0, y = 2.8, z = 0.0 },
                    heading = 0.0,
                    scenario = "WORLD_HUMAN_SEAT_CHAIR",
                    weapon = "none",
                    initial_task = "none",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", 50 }, { "Cartel", -100 } },
                    initial_events = new[] { new { description = "Seated under harsh interrogation worklight undergoing questioning", sentiment_tag = "defiant" } }
                });

                var inquisitorRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "cartel", "gang", "salva" } });
                string inquisitorModel = (inquisitorRes != null && inquisitorRes.PrimaryAsset != null) ? inquisitorRes.PrimaryAsset.ModelName : "g_m_y_salvagoon_01";
                npcs.Add(new
                {
                    entity_id = "npc_cartel_interrogator",
                    name = "Cartel Lead Inquisitor",
                    occupation = "Cartel Enforcer",
                    public_faction = "Cartel",
                    hidden_role = "Conducting aggressive interrogation of captured agent",
                    ped_model = inquisitorModel,
                    offset = new { x = 0.0, y = 1.6, z = 0.0 },
                    heading = 180.0,
                    scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                    weapon = "WEAPON_PISTOL",
                    initial_task = "aim_at:npc_interrogation_subject",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", -50 }, { "Law Enforcement", -100 } },
                    initial_events = new[] { new { description = "Demanding encryption keys and source identities from captive", sentiment_tag = "aggressive" } }
                });

                var guardRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "cartel", "guard", "security" } });
                string guardModel = (guardRes != null && guardRes.PrimaryAsset != null) ? guardRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";
                npcs.Add(new
                {
                    entity_id = "npc_cartel_guard_1",
                    name = "Cartel Heavy Guard",
                    occupation = "Safehouse Guard",
                    public_faction = "Cartel",
                    hidden_role = "Securing basement entry",
                    ped_model = guardModel,
                    offset = new { x = -2.8, y = 1.0, z = 0.0 },
                    heading = 90.0,
                    scenario = "WORLD_HUMAN_GUARD_STAND",
                    weapon = "WEAPON_PUMPSHOTGUN",
                    initial_task = "guard_post",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", -30 } },
                    initial_events = new[] { new { description = "Guarding entry door with heavy shotgun", sentiment_tag = "alert" } }
                });

                npcs.Add(new
                {
                    entity_id = "npc_cartel_guard_2",
                    name = "Cartel Lookout",
                    occupation = "Safehouse Lookout",
                    public_faction = "Cartel",
                    hidden_role = "Monitoring staircase corridor",
                    ped_model = guardModel,
                    offset = new { x = 2.8, y = 1.0, z = 0.0 },
                    heading = 270.0,
                    scenario = "WORLD_HUMAN_GUARD_STAND",
                    weapon = "WEAPON_MICROSMG",
                    initial_task = "guard_post",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", -30 } },
                    initial_events = new[] { new { description = "Covering back exit against rescue breach", sentiment_tag = "alert" } }
                });
            }
            // Cayo Perico Guard Detail / Armed Security (fallback for generic security/guard prompts)
            else if (!isDrugLab && !isDockyard && !isChopShop && !isBiker && !isEmergency && !isPolice && !isArtOrMuseum && !isHeist && !isCrime && !isMilitary && !isSciFi && !isOutdoor && (p.Contains("cayo") || p.Contains("guard") || p.Contains("security detail") || p.Contains("protecting")))
            {
                var guardRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "security", "guard", "military" } });
                string gModel = (guardRes != null && guardRes.PrimaryAsset != null) ? guardRes.PrimaryAsset.ModelName : "s_m_m_armoured_01";

                npcs.Add(new
                {
                    entity_id = "npc_guard_1",
                    name = "Cartel Armed Enforcer",
                    occupation = "Security Detail",
                    public_faction = "Cartel Security",
                    hidden_role = "Guarding VIP summit perimeter",
                    ped_model = gModel,
                    offset = new { x = -3.5, y = 1.0, z = 0.0 },
                    heading = 90.0,
                    scenario = "WORLD_HUMAN_GUARD_STAND",
                    weapon = "WEAPON_CARBINERIFLE",
                    initial_task = "guard_post",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", 0 } },
                    initial_events = new[] { new { description = "Heavily armed guard scanning the perimeter", sentiment_tag = "alert" } }
                });

                npcs.Add(new
                {
                    entity_id = "npc_guard_2",
                    name = "Cartel Tactical Guard",
                    occupation = "Perimeter Security",
                    public_faction = "Cartel Security",
                    hidden_role = "Securing entry corridor",
                    ped_model = gModel,
                    offset = new { x = 3.5, y = 1.0, z = 0.0 },
                    heading = 270.0,
                    scenario = "WORLD_HUMAN_GUARD_STAND",
                    weapon = "WEAPON_CARBINERIFLE",
                    initial_task = "guard_post",
                    initial_state = "ALERT",
                    relationships = new Dictionary<string, int> { { "Player", 0 } },
                    initial_events = new[] { new { description = "Maintains defensive perimeter around VIP meeting", sentiment_tag = "alert" } }
                });
            }

            // Fill standard NPCs matching the theme if needed
            if (npcs.Count == 0)
            {
                if (isMilitary)
                {
                    var milPedRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "military", "marine", "security" } });
                    string pModel = (milPedRes != null && milPedRes.PrimaryAsset != null) ? milPedRes.PrimaryAsset.ModelName : "s_m_y_marine_01";
                    npcs.Add(new { entity_id = "npc_tactical_lead", name = "Tactical Unit Officer", occupation = "Security Lead", public_faction = "Military", hidden_role = "Enforcing perimeter security", ped_model = pModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 180.0, scenario = "WORLD_HUMAN_GUARD_STAND", weapon = "WEAPON_CARBINERIFLE", initial_task = "guard_post", initial_state = "ALERT", relationships = new Dictionary<string, int> { { "Player", -10 } }, initial_events = new[] { new { description = "Guards the perimeter with heavy weapon", sentiment_tag = "military" } } });
                }
                else if (isCrime)
                {
                    bool hasFamilies = p.Contains("families") || p.Contains("grove") || p.Contains("famca");
                    bool hasBallas = p.Contains("ballas") || p.Contains("balla");

                    if (hasFamilies && hasBallas)
                    {
                        var famRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "families" } });
                        string famModel = (famRes != null && famRes.PrimaryAsset != null) ? famRes.PrimaryAsset.ModelName : "g_m_y_famca_01";

                        var ballasRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "ballas" } });
                        string ballasModel = (ballasRes != null && ballasRes.PrimaryAsset != null) ? ballasRes.PrimaryAsset.ModelName : "g_m_y_ballasout_01";

                        npcs.Add(new
                        {
                            entity_id = "npc_families_lead",
                            name = "Families Lieutenant",
                            occupation = "Arms Buyer",
                            public_faction = "Families",
                            hidden_role = "Negotiating firearms purchase with rival gang",
                            ped_model = famModel,
                            offset = new { x = -1.2, y = 2.0, z = 0.0 },
                            heading = 90.0,
                            scenario = "WORLD_HUMAN_DRUG_DEALER",
                            weapon = "WEAPON_MICROSMG",
                            initial_task = "talk_to:npc_ballas_lead",
                            initial_state = "ALERT",
                            relationships = new Dictionary<string, int> { { "Ballas", -80 }, { "Player", 0 } },
                            initial_events = new[] { new { description = "Negotiates transaction terms with Ballas supplier", sentiment_tag = "tense" } }
                        });

                        npcs.Add(new
                        {
                            entity_id = "npc_ballas_lead",
                            name = "Ballas Arms Supplier",
                            occupation = "Contraband Supplier",
                            public_faction = "Ballas",
                            hidden_role = "Supplying military contraband to Families",
                            ped_model = ballasModel,
                            offset = new { x = 1.2, y = 2.0, z = 0.0 },
                            heading = 270.0,
                            scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                            weapon = "WEAPON_PISTOL",
                            initial_task = "talk_to:npc_families_lead",
                            initial_state = "ALERT",
                            relationships = new Dictionary<string, int> { { "Families", -80 }, { "Player", 0 } },
                            initial_events = new[] { new { description = "Verifies cash payment against shipment manifest", sentiment_tag = "tense" } }
                        });
                    }
                    else
                    {
                        var crimePedRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "gang", "dealer", "cartel" } });
                        string pModel = (crimePedRes != null && crimePedRes.PrimaryAsset != null) ? crimePedRes.PrimaryAsset.ModelName : "g_m_y_salvagoon_01";
                        npcs.Add(new { entity_id = "npc_syndicate_lead", name = "Syndicate Broker", occupation = "Contraband Broker", public_faction = "Cartel", hidden_role = "Overseeing illegal transactions", ped_model = pModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 180.0, scenario = "WORLD_HUMAN_DRUG_DEALER", weapon = "WEAPON_PISTOL", initial_task = "none", initial_state = "ALERT", relationships = new Dictionary<string, int> { { "Player", -20 } }, initial_events = new[] { new { description = "Negotiates transaction terms with buyer", sentiment_tag = "tense" } } });
                    }
                }
                else if (isHeist)
                {
                    bool isBankVault = p.Contains("bank") || p.Contains("vault") || p.Contains("deposit") || p.Contains("gold");
                    if (isBankVault)
                    {
                        // Armed Heist Crew inside the Vault
                        var crewLeaderRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "heist", "tactical", "security" } });
                        string leaderModel = (crewLeaderRes != null && crewLeaderRes.PrimaryAsset != null) ? crewLeaderRes.PrimaryAsset.ModelName : "s_m_m_highsec_01";

                        var safecrackerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "heist_combat", "goon", "criminal" }, RawQuery = rawPrompt });
                        string safecrackerModel = (safecrackerRes != null && safecrackerRes.PrimaryAsset != null) ? safecrackerRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";

                        var guardRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "security", "guard" } });
                        string guardModel = (guardRes != null && guardRes.PrimaryAsset != null) ? guardRes.PrimaryAsset.ModelName : "s_m_m_armoured_01";

                        npcs.Add(new
                        {
                            entity_id = "npc_heist_leader",
                            name = "Heist Crew Mastermind",
                            occupation = "Infiltration Lead",
                            public_faction = "Heist Crew",
                            hidden_role = "Directing thermal breach and loot extraction",
                            ped_model = leaderModel,
                            offset = new { x = -1.5, y = 2.0, z = 0.0 },
                            heading = 90.0,
                            scenario = "WORLD_HUMAN_GUARD_STAND",
                            weapon = "WEAPON_CARBINERIFLE",
                            initial_task = "guard_post",
                            initial_state = "ALERT",
                            relationships = new Dictionary<string, int> { { "Player", 50 }, { "Police", -100 } },
                            initial_events = new[] { new { description = "Covering the vault door with an assault carbine", sentiment_tag = "alert" } }
                        });

                        npcs.Add(new
                        {
                            entity_id = "npc_vault_safecracker",
                            name = "Crew Safecracker",
                            occupation = "Vault Specialist",
                            public_faction = "Heist Crew",
                            hidden_role = "Emptying deposit boxes and bagging gold bullion",
                            ped_model = safecrackerModel,
                            offset = new { x = 1.2, y = 3.0, z = 0.0 },
                            heading = 0.0,
                            scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                            weapon = "WEAPON_PISTOL",
                            initial_task = "drill_vault",
                            initial_state = "ALERT",
                            relationships = new Dictionary<string, int> { { "Player", 50 }, { "Police", -100 } },
                            initial_events = new[] { new { description = "Stuffing cash bands and gold into duffel bags", sentiment_tag = "tense" } }
                        });

                        npcs.Add(new
                        {
                            entity_id = "npc_bank_manager",
                            name = "Subdued Bank Manager",
                            occupation = "Bank Branch Manager",
                            public_faction = "Civilians",
                            hidden_role = "Surrendered vault access key under duress",
                            ped_model = guardModel,
                            offset = new { x = 2.5, y = 1.5, z = 0.0 },
                            heading = 240.0,
                            scenario = "WORLD_HUMAN_STAND_COWER",
                            weapon = "none",
                            initial_task = "none",
                            initial_state = "FRIGHTENED",
                            relationships = new Dictionary<string, int> { { "Player", -50 } },
                            initial_events = new[] { new { description = "Cowering on the floor near the deposit lockers", sentiment_tag = "fear" } }
                        });
                    }
                    else
                    {
                        // Tactical Heist Planning Room
                        var plannerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "planner", "tactical" } });
                        string pModel = (plannerRes != null && plannerRes.PrimaryAsset != null) ? plannerRes.PrimaryAsset.ModelName : "s_m_m_highsec_01";
                        npcs.Add(new { entity_id = "npc_heist_planner", name = "Heist Coordinator", occupation = "Tactical Strategist", public_faction = "Syndicate", hidden_role = "Coordinating strike timelines", ped_model = pModel, offset = new { x = 0.0, y = 2.0, z = 0.0 }, heading = 180.0, scenario = "PROP_HUMAN_SEAT_CHAIR", weapon = "none", initial_task = "none", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 40 } }, initial_events = new[] { new { description = "Reviewing bank blueprints and escape route", sentiment_tag = "focused" } } });
                    }
                }
                else if (isParty || isLuxury)
                {
                    var vipPedRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "vip", "party", "socialite" } });
                    string pModel = (vipPedRes != null && vipPedRes.PrimaryAsset != null) ? vipPedRes.PrimaryAsset.ModelName : "csb_billionaire";
                    npcs.Add(new { entity_id = "npc_vip_patron", name = "VIP High-Roller", occupation = "High-Society VIP", public_faction = "Civilians", hidden_role = "Enjoying the exclusive atmosphere", ped_model = pModel, offset = new { x = -1.5, y = 2.0, z = 0.0 }, heading = 45.0, scenario = "WORLD_HUMAN_DRINKING", weapon = "none", initial_task = "party_roam", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 20 } }, initial_events = new[] { new { description = "Sipping vintage champagne and socializing", sentiment_tag = "relaxed" } } });

                    var dancerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "party", "dancer" } });
                    string dModel = (dancerRes != null && dancerRes.PrimaryAsset != null) ? dancerRes.PrimaryAsset.ModelName : "a_f_y_studioparty_01";
                    npcs.Add(new { entity_id = "npc_party_dancer", name = "Party Guest", occupation = "Music Enthusiast", public_faction = "Civilians", hidden_role = "Vibing to the music set", ped_model = dModel, offset = new { x = 0.5, y = 3.5, z = 0.0 }, heading = 180.0, scenario = "WORLD_HUMAN_PARTYING", weapon = "none", initial_task = "party_roam", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 25 } }, initial_events = new[] { new { description = "Dancing by the terrace sound system", sentiment_tag = "hyped" } } });
                }
                else if (isArtOrMuseum)
                {
                    var connoisseurRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "smart", "formal", "rich" } });
                    string cModel = (connoisseurRes != null && connoisseurRes.PrimaryAsset != null) ? connoisseurRes.PrimaryAsset.ModelName : "a_m_y_smartcaspat_01";
                    npcs.Add(new { entity_id = "npc_art_connoisseur", name = "Art Connoisseur", occupation = "Art Critic & Collector", public_faction = "Civilians", hidden_role = "Evaluating contemporary art collection", ped_model = cModel, offset = new { x = -2.0, y = 3.0, z = 0.0 }, heading = 340.0, scenario = "WORLD_HUMAN_TOURIST_MAP", weapon = "none", initial_task = "art_tour", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 15 } }, initial_events = new[] { new { description = "Studying gallery exhibit brochure and catalogue", sentiment_tag = "refined" } } });

                    var visitorRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "tourist", "smart", "civilian" } });
                    string vModel = (visitorRes != null && visitorRes.PrimaryAsset != null) ? visitorRes.PrimaryAsset.ModelName : "a_f_y_smartcaspat_01";
                    npcs.Add(new { entity_id = "npc_art_visitor", name = "Gallery Visitor", occupation = "Museum Patron", public_faction = "Civilians", hidden_role = "Photographing art installations", ped_model = vModel, offset = new { x = 2.0, y = 3.5, z = 0.0 }, heading = 20.0, scenario = "WORLD_HUMAN_PAPARAZZI", weapon = "none", initial_task = "art_tour", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 10 } }, initial_events = new[] { new { description = "Photographing featured paintings and sculptures", sentiment_tag = "curious" } } });
                }
                else if (isEmergency)
                {
                    var docRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "doctor", "medic", "surgeon" } });
                    string docModel = (docRes != null && docRes.PrimaryAsset != null) ? docRes.PrimaryAsset.ModelName : "s_m_m_doctor_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_emergency_doctor",
                        name = "Attending Physician",
                        occupation = "Emergency Surgeon",
                        public_faction = "Medical Personnel",
                        hidden_role = "Directing urgent patient stabilization and trauma triage",
                        ped_model = docModel,
                        offset = new { x = -1.2, y = 2.8, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "treat:npc_injured_patient",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 50 } },
                        initial_events = new[] { new { description = "Administering emergency medical stabilization", sentiment_tag = "focused" } }
                    });

                    var paramedRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "paramedic", "ambulance", "medic" } });
                    string paramedModel = (paramedRes != null && paramedRes.PrimaryAsset != null) ? paramedRes.PrimaryAsset.ModelName : "s_m_m_paramedic_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_emergency_paramedic",
                        name = "Field Paramedic",
                        occupation = "First Responder",
                        public_faction = "Medical Personnel",
                        hidden_role = "Monitoring patient oxygenation and vital statistics",
                        ped_model = paramedModel,
                        offset = new { x = 1.2, y = 2.8, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_CLIPBOARD",
                        weapon = "none",
                        initial_task = "assist:npc_emergency_doctor",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 40 } },
                        initial_events = new[] { new { description = "Logging incoming patient vitals on triage clipboard", sentiment_tag = "alert" } }
                    });

                    var patientRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, Category = "CivilianPed" });
                    string patientModel = (patientRes != null && patientRes.PrimaryAsset != null) ? patientRes.PrimaryAsset.ModelName : "a_m_y_beach_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_injured_patient",
                        name = "Critical Trauma Patient",
                        occupation = "Injured Civilian",
                        public_faction = "Civilians",
                        hidden_role = "Undergoing resuscitation on the emergency triage gurney",
                        ped_model = patientModel,
                        offset = new { x = 0.0, y = 2.8, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_SUNBATHE",
                        weapon = "none",
                        initial_task = "none",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 50 } },
                        initial_events = new[] { new { description = "Lying flat on triage gurney undergoing life support", sentiment_tag = "injured" } }
                    });
                }
                else if (isChopShop)
                {
                    var mechRes1 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "mechanic", "autoshop", "shopkeep" } });
                    string mModel1 = (mechRes1 != null && mechRes1.PrimaryAsset != null) ? mechRes1.PrimaryAsset.ModelName : "mp_m_shopkeep_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_lead_welder",
                        name = "Master Fabricator",
                        occupation = "Chop Shop Fabricator",
                        public_faction = "Syndicate",
                        hidden_role = "Cutting supercar chassis with acetylene torch",
                        ped_model = mModel1,
                        offset = new { x = -1.5, y = 3.0, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_WELDING",
                        weapon = "none",
                        initial_task = "weld_chassis",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 30 } },
                        initial_events = new[] { new { description = "Welding reinforced frame on stripped supercar", sentiment_tag = "focused" } }
                    });

                    var mechRes2 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "mechanic", "worker" } });
                    string mModel2 = (mechRes2 != null && mechRes2.PrimaryAsset != null) ? mechRes2.PrimaryAsset.ModelName : "s_m_m_autoshop_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_engine_mechanic",
                        name = "Dismantle Specialist",
                        occupation = "Auto Mechanic",
                        public_faction = "Syndicate",
                        hidden_role = "Extracting performance engine and suspension components",
                        ped_model = mModel2,
                        offset = new { x = 1.5, y = 3.0, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_VEHICLE_MECHANIC",
                        weapon = "none",
                        initial_task = "strip_engine",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 30 } },
                        initial_events = new[] { new { description = "Loosening manifold bolts with air wrench", sentiment_tag = "busy" } }
                    });

                    var foremanRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "salton", "gang", "dealer" } });
                    string fModel = (foremanRes != null && foremanRes.PrimaryAsset != null) ? foremanRes.PrimaryAsset.ModelName : "a_m_m_salton_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_chop_foreman",
                        name = "Salvage Yard Foreman",
                        occupation = "Chop Shop Boss",
                        public_faction = "Syndicate",
                        hidden_role = "Auditing stolen VIN numbers and cataloging parts inventory",
                        ped_model = fModel,
                        offset = new { x = 0.0, y = 1.2, z = 0.0 },
                        heading = 0.0,
                        scenario = "WORLD_HUMAN_CLIPBOARD",
                        weapon = "WEAPON_PISTOL",
                        initial_task = "audit_manifest",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Checking parts manifests against buyer orders", sentiment_tag = "cautious" } }
                    });
                }
                else if (isBiker)
                {
                    var lost1 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "lost", "biker" } });
                    string lModel1 = (lost1 != null && lost1.PrimaryAsset != null) ? lost1.PrimaryAsset.ModelName : "g_m_y_lost_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_biker_president",
                        name = "Lost MC Enforcer",
                        occupation = "Motorcycle Club Sergeant",
                        public_faction = "Lost MC",
                        hidden_role = "Enforcing brotherhood rules and nursing a cold beer",
                        ped_model = lModel1,
                        offset = new { x = -1.8, y = 2.0, z = 0.0 },
                        heading = 45.0,
                        scenario = "WORLD_HUMAN_DRINKING",
                        weapon = "WEAPON_SAWNOFFSHOTGUN",
                        initial_task = "drink_beer",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 10 } },
                        initial_events = new[] { new { description = "Drinking cold beer by the bar counter", sentiment_tag = "tough" } }
                    });

                    var lost2 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "lost", "biker" } });
                    string lModel2 = (lost2 != null && lost2.PrimaryAsset != null) ? lost2.PrimaryAsset.ModelName : "g_m_y_lost_02";
                    npcs.Add(new
                    {
                        entity_id = "npc_biker_road_captain",
                        name = "Lost MC Road Captain",
                        occupation = "Biker Enforcer",
                        public_faction = "Lost MC",
                        hidden_role = "Relaxing in the clubhouse lounge smoking",
                        ped_model = lModel2,
                        offset = new { x = 2.2, y = 2.0, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_SMOKING",
                        weapon = "WEAPON_MICROSMG",
                        initial_task = "lounge_smoke",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 10 } },
                        initial_events = new[] { new { description = "Smoking a cigarette watching the pool game", sentiment_tag = "relaxed" } }
                    });

                    var lost3 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "lost", "biker" } });
                    string lModel3 = (lost3 != null && lost3.PrimaryAsset != null) ? lost3.PrimaryAsset.ModelName : "g_m_y_lost_03";
                    npcs.Add(new
                    {
                        entity_id = "npc_biker_pool_player",
                        name = "Lost MC Member",
                        occupation = "Club Member",
                        public_faction = "Lost MC",
                        hidden_role = "Lining up bank shots on the pool table",
                        ped_model = lModel3,
                        offset = new { x = -0.8, y = 3.2, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "WEAPON_KNUCKLE",
                        initial_task = "play_pool",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 15 } },
                        initial_events = new[] { new { description = "Playing eight-ball on the club table", sentiment_tag = "competitive" } }
                    });
                }
                else if (isDrugLab)
                {
                    var cookRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "weed", "cook", "chemical", "worker" } });
                    string cookModel = (cookRes != null && cookRes.PrimaryAsset != null) ? cookRes.PrimaryAsset.ModelName : "mp_m_weed_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_head_cook",
                        name = "Chief Chemical Synthesizer",
                        occupation = "Meth Chemist",
                        public_faction = "Cartel Syndicate",
                        hidden_role = "Regulating boiling temperature and precursors in the chemical vat",
                        ped_model = cookModel,
                        offset = new { x = -1.5, y = 2.8, z = 0.0 },
                        heading = 0.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "cook_batch",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", -20 } },
                        initial_events = new[] { new { description = "Boiling chemical solutions in stainless vats", sentiment_tag = "focused" } }
                    });

                    var assistRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "runner", "worker", "civilian" } });
                    string aModel = (assistRes != null && assistRes.PrimaryAsset != null) ? assistRes.PrimaryAsset.ModelName : "a_m_y_runner_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_lab_packager",
                        name = "Packaging Specialist",
                        occupation = "Lab Assistant",
                        public_faction = "Cartel Syndicate",
                        hidden_role = "Weighing batches and vacuum sealing product on the work table",
                        ped_model = aModel,
                        offset = new { x = 1.5, y = 2.8, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "package_contraband",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", -20 } },
                        initial_events = new[] { new { description = "Measuring powder quantities with precision scales", sentiment_tag = "busy" } }
                    });

                    var guardRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "cartel", "guard", "security" } });
                    string gModel = (guardRes != null && guardRes.PrimaryAsset != null) ? guardRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_druglab_sentry",
                        name = "Cartel Enforcer",
                        occupation = "Lab Sentry",
                        public_faction = "Cartel Syndicate",
                        hidden_role = "Guarding basement bunker entrance with an assault rifle",
                        ped_model = gModel,
                        offset = new { x = 0.0, y = 0.8, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_ASSAULTRIFLE",
                        initial_task = "guard_post",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -40 } },
                        initial_events = new[] { new { description = "Covering the vault door against DEA raid", sentiment_tag = "alert" } }
                    });
                }
                else if (isPolice)
                {
                    var cop1 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "cop", "police", "officer" } });
                    string cModel1 = (cop1 != null && cop1.PrimaryAsset != null) ? cop1.PrimaryAsset.ModelName : "s_m_y_cop_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_desk_sergeant",
                        name = "Intake Desk Sergeant",
                        occupation = "Police Sergeant",
                        public_faction = "Law Enforcement",
                        hidden_role = "Processing arrest booking logs and cataloging confiscated property",
                        ped_model = cModel1,
                        offset = new { x = -1.2, y = 2.2, z = 0.0 },
                        heading = 0.0,
                        scenario = "WORLD_HUMAN_CLIPBOARD",
                        weapon = "WEAPON_PISTOL",
                        initial_task = "booking_intake",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Filing arrest report at the precinct front desk", sentiment_tag = "formal" } }
                    });

                    var detRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "detective", "fib", "smart" } });
                    string detModel = (detRes != null && detRes.PrimaryAsset != null) ? detRes.PrimaryAsset.ModelName : "s_m_m_fiboffice_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_lead_detective",
                        name = "Major Crimes Detective",
                        occupation = "Detective",
                        public_faction = "Law Enforcement",
                        hidden_role = "Cross-referencing suspect statements against ballistic evidence",
                        ped_model = detModel,
                        offset = new { x = 0.0, y = 3.6, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "WEAPON_PISTOL",
                        initial_task = "review_case",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Reviewing open homicide case binders", sentiment_tag = "analytical" } }
                    });

                    var prisRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "prisoner", "inmate", "criminal" } });
                    string prisModel = (prisRes != null && prisRes.PrimaryAsset != null) ? prisRes.PrimaryAsset.ModelName : "u_m_y_prisoner_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_detained_suspect",
                        name = "Detained Suspect",
                        occupation = "Arrestee",
                        public_faction = "Civilians",
                        hidden_role = "Waiting handcuffed on the intake holding bench",
                        ped_model = prisModel,
                        offset = new { x = 2.0, y = 2.5, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_SEAT_CHAIR",
                        weapon = "none",
                        initial_task = "none",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 0 }, { "Law Enforcement", -50 } },
                        initial_events = new[] { new { description = "Seated on intake bench awaiting cell transfer", sentiment_tag = "surly" } }
                    });
                }
                else if (isDockyard)
                {
                    var dockRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "dockwork", "worker", "marine" } });
                    string dModel = (dockRes != null && dockRes.PrimaryAsset != null) ? dockRes.PrimaryAsset.ModelName : "s_m_m_dockwork_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_crane_operator",
                        name = "Harbor Longshoreman",
                        occupation = "Crane Operator",
                        public_faction = "Civilians",
                        hidden_role = "Verifying shipping container manifest numbers",
                        ped_model = dModel,
                        offset = new { x = 0.0, y = 2.0, z = 0.0 },
                        heading = 0.0,
                        scenario = "WORLD_HUMAN_CLIPBOARD",
                        weapon = "none",
                        initial_task = "inspect_cargo",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 10 } },
                        initial_events = new[] { new { description = "Inspecting intermodal container seals", sentiment_tag = "routine" } }
                    });

                    var smugRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "gang", "dealer", "cartel" } });
                    string sModel = (smugRes != null && smugRes.PrimaryAsset != null) ? smugRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_cargo_smuggler",
                        name = "Smuggling Kingpin",
                        occupation = "Contraband Broker",
                        public_faction = "Syndicate",
                        hidden_role = "Supervising weapons container distribution",
                        ped_model = sModel,
                        offset = new { x = 2.2, y = 2.5, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_DRUG_DEALER",
                        weapon = "WEAPON_PISTOL",
                        initial_task = "oversee_transfer",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -20 } },
                        initial_events = new[] { new { description = "Verifying firearms shipment contents", sentiment_tag = "tense" } }
                    });
                }
                else if (isSmugglerHangar)
                {
                    var bossRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "cartel", "gang", "dealer" } });
                    string bModel = (bossRes != null && bossRes.PrimaryAsset != null) ? bossRes.PrimaryAsset.ModelName : "g_m_m_cartelguards_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_smuggler_boss",
                        name = "Cartel Smuggling Boss",
                        occupation = "Contraband Pilot & Broker",
                        public_faction = "Cartel Syndicate",
                        hidden_role = "Overseeing cargo plane loading and contraband routing",
                        ped_model = bModel,
                        offset = new { x = 0.0, y = 2.2, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "WEAPON_HEAVYPISTOL",
                        initial_task = "oversee_loading",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -20 } },
                        initial_events = new[] { new { description = "Reviewing flight manifest and cargo weight distribution", sentiment_tag = "commanding" } }
                    });

                    var sentryRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "guard", "security", "cartel" } });
                    string sModel = (sentryRes != null && sentryRes.PrimaryAsset != null) ? sentryRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_hangar_sentry",
                        name = "Hangar Armed Guard",
                        occupation = "Mercenary Guard",
                        public_faction = "Cartel Syndicate",
                        hidden_role = "Guarding the runway tarmac and hangar perimeter",
                        ped_model = sModel,
                        offset = new { x = 3.0, y = 2.5, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_ASSAULTRIFLE",
                        initial_task = "guard_perimeter",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -30 } },
                        initial_events = new[] { new { description = "Patrolling hangar tarmac with assault rifle", sentiment_tag = "vigilant" } }
                    });
                }
                else if (isRace)
                {
                    var mechRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "autoshop", "mechanic" } });
                    string mModel = (mechRes != null && mechRes.PrimaryAsset != null) ? mechRes.PrimaryAsset.ModelName : "s_m_m_autoshop_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_drag_mechanic",
                        name = "Pit Crew Chief",
                        occupation = "Race Mechanic",
                        public_faction = "Civilians",
                        hidden_role = "Tuning engine telemetry and checking tire pressures",
                        ped_model = mModel,
                        offset = new { x = -3.5, y = 3.0, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_VEHICLE_MECHANIC",
                        weapon = "none",
                        initial_task = "tune_engine",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 25 } },
                        initial_events = new[] { new { description = "Calibrating turbo boost pressures before the countdown", sentiment_tag = "focused" } }
                    });

                    var racerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "tuner", "civilian" } });
                    string rModel = (racerRes != null && racerRes.PrimaryAsset != null) ? racerRes.PrimaryAsset.ModelName : "a_m_y_hasjew_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_street_racer",
                        name = "Lead Drag Racer",
                        occupation = "Street Racer",
                        public_faction = "Civilians",
                        hidden_role = "Revving engine at the starting grid awaiting the flag",
                        ped_model = rModel,
                        offset = new { x = 3.5, y = 3.0, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "stage_drag",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Eyeing competitor down the quarter-mile tarmac", sentiment_tag = "competitive" } }
                    });
                }
                else if (isGambling)
                {
                    var dealerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "smart", "casino", "civilian" } });
                    string dModel = (dealerRes != null && dealerRes.PrimaryAsset != null) ? dealerRes.PrimaryAsset.ModelName : "s_m_m_highsec_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_poker_dealer",
                        name = "Tournament Croupier",
                        occupation = "Card Dealer",
                        public_faction = "Civilians",
                        hidden_role = "Dealing cards and raking tournament chips",
                        ped_model = dModel,
                        offset = new { x = 0.0, y = 3.6, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "deal_cards",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Shuffling high-stakes card deck and counting pot", sentiment_tag = "focused" } }
                    });

                    var playerRes1 = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "vip", "rich", "civilian" } });
                    string p1Model = (playerRes1 != null && playerRes1.PrimaryAsset != null) ? playerRes1.PrimaryAsset.ModelName : "csb_billionaire";
                    npcs.Add(new
                    {
                        entity_id = "npc_high_roller_player",
                        name = "Syndicate High-Roller",
                        occupation = "Poker Player",
                        public_faction = "Civilians",
                        hidden_role = "Holding pocket aces and raising the pot stakes",
                        ped_model = p1Model,
                        offset = new { x = -1.2, y = 2.8, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_DRINKING",
                        weapon = "none",
                        initial_task = "play_poker",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 15 } },
                        initial_events = new[] { new { description = "Sipping scotch while raising a stack of chips", sentiment_tag = "confident" } }
                    });

                    var bouncerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "bouncer", "security", "guard" } });
                    string bModel = (bouncerRes != null && bouncerRes.PrimaryAsset != null) ? bouncerRes.PrimaryAsset.ModelName : "s_m_m_bouncer_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_den_bouncer",
                        name = "Pit Security Enforcer",
                        occupation = "Bouncer",
                        public_faction = "Syndicate",
                        hidden_role = "Guarding the backroom entrance ensuring no cheats or weapons",
                        ped_model = bModel,
                        offset = new { x = 2.4, y = 1.8, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_PISTOL",
                        initial_task = "guard_door",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 0 } },
                        initial_events = new[] { new { description = "Scanning patrons entering the private poker room", sentiment_tag = "watchful" } }
                    });
                }
                else if (isMilitary)
                {
                    var milLead = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "marine", "military", "officer" } });
                    string mLeadModel = (milLead != null && milLead.PrimaryAsset != null) ? milLead.PrimaryAsset.ModelName : "s_m_m_marine_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_military_commander",
                        name = "Tactical Unit Commander",
                        occupation = "Military Officer",
                        public_faction = "Military",
                        hidden_role = "Coordinating perimeter defense and tactical operations",
                        ped_model = mLeadModel,
                        offset = new { x = -1.2, y = 2.2, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_COP_IDLES",
                        weapon = "WEAPON_CARBINERIFLE",
                        initial_task = "guard_post",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -10 } },
                        initial_events = new[] { new { description = "Reviewing perimeter defense coordinates", sentiment_tag = "military" } }
                    });

                    var sentryRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "marine", "military", "soldier" } });
                    string sModel = (sentryRes != null && sentryRes.PrimaryAsset != null) ? sentryRes.PrimaryAsset.ModelName : "s_m_y_marine_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_checkpoint_sentry",
                        name = "Checkpoint Sentry",
                        occupation = "Infantry Soldier",
                        public_faction = "Military",
                        hidden_role = "Guarding the gate perimeter with assault rifle",
                        ped_model = sModel,
                        offset = new { x = 2.2, y = 2.0, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_CARBINERIFLE",
                        initial_task = "guard_post",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", -10 } },
                        initial_events = new[] { new { description = "Stationed at gate sentry post with heavy rifle", sentiment_tag = "alert" } }
                    });
                }
                else if (isBankHeist)
                {
                    var robRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "robber", "heist" } });
                    string robModel = (robRes != null && robRes.PrimaryAsset != null) ? robRes.PrimaryAsset.ModelName : "s_m_y_robber_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_vault_driller",
                        name = "Lead Safecracker",
                        occupation = "Heist Infiltrator",
                        public_faction = "The Syndicate",
                        hidden_role = "Drilling vault locking mechanism with thermal lance",
                        ped_model = robModel,
                        offset = new { x = 0.0, y = 2.8, z = 0.0 },
                        heading = 0.0,
                        scenario = "WORLD_HUMAN_WELDING",
                        weapon = "WEAPON_CARBINERIFLE",
                        initial_task = "crack_vault",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 50 }, { "Law Enforcement", -100 } },
                        initial_events = new[] { new { description = "Boring thermal drill into bank vault door hinges", sentiment_tag = "tense" } }
                    });

                    var enforcerRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "gang", "robber", "security" } });
                    string enfModel = (enforcerRes != null && enforcerRes.PrimaryAsset != null) ? enforcerRes.PrimaryAsset.ModelName : "g_m_m_armgoon_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_heist_enforcer",
                        name = "Crowd Control Gunman",
                        occupation = "Armed Robber",
                        public_faction = "The Syndicate",
                        hidden_role = "Holding hostages on the floor at gunpoint",
                        ped_model = enfModel,
                        offset = new { x = -2.5, y = 2.0, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_PUMPSHOTGUN",
                        initial_task = "crowd_control",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 50 }, { "Law Enforcement", -100 } },
                        initial_events = new[] { new { description = "Covering bank floor keeping hostages down", sentiment_tag = "aggressive" } }
                    });

                    var host1Res = registry.Resolve(new AssetQuery { Type = AssetType.Ped, Category = "CivilianPed" });
                    string h1Model = (host1Res != null && host1Res.PrimaryAsset != null) ? host1Res.PrimaryAsset.ModelName : "a_f_y_business_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_hostage_1",
                        name = "Detained Bank Teller",
                        occupation = "Bank Employee",
                        public_faction = "Civilians",
                        hidden_role = "Lying flat on the bank floor fearing crossfire",
                        ped_model = h1Model,
                        offset = new { x = 2.0, y = 2.2, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_BUM_SLUMPED",
                        weapon = "none",
                        initial_task = "none",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 0 } },
                        initial_events = new[] { new { description = "Cowering on the floor with hands over head", sentiment_tag = "terrified" } }
                    });
                }
                else if (isSciFi)
                {
                    var sciRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "scientist", "doctor", "smart" } });
                    string sciModel = (sciRes != null && sciRes.PrimaryAsset != null) ? sciRes.PrimaryAsset.ModelName : "s_m_m_scientist_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_lead_scientist",
                        name = "Lead Biohazard Researcher",
                        occupation = "Senior Scientist",
                        public_faction = "Scientific Corps",
                        hidden_role = "Logging cellular degradation of anomalous tissue samples",
                        ped_model = sciModel,
                        offset = new { x = -1.2, y = 2.8, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_CLIPBOARD",
                        weapon = "none",
                        initial_task = "analyze_specimen",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 20 } },
                        initial_events = new[] { new { description = "Documenting specimen biosignals on research clipboard", sentiment_tag = "analytical" } }
                    });

                    var hazRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "hazmat", "scientist", "worker" } });
                    string hazModel = (hazRes != null && hazRes.PrimaryAsset != null) ? hazRes.PrimaryAsset.ModelName : "s_m_m_hazmat_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_hazmat_specialist",
                        name = "Hazmat Containment Specialist",
                        occupation = "Containment Technician",
                        public_faction = "Scientific Corps",
                        hidden_role = "Monitoring biohazard airlock and sterilization units",
                        ped_model = hazModel,
                        offset = new { x = 1.2, y = 2.8, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                        weapon = "none",
                        initial_task = "maintain_containment",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 15 } },
                        initial_events = new[] { new { description = "Inspecting chemical filtration and biosafety canisters", sentiment_tag = "focused" } }
                    });

                    var secRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "chemsec", "security", "guard" } });
                    string secModel = (secRes != null && secRes.PrimaryAsset != null) ? secRes.PrimaryAsset.ModelName : "s_m_m_chemsec_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_lab_security",
                        name = "Facility Biohazard Security",
                        occupation = "Security Operative",
                        public_faction = "Scientific Corps",
                        hidden_role = "Enforcing cleanroom perimeter access",
                        ped_model = secModel,
                        offset = new { x = 0.0, y = 1.0, z = 0.0 },
                        heading = 180.0,
                        scenario = "WORLD_HUMAN_GUARD_STAND",
                        weapon = "WEAPON_SMG",
                        initial_task = "guard_post",
                        initial_state = "ALERT",
                        relationships = new Dictionary<string, int> { { "Player", 0 } },
                        initial_events = new[] { new { description = "Guarding decontamination corridor with submachine gun", sentiment_tag = "alert" } }
                    });
                }
                else if (isOutdoor)
                {
                    var musicRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "beach", "civilian", "outdoor" } });
                    string mModel = (musicRes != null && musicRes.PrimaryAsset != null) ? musicRes.PrimaryAsset.ModelName : "a_m_m_beach_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_campfire_musician",
                        name = "Beachfront Musician",
                        occupation = "Camper",
                        public_faction = "Civilians",
                        hidden_role = "Playing acoustic guitar by the open bonfire",
                        ped_model = mModel,
                        offset = new { x = 1.5, y = 2.8, z = 0.0 },
                        heading = 270.0,
                        scenario = "WORLD_HUMAN_MUSICIAN",
                        weapon = "none",
                        initial_task = "play_guitar",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 30 } },
                        initial_events = new[] { new { description = "Playing acoustic folk chords beside the crackling fire", sentiment_tag = "relaxed" } }
                    });

                    var campRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, RequiredTags = new List<string> { "beach", "tourist", "civilian" } });
                    string cModel = (campRes != null && campRes.PrimaryAsset != null) ? campRes.PrimaryAsset.ModelName : "a_f_m_beach_01";
                    npcs.Add(new
                    {
                        entity_id = "npc_bonfire_camper",
                        name = "Social Camper",
                        occupation = "Campground Guest",
                        public_faction = "Civilians",
                        hidden_role = "Grilling skewers and sharing stories around the fire",
                        ped_model = cModel,
                        offset = new { x = -1.5, y = 2.8, z = 0.0 },
                        heading = 90.0,
                        scenario = "WORLD_HUMAN_CAMPFIRE",
                        weapon = "none",
                        initial_task = "socialize_fire",
                        initial_state = "CALM",
                        relationships = new Dictionary<string, int> { { "Player", 25 } },
                        initial_events = new[] { new { description = "Warming hands by the fire with a cold drink", sentiment_tag = "content" } }
                    });
                }
                else
                {
                    var civPedRes = registry.Resolve(new AssetQuery { Type = AssetType.Ped, Category = "CivilianPed" });
                    string pModel = (civPedRes != null && civPedRes.PrimaryAsset != null) ? civPedRes.PrimaryAsset.ModelName : "g_m_y_famca_01";
                    npcs.Add(new { entity_id = "npc_local", name = "Local Contact", occupation = "Neighborhood Resident", public_faction = "Civilians", hidden_role = "Observing street activities", ped_model = pModel, offset = new { x = 0.0, y = 1.5, z = 0.0 }, heading = 180.0, scenario = "WORLD_HUMAN_SMOKING", weapon = "none", initial_task = "none", initial_state = "CALM", relationships = new Dictionary<string, int> { { "Player", 10 } }, initial_events = new[] { new { description = "Standing on the sidewalk watching traffic", sentiment_tag = "neutral" } } });
                }
            }

            // 4. Discover / Resolve Vehicles
            SpatialFeasibilityAudit feasibilityAudit = null;
            if (p.Contains("submarine") || p.Contains("kosatka") || p.Contains("avisa"))
            {
                bool insideVenue = isParty || isLuxury || privacy.Contains("indoor");
                if (insideVenue)
                {
                    // Volumetric mismatch: 130m Kosatka cannot fit in a 25m interior
                    feasibilityAudit = new SpatialFeasibilityAudit
                    {
                        RequestedAsset = "kosatka",
                        AssetClassification = "Submarine (Massive 130m Nuclear Ballistic Vessel)",
                        PhysicalDimensions = "130.0m Length x 16.0m Width x 22.0m Height",
                        TargetEnvironment = "indoor venue (interior)",
                        EnvironmentClearance = "25.0m x 20.0m x 4.5m (SupportsVehicles: False)",
                        SpatialConflictDetected = true,
                        ConflictReason = "Volumetric Context Mismatch: The 130-meter Kosatka submarine exceeds the physical boundary dimensions and clearance ceiling of an indoor venue.",
                        ResolutionStrategy = "CENTERPIECE_SUBMERSIBLE_ADAPTATION_AND_NEARBY_REALIZATION",
                        SuggestedRealization = "1. Centerpiece Submersible Adaptation: Mount the auxiliary submersible ('avisa') as the central display centerpiece.\n2. Nearby Maritime Realization: Link a connected VIP portal to the full-scale submarine in deep waters."
                    };

                    vehicles.Add(new { vehicle_id = "centerpiece_submersible", model = "avisa", offset = new { x = 0.0, y = 3.5, z = 0.6 }, heading = 180.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
                }
                else
                {
                    vehicles.Add(new { vehicle_id = "v_sub", model = "kosatka", offset = new { x = 0.0, y = 10.0, z = 0.0 }, heading = 0.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
                }
            }
            else if (isMilitary && (isRace || p.Contains("vehicle") || p.Contains("tank") || p.Contains("convoy")))
            {
                // Military Vehicle Convoy Column dynamically resolved from registry: Heavy Tank, Armored APC, and Troop Transport
                var mil1 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "tank", "military", "armored" } });
                string m1 = (mil1 != null && mil1.PrimaryAsset != null) ? mil1.PrimaryAsset.ModelName : "khanjali";

                var mil2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "offroad", "armored", "military" } });
                string m2 = (mil2 != null && mil2.PrimaryAsset != null) ? mil2.PrimaryAsset.ModelName : "insurgent2";

                var mil3 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "truck", "military" } });
                string m3 = (mil3 != null && mil3.PrimaryAsset != null) ? mil3.PrimaryAsset.ModelName : "barracks";

                vehicles.Add(new { vehicle_id = "mil_veh_1", model = m1, offset = new { x = -2.8, y = 8.0, z = 0.0 }, heading = 0.0, primary_color = "matte_black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "mil_veh_2", model = m2, offset = new { x = 2.8, y = 8.0, z = 0.0 }, heading = 0.0, primary_color = "black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "mil_veh_3", model = m3, offset = new { x = 0.0, y = 14.0, z = 0.0 }, heading = 0.0, primary_color = "matte_black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (isRace)
            {
                var raceCar1 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "sports", "tuner", "race" } });
                string rModel1 = (raceCar1 != null && raceCar1.PrimaryAsset != null) ? raceCar1.PrimaryAsset.ModelName : "sultanrs";

                var raceCar2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "supercar", "tuner", "race" } });
                string rModel2 = (raceCar2 != null && raceCar2.PrimaryAsset != null) ? raceCar2.PrimaryAsset.ModelName : "elegy";

                var raceCar3 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "muscle", "tuner", "race" } });
                string rModel3 = (raceCar3 != null && raceCar3.PrimaryAsset != null) ? raceCar3.PrimaryAsset.ModelName : "banshee";

                vehicles.Add(new { vehicle_id = "race_car_1", model = rModel1, offset = new { x = -3.0, y = 6.0, z = 0.0 }, heading = 180.0, primary_color = "red", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "race_car_2", model = rModel2, offset = new { x = 3.0, y = 6.0, z = 0.0 }, heading = 180.0, primary_color = "blue", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "race_car_3", model = rModel3, offset = new { x = -7.0, y = 6.0, z = 0.0 }, heading = 180.0, primary_color = "matte_black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (p.Contains("car meet") || p.Contains("cars outside") || p.Contains("tuner"))
            {
                // Dynamic resolution of Los Santos Tuners high-performance street machines
                var tunerQuery1 = new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "tuner", "sports" } };
                var tunerRes1 = registry.Resolve(tunerQuery1);
                string tModel1 = (tunerRes1 != null && tunerRes1.PrimaryAsset != null) ? tunerRes1.PrimaryAsset.ModelName : "sultanrs";

                var tunerQuery2 = new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "supercar", "tuner" } };
                var tunerRes2 = registry.Resolve(tunerQuery2);
                string tModel2 = (tunerRes2 != null && tunerRes2.PrimaryAsset != null) ? tunerRes2.PrimaryAsset.ModelName : "elegy";

                vehicles.Add(new { vehicle_id = "tuner_veh_1", model = tModel1, offset = new { x = -4.0, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "red", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "tuner_veh_2", model = tModel2, offset = new { x = 4.0, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "blue", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (isLuxury && vehicles.Count == 0)
            {
                var luxuryCarRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "supercar", "luxury" } });
                string carModel = (luxuryCarRes != null && luxuryCarRes.PrimaryAsset != null) ? luxuryCarRes.PrimaryAsset.ModelName : "superd";
                vehicles.Add(new { vehicle_id = "valet_luxury_veh_1", model = carModel, offset = new { x = 4.0, y = 8.0, z = 0.0 }, heading = 90.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });

                var luxuryCarRes2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "supercar" } });
                string carModel2 = (luxuryCarRes2 != null && luxuryCarRes2.PrimaryAsset != null) ? luxuryCarRes2.PrimaryAsset.ModelName : "adder";
                vehicles.Add(new { vehicle_id = "valet_luxury_veh_2", model = carModel2, offset = new { x = -4.0, y = 8.0, z = 0.0 }, heading = 90.0, primary_color = "white", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (!isInterrogation && isCrime && vehicles.Count == 0 && (p.Contains("families") || p.Contains("ballas") || ContainsWord(p, "gang") || ContainsWord(p, "car") || ContainsWord(p, "cars") || ContainsWord(p, "vehicle") || ContainsWord(p, "vehicles")))
            {
                // Dynamic gang lowrider/muscle vehicles staged for the arms deal
                var gangVeh1 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "muscle", "lowrider", "gang" } });
                string v1Model = (gangVeh1 != null && gangVeh1.PrimaryAsset != null) ? gangVeh1.PrimaryAsset.ModelName : "buccaneer";

                var gangVeh2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "classic", "sportsclassic", "gang" } });
                string v2Model = (gangVeh2 != null && gangVeh2.PrimaryAsset != null) ? gangVeh2.PrimaryAsset.ModelName : "voodoo";

                vehicles.Add(new { vehicle_id = "gang_veh_1", model = v1Model, offset = new { x = -4.5, y = 5.0, z = 0.0 }, heading = 140.0, primary_color = "green", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "gang_veh_2", model = v2Model, offset = new { x = 4.5, y = 5.0, z = 0.0 }, heading = 220.0, primary_color = "purple", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }
            else if (isHeist && vehicles.Count == 0 && (p.Contains("bank") || p.Contains("heist") || p.Contains("getaway") || p.Contains("van") || p.Contains("escape")))
            {
                // Armored getaway vehicle staged for the bank heist
                var getVehicleRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "armored", "van" } });
                string vModel = (getVehicleRes != null && getVehicleRes.PrimaryAsset != null) ? getVehicleRes.PrimaryAsset.ModelName : "kuruma2";
                vehicles.Add(new { vehicle_id = "heist_getaway_veh", model = vModel, offset = new { x = 0.0, y = 8.0, z = 0.0 }, heading = 0.0, primary_color = "matte_black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (isEmergency && vehicles.Count == 0)
            {
                var ambRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "ambulance", "emergency" } });
                string ambModel = (ambRes != null && ambRes.PrimaryAsset != null) ? ambRes.PrimaryAsset.ModelName : "ambulance";
                vehicles.Add(new { vehicle_id = "emergency_ambulance", model = ambModel, offset = new { x = 0.0, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "white", is_engine_running = true, sirens = true, lights_on = true, driver_entity_id = "" });
            }
            else if (isChopShop && vehicles.Count == 0)
            {
                var superRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "supercar", "tuner" } });
                string superModel = (superRes != null && superRes.PrimaryAsset != null) ? superRes.PrimaryAsset.ModelName : "furia";
                // Supercar stripped inside bay
                vehicles.Add(new { vehicle_id = "chop_stripped_supercar", model = superModel, offset = new { x = 0.0, y = 3.5, z = 0.0 }, heading = 0.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });

                var towRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "truck", "industrial" } });
                string towModel = (towRes != null && towRes.PrimaryAsset != null) ? towRes.PrimaryAsset.ModelName : "flatbed";
                vehicles.Add(new { vehicle_id = "chop_flatbed_truck", model = towModel, offset = new { x = 6.0, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "red", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }
            else if (isBiker && vehicles.Count == 0)
            {
                // Chopper motorcycles staged outside clubhouse in a motorcycle line
                var bike1 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "motorcycle", "chopper" } });
                string b1 = (bike1 != null && bike1.PrimaryAsset != null) ? bike1.PrimaryAsset.ModelName : "daemon";

                var bike2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "motorcycle" } });
                string b2 = (bike2 != null && bike2.PrimaryAsset != null) ? bike2.PrimaryAsset.ModelName : "zombieb";

                var bike3 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "motorcycle" } });
                string b3 = (bike3 != null && bike3.PrimaryAsset != null) ? bike3.PrimaryAsset.ModelName : "bagger";

                var bike4 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "chopper", "motorcycle" } });
                string b4 = (bike4 != null && bike4.PrimaryAsset != null) ? bike4.PrimaryAsset.ModelName : "hexer";

                vehicles.Add(new { vehicle_id = "biker_chopper_1", model = b1, offset = new { x = -2.5, y = 7.0, z = 0.0 }, heading = 160.0, primary_color = "black", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "biker_chopper_2", model = b2, offset = new { x = 0.0, y = 7.5, z = 0.0 }, heading = 170.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "biker_chopper_3", model = b3, offset = new { x = 2.5, y = 7.0, z = 0.0 }, heading = 190.0, primary_color = "dark_red", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "biker_chopper_4", model = b4, offset = new { x = 4.8, y = 7.0, z = 0.0 }, heading = 200.0, primary_color = "black", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }
            else if (isPolice && vehicles.Count == 0)
            {
                var pol1 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "police", "emergency" } });
                string p1 = (pol1 != null && pol1.PrimaryAsset != null) ? pol1.PrimaryAsset.ModelName : "police";

                var pol2 = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "police", "emergency" } });
                string p2 = (pol2 != null && pol2.PrimaryAsset != null) ? pol2.PrimaryAsset.ModelName : "police3";

                vehicles.Add(new { vehicle_id = "precinct_cruiser_1", model = p1, offset = new { x = -3.5, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "black", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
                vehicles.Add(new { vehicle_id = "precinct_cruiser_2", model = p2, offset = new { x = 3.5, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "black", is_engine_running = false, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (isDrugLab && vehicles.Count == 0)
            {
                var vanRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "van", "transport" } });
                string vanModel = (vanRes != null && vanRes.PrimaryAsset != null) ? vanRes.PrimaryAsset.ModelName : "burrito3";
                vehicles.Add(new { vehicle_id = "druglab_transport_van", model = vanModel, offset = new { x = 0.0, y = 8.0, z = 0.0 }, heading = 180.0, primary_color = "matte_black", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }
            else if (isDockyard && vehicles.Count == 0)
            {
                var forkRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "forklift", "industrial" } });
                string forkModel = (forkRes != null && forkRes.PrimaryAsset != null) ? forkRes.PrimaryAsset.ModelName : "forklift";
                vehicles.Add(new { vehicle_id = "dock_forklift", model = forkModel, offset = new { x = -2.0, y = 6.0, z = 0.0 }, heading = 90.0, primary_color = "yellow", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });
            }
            else if (isSmugglerHangar && vehicles.Count == 0)
            {
                var planeRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "cargo", "airplane" } })
                            ?? registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "airplane" } });
                string planeModel = (planeRes != null && planeRes.PrimaryAsset != null) ? planeRes.PrimaryAsset.ModelName : "titan";
                vehicles.Add(new { vehicle_id = "smuggler_cargo_plane", model = planeModel, offset = new { x = 0.0, y = 14.0, z = 0.0 }, heading = 180.0, primary_color = "matte_black", is_engine_running = true, sirens = false, lights_on = true, driver_entity_id = "" });

                var forkRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "forklift", "industrial" } });
                string forkModel = (forkRes != null && forkRes.PrimaryAsset != null) ? forkRes.PrimaryAsset.ModelName : "forklift";
                vehicles.Add(new { vehicle_id = "smuggler_forklift", model = forkModel, offset = new { x = -5.0, y = 6.0, z = 0.0 }, heading = 90.0, primary_color = "yellow", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });

                var flatbedRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "truck", "industrial" } });
                string flatbedModel = (flatbedRes != null && flatbedRes.PrimaryAsset != null) ? flatbedRes.PrimaryAsset.ModelName : "flatbed";
                vehicles.Add(new { vehicle_id = "smuggler_flatbed", model = flatbedModel, offset = new { x = -8.0, y = 8.0, z = 0.0 }, heading = 90.0, primary_color = "red", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }
            else if (isOutdoor && vehicles.Count == 0 && (p.Contains("camp") || p.Contains("van") || p.Contains("vehicle") || p.Contains("rv")))
            {
                var campRes = registry.Resolve(new AssetQuery { Type = AssetType.Vehicle, RequiredTags = new List<string> { "camper", "van" } });
                string cModel = (campRes != null && campRes.PrimaryAsset != null) ? campRes.PrimaryAsset.ModelName : "camper";
                vehicles.Add(new { vehicle_id = "outdoor_camp_van", model = cModel, offset = new { x = 4.5, y = 6.0, z = 0.0 }, heading = 180.0, primary_color = "white", is_engine_running = false, sirens = false, lights_on = false, driver_entity_id = "" });
            }

            // 5. Multi-Tier Architectural Hierarchy Decomposition (if vertical strata requested)
            List<object> archLayers = null;
            bool isMultiTier = (p.Contains("skyscraper") || p.Contains("tower")) && (p.Contains("bunker") || p.Contains("vault") || p.Contains("prison"));
            if (isMultiTier)
            {
                archLayers = new List<object>
                {
                    new
                    {
                        layer_index = 1,
                        layer_name = "Surface / Sky Deck",
                        target_concept = "skyscraper / penthouse",
                        resolution_type = "sky_pocket",
                        assigned_interior_id = "none (modular sky deck)",
                        transition_portal_title = "Service Elevator to Subterranean Bunker Level",
                        transition_to_layer_index = 2,
                        world_coordinates = new { x = 0.0, y = 0.0, z = 500.0 }
                    },
                    new
                    {
                        layer_index = 2,
                        layer_name = "Subterranean Layer 2",
                        target_concept = "bunker",
                        resolution_type = "existing_interior",
                        assigned_interior_id = "int_military_bunker",
                        transition_portal_title = "Blast Airlock Hatch to Casino Vault Complex",
                        transition_to_layer_index = 3,
                        world_coordinates = new { x = 899.86, y = -3246.06, z = -98.26 }
                    },
                    new
                    {
                        layer_index = 3,
                        layer_name = "Sub-Bunker Layer 3",
                        target_concept = "casino vault",
                        resolution_type = "existing_interior",
                        assigned_interior_id = "int_casino_vault",
                        transition_portal_title = "Sub-Vault Security Grate to Black Site Detention",
                        transition_to_layer_index = 4,
                        world_coordinates = new { x = 980.0, y = -100.0, z = -40.0 }
                    },
                    new
                    {
                        layer_index = 4,
                        layer_name = "Deep Subterranean Layer 4",
                        target_concept = "secret prison",
                        resolution_type = "existing_interior",
                        assigned_interior_id = "int_police_jail",
                        transition_portal_title = "Emergency Return to Casino Vault Level",
                        transition_to_layer_index = 3,
                        world_coordinates = new { x = 459.05, y = -997.05, z = 24.91 }
                    }
                };
            }

            // 6. Dynamic Scenario Director Beats
            string beatAnnouncement = "Event Escalation: Atmospheric conditions shift as tensions increase across the scene.";
            string beatType = "WORLD_EVENT";
            if (isInterrogation)
            {
                beatAnnouncement = "Hostile Interrogation Escalates: Inquisitor draws sidearm to force a confession!";
                beatType = "INTERROGATION_ESCALATION";
            }
            else if (isEmergency)
            {
                beatAnnouncement = "Critical Trauma Alert: Patient vitals destabilize as defibrillator charges!";
                beatType = "MEDICAL_RESUSCITATION";
            }
            else if (isChopShop)
            {
                beatAnnouncement = "Supercar Dismantle Milestone: Mechanics separate high-output twin-turbo powerplant!";
                beatType = "CHOP_MILESTONE";
            }
            else if (isBiker)
            {
                beatAnnouncement = "Lost MC Brotherhood Call: Exhaust rumbles outside as clubhouse enforcers toast the patch!";
                beatType = "BIKER_RALLY";
            }
            else if (isDrugLab)
            {
                beatAnnouncement = "Chemical Cook Spike: Chemical vats boil vigorously as pure crystalline product condenses!";
                beatType = "CHEMICAL_COOK_REACTION";
            }
            else if (isPolice)
            {
                beatAnnouncement = "Precinct Dispatch Radio: Priority 1 Code 3 pursuit called across all district channels!";
                beatType = "POLICE_DISPATCH";
            }
            else if (isDockyard)
            {
                beatAnnouncement = "Port Authority Alert: Coast Guard cutter spotlights illuminate harbor channel!";
                beatType = "HARBOR_ALERT";
            }
            else if (isMilitary)
            {
                beatAnnouncement = "DEFCON Alert: Base perimeter sirens echo as sentries take fortified defensive positions!";
                beatType = "MILITARY_ALERT";
            }
            else if (isSciFi)
            {
                beatAnnouncement = "Biohazard Containment Alert: Pressure seals activate as anomalous biosignals spike!";
                beatType = "BIOHAZARD_ALERT";
            }
            else if (isOutdoor)
            {
                beatAnnouncement = "Acoustic Melody: Campers gather closer to the crackling fire as evening sets in.";
                beatType = "CAMPFIRE_RELAX";
            }
            else if (isHeist)
            {
                bool isBankVault = p.Contains("bank") || p.Contains("vault") || p.Contains("deposit") || p.Contains("gold");
                if (isBankVault)
                {
                    beatAnnouncement = "Silent alarm tripped at the branch! LSPD tactical units dispatched to the vault perimeter!";
                    beatType = "POLICE_RAID";
                }
                else
                {
                    beatAnnouncement = "Heist Sync: Schematics verified and crew coordinates roles for the upcoming strike.";
                    beatType = "HEIST_PLANNING_SYNC";
                }
            }
            else if (isParty)
            {
                beatAnnouncement = "Peak Performance: The DJ drops an unreleased anthem as the crowd cheers!";
                beatType = "DJ_DROP_EVENT";
            }
            else if (isCrime)
            {
                beatAnnouncement = "LSPD tactical units detected approaching the perimeter on the police scanner!";
                beatType = "POLICE_RAID";
            }
            else if (isRace)
            {
                beatAnnouncement = "GREEN FLAG! The start signal drops and high-performance engines roar down the boulevard!";
                beatType = "STREET_RACE_START";
            }
            else if (isSmugglerHangar)
            {
                beatAnnouncement = "Cargo Clearance: Flight control clears runway for clandestine narcotics air drop!";
                beatType = "SMUGGLER_AIR_TRANSIT";
            }
            else if (isGambling)
            {
                beatAnnouncement = "All-In Showdown: The dealer calls all-in as huge chip stacks and cash are pushed into the center pot!";
                beatType = "POKER_SHOWDOWN";
            }

            beats.Add(new
            {
                beat_id = "beat_dynamic_1",
                delay_seconds = 8.0,
                trigger = "timer",
                announcement = beatAnnouncement,
                world_event = new
                {
                    event_type = beatType,
                    title = "Dynamic Narrative Escalation",
                    description = "Automated scenario director event advancing the scene narrative.",
                    vehicle_model = (isInterrogation ? "" : (isCrime ? "POLICE" : "")),
                    sirens = !isInterrogation && isCrime,
                    approach_speed = (!isInterrogation && isCrime ? 30.0 : 0.0),
                    occupants = new object[0]
                },
                mutations = new[]
                {
                    new { target_entity_id = (isInterrogation ? "npc_interrogation_subject" : (isEmergency ? "npc_injured_patient" : (isChopShop ? "npc_lead_welder" : (isBiker ? "npc_biker_president" : (isDrugLab ? "npc_head_cook" : (isPolice ? "npc_detained_suspect" : (isDockyard ? "npc_crane_operator" : (isSmugglerHangar ? "npc_smuggler_boss" : (isRace ? "npc_drag_mechanic" : (isGambling ? "npc_high_roller_player" : (npcs.Count > 0 ? "npc_canonical_1" : "npc_1"))))))))))), hostility = "friendly", give_weapon = "none", narrative_reason = "Responds dynamically to scenario progression" }
                }
            });

            var dynamicEnvData = new Dictionary<string, object>
            {
                { "environment_id", envId },
                { "name", title },
                { "archetype", archetype },
                { "description", desc },
                { "center_offset", new { x = 0.0, y = 3.5, z = 0.0 } },
                { "is_sky_pocket", isMultiTier },
                { "atmosphere", new { weather = weather, time_hours = hour, time_minutes = 0, ambient_fx = ambientFx } },
                { "spatial_requirements", new { privacy = privacy, atmosphere = atmosphereTag, required_tags = requiredTags.ToArray() } },
                { "props", props },
                { "vehicles", vehicles },
                { "npcs", npcs }
            };

            if (includeScenarioDirector)
            {
                dynamicEnvData["scenario_director"] = new { scenario_title = title, narrative_intro = desc, beats = beats };
            }

            if (isMultiTier)
            {
                dynamicEnvData["sky_pocket_altitude"] = 500.0;
                dynamicEnvData["architectural_layers"] = archLayers;
            }

            if (charAudit != null)
            {
                dynamicEnvData["character_resolution_audit"] = charAudit;
            }

            if (feasibilityAudit != null)
            {
                dynamicEnvData["spatial_feasibility_audit"] = feasibilityAudit;
            }

            var rootObj = new
            {
                action = "BUILD_ENVIRONMENT",
                data = dynamicEnvData
            };

            return JsonConvert.SerializeObject(rootObj, Formatting.Indented);
        }

        public string SynthesizeScenarioOrEventLocally(string cleanPrompt)
        {
            if (ContainsAnyWord(cleanPrompt, "scenario", "story", "director", "timeline", "beat", "escalat", "plot", "act", "twist", "script"))
            {
                var scenarioObj = new
                {
                    action = "DIRECT_SCENARIO",
                    data = new
                    {
                        scenario_title = "Dynamic Narrative Scenario",
                        narrative_intro = "Director timeline orchestrated dynamically from prompt: " + cleanPrompt,
                        target_environment_id = "",
                        beats = new[]
                        {
                            new
                            {
                                beat_id = "beat_1",
                                delay_seconds = 5.0,
                                trigger = "timer",
                                announcement = "Dynamic Narrative Escalation Triggered",
                                world_event = new
                                {
                                    event_type = "DYNAMIC_INTERVENTION",
                                    title = "Tactical Reinforcements",
                                    description = "Tactical response deployed according to scenario timeline.",
                                    vehicle_model = cleanPrompt.Contains("cop") || cleanPrompt.Contains("police") ? "POLICE" : "BALLER",
                                    sirens = cleanPrompt.Contains("cop") || cleanPrompt.Contains("police"),
                                    approach_speed = 25.0,
                                    occupants = new[]
                                    {
                                        new
                                        {
                                            name = "Reinforcement Unit",
                                            ped_model = cleanPrompt.Contains("cop") || cleanPrompt.Contains("police") ? "s_m_y_cop_01" : "g_m_y_ballasout_01",
                                            weapon = "WEAPON_PISTOL",
                                            role = "reinforcement",
                                            hostility_to_player = cleanPrompt.Contains("hostile") || cleanPrompt.Contains("attack") ? "hostile" : "neutral"
                                        }
                                    }
                                },
                                mutations = new[]
                                {
                                    new
                                    {
                                        target_entity_id = "npc_1",
                                        relationship_delta = -40,
                                        hostility = "hostile_attack",
                                        new_hidden_role = "Narrative adversary",
                                        give_weapon = "WEAPON_PISTOL",
                                        narrative_reason = "Escalates engagement according to director timeline"
                                    }
                                }
                            }
                        }
                    }
                };
                return JsonConvert.SerializeObject(scenarioObj, Formatting.Indented);
            }
            else if (cleanPrompt.Contains("hostile") || cleanPrompt.Contains("fight") || cleanPrompt.Contains("shoot") || cleanPrompt.Contains("betray"))
            {
                var mutateObj = new
                {
                    action = "MUTATE_ENTITY_STATE",
                    data = new
                    {
                        target_entity_id = "",
                        relationship_delta = -60,
                        hostility = "hostile_attack",
                        new_hidden_role = "Betrays everyone in sudden hostility",
                        give_weapon = "WEAPON_PISTOL",
                        narrative_reason = "Opens fire with sudden lethal intent"
                    }
                };
                return JsonConvert.SerializeObject(mutateObj, Formatting.Indented);
            }
            else if (cleanPrompt.Contains("stash") || cleanPrompt.Contains("cash") || cleanPrompt.Contains("chest") || cleanPrompt.Contains("loot"))
            {
                var stashObj = new
                {
                    action = "ATTACH_QUEST_OR_STASH",
                    data = new
                    {
                        title = "Secret Weapons Cache",
                        description = "Contraband container with unmarked ammunition and cash",
                        container_prop = "prop_box_ammo04a",
                        reward_cash = 3500,
                        reward_weapon = "WEAPON_MICROSMG",
                        offset = new { x = 2.0, y = 3.0, z = 0.0 }
                    }
                };
                return JsonConvert.SerializeObject(stashObj, Formatting.Indented);
            }
            else
            {
                var raidObj = new
                {
                    action = "TRIGGER_WORLD_EVENT",
                    data = new
                    {
                        event_type = "POLICE_RAID",
                        title = "Emergency Tactical Response",
                        description = "Law enforcement unit responds with sirens active",
                        vehicle_model = "POLICE",
                        sirens = true,
                        approach_speed = 30.0,
                        occupants = new[] {
                            new {
                                name = "Officer Davis",
                                ped_model = "s_m_y_cop_01",
                                weapon = "WEAPON_CARBINERIFLE",
                                role = "field_supervisor",
                                hostility_to_player = "neutral"
                            }
                        }
                    }
                };
                return JsonConvert.SerializeObject(raidObj, Formatting.Indented);
            }
        }

        private string SynthesizeEventLocally(string cleanPrompt)
        {
            return SynthesizeScenarioOrEventLocally(cleanPrompt);
        }
    }
}