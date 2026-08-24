using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenEmpires
{
    // Minimal, dependency-free JSON parser for the LLM teammate pipeline. Unity's
    // JsonUtility can't read objects with dynamic/unknown keys (e.g. a Gemini
    // functionCall.args blob whose shape depends on which tool was called), so we
    // parse into a navigable JsonValue tree instead. Read-only; never used in sim
    // code, so determinism is not a concern here.
    //
    // Navigation is null-safe: indexing a missing key or wrong type returns the
    // shared Null node rather than throwing, so chains like
    // root["candidates"][0]["content"]["parts"] never NRE on a malformed response.
    public enum JsonType { Null, Bool, Number, String, Array, Object }

    public sealed class JsonValue
    {
        public static readonly JsonValue Null = new JsonValue { Type = JsonType.Null };

        public JsonType Type = JsonType.Null;
        public bool Bool;
        public double Number;
        public string Str;
        public List<JsonValue> Array;
        public Dictionary<string, JsonValue> Object;

        public bool IsNull => Type == JsonType.Null;
        public bool IsObject => Type == JsonType.Object;
        public bool IsArray => Type == JsonType.Array;

        public int Count =>
            Type == JsonType.Array ? Array.Count :
            Type == JsonType.Object ? Object.Count : 0;

        // Object member access; returns Null node when absent or not an object.
        public JsonValue this[string key]
        {
            get
            {
                if (Type == JsonType.Object && Object != null && Object.TryGetValue(key, out var v))
                    return v;
                return Null;
            }
        }

        // Array element access; returns Null node when out of range or not an array.
        public JsonValue this[int index]
        {
            get
            {
                if (Type == JsonType.Array && Array != null && index >= 0 && index < Array.Count)
                    return Array[index];
                return Null;
            }
        }

        public bool ContainsKey(string key)
            => Type == JsonType.Object && Object != null && Object.ContainsKey(key);

        public string AsString(string fallback = "")
        {
            switch (Type)
            {
                case JsonType.String: return Str;
                case JsonType.Number: return Number.ToString(CultureInfo.InvariantCulture);
                case JsonType.Bool:   return Bool ? "true" : "false";
                default:              return fallback;
            }
        }

        public double AsDouble(double fallback = 0)
            => Type == JsonType.Number ? Number : fallback;

        public int AsInt(int fallback = 0)
        {
            if (Type == JsonType.Number) return (int)System.Math.Round(Number);
            // Tolerate numbers delivered as strings (LLMs sometimes quote them).
            if (Type == JsonType.String && double.TryParse(Str, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double d))
                return (int)System.Math.Round(d);
            return fallback;
        }

        public bool AsBool(bool fallback = false)
        {
            if (Type == JsonType.Bool) return Bool;
            if (Type == JsonType.String) return Str == "true" || Str == "True";
            return fallback;
        }

        // Re-serialize this node to compact JSON. Used to echo a model's functionCall
        // verbatim back into the next request turn (Gemini requires the call to precede
        // its matching functionResponse).
        public string ToJson()
        {
            var sb = new StringBuilder(128);
            AppendTo(sb);
            return sb.ToString();
        }

        public void AppendTo(StringBuilder sb)
        {
            switch (Type)
            {
                case JsonType.Null:
                    sb.Append("null");
                    break;
                case JsonType.Bool:
                    sb.Append(Bool ? "true" : "false");
                    break;
                case JsonType.Number:
                    // Emit integers without a trailing ".0" so enum-ish args stay clean.
                    if (Number == System.Math.Floor(Number) && !double.IsInfinity(Number))
                        sb.Append(((long)Number).ToString(CultureInfo.InvariantCulture));
                    else
                        sb.Append(Number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case JsonType.String:
                    AppendEscaped(sb, Str);
                    break;
                case JsonType.Array:
                    sb.Append('[');
                    for (int i = 0; i < Array.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Array[i].AppendTo(sb);
                    }
                    sb.Append(']');
                    break;
                case JsonType.Object:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in Object)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        AppendEscaped(sb, kv.Key);
                        sb.Append(':');
                        kv.Value.AppendTo(sb);
                    }
                    sb.Append('}');
                    break;
            }
        }

        public static void AppendEscaped(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    switch (c)
                    {
                        case '"':  sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        default:
                            if (c < 0x20) sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                            else sb.Append(c);
                            break;
                    }
                }
            }
            sb.Append('"');
        }
    }

    public static class LlmJson
    {
        // Returns null on any parse failure (caller should treat as "no data").
        public static JsonValue Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                int i = 0;
                SkipWhitespace(text, ref i);
                var v = ParseValue(text, ref i);
                return v;
            }
            catch
            {
                return null;
            }
        }

        private static JsonValue ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length) throw new System.FormatException("unexpected end");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return new JsonValue { Type = JsonType.String, Str = ParseString(s, ref i) };
                case 't':
                case 'f': return ParseBool(s, ref i);
                case 'n': Expect(s, ref i, "null"); return JsonValue.Null;
                default:  return ParseNumber(s, ref i);
            }
        }

        private static JsonValue ParseObject(string s, ref int i)
        {
            var obj = new Dictionary<string, JsonValue>();
            i++; // consume '{'
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return new JsonValue { Type = JsonType.Object, Object = obj }; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new System.FormatException("expected key");
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new System.FormatException("expected ':'");
                i++; // consume ':'
                var val = ParseValue(s, ref i);
                obj[key] = val;
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new System.FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                throw new System.FormatException("expected ',' or '}'");
            }
            return new JsonValue { Type = JsonType.Object, Object = obj };
        }

        private static JsonValue ParseArray(string s, ref int i)
        {
            var list = new List<JsonValue>();
            i++; // consume '['
            SkipWhitespace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return new JsonValue { Type = JsonType.Array, Array = list }; }
            while (true)
            {
                var val = ParseValue(s, ref i);
                list.Add(val);
                SkipWhitespace(s, ref i);
                if (i >= s.Length) throw new System.FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                throw new System.FormatException("expected ',' or ']'");
            }
            return new JsonValue { Type = JsonType.Array, Array = list };
        }

        private static string ParseString(string s, ref int i)
        {
            i++; // consume opening quote
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"':  sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/':  sb.Append('/'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        case 'b':  sb.Append('\b'); break;
                        case 'f':  sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 <= s.Length &&
                                int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out int code))
                            {
                                // char is UTF-16; appending each \uXXXX preserves surrogate pairs.
                                sb.Append((char)code);
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new System.FormatException("unterminated string");
        }

        private static JsonValue ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') i++;
                else break;
            }
            string num = s.Substring(start, i - start);
            if (!double.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                throw new System.FormatException("bad number: " + num);
            return new JsonValue { Type = JsonType.Number, Number = d };
        }

        private static JsonValue ParseBool(string s, ref int i)
        {
            if (s[i] == 't') { Expect(s, ref i, "true"); return new JsonValue { Type = JsonType.Bool, Bool = true }; }
            Expect(s, ref i, "false");
            return new JsonValue { Type = JsonType.Bool, Bool = false };
        }

        private static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new System.FormatException("expected '" + literal + "'");
            i += literal.Length;
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }
    }
}
