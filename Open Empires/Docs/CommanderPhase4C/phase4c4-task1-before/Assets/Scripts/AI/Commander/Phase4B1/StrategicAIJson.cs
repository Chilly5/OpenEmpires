using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public static class StrategicAIJson
    {
        public const int MaximumResponseCharacters = 8192;

        public static StrategicAIProviderResult Parse(string rawText, StrategicAIRequest request)
        {
            if (request == null)
                return StrategicAIProviderResult.Rejected("Missing trusted strategic context.");
            try
            {
                string json = Cleanup(rawText);
                CheckSyntax(json);
                JObject root;
                using (var reader = new JsonTextReader(new StringReader(json))
                    { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                {
                    root = JObject.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        CommentHandling = CommentHandling.Load
                    });
                    if (reader.Read()) throw new JsonException();
                }
                foreach (var field in root.Properties())
                    if (field.Name != "intentCategory" && field.Name != "objectiveType"
                        && field.Name != "parameters") throw new JsonException();
                if (ReadString(root, "intentCategory") != "Strategic")
                    return StrategicAIProviderResult.Rejected("Only strategic intents are accepted.");

                string objective = ReadString(root, "objectiveType");
                StrategicObjectiveType objectiveType;
                // Exact names only: Enum.TryParse also accepts numbers and combined enum values.
                switch (objective)
                {
                    case "AttackPreparation": objectiveType = StrategicObjectiveType.AttackPreparation; break;
                    case "DefensivePreparation": objectiveType = StrategicObjectiveType.DefensivePreparation; break;
                    case "EconomicExpansion": objectiveType = StrategicObjectiveType.EconomicExpansion; break;
                    case "MilitaryReinforcement": objectiveType = StrategicObjectiveType.MilitaryReinforcement; break;
                    default: return StrategicAIProviderResult.Rejected("Unsupported strategic objective.");
                }

                var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
                if (root.TryGetValue("parameters", out JToken token))
                {
                    if (!(token is JObject values)) throw new JsonException();
                    foreach (var property in values.Properties())
                    {
                        if (objectiveType != StrategicObjectiveType.AttackPreparation
                            || property.Name != "focus" || property.Value.Type != JTokenType.String
                            || (string)property.Value != "cavalry")
                            return StrategicAIProviderResult.Rejected("Unsupported strategic parameter.");
                        parameters.Add("focus", "cavalry");
                    }
                }

                var dto = new StrategicIntentDTO(objective, parameters);
                var intent = new StrategicIntent(request.IntentId, request.Context.PlayerId,
                    objectiveType, request.Context.SnapshotTick, parameters, null,
                    StrategicIntentSource.AIRecommendation);
                // Existing validator remains authoritative. The registry is private and default;
                // no external templates or execution callbacks can be injected at this boundary.
                var validation = new StrategicIntentValidator().Validate(intent,
                    request.Context.PlayerId, StrategicPlanRegistry.CreateDefault());
                if (!validation.IsValid)
                    return StrategicAIProviderResult.Rejected("Strategic intent validation failed.");
                if (!request.BindIntent(intent))
                    return StrategicAIProviderResult.Rejected("Strategic request identity was already consumed.");

                var canonical = new JObject
                {
                    ["intentCategory"] = dto.intentCategory,
                    ["objectiveType"] = dto.objectiveType,
                    ["parameters"] = JObject.FromObject(parameters)
                };
                return StrategicAIProviderResult.Accepted(dto, intent, canonical.ToString(Formatting.None));
            }
            catch (Exception error) when (error is JsonException || error is FormatException
                || error is OverflowException || error is ArgumentException)
            {
                // Model-controlled field names/values and parser internals must never be echoed.
                return StrategicAIProviderResult.Rejected("Invalid strategic intent JSON.");
            }
        }

        private static string ReadString(JObject root, string name)
        {
            var token = root[name];
            if (token == null || token.Type != JTokenType.String) throw new JsonException();
            return (string)token;
        }

        private static string Cleanup(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaximumResponseCharacters)
                throw new JsonException();
            string text = raw.Trim();
            if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
            int newline = text.IndexOf('\n');
            if (newline < 0 || !text.EndsWith("```", StringComparison.Ordinal)) throw new JsonException();
            string fence = text.Substring(0, newline).TrimEnd('\r');
            if (fence != "```" && fence != "```json") throw new JsonException();
            return text.Substring(newline + 1, text.Length - newline - 4).Trim();
        }

        // This schema consists entirely of objects and strings. Disallow Json.NET's
        // JavaScript extensions (comments, single quotes, trailing commas, literals).
        // JsonTextReader subsequently checks escapes, separators and object structure.
        private static void CheckSyntax(string json)
        {
            char previous = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    bool closed = false;
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new JsonException();
                        if (c == '\\')
                        {
                            if (++i >= json.Length || "\"\\/bfnrtu".IndexOf(json[i]) < 0)
                                throw new JsonException();
                            if (json[i] == 'u')
                                for (int d = 0; d < 4; d++)
                                    if (++i >= json.Length || !Uri.IsHexDigit(json[i])) throw new JsonException();
                        }
                    }
                    if (!closed) throw new JsonException();
                    previous = '"';
                }
                else if (c == ' ' || c == '\r' || c == '\n' || c == '\t') continue;
                else
                {
                    if ("{}:,".IndexOf(c) < 0 || (c == '}' && previous == ',')) throw new JsonException();
                    previous = c;
                }
            }
        }
    }
}
