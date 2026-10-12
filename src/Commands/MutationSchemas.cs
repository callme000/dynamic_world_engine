using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicWorldEngine.Commands
{
    // --- 1. MUTATE ENTITY STATE ---

    public class MutateEntityPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public MutateEntityData Data { get; set; }
    }

    public class MutateEntityData
    {
        [JsonProperty("target_entity_id")]
        public string TargetEntityId { get; set; }

        [JsonProperty("relationship_delta")]
        public int RelationshipDelta { get; set; }

        [JsonProperty("hostility")]
        public string Hostility { get; set; } // "hostile_attack", "flee_panic", "hands_up", "stand_guard", "friendly_follow", "none"

        [JsonProperty("new_hidden_role")]
        public string NewHiddenRole { get; set; }

        [JsonProperty("give_weapon")]
        public string GiveWeapon { get; set; }

        [JsonProperty("narrative_reason")]
        public string NarrativeReason { get; set; }

        public MutateEntityData()
        {
            Hostility = "none";
        }
    }

    // --- 2. TRIGGER WORLD EVENT ---

    public class TriggerWorldEventPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public TriggerWorldEventData Data { get; set; }
    }

    public class TriggerWorldEventData
    {
        [JsonProperty("event_id")]
        public string EventId { get; set; }

        [JsonProperty("event_type")]
        public string EventType { get; set; } // "POLICE_RAID", "DRIVEBY_AMBUSH", "BACKUP_ARRIVAL", "CONTRABAND_DROP", "GANG_WAR"

        [JsonProperty("title")]
        public string Title { get; set; }

        public string EventTitle { get { return Title; } set { Title = value; } }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("vehicle_model")]
        public string VehicleModel { get; set; }

        [JsonProperty("occupants")]
        public List<EventOccupantData> Occupants { get; set; }

        [JsonProperty("sirens")]
        public bool Sirens { get; set; }

        [JsonProperty("approach_speed")]
        public float ApproachSpeed { get; set; }

        public TriggerWorldEventData()
        {
            Occupants = new List<EventOccupantData>();
            Sirens = false;
            ApproachSpeed = 25.0f;
        }
    }

    public class EventOccupantConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(EventOccupantData);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            if (reader.TokenType == JsonToken.String)
            {
                string strVal = (string)reader.Value;
                return new EventOccupantData
                {
                    Name = strVal,
                    PedModel = (strVal.StartsWith("s_") || strVal.StartsWith("u_") || strVal.StartsWith("a_") || strVal.StartsWith("g_")) ? strVal : "s_m_y_cop_01",
                    Weapon = "WEAPON_PISTOL",
                    Role = "officer",
                    HostilityToPlayer = "neutral"
                };
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject jo = JObject.Load(reader);
                var occ = new EventOccupantData();
                serializer.Populate(jo.CreateReader(), occ);
                if (string.IsNullOrEmpty(occ.PedModel)) occ.PedModel = "s_m_y_cop_01";
                if (string.IsNullOrEmpty(occ.Weapon)) occ.Weapon = "WEAPON_PISTOL";
                if (string.IsNullOrEmpty(occ.Role)) occ.Role = "officer";
                return occ;
            }

            return new EventOccupantData();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var occ = value as EventOccupantData;
            if (occ == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(occ.Name ?? "");
            writer.WritePropertyName("ped_model");
            writer.WriteValue(occ.PedModel ?? "s_m_y_cop_01");
            writer.WritePropertyName("weapon");
            writer.WriteValue(occ.Weapon ?? "WEAPON_PISTOL");
            writer.WritePropertyName("role");
            writer.WriteValue(occ.Role ?? "officer");
            writer.WritePropertyName("hostility_to_player");
            writer.WriteValue(occ.HostilityToPlayer ?? "neutral");
            writer.WriteEndObject();
        }
    }

    [JsonConverter(typeof(EventOccupantConverter))]
    public class EventOccupantData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ped_model")]
        public string PedModel { get; set; }

        [JsonProperty("weapon")]
        public string Weapon { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; } // "driver", "passenger", "shooter", "officer"

        [JsonProperty("hostility_to_player")]
        public string HostilityToPlayer { get; set; } // "attack", "neutral", "flee"

        public EventOccupantData()
        {
            HostilityToPlayer = "neutral";
        }
    }

    // --- 3. ATTACH QUEST OR STASH ---

    public class CreateStashPayload
    {
        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("data")]
        public CreateStashData Data { get; set; }
    }

    public class CreateStashData
    {
        [JsonProperty("stash_id")]
        public string StashId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("container_prop")]
        public string ContainerProp { get; set; }

        [JsonProperty("reward_cash")]
        public int RewardCash { get; set; }

        [JsonProperty("reward_weapon")]
        public string RewardWeapon { get; set; }

        [JsonProperty("offset")]
        public Vector3Offset Offset { get; set; }

        public CreateStashData()
        {
            ContainerProp = "prop_box_ammo04a";
            RewardCash = 1000;
            Offset = new Vector3Offset();
        }
    }
}
