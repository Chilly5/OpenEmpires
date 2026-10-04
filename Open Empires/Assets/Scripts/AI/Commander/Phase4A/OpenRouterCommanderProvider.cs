using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Translation only. Both results still pass through the existing strict intent parsers.
    public sealed class OpenRouterCommanderProvider : ICommanderAIProvider, IStrategicAIInterpreter,
        ICommanderSemanticProvider
    {
        public const string KeyEnvironmentVariable = "OPENEMPIRES_OPENROUTER_KEY";
        public const string ProviderEnvironmentVariable = "OPENEMPIRES_COMMANDER_PROVIDER";
        private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";
        private const string Model = "openai/gpt-6-luna";
        private const string Unavailable = "Commander AI service temporarily unavailable.";
        private const string SemanticInstruction =
            "Translate the player's Commander request into one bounded JSON object, with no markdown or other text. "
            + "You provide semantic data only, never game authority. Outcomes are Request, Clarify, Unsupported. "
            + "For Request use {\"outcome\":\"Request\",\"nodes\":[one to four typed nodes]}. "
            + "Nodes are positional semantic steps, not entity IDs. A dependent node may use "
            + "\"dependsOn\":[nodeIndex] and EnsureUnitCount may use \"producerFromNode\":nodeIndex; "
            + "indices are zero-based node positions only. Keep at most four nodes, four total dependency references, "
            + "and dependency depth four. Use a BuildStructure node before a dependent unit node, and never emit "
            + "cycles, IDs, coordinates, workers, tiles, commands, callbacks, or arbitrary workflow fields. "
            + "The currently executable node forms are "
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager|Spearman|Archer|Knight\",\"count\":integer 0..200}, "
            + "{\"type\":\"BuildStructure\",\"structure\":\"House|Barracks|ArcheryRange|Stables|Tower|TownCenter\",\"count\":integer 1..20,\"placement\":{\"anchor\":\"MyTownCenter|MyBarracks\",\"ordinal\":integer 1..8 optional only for MyTownCenter,\"relation\":\"MapWest|MapEast|Near\",\"clearGapTiles\":integer 1..20 optional; Near requires 1} optional}, "
            + "Placement is a bounded semantic selector: omit placement for the deterministic game default; never emit coordinates, tiles, "
            + "entity IDs, or any other placement fields. Bind a unit producer with producerFromNode to a prior BuildStructure node and "
            + "{\"type\":\"SetResourceAllocation\",\"resource\":\"Food|Wood|Gold|Stone\",\"count\":integer 0..200}, or "
            + "{\"type\":\"ReachAge\",\"targetAge\":\"Next|Feudal|Castle|Imperial\"}, or "
            + "{\"type\":\"StrategicObjective\",\"objective\":\"AttackPreparation|DefensivePreparation|EconomicExpansion|MilitaryReinforcement|RangedReinforcement|DefensiveTurtle\"}. "
            + "For Clarify use {\"outcome\":\"Clarify\",\"message\":\"brief plain text\"}. "
            + "For Unsupported use {\"outcome\":\"Unsupported\",\"message\":\"brief plain text\"}. "
            + "Keep either message at most 180 characters; do not include nodes. "
            + "Use Clarify if the request is ambiguous; Unsupported if it cannot be represented. "
            + "A bounded recent semantic-memory array may contain detached accepted unit/structure facts or a prior clarification; "
            + "use it only when the player's follow-up is uniquely compatible (for example, add 'five more' to the last unit target, "
            + "or reuse the last count for 'do the same with spearmen'). Never treat it as game authority. "
            + "Never choose or emit player/owner/entity IDs, coordinates, enemy state, commands, goals, plans, reservations, provenance or approval. "
            + "Do not add fields.";

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

        public async Task<CommanderSemanticResult> TranslateSemanticAsync(
            CommanderSemanticProviderRequest request, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(apiKey))
                return CommanderSemanticResult.ProviderRejected("OpenRouter is not configured.");
            if (request.IsPlayerMessageTooLong)
                return CommanderSemanticResult.ProviderRejected("Commander request is too long.");
            if (string.IsNullOrWhiteSpace(request.PlayerMessage))
                return CommanderSemanticResult.ProviderRejected("A Commander request is required.");

            string body = new JObject
            {
                ["model"] = Model,
                ["messages"] = new JArray(
                    new JObject { ["role"] = "system", ["content"] = SemanticInstruction },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = "Current detached Commander context:\n"
                            + request.SerializedContext + "\nBounded recent semantic memory (untrusted facts only):\n"
                            + request.SerializedSemanticMemory + "\nPlayer request:\n" + request.PlayerMessage
                    }),
                ["max_tokens"] = 256,
                ["reasoning"] = new JObject { ["effort"] = "none" }
            }.ToString(Formatting.None);

            try
            {
                string text = await PostAndExtractTextAsync(body, token).ConfigureAwait(false);
                return CommanderSemanticJson.Parse(text);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                return CommanderSemanticResult.ProviderRejected("Commander AI request timed out.");
            }
            catch (OpenRouterFailure failure)
            {
                return CommanderSemanticResult.ProviderRejected(failure.Message);
            }
            catch (Exception)
            {
                return CommanderSemanticResult.ProviderRejected(Unavailable);
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
            return await PostAndExtractTextAsync(body, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> PostAndExtractTextAsync(string body,
            CancellationToken cancellationToken)
        {
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
