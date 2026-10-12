using System;
using System.Collections.Generic;
using GTA.Math;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Autonomous Semantic Classification Engine.
    /// Analyzes raw discovered assets and derives semantic tags, functional affordances,
    /// and capability classifications using a generalized naming ontology without hardcoded asset lists.
    /// </summary>
    public class SemanticClassificationEngine : ISemanticEnrichmentProvider
    {
        public string ProviderName { get { return "SemanticClassificationEngine"; } }

        public void Enrich(GtaAssetDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.ModelName)) return;

            string model = def.ModelName.ToLowerInvariant();

            // Comprehensive tokenization supporting PascalCase (Cop01SMY -> cop, 01, s, m, y),
            // snake_case, hyphens, and digit boundaries.
            string separated = System.Text.RegularExpressions.Regex.Replace(def.ModelName, "([a-z])([A-Z])", "$1_$2");
            separated = System.Text.RegularExpressions.Regex.Replace(separated, "([A-Za-z])([0-9])", "$1_$2");
            separated = System.Text.RegularExpressions.Regex.Replace(separated, "([0-9])([A-Za-z])", "$1_$2");
            string[] rawTokens = separated.ToLowerInvariant().Split(new char[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var tokenList = new List<string>(rawTokens);
            if (!tokenList.Contains(model)) tokenList.Add(model);
            string[] tokens = tokenList.ToArray();

            // 1. DLC Prefix & Era Provenance Enrichment
            EnrichDlcPrefixProvenance(def, model);

            // 2. Asset Type Specific Semantic Classification
            switch (def.Type)
            {
                case AssetType.Prop:
                    ClassifyPropSemantics(def, model, tokens);
                    break;

                case AssetType.Ped:
                    ClassifyPedSemantics(def, model, tokens);
                    break;

                case AssetType.Vehicle:
                    ClassifyVehicleSemantics(def, model, tokens);
                    break;

                case AssetType.Interior:
                    ClassifyInteriorSemantics(def, model, tokens);
                    break;
            }
        }

        private void EnrichDlcPrefixProvenance(GtaAssetDefinition def, string model)
        {
            if (model.StartsWith("ba_"))
            {
                AddTag(def, "after_hours");
                AddTag(def, "nightclub");
                AddAffordance(def, "social_nightlife");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpBattle";
            }
            else if (model.StartsWith("xm_"))
            {
                AddTag(def, "doomsday");
                AddTag(def, "military_command");
                AddAffordance(def, "tactical_defense");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpChristmas2017";
            }
            else if (model.StartsWith("h4_"))
            {
                AddTag(def, "cayo_perico");
                AddTag(def, "tropical");
                AddTag(def, "cartel");
                AddAffordance(def, "island_compound");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpHeist4";
            }
            else if (model.StartsWith("vw_"))
            {
                AddTag(def, "casino");
                AddTag(def, "diamond_casino");
                AddTag(def, "luxury_vip");
                AddAffordance(def, "high_roller_entertainment");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpVinewood";
            }
            else if (model.StartsWith("sf_"))
            {
                AddTag(def, "the_contract");
                AddTag(def, "agency");
                AddTag(def, "studio");
                AddAffordance(def, "vip_contract");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpSecurity";
            }
            else if (model.StartsWith("gr_"))
            {
                AddTag(def, "gunrunning");
                AddTag(def, "bunker");
                AddAffordance(def, "blast_shelter");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpGunrunning";
            }
            else if (model.StartsWith("stt_"))
            {
                AddTag(def, "stunt");
                AddTag(def, "creator");
                AddTag(def, "race");
                AddAffordance(def, "creator_stunt");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpStunt";
            }
            else if (model.StartsWith("ar_"))
            {
                AddTag(def, "arena_war");
                AddTag(def, "creator");
                AddTag(def, "combat_arena");
                AddAffordance(def, "arena_combat");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpArena";
            }
            else if (model.StartsWith("sr_"))
            {
                AddTag(def, "special_race");
                AddTag(def, "creator");
                AddTag(def, "race");
                AddAffordance(def, "creator_race");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpSpecialRaces";
            }
            else if (model.StartsWith("ch_"))
            {
                AddTag(def, "casino_heist");
                AddTag(def, "heist");
                AddAffordance(def, "heist_equipment");
                if (string.IsNullOrEmpty(def.DlcPackOrigin)) def.DlcPackOrigin = "mpHeist3";
            }
        }

        private void ClassifyPropSemantics(GtaAssetDefinition def, string model, string[] tokens)
        {
            // Surface & Tables
            if (ContainsAny(tokens, "table", "desk", "stand", "counter", "bar", "podium", "lectern", "bench"))
            {
                def.Category = "table";
                AddAffordance(def, "can_be_surface");
                AddAffordance(def, "supports_objects");
                AddAffordance(def, "supports_seating_adjacent");
                AddTag(def, "surface");
                def.SurfaceElevation = Math.Max(def.SurfaceElevation, 0.76f);
            }

            // Seating & Furniture
            if (ContainsAny(tokens, "chair", "seat", "sofa", "couch", "stool", "lounger"))
            {
                def.Category = "seat";
                AddAffordance(def, "supports_npc_seating");
                AddAffordance(def, "rest_posture");
                AddTag(def, "seating");
            }

            // Barriers & Walls
            if (ContainsAny(tokens, "wall", "fence", "barrier", "stanchion", "gate", "door", "sandbag", "bollard", "tyre"))
            {
                def.Category = "barrier";
                AddAffordance(def, "perimeter_barrier");
                AddAffordance(def, "blocks_movement");
                def.BlocksNavMesh = true;
                AddTag(def, "barrier");

                if (ContainsAny(tokens, "gate", "door"))
                {
                    AddAffordance(def, "portal_transition");
                    AddTag(def, "portal");
                }
            }

            // Audio & DJ Equipment
            if (ContainsAny(tokens, "dj", "deck", "turntable", "speaker", "subwoofer", "audio", "mixer", "tannoy"))
            {
                def.Category = "audio";
                AddAffordance(def, "dj_equipment");
                AddAffordance(def, "audio_projection");
                AddAffordance(def, "music_production");
                AddTag(def, "dj");
                AddTag(def, "party_equipment");
                if (ContainsAny(tokens, "tannoy"))
                {
                    AddTag(def, "tannoy");
                    AddAffordance(def, "public_address");
                }
            }

            // Race Starting Structures, Gantries & Decals (Rockstar Creator)
            if (ContainsAny(tokens, "gantry", "startline", "start_line", "finishline", "flagpole", "stunt_track"))
            {
                def.Category = "race_structure";
                AddAffordance(def, "race_start_gantry");
                AddAffordance(def, "signal_lights");
                AddTag(def, "race_start");
                AddTag(def, "gantry");
                AddTag(def, "creator");
            }

            // Valuables & Heist Target Contraband
            if (ContainsAny(tokens, "cash", "gold", "loot", "briefcase", "case", "statue", "panther", "disp", "coke", "meth", "weed", "drug"))
            {
                def.Category = "contraband";
                AddAffordance(def, "heist_loot");
                AddAffordance(def, "high_value_target");
                AddAffordance(def, "contraband");
                AddTag(def, "valuable");
            }

            // Surveillance & Tactical Electronics
            if (ContainsAny(tokens, "screen", "monitor", "server", "rack", "laptop", "cctv", "camera", "satellite"))
            {
                def.Category = "electronics";
                AddAffordance(def, "surveillance");
                AddAffordance(def, "command_monitoring");
                AddAffordance(def, "tactical_planning");
                AddTag(def, "electronics");
            }

            // Hospitality & Party Supplies (Drinks & Barware)
            if (ContainsAny(tokens, "champ", "beer", "wine", "bottle", "bucket", "cooler", "cocktail", "liquor", "glass", "cup"))
            {
                def.Category = "hospitality";
                AddAffordance(def, "drinks_service");
                AddAffordance(def, "social_prop");
                AddTag(def, "party");
            }

            // Cooking & Barbecue Equipment
            if (ContainsAny(tokens, "bbq", "grill", "smoker", "cooker", "stove"))
            {
                def.Category = "cooking";
                AddAffordance(def, "food_preparation");
                AddAffordance(def, "bbq_grill");
                AddAffordance(def, "outdoor_equipment");
                AddTag(def, "bbq");
                AddTag(def, "cooking");
            }

            // Art, Paintings & Museum Exhibits
            if (ContainsAny(tokens, "paint", "art", "sculpt", "statue", "canvas", "easel", "frame", "portrait", "museum", "gallery", "exhibit"))
            {
                def.Category = "art_exhibit";
                AddAffordance(def, "art_viewing");
                AddAffordance(def, "visual_appreciation");
                AddAffordance(def, "cultural_display");
                AddTag(def, "art");
                AddTag(def, "gallery");
            }

            // Tropical Flora & Greenery
            if (ContainsAny(tokens, "palm", "plant", "tree", "flora"))
            {
                def.Category = "vegetation";
                AddAffordance(def, "tropical_nature");
                AddAffordance(def, "ambient_decoration");
                AddTag(def, "tropical");
            }

            // Submarines, Naval & Marine Hardware
            if (model.Contains("sub") || ContainsAny(tokens, "sub", "submarine", "periscope", "torpedo", "sonar", "propeller", "dive", "ballast", "hull"))
            {
                def.Category = "submersible";
                AddAffordance(def, "submersible");
                AddAffordance(def, "marine_hardware");
                AddTag(def, "submarine");
                AddTag(def, "marine");
            }
        }

        private void ClassifyPedSemantics(GtaAssetDefinition def, string model, string[] tokens)
        {
            // Unique / Canonical Characters (ig_ and csb_ prefixes)
            if (model.StartsWith("ig_") || model.StartsWith("csb_") || model.StartsWith("p_"))
            {
                def.Confidence = KnowledgeConfidence.CuratedCanonical;
                AddTag(def, "canonical_character");
                AddAffordance(def, "unique_identity");

                if (model.Contains("musician") || model.Contains("moodyman") || model.Contains("solomon") || model.Contains("dj"))
                {
                    AddRole(def, "musician");
                    AddRole(def, "party_dj");
                    AddAffordance(def, "headline_performer");
                }
                if (model.Contains("franklin") || model.Contains("lamar"))
                {
                    AddRole(def, "heist_mastermind");
                    AddRole(def, "agency_partner");
                    AddAffordance(def, "tactical_planning");
                }
                if (model.Contains("billionaire"))
                {
                    AddRole(def, "high_roller");
                    AddRole(def, "art_collector");
                    AddAffordance(def, "vip_patron");
                }
            }

            // Law Enforcement & Federal Agents
            if (ContainsAny(tokens, "cop", "police", "sheriff", "swat", "hwaycop", "snowcop", "fib", "fibsec", "fiboffice", "fibarchitect"))
            {
                AddRole(def, "law_enforcement");
                AddRole(def, "police_officer");
                AddRole(def, "desk_sergeant");
                AddRole(def, "patrol_officer");
                if (ContainsAny(tokens, "fib", "fibsec", "fiboffice", "fibarchitect"))
                {
                    AddRole(def, "fib_agent");
                    AddTag(def, "fib");
                }
                AddAffordance(def, "police_investigation");
                AddAffordance(def, "law_enforcement");
                AddTag(def, "police");
                AddTag(def, "cop");
                AddTag(def, "officer");
                AddTag(def, "law_enforcement");
            }

            // Military & Tactical Operatives
            if (ContainsAny(tokens, "marine", "blackops", "armoured", "army", "strprepd"))
            {
                AddRole(def, "military_guard");
                AddRole(def, "tactical_escort");
                AddRole(def, "military_officer");
                AddAffordance(def, "armed_defense");
                AddTag(def, "military");
                AddTag(def, "marine");
                AddTag(def, "soldier");
                AddTag(def, "military_officer");
            }

            // Scientists, Hazmat & Biohazard Researchers
            if (ContainsAny(tokens, "scientist", "hazmat", "chemsec", "doctor"))
            {
                AddRole(def, "lead_researcher");
                AddRole(def, "hazmat_specialist");
                AddAffordance(def, "scientific_research");
                AddTag(def, "scientist");
                AddTag(def, "hazmat");
                AddTag(def, "biohazard");
                AddTag(def, "research");
            }

            // Robbers & Safecrackers
            if (ContainsAny(tokens, "robber", "heist"))
            {
                AddRole(def, "heist_operative");
                AddRole(def, "safecracker");
                AddAffordance(def, "heist_breach");
                AddTag(def, "robber");
                AddTag(def, "heist");
            }

            // Beachgoers & Campers
            if (ContainsAny(tokens, "beach", "beachvesp"))
            {
                AddRole(def, "beachgoer");
                AddRole(def, "camper");
                AddAffordance(def, "outdoor_leisure");
                AddTag(def, "beach");
                AddTag(def, "outdoor");
                AddTag(def, "camper");
            }

            // Cartel & Organized Crime
            if (ContainsAny(tokens, "cartel", "cartelguards", "salva", "salvagoon", "ballas", "fam", "lost"))
            {
                AddRole(def, "cartel_enforcer");
                AddRole(def, "vault_guardian");
                AddAffordance(def, "armed_defense");
                AddTag(def, "cartel");
            }

            // VIP Clubgoers & Socialites
            if (ContainsAny(tokens, "smartcaspat", "bevhills", "studioparty", "tourist"))
            {
                AddRole(def, "vip_guest");
                AddRole(def, "socialite");
                AddAffordance(def, "social_spectator");
                AddTag(def, "vip");
            }

            // Hospitality & Venue Staff
            if (ContainsAny(tokens, "clubbar", "bartender", "bouncer"))
            {
                AddRole(def, "venue_staff");
                AddRole(def, "security");
                AddAffordance(def, "service_staff");
                AddTag(def, "staff");
            }
        }

        private void ClassifyVehicleSemantics(GtaAssetDefinition def, string model, string[] tokens)
        {
            // Military & Armored Combat
            if (ContainsAny(tokens, "khanjali", "apc", "insurgent", "insurgent2", "barrage", "halftrack", "crusader", "rhino"))
            {
                AddAffordance(def, "combat_vehicle");
                AddAffordance(def, "heavy_armor");
                AddAffordance(def, "military_defense");
                AddTag(def, "military");
                AddTag(def, "armored");
                if (ContainsAny(tokens, "rhino", "khanjali"))
                {
                    AddTag(def, "tank");
                    AddAffordance(def, "tank_artillery");
                }
            }

            // Outdoor Camping, RVs & Camper Vans
            if (ContainsAny(tokens, "camper", "journey", "surfer", "surfer2"))
            {
                AddAffordance(def, "camper_van");
                AddAffordance(def, "outdoor_transport");
                AddTag(def, "camper");
                AddTag(def, "outdoor");
                AddTag(def, "van");
            }
            if (ContainsAny(tokens, "bfinjection"))
            {
                AddAffordance(def, "offroad_buggy");
                AddTag(def, "buggy");
                AddTag(def, "offroad");
                AddTag(def, "outdoor");
            }

            // Vans, Cargo Transporters & Heist Vehicles
            if (ContainsAny(tokens, "burrito", "burrito2", "burrito3", "burrito4", "burrito5", "speedo", "speedo2", "speedo4",
                                   "rumpo", "rumpo2", "rumpo3", "youga", "youga2", "boxville", "mule", "mule2", "mule3",
                                   "pony", "pony2", "minivan", "minivan2", "stockade", "stockade3", "paradise"))
            {
                def.Category = "van";
                AddAffordance(def, "cargo_transport");
                AddAffordance(def, "utility_transport");
                AddTag(def, "van");
                AddTag(def, "transporter");
                AddTag(def, "transport");
                AddTag(def, "cargo");

                // Armored Heist & Tactical Vans
                if (ContainsAny(tokens, "speedo4", "rumpo3", "burrito", "burrito2", "burrito3", "burrito4", "burrito5", "stockade", "stockade3"))
                {
                    AddAffordance(def, "armored_transport");
                    AddAffordance(def, "heist_getaway");
                    AddTag(def, "armored");
                    AddTag(def, "heist_getaway");
                    AddTag(def, "bank_heist");
                }
            }

            // Marine & Watercraft
            if (ContainsAny(tokens, "longfin", "speeder", "toro", "dinghy", "squalo", "marquis", "seashark", "tropic", "jetmax"))
            {
                AddAffordance(def, "amphibious");
                AddAffordance(def, "watercraft");
                AddAffordance(def, "marine_transport");
                AddTag(def, "boat");
                AddTag(def, "marine");
            }

            // Submersible & Submarines
            if (model.Contains("sub") || ContainsAny(tokens, "kosatka", "avisa", "submersible", "submersible2", "submarine"))
            {
                AddAffordance(def, "submersible");
                AddAffordance(def, "deep_sea_transport");
                AddTag(def, "submarine");
                AddTag(def, "marine");

                if (def.Dimensions.X <= 1.0f && def.Dimensions.Y <= 1.0f)
                {
                    if (model.Contains("kosatka")) def.Dimensions = new Vector3(12.5f, 111.5f, 18.0f);
                    else if (model.Contains("avisa") || model.Contains("submersible")) def.Dimensions = new Vector3(3.2f, 6.5f, 3.0f);
                    else def.Dimensions = new Vector3(5.0f, 20.0f, 6.0f);
                }
            }

            // Luxury & High-Society
            if (ContainsAny(tokens, "superd", "adder", "windsor", "paragon", "nero", "cognoscenti", "osiris", "zentorno"))
            {
                AddAffordance(def, "luxury_transport");
                AddAffordance(def, "high_roller_vehicle");
                AddTag(def, "luxury");
                AddTag(def, "supercar");
            }

            // Racing & Tuner
            if (ContainsAny(tokens, "sultanrs", "elegy", "banshee", "comet", "jester", "coquette", "calico", "jesterrr"))
            {
                AddAffordance(def, "racing_vehicle");
                AddAffordance(def, "street_tuner");
                AddTag(def, "race");
                AddTag(def, "tuner");
                AddTag(def, "sports");
            }

            // Law Enforcement
            if (ContainsAny(tokens, "police", "riot", "fbi", "fbi2", "sheriff"))
            {
                AddAffordance(def, "law_enforcement");
                AddAffordance(def, "pursuit_vehicle");
                AddTag(def, "police");
            }

            // Aircraft: Airplanes, Jets & Cargo Transports
            if (ContainsAny(tokens, "titan", "cargoplane", "cuban800", "mammatus", "duster", "velum", "velum2", "shamal", "luxor", "luxor2", "miljet", "jet", "lazer", "hydra", "besra", "rogue", "bombushka", "mogul", "tula", "nokota", "pyro", "starling", "alkonost", "alphaz1", "howard"))
            {
                def.Category = "plane";
                AddAffordance(def, "aerial_transport");
                AddAffordance(def, "aviation");
                AddTag(def, "plane");
                AddTag(def, "airplane");
                AddTag(def, "aircraft");
                AddTag(def, "aviation");

                if (ContainsAny(tokens, "titan", "cargoplane", "bombushka", "cuban800", "miljet", "mogul", "tula"))
                {
                    AddAffordance(def, "cargo_air_transport");
                    AddTag(def, "cargo");
                    AddTag(def, "smuggler");
                    AddTag(def, "air_cargo");
                }
            }

            // Aircraft: Helicopters & Rotorcraft
            if (ContainsAny(tokens, "buzzard", "buzzard2", "annihilator", "savage", "cargobob", "cargobob2", "cargobob3", "cargobob4", "valkyrie", "valkyrie2", "frogger", "maverick", "polmav", "supervolito", "swift", "swift2", "volatus", "havok", "hunter", "akula"))
            {
                def.Category = "helicopter";
                AddAffordance(def, "aerial_transport");
                AddAffordance(def, "vertical_takeoff");
                AddTag(def, "helicopter");
                AddTag(def, "rotorcraft");
                AddTag(def, "aircraft");
                AddTag(def, "aviation");
            }
        }

        private void ClassifyInteriorSemantics(GtaAssetDefinition def, string model, string[] tokens)
        {
            // Also collect tokens from the friendly Name if present
            List<string> allTokens = new List<string>(tokens);
            if (!string.IsNullOrEmpty(def.Name))
            {
                string[] nameTokens = def.Name.ToLowerInvariant().Split(new char[] { '_', '-', ' ', '&', '\'', '"', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                allTokens.AddRange(nameTokens);
            }
            string[] combinedTokens = allTokens.ToArray();

            // Default spatial container affordance for any interior
            AddAffordance(def, "spatial_container");

            // Studios, Art Galleries & Workshops (Takes precedence over general mansion keywords)
            if (ContainsAny(combinedTokens, "studio", "art", "gallery", "exhibit", "exhibition", "workshop"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "mansion") def.Category = "studio";
                AddTag(def, "studio");
                AddTag(def, "art");
                AddTag(def, "gallery");
                AddTag(def, "exhibition");
                AddAffordance(def, "art_gallery");
                AddAffordance(def, "creative_space");
            }

            // Mansions & Luxury Estates (exclude if it's explicitly a studio/gallery/workshop)
            if (ContainsAny(combinedTokens, "mansion", "villa", "residence", "estate", "chateau", "richman", "vinewood", "tongva"))
            {
                if (!ContainsAny(combinedTokens, "studio", "art", "gallery", "workshop", "exhibit", "exhibition"))
                {
                    if (string.IsNullOrEmpty(def.Category)) def.Category = "mansion";
                    AddTag(def, "mansion");
                    AddTag(def, "party");
                    AddTag(def, "luxury");
                    AddTag(def, "vip");
                    AddTag(def, "estate");
                    AddTag(def, "residence");
                    AddAffordance(def, "luxury_estate");
                    AddAffordance(def, "party_venue");
                    AddAffordance(def, "vip_social");
                    AddAffordance(def, "residential_grounds");
                }
            }

            // Interrogation Rooms, Torture Chambers & Covert Safehouses / Basements
            if (ContainsAny(combinedTokens, "torture", "interrogation", "interogation", "safehouse", "hideout", "hostage", "basement", "slaughter", "meatpacking") ||
                (ContainsAny(combinedTokens, "lockup") && !ContainsAny(combinedTokens, "cocaine", "coke", "weed", "meth", "drug")))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "interrogation";
                AddTag(def, "interrogation");
                AddTag(def, "torture");
                AddTag(def, "safehouse");
                AddTag(def, "hideout");
                AddTag(def, "hostage");
                AddTag(def, "basement");
                AddTag(def, "covert");
                AddTag(def, "cartel");
                AddTag(def, "undercover");
                AddAffordance(def, "covert_interrogation");
                AddAffordance(def, "clandestine_holding");
                AddAffordance(def, "interrogation_chamber");
            }

            // Nightclubs & Social Venues
            if (ContainsAny(combinedTokens, "club", "nightclub", "lounge", "bar", "bahama", "tequi", "pub", "disco"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "nightclub";
                AddTag(def, "nightclub");
                AddTag(def, "party");
                AddTag(def, "nightlife");
                AddTag(def, "music");
                AddTag(def, "social");
                AddTag(def, "dance");
                AddAffordance(def, "social_nightlife");
                AddAffordance(def, "music_performance");
                AddAffordance(def, "party_venue");
                AddAffordance(def, "vip_social");
            }

            // Bunkers, Silos & Military
            if (ContainsAny(combinedTokens, "bunker", "missile", "silo", "facility", "hangar", "command", "mercenaries", "zancudo"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "bunker";
                AddTag(def, "bunker");
                AddTag(def, "facility");
                AddTag(def, "military");
                AddTag(def, "covert");
                AddTag(def, "defense");
                AddTag(def, "tactical");
                AddTag(def, "subterranean");
                AddAffordance(def, "blast_shelter");
                AddAffordance(def, "subterranean_bunker");
                AddAffordance(def, "tactical_defense");
            }

            // Vaults, Banks & Server Centers
            if (ContainsAny(combinedTokens, "vault", "bank", "server", "depository", "union", "pacific"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "vault";
                AddTag(def, "vault");
                AddTag(def, "bank");
                AddTag(def, "bank_vault");
                AddTag(def, "heist");
                AddTag(def, "secure");
                AddTag(def, "finance");
                AddAffordance(def, "secure_containment");
                AddAffordance(def, "heist_target");
                if (ContainsAny(combinedTokens, "server", "mainframe", "data"))
                {
                    AddTag(def, "server");
                    AddTag(def, "data_center");
                    AddTag(def, "laboratory");
                    AddTag(def, "research");
                    AddTag(def, "biohazard");
                    AddTag(def, "science");
                    AddAffordance(def, "data_center");
                    AddAffordance(def, "high_tech_research");
                }
            }

            // Penthouses & Luxury Suites
            if (ContainsAny(combinedTokens, "penthouse", "suite"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "penthouse";
                AddTag(def, "penthouse");
                AddTag(def, "luxury");
                AddTag(def, "vip");
                AddTag(def, "party");
                AddAffordance(def, "luxury_hospitality");
                AddAffordance(def, "vip_social");
                AddAffordance(def, "party_venue");
            }

            // Casinos & Gambling Floors
            if (ContainsAny(combinedTokens, "casino", "diamond"))
            {
                AddTag(def, "casino");
                if (string.IsNullOrEmpty(def.Category)) def.Category = "casino";
                AddTag(def, "gambling");
                AddTag(def, "high_roller");
                AddAffordance(def, "high_roller_entertainment");
            }

            // Music Studios & Recording
            if (ContainsAny(combinedTokens, "studio", "record", "audio", "music"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "studio";
                AddTag(def, "studio");
                AddTag(def, "music");
                AddTag(def, "recording");
                AddAffordance(def, "music_production");
                AddAffordance(def, "audio_recording");
                AddAffordance(def, "private_meeting");
            }

            // Submarines & Submersible Vessels (Separate from luxury yachts)
            if (ContainsAny(combinedTokens, "sub", "submarine", "submersible", "kosatka"))
            {
                def.Category = "submersible";
                AddTag(def, "submarine");
                AddTag(def, "submersible");
                AddTag(def, "naval");
                AddTag(def, "underwater");
                AddTag(def, "maritime");
                AddAffordance(def, "submersible");
                AddAffordance(def, "naval_vessel");
                AddAffordance(def, "deep_sea_base");
            }
            // Yachts & Luxury Maritime Vessels
            else if (ContainsAny(combinedTokens, "yacht", "carrier", "ship", "boat"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "yacht";
                AddTag(def, "yacht");
                AddTag(def, "maritime");
                AddTag(def, "luxury");
                AddTag(def, "vip");
                AddTag(def, "ocean");
                AddAffordance(def, "maritime_luxury");
                AddAffordance(def, "maritime_access");
                AddAffordance(def, "vip_social");
                AddAffordance(def, "yacht_deck");
            }

            // Biker Clubhouses & Motorcycle Gang Dens
            if (ContainsAny(combinedTokens, "biker", "clubhouse", "motorcycle", "chopper", "lost_mc", "lostmc"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "nightclub") def.Category = "biker_clubhouse";
                AddTag(def, "biker");
                AddTag(def, "clubhouse");
                AddTag(def, "motorcycle");
                AddTag(def, "chopper");
                AddTag(def, "lost_mc");
                AddTag(def, "biker_gang");
                AddTag(def, "bar");
                AddAffordance(def, "biker_hangout");
                AddAffordance(def, "gang_headquarters");
                AddAffordance(def, "bar_service");
            }

            // Hospitals, Clinics & Medical Centers
            if (ContainsAny(combinedTokens, "hospital", "clinic", "medical", "triage", "emergency", "paramedic", "doctor", "surgery", "infirmary"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "hospital";
                AddTag(def, "hospital");
                AddTag(def, "medical");
                AddTag(def, "clinic");
                AddTag(def, "emergency");
                AddTag(def, "triage");
                AddTag(def, "paramedic");
                AddTag(def, "doctor");
                AddAffordance(def, "medical_treatment");
                AddAffordance(def, "emergency_response");
            }

            // Chop Shops, Salvage Yards & Vehicle Workshops
            if (ContainsAny(combinedTokens, "salvage", "chop", "mechanic", "autoshop", "scrapyard", "workshop", "dismantle"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "warehouse") def.Category = "chop_shop";
                AddTag(def, "chop_shop");
                AddTag(def, "salvage");
                AddTag(def, "mechanic");
                AddTag(def, "workshop");
                AddTag(def, "garage");
                AddTag(def, "autoshop");
                AddTag(def, "stripping");
                AddAffordance(def, "vehicle_modification");
                AddAffordance(def, "chop_shop");
                AddAffordance(def, "vehicle_staging");
            }
            // Warehouses, Garages & Industrial
            else if (ContainsAny(combinedTokens, "warehouse", "yard", "foundry", "scrap", "garage"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "warehouse";
                AddTag(def, "warehouse");
                AddTag(def, "industrial");
                AddTag(def, "storage");
                AddAffordance(def, "industrial_storage");
            }

            // Offices, Agencies & Corporate
            if (ContainsAny(combinedTokens, "office", "agency", "tower", "executive", "arcadius", "lombank", "mazebank"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "office";
                AddTag(def, "office");
                AddTag(def, "agency");
                AddTag(def, "executive");
                AddTag(def, "business");
                AddAffordance(def, "business_operations");
            }

            // Arcades & Arenas
            if (ContainsAny(combinedTokens, "arcade", "arena"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "arcade";
                AddTag(def, "arcade");
                AddTag(def, "gaming");
                AddTag(def, "entertainment");
                AddAffordance(def, "entertainment_venue");
            }

            // Illicit Narcotics & Chemical Labs
            if (ContainsAny(combinedTokens, "meth", "weed", "cocaine", "acid", "forgery", "lab", "chemical", "cooks", "hazmat"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "drug_lab";
                AddTag(def, "lab");
                AddTag(def, "drug_lab");
                AddTag(def, "meth");
                AddTag(def, "cocaine");
                AddTag(def, "chemical");
                AddTag(def, "manufacturing");
                AddTag(def, "illicit");
                AddTag(def, "contraband");
                AddAffordance(def, "clandestine_lab");
                AddAffordance(def, "chemical_manufacturing");
            }

            // Police Stations, Precincts & Law Enforcement Lockups
            if (ContainsAny(combinedTokens, "jail", "prison", "holding_cell", "sheriff", "police", "precinct", "booking_desk") ||
                (ContainsAny(combinedTokens, "holding", "cell", "lockup", "booking") && !ContainsAny(combinedTokens, "cocaine", "coke", "weed", "meth", "drug")))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "police_station";
                AddTag(def, "police");
                AddTag(def, "police_station");
                AddTag(def, "precinct");
                AddTag(def, "cop");
                AddTag(def, "prison");
                AddTag(def, "jail");
                AddTag(def, "lockup");
                AddTag(def, "law_enforcement");
                AddAffordance(def, "containment_facility");
                AddAffordance(def, "law_enforcement");
                AddAffordance(def, "police_operations");
            }

            // Cargo Ships, Docks & Port Shipping
            if (ContainsAny(combinedTokens, "cargo", "freighter", "dockyard", "shipping", "container", "smuggling", "port"))
            {
                if (string.IsNullOrEmpty(def.Category)) def.Category = "cargo_ship";
                AddTag(def, "cargo_ship");
                AddTag(def, "dockyard");
                AddTag(def, "shipping");
                AddTag(def, "container");
                AddTag(def, "smuggling");
                AddTag(def, "port");
                AddAffordance(def, "cargo_logistics");
                AddAffordance(def, "contraband_smuggling");
            }

            // Open-World Airfields, Runways & Airstrips (Rockstar Creator Arenas)
            if (ContainsAny(combinedTokens, "airfield", "runway", "airstrip", "tarmac", "sandyshores", "grapeseed", "lsia", "mckenzie"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "airfield";
                AddTag(def, "airfield");
                AddTag(def, "runway");
                AddTag(def, "airstrip");
                AddTag(def, "aviation");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "runway_staging");
                AddAffordance(def, "aircraft_staging");
                AddAffordance(def, "vehicle_staging");
            }

            // Open-World Racetracks, Motocross Circuits & Drag Channels
            if (ContainsAny(combinedTokens, "motocross", "dirt_track", "racetrack", "circuit", "redwood", "cypress", "drag_strip"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "track";
                AddTag(def, "track");
                AddTag(def, "race");
                AddTag(def, "circuit");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "race_circuit");
                AddAffordance(def, "vehicle_staging");
            }

            // Urban Gang Turf, Cul-de-Sacs & Public Housing Projects
            if (ContainsAny(combinedTokens, "grovestreet", "grove_street", "culdesac", "rancho", "carson", "hood_street", "families_turf"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "hood";
                AddTag(def, "hood");
                AddTag(def, "gang");
                AddTag(def, "turf");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "street_turf");
                AddAffordance(def, "vehicle_staging");
            }

            // Coastal Beaches, Sands & Boardwalk Piers
            if (ContainsAny(combinedTokens, "delperro", "vespucci", "boardwalk", "beach_sands", "pleasure_pier"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "beach";
                AddTag(def, "beach");
                AddTag(def, "pier");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "beachfront_leisure");
                AddAffordance(def, "scenic_vista");
            }

            // Mountain Summits, Peaks & Panoramic Overlooks
            if (ContainsAny(combinedTokens, "chiliad", "summit", "observatory", "lookout", "cablecar", "overlook"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "mountain";
                AddTag(def, "mountain");
                AddTag(def, "summit");
                AddTag(def, "lookout");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "scenic_vista");
                AddAffordance(def, "panoramic_overlook");
            }

            // Industrial Infrastructure: Dams, Quarries, Power Stations & Sawmills
            if (ContainsAny(combinedTokens, "dam", "quarry", "substation", "sawmill", "power_station", "oilfield", "palmer_taylor", "davis_quartz"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "industrial";
                AddTag(def, "industrial");
                AddTag(def, "infrastructure");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "industrial_complex");
            }

            // Fortified Bases & Checkpoints (Fort Zancudo, Humane Labs)
            if (ContainsAny(combinedTokens, "zancudo", "military_base", "humane_labs", "checkpoint"))
            {
                AddTag(def, "military");
                AddTag(def, "defense");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "military_installation");
                AddAffordance(def, "tactical_defense");
            }

            // Biker Compound & Trailer Park Strongholds (Stab City)
            if (ContainsAny(combinedTokens, "stabcity", "stab_city", "trailerpark", "lostmc"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "biker_compound";
                AddTag(def, "biker");
                AddTag(def, "compound");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "biker_stronghold");
            }

            // Civic Plazas, Amphitheaters & Lakes (Legion Square, Vinewood Bowl, Mirror Park)
            if (ContainsAny(combinedTokens, "legion_square", "vinewood_bowl", "mirror_park", "amphitheater", "plaza"))
            {
                if (string.IsNullOrEmpty(def.Category) || def.Category == "uncategorized") def.Category = "plaza";
                AddTag(def, "plaza");
                AddTag(def, "outdoor");
                AddTag(def, "open_world");
                AddTag(def, "landmark");
                AddAffordance(def, "open_world");
                AddAffordance(def, "civic_gathering");
            }
        }

        private static bool ContainsAny(string[] tokens, params string[] targets)
        {
            if (tokens == null || targets == null) return false;
            foreach (var tok in tokens)
            {
                string tLower = tok.ToLowerInvariant();
                foreach (var tgt in targets)
                {
                    string tgLower = tgt.ToLowerInvariant();
                    if (tLower.Equals(tgLower, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (tgLower.Length >= 4 && tLower.Contains(tgLower))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void AddTag(GtaAssetDefinition def, string tag)
        {
            if (!def.Tags.Contains(tag)) def.Tags.Add(tag);
        }

        private static void AddAffordance(GtaAssetDefinition def, string affordance)
        {
            if (!def.Affordances.Contains(affordance)) def.Affordances.Add(affordance);
        }

        private static void AddRole(GtaAssetDefinition def, string role)
        {
            if (!def.Roles.Contains(role)) def.Roles.Add(role);
        }
    }
}
