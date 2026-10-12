using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DynamicWorldEngine.Commands;
using GTA.Math;

namespace DynamicWorldEngine.Spatial
{
    /// <summary>
    /// Affordance-Aware Living Crowd Scaling.
    /// Analyzes venue interest points and thematic context to populate authentic living crowds.
    /// </summary>
    public static class ProceduralCrowdDirector
    {
        public static void PopulateCrowd(
            SpatialExecutionPlan plan,
            BuildEnvironmentData data,
            Vector3 centerPos,
            string fullQuery)
        {
            if (plan == null || data == null) return;

            string sceneText = (plan.Name + " " + plan.Archetype + " " + (data.Description ?? "") + " " + (data.OriginalPrompt ?? "")).ToLowerInvariant();
            bool isBankHeistScene = plan.Archetype.Contains("bank_heist") || (sceneText.Contains("vault") && sceneText.Contains("heist")) || (sceneText.Contains("bank") && sceneText.Contains("robbery"));
            bool isHeistPlanning = (sceneText.Contains("planning") || sceneText.Contains("blueprint") || plan.Archetype.Contains("clandestine_planning")) && !isBankHeistScene;
            bool isInterrogationScene = sceneText.Contains("interrogat") || sceneText.Contains("torture") || sceneText.Contains("hostage") || sceneText.Contains("safehouse");
            bool isBikerScene = plan.Archetype.Contains("biker") || sceneText.Contains("biker") || sceneText.Contains("motorcycle") || sceneText.Contains("lost mc") || sceneText.Contains("clubhouse");
            bool isMilitaryScene = !sceneText.Contains("race") && (plan.Archetype.Contains("military") || sceneText.Contains("military") || sceneText.Contains("marine") || sceneText.Contains("checkpoint") || sceneText.Contains("army"));
            bool isRaceScene = !sceneText.Contains("conference") && !sceneText.Contains("summit") && (plan.Archetype.Contains("racing") || sceneText.Contains("race") || sceneText.Contains("drag strip") || sceneText.Contains("car meet") || sceneText.Contains("tuner"));
            bool isSmugglerHangarScene = plan.Archetype.Contains("smuggler_hangar") || sceneText.Contains("hangar") || sceneText.Contains("airstrip") || sceneText.Contains("airfield");
            bool isDockyardScene = plan.Archetype.Contains("dockyard") || sceneText.Contains("dockyard") || sceneText.Contains("docks") || sceneText.Contains("harbor") || sceneText.Contains("longshoreman");
            bool isEmergencyScene = plan.Archetype.Contains("medical") || plan.Archetype.Contains("emergency") || sceneText.Contains("hospital") || sceneText.Contains("triage") || sceneText.Contains("paramedic") || sceneText.Contains("trauma");
            bool isPoliceScene = !isInterrogationScene && (plan.Archetype.Contains("police") || sceneText.Contains("police station") || sceneText.Contains("precinct") || sceneText.Contains("booking desk"));
            bool isDrugLabScene = !isInterrogationScene && (plan.Archetype.Contains("drug") || plan.Archetype.Contains("illicit_manufacturing") || sceneText.Contains("meth") || sceneText.Contains("chemical narcotics") || sceneText.Contains("cook vat"));
            bool isGamblingScene = !isInterrogationScene && !sceneText.Contains("conference") && !sceneText.Contains("summit") && (plan.Archetype.Contains("gambling") || sceneText.Contains("gambling") || sceneText.Contains("poker") || sceneText.Contains("blackjack") || (sceneText.Contains("high-stakes") && (sceneText.Contains("cards") || sceneText.Contains("chips") || sceneText.Contains("bet") || sceneText.Contains("tournament") || sceneText.Contains("casino") || sceneText.Contains("croupier"))));

            bool isThematicNonParty = isHeistPlanning || isInterrogationScene || isBikerScene || isMilitaryScene || isRaceScene || isBankHeistScene || isSmugglerHangarScene || isDockyardScene || isEmergencyScene || isPoliceScene || isDrugLabScene || isGamblingScene || plan.Archetype.Contains("chop") || sceneText.Contains("conference") || sceneText.Contains("summit");
            bool isPartyScene = !isThematicNonParty && (sceneText.Contains("party") || sceneText.Contains("nightclub") || sceneText.Contains("dj") || sceneText.Contains("celebrat") || sceneText.Contains("rave") || (sceneText.Contains("club") && !sceneText.Contains("clubhouse")));
            bool isArtScene = !isThematicNonParty && Regex.IsMatch(sceneText, @"\b(art|museum|gallery|exhibit|kortz|sculpture)\b");

            string vModelName = plan.MatchedInterior != null ? plan.MatchedInterior.InteriorId : "";
            string vTitle = plan.MatchedInterior != null ? plan.MatchedInterior.Name : "";
            var affordancePoints = SpatialAffordanceScanner.ScanInterestPoints(centerPos, 35.0f, vModelName, vTitle);

            if (isPartyScene && plan.ResolvedNpcs.Count < 9)
            {
                string[] partyModels = new string[] {
                    "a_f_y_studioparty_01",
                    "a_f_y_studioparty_02",
                    "a_m_y_studioparty_01",
                    "a_m_m_studioparty_01",
                    "a_f_y_smartcaspat_01",
                    "a_m_y_smartcaspat_01",
                    "csb_billionaire",
                    "s_f_y_clubbar_02"
                };

                int needed = 9 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(101);

                var poolPoint = affordancePoints.Find(p => p.Type == AffordanceType.WaterSwimming);
                var sunPoints = affordancePoints.FindAll(p => p.Type == AffordanceType.WaterSunbathing);
                var barPoint = affordancePoints.Find(p => p.Type == AffordanceType.BarService);
                var dancePoint = affordancePoints.Find(p => p.Type == AffordanceType.DanceFloor);

                for (int i = 0; i < needed; i++)
                {
                    string gModel = partyModels[i % partyModels.Length];
                    string gScenario = "WORLD_HUMAN_PARTYING";
                    string gRole = "Party Guest";
                    string gTask = "party_roam";
                    Vector3 gPos = centerPos;
                    float gHeading = (float)(rnd.NextDouble() * 360.0);

                    if (i == 0 && poolPoint != null)
                    {
                        gModel = "a_f_y_beach_01";
                        gPos = poolPoint.Position;
                        gScenario = "SWIMMING";
                        gRole = "Pool Swimmer";
                        gTask = "swim_in_pool";
                    }
                    else if (i == 1 && poolPoint != null)
                    {
                        gModel = "a_m_y_beach_01";
                        gPos = new Vector3(poolPoint.Position.X + 2.0f, poolPoint.Position.Y - 1.5f, poolPoint.Position.Z);
                        gScenario = "SWIMMING";
                        gRole = "Pool Swimmer";
                        gTask = "swim_in_pool";
                    }
                    else if (i == 2 && sunPoints.Count > 0)
                    {
                        var sp = sunPoints[0];
                        gModel = "a_f_m_beach_01";
                        gPos = sp.ApproachPosition;
                        gHeading = sp.Heading;
                        gScenario = "WORLD_HUMAN_SUNBATHE";
                        gRole = "Pool Sunbather";
                        gTask = "sunbathe";
                    }
                    else if (i == 3 && barPoint != null)
                    {
                        gPos = barPoint.ApproachPosition;
                        gHeading = barPoint.Heading;
                        gScenario = "WORLD_HUMAN_DRINKING";
                        gRole = "Cocktail Patron";
                        gTask = "party_roam";
                    }
                    else if (i == 4 && dancePoint != null)
                    {
                        gPos = dancePoint.ApproachPosition;
                        gHeading = dancePoint.Heading;
                        gScenario = "WORLD_HUMAN_PARTYING";
                        gRole = "Party Dancer";
                        gTask = "party_roam";
                    }
                    else
                    {
                        float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                        float radius = 3.5f + (float)(rnd.NextDouble() * 7.5);
                        float guestLx = (float)Math.Cos(angle) * radius;
                        float guestLy = (float)Math.Sin(angle) * radius;

                        CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                            centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                            guestLx, guestLy, 0f,
                            gHeading, 0f, 0f
                        );

                        gPos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                        if (plan.MatchedInterior == null && !data.IsSkyPocket)
                        {
                            gPos = SpatialQueryService.FindGroundPosition(gPos);
                        }
                        gScenario = (i % 2 == 0) ? "WORLD_HUMAN_DRINKING" : "WORLD_HUMAN_PARTYING";
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_vip_guest_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("VIP Guest {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "High-Society VIP",
                        PublicFaction = "Civilians",
                        HiddenRole = gRole,
                        PedModel = gModel,
                        WorldPosition = gPos,
                        Heading = gHeading,
                        Scenario = gScenario,
                        Weapon = "none",
                        InitialTask = gTask,
                        InitialState = "CALM"
                    });
                }
            }
            else if (isArtScene && plan.ResolvedNpcs.Count < 6)
            {
                string[] artModels = new string[] {
                    "a_m_y_smartcaspat_01",
                    "a_f_y_smartcaspat_01",
                    "a_m_m_prolhost_01",
                    "a_f_y_tourist_01",
                    "csb_billionaire",
                    "a_f_m_downtown_01"
                };

                string[] artScenarios = new string[] {
                    "WORLD_HUMAN_TOURIST_MAP",
                    "WORLD_HUMAN_PAPARAZZI",
                    "WORLD_HUMAN_STAND_IMPARTIAL"
                };

                string[] artRoles = new string[] {
                    "Admiring contemporary gallery painting",
                    "Photographing featured bronze sculpture",
                    "Evaluating expressionist exhibition catalogue",
                    "Contemplating avant-garde art installation"
                };

                var artPoints = affordancePoints.FindAll(p => p.Type == AffordanceType.ArtExhibit || p.Type == AffordanceType.BalconyScenic);
                int needed = 6 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(202);

                for (int i = 0; i < needed; i++)
                {
                    string aModel = artModels[i % artModels.Length];
                    string aScenario = artScenarios[i % artScenarios.Length];
                    string aRole = artRoles[i % artRoles.Length];
                    Vector3 aPos = centerPos;
                    float aHeading = 0.0f;

                    if (artPoints.Count > 0)
                    {
                        var ap = artPoints[i % artPoints.Count];
                        aPos = ap.ApproachPosition;
                        aHeading = ap.Heading;
                        aScenario = ap.RecommendedScenario;
                    }
                    else
                    {
                        float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                        float radius = 2.5f + (float)(rnd.NextDouble() * 5.0);
                        aPos = new Vector3(centerPos.X + (float)Math.Cos(angle) * radius, centerPos.Y + (float)Math.Sin(angle) * radius, centerPos.Z);
                        aHeading = (float)(rnd.NextDouble() * 360.0);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_art_patron_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Gallery Patron {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Art Exhibition Visitor",
                        PublicFaction = "Civilians",
                        HiddenRole = aRole,
                        PedModel = aModel,
                        WorldPosition = aPos,
                        Heading = aHeading,
                        Scenario = aScenario,
                        Weapon = "none",
                        InitialTask = "art_tour",
                        InitialState = "CALM"
                    });
                }
            }
            else if (isBikerScene && plan.ResolvedNpcs.Count < 5)
            {
                string[] bikerModels = new string[] { "g_m_y_lost_01", "g_m_y_lost_02", "g_m_y_lost_03" };
                string[] bikerScenarios = new string[] { "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_STAND_IMPARTIAL" };
                string[] bikerRoles = new string[] { "Lost MC Prospect", "Lost MC Veteran", "Lost MC Associate" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(303);
                for (int i = 0; i < needed; i++)
                {
                    string bModel = bikerModels[i % bikerModels.Length];
                    string bScenario = bikerScenarios[i % bikerScenarios.Length];
                    string bRole = bikerRoles[i % bikerRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 2.0f + (float)(rnd.NextDouble() * 3.0);
                    Vector3 bPos = new Vector3(centerPos.X + (float)Math.Cos(angle) * radius, centerPos.Y + (float)Math.Sin(angle) * radius, centerPos.Z);
                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_biker_crowd_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Lost MC Brother {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Motorcycle Club Member",
                        PublicFaction = "Lost MC",
                        HiddenRole = bRole,
                        PedModel = bModel,
                        WorldPosition = bPos,
                        Heading = (float)(rnd.NextDouble() * 360.0),
                        Scenario = bScenario,
                        Weapon = "WEAPON_PISTOL",
                        InitialTask = "hangout",
                        InitialState = "CALM"
                    });
                }
            }
            else if (isMilitaryScene && plan.ResolvedNpcs.Count < 5)
            {
                string[] milModels = new string[] { "s_m_y_marine_01", "s_m_m_marine_01", "s_m_y_marine_02", "s_m_y_armymech_01" };
                string[] milScenarios = new string[] { "WORLD_HUMAN_GUARD_STAND", "WORLD_HUMAN_COP_IDLES", "WORLD_HUMAN_STAND_IMPARTIAL" };
                string[] milRoles = new string[] { "Tactical Checkpoint Sentinel", "Perimeter Sandbag Gunner", "Field Communications Specialist", "Garrison Patrol Soldier" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(404);
                for (int i = 0; i < needed; i++)
                {
                    string mModel = milModels[i % milModels.Length];
                    string mScenario = milScenarios[i % milScenarios.Length];
                    string mRole = milRoles[i % milRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 2.5f + (float)(rnd.NextDouble() * 3.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_military_garrison_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Marine Soldier {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Marine Tactical Specialist",
                        PublicFaction = "Military",
                        HiddenRole = mRole,
                        PedModel = mModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = mScenario,
                        Weapon = "WEAPON_CARBINERIFLE",
                        InitialTask = "guard_post",
                        InitialState = "ALERT"
                    });
                }
            }
            else if (isRaceScene && plan.ResolvedNpcs.Count < 7)
            {
                string[] raceModels = new string[] { "a_m_y_smartcaspat_01", "a_f_y_beach_01", "s_m_m_autoshop_01", "a_m_y_runner_01", "a_f_y_tourist_01" };
                string[] raceScenarios = new string[] { "WORLD_HUMAN_CHEERING", "WORLD_HUMAN_MOBILE_FILM_FLICKING", "WORLD_HUMAN_VEHICLE_MECHANIC", "WORLD_HUMAN_STAND_IMPARTIAL" };
                string[] raceRoles = new string[] { "Starting Grid Flag Marshall", "Pit Crew Tire Mechanic", "Adrenaline Spectator", "Drag Strip Streamer", "Rival Racer Spectator" };
                int needed = 7 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(505);
                for (int i = 0; i < needed; i++)
                {
                    string rModel = raceModels[i % raceModels.Length];
                    string rScenario = raceScenarios[i % raceScenarios.Length];
                    string rRole = raceRoles[i % raceRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 2.5f + (float)(rnd.NextDouble() * 4.0);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_race_crowd_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Drag Meet Crowd {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Street Racing Enthusiast",
                        PublicFaction = "Street Racers",
                        HiddenRole = rRole,
                        PedModel = rModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = rScenario,
                        Weapon = "none",
                        InitialTask = "hangout",
                        InitialState = "CALM"
                    });
                }
            }
            else if (isBankHeistScene && plan.ResolvedNpcs.Count < 6)
            {
                string[] heistModels = new string[] { "a_f_y_business_01", "a_m_m_prolhost_01", "s_m_m_security_01", "a_f_m_business_02" };
                string[] heistScenarios = new string[] { "WORLD_HUMAN_STAND_COWER", "WORLD_HUMAN_BUM_SLUMPED", "WORLD_HUMAN_STAND_COWER", "WORLD_HUMAN_STAND_IMPARTIAL" };
                string[] heistRoles = new string[] { "Cowering Bank Teller", "Subdued Vault Hostage", "Disarmed Bank Security Officer", "Held Custody Vault Witness" };
                int needed = 6 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(606);
                for (int i = 0; i < needed; i++)
                {
                    string hModel = heistModels[i % heistModels.Length];
                    string hScenario = heistScenarios[i % heistScenarios.Length];
                    string hRole = heistRoles[i % heistRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 1.5f + (float)(rnd.NextDouble() * 2.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_vault_hostage_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Bank Vault Hostage {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Bank Facility Staff",
                        PublicFaction = "Civilians",
                        HiddenRole = hRole,
                        PedModel = hModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = hScenario,
                        Weapon = (hModel == "s_m_m_security_01") ? "WEAPON_PISTOL" : "none",
                        InitialTask = "cower",
                        InitialState = "PANIC"
                    });
                }
            }
            else if ((isSmugglerHangarScene || isDockyardScene) && plan.ResolvedNpcs.Count < 5)
            {
                string[] dockModels = new string[] { "s_m_m_dockwork_01", "s_m_y_dockwork_01", "g_m_m_armgoon_01", "s_m_m_armoured_01" };
                string[] dockScenarios = new string[] { "WORLD_HUMAN_CLIPBOARD", "WORLD_HUMAN_HANG_OUT_STREET", "WORLD_HUMAN_GUARD_STAND", "WORLD_HUMAN_SMOKING" };
                string[] dockRoles = new string[] { "Logistics Cargo Handler", "Freight Manifest Inspector", "Perimeter Armed Guard", "Facility Warehouse Watcher" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(707);
                for (int i = 0; i < needed; i++)
                {
                    string dModel = dockModels[i % dockModels.Length];
                    string dScenario = dockScenarios[i % dockScenarios.Length];
                    string dRole = dockRoles[i % dockRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 2.0f + (float)(rnd.NextDouble() * 3.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_logistics_crew_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Logistics Specialist {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Airfield & Port Logistics Operator",
                        PublicFaction = "Cartel",
                        HiddenRole = dRole,
                        PedModel = dModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = dScenario,
                        Weapon = (dModel.Contains("armgoon") || dModel.Contains("armoured")) ? "WEAPON_MICROSMG" : "none",
                        InitialTask = "hangout",
                        InitialState = "ALERT"
                    });
                }
            }
            else if (isEmergencyScene && plan.ResolvedNpcs.Count < 5)
            {
                string[] medModels = new string[] { "s_f_y_scrubs_01", "s_m_m_paramedic_01", "a_f_y_smartcaspat_01", "s_m_m_doctor_01" };
                string[] medScenarios = new string[] { "WORLD_HUMAN_CLIPBOARD", "WORLD_HUMAN_STAND_IMPARTIAL", "WORLD_HUMAN_BUM_SLUMPED", "WORLD_HUMAN_STAND_IMPARTIAL" };
                string[] medRoles = new string[] { "Trauma Resuscitation Nurse", "ER Triage Specialist", "Critical Care Patient", "Admitting Field Medic" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(808);
                for (int i = 0; i < needed; i++)
                {
                    string mdModel = medModels[i % medModels.Length];
                    string mdScenario = medScenarios[i % medScenarios.Length];
                    string mdRole = medRoles[i % medRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 1.5f + (float)(rnd.NextDouble() * 2.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_hospital_staff_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Hospital Care Provider {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Emergency Medical Personnel",
                        PublicFaction = "Emergency Services",
                        HiddenRole = mdRole,
                        PedModel = mdModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = mdScenario,
                        Weapon = "none",
                        InitialTask = "triage",
                        InitialState = "CALM"
                    });
                }
            }
            else if (isPoliceScene && plan.ResolvedNpcs.Count < 5)
            {
                string[] copModels = new string[] { "s_f_y_cop_01", "s_m_y_cop_01", "s_m_m_fiboffice_01", "a_m_y_skater_01" };
                string[] copScenarios = new string[] { "WORLD_HUMAN_COP_IDLES", "WORLD_HUMAN_CLIPBOARD", "WORLD_HUMAN_STAND_IMPARTIAL", "WORLD_HUMAN_BUM_SLUMPED" };
                string[] copRoles = new string[] { "Patrol Intake Officer", "Evidence Dispatch Officer", "Major Crimes Detective", "Detained Suspect" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(909);
                for (int i = 0; i < needed; i++)
                {
                    string cpModel = copModels[i % copModels.Length];
                    string cpScenario = copScenarios[i % copScenarios.Length];
                    string cpRole = copRoles[i % copRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 1.5f + (float)(rnd.NextDouble() * 2.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_police_personnel_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Precinct Officer {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Law Enforcement Staff",
                        PublicFaction = "Police",
                        HiddenRole = cpRole,
                        PedModel = cpModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = cpScenario,
                        Weapon = (cpModel == "a_m_y_skater_01") ? "none" : "WEAPON_PISTOL",
                        InitialTask = "desk_duty",
                        InitialState = "CALM"
                    });
                }
            }
            else if (isDrugLabScene && plan.ResolvedNpcs.Count < 5)
            {
                string[] drugModels = new string[] { "mp_m_weed_01", "g_m_m_armgoon_01", "g_m_y_mexgoon_01" };
                string[] drugScenarios = new string[] { "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_GUARD_STAND", "WORLD_HUMAN_DRUG_DEALER" };
                string[] drugRoles = new string[] { "Precursor Chemical Loader", "Facility Armed Lookout", "Contraband Assayer" };
                int needed = 5 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(1010);
                for (int i = 0; i < needed; i++)
                {
                    string dgModel = drugModels[i % drugModels.Length];
                    string dgScenario = drugScenarios[i % drugScenarios.Length];
                    string dgRole = drugRoles[i % drugRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 1.5f + (float)(rnd.NextDouble() * 2.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_lab_crew_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Synthesis Operator {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "Illicit Laboratory Crew",
                        PublicFaction = "Cartel",
                        HiddenRole = dgRole,
                        PedModel = dgModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = dgScenario,
                        Weapon = (dgModel == "mp_m_weed_01") ? "none" : "WEAPON_MICROSMG",
                        InitialTask = "cook",
                        InitialState = "ALERT"
                    });
                }
            }
            else if (isGamblingScene && plan.ResolvedNpcs.Count < 6)
            {
                string[] gambModels = new string[] { "a_f_y_smartcaspat_01", "a_m_y_smartcaspat_01", "csb_billionaire", "s_m_m_security_01" };
                string[] gambScenarios = new string[] { "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_STAND_IMPARTIAL", "WORLD_HUMAN_STAND_IMPARTIAL", "WORLD_HUMAN_GUARD_STAND" };
                string[] gambRoles = new string[] { "High-Stakes Table Bettor", "VIP Tournament Observer", "Syndicate Card Sharp", "Floor Pit Security Guard" };
                int needed = 6 - plan.ResolvedNpcs.Count;
                Random rnd = new Random(1111);
                for (int i = 0; i < needed; i++)
                {
                    string gbModel = gambModels[i % gambModels.Length];
                    string gbScenario = gambScenarios[i % gambScenarios.Length];
                    string gbRole = gambRoles[i % gambRoles.Length];
                    float angle = (float)(rnd.NextDouble() * Math.PI * 2.0);
                    float radius = 1.5f + (float)(rnd.NextDouble() * 2.5);
                    float lx = (float)Math.Cos(angle) * radius;
                    float ly = (float)Math.Sin(angle) * radius;

                    CalculatedTransform gTf = PropPlacementEngine.LocalToWorldFullTransform(
                        centerPos.X, centerPos.Y, centerPos.Z, plan.Heading, 0f, 0f,
                        lx, ly, 0f,
                        (float)(rnd.NextDouble() * 360.0), 0f, 0f
                    );
                    Vector3 pos = new Vector3(gTf.X, gTf.Y, gTf.Z);
                    if (plan.MatchedInterior == null && !data.IsSkyPocket)
                    {
                        pos = SpatialQueryService.FindGroundPosition(pos);
                    }

                    plan.ResolvedNpcs.Add(new ResolvedNpcPlacement
                    {
                        EntityId = string.Format("npc_gambling_patron_{0}", plan.ResolvedNpcs.Count + 1),
                        Name = string.Format("Poker Tournament Patron {0}", plan.ResolvedNpcs.Count + 1),
                        Occupation = "High-Roller Gambler",
                        PublicFaction = "Civilians",
                        HiddenRole = gbRole,
                        PedModel = gbModel,
                        WorldPosition = pos,
                        Heading = gTf.Heading,
                        Scenario = gbScenario,
                        Weapon = (gbModel == "s_m_m_security_01") ? "WEAPON_PISTOL" : "none",
                        InitialTask = "play_cards",
                        InitialState = "CALM"
                    });
                }
            }
        }
    }
}
