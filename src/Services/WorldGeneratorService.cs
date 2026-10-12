using System;
using System.IO;
using DynamicWorldEngine.Archetypes;
using DynamicWorldEngine.Retrieval;
using DynamicWorldEngine.Spatial;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Services
{
    /// <summary>
    /// World Generator Coordinator (Phase 3: Modular Decomposition).
    /// Coordinates prompt caching, preset routing, system prompt authoring,
    /// LLM transport failover, and delegates offline procedural synthesis to LocalWorldSynthesizer.
    /// Preserves 100% backward-compatible public API methods.
    /// </summary>
    public class WorldGeneratorService
    {
        private readonly PromptCache _promptCache;
        private readonly LlmTransportClient _transportClient;
        private readonly LocalWorldSynthesizer _synthesizer;

        public PromptCache Cache { get { return _promptCache; } }
        public LlmTransportClient TransportClient { get { return _transportClient; } }
        public LocalWorldSynthesizer LocalSynthesizer { get { return _synthesizer; } }

        public WorldGeneratorService(string dataFolderPath = null, string customIniPath = null)
        {
            _promptCache = new PromptCache(dataFolderPath);
            _transportClient = new LlmTransportClient(customIniPath);
            _synthesizer = new LocalWorldSynthesizer();
        }

        public WorldGeneratorService(PromptCache cache, LlmTransportClient transportClient, LocalWorldSynthesizer synthesizer)
        {
            _promptCache = cache ?? new PromptCache(null);
            _transportClient = transportClient ?? new LlmTransportClient();
            _synthesizer = synthesizer ?? new LocalWorldSynthesizer();
        }

        public string GenerateSceneJsonFromPrompt(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new ArgumentException("Prompt cannot be empty.");
            }

            string cleanPrompt = userPrompt.Trim().ToLowerInvariant();

            // Explicit Preset Fast-Path (only if user explicitly types preset:<name>)
            if (cleanPrompt.StartsWith("preset:"))
            {
                string p = cleanPrompt.Substring(7).Trim();
                string presetJson = EnvironmentCatalog.GetArchetypeJson(p);
                if (!string.IsNullOrEmpty(presetJson)) return presetJson;

                // Fallback alias resolution
                if (p == "gambling" || p == "poker") presetJson = EnvironmentCatalog.GetGamblingDenJson();
                else if (p == "chopshop" || p == "garage") presetJson = EnvironmentCatalog.GetChopShopJson();
                else if (p == "streetfood" || p == "vendor") presetJson = EnvironmentCatalog.GetStreetVendorJson();
                else if (p == "roadblock" || p == "police") presetJson = EnvironmentCatalog.GetPoliceRoadblockJson();
                else if (p == "bbq" || p == "party") presetJson = EnvironmentCatalog.GetBbqPartyJson();

                if (!string.IsNullOrEmpty(presetJson)) return presetJson;
            }

            // Local LRU Prompt Cache
            string cachedResponse;
            if (_promptCache.TryGetCachedResponse(cleanPrompt, out cachedResponse))
            {
                return cachedResponse;
            }

            string systemPrompt = GetAstraSceneArchitectSystemPrompt();
            string generatedJson = _transportClient.ExecuteGenerationWithFailover(systemPrompt, userPrompt);

            // Fallback to local synthesizer if LLM fails or is unconfigured
            if (string.IsNullOrEmpty(generatedJson))
            {
                Console.WriteLine("[WorldGeneratorService] Invoking Astra Local Semantic Synthesizer for scene: '{0}'", userPrompt);
                generatedJson = _synthesizer.SynthesizeEnvironmentLocally(userPrompt, false);
            }

            if (!string.IsNullOrEmpty(generatedJson))
            {
                try
                {
                    JObject root = JObject.Parse(generatedJson);
                    JObject dataObj = root["data"] as JObject;
                    if (dataObj != null)
                    {
                        if (dataObj["original_prompt"] == null)
                        {
                            dataObj["original_prompt"] = userPrompt;
                            generatedJson = root.ToString(Formatting.Indented);
                        }
                    }
                }
                catch { }

                _promptCache.StoreResponse(cleanPrompt, generatedJson);
            }

            return generatedJson;
        }

        public string GenerateSceneJsonFromPrompt(string userPrompt, WorldContextPacket contextPacket)
        {
            if (contextPacket != null)
            {
                string packetBrief = contextPacket.FormatForPrompt();
                string enrichedPrompt = userPrompt + "\n\n" + packetBrief;
                return GenerateSceneJsonFromPrompt(enrichedPrompt);
            }
            return GenerateSceneJsonFromPrompt(userPrompt);
        }

        public string GenerateEnvironmentJsonFromPrompt(string userPrompt)
        {
            return GenerateSceneJsonFromPrompt(userPrompt);
        }

        public string GenerateScenarioDirectorJsonFromPrompt(string userPrompt, string worldContext = "")
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
            {
                throw new ArgumentException("Prompt cannot be empty.");
            }

            string cleanPrompt = userPrompt.Trim().ToLowerInvariant();
            string cacheKey = "director_" + cleanPrompt;

            string cachedResponse;
            if (_promptCache.TryGetCachedResponse(cacheKey, out cachedResponse))
            {
                return cachedResponse;
            }

            string systemPrompt = GetAstraWorldDirectorSystemPrompt(worldContext);
            string generatedJson = _transportClient.ExecuteGenerationWithFailover(systemPrompt, userPrompt);

            if (string.IsNullOrEmpty(generatedJson))
            {
                Console.WriteLine("[WorldGeneratorService] Invoking Astra Local Semantic Synthesizer for director: '{0}'", userPrompt);
                generatedJson = _synthesizer.SynthesizeScenarioOrEventLocally(cleanPrompt);
            }

            if (!string.IsNullOrEmpty(generatedJson))
            {
                _promptCache.StoreResponse(cacheKey, generatedJson);
            }
            return generatedJson;
        }

        public string GenerateScenarioDirectorJsonFromPrompt(string userPrompt, KairosSituationBrief brief)
        {
            string context = brief != null ? brief.FormatForPrompt() : "";
            return GenerateScenarioDirectorJsonFromPrompt(userPrompt, context);
        }

        public string GenerateScenarioDirectorJsonFromPrompt(string userPrompt, WorldPerceptionSnapshot perception, KairosSituationBrief brief = null)
        {
            string context = "";
            if (perception != null) context += perception.FormatForPrompt() + "\n\n";
            if (brief != null) context += brief.FormatForPrompt();
            return GenerateScenarioDirectorJsonFromPrompt(userPrompt, context.TrimEnd());
        }

        public string GenerateMutationOrEventJsonFromPrompt(string userPrompt, string worldContext = "")
        {
            return GenerateScenarioDirectorJsonFromPrompt(userPrompt, worldContext);
        }

        public string SynthesizeEnvironmentLocally(string prompt)
        {
            return _synthesizer.SynthesizeEnvironmentLocally(prompt, false);
        }

        public string SynthesizeEnvironmentLocally(string prompt, bool includeScenarioDirector)
        {
            return _synthesizer.SynthesizeEnvironmentLocally(prompt, includeScenarioDirector);
        }

        public string SynthesizeScenarioOrEventLocally(string cleanPrompt)
        {
            return _synthesizer.SynthesizeScenarioOrEventLocally(cleanPrompt);
        }

        public string GetAstraSceneArchitectSystemPrompt()
        {
            return @"You are GPT-6 Astra: the GTA V Autonomous World Architect (Scene Generator).
The player has typed a freeform natural language prompt describing whatever their imagination desires.
You must construct a complete, immersive, living scene with themed props, authentic GTA V NPCs, vehicles, and atmosphere at t = 0.
NOTE: Do NOT force narrative script beats or premature combat shootouts into scene generation. Focus exclusively on pure spatial scene construction.
Output ONLY valid JSON matching BUILD_ENVIRONMENT schema. No markdown codeblocks.

SCHEMA:
{
  ""action"": ""BUILD_ENVIRONMENT"",
  ""data"": {
    ""environment_id"": ""env_<unique_slug>"",
    ""name"": ""<Cinematic Title>"",
    ""archetype"": ""<Thematic Category>"",
    ""description"": ""<Detailed atmospheric summary>"",
    ""center_offset"": { ""x"": 0.0, ""y"": 3.5, ""z"": 0.0 },
    ""is_sky_pocket"": false,
    ""atmosphere"": {
      ""weather"": ""<CLEAR/EXTRASUNNY/CLOUDS/OVERCAST/RAIN/THUNDER/FOGGY/SMOG>"",
      ""time_hours"": 22,
      ""time_minutes"": 0,
      ""ambient_fx"": ""<bonfire/smoke_plume/flares/sparks/none>""
    },
    ""spatial_requirements"": {
      ""privacy"": ""<open_public / commercial_indoor / luxury_estate / subterranean_bunker / hidden_basement / high_security_vault>"",
      ""atmosphere"": ""<luxury_vip / military_secure / abandoned_industrial / high_tech / street_gritty>"",
      ""required_tags"": [""<descriptive_thematic_tags_for_interior_matching>""]
    },
    ""props"": [
      { ""prop_id"": ""item_1"", ""parent_prop_id"": """", ""model"": ""<semantic_descriptor (e.g. folding_table, office_chair, ammo_crate, cash_briefcase, worklight)>"", ""offset"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 }, ""heading"": 0.0, ""is_dynamic"": false, ""is_nav_blocker"": false },
      { ""prop_id"": ""item_2"", ""parent_prop_id"": ""item_1"", ""model"": ""<child_descriptor (e.g. laptop, cash_stack, bottle, weapons_case)>"", ""offset"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.76 }, ""heading"": 0.0 }
    ],
    ""vehicles"": [
      {
        ""vehicle_id"": ""veh_1"",
        ""model"": ""<vehicle_class (e.g. police_cruiser, luxury_sedan, sports_car, helicopter, motorcycle)>"",
        ""offset"": { ""x"": -4.0, ""y"": 5.0, ""z"": 0.0 },
        ""heading"": 90.0,
        ""primary_color"": ""<black/red/white/matte_black/blue/yellow>"",
        ""is_engine_running"": true,
        ""sirens"": false,
        ""lights_on"": true,
        ""driver_entity_id"": ""<npc_id or empty>""
      }
    ],
    ""npcs"": [
      {
        ""entity_id"": ""npc_1"",
        ""name"": ""<Character Name or Canonical Identity>"",
        ""occupation"": ""<Occupation or Role>"",
        ""public_faction"": ""<Cartel / Ballas / Families / Police / Military / Civilians / Bikers / Mafia>"",
        ""hidden_role"": ""<Secret motive, backstory or allegiance>"",
        ""ped_model"": ""<role_or_canonical_identity (e.g. cartel_soldier, police_officer, nightclub_bouncer)>"",
        ""offset"": { ""x"": 0.0, ""y"": 2.0, ""z"": 0.0 },
        ""heading"": 180.0,
        ""scenario"": ""<WORLD_HUMAN_SMOKING / WORLD_HUMAN_DRINKING / WORLD_HUMAN_BBQ / WORLD_HUMAN_PARTYING / WORLD_HUMAN_GUARD_STAND / WORLD_HUMAN_COP_IDLES / WORLD_HUMAN_VEHICLE_MECHANIC / WORLD_HUMAN_CHEERING / WORLD_HUMAN_YOGA / none>"",
        ""weapon"": ""<WEAPON_PISTOL / WEAPON_CARBINERIFLE / WEAPON_MICROSMG / WEAPON_PUMPSHOTGUN / WEAPON_KNIFE / none>"",
        ""initial_task"": ""<aim_at:npc_2 / hostage_hands_up / guard_post / cower / talk_to:npc_2 / none>"",
        ""initial_state"": ""<AGGRESSIVE / HOSTILE / CALM / PANIC / ALERT>"",
        ""relationships"": { ""Player"": 0 },
        ""initial_events"": [ { ""description"": ""<Initial Backstory Event>"", ""sentiment_tag"": ""neutral"" } ]
      }
    ],
    ""asset_transfer_audit"": {
      ""host_environment"": ""<Target venue / location ownership>"",
      ""donor_asset_origin"": ""<Faction / DLC donor origin>"",
      ""requested_asset_family"": ""<Transferred equipment / technology family>"",
      ""ontological_distinction"": ""<Explanation separating location ownership from asset origin>""
    }
  }
}

ARCHITECTURAL PRINCIPLES:
1. SEMANTIC INTENT (THE ENGRAM PRINCIPLE): Describe roles, occupations, prop types, and vehicle classes using descriptive semantic labels (e.g. 'folding_table', 'ammo_crate', 'police_cruiser', 'cartel_soldier'). You do NOT need to memorize or guess internal GTA V asset hashes — the engine's EngramResolver and asset registry deterministically query and resolve exact game models from installed game data.
2. VENUE PRIVACY & TAGS: For mansions, estates, villas, or private superyachts, set privacy to 'luxury_estate' and include relevant tags (e.g. 'mansion', 'estate', 'villa', 'yacht'). For nightclubs, lounges, or public commercial spots, use 'commercial_indoor'.
3. SPATIAL HIERARCHY: Child props attach cleanly to parent props (e.g. glassware, laptops, cash bundles, or tools placed on top of tables specify parent_prop_id and appropriate elevation z=0.76).
4. MULTI-TIER STRUCTURES: For vertical complexes combining distinct spatial realities across vertical strata (e.g. skyscraper nightclub + subterranean bunker + vault + prison), provide architectural_layers detailing each tier (sky_pocket, existing_interior) with connected pocket transitions.
5. PHYSICAL FEASIBILITY: When oversized vehicles (e.g. submarines, planes) are requested in indoor venues, declare spatial_feasibility_audit and adaptive realization strategies (such as mounting auxiliary submersibles on display platforms while linking portals to deep-water moorings).
6. INDEPENDENT DIMENSIONS: Treat environment, vehicles, characters, and events as independent semantic dimensions.
7. LOCATION OWNERSHIP VS ASSET ORIGIN: When a prompt borrows equipment, technology, or props from a specific faction or DLC (e.g. 'surveillance equipment from the FIB' in a 'police station'):
   - LOCATION OWNERSHIP: Set environment name, archetype, and spatial_requirements exclusively for the Host Environment (e.g. police station, precinct, holding cells). NEVER assign the environment or interior to the donor location (do NOT move player to FIB).
   - ASSET ORIGIN: Populate props with authentic items from the requested asset family (e.g. FIB surveillance servers, CCTV monitors, wiretap terminals).
   - EMIT ASSET TRANSFER AUDIT: Detail host_environment, donor_asset_origin, requested_asset_family, and ontological_distinction in asset_transfer_audit.
8. SPATIAL REALITY & PERCEPTION: When a '### SPATIAL PERCEPTION SNAPSHOT' is present in context:
   - Stay strictly within the Usable Footprint (Available Width and Length) when calculating prop and vehicle offsets.
   - If an alley or corridor is detected, align compound layout linearly along the clear span and utilize boundaries for cover or tactical choke points.
   - Do NOT place vehicles or structures outside clear boundaries or on steep slopes (> 15°).
   - Leverage nearby road and sidewalk access points for entry checkpoints and vehicle staging.
9. CANDIDATE SPATIAL REGIONS & INTERACTION PATTERNS: When 'Candidate Usable Regions' are listed in perception:
   - Select an optimal Candidate Region (e.g. Area_A) matching the scene's requirements (seclusion, surface, crowd density).
   - Design interaction patterns with orientation in mind (e.g. face-to-face meetings have opposing chairs facing a focal table; campfires have radial chairs facing inward; guards monitor approach paths).
   - The engine's SpatialPlacementSolver will automatically ground exact transforms, clearances, and facing headings.";
        }

        public string GetAstraWorldDirectorSystemPrompt(string worldContext = "")
        {
            return @"You are GPT-6 Astra: the GTA V AI World & Story Director (t > 0).
Your mission is to direct narrative storylines, timeline beats, tactical escalations, ambient world events, or psychological character mutations on top of the active living world without respawning or altering physical scene props.

Output EXACTLY ONE of the following 4 JSON actions matching the schema below. No markdown, no commentary:

ACTION 1: DIRECT_SCENARIO (Multi-beat storyline / scripted timeline with escalations):
{
  ""action"": ""DIRECT_SCENARIO"",
  ""data"": {
    ""scenario_title"": ""<Scenario Title>"",
    ""narrative_intro"": ""<Story intro / context>"",
    ""target_environment_id"": """",
    ""beats"": [
      {
        ""beat_id"": ""beat_1"",
        ""delay_seconds"": 5.0,
        ""trigger"": ""timer"",
        ""announcement"": ""<Dramatic announcement or twist>"",
        ""world_event"": {
          ""event_type"": ""<TACTICAL_RAID / POLICE_RESPONSE / GANG_AMBUSH / VIP_ARRIVAL / none>"",
          ""title"": ""<Event Title>"",
          ""description"": ""<Event Narrative>"",
          ""vehicle_model"": ""<POLICE / BALLER / BUZZARD / none>"",
          ""sirens"": false,
          ""approach_speed"": 25.0,
          ""occupants"": [
            { ""name"": ""<Name>"", ""ped_model"": ""<Model>"", ""weapon"": ""<Weapon>"", ""role"": ""<Role>"", ""hostility_to_player"": ""<hostile/neutral/friendly>"" }
          ]
        },
        ""mutations"": [
          { ""target_entity_id"": ""<npc_id or character name>"", ""relationship_delta"": -50, ""hostility"": ""hostile_attack"", ""give_weapon"": ""WEAPON_PISTOL"", ""narrative_reason"": ""<Reason for mutation>"" }
        ]
      }
    ]
  }
}

ACTION 2: TRIGGER_WORLD_EVENT (Single ambient event, police raid, faction arrival, or tactical intervention):
{
  ""action"": ""TRIGGER_WORLD_EVENT"",
  ""data"": {
    ""event_type"": ""POLICE_RAID"",
    ""title"": ""<Event Title>"",
    ""description"": ""<Event Description>"",
    ""vehicle_model"": ""POLICE"",
    ""sirens"": true,
    ""approach_speed"": 30.0,
    ""occupants"": [
      { ""name"": ""Officer"", ""ped_model"": ""s_m_y_cop_01"", ""weapon"": ""WEAPON_CARBINERIFLE"", ""role"": ""supervisor"", ""hostility_to_player"": ""neutral"" }
    ]
  }
}

ACTION 3: MUTATE_ENTITY_STATE (Psychological, hostility, allegiance, or equipment mutation of existing NPCs):
{
  ""action"": ""MUTATE_ENTITY_STATE"",
  ""data"": {
    ""target_entity_id"": ""<entity_id or character name>"",
    ""relationship_delta"": -50,
    ""hostility"": ""hostile_attack"",
    ""new_hidden_role"": ""<Secret motive or new allegiance>"",
    ""give_weapon"": ""WEAPON_PISTOL"",
    ""narrative_reason"": ""<Reason for mutation>""
  }
}

ACTION 4: ATTACH_QUEST_OR_STASH (Physical stash, discovery, reward cache, or narrative container):
{
  ""action"": ""ATTACH_QUEST_OR_STASH"",
  ""data"": {
    ""title"": ""<Stash / Contraband Title>"",
    ""description"": ""<Description>"",
    ""container_prop"": ""prop_box_ammo04a"",
    ""reward_cash"": 3500,
    ""reward_weapon"": ""WEAPON_MICROSMG"",
    ""offset"": { ""x"": 2.0, ""y"": 3.0, ""z"": 0.0 }
  }
}

DIRECTOR PRINCIPLES:
1. SPATIAL PERCEPTION: When a '### SPATIAL PERCEPTION SNAPSHOT' is present in context:
   - For vehicle arrivals, stage approaches along known road and street access vectors.
   - If an alley or corridor is active, leverage narrow choke points for tactical ambushes, standoffs, or police blockades.
   - Respect living entity density: avoid spawning conflicting factions unless triggering an explicit clash, drive-by, or raid.
   - If police activity is already 'Active Alert', escalate response with tactical urgency.

ACTIVE WORLD CONTEXT:
" + (string.IsNullOrEmpty(worldContext) ? "Active environment and surrounding living entities." : worldContext);
        }
    }
}
