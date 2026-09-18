using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed class CommanderHttpResponse
    {
        public int StatusCode { get; }
        public string Body { get; }

        public CommanderHttpResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }
    }

    public interface ICommanderHttpTransport
    {
        Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
            IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken);
    }

    internal sealed class CommanderHttpClientTransport : ICommanderHttpTransport
    {
        private static readonly HttpClient Client = new HttpClient();

        public async Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string json,
            IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                request.Content = new StringContent(json ?? string.Empty, Encoding.UTF8,
                    "application/json");
                if (headers != null)
                    foreach (KeyValuePair<string, string> header in headers)
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);

                using (HttpResponseMessage response = await Client.SendAsync(
                    request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                    .ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new CommanderHttpResponse((int)response.StatusCode, body);
                }
            }
        }
    }

    public sealed class GeminiAIProvider : ICommanderAIProvider
    {
        public const string KeyEnvironmentVariable = "OPENEMPIRES_GEMINI_KEY";
        public const string PrimaryModel = "gemini-flash-lite";
        public const string FallbackModel = "gemini-flash-latest";
        private const string EndpointPrefix =
            "https://generativelanguage.googleapis.com/v1beta/models/";

        private const string SystemInstruction =
            "You are not the game AI. You translate player language into Commander Intent JSON. "
            + "You cannot invent commands, execute actions, or modify game state. "
            + "Phase 4A is tactical only. Output exactly one JSON object and nothing else: no markdown and no explanation. "
            + "Allowed forms are: "
            + "{\"intentCategory\":\"Tactical\",\"intentType\":\"EnsureUnitCount\",\"parameters\":{\"unit\":\"Spearman|Archer|Knight\",\"count\":integer}}, "
            + "{\"intentCategory\":\"Tactical\",\"intentType\":\"BuildStructure\",\"parameters\":{\"structure\":\"Barracks\",\"count\":1}}, or "
            + "{\"intentCategory\":\"Tactical\",\"intentType\":\"SetResourceAllocation\",\"parameters\":{\"resource\":\"Wood|Food\",\"count\":integer}}. "
            + "Do not return strategic intents, player IDs, game commands, simulation objects, or extra fields.";

        private readonly string apiKey;
        private readonly ICommanderHttpTransport transport;

        public GeminiAIProvider(string apiKey = null, ICommanderHttpTransport transport = null)
        {
            this.apiKey = string.IsNullOrWhiteSpace(apiKey)
                ? DotEnvLoader.Get(KeyEnvironmentVariable)
                : apiKey;
            this.transport = transport ?? new CommanderHttpClientTransport();
        }

        public async Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(apiKey))
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    KeyEnvironmentVariable + " is not configured.",
                    "Gemini is unavailable; the Commander can still run with the mock provider.");

            string body = BuildRequestJson(request);
            string[] models = { PrimaryModel, FallbackModel };
            for (int i = 0; i < models.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CommanderHttpResponse response;
                try
                {
                    var headers = new Dictionary<string, string>
                    {
                        ["x-goog-api-key"] = apiKey
                    };
                    response = await transport.PostJsonAsync(
                        new Uri(EndpointPrefix + Uri.EscapeDataString(models[i]) + ":generateContent"),
                        body, headers, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    const string message = "Commander AI service temporarily unavailable.";
                    return CommanderAIProviderResult.Rejected(
                        CommanderIntentErrorCode.ProviderFailure, message, message);
                }

                if (response.StatusCode >= 200 && response.StatusCode <= 299)
                {
                    string modelText = ExtractModelText(response.Body);
                    if (modelText == null)
                        return CommanderAIProviderResult.Rejected(
                            CommanderIntentErrorCode.InvalidJson,
                            "Gemini returned no text intent candidate.");
                    return CommanderAIJson.ParseTacticalIntent(modelText, request.Context);
                }

                string safeMessage = null;
                if (response.StatusCode == 429)
                    safeMessage = "Commander AI quota exhausted. Please wait or use offline commands.";
                else if (response.StatusCode == 401 || response.StatusCode == 403)
                    safeMessage = "Commander AI authentication failed.";
                else if (response.StatusCode >= 500 && response.StatusCode <= 599)
                    safeMessage = "Commander AI service temporarily unavailable.";
                if (safeMessage != null)
                    return CommanderAIProviderResult.Rejected(
                        CommanderIntentErrorCode.ProviderFailure, safeMessage, safeMessage);

                bool modelUnavailable = response.StatusCode == 400 || response.StatusCode == 404;
                if (i == 0 && modelUnavailable) continue;
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Gemini generateContent returned HTTP " + response.StatusCode + ".");
            }

            return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                "No configured Gemini model was available.");
        }

        public static string BuildRequestJson(CommanderAIRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var contents = new JArray();
            int firstUserTurn = 0;
            while (firstUserTurn < request.ConversationHistory.Count
                && request.ConversationHistory[firstUserTurn].Role
                    != CommanderConversationRole.Player)
                firstUserTurn++;
            for (int i = firstUserTurn; i < request.ConversationHistory.Count; i++)
            {
                CommanderConversationMessage turn = request.ConversationHistory[i];
                contents.Add(new JObject
                {
                    ["role"] = turn.Role == CommanderConversationRole.Player ? "user" : "model",
                    ["parts"] = new JArray(new JObject { ["text"] = turn.Text })
                });
            }

            contents.Add(new JObject
            {
                ["role"] = "user",
                ["parts"] = new JArray(new JObject
                {
                    ["text"] = "Current fog-safe Commander context:\n"
                        + CommanderAIContextSerializer.Serialize(request.Context)
                        + "\nPlayer request:\n" + request.PlayerMessage
                })
            });

            var root = new JObject
            {
                ["system_instruction"] = new JObject
                {
                    ["parts"] = new JArray(new JObject { ["text"] = SystemInstruction })
                },
                ["contents"] = contents,
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = 0,
                    ["maxOutputTokens"] = 256,
                    ["responseMimeType"] = "application/json"
                }
            };
            return root.ToString(Formatting.None);
        }

        public static string ExtractModelText(string generateContentJson)
        {
            try
            {
                JObject root = JObject.Parse(generateContentJson ?? string.Empty);
                JArray candidates = root["candidates"] as JArray;
                JArray parts = candidates?[0]?["content"]?["parts"] as JArray;
                if (parts == null) return null;
                var builder = new StringBuilder();
                for (int i = 0; i < parts.Count; i++)
                {
                    JToken text = parts[i]?["text"];
                    if (text == null || text.Type != JTokenType.String) continue;
                    if (builder.Length > 0) builder.Append('\n');
                    builder.Append((string)text);
                }
                return builder.Length == 0 ? null : builder.ToString();
            }
            catch (JsonException) { return null; }
        }
    }

    public static class CommanderAIProviderFactory
    {
        public static ICommanderAIProvider CreateDefault()
        {
            string preference = Environment.GetEnvironmentVariable(
                "OPENEMPIRES_COMMANDER_PROVIDER");
            if (string.Equals(preference, "mock", StringComparison.OrdinalIgnoreCase))
                return new MockAIProvider();

            string key = DotEnvLoader.Get(GeminiAIProvider.KeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(key))
                return new GeminiAIProvider(key);
            return new MockAIProvider();
        }
    }
}
