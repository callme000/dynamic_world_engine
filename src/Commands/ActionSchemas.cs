using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Commands
{
    public class ActionPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public SpawnNpcData Data { get; set; }
    }

    public class SpawnNpcData
    {
        [JsonProperty("entity_id")]
        public string EntityId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("occupation")]
        public string Occupation { get; set; }

        [JsonProperty("public_faction")]
        public string PublicFaction { get; set; }

        [JsonProperty("hidden_role")]
        public string HiddenRole { get; set; }

        [JsonProperty("ped_model")]
        public string PedModel { get; set; }

        [JsonProperty("position_offset")]
        public Vector3Offset PositionOffset { get; set; }

        [JsonProperty("relationships")]
        public Dictionary<string, int> Relationships { get; set; }

        [JsonProperty("initial_events")]
        public List<InitialEventData> InitialEvents { get; set; }

        public SpawnNpcData()
        {
            Relationships = new Dictionary<string, int>();
            InitialEvents = new List<InitialEventData>();
            PositionOffset = new Vector3Offset();
        }
    }

    public class Vector3Offset
    {
        [JsonProperty("x")]
        public float X { get; set; }

        [JsonProperty("y")]
        public float Y { get; set; }

        [JsonProperty("z")]
        public float Z { get; set; }

        public Vector3Offset()
        {
            X = 0f;
            Y = 0f;
            Z = 0f;
        }

        public Vector3Offset(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class InitialEventConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(InitialEventData);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            if (reader.TokenType == JsonToken.String)
            {
                return new InitialEventData
                {
                    Description = (string)reader.Value,
                    SentimentTag = "neutral"
                };
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject jo = JObject.Load(reader);
                var ev = new InitialEventData();
                serializer.Populate(jo.CreateReader(), ev);
                if (string.IsNullOrEmpty(ev.Description)) ev.Description = "";
                if (string.IsNullOrEmpty(ev.SentimentTag)) ev.SentimentTag = "neutral";
                return ev;
            }

            return new InitialEventData();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var ev = value as InitialEventData;
            if (ev == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("description");
            writer.WriteValue(ev.Description ?? "");
            writer.WritePropertyName("sentiment_tag");
            writer.WriteValue(ev.SentimentTag ?? "neutral");
            writer.WriteEndObject();
        }
    }

    [JsonConverter(typeof(InitialEventConverter))]
    public class InitialEventData
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("sentiment_tag")]
        public string SentimentTag { get; set; }
    }

    // --- MULTI-CRITERIA SPATIAL REQUIREMENTS & ACCESS GATING ---

    public class SpatialRequirementsData
    {
        [JsonProperty("privacy")]
        public string Privacy { get; set; }

        [JsonProperty("atmosphere")]
        public string Atmosphere { get; set; }

        [JsonProperty("security_level")]
        public string SecurityLevel { get; set; }

        [JsonProperty("min_capacity")]
        public int MinCapacity { get; set; }

        [JsonProperty("required_tags")]
        public List<string> RequiredTags { get; set; }

        [JsonProperty("access_gating")]
        public AccessGatingData AccessGating { get; set; }

        public SpatialRequirementsData()
        {
            RequiredTags = new List<string>();
            MinCapacity = 1;
        }
    }

    public class AccessGatingData
    {
        [JsonProperty("is_locked")]
        public bool IsLocked { get; set; }

        [JsonProperty("is_known_to_player")]
        public bool IsKnownToPlayer { get; set; }

        [JsonProperty("lock_reason")]
        public string LockReason { get; set; }

        [JsonProperty("required_secret")]
        public string RequiredSecret { get; set; }

        [JsonProperty("required_faction")]
        public string RequiredFaction { get; set; }

        [JsonProperty("required_relationship")]
        public int RequiredRelationship { get; set; }

        public AccessGatingData()
        {
            IsLocked = false;
            IsKnownToPlayer = true;
            LockReason = "Locked";
            RequiredRelationship = -100;
        }
    }

    // --- INTEGRATED SCENARIO DIRECTOR & BEATS ---

    public class ScenarioDirectorData
    {
        [JsonProperty("environment_id")]
        public string EnvironmentId { get; set; }

        [JsonProperty("target_environment_id")]
        public string TargetEnvironmentId
        {
            get { return EnvironmentId; }
            set { if (!string.IsNullOrEmpty(value)) EnvironmentId = value; }
        }

        [JsonProperty("scenario_title")]
        public string ScenarioTitle { get; set; }

        [JsonProperty("narrative_intro")]
        public string NarrativeIntro { get; set; }

        [JsonProperty("beats")]
        public List<ScenarioBeatData> Beats { get; set; }

        public ScenarioDirectorData()
        {
            EnvironmentId = "";
            Beats = new List<ScenarioBeatData>();
        }
    }

    public class ScenarioBeatData
    {
        [JsonProperty("beat_id")]
        public string BeatId { get; set; }

        [JsonProperty("delay_seconds")]
        public float DelaySeconds { get; set; }

        [JsonProperty("trigger")]
        public string Trigger { get; set; } // "timer", "on_player_approach", "on_weapon_drawn", "on_combat"

        [JsonProperty("announcement")]
        public string Announcement { get; set; }

        [JsonProperty("world_event")]
        public TriggerWorldEventData WorldEvent { get; set; }

        [JsonProperty("mutations")]
        public List<MutateEntityData> Mutations { get; set; }

        public ScenarioBeatData()
        {
            DelaySeconds = 5.0f;
            Trigger = "timer";
            Mutations = new List<MutateEntityData>();
        }
    }

    // --- DIRECT SCENARIO PAYLOAD (INDEPENDENT WORLD DIRECTOR) ---

    public class DirectScenarioPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public ScenarioDirectorData Data { get; set; }

        public DirectScenarioPayload()
        {
            Action = "DIRECT_SCENARIO";
            Data = new ScenarioDirectorData();
        }
    }

    // --- BUILD ENVIRONMENT PAYLOAD (PURE SCENE GENERATOR) ---

    public class BuildEnvironmentPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public BuildEnvironmentData Data { get; set; }
    }

    public class BuildEnvironmentData
    {
        [JsonProperty("environment_id")]
        public string EnvironmentId { get; set; }

        [JsonProperty("original_prompt")]
        public string OriginalPrompt { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("archetype")]
        public string Archetype { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("center_offset")]
        public Vector3Offset CenterOffset { get; set; }

        [JsonProperty("is_sky_pocket")]
        public bool IsSkyPocket { get; set; }

        [JsonProperty("sky_pocket_altitude")]
        public float SkyPocketAltitude { get; set; }

        [JsonProperty("entrance_title")]
        public string EntranceTitle { get; set; }

        [JsonProperty("spatial_requirements")]
        public SpatialRequirementsData SpatialRequirements { get; set; }

        [JsonProperty("props")]
        public List<PropPlacementData> Props { get; set; }

        [JsonProperty("npcs")]
        public List<EnvironmentNpcData> Npcs { get; set; }

        [JsonProperty("vehicles")]
        public List<VehiclePlacementData> Vehicles { get; set; }

        [JsonProperty("atmosphere")]
        public EnvironmentAtmosphereData Atmosphere { get; set; }

        [JsonProperty("scenario_director")]
        public ScenarioDirectorData ScenarioDirector { get; set; }

        [JsonProperty("architectural_layers")]
        public List<ArchitecturalLayerDecomposition> ArchitecturalLayers { get; set; }

        [JsonProperty("pocket_transitions")]
        public object PocketTransitionsRaw { get; set; }

        [JsonProperty("spatial_feasibility_audit")]
        public SpatialFeasibilityAudit SpatialFeasibilityAudit { get; set; }

        [JsonProperty("character_resolution_audit")]
        public CharacterResolutionAudit CharacterResolutionAudit { get; set; }

        [JsonProperty("asset_transfer_audit")]
        public AssetTransferAudit AssetTransferAudit { get; set; }

        public BuildEnvironmentData()
        {
            CenterOffset = new Vector3Offset();
            IsSkyPocket = false;
            SkyPocketAltitude = 1500.0f;
            EntranceTitle = "Underground Complex";
            Props = new List<PropPlacementData>();
            Npcs = new List<EnvironmentNpcData>();
            Vehicles = new List<VehiclePlacementData>();
            Atmosphere = new EnvironmentAtmosphereData();
            ArchitecturalLayers = new List<ArchitecturalLayerDecomposition>();
            SpatialFeasibilityAudit = null;
            CharacterResolutionAudit = null;
            AssetTransferAudit = null;
        }
    }

    public class CharacterResolutionAudit
    {
        [JsonProperty("requested_query")]
        public string RequestedQuery { get; set; }

        [JsonProperty("requested_role")]
        public string RequestedRole { get; set; }

        [JsonProperty("resolved_character_name")]
        public string ResolvedCharacterName { get; set; }

        [JsonProperty("resolved_ped_model")]
        public string ResolvedPedModel { get; set; }

        [JsonProperty("canonical_dlc_origin")]
        public string CanonicalDlcOrigin { get; set; }

        [JsonProperty("rejection_reason")]
        public string RejectionReason { get; set; }

        [JsonProperty("verification_summary")]
        public string VerificationSummary { get; set; }
    }

    public class SpatialFeasibilityAudit
    {
        [JsonProperty("requested_asset")]
        public string RequestedAsset { get; set; }

        [JsonProperty("asset_classification")]
        public string AssetClassification { get; set; }

        [JsonProperty("physical_dimensions")]
        public string PhysicalDimensions { get; set; }

        [JsonProperty("target_environment")]
        public string TargetEnvironment { get; set; }

        [JsonProperty("environment_clearance")]
        public string EnvironmentClearance { get; set; }

        [JsonProperty("spatial_conflict_detected")]
        public bool SpatialConflictDetected { get; set; }

        [JsonProperty("conflict_reason")]
        public string ConflictReason { get; set; }

        [JsonProperty("resolution_strategy")]
        public string ResolutionStrategy { get; set; }

        [JsonProperty("suggested_realization")]
        public string SuggestedRealization { get; set; }

        public SpatialFeasibilityAudit()
        {
            RequestedAsset = "";
            AssetClassification = "";
            PhysicalDimensions = "";
            TargetEnvironment = "";
            EnvironmentClearance = "";
            SpatialConflictDetected = false;
            ConflictReason = "";
            ResolutionStrategy = "";
            SuggestedRealization = "";
        }
    }

    public class AssetTransferAudit
    {
        [JsonProperty("host_environment")]
        public string HostEnvironment { get; set; }

        [JsonProperty("donor_asset_origin")]
        public string DonorAssetOrigin { get; set; }

        [JsonProperty("requested_asset_family")]
        public string RequestedAssetFamily { get; set; }

        [JsonProperty("ontological_distinction")]
        public string OntologicalDistinction { get; set; }

        [JsonProperty("resolved_venue_interior")]
        public string ResolvedVenueInterior { get; set; }

        [JsonProperty("transferred_asset_models")]
        public List<string> TransferredAssetModels { get; set; }

        public AssetTransferAudit()
        {
            HostEnvironment = "";
            DonorAssetOrigin = "";
            RequestedAssetFamily = "";
            OntologicalDistinction = "Location ownership is preserved independently from asset origin.";
            ResolvedVenueInterior = "";
            TransferredAssetModels = new List<string>();
        }
    }

    public class LayerPortalData
    {
        [JsonProperty("portal_id")]
        public string PortalId { get; set; }

        [JsonProperty("portal_type")]
        public string PortalType { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("target_layer_index")]
        public int TargetLayerIndex { get; set; }
    }

    public class ArchitecturalLayerDecomposition
    {
        private int _layerIndex = 1;
        [JsonProperty("layer_index")]
        public int LayerIndex
        {
            get { return _layerIndex; }
            set { if (value > 0) _layerIndex = value; }
        }

        [JsonProperty("tier_index")]
        public int TierIndex
        {
            get { return _layerIndex; }
            set { if (value > 0) _layerIndex = value; }
        }

        [JsonProperty("layer_id")]
        public string LayerId { get; set; }

        private string _layerName = "";
        [JsonProperty("layer_name")]
        public string LayerName
        {
            get { return _layerName; }
            set { if (!string.IsNullOrEmpty(value)) _layerName = value; }
        }

        [JsonProperty("name")]
        public string Name
        {
            get { return _layerName; }
            set { if (!string.IsNullOrEmpty(value)) _layerName = value; }
        }

        private string _targetConcept = "";
        [JsonProperty("target_concept")]
        public string TargetConcept
        {
            get { return _targetConcept; }
            set { if (!string.IsNullOrEmpty(value)) _targetConcept = value; }
        }

        [JsonProperty("description")]
        public string Description
        {
            get { return _targetConcept; }
            set { if (!string.IsNullOrEmpty(value)) _targetConcept = value; }
        }

        [JsonProperty("resolution_type")]
        public string ResolutionType { get; set; } // "sky_pocket", "existing_interior", "modular_environment", "connected_teleport_pocket", "separate_canonical_environment"

        [JsonProperty("assigned_interior_id")]
        public string AssignedInteriorId { get; set; }

        private string _transitionPortalTitle = "";
        [JsonProperty("transition_portal_title")]
        public string TransitionPortalTitle
        {
            get { return _transitionPortalTitle; }
            set { if (!string.IsNullOrEmpty(value)) _transitionPortalTitle = value; }
        }

        private int _transitionToLayerIndex;
        [JsonProperty("transition_to_layer_index")]
        public int TransitionToLayerIndex
        {
            get { return _transitionToLayerIndex; }
            set { if (value > 0) _transitionToLayerIndex = value; }
        }

        [JsonProperty("portal_to_next_layer")]
        public object PortalToNextLayer
        {
            get { return _transitionPortalTitle; }
            set
            {
                if (value == null) return;
                if (value is JObject)
                {
                    JObject jobj = (JObject)value;
                    var label = jobj["label"] ?? jobj["portal_name"] ?? jobj["portal_title"] ?? jobj["title"] ?? jobj["name"];
                    if (label != null) _transitionPortalTitle = label.ToString();
                    var target = jobj["target_layer_index"] ?? jobj["target_layer"];
                    if (target != null) int.TryParse(target.ToString(), out _transitionToLayerIndex);
                }
                else
                {
                    string s = value.ToString();
                    if (!string.IsNullOrEmpty(s) && !s.Equals("none", StringComparison.OrdinalIgnoreCase))
                    {
                        _transitionPortalTitle = s;
                    }
                }
            }
        }

        [JsonProperty("world_coordinates")]
        public Vector3Offset WorldCoordinates { get; set; }

        public ArchitecturalLayerDecomposition()
        {
            _layerIndex = 1;
            _layerName = "";
            _targetConcept = "";
            ResolutionType = "modular_environment";
            AssignedInteriorId = "";
            _transitionPortalTitle = "";
            _transitionToLayerIndex = 0;
            WorldCoordinates = new Vector3Offset();
        }
    }

    public class VehiclePlacementData
    {
        [JsonProperty("vehicle_id")]
        public string VehicleId { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("offset")]
        public Vector3Offset Offset { get; set; }

        [JsonProperty("heading")]
        public float Heading { get; set; }

        [JsonProperty("primary_color")]
        public string PrimaryColor { get; set; }

        [JsonProperty("is_engine_running")]
        public bool IsEngineRunning { get; set; }

        [JsonProperty("sirens")]
        public bool Sirens { get; set; }

        [JsonProperty("lights_on")]
        public bool LightsOn { get; set; }

        [JsonProperty("driver_entity_id")]
        public string DriverEntityId { get; set; }

        public VehiclePlacementData()
        {
            VehicleId = "";
            Model = "police";
            Offset = new Vector3Offset();
            Heading = 0f;
            PrimaryColor = "";
            IsEngineRunning = false;
            Sirens = false;
            LightsOn = false;
            DriverEntityId = "";
        }
    }

    public class EnvironmentAtmosphereData
    {
        [JsonProperty("weather")]
        public string Weather { get; set; }

        [JsonProperty("time_hours")]
        public int TimeHours { get; set; }

        [JsonProperty("time_minutes")]
        public int TimeMinutes { get; set; }

        [JsonProperty("ambient_fx")]
        public string AmbientFx { get; set; }

        public EnvironmentAtmosphereData()
        {
            Weather = "";
            TimeHours = -1;
            TimeMinutes = 0;
            AmbientFx = "none";
        }
    }

    public class PropPlacementData
    {
        [JsonProperty("prop_id")]
        public string PropId { get; set; }

        [JsonProperty("parent_prop_id")]
        public string ParentPropId { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("offset")]
        public Vector3Offset Offset { get; set; }

        [JsonProperty("heading")]
        public float Heading { get; set; }

        [JsonProperty("pitch")]
        public float Pitch { get; set; }

        [JsonProperty("roll")]
        public float Roll { get; set; }

        [JsonProperty("is_dynamic")]
        public bool IsDynamic { get; set; }

        [JsonProperty("is_nav_blocker")]
        public bool IsNavBlocker { get; set; }

        public PropPlacementData()
        {
            PropId = "";
            ParentPropId = "";
            Offset = new Vector3Offset();
            Heading = 0f;
            Pitch = 0f;
            Roll = 0f;
            IsDynamic = false;
            IsNavBlocker = false;
        }
    }

    public class EnvironmentNpcData
    {
        [JsonProperty("entity_id")]
        public string EntityId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("occupation")]
        public string Occupation { get; set; }

        [JsonProperty("public_faction")]
        public string PublicFaction { get; set; }

        [JsonProperty("hidden_role")]
        public string HiddenRole { get; set; }

        [JsonProperty("ped_model")]
        public string PedModel { get; set; }

        [JsonProperty("offset")]
        public Vector3Offset Offset { get; set; }

        [JsonProperty("heading")]
        public float Heading { get; set; }

        [JsonProperty("scenario")]
        public string Scenario { get; set; }

        [JsonProperty("weapon")]
        public string Weapon { get; set; }

        [JsonProperty("initial_task")]
        public string InitialTask { get; set; } // "aim_at:<id>", "shoot_at:<id>", "hostage_hands_up", "guard_post", "cower", "sit_chair", "talk_to:<id>"

        [JsonProperty("initial_state")]
        public string InitialState { get; set; } // "HOSTILE", "ALERT", "CALM", "PANIC", "AGGRESSIVE"

        [JsonProperty("relationships")]
        public Dictionary<string, int> Relationships { get; set; }

        [JsonProperty("initial_events")]
        public List<InitialEventData> InitialEvents { get; set; }

        public EnvironmentNpcData()
        {
            Offset = new Vector3Offset();
            Relationships = new Dictionary<string, int>();
            InitialEvents = new List<InitialEventData>();
            InitialTask = "";
            InitialState = "CALM";
        }
    }

    public class ClearEnvironmentPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public ClearEnvironmentData Data { get; set; }
    }

    public class ClearEnvironmentData
    {
        [JsonProperty("environment_id")]
        public string EnvironmentId { get; set; }
    }
}
