using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // A repair candidate exists only when changing numeric-string scalar shape yields a
    // fully valid strict semantic response. It cannot choose a new effect or authority.
    internal static class CommanderSemanticSchemaRepair
    {
        internal static bool TryCreateTemplate(string raw, out JObject template)
        {
            template = null;
            try
            {
                JObject candidate = LoadStrict(raw);
                string outcome = (string)candidate["outcome"];
                bool changed = outcome == "DynamicPlan"
                    ? NormalizeDynamic(candidate)
                    : outcome == "Request" ? NormalizeRequest(candidate) : false;
                if (!changed) return false;
                string normalized = candidate.ToString(Formatting.None);
                if (normalized.Length > CommanderDynamicPlan.MaximumCharacters ||
                    !CommanderSemanticJson.Parse(normalized).IsValid) return false;
                template = candidate;
                return true;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException ||
                error is FormatException || error is OverflowException || error is InvalidCastException)
            {
                return false;
            }
        }

        internal static bool MatchesTemplate(string raw, JObject template)
        {
            try
            {
                return template != null && EqualIgnoringPropertyOrder(LoadStrict(raw), template);
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException ||
                error is FormatException || error is OverflowException || error is InvalidCastException)
            {
                return false;
            }
        }

        private static bool NormalizeDynamic(JObject root)
        {
            if (!(root["nodes"] is JArray nodes)) return false;
            bool changed = false;
            foreach (JToken value in nodes)
            {
                if (!(value is JObject node) || !(node["parameters"] is JObject parameters))
                    continue;
                CommanderDynamicPrimitive primitive = CommanderDynamicPrimitiveRegistry.Find(
                    (string)node["mechanic"]);
                if (primitive == null) continue;
                foreach (CommanderDynamicField field in primitive.Parameters)
                    if (field.Kind == CommanderDynamicFieldKind.Count ||
                        field.Kind == CommanderDynamicFieldKind.Offset ||
                        field.Kind == CommanderDynamicFieldKind.ClearGapTiles)
                        changed |= NormalizeInteger(parameters, field.Name);
            }
            return changed;
        }

        private static bool NormalizeRequest(JObject root)
        {
            if (!(root["nodes"] is JArray nodes)) return false;
            bool changed = false;
            foreach (JToken value in nodes)
            {
                if (!(value is JObject node)) continue;
                changed |= NormalizeInteger(node, "count");
                if (node["placement"] is JObject placement)
                {
                    changed |= NormalizeInteger(placement, "ordinal");
                    changed |= NormalizeInteger(placement, "clearGapTiles");
                }
                if (node["constraints"] is JArray constraints)
                    foreach (JToken item in constraints)
                        if (item is JObject constraint)
                            changed |= NormalizeInteger(constraint, "amount");
            }
            return changed;
        }

        private static bool NormalizeInteger(JObject owner, string name)
        {
            if (owner[name]?.Type != JTokenType.String) return false;
            string digits = (string)owner[name];
            if (string.IsNullOrEmpty(digits) || (digits.Length > 1 && digits[0] == '0')) return false;
            foreach (char digit in digits)
                if (digit < '0' || digit > '9') return false;
            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return false;
            owner[name] = number;
            return true;
        }

        private static JObject LoadStrict(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > CommanderDynamicPlan.MaximumCharacters)
                throw new JsonException();
            CheckStrictSyntax(raw);
            using (var reader = new JsonTextReader(new StringReader(raw))
                { MaxDepth = 12, DateParseHandling = DateParseHandling.None })
            {
                JObject root = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    CommentHandling = CommentHandling.Load
                });
                if (reader.Read()) throw new JsonException();
                return root;
            }
        }

        private static bool EqualIgnoringPropertyOrder(JToken left, JToken right)
        {
            if (left.Type != right.Type) return false;
            if (left is JObject a && right is JObject b)
            {
                if (a.Count != b.Count) return false;
                foreach (JProperty property in a.Properties())
                {
                    JProperty other = b.Property(property.Name);
                    if (other == null || !EqualIgnoringPropertyOrder(property.Value, other.Value))
                        return false;
                }
                return true;
            }
            if (left is JArray x && right is JArray y)
            {
                if (x.Count != y.Count) return false;
                for (int i = 0; i < x.Count; i++)
                    if (!EqualIgnoringPropertyOrder(x[i], y[i])) return false;
                return true;
            }
            return JToken.DeepEquals(left, right);
        }

        // Match the strict parser's lexical boundary before Json.NET can normalize
        // comments, trailing commas, unsupported escapes or malformed UTF-16.
        private static void CheckStrictSyntax(string json)
        {
            char previous = '\0';
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    bool closed = false, pendingHighSurrogate = false;
                    while (++i < json.Length)
                    {
                        c = json[i];
                        if (c == '"')
                        {
                            if (pendingHighSurrogate) throw new JsonException();
                            closed = true;
                            break;
                        }
                        if (c < 32) throw new JsonException();
                        if (c == '\\')
                        {
                            if (++i >= json.Length || "\"\\/bfnrtu".IndexOf(json[i]) < 0)
                                throw new JsonException();
                            c = json[i];
                            if (json[i] == 'u')
                            {
                                int codeUnit = 0;
                                for (int d = 0; d < 4; d++)
                                {
                                    if (++i >= json.Length || !Uri.IsHexDigit(json[i])) throw new JsonException();
                                    char hex = json[i];
                                    codeUnit = codeUnit * 16 + (hex <= '9' ? hex - '0'
                                        : char.ToUpperInvariant(hex) - 'A' + 10);
                                }
                                c = (char)codeUnit;
                            }
                        }
                        if (pendingHighSurrogate)
                        {
                            if (!char.IsLowSurrogate(c)) throw new JsonException();
                            pendingHighSurrogate = false;
                        }
                        else if (char.IsHighSurrogate(c)) pendingHighSurrogate = true;
                        else if (char.IsLowSurrogate(c)) throw new JsonException();
                    }
                    if (!closed) throw new JsonException();
                    previous = '"';
                }
                else if (c == ' ' || c == '\r' || c == '\n' || c == '\t') continue;
                else if (c == '{' || c == '[' || c == ':' || c == ',') previous = c;
                else if (c == '}' || c == ']')
                {
                    if (previous == ',') throw new JsonException();
                    previous = c;
                }
                else if (c == '-' || (c >= '0' && c <= '9'))
                {
                    if (c == '-')
                    {
                        if (++i >= json.Length || json[i] < '0' || json[i] > '9') throw new JsonException();
                    }
                    if (json[i] == '0' && i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9')
                        throw new JsonException();
                    while (i + 1 < json.Length && json[i + 1] >= '0' && json[i + 1] <= '9') i++;
                    previous = '0';
                }
                else throw new JsonException();
            }
        }
    }
}
