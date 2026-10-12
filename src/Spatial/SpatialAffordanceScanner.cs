using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using DynamicWorldEngine.Database;

namespace DynamicWorldEngine.Spatial
{
    public enum AffordanceType
    {
        ArtExhibit,       // Paintings, sculptures, gallery showcases
        WaterSwimming,    // Swimming pool water body, lake, ocean
        WaterSunbathing,  // Pool deck edge, beach sand, lounge chairs
        BarService,       // Bar counter, cocktail bar, tap
        SeatingLounge,    // Couches, benches, luxury armchairs
        GamingTable,      // Roulette, poker, card tables, slot machines
        VehicleBay,       // Car lifts, engine repair benches, tool racks
        BalconyScenic,    // Railing overlooking city or valley, scenic viewpoint
        DanceFloor,       // Stage, dance floor, DJ area
        CookingGrill      // BBQ grills, food preparation counters
    }

    public class EnvironmentalInterestPoint
    {
        public string PointId { get; set; }
        public AffordanceType Type { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 ApproachPosition { get; set; }
        public float Heading { get; set; }
        public string Description { get; set; }
        public string RecommendedScenario { get; set; }
        public int MaxOccupants { get; set; }
        public int CurrentOccupants { get; set; }

        public EnvironmentalInterestPoint()
        {
            MaxOccupants = 2;
            CurrentOccupants = 0;
            RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL";
        }
    }

    /// <summary>
    /// Autonomous Spatial Affordance Scanner.
    /// Inspects the physical 3D environment in any venue (swimming pools, art exhibits, bar counters,
    /// car lifts, roulette tables, balconies) and dynamically extracts interest points for living NPC behavior.
    /// </summary>
    public static class SpatialAffordanceScanner
    {
        /// <summary>
        /// Scans an environment center for all physical and canonical affordances within the given radius.
        /// </summary>
        public static List<EnvironmentalInterestPoint> ScanInterestPoints(Vector3 centerPos, float radius = 35.0f, string venueId = "", string venueName = "")
        {
            var points = new List<EnvironmentalInterestPoint>();

            // 1. Dynamic Physical Prop Probing (GTA V Runtime)
            try
            {
                Prop[] nearbyProps = World.GetNearbyProps(centerPos, radius);
                if (nearbyProps != null && nearbyProps.Length > 0)
                {
                    int artIdx = 1;
                    int barIdx = 1;
                    int seatIdx = 1;
                    int gameIdx = 1;
                    int mechIdx = 1;

                    foreach (var prop in nearbyProps)
                    {
                        if (prop == null || !prop.Exists()) continue;

                        Vector3 pPos = prop.Position;
                        float pHeading = prop.Heading;

                        // Check model names for semantic affordances
                        string mLower = "";
                        try { mLower = prop.Model.ToString().ToLowerInvariant(); } catch { }

                        // A. Art Exhibits & Paintings
                        if (mLower.Contains("paint") || mLower.Contains("art") || mLower.Contains("frame") ||
                            mLower.Contains("sculpt") || mLower.Contains("statue") || mLower.Contains("canvas") ||
                            mLower.Contains("exhibit") || mLower.Contains("museum") || mLower.Contains("gallery"))
                        {
                            float rad = (pHeading + 180.0f) * (float)Math.PI / 180.0f;
                            Vector3 approach = new Vector3(pPos.X + (float)Math.Sin(rad) * 1.2f, pPos.Y - (float)Math.Cos(rad) * 1.2f, pPos.Z);
                            
                            points.Add(new EnvironmentalInterestPoint
                            {
                                PointId = "art_point_" + artIdx++,
                                Type = AffordanceType.ArtExhibit,
                                Position = pPos,
                                ApproachPosition = approach,
                                Heading = pHeading,
                                Description = "Art Exhibit: " + prop.Model.ToString(),
                                RecommendedScenario = (artIdx % 2 == 0) ? "WORLD_HUMAN_PAPARAZZI" : "WORLD_HUMAN_TOURIST_MAP",
                                MaxOccupants = 2
                            });
                        }
                        // B. Bar Counters & Beverage Stations
                        else if (mLower.Contains("bar") || mLower.Contains("counter") || mLower.Contains("tap") || mLower.Contains("keg"))
                        {
                            float rad = (pHeading + 180.0f) * (float)Math.PI / 180.0f;
                            Vector3 approach = new Vector3(pPos.X + (float)Math.Sin(rad) * 0.9f, pPos.Y - (float)Math.Cos(rad) * 0.9f, pPos.Z);

                            points.Add(new EnvironmentalInterestPoint
                            {
                                PointId = "bar_point_" + barIdx++,
                                Type = AffordanceType.BarService,
                                Position = pPos,
                                ApproachPosition = approach,
                                Heading = pHeading,
                                Description = "Bar Service Station",
                                RecommendedScenario = "WORLD_HUMAN_DRINKING",
                                MaxOccupants = 3
                            });
                        }
                        // C. Seating & Lounges
                        else if (mLower.Contains("chair") || mLower.Contains("couch") || mLower.Contains("sofa") || mLower.Contains("bench") || mLower.Contains("lounger"))
                        {
                            points.Add(new EnvironmentalInterestPoint
                            {
                                PointId = "seat_point_" + seatIdx++,
                                Type = AffordanceType.SeatingLounge,
                                Position = pPos,
                                ApproachPosition = pPos,
                                Heading = pHeading,
                                Description = "Lounge Seating",
                                RecommendedScenario = "PROP_HUMAN_SEAT_CHAIR",
                                MaxOccupants = 1
                            });
                        }
                        // D. Gaming Tables (Casino / Poker / Roulette)
                        else if (mLower.Contains("roulette") || mLower.Contains("blackjack") || mLower.Contains("poker") || mLower.Contains("casino") || mLower.Contains("slot"))
                        {
                            points.Add(new EnvironmentalInterestPoint
                            {
                                PointId = "game_point_" + gameIdx++,
                                Type = AffordanceType.GamingTable,
                                Position = pPos,
                                ApproachPosition = pPos,
                                Heading = pHeading,
                                Description = "Gaming Table",
                                RecommendedScenario = "WORLD_HUMAN_CHEERING",
                                MaxOccupants = 4
                            });
                        }
                        // E. Vehicle Repair & Workshop
                        else if (mLower.Contains("jack") || mLower.Contains("tool") || mLower.Contains("hoist") || mLower.Contains("lift") || mLower.Contains("workbench"))
                        {
                            points.Add(new EnvironmentalInterestPoint
                            {
                                PointId = "mech_point_" + mechIdx++,
                                Type = AffordanceType.VehicleBay,
                                Position = pPos,
                                ApproachPosition = pPos,
                                Heading = pHeading,
                                Description = "Mechanical Workstation",
                                RecommendedScenario = "WORLD_HUMAN_VEHICLE_MECHANIC",
                                MaxOccupants = 2
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SpatialAffordanceScanner] Dynamic prop scan exception: " + ex.Message);
            }

            // 2. Physical Water Body Probing (Pools, Fountains, Oceans)
            try
            {
                Vector3 waterCenter;
                float waterElev;
                float waterRad;
                if (SpatialVisionEngine.ProbeWaterSurface(centerPos, radius, out waterCenter, out waterElev, out waterRad))
                {
                    // Swimming point in pool center
                    points.Add(new EnvironmentalInterestPoint
                    {
                        PointId = "water_swimming_pool",
                        Type = AffordanceType.WaterSwimming,
                        Position = new Vector3(waterCenter.X, waterCenter.Y, waterElev - 0.5f),
                        ApproachPosition = new Vector3(waterCenter.X, waterCenter.Y, waterElev - 0.2f),
                        Heading = 0.0f,
                        Description = "Swimming Pool Deep Water",
                        RecommendedScenario = "SWIMMING",
                        MaxOccupants = 6
                    });

                    // Sunbathing points on the dry deck around water perimeter
                    float deckDist = waterRad + 1.8f;
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * (float)Math.PI * 0.5f;
                        float dx = (float)Math.Cos(angle) * deckDist;
                        float dy = (float)Math.Sin(angle) * deckDist;
                        Vector3 deckPos = new Vector3(waterCenter.X + dx, waterCenter.Y + dy, waterElev + 0.1f);
                        float heading = (float)(angle * 180.0f / Math.PI) + 180.0f;

                        points.Add(new EnvironmentalInterestPoint
                        {
                            PointId = "pool_deck_sunbathe_" + (i + 1),
                            Type = AffordanceType.WaterSunbathing,
                            Position = deckPos,
                            ApproachPosition = deckPos,
                            Heading = heading,
                            Description = "Pool Deck Sunbathing Area",
                            RecommendedScenario = "WORLD_HUMAN_SUNBATHE",
                            MaxOccupants = 2
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SpatialAffordanceScanner] Water probing exception: " + ex.Message);
            }

            // 3. Canonical Landmark Affordance Catalog (Works Offline, Unit Tests & Static Map Venues)
            string idLower = (venueId ?? "").ToLowerInvariant();
            string nameLower = (venueName ?? "").ToLowerInvariant();

            // A. The Kortz Center (Art Gallery & Museum Complex)
            if (idLower.Contains("kortz") || nameLower.Contains("kortz") || (Math.Abs(centerPos.X - (-2290.61f)) < 30.0f && Math.Abs(centerPos.Y - 364.12f) < 30.0f))
            {
                AddKortzCenterAffordances(points, centerPos);
            }
            // B. Richman Mansion (Luxury Pool Terrace, Pool Bar & Gardens)
            else if (idLower.Contains("richman") || nameLower.Contains("richman") || (Math.Abs(centerPos.X - (-1644.00f)) < 40.0f && Math.Abs(centerPos.Y - 476.00f) < 40.0f))
            {
                AddRichmanMansionAffordances(points, centerPos);
            }
            // C. Vinewood Mansion
            else if (idLower.Contains("vinewood") || nameLower.Contains("vinewood") || (Math.Abs(centerPos.X - 545.00f) < 40.0f && Math.Abs(centerPos.Y - 728.00f) < 40.0f))
            {
                AddVinewoodMansionAffordances(points, centerPos);
            }
            // D. Tongva Hills Mansion
            else if (idLower.Contains("tongva") || nameLower.Contains("tongva") || (Math.Abs(centerPos.X - (-2593.00f)) < 40.0f && Math.Abs(centerPos.Y - 1889.00f) < 40.0f))
            {
                AddTongvaMansionAffordances(points, centerPos);
            }
            // E. Interrogation Room / Torture Chamber / Covert Safehouse
            else if (idLower.Contains("torture") || nameLower.Contains("torture") || idLower.Contains("interrogat") || nameLower.Contains("interrogat") || (Math.Abs(centerPos.X - 134.40f) < 25.0f && Math.Abs(centerPos.Y - (-2203.40f)) < 25.0f))
            {
                AddInterrogationRoomAffordances(points, centerPos);
            }
            // F. Hospital / Medical Clinic / Emergency Triage
            else if (idLower.Contains("hospital") || nameLower.Contains("hospital") || idLower.Contains("clinic") || nameLower.Contains("clinic") || idLower.Contains("triage") || nameLower.Contains("triage"))
            {
                AddHospitalAffordances(points, centerPos);
            }
            // G. Chop Shop / Auto Salvage Workshop
            else if (idLower.Contains("chop") || nameLower.Contains("chop") || idLower.Contains("salvage") || nameLower.Contains("salvage") || idLower.Contains("mechanic"))
            {
                AddChopShopAffordances(points, centerPos);
            }
            // H. Biker Clubhouse / Motorcycle Gang Bar
            else if (idLower.Contains("biker") || nameLower.Contains("biker") || idLower.Contains("clubhouse") || nameLower.Contains("clubhouse"))
            {
                AddBikerClubhouseAffordances(points, centerPos);
            }
            // I. Illicit Chemical / Narcotics Lab
            else if (idLower.Contains("meth") || nameLower.Contains("meth") || idLower.Contains("cocaine") || nameLower.Contains("cocaine") || idLower.Contains("drug_lab") || nameLower.Contains("drug lab"))
            {
                AddDrugLabAffordances(points, centerPos);
            }
            // J. Police Precinct & Booking Room
            else if (idLower.Contains("police") || nameLower.Contains("police") || idLower.Contains("precinct") || nameLower.Contains("precinct") || idLower.Contains("jail") || nameLower.Contains("jail"))
            {
                AddPolicePrecinctAffordances(points, centerPos);
            }
            // K. Generic Fallback Affordance Grid for any custom venue
            else if (points.Count == 0)
            {
                AddGenericVenueAffordances(points, centerPos);
            }

            return points;
        }

        private static void AddKortzCenterAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "kortz_art_north_wing",
                Type = AffordanceType.ArtExhibit,
                Position = new Vector3(-2286.5f, 372.0f, 174.6f),
                ApproachPosition = new Vector3(-2286.5f, 370.5f, 174.6f),
                Heading = 0.0f,
                Description = "Kortz Gallery North: Modern Expressionist Masterpiece",
                RecommendedScenario = "WORLD_HUMAN_TOURIST_MAP",
                MaxOccupants = 3
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "kortz_art_east_wing",
                Type = AffordanceType.ArtExhibit,
                Position = new Vector3(-2278.0f, 364.0f, 174.6f),
                ApproachPosition = new Vector3(-2279.5f, 364.0f, 174.6f),
                Heading = 90.0f,
                Description = "Kortz Gallery East: Contemporary Bronze Sculpture",
                RecommendedScenario = "WORLD_HUMAN_PAPARAZZI",
                MaxOccupants = 3
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "kortz_art_south_wing",
                Type = AffordanceType.ArtExhibit,
                Position = new Vector3(-2288.0f, 354.0f, 174.6f),
                ApproachPosition = new Vector3(-2288.0f, 355.8f, 174.6f),
                Heading = 180.0f,
                Description = "Kortz Gallery South: Historical Canvas & Portraiture",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 3
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "kortz_art_west_wing",
                Type = AffordanceType.ArtExhibit,
                Position = new Vector3(-2298.0f, 362.0f, 174.6f),
                ApproachPosition = new Vector3(-2296.2f, 362.0f, 174.6f),
                Heading = 270.0f,
                Description = "Kortz Gallery West: Avant-Garde Visual Installation",
                RecommendedScenario = "WORLD_HUMAN_PAPARAZZI",
                MaxOccupants = 3
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "kortz_fountain_scenic",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(-2290.6f, 364.1f, 174.6f),
                ApproachPosition = new Vector3(-2290.6f, 366.5f, 174.6f),
                Heading = 180.0f,
                Description = "Kortz Central Plaza Fountain & Garden Overlook",
                RecommendedScenario = "WORLD_HUMAN_LEANING",
                MaxOccupants = 4
            });
        }

        private static void AddRichmanMansionAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "richman_swimming_pool",
                Type = AffordanceType.WaterSwimming,
                Position = new Vector3(-1645.0f, 448.0f, 127.8f),
                ApproachPosition = new Vector3(-1645.0f, 448.0f, 128.2f),
                Heading = 0.0f,
                Description = "Richman Estate Olympic Swimming Pool",
                RecommendedScenario = "SWIMMING",
                MaxOccupants = 6
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "richman_pool_deck_sunbathe",
                Type = AffordanceType.WaterSunbathing,
                Position = new Vector3(-1649.0f, 449.5f, 128.84f),
                ApproachPosition = new Vector3(-1649.0f, 449.5f, 128.84f),
                Heading = 90.0f,
                Description = "Richman Poolside Loungers & Towels",
                RecommendedScenario = "WORLD_HUMAN_SUNBATHE",
                MaxOccupants = 4
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "richman_pool_bar",
                Type = AffordanceType.BarService,
                Position = new Vector3(-1648.5f, 471.2f, 128.84f),
                ApproachPosition = new Vector3(-1647.2f, 471.2f, 128.84f),
                Heading = 270.0f,
                Description = "Richman Luxury Cocktail & Champagne Bar",
                RecommendedScenario = "WORLD_HUMAN_DRINKING",
                MaxOccupants = 4
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "richman_terrace_dancefloor",
                Type = AffordanceType.DanceFloor,
                Position = new Vector3(-1641.5f, 477.0f, 128.84f),
                ApproachPosition = new Vector3(-1641.5f, 475.5f, 128.84f),
                Heading = 0.0f,
                Description = "Richman Terrace DJ Booth & VIP Dance Stage",
                RecommendedScenario = "WORLD_HUMAN_PARTYING",
                MaxOccupants = 8
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "richman_scenic_overlook",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(-1635.0f, 465.0f, 128.84f),
                ApproachPosition = new Vector3(-1636.5f, 465.0f, 128.84f),
                Heading = 90.0f,
                Description = "Richman Estate Garden Gazebo Overlook",
                RecommendedScenario = "WORLD_HUMAN_LEANING",
                MaxOccupants = 3
            });
        }

        private static void AddVinewoodMansionAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "vinewood_pool",
                Type = AffordanceType.WaterSwimming,
                Position = new Vector3(538.0f, 720.0f, 201.0f),
                ApproachPosition = new Vector3(538.0f, 720.0f, 201.5f),
                Heading = 0.0f,
                Description = "Vinewood Mansion Infinity Pool",
                RecommendedScenario = "SWIMMING",
                MaxOccupants = 4
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "vinewood_terrace_bar",
                Type = AffordanceType.BarService,
                Position = new Vector3(548.0f, 730.0f, 202.0f),
                ApproachPosition = new Vector3(546.5f, 730.0f, 202.0f),
                Heading = 90.0f,
                Description = "Vinewood Skyline Cocktail Bar",
                RecommendedScenario = "WORLD_HUMAN_DRINKING",
                MaxOccupants = 4
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "vinewood_skyline_balcony",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(542.0f, 712.0f, 202.0f),
                ApproachPosition = new Vector3(542.0f, 714.0f, 202.0f),
                Heading = 180.0f,
                Description = "Vinewood Panoramic City Overlook",
                RecommendedScenario = "WORLD_HUMAN_LEANING",
                MaxOccupants = 3
            });
        }

        private static void AddTongvaMansionAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "tongva_pool",
                Type = AffordanceType.WaterSwimming,
                Position = new Vector3(-2585.0f, 1895.0f, 166.0f),
                ApproachPosition = new Vector3(-2585.0f, 1895.0f, 166.5f),
                Heading = 0.0f,
                Description = "Tongva Hills Vineyard Estate Pool",
                RecommendedScenario = "SWIMMING",
                MaxOccupants = 4
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "tongva_wine_terrace",
                Type = AffordanceType.BarService,
                Position = new Vector3(-2595.0f, 1885.0f, 167.0f),
                ApproachPosition = new Vector3(-2593.5f, 1885.0f, 167.0f),
                Heading = 270.0f,
                Description = "Tongva Hills Wine Tasting Pavilion",
                RecommendedScenario = "WORLD_HUMAN_DRINKING",
                MaxOccupants = 4
            });
        }

        private static void AddInterrogationRoomAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "interrogation_chair_subject",
                Type = AffordanceType.SeatingLounge,
                Position = new Vector3(center.X, center.Y + 2.5f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 2.5f, center.Z),
                Heading = 0.0f,
                Description = "Central Interrogation Chair for compromised subject",
                RecommendedScenario = "WORLD_HUMAN_SEAT_CHAIR",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "interrogation_table_tools",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X, center.Y + 4.0f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 3.5f, center.Z),
                Heading = 180.0f,
                Description = "Interrogation table with instruments and evidence",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "interrogation_guard_left",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(center.X - 2.2f, center.Y + 2.5f, center.Z),
                ApproachPosition = new Vector3(center.X - 2.2f, center.Y + 2.5f, center.Z),
                Heading = 90.0f,
                Description = "Enforcer perimeter guard position",
                RecommendedScenario = "WORLD_HUMAN_GUARD_STAND",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "interrogation_guard_right",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(center.X + 2.2f, center.Y + 2.5f, center.Z),
                ApproachPosition = new Vector3(center.X + 2.2f, center.Y + 2.5f, center.Z),
                Heading = 270.0f,
                Description = "Enforcer exit security post",
                RecommendedScenario = "WORLD_HUMAN_GUARD_STAND",
                MaxOccupants = 1
            });
        }

        private static void AddHospitalAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "hospital_gurney_patient",
                Type = AffordanceType.SeatingLounge,
                Position = new Vector3(center.X, center.Y + 2.8f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 2.8f, center.Z),
                Heading = 180.0f,
                Description = "Triage examination bed for injured patient",
                RecommendedScenario = "WORLD_HUMAN_SUNBATHE",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "hospital_doctor_station",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X - 1.2f, center.Y + 2.8f, center.Z),
                ApproachPosition = new Vector3(center.X - 1.2f, center.Y + 2.8f, center.Z),
                Heading = 90.0f,
                Description = "Attending physician surgical monitoring station",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "hospital_paramedic_station",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X + 1.2f, center.Y + 2.8f, center.Z),
                ApproachPosition = new Vector3(center.X + 1.2f, center.Y + 2.8f, center.Z),
                Heading = 270.0f,
                Description = "Emergency paramedic resuscitation post",
                RecommendedScenario = "WORLD_HUMAN_CLIPBOARD",
                MaxOccupants = 1
            });
        }

        private static void AddChopShopAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "chop_welding_station",
                Type = AffordanceType.VehicleBay,
                Position = new Vector3(center.X - 1.5f, center.Y + 3.0f, center.Z),
                ApproachPosition = new Vector3(center.X - 1.5f, center.Y + 3.0f, center.Z),
                Heading = 90.0f,
                Description = "Chassis dismantling and torch welding bay",
                RecommendedScenario = "WORLD_HUMAN_WELDING",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "chop_mechanic_station",
                Type = AffordanceType.VehicleBay,
                Position = new Vector3(center.X + 1.5f, center.Y + 3.0f, center.Z),
                ApproachPosition = new Vector3(center.X + 1.5f, center.Y + 3.0f, center.Z),
                Heading = 270.0f,
                Description = "Supercar engine component extraction bay",
                RecommendedScenario = "WORLD_HUMAN_VEHICLE_MECHANIC",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "chop_inventory_desk",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X, center.Y + 1.2f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 1.2f, center.Z),
                Heading = 0.0f,
                Description = "Salvage manifests and toolchest logging station",
                RecommendedScenario = "WORLD_HUMAN_CLIPBOARD",
                MaxOccupants = 1
            });
        }

        private static void AddBikerClubhouseAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "biker_pool_table",
                Type = AffordanceType.GamingTable,
                Position = new Vector3(center.X, center.Y + 3.2f, center.Z),
                ApproachPosition = new Vector3(center.X - 1.2f, center.Y + 3.2f, center.Z),
                Heading = 90.0f,
                Description = "Clubhouse regulation billiards & pool table",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 2
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "biker_bar_counter",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X - 2.8f, center.Y + 1.5f, center.Z),
                ApproachPosition = new Vector3(center.X - 2.2f, center.Y + 1.5f, center.Z),
                Heading = 270.0f,
                Description = "Lost MC craft beer and whiskey counter",
                RecommendedScenario = "WORLD_HUMAN_DRINKING",
                MaxOccupants = 3
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "biker_lounge_seating",
                Type = AffordanceType.SeatingLounge,
                Position = new Vector3(center.X + 2.5f, center.Y + 1.8f, center.Z),
                ApproachPosition = new Vector3(center.X + 2.5f, center.Y + 1.8f, center.Z),
                Heading = 270.0f,
                Description = "Leather armchairs and club lounge area",
                RecommendedScenario = "WORLD_HUMAN_SMOKING",
                MaxOccupants = 2
            });
        }

        private static void AddDrugLabAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "druglab_boiling_vat",
                Type = AffordanceType.CookingGrill,
                Position = new Vector3(center.X - 1.8f, center.Y + 3.0f, center.Z),
                ApproachPosition = new Vector3(center.X - 1.8f, center.Y + 2.2f, center.Z),
                Heading = 0.0f,
                Description = "Primary chemical synthesis cooking vat",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "druglab_packaging_table",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X + 1.8f, center.Y + 3.0f, center.Z),
                ApproachPosition = new Vector3(center.X + 1.8f, center.Y + 2.2f, center.Z),
                Heading = 0.0f,
                Description = "Chemical scale, packaging and burner table",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "druglab_entry_guard",
                Type = AffordanceType.BalconyScenic,
                Position = new Vector3(center.X, center.Y + 0.5f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 0.5f, center.Z),
                Heading = 180.0f,
                Description = "Cartel subterranean security entry post",
                RecommendedScenario = "WORLD_HUMAN_GUARD_STAND",
                MaxOccupants = 1
            });
        }

        private static void AddPolicePrecinctAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "police_booking_desk",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X - 1.5f, center.Y + 2.5f, center.Z),
                ApproachPosition = new Vector3(center.X - 1.5f, center.Y + 1.8f, center.Z),
                Heading = 0.0f,
                Description = "LSPD intake booking counter & logging terminal",
                RecommendedScenario = "WORLD_HUMAN_CLIPBOARD",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "police_holding_bench",
                Type = AffordanceType.SeatingLounge,
                Position = new Vector3(center.X + 2.0f, center.Y + 2.5f, center.Z),
                ApproachPosition = new Vector3(center.X + 2.0f, center.Y + 2.5f, center.Z),
                Heading = 180.0f,
                Description = "Intake bench for detained suspects awaiting processing",
                RecommendedScenario = "WORLD_HUMAN_SEAT_CHAIR",
                MaxOccupants = 1
            });

            points.Add(new EnvironmentalInterestPoint
            {
                PointId = "police_detective_station",
                Type = AffordanceType.BarService,
                Position = new Vector3(center.X, center.Y + 4.0f, center.Z),
                ApproachPosition = new Vector3(center.X, center.Y + 3.2f, center.Z),
                Heading = 0.0f,
                Description = "Detective case review desk and records terminal",
                RecommendedScenario = "WORLD_HUMAN_STAND_IMPARTIAL",
                MaxOccupants = 1
            });
        }

        private static void AddGenericVenueAffordances(List<EnvironmentalInterestPoint> points, Vector3 center)
        {
            float[] angles = new float[] { 0f, 90f, 180f, 270f };
            string[] scens = new string[] { "WORLD_HUMAN_STAND_IMPARTIAL", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_LEANING" };

            for (int i = 0; i < 4; i++)
            {
                float rad = angles[i] * (float)Math.PI / 180.0f;
                Vector3 p = new Vector3(center.X + (float)Math.Cos(rad) * 6.0f, center.Y + (float)Math.Sin(rad) * 6.0f, center.Z);
                points.Add(new EnvironmentalInterestPoint
                {
                    PointId = "generic_interest_" + (i + 1),
                    Type = AffordanceType.BalconyScenic,
                    Position = p,
                    ApproachPosition = p,
                    Heading = angles[i] + 180.0f,
                    Description = "Venue Social Activity Point " + (i + 1),
                    RecommendedScenario = scens[i],
                    MaxOccupants = 2
                });
            }
        }
    }
}
