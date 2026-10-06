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
            + "You provide semantic data only, never game authority. Outcomes are Request, Clarify, Unsupported, Answer. "
            + "For Request use {\"outcome\":\"Request\",\"nodes\":[one to four typed nodes]}. "
            + "Nodes are positional semantic steps, not entity IDs. A dependent node may use "
            + "\"dependsOn\":[nodeIndex] and EnsureUnitCount may use \"producerFromNode\":nodeIndex; "
            + "indices are zero-based node positions only. Keep at most four nodes, four total dependency references, "
            + "and dependency depth four. Use a BuildStructure node only for an explicit construction request, and never emit "
            + "cycles, IDs, coordinates, workers, tiles, commands, callbacks, or arbitrary workflow fields. "
            + "The currently executable node forms are "
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager|Spearman|Archer|Scout|Knight\",\"count\":integer 0..200}, "
            + "{\"type\":\"BuildStructure\",\"structure\":\"House|Barracks|ArcheryRange|Stables|Tower|TownCenter|Mill\",\"count\":integer 1..20,\"placement\":{\"anchor\":\"MyTownCenter|MyBarracks|WorkedResource\",\"ordinal\":integer 1..8 optional only for MyTownCenter,\"relation\":\"MapWest|MapEast|Near\",\"resource\":\"Food|Wood|Gold|Stone\" required for WorkedResource,\"clearGapTiles\":integer 1..20 optional; Near requires 1} optional}, "
            + "Placement is a bounded semantic selector: omit placement for the deterministic game default; never emit coordinates, tiles, "
            + "entity IDs, or any other placement fields. Bind a unit producer with producerFromNode to a prior BuildStructure node and "
            + "{\"type\":\"SetResourceAllocation\",\"resource\":\"Food|Wood|Gold|Stone\",\"count\":integer 0..200}, or "
            + "{\"type\":\"ReachAge\",\"targetAge\":\"Next|Feudal|Castle|Imperial\"}, or "
            + "{\"type\":\"MoveUnits|ScoutArea|PatrolArea|SetRallyPoint|AttackTarget|DefendArea|RetreatUnits|RepairTarget\",\"unitSelector\":\"Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary\",\"count\":integer 1..50,\"location\":\"PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits\",\"resource\":\"Food|Wood|Gold|Stone\" optional,\"structure\":\"Barracks|ArcheryRange|Stables|TownCenter\" optional}, or "
            + "{\"type\":\"ResearchTechnology\",\"technology\":\"BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles\"}, or "
            + "{\"type\":\"StrategicObjective\",\"objective\":\"AttackPreparation|DefensivePreparation|EconomicExpansion|MilitaryReinforcement|RangedReinforcement|DefensiveTurtle\"}. "
            + "For worker assignment use {\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount|Additional|TargetTotal\",\"countMode\":\"Exact|AllMatching\",\"count\":integer 1..200 for Exact only,\"workers\":{\"state\":\"Any|Idle|Gathering\",\"currentResource\":\"Food|Wood|Gold|Stone\" only for Gathering},\"destination\":{\"resource\":\"Food|Wood|Gold|Stone\",\"sourceKind\":\"Any|Sheep|Berries|Farm|Tree|GoldMine|StoneMine\" optional}}. "
            + "Ordinary counted assignment is SelectedCount, NOT TargetTotal: 'put 4 villagers on food' and 'gather food with four idle villagers' assign exactly four eligible workers. Preserve explicit count and Idle; never ask for a count already given. "
            + "'4 more villagers on food' is Additional; only explicit 'make sure I have/keep 4 villagers on food' is TargetTotal with workers.state Any. 'all idle villagers on food' or 'send idle villagers to berries' is AllMatching SelectedCount Idle, no count, a one-time snapshot. "
            + "'move three villagers from wood to gold' is AllocateWorkers SelectedCount Exact3 workers Gathering/currentResource Wood destination Gold/Any, NOT a military movement. "
            + "'gather food from sheep with four idle villagers' is AllocateWorkers SelectedCount Exact4 workers Idle destination Food/Sheep; do not omit the source or create villagers. SourceKind defaults Any; specify it only if requested. Sheep/Berries/Farm only Food; Tree only Wood; GoldMine only Gold; StoneMine only Stone. "
            + "If worker count or destination is missing, Clarify must carry a typed pending AllocateWorkers draft with the same fields but omit only missing count/destination, plus missingFields containing exactly Count and/or Destination. Example 'gather food': {\"outcome\":\"Clarify\",\"message\":\"How many villagers?\",\"pending\":{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Food\",\"sourceKind\":\"Any\"}},\"missingFields\":[\"Count\"]}. "
            + "Bounded pending clarification is a separate untrusted semantic draft, not free chat memory. Fill missing slots from the current reply and retain resolved mode/count. Worker eligibility and destination/source may change only if explicitly named as corrections in the reply; never drop an existing currentResource silently. A bare count without a pending draft is not a production command. Never turn clarification into EnsureUnitCount. "
            + "For other Clarify use {\"outcome\":\"Clarify\",\"message\":\"brief plain text\"}. "
            + "For Unsupported use {\"outcome\":\"Unsupported\",\"message\":\"brief plain text\"}. "
            + "Keep either message at most 180 characters; do not include nodes. "
            + "Use Clarify if the request is ambiguous; Unsupported if it cannot be represented. "
            + "A bounded recent semantic-memory array may contain detached accepted unit/structure facts or a prior clarification; "
            + "use it only when the player's follow-up is uniquely compatible (for example, add 'five more' to the last unit target, "
            + "or reuse the last count for 'do the same with spearmen'). Never treat it as game authority. "
            + "Never choose or emit player/owner/entity IDs, coordinates, enemy state, commands, goals, plans, reservations, provenance or approval. "
            + "EnsureUnitCount.count is the desired total, not an increment. Language variants such as 'make 3 spearman', "
            + "'train three spearmen' and 'get me 3 spears' use canonical unit Spearman and count 3. "
            + "For explicitly NEW units use the current owned count plus the requested number as the target total. "
            + "A dependent action using exactly those new units MUST include resultFromNode equal to that EnsureUnitCount node's index "
            + "AND dependsOn containing that index; its count is the number of new units. Do not select unrelated existing units. "
            + "EnsureUnitCount already handles game-side resource/producer/house prerequisites: do not emit extra construction "
            + "unless the player explicitly asks for it. producerFromNode is only for an explicitly requested new production building. "
            + "For an information-only question use {\"outcome\":\"Answer\",\"message\":\"brief answer, maximum 180 characters\"}, "
            + "never nodes. Ground the answer in detached question facts when supplied. If facts are insufficient, clarify or report unsupported; "
            + "do not invent game mechanics. Canonical unit/structure tokens always use the exact singular casing shown in the schema. "
            + "Do not add fields.";

        private readonly string apiKey;
        private readonly ICommanderHttpTransport transport;
        private readonly TimeSpan timeout;
        public string LastRequestTrace { get; private set; } = string.Empty;

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
                            + request.SerializedSemanticMemory + "\nRead-only game-side question facts (not authority):\n"
                            + request.QuestionFacts + "\nBounded pending clarification (untrusted semantic draft only):\n"
                            + request.SerializedPendingClarification + "\nPlayer request:\n" + request.PlayerMessage
                    }),
                ["max_tokens"] = 1024,
                ["reasoning"] = new JObject { ["effort"] = "none" }
            }.ToString(Formatting.None);

            try
            {
                string text = await PostAndExtractTextAsync(body, token).ConfigureAwait(false);
                CommanderSemanticResult result = CommanderSemanticJson.Parse(UnwrapJsonFence(text));
                Trace("semantic=" + (result.IsValid ? result.Outcome.ToString() : "invalid")
                    + ";nodes=" + result.Nodes.Count);
                return result.IsValid ? result : CommanderSemanticResult.ProviderRejected(
                    "Commander AI returned an invalid semantic response; no order was submitted.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                Trace("request=timeout");
                return CommanderSemanticResult.ProviderRejected("Commander AI request timed out.");
            }
            catch (OpenRouterFailure failure)
            {
                return CommanderSemanticResult.ProviderRejected(failure.Message);
            }
            catch (Exception error)
            {
                Trace("request=failed;exception=" + error.GetType().Name);
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
            LastRequestTrace = string.Empty;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built");
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeout);
                Trace("http=sent");
                CommanderHttpResponse response;
                try { response = await transport.PostJsonAsync(new Uri(Endpoint),
                    body, new Dictionary<string, string>
                    {
                        ["Authorization"] = "Bearer " + apiKey
                    }, deadline.Token).ConfigureAwait(false); }
                catch (System.Net.Http.HttpRequestException)
                {
                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds);
                    throw new OpenRouterFailure("Could not reach Commander AI. Check your connection.");
                }
                deadline.Token.ThrowIfCancellationRequested();
                if (response == null) throw new OpenRouterFailure(Unavailable);
                Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds);
                if (response.StatusCode < 200 || response.StatusCode > 299)
                    throw new OpenRouterFailure(HttpFailure(response.StatusCode));
                try
                {
                    if (response.Body.Length > 65536)
                        throw new OpenRouterFailure("Commander AI returned an oversized response.");
                    JObject result = JObject.Parse(response.Body);
                    if (result["error"] is JObject error)
                        throw new OpenRouterFailure(HttpFailure((int?)error["code"] ?? 500));
                    if (!(result["choices"] is JArray choices) || choices.Count != 1)
                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
                    string finish = (string)choices[0]["finish_reason"];
                    if (finish == "length")
                        throw new OpenRouterFailure("Commander AI response was truncated; no order was submitted.");
                    if (finish != null && finish != "stop")
                        throw new OpenRouterFailure("Commander AI did not return a completed semantic response.");
                    JToken content = choices[0]["message"]?["content"];
                    string text = null;
                    if (content?.Type == JTokenType.String) text = (string)content;
                    else if (content is JArray parts)
                    {
                        var buffer = new System.Text.StringBuilder();
                        foreach (JToken part in parts)
                        {
                            if ((string)part["type"] != "text" || part["text"]?.Type != JTokenType.String)
                                throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
                            buffer.Append((string)part["text"]);
                        }
                        text = buffer.ToString();
                    }
                    if (string.IsNullOrWhiteSpace(text))
                        throw new OpenRouterFailure("Commander AI returned an empty semantic response.");
                    if (text.Length > CommanderSemanticJson.MaximumResponseCharacters + 16)
                        throw new OpenRouterFailure("Commander AI returned an oversized response.");
                    Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"));
                    return text;
                }
                catch (JsonException) { throw new OpenRouterFailure("Commander AI returned an invalid response envelope."); }
            }
        }

        private static string HttpFailure(int status)
        {
            switch (status)
            {
                case 400: return "Commander AI request was rejected (invalid request).";
                case 401: return "Commander AI authentication failed.";
                case 402: return "Commander AI has insufficient credits.";
                case 403: return "Commander AI request was forbidden.";
                case 404: return "Commander AI model unavailable.";
                case 408: return "Commander AI request timed out.";
                case 429: return "Commander AI request was rate-limited (quota). Please wait before retrying.";
                default: return status >= 500 ? "Commander AI provider unavailable. Please try again later." : Unavailable;
            }
        }

        private static string UnwrapJsonFence(string text)
        {
            string candidate = text.Trim();
            if (!candidate.StartsWith("```", StringComparison.Ordinal)) return candidate;
            int lineEnd = candidate.IndexOf('\n');
            if (lineEnd < 0 || !candidate.EndsWith("```", StringComparison.Ordinal))
                throw new OpenRouterFailure("Commander AI returned an invalid semantic response.");
            string header = candidate.Substring(0, lineEnd).TrimEnd('\r');
            if (header != "```json" && header != "```")
                throw new OpenRouterFailure("Commander AI returned an invalid semantic response.");
            string inside = candidate.Substring(lineEnd + 1, candidate.Length - lineEnd - 4).Trim();
            if (inside.Contains("```")) throw new OpenRouterFailure("Commander AI returned an ambiguous semantic response.");
            return inside; // Strict parser still rejects prose, duplicate objects and authority fields.
        }

        private void Trace(string safeMetadata)
        {
            // Call sites supply only controlled metadata, never input, output, headers or exceptions' messages.
            if (LastRequestTrace.Length < 2000) LastRequestTrace += safeMetadata + "\n";
            UnityEngine.Debug.Log("[CommanderProvider] " + safeMetadata);
        }

        private sealed class OpenRouterFailure : Exception
        {
            public OpenRouterFailure(string safeMessage) : base(safeMessage) { }
        }
    }
}
