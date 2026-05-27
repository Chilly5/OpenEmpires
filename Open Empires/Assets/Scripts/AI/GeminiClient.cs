using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenEmpires
{
    // Thin coroutine wrapper around Gemini's generateContent endpoint. Coroutines are
    // required because UnityWebRequest is async-only; the controller (a MonoBehaviour)
    // owns the coroutine lifetime.
    public static class GeminiClient
    {
        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
        private const float TimeoutSeconds = 8f;

        public struct Turn
        {
            public bool IsUser; // false = AI/model
            public string Text;
        }

        public static IEnumerator Send(string apiKey, string systemPrompt, List<Turn> history,
            string userMessage, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                onFailure?.Invoke("missing-api-key");
                yield break;
            }

            string body = BuildRequestBody(systemPrompt, history, userMessage);
            string url = Endpoint + "?key=" + UnityWebRequest.EscapeURL(apiKey);

            using (var req = new UnityWebRequest(url, "POST"))
            {
                byte[] payload = Encoding.UTF8.GetBytes(body);
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = (int)TimeoutSeconds;

                var op = req.SendWebRequest();
                float started = Time.realtimeSinceStartup;
                while (!op.isDone)
                {
                    if (Time.realtimeSinceStartup - started > TimeoutSeconds)
                    {
                        req.Abort();
                        onFailure?.Invoke("timeout");
                        yield break;
                    }
                    yield return null;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    onFailure?.Invoke($"http-{(int)req.responseCode}:{req.error}");
                    yield break;
                }

                string text = ExtractTextFromResponse(req.downloadHandler.text);
                if (string.IsNullOrEmpty(text))
                {
                    onFailure?.Invoke("empty-response");
                    yield break;
                }
                onSuccess?.Invoke(text);
            }
        }

        private static string BuildRequestBody(string systemPrompt, List<Turn> history, string userMessage)
        {
            var sb = new StringBuilder(1024);
            sb.Append("{");
            sb.Append("\"system_instruction\":{\"parts\":[{\"text\":");
            AppendJsonString(sb, systemPrompt ?? string.Empty);
            sb.Append("}]},");
            sb.Append("\"contents\":[");
            bool first = true;
            if (history != null)
            {
                for (int i = 0; i < history.Count; i++)
                {
                    if (!first) sb.Append(',');
                    sb.Append("{\"role\":\"");
                    sb.Append(history[i].IsUser ? "user" : "model");
                    sb.Append("\",\"parts\":[{\"text\":");
                    AppendJsonString(sb, history[i].Text ?? string.Empty);
                    sb.Append("}]}");
                    first = false;
                }
            }
            if (!first) sb.Append(',');
            sb.Append("{\"role\":\"user\",\"parts\":[{\"text\":");
            AppendJsonString(sb, userMessage ?? string.Empty);
            sb.Append("}]}");
            sb.Append("],");
            sb.Append("\"generationConfig\":{");
            sb.Append("\"responseMimeType\":\"application/json\",");
            sb.Append("\"temperature\":0.7,");
            sb.Append("\"maxOutputTokens\":400");
            sb.Append("}}");
            return sb.ToString();
        }

        private static void AppendJsonString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // Pull candidates[0].content.parts[0].text out of the response without a full
        // JSON parser. The shape is fixed by Gemini; this is fragile but cheap.
        private static string ExtractTextFromResponse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int candIdx = json.IndexOf("\"candidates\"", StringComparison.Ordinal);
            if (candIdx < 0) return null;
            int textIdx = json.IndexOf("\"text\"", candIdx, StringComparison.Ordinal);
            if (textIdx < 0) return null;
            int colon = json.IndexOf(':', textIdx);
            if (colon < 0) return null;
            int i = colon + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t' || json[i] == '\n' || json[i] == '\r')) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;
            var sb = new StringBuilder(256);
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char n = json[i + 1];
                    switch (n)
                    {
                        case '"':  sb.Append('"');  i += 2; continue;
                        case '\\': sb.Append('\\'); i += 2; continue;
                        case 'n':  sb.Append('\n'); i += 2; continue;
                        case 'r':  sb.Append('\r'); i += 2; continue;
                        case 't':  sb.Append('\t'); i += 2; continue;
                        case '/':  sb.Append('/');  i += 2; continue;
                        case 'b':  sb.Append('\b'); i += 2; continue;
                        case 'f':  sb.Append('\f'); i += 2; continue;
                        case 'u':
                            if (i + 5 < json.Length && int.TryParse(json.Substring(i + 2, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out int code))
                            {
                                sb.Append((char)code);
                                i += 6;
                                continue;
                            }
                            break;
                    }
                    sb.Append(c); i++;
                }
                else if (c == '"')
                {
                    return sb.ToString();
                }
                else
                {
                    sb.Append(c); i++;
                }
            }
            return null;
        }
    }
}
