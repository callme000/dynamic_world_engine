using System;
using Newtonsoft.Json;

namespace DynamicWorldEngine.Commands
{
    /// <summary>
    /// Pure JSON action schema deserializer and validator.
    /// Input: raw JSON string. Output: validated command/intent DTO.
    /// Stateless, zero database dependencies, zero execution side-effects.
    /// </summary>
    public class ActionParser
    {
        public ActionParser()
        {
        }

        [Obsolete("ActionParser no longer requires dependencies. Use parameterless constructor.")]
        public ActionParser(DynamicWorldEngine.Database.WorldMemoryStore store)
        {
        }

        public ActionPayload ParseAndValidate(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                ActionPayload payload = JsonConvert.DeserializeObject<ActionPayload>(jsonInput);
                if (payload == null || string.IsNullOrEmpty(payload.Action))
                {
                    throw new JsonSerializationException("Payload root or 'action' property is missing.");
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public BuildEnvironmentPayload ParseBuildEnvironment(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                BuildEnvironmentPayload payload = JsonConvert.DeserializeObject<BuildEnvironmentPayload>(jsonInput);
                if (payload == null || payload.Data == null)
                {
                    throw new JsonSerializationException("Payload root or 'data' node is missing.");
                }
                if (!string.Equals(payload.Action, "BUILD_ENVIRONMENT", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Invalid action type '{0}'. Expected 'BUILD_ENVIRONMENT'.", payload.Action));
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public ClearEnvironmentPayload ParseClearEnvironment(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                ClearEnvironmentPayload payload = JsonConvert.DeserializeObject<ClearEnvironmentPayload>(jsonInput);
                if (payload == null || !string.Equals(payload.Action, "CLEAR_ENVIRONMENT", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Invalid action type. Expected 'CLEAR_ENVIRONMENT'.");
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public MutateEntityPayload ParseMutateEntity(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                MutateEntityPayload payload = JsonConvert.DeserializeObject<MutateEntityPayload>(jsonInput);
                if (payload == null || payload.Data == null)
                {
                    throw new JsonSerializationException("Payload root or 'data' node is missing.");
                }
                if (!string.Equals(payload.Action, "MUTATE_ENTITY_STATE", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Invalid action type. Expected 'MUTATE_ENTITY_STATE'.");
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public TriggerWorldEventPayload ParseTriggerWorldEvent(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                TriggerWorldEventPayload payload = JsonConvert.DeserializeObject<TriggerWorldEventPayload>(jsonInput);
                if (payload == null || payload.Data == null)
                {
                    throw new JsonSerializationException("Payload root or 'data' node is missing.");
                }
                if (!string.Equals(payload.Action, "TRIGGER_WORLD_EVENT", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Invalid action type. Expected 'TRIGGER_WORLD_EVENT'.");
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public CreateStashPayload ParseCreateStash(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                CreateStashPayload payload = JsonConvert.DeserializeObject<CreateStashPayload>(jsonInput);
                if (payload == null || payload.Data == null)
                {
                    throw new JsonSerializationException("Payload root or 'data' node is missing.");
                }
                if (!string.Equals(payload.Action, "ATTACH_QUEST_OR_STASH", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Invalid action type. Expected 'ATTACH_QUEST_OR_STASH'.");
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }

        public DirectScenarioPayload ParseDirectScenario(string jsonInput)
        {
            if (string.IsNullOrWhiteSpace(jsonInput))
            {
                throw new ArgumentException("JSON payload is empty.");
            }

            try
            {
                DirectScenarioPayload payload = JsonConvert.DeserializeObject<DirectScenarioPayload>(jsonInput);
                if (payload == null || payload.Data == null)
                {
                    throw new JsonSerializationException("Payload root or 'data' node is missing.");
                }
                if (!string.Equals(payload.Action, "DIRECT_SCENARIO", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(string.Format("Invalid action type '{0}'. Expected 'DIRECT_SCENARIO'.", payload.Action));
                }
                return payload;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(string.Format("JSON schema mismatch: {0}", ex.Message));
            }
        }
    }
}
