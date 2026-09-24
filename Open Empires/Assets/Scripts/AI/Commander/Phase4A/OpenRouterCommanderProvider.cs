using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Translation only. Both results still pass through the existing strict intent parsers.
    public sealed class OpenRouterCommanderProvider : ICommanderAIProvider, IStrategicAIInterpreter
    {
        public const string KeyEnvironmentVariable = "OPENEMPIRES_OPENROUTER_KEY";
        public const string ProviderEnvironmentVariable = "OPENEMPIRES_COMMANDER_PROVIDER";
        private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";
        private const string Model = "openai/gpt-6-luna";
        private const string Unavailable = "Commander AI service temporarily unavailable.";

        private readonly string apiKey;
        private readonly ICommanderHttpTransport transport;
        private readonly TimeSpan timeout;

        public OpenRouterCommanderProvider(string apiKey = null,
            ICommanderHttpTransport transport = null, TimeSpan? providerTimeout = null)
        {
            this.apiKey = apiKey ?? ReadSetting(KeyEnvironmentVariable);
            this.transport = transport ?? new CommanderHttpClientTransport();
            timeout = providerTimeout ?? TimeSpan.FromSeconds(15);
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(providerTimeout));
        }

        // The editor process may predate a new user-level setting. Project .env and
        // process variables retain precedence for existing setups.
        internal static string ReadSetting(string name)
        {
            string value = DotEnvLoader.Get(name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            try { return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User); }
            catch { return null; }
        }

        public async Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(apiKey))
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "OpenRouter is not configured.");
            try
            {
                string text = await RequestTextAsync(GeminiAIProvider.BuildRequestJson(request),
                    cancellationToken).ConfigureAwait(false);
                return CommanderAIJson.ParseTacticalIntent(text, request.Context);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Commander AI request timed out.");
            }
            catch (OpenRouterFailure failure)
            {
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    failure.Message);
            }
            catch (Exception)
            {
                return CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    Unavailable);
            }
        }

        public async Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
            StrategicAIRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(apiKey))
                return StrategicAIProviderResult.Rejected("OpenRouter is not configured.");
            if (string.IsNullOrWhiteSpace(request.PlayerMessage))
                return StrategicAIProviderResult.Rejected("A strategic request is required.");
            try
            {
                string text = await RequestTextAsync(
                    GeminiStrategicAIProvider.BuildRequestJson(request), cancellationToken)
                    .ConfigureAwait(false);
                return StrategicAIJson.Parse(text, request);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                return StrategicAIProviderResult.Rejected("Commander AI request timed out.");
            }
            catch (OpenRouterFailure failure)
            {
                return StrategicAIProviderResult.Rejected(failure.Message);
            }
            catch (Exception)
            {
                return StrategicAIProviderResult.Rejected(Unavailable);
            }
        }

        private async Task<string> RequestTextAsync(string geminiRequest,
            CancellationToken cancellationToken)
        {
            JObject source = JObject.Parse(geminiRequest);
            var messages = new JArray(new JObject
            {
                ["role"] = "system",
                ["content"] = (string)source["system_instruction"]?["parts"]?[0]?["text"]
            });
            foreach (JToken turn in (JArray)source["contents"])
                messages.Add(new JObject
                {
                    ["role"] = (string)turn["role"] == "model" ? "assistant" : "user",
                    ["content"] = (string)turn["parts"]?[0]?["text"]
                });
            string body = new JObject
            {
                ["model"] = Model,
                ["messages"] = messages,
                ["max_tokens"] = 256,
                ["reasoning"] = new JObject { ["effort"] = "none" }
            }.ToString(Formatting.None);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                CommanderHttpResponse response = await transport.PostJsonAsync(new Uri(Endpoint),
                    body, new Dictionary<string, string>
                    {
                        ["Authorization"] = "Bearer " + apiKey
                    }, deadline.Token).ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
                if (response == null) throw new OpenRouterFailure(Unavailable);
                if (response.StatusCode == 401 || response.StatusCode == 403)
                    throw new OpenRouterFailure("Commander AI authentication failed.");
                if (response.StatusCode == 429)
                    throw new OpenRouterFailure(
                        "Commander AI quota exhausted. Please wait or use offline commands.");
                if (response.StatusCode < 200 || response.StatusCode > 299)
                    throw new OpenRouterFailure(Unavailable);
                try
                {
                    JObject result = JObject.Parse(response.Body);
                    string text = (string)result["choices"]?[0]?["message"]?["content"];
                    if (string.IsNullOrWhiteSpace(text)) throw new OpenRouterFailure(Unavailable);
                    return text;
                }
                catch (JsonException) { throw new OpenRouterFailure(Unavailable); }
            }
        }

        private sealed class OpenRouterFailure : Exception
        {
            public OpenRouterFailure(string safeMessage) : base(safeMessage) { }
        }
    }
}
