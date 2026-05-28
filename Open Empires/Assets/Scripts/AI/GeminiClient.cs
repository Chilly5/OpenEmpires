using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OpenEmpires
{
    // Transport layer for Gemini's generateContent endpoint with native function
    // calling. One Generate() call is a single HTTP round-trip; the multi-turn tool
    // loop (call -> functionResponse -> call -> final reply) lives in LlmToolLoop,
    // which owns the growing contents list and re-invokes Generate per turn.
    //
    // Coroutines are required because UnityWebRequest is async-only; the calling
    // MonoBehaviour owns the coroutine lifetime. Everything here is non-deterministic
    // and local to the owner client — no sim state is ever read or written.
    public static class GeminiClient
    {
        private const string Model = "gemini-3.5-flash";
        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/" + Model + ":generateContent";
        private const float TimeoutSeconds = 30f; // thinking models can be slow; the loop, not the game, waits

        // ── Stored conversation history (text only). Used by LlmConversationMemory. ──
        public struct Turn
        {
            public bool IsUser; // false = AI/model
            public string Text;
        }

        // ── Live request content model. A turn's parts may mix text, functionCall
        //    (echoed model calls), and functionResponse (our tool results). ──
        public sealed class Content
        {
            public string Role;                 // "user" | "model"
            public readonly List<Part> Parts = new List<Part>();

            public Content(string role) { Role = role; }

            public static Content UserText(string text)
            {
                var c = new Content("user");
                c.Parts.Add(new Part { Text = text });
                return c;
            }

            public static Content ModelText(string text)
            {
                var c = new Content("model");
                c.Parts.Add(new Part { Text = text });
                return c;
            }
        }

        public sealed class Part
        {
            // Exactly one of these is populated.
            public string Text;
            public FunctionCall Call;
            public FunctionResponse Response;
        }

        public sealed class FunctionCall
        {
            public string Name;
            public JsonValue Args;          // parsed args object (may be null / empty)
            public string ThoughtSignature; // Gemini 3: opaque token that MUST be echoed back verbatim
        }

        public sealed class FunctionResponse
        {
            public string Name;
            public string ResultText;
        }

        // ── Parsed model response for one turn. ──
        public sealed class Response
        {
            public string Text = string.Empty;
            public string Thoughts = string.Empty; // Gemini thinking summary (debug only)
            public readonly List<FunctionCall> Calls = new List<FunctionCall>();
            public string FinishReason = string.Empty;
            public bool HasCalls => Calls.Count > 0;
        }

        // Single round-trip. toolsJson is the inner tool object
        // ({"functionDeclarations":[...]}) or null to disable tools for this call.
        public static IEnumerator Generate(string apiKey, string systemPrompt,
            List<Content> contents, string toolsJson,
            Action<Response> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                onFailure?.Invoke("missing-api-key");
                yield break;
            }

            string body = BuildRequestBody(systemPrompt, contents, toolsJson);
            LlmDebug.Http("request: " + body);
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
                    string detail = req.downloadHandler != null ? req.downloadHandler.text : null;
                    onFailure?.Invoke($"http-{(int)req.responseCode}:{req.error}:{Truncate(detail, 300)}");
                    yield break;
                }

                LlmDebug.Http("response: " + req.downloadHandler.text);
                var parsed = ParseResponse(req.downloadHandler.text);
                if (parsed == null)
                {
                    onFailure?.Invoke("unparseable-response");
                    yield break;
                }
                onSuccess?.Invoke(parsed);
            }
        }

        private static string BuildRequestBody(string systemPrompt, List<Content> contents, string toolsJson)
        {
            var sb = new StringBuilder(2048);
            sb.Append('{');

            sb.Append("\"system_instruction\":{\"parts\":[{\"text\":");
            JsonValue.AppendEscaped(sb, systemPrompt ?? string.Empty);
            sb.Append("}]},");

            sb.Append("\"contents\":[");
            if (contents != null)
            {
                for (int i = 0; i < contents.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    AppendContent(sb, contents[i]);
                }
            }
            sb.Append("],");

            if (!string.IsNullOrEmpty(toolsJson))
            {
                sb.Append("\"tools\":[").Append(toolsJson).Append("],");
                sb.Append("\"toolConfig\":{\"functionCallingConfig\":{\"mode\":\"AUTO\"}},");
            }

            sb.Append("\"generationConfig\":{");
            sb.Append("\"temperature\":0.7,");
            sb.Append("\"maxOutputTokens\":4096,");
            // Ask Gemini to return a summary of its reasoning so we can log it.
            sb.Append("\"thinkingConfig\":{\"includeThoughts\":true}");
            sb.Append("}}");
            return sb.ToString();
        }

        private static void AppendContent(StringBuilder sb, Content c)
        {
            sb.Append("{\"role\":\"").Append(c.Role).Append("\",\"parts\":[");
            for (int i = 0; i < c.Parts.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendPart(sb, c.Parts[i]);
            }
            sb.Append("]}");
        }

        private static void AppendPart(StringBuilder sb, Part p)
        {
            if (p.Call != null)
            {
                sb.Append("{\"functionCall\":{\"name\":");
                JsonValue.AppendEscaped(sb, p.Call.Name);
                sb.Append(",\"args\":");
                if (p.Call.Args != null && p.Call.Args.IsObject) p.Call.Args.AppendTo(sb);
                else sb.Append("{}");
                sb.Append('}'); // close functionCall
                // Gemini 3 requires the thought signature to ride back with the echoed call.
                if (!string.IsNullOrEmpty(p.Call.ThoughtSignature))
                {
                    sb.Append(",\"thoughtSignature\":");
                    JsonValue.AppendEscaped(sb, p.Call.ThoughtSignature);
                }
                sb.Append('}'); // close part
            }
            else if (p.Response != null)
            {
                sb.Append("{\"functionResponse\":{\"name\":");
                JsonValue.AppendEscaped(sb, p.Response.Name);
                sb.Append(",\"response\":{\"result\":");
                JsonValue.AppendEscaped(sb, p.Response.ResultText ?? string.Empty);
                sb.Append("}}}");
            }
            else
            {
                sb.Append("{\"text\":");
                JsonValue.AppendEscaped(sb, p.Text ?? string.Empty);
                sb.Append('}');
            }
        }

        private static Response ParseResponse(string json)
        {
            var root = LlmJson.Parse(json);
            if (root == null) return null;

            var result = new Response();
            var cand = root["candidates"][0];
            result.FinishReason = cand["finishReason"].AsString();

            var parts = cand["content"]["parts"];
            if (parts.IsArray)
            {
                var sb = new StringBuilder(256);
                var thoughtSb = new StringBuilder(256);
                for (int i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    // "thought" parts are Gemini's reasoning summary — capture for debug
                    // logging but keep them out of the visible reply.
                    if (part["thought"].AsBool())
                    {
                        if (part.ContainsKey("text"))
                        {
                            if (thoughtSb.Length > 0) thoughtSb.Append(' ');
                            thoughtSb.Append(part["text"].AsString());
                        }
                        continue;
                    }

                    if (part.ContainsKey("functionCall"))
                    {
                        var fc = part["functionCall"];
                        result.Calls.Add(new FunctionCall
                        {
                            Name = fc["name"].AsString(),
                            Args = fc["args"],
                            ThoughtSignature = part["thoughtSignature"].AsString(),
                        });
                    }
                    else if (part.ContainsKey("text"))
                    {
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(part["text"].AsString());
                    }
                }
                result.Text = sb.ToString();
                result.Thoughts = thoughtSb.ToString();
            }
            return result;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
