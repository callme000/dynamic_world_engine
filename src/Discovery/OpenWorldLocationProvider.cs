using System;
using System.Collections.Generic;
using System.IO;
using DynamicWorldEngine.Database;
using GTA.Math;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Discovery
{
    /// <summary>
    /// Autonomous Open-World Location & Rockstar Creator Arena Provider.
    /// Discovers and registers authentic GTA V open-world landmarks, airfields, racetracks,
    /// gang cul-de-sacs, docks, and survival arenas as first-class scenario stages.
    /// </summary>
    public class OpenWorldLocationProvider : IAssetDiscoveryProvider
    {
        public string ProviderName { get { return "OpenWorldLocationProvider"; } }
        public int Priority { get { return 85; } }

        private string _manifestPath = "";
        private readonly List<GtaAssetDefinition> _discoveredLocations = new List<GtaAssetDefinition>();

        public void Initialize(SpatialAssetRegistry registry, string gtaRootPath)
        {
            _discoveredLocations.Clear();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            List<string> candidatePaths = new List<string>
            {
                Path.Combine(baseDir, "DynamicWorldEngineData", "locations.json"),
                Path.Combine(baseDir, "scripts", "DynamicWorldEngineData", "locations.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "DynamicWorldEngineData", "locations.json")
            };

            if (!string.IsNullOrEmpty(gtaRootPath))
            {
                candidatePaths.Add(Path.Combine(gtaRootPath, "scripts", "DynamicWorldEngineData", "locations.json"));
                candidatePaths.Add(Path.Combine(gtaRootPath, "DynamicWorldEngineData", "locations.json"));
            }

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    _manifestPath = path;
                    break;
                }
            }

            LoadLocations();
        }

        private void LoadLocations()
        {
            if (!string.IsNullOrEmpty(_manifestPath) && File.Exists(_manifestPath))
            {
                try
                {
                    string json = File.ReadAllText(_manifestPath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        JArray arr = JArray.Parse(json);
                        foreach (JObject item in arr)
                        {
                            try
                            {
                                var def = ParseLocationDefinition(item);
                                if (def != null)
                                {
                                    bool exists = false;
                                    for (int i = 0; i < _discoveredLocations.Count; i++)
                                    {
                                        if (string.Equals(_discoveredLocations[i].ModelName, def.ModelName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            exists = true;
                                            break;
                                        }
                                    }
                                    if (!exists)
                                    {
                                        _discoveredLocations.Add(def);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            // Always ensure the canonical 27 Rockstar Creator arenas & landmarks are populated as fallbacks
            PopulateCanonicalFallbacks();
        }

        private GtaAssetDefinition ParseLocationDefinition(JObject item)
        {
            string locId = (string)item["LocationId"];
            if (string.IsNullOrEmpty(locId)) return null;

            string name = (string)item["Name"] ?? locId;
            string cat = (string)item["Category"] ?? "open_world";
            string desc = (string)item["Description"] ?? "Authentic GTA V open-world location.";
            int capacity = item["CapacityNpcs"] != null ? (int)item["CapacityNpcs"] : 30;
            bool supportsVehicles = item["SupportsVehicles"] == null || (bool)item["SupportsVehicles"];

            Vector3 interiorCoords = Vector3.Zero;
            if (item["InteriorCoords"] != null)
            {
                float x = item["InteriorCoords"]["X"] != null ? (float)item["InteriorCoords"]["X"] : 0f;
                float y = item["InteriorCoords"]["Y"] != null ? (float)item["InteriorCoords"]["Y"] : 0f;
                float z = item["InteriorCoords"]["Z"] != null ? (float)item["InteriorCoords"]["Z"] : 0f;
                interiorCoords = new Vector3(x, y, z);
            }

            Vector3 entranceCoords = interiorCoords;
            if (item["EntranceCoords"] != null)
            {
                float ex = item["EntranceCoords"]["X"] != null ? (float)item["EntranceCoords"]["X"] : 0f;
                float ey = item["EntranceCoords"]["Y"] != null ? (float)item["EntranceCoords"]["Y"] : 0f;
                float ez = item["EntranceCoords"]["Z"] != null ? (float)item["EntranceCoords"]["Z"] : 0f;
                entranceCoords = new Vector3(ex, ey, ez);
            }

            float heading = item["InteriorHeading"] != null ? (float)item["InteriorHeading"] : 0f;

            var def = new GtaAssetDefinition
            {
                ModelName = locId,
                Name = name,
                Type = AssetType.Interior, // Registered as first-class venue stage
                Category = cat,
                Description = desc,
                Capacity = capacity,
                SupportsVehicles = supportsVehicles,
                Confidence = KnowledgeConfidence.CuratedCanonical,
                SemanticConfidenceScore = 0.98f,
                DefaultCoordinates = interiorCoords,
                EntranceCoords = entranceCoords,
                DefaultHeading = heading
            };

            // Always add base open-world tags
            def.Tags.Add("outdoor");
            def.Tags.Add("open_world");
            def.Tags.Add("landmark");
            if (!string.IsNullOrEmpty(cat) && !def.Tags.Contains(cat)) def.Tags.Add(cat);

            if (item["SemanticTags"] != null)
            {
                foreach (var t in (JArray)item["SemanticTags"])
                {
                    string tagStr = (string)t;
                    if (!string.IsNullOrEmpty(tagStr) && !def.Tags.Contains(tagStr)) def.Tags.Add(tagStr);
                }
            }

            // Identities
            def.Identities.Add(name);
            def.Identities.Add(locId);

            if (item["Keywords"] != null)
            {
                foreach (var kw in (JArray)item["Keywords"])
                {
                    string kwStr = (string)kw;
                    if (!string.IsNullOrEmpty(kwStr))
                    {
                        if (!def.Identities.Contains(kwStr)) def.Identities.Add(kwStr);
                        if (!def.Tags.Contains(kwStr)) def.Tags.Add(kwStr);
                    }
                }
            }

            // Affordances
            def.Affordances.Add("spatial_container");
            def.Affordances.Add("open_world");
            def.Affordances.Add("open_public");
            if (def.SupportsVehicles) def.Affordances.Add("vehicle_staging");
            if (def.Capacity >= 20) def.Affordances.Add("crowd_gathering");

            if (item["Privacy"] != null)
            {
                string priv = (string)item["Privacy"];
                if (!def.Tags.Contains(priv)) def.Tags.Add(priv);
                def.Affordances.Add(priv);
            }
            if (item["Atmosphere"] != null)
            {
                string atmo = (string)item["Atmosphere"];
                if (!def.Tags.Contains(atmo)) def.Tags.Add(atmo);
                def.Affordances.Add(atmo);
            }

            return def;
        }

        private void PopulateCanonicalFallbacks()
        {
            AddLocation("loc_sandyshores_airfield", "Sandy Shores Airfield", "airfield",
                "Blaine County desert airstrip and dual runway, official Rockstar drag racing and air smuggling venue.",
                new Vector3(1735.0f, 3255.0f, 41.2f), 105.0f, 35, true,
                new[] { "airfield", "airstrip", "runway", "drag", "race", "sandyshores", "desert", "blainecounty", "aviation", "plane", "hangar", "smuggler", "creator_arena" },
                new[] { "Sandy Shores Airfield", "Sandy Shores Runway", "Sandy Airstrip", "Desert Airfield", "Sandy Shores Airport", "Sandy Airfield", "Sandy Runway" });

            AddLocation("loc_grapeseed_airstrip", "Grapeseed Airstrip", "airfield",
                "McKenzie Field rural airstrip in Grapeseed, hub for contraband air drops and cropdusters.",
                new Vector3(2135.0f, 4790.0f, 41.1f), 115.0f, 25, true,
                new[] { "grapeseed", "airstrip", "airfield", "runway", "rural", "farm", "cropduster", "aviation", "smuggler", "mckenzie" },
                new[] { "Grapeseed Airstrip", "Grapeseed Airfield", "Grapeseed Runway", "McKenzie Field", "McKenzie Airstrip" });

            AddLocation("loc_lsia_runway", "LSIA Commercial Runway", "airfield",
                "Los Santos International Airport main commercial runway tarmac and aviation exhibition strip.",
                new Vector3(-1336.0f, -3044.0f, 13.9f), 60.0f, 45, true,
                new[] { "lsia", "airport", "runway", "tarmac", "aviation", "jet", "plane", "terminal", "supercar", "speedrun", "drag" },
                new[] { "LSIA Runway", "Los Santos International Airport", "LSIA Tarmac", "LSIA Airport", "LSIA Runway Tarmac", "Airport Runway" });

            AddLocation("loc_redwood_lights_track", "Redwood Lights Motocross Track", "track",
                "Official dirt and motocross circuit with dirt jumps, berms, and spectator bleachers.",
                new Vector3(1040.0f, 2650.0f, 39.5f), 180.0f, 35, true,
                new[] { "track", "motocross", "dirt_track", "redwood", "redwood_lights", "dirtbike", "offroad", "race", "buggy", "circuit", "creator_arena" },
                new[] { "Redwood Lights Track", "Redwood Lights Motocross Track", "Dirt Track", "Motocross Track", "Redwood Lights", "Redwood Motocross" });

            AddLocation("loc_cypress_flats_drag", "Cypress Flats Industrial Drag Strip", "street",
                "Straight quarter-mile industrial channel in Cypress Flats, favored for underground street drag racing.",
                new Vector3(820.0f, -2100.0f, 29.0f), 350.0f, 30, true,
                new[] { "cypressflats", "drag", "street_race", "industrial", "tuner", "carmeet", "canal", "race", "quarter_mile" },
                new[] { "Cypress Flats Drag Strip", "Cypress Flats", "Industrial Drag Strip", "Canal Drag Strip", "Cypress Drag" });

            AddLocation("loc_lscustoms_burton", "Los Santos Customs Burton Alley", "tuning",
                "Iconic Los Santos Customs automotive repair and street tuner staging alley in Burton.",
                new Vector3(-360.0f, -120.0f, 38.7f), 160.0f, 25, true,
                new[] { "lscustoms", "customs", "tuning", "carmeet", "mechanic", "autoshop", "street_tuner", "burton", "garage" },
                new[] { "Los Santos Customs", "LS Customs Burton", "LS Customs", "Customs Alley", "Burton LS Customs" });

            AddLocation("loc_vinewood_racetrack", "Vinewood Racetrack", "track",
                "Historic horse and automotive dirt oval track behind the Diamond Casino with expansive grandstands.",
                new Vector3(1150.0f, 120.0f, 81.0f), 320.0f, 50, true,
                new[] { "vinewood_racetrack", "casino_track", "racetrack", "oval", "grandstand", "race", "horse_track" },
                new[] { "Vinewood Racetrack", "Casino Racetrack", "Vinewood Oval Track", "Casino Horse Track", "Vinewood Track" });

            AddLocation("loc_grove_street", "Grove Street Cul-de-Sac", "hood",
                "Legendary circular cul-de-sac in Davis, historic turf of the Families gang.",
                new Vector3(100.0f, -1940.0f, 20.8f), 45.0f, 30, true,
                new[] { "grovestreet", "grove_street", "families", "ballas", "gang", "hood", "turf", "culdesac", "lowrider", "driveby", "davis", "ganton", "creator_arena" },
                new[] { "Grove Street", "Grove Street Cul-de-Sac", "The Cul-de-Sac", "Families Turf", "Grove St", "Davis Cul-de-Sac" });

            AddLocation("loc_rancho_projects", "Rancho Projects Carson Ave", "hood",
                "High-density public housing project courtyards along Carson Avenue, territory of the Los Santos Vagos.",
                new Vector3(350.0f, -2040.0f, 21.0f), 270.0f, 30, true,
                new[] { "rancho", "vagos", "gang", "projects", "carson", "hood", "turf", "lowrider", "drugs" },
                new[] { "Rancho Projects", "Rancho Vagos Turf", "Carson Avenue Projects", "Rancho", "Rancho Turf" });

            AddLocation("loc_el_burro_oilfields", "El Burro Heights Oilfield", "industrial",
                "Derelict hillside oil pump derricks and industrial scrap vehicle yard in El Burro Heights.",
                new Vector3(1630.0f, -2330.0f, 65.0f), 90.0f, 25, true,
                new[] { "elburro", "oilfields", "derrick", "scrapyard", "industrial", "wasteland", "chopshop", "gang", "deal", "creator_arena" },
                new[] { "El Burro Heights Oilfields", "El Burro Oilfields", "El Burro Scrapyard", "Oilfields", "El Burro Heights" });

            AddLocation("loc_stab_city", "Stab City Lost MC Compound", "biker_compound",
                "Secluded trailer park compound beside the Alamo Sea, official headquarters and arena of The Lost MC.",
                new Vector3(60.0f, 3680.0f, 39.5f), 120.0f, 35, true,
                new[] { "stabcity", "stab_city", "lostmc", "biker", "trailerpark", "compound", "chopper", "redneck", "shootout", "creator_arena" },
                new[] { "Stab City", "Lost MC Trailer Park", "Lost MC Compound", "Stab City Trailer Park", "Lost MC Camp" });

            AddLocation("loc_port_of_ls_docks", "Port of Los Santos Terminal", "docks",
                "Deep-water container shipping terminal with gantry cranes, stacked ISO containers, and freighter berths.",
                new Vector3(80.0f, -2700.0f, 6.0f), 180.0f, 35, true,
                new[] { "docks", "port", "terminal", "shipping", "freighter", "container", "crane", "cargo", "smuggling", "harbor", "elysian", "creator_arena" },
                new[] { "Port of Los Santos", "Terminal Docks", "Los Santos Docks", "Container Terminal", "Port of LS", "Terminal Container Yard" });

            AddLocation("loc_del_perro_pier", "Del Perro Pier Boardwalk", "pier",
                "Historic pleasure pier over the Pacific with carnival rides, boardwalk concessions, and ocean overlooks.",
                new Vector3(-1660.0f, -1070.0f, 13.0f), 50.0f, 45, false,
                new[] { "delperro", "del_perro_pier", "pier", "pleasure_pier", "boardwalk", "carnival", "ferris_wheel", "ocean", "beach", "creator_arena" },
                new[] { "Del Perro Pier", "Pleasure Pier", "The Pier", "Del Perro Boardwalk", "Ferris Wheel Pier" });

            AddLocation("loc_del_perro_beach", "Del Perro Beach Sands", "beach",
                "Expansive Pacific beachfront sands ideal for social bonfires, volleyball, and sunbathing.",
                new Vector3(-1500.0f, -1190.0f, 2.5f), 140.0f, 40, true,
                new[] { "beach", "del_perro_beach", "vespucci_beach", "bonfire", "sands", "surf", "swimming", "ocean", "sunbathe" },
                new[] { "Del Perro Beach", "Vespucci Beach", "Los Santos Beach", "The Beach", "Beach Sands", "Beach Bonfire" });

            AddLocation("loc_alamo_sea_marina", "Alamo Sea Marina", "marina",
                "Quiet rustic boat dock and shoreline launch on the southern edge of the Alamo Sea.",
                new Vector3(1320.0f, 4230.0f, 30.5f), 330.0f, 25, true,
                new[] { "alamo_sea", "alamo", "marina", "boatlaunch", "lakeside", "dock", "sandyshores", "water", "fishing" },
                new[] { "Alamo Sea Marina", "Alamo Sea", "Sandy Shores Marina", "Alamo Shoreline", "Alamo Boat Dock" });

            AddLocation("loc_mount_chiliad_peak", "Mount Chiliad Summit Lookout", "mountain",
                "Highest peak in San Andreas with cable car observation deck, panoramic views, and mountain wilderness.",
                new Vector3(450.0f, 5580.0f, 788.5f), 135.0f, 30, true,
                new[] { "mount_chiliad", "chiliad", "summit", "peak", "cablecar", "lookout", "mountain", "wilderness", "sniper", "camp", "overlook" },
                new[] { "Mount Chiliad", "Mount Chiliad Summit", "Chiliad Peak", "Chiliad Lookout", "Mount Chiliad Peak", "Chiliad Mountain" });

            AddLocation("loc_galileo_observatory", "Galileo Observatory Plaza", "observatory",
                "Historic planetary observatory on the Vinewood Hills ridge with grand plaza and citywide vistas.",
                new Vector3(-435.0f, 1125.0f, 325.8f), 190.0f, 35, true,
                new[] { "galileo", "observatory", "galileo_observatory", "vinewood_hills", "monument", "panoramic", "meeting", "summit", "overlook", "creator_arena" },
                new[] { "Galileo Observatory", "The Observatory", "Galileo Plaza", "Observatory Plaza", "Vinewood Observatory" });

            AddLocation("loc_land_act_dam", "Land Act Dam Overlook", "dam",
                "Massive concrete hydroelectric dam structure and reservoir bridge in the Tataviam Mountains.",
                new Vector3(1660.0f, -25.0f, 173.0f), 230.0f, 25, true,
                new[] { "land_act_dam", "dam", "reservoir", "water", "hydroelectric", "bridge", "dropoff", "standoff" },
                new[] { "Land Act Dam", "The Dam", "Land Act Reservoir", "Los Santos Dam", "Tataviam Dam" });

            AddLocation("loc_davis_quartz_quarry", "Davis Quartz Quarry Pit", "quarry",
                "Vast open-pit quarry excavation site in the Senora desert with industrial terraces and stone piles.",
                new Vector3(2940.0f, 2790.0f, 40.0f), 240.0f, 30, true,
                new[] { "quarry", "davis_quartz", "excavator", "industrial", "pit", "mining", "ambush", "dumping", "creator_arena" },
                new[] { "Davis Quartz Quarry", "Davis Quartz", "The Quarry", "Quarry Pit", "Senora Quarry" });

            AddLocation("loc_marlowe_vineyards", "Marlowe Vineyards Estate", "vineyard",
                "Picturesque rolling hillside vineyards and tasting terrace in the Tongva Valley.",
                new Vector3(-1880.0f, 2060.0f, 140.0f), 80.0f, 35, true,
                new[] { "vineyard", "winery", "tongva", "marlowe", "grapes", "wine", "estate", "tasting", "hills", "creator_arena" },
                new[] { "Marlowe Vineyards", "Tongva Valley Vineyards", "The Vineyard", "Tongva Winery", "Marlowe Estate" });

            AddLocation("loc_paleto_sawmill", "Paleto Bay Sawmill", "sawmill",
                "Sprawling logging and timber processing sawmill compound nestled in the Paleto Forest.",
                new Vector3(-570.0f, 5320.0f, 70.0f), 70.0f, 30, true,
                new[] { "sawmill", "paleto", "logging", "lumber", "forest", "industrial", "hideout", "heistprep", "creator_arena" },
                new[] { "Paleto Bay Sawmill", "Paleto Sawmill", "The Sawmill", "Logging Camp", "Paleto Forest Sawmill" });

            AddLocation("loc_vinewood_bowl", "Vinewood Bowl Amphitheater", "amphitheater",
                "Iconic open-air outdoor concert amphitheater and performance stage nestled in the Vinewood Hills.",
                new Vector3(685.0f, 570.0f, 130.0f), 180.0f, 50, false,
                new[] { "vinewood_bowl", "amphitheater", "stage", "concert", "festival", "performance", "music", "grandstand" },
                new[] { "Vinewood Bowl", "The Bowl", "Vinewood Amphitheater", "Vinewood Bowl Stage", "Vinewood Concert" });

            AddLocation("loc_mirror_park", "Mirror Park Lake Pavilion", "park",
                "Central suburban lake and island pavilion surrounded by walking paths in Mirror Park.",
                new Vector3(1040.0f, -710.0f, 57.0f), 90.0f, 30, true,
                new[] { "mirror_park", "lake", "pavilion", "island", "park", "picnic", "suburban", "pond" },
                new[] { "Mirror Park", "Mirror Park Lake", "Mirror Park Pavilion", "The Lake", "Mirror Lake" });

            AddLocation("loc_fort_zancudo_checkpoint", "Fort Zancudo Perimeter Checkpoint", "military_base",
                "Fortified main security gate and perimeter entrance into Fort Zancudo military air base.",
                new Vector3(-1600.0f, 2800.0f, 17.0f), 60.0f, 40, true,
                new[] { "fort_zancudo", "zancudo", "military_base", "military", "runway", "hangar", "checkpoint", "tank", "jet", "patrol", "army", "defense", "creator_arena" },
                new[] { "Fort Zancudo", "Fort Zancudo Runway", "Zancudo Checkpoint", "Fort Zancudo Military Base", "Military Base", "Zancudo Base" });

            AddLocation("loc_palmer_taylor_station", "Palmer-Taylor Power Station", "power_station",
                "Sprawling coastal high-voltage electrical substation and generator arrays.",
                new Vector3(2730.0f, 1530.0f, 24.5f), 270.0f, 30, true,
                new[] { "palmer_taylor", "power_station", "substation", "generators", "industrial", "sabotage", "transformers", "grid", "creator_arena" },
                new[] { "Palmer-Taylor Power Station", "Palmer Taylor Power Station", "Power Station", "Power Grid", "Palmer Taylor" });

            AddLocation("loc_humane_labs_exterior", "Humane Labs Exterior Courtyard", "research_facility",
                "High-security pharmaceutical research complex courtyard and subterranean coastal staging dock.",
                new Vector3(3616.0f, 3737.0f, 28.7f), 340.0f, 35, true,
                new[] { "humane_labs", "humanelabs", "biolab", "research", "courtyard", "covert", "blackops", "docks", "chemical" },
                new[] { "Humane Labs Exterior", "Humane Labs", "Humane Research Labs", "Humane Labs Courtyard", "Humane Labs Docks" });

            AddLocation("loc_legion_square", "Legion Square Downtown Plaza", "plaza",
                "Heart of downtown Los Santos, public gathering plaza with modern yellow sculpture and pedestrian terraces.",
                new Vector3(200.0f, -930.0f, 30.5f), 0.0f, 50, true,
                new[] { "legion_square", "legion", "plaza", "square", "downtown", "city", "protest", "rally", "sculpture", "creator_arena" },
                new[] { "Legion Square", "Legion Square Plaza", "Downtown Plaza", "The Square", "Legion Plaza" });
        }

        private void AddLocation(string id, string name, string category, string desc, Vector3 coords, float heading, int capacity, bool supportsVehicles, string[] tags, string[] keywords)
        {
            for (int i = 0; i < _discoveredLocations.Count; i++)
            {
                if (string.Equals(_discoveredLocations[i].ModelName, id, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            var def = new GtaAssetDefinition
            {
                ModelName = id,
                Name = name,
                Type = AssetType.Interior,
                Category = category,
                Description = desc,
                Capacity = capacity,
                SupportsVehicles = supportsVehicles,
                Confidence = KnowledgeConfidence.CuratedCanonical,
                SemanticConfidenceScore = 0.98f,
                DefaultCoordinates = coords,
                EntranceCoords = coords,
                DefaultHeading = heading
            };

            def.Tags.Add("outdoor");
            def.Tags.Add("open_world");
            def.Tags.Add("landmark");
            if (!string.IsNullOrEmpty(category)) def.Tags.Add(category);

            foreach (var t in tags)
            {
                if (!def.Tags.Contains(t)) def.Tags.Add(t);
            }

            def.Identities.Add(name);
            def.Identities.Add(id);

            foreach (var kw in keywords)
            {
                if (!def.Identities.Contains(kw)) def.Identities.Add(kw);
                if (!def.Tags.Contains(kw)) def.Tags.Add(kw);
            }

            def.Affordances.Add("spatial_container");
            def.Affordances.Add("open_world");
            def.Affordances.Add("open_public");
            if (supportsVehicles) def.Affordances.Add("vehicle_staging");
            if (capacity >= 20) def.Affordances.Add("crowd_gathering");

            _discoveredLocations.Add(def);
        }

        public IEnumerable<GtaAssetDefinition> ProvideAssets()
        {
            foreach (var loc in _discoveredLocations)
            {
                yield return loc;
            }
        }
    }
}
