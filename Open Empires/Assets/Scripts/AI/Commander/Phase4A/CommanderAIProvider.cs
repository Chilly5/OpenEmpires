using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public enum CommanderConversationRole
    {
        Player,
        Commander
    }

    public sealed class CommanderConversationMessage
    {
        public CommanderConversationRole Role { get; }
        public string Text { get; }

        public CommanderConversationMessage(CommanderConversationRole role, string text)
        {
            Role = role;
            Text = text ?? string.Empty;
        }
    }

    public sealed class CommanderAIRequest
    {
        public string PlayerMessage { get; }
        public CommanderContext Context { get; }
        public IReadOnlyList<CommanderConversationMessage> ConversationHistory { get; }

        public CommanderAIRequest(string playerMessage, CommanderContext context,
            IReadOnlyList<CommanderConversationMessage> conversationHistory)
        {
            PlayerMessage = playerMessage ?? string.Empty;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ConversationHistory = conversationHistory ?? Array.Empty<CommanderConversationMessage>();
        }
    }

    public sealed class CommanderAIProviderResult
    {
        public bool Success { get; }
        public CommanderIntentDTO IntentDto { get; }
        public string IntentJson { get; }
        public string AIResponseText { get; }
        public CommanderIntentErrorCode ErrorCode { get; }
        public string FailureReason { get; }

        private CommanderAIProviderResult(bool success, CommanderIntentDTO intentDto,
            string intentJson, string responseText, CommanderIntentErrorCode errorCode,
            string failureReason)
        {
            Success = success;
            IntentDto = intentDto;
            IntentJson = intentJson ?? string.Empty;
            AIResponseText = responseText ?? string.Empty;
            ErrorCode = errorCode;
            FailureReason = failureReason ?? string.Empty;
        }

        public static CommanderAIProviderResult Accepted(CommanderIntentDTO dto,
            string intentJson, string responseText)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            return new CommanderAIProviderResult(true, dto, intentJson, responseText,
                CommanderIntentErrorCode.None, string.Empty);
        }

        public static CommanderAIProviderResult Rejected(CommanderIntentErrorCode errorCode,
            string reason, string responseText = "I could not translate that into a safe Commander order.")
        {
            return new CommanderAIProviderResult(false, null, string.Empty, responseText,
                errorCode, reason);
        }
    }

    // Data-only boundary: implementations have no simulation, command-buffer, or
    // networking dependency and therefore cannot execute the intent they produce.
    public interface ICommanderAIProvider
    {
        System.Threading.Tasks.Task<CommanderAIProviderResult> TranslateAsync(
            CommanderAIRequest request, System.Threading.CancellationToken cancellationToken);
    }

    public sealed class CommanderConversationHistory
    {
        public const int DefaultCapacity = 12;
        private readonly List<CommanderConversationMessage> messages =
            new List<CommanderConversationMessage>();

        public int Capacity { get; }
        public int Count => messages.Count;

        public CommanderConversationHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 2 || capacity > 40)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public void Append(CommanderConversationRole role, string text)
        {
            messages.Add(new CommanderConversationMessage(role, text));
            while (messages.Count > Capacity)
                messages.RemoveAt(0);
        }

        public IReadOnlyList<CommanderConversationMessage> Snapshot()
        {
            return new List<CommanderConversationMessage>(messages).AsReadOnly();
        }

        public void Clear() => messages.Clear();
    }

    public static class CommanderAIContextSerializer
    {
        // The input is already a detached, fog-safe Phase 3 snapshot. This projection
        // intentionally includes only local-player state plus currently visible resources.
        public static string Serialize(CommanderContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var root = new JObject
            {
                ["playerId"] = context.PlayerId,
                ["snapshotTick"] = context.SnapshotTick,
                ["age"] = context.Age,
                ["civilization"] = context.Civilization,
                ["population"] = context.Population,
                ["populationCap"] = context.PopulationCap,
                ["maximumPopulation"] = context.MaximumPopulation,
                ["resources"] = JObject.FromObject(context.Resources),
                ["units"] = JArray.FromObject(context.Units),
                ["buildings"] = JArray.FromObject(context.Buildings),
                ["workerAllocation"] = JArray.FromObject(context.WorkerAllocation),
                ["visibleResources"] = JArray.FromObject(context.VisibleResources),
                ["availableUnitCapabilities"] = JArray.FromObject(context.UnitOptions),
                ["activeGoals"] = JArray.FromObject(context.ActiveGoals),
                ["supportedTacticalIntents"] = new JArray(
                    "EnsureUnitCount", "BuildStructure", "SetResourceAllocation"),
                ["visibilityPolicy"] = context.EnemyAwarenessPolicy
            };
            return root.ToString(Formatting.None);
        }
    }

    public static class CommanderAIJson
    {
        public static CommanderAIProviderResult ParseTacticalIntent(string rawText,
            CommanderContext context)
        {
            if (context == null)
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.InvalidPlayer,
                    "Missing trusted Commander context.");

            try
            {
                string json = Cleanup(rawText);
                if (string.IsNullOrWhiteSpace(json)
                    || json.Length > CommanderIntentDtoCodec.MaximumResponseCharacters)
                    throw new JsonException("The model response is empty or too large.");

                JObject root;
                using (var reader = new DoubleQuoteJsonReader(new StringReader(json))
                {
                    MaxDepth = 8,
                    DateParseHandling = DateParseHandling.None
                })
                {
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Load
                    });
                    if (reader.Read()) throw new JsonException("Trailing JSON content is not allowed.");
                }

                CheckFields(root, "intentCategory", "intentType", "parameters");
                string category = ReadString(root, "intentCategory");
                string intentType = ReadString(root, "intentType");
                if (!string.Equals(category, "Tactical", StringComparison.OrdinalIgnoreCase))
                    return CommanderAIProviderResult.Rejected(
                        CommanderIntentErrorCode.UnsupportedIntentExecution,
                        "Phase 4A accepts tactical intents only.");
                if (!(root["parameters"] is JObject parameters))
                    throw new JsonException("parameters must be a JSON object.");

                var dto = new CommanderIntentDTO
                {
                    intentCategory = "Tactical",
                    intentType = intentType
                };

                if (string.Equals(intentType, "EnsureUnitCount", StringComparison.OrdinalIgnoreCase))
                {
                    CheckFields(parameters, "unit", "count");
                    dto.unit = ReadString(parameters, "unit");
                    dto.amount = ReadInteger(parameters, "count");
                }
                else if (string.Equals(intentType, "BuildStructure", StringComparison.OrdinalIgnoreCase))
                {
                    CheckFields(parameters, "structure", "count");
                    dto.structure = ReadString(parameters, "structure");
                    dto.amount = parameters["count"] == null ? 1 : ReadInteger(parameters, "count");
                }
                else if (string.Equals(intentType, "SetResourceAllocation", StringComparison.OrdinalIgnoreCase))
                {
                    CheckFields(parameters, "resource", "count");
                    dto.resource = ReadString(parameters, "resource");
                    dto.mode = ResourceAllocationMode.SetExact.ToString();
                    dto.amount = ReadInteger(parameters, "count");
                }
                else
                {
                    return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.UnknownCommand,
                        "The model returned an unsupported tactical intent type.");
                }

                CommanderIntentInterpretation interpretation =
                    CommanderIntentDtoCodec.ValidateAndConvert(dto, context);
                if (!interpretation.Success || interpretation.Intent == null
                    || interpretation.StrategicIntent != null)
                {
                    return CommanderAIProviderResult.Rejected(interpretation.ErrorCode,
                        interpretation.Reason);
                }

                CommanderIntentDTO canonical = CommanderIntentDtoCodec.FromIntent(interpretation.Intent);
                return CommanderAIProviderResult.Accepted(canonical, json,
                    CommanderAIResponseText.ForIntent(interpretation.Intent));
            }
            catch (Exception error) when (error is JsonException || error is FormatException
                || error is OverflowException)
            {
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.InvalidJson,
                    error.Message);
            }
        }

        public static string Cleanup(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
            string text = rawText.Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = text.IndexOf('\n');
                if (firstLine >= 0) text = text.Substring(firstLine + 1);
                int closingFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0) text = text.Substring(0, closingFence);
                text = text.Trim();
            }
            int firstObject = text.IndexOf('{');
            int lastObject = text.LastIndexOf('}');
            if (firstObject >= 0 && lastObject >= firstObject)
                text = text.Substring(firstObject, lastObject - firstObject + 1);
            return text.Trim();
        }

        private static void CheckFields(JObject obj, params string[] allowed)
        {
            foreach (JProperty property in obj.Properties())
                if (Array.IndexOf(allowed, property.Name) < 0)
                    throw new JsonException("Unknown field: " + property.Name);
        }

        private static string ReadString(JObject obj, string key)
        {
            JToken token = obj[key];
            if (token == null || token.Type != JTokenType.String
                || string.IsNullOrWhiteSpace((string)token))
                throw new JsonException(key + " must be a non-empty string.");
            return (string)token;
        }

        private static int ReadInteger(JObject obj, string key)
        {
            JToken token = obj[key];
            if (token == null || token.Type != JTokenType.Integer)
                throw new JsonException(key + " must be an integer.");
            return token.Value<int>();
        }

        private sealed class DoubleQuoteJsonReader : JsonTextReader
        {
            public DoubleQuoteJsonReader(TextReader reader) : base(reader) { }

            public override bool Read()
            {
                bool read = base.Read();
                if (read && (TokenType == JsonToken.PropertyName || TokenType == JsonToken.String)
                    && QuoteChar != '"')
                    throw new JsonException("Only double-quoted JSON strings are accepted.");
                return read;
            }
        }
    }

    public static class CommanderAIResponseText
    {
        public static string ForIntent(CommanderIntent intent)
        {
            if (intent is EnsureUnitCountIntent ensure)
                return "Preparing " + ensure.TargetTotal + " "
                    + CommanderIntentCatalog.GetUnitDisplayName(ensure.UnitType, ensure.TargetTotal != 1).ToLowerInvariant() + ".";
            if (intent is BuildStructureIntent build)
                return "Building " + build.Count + " "
                    + CommanderIntentCatalog.GetStructureDisplayName(build.StructureType).ToLowerInvariant()
                    + (build.Count == 1 ? "." : "s.");
            if (intent is SetResourceAllocationIntent allocation)
                return "Assigning " + allocation.WorkerCount.GetValueOrDefault() + " villagers to "
                    + allocation.Resource.ToString().ToLowerInvariant() + ".";
            return "Order accepted.";
        }
    }
}
