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
            + "You provide semantic data only, never game authority. Outcomes are Request, DynamicPlan, Clarify, Unsupported, Answer. "
            + "For Request use {\"outcome\":\"Request\",\"nodes\":[one to four typed nodes]}. Every compound Request MUST also declare executionOrder as Independent, Sequential, or DependencyGraph. Independent has no dependencies; Sequential means each step waits for the previous step's actual completion and each later node MUST include dependsOn:[previousIndex]. DependencyGraph describes explicitly mixed ordering with all requested edges retained. Temporal first/then/after/once/when-complete/only-after-completion wording describes these same supported completion dependencies, not an unsupported workflow. Order effects as stated and retain the edges. Existing owned actors remain existing unless the player explicitly requests newly produced actors; a completed building does not imply production of a new villager. Indefinite singular a/an/one denotes count 1. Do not invent an EnsureUnitCount or resultFromNode for an ordinary existing-worker follow-up. Never drop temporal restrictions. If you cannot represent them, Clarify or Unsupported, not a different plan. "
            + "Nodes are positional semantic steps, not entity IDs. A dependent node may use "
            + "\"dependsOn\":[nodeIndex] and EnsureUnitCount may use \"producerFromNode\":nodeIndex; "
            + "indices are zero-based node positions only. Keep at most four nodes, eight total dependency references, "
            + "and dependency depth four. Use a BuildStructure node only for an explicit construction request, and never emit "
            + "cycles, entity/worker IDs or objects, coordinates, tiles, commands, callbacks, or arbitrary workflow fields. Typed worker eligibility selectors below are permitted. "
            + "The currently executable node forms are "
            + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Villager|Spearman|Archer|Scout|Knight\",\"count\":integer 0..200,\"quantityMode\":\"TargetTotal|New\" optional}, "
            + "EnsureUnitCount defaults to TargetTotal: owned and matching queued units count toward that total. Explicit 'new'/'more'/'additional' uses quantityMode New, count means exactly that many new request-attributed units regardless of old units or human queues. A resultFromNode unit action consumes an exact newly produced set, never existing/human-produced units; its count must equal the game-side production forecast. Do not widen a total goal to new production to repair a mismatch; Clarify or preserve the original request for deterministic preflight rejection. "
            + "{\"type\":\"BuildStructure\",\"structure\":\"exact name from detached structureCapabilities\",\"count\":integer 1..20,\"builders\":{\"state\":\"Eligible|IdleOnly\",\"count\":1},\"placement\":optional one of the following DISJOINT forms}. "
            + "Town Center placement: {\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest|MapEast|Near\",\"ordinal\":optional integer 1..8,\"clearGapTiles\":optional integer 1..20; Near permits only 1}. Barracks placement: {\"anchor\":\"MyBarracks\",\"relation\":\"MapWest|MapEast|Near\",\"clearGapTiles\":optional integer 1..20; Near permits only 1}. Resource placement: {\"anchor\":\"VisibleResource|WorkedResource\",\"relation\":\"Near\",\"resource\":\"Food|Wood|Gold|Stone\",\"sourceKind\":optional \"Any|Berries|Tree|GoldMine|StoneMine|Farm|Sheep\"}. Resource placement NEVER contains ordinal. Ordinal is NOT a default field and is forbidden outside MyTownCenter. For resource Near placement omit clearGapTiles unless the supported value 1 is explicitly needed. Never carry fields from another placement form. "
            + "Every Request BuildStructure MUST declare builders. Eligible is the game's ordinary eligible-builder policy when the player did not restrict builders; IdleOnly is a strict idle-only selector, not a preference. Builder count is separate from structure count. This ordinary path supports exactly one builder; another explicit builder quantity cannot be dropped or defaulted to one: Clarify or Unsupported if no existing typed form can preserve it. Never emit Eligible for an explicit idle-only restriction. "
            + "Optional node constraints use exact objects: {\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}, {\"type\":\"ProtectedResource\",\"resource\":\"Gold\",\"amount\":optional integer 0..200}, {\"type\":\"NoConstruction\"}, {\"type\":\"MaximumQueue\",\"amount\":integer 1..8}, {\"type\":\"ResourceSource\",\"resource\":\"Food\",\"sourceKind\":\"Sheep\"}. Preserve explicitly requested restrictions on every affected node and prerequisite; if essential restrictions are unrepresentable, clarify instead of omitting them. Builders IdleOnly is normalized game-side to PreferredWorkers IdleOnly; any duplicated constraint must agree. DynamicPlan instead uses its existing top-level constraint vocabulary. "
            + "Resource-relative construction uses VisibleResource unless the player explicitly requests a currently worked source. Near the woodline uses VisibleResource/Wood/Tree; near berry bushes uses VisibleResource/Food/Berries, neither requires gathering. Near berries my villagers are working uses WorkedResource/Food/Berries strictly. Never introduce gathering to satisfy an anchor. Always declare builders: Eligible when unrestricted, IdleOnly when idle-only was requested, count 1. Do not take workers off gold uses ProtectedResource Gold, retaining those restrictions through all prerequisites and retries. "
            + "Placement is a bounded semantic selector: omit placement for the deterministic game default; never emit coordinates, tiles, "
            + "entity IDs, or any other placement fields. Bind a unit producer with producerFromNode to a prior BuildStructure node and "
            + "{\"type\":\"SetResourceAllocation\",\"resource\":\"Food|Wood|Gold|Stone\",\"count\":integer 0..200}, or "
            + "{\"type\":\"ReachAge\",\"targetAge\":integer 2..4 or \"Next|Feudal|Castle|Imperial\"}, or "
            + "Absolute age identities are Age 2 = Feudal, Age 3 = Castle, Age 4 = Imperial. Numerical and named targets have identical meanings, independent of the current age. An explicit numerical/named target must never become Next. Next is relative and used only when the player actually requests the next age. "
            + "{\"type\":\"MoveUnits|ScoutArea|PatrolArea|SetRallyPoint|AttackTarget|DefendArea|RetreatUnits|RepairTarget\",\"unitSelector\":\"Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary\",\"count\":integer 1..50,\"location\":\"PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits\",\"resource\":\"Food|Wood|Gold|Stone\" optional,\"structure\":\"Barracks|ArcheryRange|Stables|TownCenter\" optional}, or "
            + "{\"type\":\"ResearchTechnology\",\"technology\":\"BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles\"}, or "
            + "Location selectors for movement/defense/scouting/patrol resolve one game-side anchor point, not an area. PatrolArea uses the ordinary start-to-anchor patrol route, never a circular perimeter. Explicit radius, perimeter, circle or rear-of-another-group constraints cannot be represented: Clarify or Unsupported rather than dropping the restriction or claiming a point command satisfies it. Unspecified colloquial 'patrol near/around a resource' may use a point anchor only when no area restriction was requested, and must be described honestly. Raw coordinates, bridge-specific tiles, hidden-base locations and unsupported group anchors remain prohibited; construction clearGapTiles is the separate supported footprint-to-footprint placement convention, not an action radius. "
            + "AttackTarget and RepairTarget accept a separate optional target object: {\"kind\":\"UnitType\",\"unit\":\"Villager|Spearman|Archer|Scout|Knight\"} or {\"kind\":\"BuildingType\",\"structure\":a canonical building name from selectorCapabilities.targets}. Preserve every explicitly named target; unitSelector always describes the actors, never the enemy. RepairTarget requires Villagers, PlayerBase and a building target; AttackTarget requires VisibleEnemy and military actors. Omit target only for genuinely generic requests. A named repair building selects the lowest-ID owned matching building before checking damage; another is not substituted when it is already repaired. Enemy targets select the nearest currently visible matching type to the first owned Town Center, ties by ID, and ordinary combat may later retarget. Never promise a lasting only-attack-type policy. Ambiguous 'those' or selected-group targets unsupported by the contract must Clarify, never invent IDs or infer a different type. "
            + "{\"type\":\"StrategicObjective\",\"objective\":\"AttackPreparation|DefensivePreparation|EconomicExpansion|MilitaryReinforcement|RangedReinforcement|DefensiveTurtle\"}. "
            + "Names are canonical game content from the relevant knowledge slice and detached capabilities, not limited to the examples above. Recognize exact/normalized names and aliases; for typo/functional suggestions show the canonical interpretation. Ambiguous names require Clarify; never silently choose a different civilization variant. Known but unsupported content must be explained as unsupported, not unknown; honor civilization/age availability and normal prerequisites. Construction/training may use any canonical name in their respective capabilities; action selectors keep their own separate bounded vocabulary. "
            + "Finite future-unit orders use {\"type\":\"WatchFutureUnits\",\"unit\":canonical unit name,\"count\":integer 1..50,\"producer\":canonical producer building name,\"producerOrdinal\":integer 1..8 only if explicitly selected,\"action\":\"Gather\",\"resource\":\"Food|Wood|Gold|Stone\",\"sourceKind\":optional requested source kind}; or action Patrol, location WorkedResource, resource Gold etc. 'next five villagers from my TC to wood' watches the next five actual births from the bound producer, INCLUDING existing human queues, and NEVER queues production. Multiple matching producers without explicit ordinal require clarification. 'Train five new villagers and send them to wood' instead uses EnsureUnitCount quantityMode New plus AllocateWorkers SelectedCount Exact5 workers Any with dependsOn:[0],resultFromNode:0; only that exact request's new production results count, never old units/human queues. These future/compound requests require the existing explicit preview confirmation. Never drop future-order qualifiers or replace them with all-existing worker allocation. Every future unit/indefinite policies are unsupported. "
            + "Natural producer ordinals first/second/third/fourth/fifth/sixth/seventh/eighth are 1/2/3/4/5/6/7/8, equivalent to numbered producer wording. An explicit ordinal is not ambiguous merely because several producers exist; emit producerOrdinal and let the game resolve the exact owned producer. Do not confuse a producer's first/second ordinal with temporal ordering of actions. If the requested follow-up action is absent, clarify that missing action rather than inventing it or discarding the resolved ordinal. "
            + "For worker assignment use {\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount|Additional|TargetTotal\",\"countMode\":\"Exact|AllMatching\",\"count\":integer 1..200 for Exact only,\"workers\":{\"state\":\"Any|Idle|Gathering\",\"currentResource\":\"Food|Wood|Gold|Stone\" only for Gathering},\"destination\":{\"resource\":\"Food|Wood|Gold|Stone\",\"sourceKind\":\"Any|Sheep|Berries|Farm|Tree|GoldMine|StoneMine\" optional},\"resourceAmount\":optional integer 1..1000000,\"resourceAmountMode\":optional \"Stockpile|AdditionalGathered\"}. The two resource-amount fields are supplied together for a finite resource objective, or both omitted for an ordinary assignment. They are supported fields in this same typed form, not a different mechanic or a reason to ask for an already specified worker count. "
            + "Ordinary counted assignment is SelectedCount, NOT TargetTotal: 'put 4 villagers on food' and 'gather food with four idle villagers' assign exactly four eligible workers. Preserve explicit count and Idle; never ask for a count already given. "
            + "Resource amounts are NOT worker counts. For 'gather/have 400 food', include resourceAmount:400 and resourceAmountMode:Stockpile on AllocateWorkers and on its pending draft; if worker count is missing ask it while retaining resource, amount and source. For 'gather 800 additional food' use AdditionalGathered. These are long-running resource objectives; worker assignment alone does not complete them. Display the selected stockpile/additional meaning. Bare 4/four fills only the missing Count locally. Preserve all resolved amount/meaning fields on continuation; never reinterpret a bare number as a different action or approval. "
            + "Resolve resource amount and worker quantity as independent slots, including numbers written as words. Additional/new RESOURCE income selects resourceAmountMode AdditionalGathered, not worker mode Additional; only additional WORKERS select that worker mode. When both quantities are supplied, neither slot is missing and no Count clarification is needed. Example: gather 700 additional Wood using three idle villagers -> {\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":3,\"workers\":{\"state\":\"Idle\"},\"destination\":{\"resource\":\"Wood\",\"sourceKind\":\"Any\"},\"resourceAmount\":700,\"resourceAmountMode\":\"AdditionalGathered\"}. Preserve explicit source restrictions in destination.sourceKind without changing either quantity. Never ask the player to restate a number already given. "
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
            + "Never choose or emit player/owner/entity IDs, coordinates, enemy state, commands, reservations, provenance or approval. "
            + "Semantic requested effects and explicit strategy advice may be described, but never authorize or instantiate runtime goals or plans. "
            + "Do not introduce unrequested producer buildings, attacks, age advances or strategic objectives. "
            + "A question about Castle age is Answer or Clarify, while an explicit request to reach Castle age may be ReachAge. "
            + "Keep faithful known requests, including representable multi-node composition, in typed Request form; use one DynamicPlan only for genuine representation gaps, subject to player confirmation. Never replace requested content with another building merely because a short example omits it. "
            + "EnsureUnitCount.count is the desired total, not an increment. Language variants such as 'make 3 spearman', "
            + "'train three spearmen' and 'get me 3 spears' use canonical unit Spearman and count 3. "
            + "For explicitly NEW units use quantityMode New and the requested new count; never add existing units to this count. The game computes the authoritative forecast. "
            + "A dependent action using exactly those new units MUST include resultFromNode equal to that EnsureUnitCount node's index "
            + "AND dependsOn containing that index; its count is the number of new units. Do not select unrelated existing units. "
            + "EnsureUnitCount already handles game-side resource/producer/house prerequisites: do not emit extra construction "
            + "unless the player explicitly asks for it. producerFromNode is only for an explicitly requested new production building. "
            + "For an information-only question use {\"outcome\":\"Answer\",\"message\":\"brief answer, maximum 180 characters\"}, "
            + "never nodes. Ground the answer in detached question facts when supplied. If facts are insufficient, clarify or report unsupported; "
            + "do not invent game mechanics. Canonical unit/structure tokens always use the exact singular casing shown in the schema. "
            + "Do not add fields.";

        private const string SemanticFidelityCheck =
            " Final interpretation check for the existing typed Request forms: a/an worker or villager is EXACTLY ONE eligible existing worker, not an unspecified count. Number words also fill their respective integer slots. A supplied quantity must not become a Count question. Resource amount and worker count stay separate; preserve the requested source and absolute age. A completion phrase connects effects with dependsOn, not newly produced actors. Example 'Build a Mill, and after completion send a worker to gold' is {\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Gold\"},\"dependsOn\":[0]}]}. Clarify only genuinely missing meaning; no runtime IDs, coordinates, commands, authority, dropped constraints or invented production.";

        private readonly string apiKey;
        private readonly ICommanderHttpTransport transport;
        private readonly TimeSpan timeout;
        private readonly int semanticMaxTokens;
        public string LastRequestTrace { get; private set; } = string.Empty;
        internal bool HasConfiguration => !string.IsNullOrWhiteSpace(apiKey);
        internal bool UsesGateway => transport is CommanderGatewaySemanticTransport;
        internal bool UsesTransport(ICommanderHttpTransport candidate) => ReferenceEquals(transport,candidate);
        internal bool HasGatewayConfiguration => transport is CommanderGatewaySemanticTransport gateway && gateway.IsConfigured;
        internal int? LastHttpStatusCode { get; private set; }
        internal bool LastNetworkFailure { get; private set; }
        internal bool LastRequestTimedOut { get; private set; }
        internal bool HasAttemptedRequest { get; private set; }
        internal bool HasValidatedSemanticResponse { get; private set; }

        public OpenRouterCommanderProvider(string apiKey = null,
            ICommanderHttpTransport transport = null, TimeSpan? providerTimeout = null,
            int semanticMaxTokens = 4096)
        {
            this.apiKey = apiKey ?? ReadSetting(KeyEnvironmentVariable);
            this.transport = transport ?? new CommanderHttpClientTransport();
            timeout = providerTimeout ?? TimeSpan.FromSeconds(15);
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(providerTimeout));
            if (semanticMaxTokens < 1024 || semanticMaxTokens > 4096)
                throw new ArgumentOutOfRangeException(nameof(semanticMaxTokens));
            this.semanticMaxTokens = semanticMaxTokens;
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
            HasValidatedSemanticResponse = false;
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
                    new JObject { ["role"] = "system", ["content"] = SemanticInstruction
                        + CommanderDynamicProviderVocabulary.Build(request.SerializedContext) + SemanticFidelityCheck },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = "Current detached Commander context:\n"
                            + request.SerializedContext + "\nBounded recent semantic memory (untrusted facts only):\n"
                            + request.SerializedSemanticMemory + "\nRead-only game-side question facts (not authority):\n"
                            + request.QuestionFacts + "\nBounded pending clarification (untrusted semantic draft only):\n"
                            + (request.IsReadOnlyQuestion?"INFORMATION-ONLY turn: Answer/Clarify/Unsupported only; never Request/DynamicPlan, instructions or approval. Tactical status is observed game-side; do not invent a reason or choose an ambiguous request.\n":string.Empty)
                            + request.SerializedPendingClarification + "\nPlayer request:\n" + request.PlayerMessage
                    }),
                ["max_tokens"] = semanticMaxTokens,
                ["reasoning"] = new JObject { ["effort"] = "none" }
            }.ToString(Formatting.None);

            try
            {
                string text = UnwrapJsonFence(await PostAndExtractTextAsync(body, token,
                    CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
                CommanderSemanticResult result = CommanderSemanticJson.ParseProviderResponse(text);
                if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic);
                if (!result.IsValid && CommanderSemanticSchemaRepair.TryCreateTemplate(text,
                    out JObject template, requireProviderDeclarations: true))
                {
                    string repairBody = BuildRepairBody(template);
                    string initialTrace = LastRequestTrace;
                    string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
                        token, CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
                    LastRequestTrace = initialTrace + "repair-attempt:\n" + LastRequestTrace;
                    result = CommanderSemanticJson.ParseProviderResponse(repairedText);
                    if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic);
                    if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                        repairedText, template))
                        result = CommanderSemanticResult.ProviderRejected(
                            "Commander AI returned an invalid semantic response; no order was submitted.");
                }
                Trace("semantic=" + (result.IsValid ? result.Outcome.ToString() : "invalid")
                    + ";nodes=" + result.Nodes.Count);
                HasValidatedSemanticResponse = result.IsValid;
                return result.IsValid ? result : CommanderSemanticResult.ProviderRejected(
                    "Commander AI returned an invalid semantic response; no order was submitted.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (OperationCanceledException)
            {
                Trace("request=timeout");
                LastRequestTimedOut = true;
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
            return await PostAndExtractTextAsync(body, cancellationToken,
                CommanderSemanticJson.MaximumResponseCharacters).ConfigureAwait(false);
        }

        private string BuildRepairBody(JObject template)
        {
            return new JObject
            {
                ["model"] = Model,
                ["messages"] = new JArray(
                    new JObject { ["role"] = "system", ["content"] =
                        "Schema-only repair. Return exactly the supplied JSON data with unchanged outcome, fields, values, roots and references. "
                        + "Only preserve the already-normalized numeric scalar shape. No new content, explanation or markdown." },
                    new JObject { ["role"] = "user", ["content"] =
                        "Return this normalized semantic JSON template exactly (property order may differ):\n"
                        + template.ToString(Formatting.None) }),
                ["max_tokens"] = semanticMaxTokens,
                ["reasoning"] = new JObject { ["effort"] = "none" }
            }.ToString(Formatting.None);
        }

        private async Task<string> PostAndExtractTextAsync(string body,
            CancellationToken cancellationToken, int maximumContentCharacters)
        {
            LastRequestTrace = string.Empty;
            HasAttemptedRequest = true; LastHttpStatusCode = null;
            LastNetworkFailure = false; LastRequestTimedOut = false;
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
                catch (CommanderHttpResponseLimitException error)
                {
                    LastHttpStatusCode = error.StatusCode;
                    Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds);
                    throw new OpenRouterFailure(error.StatusCode < 200 || error.StatusCode > 299
                        ? HttpFailure(error.StatusCode) : "Commander AI returned an oversized response.");
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    LastNetworkFailure = true;
                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds);
                    throw new OpenRouterFailure("Could not reach Commander AI. Check your connection.");
                }
                deadline.Token.ThrowIfCancellationRequested();
                if (response == null) throw new OpenRouterFailure(Unavailable);
                LastHttpStatusCode = response.StatusCode;
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
                    // Bounded diagnostic category BEFORE rejection; never echo
                    // arbitrary provider finish strings or the response body.
                    Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"));
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
                    if (text.Length > maximumContentCharacters + 16)
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
