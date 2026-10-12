# Frozen Task1 fix2 source review package
Baseline4c9de96... tracked cumulative diff; original/current shared pack files full. Prior fixes distinguished by report. GREEN missing: Unity bridge unavailable, unresolved0da job, no test rerun authorized here.
diff --git a/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs b/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs
index 6ad8c5e..708d666 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs	
@@ -4,20 +4,24 @@ using System.Linq;
 
 namespace OpenEmpires
 {
     public sealed class CommanderContextBuilder
     {
         // Call once at submission on the simulation's owning (Unity main) thread.
         public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager)
             => Build(simulation, manager, string.Empty);
 
         public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager, string knowledgeQuery)
+            => Build(simulation, manager, knowledgeQuery, null);
+
+        public CommanderContext Build(GameSimulation simulation, CommanderGoalManager manager, string knowledgeQuery,
+            string boundedPending)
         {
             if (simulation == null) throw new ArgumentNullException(nameof(simulation));
             if (manager == null) throw new ArgumentNullException(nameof(manager));
             int player = manager.PlayerId;
             if (player < 0 || player >= simulation.PlayerCount) throw new ArgumentOutOfRangeException(nameof(manager));
             var resources = simulation.ResourceManager.GetPlayerResources(player);
             var counts = new SortedDictionary<int, int>();
             var queued = new SortedDictionary<int, int>();
             var workerAllocation = new SortedDictionary<ResourceType, int>();
             int idleVillagers = 0;
@@ -127,21 +131,21 @@ namespace OpenEmpires
             var workerAllocationSnapshots = new List<CommanderWorkerAllocationSnapshot>();
             foreach (KeyValuePair<ResourceType, int> entry in workerAllocation)
                 workerAllocationSnapshots.Add(new CommanderWorkerAllocationSnapshot(
                     entry.Key, entry.Value));
             var visibleEnemyMilitary = new List<CommanderVisibleEnemyMilitarySnapshot>();
             foreach (KeyValuePair<int, int> entry in visibleEnemyCounts)
                 visibleEnemyMilitary.Add(new CommanderVisibleEnemyMilitarySnapshot(
                     entry.Key, entry.Value));
             var catalog = GameKnowledgeCatalog.Build(simulation);
             string knowledgeContext = CommanderKnowledgeRetrieval.Retrieve(catalog, knowledgeQuery,
-                simulation.GetPlayerCivilization(player));
+                simulation.GetPlayerCivilization(player), boundedPending);
             var civilization = catalog.Civilizations.FirstOrDefault(c =>
                 c.Civilization == simulation.GetPlayerCivilization(player));
             // Detached projections over the same canonical data and implemented
             // adapters used at compilation; no name-to-ID table or invented costs.
             var canonicalUnits = civilization == null ? Array.Empty<string>() : catalog.Units
                 .Where(u => CommanderDynamicCompiler.TryResolveTrainingRequest(u, catalog, simulation, player, out _)
                     && civilization.AvailableUnitIds.Contains(u.StableId))
                 .Select(u => u.StableId).ToArray();
             var canonicalBuildings = civilization == null ? Array.Empty<string>() : catalog.Buildings
                 .Where(b => CommanderIntentCatalog.IsSupportedStructure(b.BuildingType)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs
index ae93489..a16dea5 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs	
@@ -236,20 +236,27 @@ namespace OpenEmpires
             {
                 // Explicit strategic controls may invalidate an in-flight translation;
                 // the busy guard must not prevent the human from pausing/cancelling it.
                 // Pending worker slot-filling stays serialized with its active reply.
                 if (pendingClarification == null) TryHandleStrategicLifecycle(trimmed);
                 return null;
             }
             currentRequestGoalId = 0;
             if (HandlePendingControl(trimmed)) return null;
             if (HandleActionPlanControl(trimmed)) return null;
+            if (pendingClarification == null
+                && (NormalizeWholeForm(trimmed) == "yes" || NormalizeWholeForm(trimmed) == "no"))
+            {
+                AppendLine("Player", trimmed);
+                AppendLine("Commander", "No Commander question or action plan is awaiting that answer.");
+                return null;
+            }
             ClearActionPlanPreview(); // An independent player turn supersedes an unapproved candidate.
             // Any new player turn dismisses an adaptation, while no-plan recommendations
             // retain the established preference/explanation query behavior.
             if (pendingAdaptationProposal != null)
             {
                 strategicBridge?.ClearPending();
                 ClearStrategicPreview();
             }
             if (TryHandleStrategicLifecycle(trimmed)) return null;
             if (TryHandleExplanationQuery(trimmed)) return null;
@@ -437,37 +444,47 @@ namespace OpenEmpires
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
                     semanticStage = "context-projection";
                     CommanderContext context = new CommanderContextBuilder().Build(simulation, manager,
-                        capturedPending?.OriginalText ?? message);
+                        message, capturedPending?.Serialize());
                     if (context.PlayerId != owner)
                     {
                         AppendLine("Commander", "Commander ownership changed; ask again.");
                         return null;
                     }
                     IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemoryAtTurnStart =
                         Conversation.SemanticMemory.Snapshot();
                     TryObserveQuestionAnswer(NormalizeExplanationWholeForm(message), out string questionFacts);
                     var providerRequest = new CommanderSemanticProviderRequest(message, context,
                         semanticMemoryAtTurnStart, questionFacts, capturedPending);
-                    semanticStage = "provider-request";
-                    Debug.Log("[Commander] context=projected;provider-request=started");
                     CommanderSemanticResult localCount = LocalCountContinuation(capturedPending, message);
-                    Task<CommanderSemanticResult> translation = localCount == null
+                    CommanderSemanticResult localGraph = LocalGraphContinuation(capturedPending, message);
+                    if (capturedPending?.GraphDraft != null && localGraph == null)
+                    {
+                        semanticStage = "graph-local-reask";
+                        ReaskPending(CommanderRequestGraphDraft.IsCorrectionUtterance(message)
+                            ? "Please identify one graph node and field to correct with a supported value."
+                            : "That reply did not identify one safe graph detail.");
+                        return null;
+                    }
+                    semanticStage = localCount != null || localGraph != null ? "local-continuation" : "provider-request";
+                    if (localCount == null && localGraph == null)
+                        Debug.Log("[Commander] context=projected;provider-request=started");
+                    Task<CommanderSemanticResult> translation = localCount == null && localGraph == null
                         ? RequestSemanticTranslation(provider, providerRequest, request.Token)
-                        : Task.FromResult(localCount);
+                        : Task.FromResult(localCount ?? localGraph);
                     // A provider may ignore cancellation. Keep the host usable and observe
                     // any late fault without ever admitting its late response.
                     _ = translation.ContinueWith(task => { var ignored = task.Exception; },
                         CancellationToken.None,
                         TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                         TaskScheduler.Default);
                     Task finished = await Task.WhenAny(translation,
                         Task.Delay(semanticProviderTimeout, request.Token));
                     if (finished != translation)
                     {
@@ -499,54 +516,56 @@ namespace OpenEmpires
                     if (!string.IsNullOrEmpty(questionFacts)
                         && (result.Outcome == CommanderSemanticOutcome.Request || result.Outcome == CommanderSemanticOutcome.DynamicPlan))
                     {
                         AppendLine("Commander", "You asked for information; no gameplay order was submitted.");
                         Debug.Log("[Commander] question=read-only;executable-response=rejected");
                         return null;
                     }
                     if (result.Outcome != CommanderSemanticOutcome.Request
                         && result.Outcome != CommanderSemanticOutcome.DynamicPlan)
                     {
-                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                        if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                            ReferenceEquals(result, localGraph))) return null;
                         if (result.Outcome == CommanderSemanticOutcome.Clarify)
                             Conversation.SemanticMemory.RecordClarification(result.SafeExplanation);
                         AppendLine("Commander", string.IsNullOrWhiteSpace(result.SafeExplanation)
                             ? "Please clarify or try a supported Commander request."
                             : result.SafeExplanation);
                         return null;
                     }
-                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket)) return null;
+                    if (HandleClarificationResult(result, capturedPending, message, generation, requestTicket,
+                        ReferenceEquals(result, localGraph))) return null;
                     if (result.Outcome == CommanderSemanticOutcome.DynamicPlan)
                     {
                         semanticStage = "dynamic-admission";
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
                     }
                     if (result.Nodes == null || result.Nodes.Count < 1
                         || result.Nodes.Count > CommanderSemanticGraphAdmission.MaximumNodes)
                     {
                         AppendLine("Commander", "That compound Commander request is outside the bounded request limit.");
                         return null;
                     }
                     if (result.Nodes.Count > 1 || result.Nodes[0].Type == CommanderSemanticNodeType.WatchFutureUnits)
                     {
                         semanticStage = "graph-admission";
                         if (!CommanderSemanticGraphAdmission.TryAdmit(result, context,
                             out CommanderSemanticGraphPlan graph, out string graphReason))
                         {
                             AppendLine("Commander", graphReason);
                             return null;
                         }
-                        pendingActionPlan = manager.PrepareActionPlan(result, message, generation,
+                        pendingActionPlan = manager.PrepareActionPlan(result, requestTicket.OriginalInput, generation,
                             strategicPipeline, IsCurrentScope, requestTicket);
                         AppendLine("Commander", pendingActionPlan.Preview);
                         UpdateStrategicControls();
                         return null;
                     }
                     CommanderSemanticNode node = result.Nodes[0];
                     if (node.Type == CommanderSemanticNodeType.StrategicObjective)
                     {
                         if (!node.StrategicObjectiveType.HasValue || bridge == null
                             || sourcePipeline == null || !sourceCaptured)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 2cf6816..93ea016 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs	
@@ -1,12 +1,14 @@
 using System;
 using System.Collections.Generic;
+using System.Collections.ObjectModel;
+using System.Text;
 using System.Threading;
 using System.Threading.Tasks;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     // Translation only. Both results still pass through the existing strict intent parsers.
     public sealed class OpenRouterCommanderProvider : ICommanderAIProvider, IStrategicAIInterpreter,
         ICommanderSemanticProvider
@@ -80,30 +82,81 @@ namespace OpenEmpires
             + "Do not add fields.";
 
         private const string SemanticFidelityCheck =
             " Final interpretation check for the existing typed Request forms: a/an worker or villager is EXACTLY ONE eligible existing worker, not an unspecified count. Number words also fill their respective integer slots. A supplied quantity must not become a Count question. Resource amount and worker count stay separate; preserve the requested source and absolute age. A completion phrase connects effects with dependsOn, not newly produced actors. Example 'Build a Mill, and after completion send a worker to gold' is {\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":[{\"type\":\"BuildStructure\",\"structure\":\"Mill\",\"count\":1,\"builders\":{\"state\":\"Eligible\",\"count\":1}},{\"type\":\"AllocateWorkers\",\"mode\":\"SelectedCount\",\"countMode\":\"Exact\",\"count\":1,\"workers\":{\"state\":\"Any\"},\"destination\":{\"resource\":\"Gold\"},\"dependsOn\":[0]}]}. Clarify only genuinely missing meaning; no runtime IDs, coordinates, commands, authority, dropped constraints or invented production.";
 
         private readonly string apiKey;
         private readonly ICommanderHttpTransport transport;
         private readonly TimeSpan timeout;
         private readonly int semanticMaxTokens;
         public string LastRequestTrace { get; private set; } = string.Empty;
+        public CommanderProviderUsage LastUsage { get; private set; } = CommanderProviderUsage.Unknown;
+        public int? LastInputEstimate { get; private set; }
+        public IReadOnlyDictionary<string, int> LastInputSectionEstimates { get; private set; }
+        private readonly object semanticGate = new object();
+        private SemanticRun activeSemanticRun;
+        private int semanticHttpAttemptCount;
+        private readonly List<CommanderProviderAttempt> recentSemanticAttempts = new List<CommanderProviderAttempt>();
+        public int SemanticHttpAttemptCount { get { lock (semanticGate) return semanticHttpAttemptCount; } }
+        public IReadOnlyList<CommanderProviderAttempt> RecentSemanticAttempts { get { lock (semanticGate)
+            return Array.AsReadOnly(recentSemanticAttempts.ToArray()); } }
+        public IReadOnlyList<CommanderProviderAttempt> LastSemanticAttempts { get; private set; }
+            = Array.AsReadOnly(Array.Empty<CommanderProviderAttempt>());
         internal bool HasConfiguration => !string.IsNullOrWhiteSpace(apiKey);
         internal bool UsesGateway => transport is CommanderGatewaySemanticTransport;
         internal bool UsesTransport(ICommanderHttpTransport candidate) => ReferenceEquals(transport,candidate);
         internal bool HasGatewayConfiguration => transport is CommanderGatewaySemanticTransport gateway && gateway.IsConfigured;
         internal int? LastHttpStatusCode { get; private set; }
         internal bool LastNetworkFailure { get; private set; }
         internal bool LastRequestTimedOut { get; private set; }
         internal bool HasAttemptedRequest { get; private set; }
         internal bool HasValidatedSemanticResponse { get; private set; }
 
+        private sealed class SemanticRun
+        {
+            public readonly List<CommanderProviderAttempt> Attempts = new List<CommanderProviderAttempt>();
+            public string Trace = string.Empty;
+            public CommanderProviderUsage Usage = CommanderProviderUsage.Unknown;
+            public int? HttpStatusCode;
+            public bool NetworkFailure;
+            public bool TimedOut;
+            public bool Attempted;
+            public bool Validated;
+        }
+
+        private void Publish(SemanticRun run)
+        {
+            lock (semanticGate)
+            {
+                if (!ReferenceEquals(activeSemanticRun, run)) return;
+                LastRequestTrace = run.Trace;
+                LastUsage = run.Usage;
+                LastHttpStatusCode = run.HttpStatusCode;
+                LastNetworkFailure = run.NetworkFailure;
+                LastRequestTimedOut = run.TimedOut;
+                HasAttemptedRequest = run.Attempted;
+                HasValidatedSemanticResponse = run.Validated;
+                LastSemanticAttempts = Array.AsReadOnly(run.Attempts.ToArray());
+            }
+        }
+
+        private void PublishEstimate(SemanticRun run, int? estimate,
+            IReadOnlyDictionary<string, int> sections)
+        {
+            lock (semanticGate)
+            {
+                if (!ReferenceEquals(activeSemanticRun, run)) return;
+                LastInputEstimate = estimate;
+                LastInputSectionEstimates = sections;
+            }
+        }
+
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
@@ -182,89 +235,97 @@ namespace OpenEmpires
             {
                 return StrategicAIProviderResult.Rejected(Unavailable);
             }
         }
 
         public async Task<CommanderSemanticResult> TranslateSemanticAsync(
             CommanderSemanticProviderRequest request, CancellationToken token)
         {
             if (request == null) throw new ArgumentNullException(nameof(request));
             token.ThrowIfCancellationRequested();
-            HasValidatedSemanticResponse = false;
+            var run = new SemanticRun();
+            lock (semanticGate) activeSemanticRun = run;
+            Publish(run);
+            PublishEstimate(run, null, null);
             if (string.IsNullOrWhiteSpace(apiKey))
                 return CommanderSemanticResult.ProviderRejected("OpenRouter is not configured.");
             if (request.IsPlayerMessageTooLong)
                 return CommanderSemanticResult.ProviderRejected("Commander request is too long.");
             if (string.IsNullOrWhiteSpace(request.PlayerMessage))
                 return CommanderSemanticResult.ProviderRejected("A Commander request is required.");
 
-            string body = new JObject
+            if (!CommanderSemanticRequestPack.TryBuild(request, Model, semanticMaxTokens, out var packed, out string limitReason))
             {
-                ["model"] = Model,
-                ["messages"] = new JArray(
-                    new JObject { ["role"] = "system", ["content"] = SemanticInstruction
-                        + CommanderDynamicProviderVocabulary.Build(request.SerializedContext) + SemanticFidelityCheck },
-                    new JObject
-                    {
-                        ["role"] = "user",
-                        ["content"] = "Current detached Commander context:\n"
-                            + request.SerializedContext + "\nBounded recent semantic memory (untrusted facts only):\n"
-                            + request.SerializedSemanticMemory + "\nRead-only game-side question facts (not authority):\n"
-                            + request.QuestionFacts + "\nBounded pending clarification (untrusted semantic draft only):\n"
-                            + (request.IsReadOnlyQuestion?"INFORMATION-ONLY turn: Answer/Clarify/Unsupported only; never Request/DynamicPlan, instructions or approval. Tactical status is observed game-side; do not invent a reason or choose an ambiguous request.\n":string.Empty)
-                            + request.SerializedPendingClarification + "\nPlayer request:\n" + request.PlayerMessage
-                    }),
-                ["max_tokens"] = semanticMaxTokens,
-                ["reasoning"] = new JObject { ["effort"] = "none" }
-            }.ToString(Formatting.None);
+                PublishEstimate(run, packed?.EstimatedInputTokens, packed?.SectionEstimates);
+                return CommanderSemanticResult.ProviderRejected(limitReason);
+            }
+            PublishEstimate(run, packed.EstimatedInputTokens, packed.SectionEstimates);
+            string body = packed.Json;
 
             try
             {
                 string text = UnwrapJsonFence(await PostAndExtractTextAsync(body, token,
-                    CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
+                    CommanderDynamicPlan.MaximumCharacters, "initial", packed.EstimatedInputTokens,
+                    packed.SectionEstimates, run).ConfigureAwait(false));
                 CommanderSemanticResult result = CommanderSemanticJson.ParseProviderResponse(text);
-                if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic);
+                MarkLastSemanticAttempt(run, result.IsValid ? "semantic-valid" : "schema-invalid");
+                if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic, run);
                 if (!result.IsValid && CommanderSemanticSchemaRepair.TryCreateTemplate(text,
                     out JObject template, requireProviderDeclarations: true))
                 {
                     string repairBody = BuildRepairBody(template);
-                    string initialTrace = LastRequestTrace;
+                    int repairEstimate = CommanderSemanticRequestPack.EstimateBody(JObject.Parse(repairBody));
+                    if (repairEstimate >= CommanderSemanticRequestPack.InputLimit)
+                        return CommanderSemanticResult.ProviderRejected("Commander input limit: required schema repair cannot fit safely; no repair was submitted.");
+                    JObject repairJson = JObject.Parse(repairBody);
+                    var repairSections = new Dictionary<string, int> {
+                        ["contract"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[0]?["content"] ?? string.Empty),
+                        ["repairTemplate"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[1]?["content"] ?? string.Empty),
+                        ["messageMargin"] = CommanderSemanticRequestPack.MessageMargin };
+                    string initialTrace = run.Trace;
                     string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
-                        token, CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
-                    LastRequestTrace = initialTrace + "repair-attempt:\n" + LastRequestTrace;
+                        token, CommanderDynamicPlan.MaximumCharacters, "repair", repairEstimate,
+                        repairSections, run).ConfigureAwait(false));
+                    run.Trace = initialTrace + "repair-attempt:\n" + run.Trace;
+                    Publish(run);
                     result = CommanderSemanticJson.ParseProviderResponse(repairedText);
-                    if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic);
+                    if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic, run);
                     if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                         repairedText, template))
                         result = CommanderSemanticResult.ProviderRejected(
                             "Commander AI returned an invalid semantic response; no order was submitted.");
+                    MarkLastSemanticAttempt(run, result.IsValid ? "semantic-valid" : "schema-invalid");
                 }
                 Trace("semantic=" + (result.IsValid ? result.Outcome.ToString() : "invalid")
-                    + ";nodes=" + result.Nodes.Count);
-                HasValidatedSemanticResponse = result.IsValid;
+                    + ";nodes=" + result.Nodes.Count, run);
+                run.Validated = result.IsValid;
+                Publish(run);
                 return result.IsValid ? result : CommanderSemanticResult.ProviderRejected(
                     "Commander AI returned an invalid semantic response; no order was submitted.");
             }
             catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
             catch (OperationCanceledException)
             {
-                Trace("request=timeout");
-                LastRequestTimedOut = true;
+                Trace("request=timeout", run);
+                run.TimedOut = true;
+                Publish(run);
                 return CommanderSemanticResult.ProviderRejected("Commander AI request timed out.");
             }
             catch (OpenRouterFailure failure)
             {
+                if (run.Attempts.Count > 0 && run.Attempts[run.Attempts.Count - 1].ResultStage == "response-extracted")
+                    MarkLastSemanticAttempt(run, "content-invalid-format");
                 return CommanderSemanticResult.ProviderRejected(failure.Message);
             }
             catch (Exception error)
             {
-                Trace("request=failed;exception=" + error.GetType().Name);
+                Trace("request=failed;exception=" + error.GetType().Name, run);
                 return CommanderSemanticResult.ProviderRejected(Unavailable);
             }
         }
 
         private async Task<string> RequestTextAsync(string geminiRequest,
             CancellationToken cancellationToken)
         {
             JObject source = JObject.Parse(geminiRequest);
             var messages = new JArray(new JObject
             {
@@ -298,96 +359,207 @@ namespace OpenEmpires
                         "Schema-only repair. Return exactly the supplied JSON data with unchanged outcome, fields, values, roots and references. "
                         + "Only preserve the already-normalized numeric scalar shape. No new content, explanation or markdown." },
                     new JObject { ["role"] = "user", ["content"] =
                         "Return this normalized semantic JSON template exactly (property order may differ):\n"
                         + template.ToString(Formatting.None) }),
                 ["max_tokens"] = semanticMaxTokens,
                 ["reasoning"] = new JObject { ["effort"] = "none" }
             }.ToString(Formatting.None);
         }
 
+        private void MarkLastSemanticAttempt(SemanticRun run, string stage)
+        {
+            if (run.Attempts.Count > 0)
+            {
+                var previous = run.Attempts[run.Attempts.Count - 1];
+                var updated = previous.WithResultStage(stage);
+                run.Attempts[run.Attempts.Count - 1] = updated;
+                lock (semanticGate)
+                {
+                    int index = recentSemanticAttempts.FindLastIndex(attempt => ReferenceEquals(attempt, previous));
+                    if (index >= 0) recentSemanticAttempts[index] = updated;
+                }
+                Publish(run);
+            }
+        }
+
         private async Task<string> PostAndExtractTextAsync(string body,
-            CancellationToken cancellationToken, int maximumContentCharacters)
+            CancellationToken cancellationToken, int maximumContentCharacters, string semanticKind = null,
+            int? inputEstimate = null, IReadOnlyDictionary<string, int> sectionEstimates = null,
+            SemanticRun run = null)
         {
-            LastRequestTrace = string.Empty;
-            HasAttemptedRequest = true; LastHttpStatusCode = null;
-            LastNetworkFailure = false; LastRequestTimedOut = false;
+            if (run != null)
+            {
+                run.Trace = string.Empty;
+                run.Usage = CommanderProviderUsage.Unknown;
+                run.Attempted = true;
+                run.HttpStatusCode = null;
+                run.NetworkFailure = false;
+                run.TimedOut = false;
+                lock (semanticGate) semanticHttpAttemptCount++;
+                Publish(run);
+            }
+            else
+            {
+                LastRequestTrace = string.Empty;
+                LastUsage = CommanderProviderUsage.Unknown;
+                HasAttemptedRequest = true; LastHttpStatusCode = null;
+                LastNetworkFailure = false; LastRequestTimedOut = false;
+            }
+            CommanderProviderUsage attemptUsage = CommanderProviderUsage.Unknown;
+            int? attemptHttpStatus = null;
             var elapsed = System.Diagnostics.Stopwatch.StartNew();
-            Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built");
+            string resultStage = "http-start";
+            Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built", run);
+            try
+            {
             using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
             {
                 deadline.CancelAfter(timeout);
-                Trace("http=sent");
+                Trace("http=sent", run);
                 CommanderHttpResponse response;
                 try { response = await transport.PostJsonAsync(new Uri(Endpoint),
                     body, new Dictionary<string, string>
                     {
                         ["Authorization"] = "Bearer " + apiKey
                     }, deadline.Token).ConfigureAwait(false); }
                 catch (CommanderHttpResponseLimitException error)
                 {
-                    LastHttpStatusCode = error.StatusCode;
-                    Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds);
+                    resultStage = "response-byte-limit";
+                    attemptHttpStatus = error.StatusCode;
+                    if (run != null) { run.HttpStatusCode = attemptHttpStatus; Publish(run); }
+                    else LastHttpStatusCode = attemptHttpStatus;
+                    Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds, run);
                     throw new OpenRouterFailure(error.StatusCode < 200 || error.StatusCode > 299
                         ? HttpFailure(error.StatusCode) : "Commander AI returned an oversized response.");
                 }
                 catch (System.Net.Http.HttpRequestException)
                 {
-                    LastNetworkFailure = true;
-                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds);
+                    resultStage = "network-failure";
+                    if (run != null) { run.NetworkFailure = true; Publish(run); }
+                    else LastNetworkFailure = true;
+                    Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds, run);
                     throw new OpenRouterFailure("Could not reach Commander AI. Check your connection.");
                 }
                 deadline.Token.ThrowIfCancellationRequested();
-                if (response == null) throw new OpenRouterFailure(Unavailable);
-                LastHttpStatusCode = response.StatusCode;
-                Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds);
+                if (response == null)
+                {
+                    resultStage = "transport-null";
+                    throw new OpenRouterFailure(Unavailable);
+                }
+                attemptHttpStatus = response.StatusCode;
+                if (run != null) { run.HttpStatusCode = attemptHttpStatus; Publish(run); }
+                else LastHttpStatusCode = attemptHttpStatus;
+                resultStage = response.StatusCode >= 200 && response.StatusCode <= 299 ? "http-success" : "http-failure";
+                Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds, run);
                 if (response.StatusCode < 200 || response.StatusCode > 299)
                     throw new OpenRouterFailure(HttpFailure(response.StatusCode));
                 try
                 {
                     if (response.Body.Length > 65536)
+                    {
+                        resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
+                    }
                     JObject result = JObject.Parse(response.Body);
+                    attemptUsage = CommanderProviderUsage.Read(result);
+                    if (run != null) { run.Usage = attemptUsage; Publish(run); }
+                    else LastUsage = attemptUsage;
                     if (result["error"] is JObject error)
+                    {
+                        resultStage = "provider-error";
                         throw new OpenRouterFailure(HttpFailure((int?)error["code"] ?? 500));
-                    if (!(result["choices"] is JArray choices) || choices.Count != 1)
+                    }
+                    if (!(result["choices"] is JArray choices) || choices.Count != 1 || !(choices[0] is JObject))
+                    {
+                        resultStage = "response-invalid-envelope";
                         throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                    }
                     string finish = (string)choices[0]["finish_reason"];
                     // Bounded diagnostic category BEFORE rejection; never echo
                     // arbitrary provider finish strings or the response body.
-                    Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"));
+                    Trace("finish="+(finish=="stop"?"stop":finish=="length"?"length":finish==null?"unspecified":"other"), run);
                     if (finish == "length")
+                    {
+                        resultStage = "response-truncated";
                         throw new OpenRouterFailure("Commander AI response was truncated; no order was submitted.");
+                    }
                     if (finish != null && finish != "stop")
+                    {
+                        resultStage = "response-incomplete";
                         throw new OpenRouterFailure("Commander AI did not return a completed semantic response.");
-                    JToken content = choices[0]["message"]?["content"];
+                    }
+                    if (!(choices[0]["message"] is JObject message))
+                    {
+                        resultStage = "response-invalid-envelope";
+                        throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                    }
+                    JToken content = message["content"];
                     string text = null;
                     if (content?.Type == JTokenType.String) text = (string)content;
                     else if (content is JArray parts)
                     {
                         var buffer = new System.Text.StringBuilder();
                         foreach (JToken part in parts)
                         {
-                            if ((string)part["type"] != "text" || part["text"]?.Type != JTokenType.String)
+                            if (!(part is JObject textPart) || (string)textPart["type"] != "text"
+                                || textPart["text"]?.Type != JTokenType.String)
+                            {
+                                resultStage = "response-unsupported-parts";
                                 throw new OpenRouterFailure("Commander AI returned unsupported content parts.");
-                            buffer.Append((string)part["text"]);
+                            }
+                            buffer.Append((string)textPart["text"]);
                         }
                         text = buffer.ToString();
                     }
                     if (string.IsNullOrWhiteSpace(text))
+                    {
+                        resultStage = "response-empty";
                         throw new OpenRouterFailure("Commander AI returned an empty semantic response.");
+                    }
                     if (text.Length > maximumContentCharacters + 16)
+                    {
+                        resultStage = "response-oversize";
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
-                    Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"));
+                    }
+                    Trace("assistantContent=extracted;characters=" + text.Length + ";finish=" + (finish ?? "unspecified"), run);
+                    resultStage = "response-extracted";
                     return text;
                 }
-                catch (JsonException) { throw new OpenRouterFailure("Commander AI returned an invalid response envelope."); }
+                catch (JsonException)
+                {
+                    resultStage = "response-invalid-envelope";
+                    throw new OpenRouterFailure("Commander AI returned an invalid response envelope.");
+                }
+            }
+            }
+            catch (OperationCanceledException)
+            {
+                resultStage = cancellationToken.IsCancellationRequested ? "caller-canceled" : "timeout";
+                throw;
+            }
+            finally
+            {
+                if (semanticKind != null)
+                {
+                    var attempt = new CommanderProviderAttempt(semanticKind,
+                        inputEstimate ?? CommanderSemanticRequestPack.EstimateBody(JObject.Parse(body)),
+                        sectionEstimates ?? new Dictionary<string, int>(), attemptUsage,
+                        elapsed.ElapsedMilliseconds, attemptHttpStatus, resultStage);
+                    run.Attempts.Add(attempt);
+                    lock (semanticGate)
+                    {
+                        recentSemanticAttempts.Add(attempt);
+                        if (recentSemanticAttempts.Count > 32) recentSemanticAttempts.RemoveAt(0);
+                    }
+                    Publish(run);
+                }
             }
         }
 
         private static string HttpFailure(int status)
         {
             switch (status)
             {
                 case 400: return "Commander AI request was rejected (invalid request).";
                 case 401: return "Commander AI authentication failed.";
                 case 402: return "Commander AI has insufficient credits.";
@@ -407,23 +579,28 @@ namespace OpenEmpires
             if (lineEnd < 0 || !candidate.EndsWith("```", StringComparison.Ordinal))
                 throw new OpenRouterFailure("Commander AI returned an invalid semantic response.");
             string header = candidate.Substring(0, lineEnd).TrimEnd('\r');
             if (header != "```json" && header != "```")
                 throw new OpenRouterFailure("Commander AI returned an invalid semantic response.");
             string inside = candidate.Substring(lineEnd + 1, candidate.Length - lineEnd - 4).Trim();
             if (inside.Contains("```")) throw new OpenRouterFailure("Commander AI returned an ambiguous semantic response.");
             return inside; // Strict parser still rejects prose, duplicate objects and authority fields.
         }
 
-        private void Trace(string safeMetadata)
+        private void Trace(string safeMetadata, SemanticRun run = null)
         {
             // Call sites supply only controlled metadata, never input, output, headers or exceptions' messages.
-            if (LastRequestTrace.Length < 2000) LastRequestTrace += safeMetadata + "\n";
+            if (run != null)
+            {
+                if (run.Trace.Length < 2000) run.Trace += safeMetadata + "\n";
+                Publish(run);
+            }
+            else if (LastRequestTrace.Length < 2000) LastRequestTrace += safeMetadata + "\n";
             UnityEngine.Debug.Log("[CommanderProvider] " + safeMetadata);
         }
 
         private sealed class OpenRouterFailure : Exception
         {
             public OpenRouterFailure(string safeMessage) : base(safeMessage) { }
         }
     }
 }
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs
index a901a06..53a18e6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs	
@@ -115,21 +115,30 @@ namespace OpenEmpires
                         ["BuildingType"] = new JArray(Enum.GetNames(typeof(BuildingType))) }
                 },
                 ["knowledge"] = ParseKnowledge(context.KnowledgeContext)
             };
             var gathering = new JObject();
             foreach (var allocation in context.WorkerAllocation.OrderBy(x => x.ResourceType))
                 gathering[allocation.ResourceType.ToString()] = Math.Min(200, allocation.AssignedWorkers);
             root["workerCounts"] = new JObject { ["idleUnqueued"] = Math.Min(200, context.IdleVillagers), ["gatheringByResource"] = gathering };
             if(status!=null)root["tacticalStatus"]=status.Json();
             string serialized = root.ToString(Formatting.None);
-            if(status!=null&&serialized.Length>MaximumContextCharacters)
+            if (serialized.Length > MaximumContextCharacters
+                && root["knowledge"]?["requiredCanonicalIds"] is JArray required && required.Count > 0)
+            {
+                // Do not silently drop required rows to satisfy the detached-context bound.
+                // The pack recognizes this small marker and refuses the complete request.
+                root["knowledge"] = new JObject { ["requiredReferenceOverflow"] = true };
+                serialized = root.ToString(Formatting.None);
+            }
+            if(status!=null&&serialized.Length>MaximumContextCharacters
+                && (bool?)root["knowledge"]?["requiredReferenceOverflow"] != true)
             {
                 root["knowledge"]=new JObject{["omitted"]=true,["reason"]="Tactical status prioritized within unchanged context bound."};
                 serialized=root.ToString(Formatting.None);
             }
             if (serialized.Length > MaximumContextCharacters)
                 throw new InvalidOperationException("Semantic context exceeded its limit.");
             return serialized;
         }
 
         private static JToken ParseKnowledge(string json)
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs
index c7d1678..76b18d6 100644
--- a/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs	
+++ b/Open Empires/Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs	
@@ -3,97 +3,97 @@ using System.Collections.Generic;
 using System.Linq;
 using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires
 {
     // Retrieval only: lexical similarity never chooses an entity or authorizes an effect.
     internal static class CommanderKnowledgeRetrieval
     {
-        internal const int MaximumEntries = 8;
-        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization)
+        // Required references are independent of optional lexical suggestions. The finite
+        // ceiling is a refusal boundary, never permission to omit the seventeenth reference.
+        internal const int MaximumRequiredEntries = 16;
+        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization,
+            string boundedPending = null)
         {
-            var unitScores = new Dictionary<string, int>(StringComparer.Ordinal);
-            var buildingScores = new Dictionary<string, int>(StringComparer.Ordinal);
+            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
+            var required = new HashSet<string>(StringComparer.Ordinal);
+            var matchedPhrases = new HashSet<string>(StringComparer.Ordinal);
             var resolutions = new List<JObject>();
-            string text = (input ?? string.Empty);
-            if (text.Length > 1024) text = text.Substring(0, 1024);
-            var words = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
-                .Cast<Match>().Select(x => x.Value).Take(96).ToArray();
-            string searchable = " " + string.Join(" ", words) + " ";
-            foreach (var unit in catalog.Units)
-                ScoreAliases(unitScores, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
-            foreach (var building in catalog.Buildings)
-                ScoreAliases(buildingScores, building.StableId, building.DisplayName, building.Aliases, searchable);
-
-            var phrases = new HashSet<string>(StringComparer.Ordinal);
-            for (int i = 0; i < words.Length && phrases.Count < 128; i++)
-                for (int length = 1; length <= 4 && i + length <= words.Length && phrases.Count < 128; length++)
-                {
-                    string phrase = string.Join(" ", words.Skip(i).Take(length));
-                    if (phrase.Length > 64 || !phrases.Add(phrase)) continue;
-                    var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
-                    var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
-                    if (unit.Match != null) Raise(unitScores, unit.Match.StableId, 20 + phrase.Length);
-                    foreach (var suggestion in unit.Suggestions) Raise(unitScores, suggestion.StableId, 3);
-                    if (building.Match != null) Raise(buildingScores, building.Match.StableId, 20 + phrase.Length);
-                    foreach (var suggestion in building.Suggestions) Raise(buildingScores, suggestion.StableId, 3);
-                    if (resolutions.Count < 6 && (unit.Suggestions.Count > 0 || building.Suggestions.Count > 0))
-                        resolutions.Add(new JObject { ["phrase"] = phrase,
-                            ["interpretationOnly"] = true,
-                            ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
-                            ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
-                            ["ambiguous"] = unit.Status == CommanderContentResolutionStatus.Ambiguous
-                                || building.Status == CommanderContentResolutionStatus.Ambiguous });
-                }
-            var chosen = unitScores.Select(x => (id: x.Key, score: x.Value, unit: true))
-                .Concat(buildingScores.Select(x => (id: x.Key, score: x.Value, unit: false)))
-                .OrderByDescending(x => x.score).ThenBy(x => x.id, StringComparer.Ordinal)
-                .Take(MaximumEntries).ToArray();
+            foreach (string source in new[] { input ?? string.Empty, boundedPending ?? string.Empty })
+            {
+                // Both sources already have independent caller bounds (1024 player, 4096 pending).
+                // Scan the complete bounded strings; prefix/word/phrase cuts lose tail facts.
+                string searchable = " " + string.Join(" ", Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
+                    .Cast<Match>().Select(x => x.Value)) + " ";
+                foreach (var unit in catalog.Units)
+                    MatchAliases(required, matchedPhrases, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
+                foreach (var building in catalog.Buildings)
+                    MatchAliases(required, matchedPhrases, building.StableId, building.DisplayName, building.Aliases, searchable);
+                // Role words such as "cavalry" are resolver-defined ambiguous names, not
+                // a new local name table. Deduplication bounds resolver work for long replies.
+                foreach (string word in Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
+                    .Cast<Match>().Select(x => x.Value).Distinct(StringComparer.Ordinal))
+                    if (word.Length >= 3) matchedPhrases.Add(word);
+            }
+            foreach (string phrase in matchedPhrases.OrderBy(x => x, StringComparer.Ordinal))
+            {
+                var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
+                var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
+                if (unit.Status != CommanderContentResolutionStatus.Ambiguous
+                    && building.Status != CommanderContentResolutionStatus.Ambiguous) continue;
+                foreach (var candidate in unit.Suggestions) required.Add(candidate.StableId);
+                foreach (var candidate in building.Suggestions) required.Add(candidate.StableId);
+                resolutions.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
+                    ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
+                    ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
+                    ["ambiguous"] = true });
+            }
+            bool overflow = required.Count > MaximumRequiredEntries || resolutions.Count > 6;
+            var chosen = overflow ? Array.Empty<string>() : required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
             var units = new JArray(); var buildings = new JArray();
-            foreach (var entry in chosen)
+            foreach (string id in chosen)
             {
-                if (entry.unit)
+                if (id.StartsWith("unit:", StringComparison.Ordinal))
                 {
-                    var data = catalog.FindUnit(entry.id);
+                    var data = catalog.FindUnit(id);
                     var resolution = CommanderContentNameResolver.ResolveUnit(data.StableId, catalog, civilization);
                     units.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["producer"] = data.ProductionBuildingType < 0
                             ? null : ((BuildingType)data.ProductionBuildingType).ToString(),
                         ["cost"] = Cost(data.Cost), ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                         ["commanderExecutable"] = resolution.IsExecutable });
                 }
                 else
                 {
-                    var data = catalog.FindBuilding(entry.id);
+                    var data = catalog.FindBuilding(id);
                     var resolution = CommanderContentNameResolver.ResolveBuilding(data.StableId, catalog, civilization);
                     buildings.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                         ["age"] = data.RequiredAge, ["cost"] = Cost(data.Cost),
                         ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                         ["commanderExecutable"] = resolution.IsExecutable,
                         ["dropoffResources"] = new JArray(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                             .Where(r => GameSimulation.AcceptsResourceType(data.BuildingType, r)).Select(r => r.ToString())) });
                 }
             }
             return new JObject { ["units"] = units, ["buildings"] = buildings,
-                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(resolutions),
+                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(overflow ? Enumerable.Empty<JObject>() : resolutions),
+                ["requiredCanonicalIds"] = new JArray(chosen), ["requiredReferenceOverflow"] = overflow,
                 ["policy"] = "Knowledge is not execution authority. Show canonical interpretations; clarify ambiguous names. Availability and support are separate." }
                 .ToString(Formatting.None);
         }
 
         private static JObject Cost(KnowledgeCost value) => new JObject
             { ["food"] = value.Food, ["wood"] = value.Wood, ["gold"] = value.Gold, ["stone"] = value.Stone };
-        private static void Raise(Dictionary<string, int> scores, string id, int score)
-        { if (!scores.TryGetValue(id, out int old) || old < score) scores[id] = score; }
-        private static void ScoreAliases(Dictionary<string, int> scores, string id, string name,
+        private static void MatchAliases(HashSet<string> required, HashSet<string> matchedPhrases, string id, string name,
             IEnumerable<string> aliases, string searchable)
         {
             foreach (string label in new[] { name }.Concat(aliases))
             {
                 string normalized = Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
                 if (normalized.Length > 0 && searchable.Contains(" " + normalized + " "))
-                    Raise(scores, id, 30 + normalized.Length);
+                { required.Add(id); matchedPhrases.Add(normalized); }
             }
         }
     }
 }
# Full source: D:\unity_projects\OpenEmpires\Open Empires\Assets\Scripts\AI\Commander\Phase5C\CommanderProviderUsage.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Reported observations only: no headers, response text, credentials or game authority.
    public sealed class CommanderProviderUsage
    {
        public long? PromptTokens { get; }
        public long? CompletionTokens { get; }
        public long? TotalTokens { get; }
        public long? CachedTokens { get; }
        public long? ReasoningTokens { get; }
        public decimal? Cost { get; }
        public bool HasReportedUsage { get; }
        public static CommanderProviderUsage Unknown { get; } = new CommanderProviderUsage(null);

        private CommanderProviderUsage(JObject value)
        {
            PromptTokens = NonnegativeInteger(value?["prompt_tokens"]);
            CompletionTokens = NonnegativeInteger(value?["completion_tokens"]);
            TotalTokens = NonnegativeInteger(value?["total_tokens"]);
            CachedTokens = NonnegativeInteger((value?["prompt_tokens_details"] as JObject)?["cached_tokens"]);
            ReasoningTokens = NonnegativeInteger((value?["completion_tokens_details"] as JObject)?["reasoning_tokens"]);
            try
            {
                JToken cost = value?["cost"];
                if (cost != null && (cost.Type == JTokenType.Float || cost.Type == JTokenType.Integer))
                { decimal amount = cost.Value<decimal>(); if (amount >= 0) Cost = amount; }
            }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException) { }
            HasReportedUsage = PromptTokens.HasValue || CompletionTokens.HasValue || TotalTokens.HasValue
                || CachedTokens.HasValue || ReasoningTokens.HasValue || Cost.HasValue;
        }

        public static CommanderProviderUsage Read(JObject envelope)
            => envelope?["usage"] is JObject usage ? new CommanderProviderUsage(usage) : Unknown;

        private static long? NonnegativeInteger(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer) return null;
            try { long count = value.Value<long>(); return count >= 0 ? count : (long?)null; }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException)
            { return null; }
        }
    }

    // Immutable, redacted observation of one HTTP attempt. It never retains payloads or headers.
    public sealed class CommanderProviderAttempt
    {
        public string Kind { get; }
        public int InputEstimate { get; }
        public IReadOnlyDictionary<string, int> SectionEstimates { get; }
        public CommanderProviderUsage Usage { get; }
        public long ElapsedMilliseconds { get; }
        public int? HttpStatusCode { get; }
        public string ResultStage { get; }

        public CommanderProviderAttempt(string kind, int inputEstimate, IReadOnlyDictionary<string, int> sections,
            CommanderProviderUsage usage, long elapsedMilliseconds, int? httpStatusCode, string resultStage)
        {
            Kind = kind;
            InputEstimate = inputEstimate;
            SectionEstimates = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(sections));
            Usage = usage ?? CommanderProviderUsage.Unknown;
            ElapsedMilliseconds = elapsedMilliseconds;
            HttpStatusCode = httpStatusCode;
            ResultStage = resultStage;
        }

        internal CommanderProviderAttempt WithResultStage(string stage) => new CommanderProviderAttempt(
            Kind, InputEstimate, SectionEstimates, Usage, ElapsedMilliseconds, HttpStatusCode, stage);
    }
}
# Full source: D:\unity_projects\OpenEmpires\Open Empires\Assets\Scripts\AI\Commander\Phase5C\CommanderSemanticRequestPack.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Retrieval/packing only. Nothing here parses an order, selects runtime actors or grants approval.
    internal sealed class CommanderSemanticRequestPack
    {
        internal const int InputLimit = 4000;
        internal const int MessageMargin = 256;
        internal string Json { get; }
        internal int EstimatedInputTokens { get; }
        internal IReadOnlyDictionary<string, int> SectionEstimates { get; }

        private CommanderSemanticRequestPack(JObject body, Dictionary<string, int> sections)
        {
            Json = body.ToString(Formatting.None);
            EstimatedInputTokens = EstimateBody(body);
            SectionEstimates = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(sections);
        }

        // Deliberately conservative UTF8 byte units, not characters/4 or an unverified tokenizer.
        // Actual endpoint-reported prompt usage is recorded separately; this is NOT certification
        // of a rolling alias's tokenizer/hidden endpoint overhead before live calibration.
        internal static int EstimateBody(JObject body)
        {
            JToken modelFacing = body["messages"];
            if (modelFacing == null) return int.MaxValue;
            long estimate = MessageMargin;
            if (!(modelFacing is JArray messages)) return int.MaxValue;
            foreach (var message in messages.OfType<JObject>())
                foreach (string field in new[] { "role", "content", "name", "tool_calls", "tool_call_id" })
                    if (message[field] != null) estimate += Encoding.UTF8.GetByteCount(message[field].Type == JTokenType.String
                        ? (string)message[field] : message[field].ToString(Formatting.None));
            foreach (string field in new[] { "tools", "response_format", "functions", "tool_choice", "function_call" })
                if (body[field] != null) estimate += Encoding.UTF8.GetByteCount(body[field].ToString(Formatting.None));
            return estimate > int.MaxValue ? int.MaxValue : (int)estimate;
        }

        internal static bool TryBuild(CommanderSemanticProviderRequest request, string model, int maxOutputTokens,
            out CommanderSemanticRequestPack pack, out string reason)
        {
            pack = null; reason = string.Empty;
            var context = JObject.Parse(request.SerializedContext);
            var knowledge = context["knowledge"] as JObject;
            if ((bool?)knowledge?["requiredReferenceOverflow"] == true)
            {
                reason = "Commander input limit: required canonical context cannot fit safely; the complete request was not submitted.";
                return false;
            }
            string search = request.PlayerMessage + " " + request.SerializedPendingClarification;
            var words = new HashSet<string>(Regex.Matches(search.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
            bool Has(params string[] terms) => terms.Any(words.Contains);
            bool fallbackAll = false;
            bool construction = Has("build", "construct", "house", "houses", "mill", "farm", "farms", "barracks", "mine", "yard");
            bool allocation = Has("gather", "gathering", "food", "wood", "gold", "stone", "berries", "sheep", "farms", "split", "assign", "allocation", "allocate", "put");
            bool future = Has("next", "future", "births");
            bool production = Has("train", "produce", "recruit", "new", "more", "spearmen", "archers", "knights");
            bool age = Has("age", "ages", "feudal", "castle", "imperial", "advance");
            bool action = Has("move", "march", "go", "guard", "protect", "defend", "defense", "patrol", "scout", "explore", "find", "discover", "enemy", "attack", "strike", "retreat", "withdraw", "repair", "fix", "rally", "garrison", "ungarrison")
                || Has("send") && Has("spearmen", "archers", "knights", "army", "troops", "military");
            bool research = Has("research", "technology", "upgrade");
            bool strategy = Has("strategy", "strategic");
            if (!construction && !allocation && !future && !production && !age && !action && !research && !strategy)
            {
                construction = allocation = future = production = age = action = research = strategy = true;
                fallbackAll = true;
            }
            var selected = new HashSet<CommanderSemanticNodeType>();
            if (construction) selected.Add(CommanderSemanticNodeType.BuildStructure);
            if (allocation) selected.Add(CommanderSemanticNodeType.AllocateWorkers);
            if (fallbackAll || allocation && Has("allocation", "allocate", "set")) selected.Add(CommanderSemanticNodeType.SetResourceAllocation);
            if (future) selected.Add(CommanderSemanticNodeType.WatchFutureUnits);
            if (production) selected.Add(CommanderSemanticNodeType.EnsureUnitCount);
            if (age) selected.Add(CommanderSemanticNodeType.ReachAge);
            if (research) selected.Add(CommanderSemanticNodeType.ResearchTechnology);
            if (strategy) selected.Add(CommanderSemanticNodeType.StrategicObjective);
            if (action)
                foreach (CommanderCapabilityActionType capability in Enum.GetValues(typeof(CommanderCapabilityActionType)))
                    if (capability != CommanderCapabilityActionType.ResearchTechnology
                        && (fallbackAll || MatchesAction(capability, words))
                        && Enum.TryParse(capability.ToString(), out CommanderSemanticNodeType type)) selected.Add(type);

            string instruction = request.IsReadOnlyQuestion ? ReadOnlyRules : CommonRules + Vocabulary(selected);
            // Contract retrieval only: quantities on future units must not imply repeated buildings.
            bool repeated = Has("split", "partition") || construction && (Has("third") || Regex.IsMatch(search,
                @"\b(?:build|construct|make)\s+(?:two|three|four|five|six|seven|eight|nine|ten|[2-9]|1[0-9]|20)\b",
                RegexOptions.IgnoreCase));
            if (repeated && !request.IsReadOnlyQuestion) instruction += DynamicVocabulary();
            JObject compact = CompactContext(context, strategy);
            if (knowledge?["requiredCanonicalIds"] is JArray requiredIds
                && !RequiredRowsPresent(requiredIds, compact["knowledge"] as JObject))
            {
                reason = "Commander input limit: required canonical facts are missing from the complete context; nothing was submitted.";
                return false;
            }
            string compactContext = compact.ToString(Formatting.None);
            // Essential turn/pending data is never truncated to squeeze through the budget.
            string user = "Context:" + compactContext + "\nMemory:" + request.SerializedSemanticMemory
                + "\nFacts:" + request.QuestionFacts + "\nBounded pending:" + request.SerializedPendingClarification
                + "\nPlayer:" + request.PlayerMessage;
            var body = new JObject { ["model"] = model,
                ["messages"] = new JArray(new JObject { ["role"] = "system", ["content"] = instruction },
                    new JObject { ["role"] = "user", ["content"] = user }),
                ["max_tokens"] = maxOutputTokens, ["reasoning"] = new JObject { ["effort"] = "none" } };
            var sections = new Dictionary<string, int> {
                ["contract"] = Encoding.UTF8.GetByteCount(instruction),
                ["context"] = Encoding.UTF8.GetByteCount(compactContext),
                ["memory"] = Encoding.UTF8.GetByteCount(request.SerializedSemanticMemory),
                ["questionFacts"] = Encoding.UTF8.GetByteCount(request.QuestionFacts),
                ["pending"] = Encoding.UTF8.GetByteCount(request.SerializedPendingClarification),
                ["utterance"] = Encoding.UTF8.GetByteCount(request.PlayerMessage), ["messageMargin"] = MessageMargin };
            var candidate = new CommanderSemanticRequestPack(body, sections);
            pack = candidate; // Observed packing metrics survive a rejection; this is not dispatch authority.
            if (candidate.EstimatedInputTokens >= InputLimit)
            { reason = "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question."; return false; }
            return true;
        }

        private static bool RequiredRowsPresent(JArray required, JObject compactKnowledge)
        {
            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(compactKnowledge?[table + "Columns"] is JArray columns)
                    || !(compactKnowledge[table] is JArray rows)) continue;
                int id = columns.Select((value, index) => (value: (string)value, index))
                    .Where(value => value.value == "id").Select(value => value.index).DefaultIfEmpty(-1).First();
                if (id < 0) continue;
                foreach (JArray row in rows.OfType<JArray>())
                    if (id < row.Count && row[id].Type == JTokenType.String) present.Add((string)row[id]);
            }
            return required.All(id => id.Type == JTokenType.String && present.Contains((string)id));
        }

        private const string ReadOnlyRules = "Return one JSON object, no markdown: {\"outcome\":\"Answer\",\"message\":\"brief grounded answer\"}, "
            + "{\"outcome\":\"Clarify\",\"message\":\"specific missing fact\"} or {\"outcome\":\"Unsupported\",\"message\":\"reason\"}. "
            + "Message<=180 chars. INFORMATION ONLY: no nodes/plans/approval. Use owned task facts; unknown facts remain unknown. No hidden state, IDs or commands.";
        private static bool MatchesAction(CommanderCapabilityActionType type, HashSet<string> words)
        {
            bool Has(params string[] terms) => terms.Any(words.Contains);
            switch (type)
            {
                case CommanderCapabilityActionType.MoveUnits: return Has("move", "send", "march", "go");
                case CommanderCapabilityActionType.ScoutArea: return Has("scout", "explore", "find", "discover");
                case CommanderCapabilityActionType.PatrolArea: return Has("patrol");
                case CommanderCapabilityActionType.SetRallyPoint: return Has("rally");
                case CommanderCapabilityActionType.AttackTarget: return Has("attack", "strike");
                case CommanderCapabilityActionType.DefendArea: return Has("defend", "defense", "guard", "protect");
                case CommanderCapabilityActionType.RetreatUnits: return Has("retreat", "withdraw");
                case CommanderCapabilityActionType.RepairTarget: return Has("repair", "fix");
                case CommanderCapabilityActionType.ResearchTechnology: return false;
                default: throw new InvalidOperationException("No capability retrieval mapping: " + type);
            }
        }
        private const string CommonRules =
            "JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. "
            + "Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; after/then=completion dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new; a/an=1,number words,left=MapWest. "
            + "Clarify{outcome:Clarify,message<=180,pending:{executionOrder,nodes:partial1..4},missingSlots:[{node,path,type:Confirmation|Count|SourceKind|CanonicalName,alternatives:[canonical]}]};retain known leaves/edges/refs,omit only missing;Confirmation retains offer. Worker pending+missingFields valid;not approval. Answer/Unsupported{outcome,message<=180};no nodes.";

        private static string Vocabulary(HashSet<CommanderSemanticNodeType> selected)
        {
            var text = new StringBuilder();
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
            {
                if (!selected.Contains(type)) continue;
                switch (type)
                {
                    case CommanderSemanticNodeType.BuildStructure:
                        text.Append("BuildStructure{structure:canonical,count:1..20,builders:{state:Eligible(default)|IdleOnly(strict),count:1},placement?}. placement TC/Barracks:{anchor:MyTownCenter|MyBarracks,relation:Near|MapWest|MapEast,clearGapTiles?:1..20,ordinal?:1..8 TC-only}; resource:{anchor:VisibleResource(default)|WorkedResource(explicit),relation:Near,resource,sourceKind?}. Near gap1,no resource ordinal. "); break;
                    case CommanderSemanticNodeType.SetResourceAllocation:
                        text.Append("SetResourceAllocation{resource,count:0..200} sets worker target; distinct from AllocateWorkers. "); break;
                    case CommanderSemanticNodeType.AllocateWorkers:
                        text.Append("AllocateWorkers{mode:SelectedCount(default)|Additional(more workers)|TargetTotal(keep total),countMode:Exact|AllMatching(snapshot),count:1..200 omit if AllMatching,workers:{state:Any|Idle|Gathering,currentResource?},destination:{resource,sourceKind?},resourceAmount?:1..1000000 paired resourceAmountMode:Stockpile|AdditionalGathered(player-wide)}. Amount!=worker count;assignment!=income. "); break;
                    case CommanderSemanticNodeType.EnsureUnitCount:
                        text.Append("EnsureUnitCount:unit canonical,count0..200,quantityMode:TargetTotal default|New. New/more=exact new count, no existing substitutes. Game handles prerequisites; extra buildings only explicit. "); break;
                    case CommanderSemanticNodeType.WatchFutureUnits:
                        text.Append("WatchFutureUnits{unit:canonical,count:1..50,producer:canonical,producerOrdinal?:1..8,action:Gather(Villager only),resource,sourceKind?} OR action:Patrol,location:WorkedResource,resource. Next actual births incl human queues,never train;ambiguous producer clarify. "); break;
                    case CommanderSemanticNodeType.ReachAge:
                        text.Append("ReachAge:targetAge:Feudal|Castle|Imperial|Next. Explicit2/3/4=Feudal/Castle/Imperial, not Next. "); break;
                    case CommanderSemanticNodeType.ResearchTechnology:
                        text.Append("ResearchTechnology:technology:BlacksmithDamage|BlacksmithDefense|Ballistics|SiegeEngineering|Chemistry|MurderHoles. "); break;
                    case CommanderSemanticNodeType.StrategicObjective:
                        text.Append("StrategicObjective only explicit player strategy; objective from context strategyCapabilities, constraints retained. "); break;
                    default:
                        if (!Enum.TryParse(type.ToString(), out CommanderCapabilityActionType _)
                            || type == CommanderSemanticNodeType.ResearchTechnology)
                            throw new InvalidOperationException("Semantic family has no compact contract: " + type);
                        break; // Registered capability actions share one field contract below.
                }
            }
            var actions = selected.Where(t => t != CommanderSemanticNodeType.ResearchTechnology
                && Enum.TryParse(t.ToString(), out CommanderCapabilityActionType _)).OrderBy(t => t).ToArray();
            if (actions.Length > 0)
            {
                text.Append("Action type:").Append(string.Join("|", actions))
                    .Append("; unitSelector,count:1..50,location,resource/structure?,resultFromNode/dependsOn?. ")
                    .Append("unitSelector:Military|Scout|Villagers|Spearman|Archer|Knight|DamagedMilitary; location:PlayerBase|WorkedResource|VisibleResource|VisibleEnemy|RelativeToSelectedUnits. ALL/current-resource workers/composed side+resource+TC location unsupported: Clarify/Unsupported,never drop criteria. VisibleEnemy cannot discover unseen base;point only,no radius. ");
                if (actions.Contains(CommanderSemanticNodeType.AttackTarget) || actions.Contains(CommanderSemanticNodeType.RepairTarget))
                    text.Append("Attack/Repair target?{kind:UnitType,unit}|{kind:BuildingType,structure}. ");
                if (actions.Contains(CommanderSemanticNodeType.SetRallyPoint)) text.Append("Rally uses owned producer. ");
            }
            text.Append("resource:Food|Wood|Gold|Stone; sourceKind:Any or Food=Sheep|Berries|Farm,Wood=Tree,Gold=GoldMine,Stone=StoneMine. "
                + "constraints?<=4 objects type:NoConstruction;PreferredWorkers mode:IdleOnly;ProtectedResource resource,amount?:0..200;MaximumQueue amount1..8;ResourceSource resource,sourceKind. ");
            return text.ToString();
        }

        private static string DynamicVocabulary()
        {
            var text = new StringBuilder("DynamicPlan for repeated placed construction/shared workers: {outcome:DynamicPlan,version:1,nodes:[{id:symbol,mechanic,parameters:{},inputs:{},dependsOn:[]}]}. One shared worker selection, partition disjointly; exact producer set for explicitly new buildings. Registry fields below use name:type; ? optional; input refs also in dependsOn. ");
            foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
            {
                text.Append(primitive.Id).Append(" p[");
                foreach (var field in primitive.Parameters) text.Append(field.Name).Append(':').Append(field.Kind).Append(field.Required ? ";" : "?;");
                text.Append("] in[");
                foreach (var input in primitive.Inputs) text.Append(input.Role).Append(':').Append(input.Accepts).Append(input.Required ? ";" : "?;");
                text.Append("] ->").Append(primitive.Result).Append('.');
            }
            return text.Append(" Max12 nodes,4 refs/node,depth5,total counts200. Canonical IDs from context only; relation Near|MapWest|MapEast, Near gap1; resource mode Visible|Worked. Simple known requests keep Request fast path. ").ToString();
        }

        private static JObject CompactContext(JObject source, bool includeStrategy)
        {
            var result = new JObject();
            foreach (string key in new[] { "age", "civilization", "resources", "population", "populationCap", "workerCounts", "tacticalStatus" })
                if (source[key] != null) result[key] = source[key].DeepClone();
            if (source["knowledge"] is JObject knowledge)
            {
                var facts = new JObject();
                var supportedIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (string field in new[] { "canonicalUnitIds", "canonicalBuildingIds" })
                    if (source[field] is JArray ids)
                        foreach (var id in ids) if (id.Type == JTokenType.String) supportedIds.Add((string)id);
                foreach (var property in knowledge.Properties())
                    if (property.Name != "policy" && property.Name != "requiredCanonicalIds"
                        && property.Name != "requiredReferenceOverflow"
                        && (!(property.Value is JArray array) || array.Count > 0))
                    {
                        if (property.Value is JArray rows && rows.Count > 0 && rows.All(v => v is JObject))
                        {
                            var detachedRows = rows.OfType<JObject>().Select(v => (JObject)v.DeepClone()).ToArray();
                            if (property.Name == "units" || property.Name == "buildings")
                            {
                                // Existing availability/executable facts normally express this already.
                                // Retain an explicit override only when the current adapter differs.
                                bool differs = detachedRows.Any(row => supportedIds.Contains((string)row["id"])
                                    != ((bool?)row["availableForCivilization"] == true && (bool?)row["commanderExecutable"] == true));
                                if (differs) foreach (var row in detachedRows)
                                    row["capabilitySupported"] = supportedIds.Contains((string)row["id"]);
                            }
                            var columns = detachedRows.SelectMany(v => v.Properties().Select(p => p.Name))
                                .Distinct(StringComparer.Ordinal).ToArray();
                            facts[property.Name + "Columns"] = new JArray(columns);
                            facts[property.Name] = new JArray(detachedRows.Select(row => new JArray(columns.Select(column =>
                                column == "cost" && row[column] is JObject cost
                                    ? (JToken)new JArray(new[] { "food", "wood", "gold", "stone" }.Select(resource => cost[resource]?.DeepClone() ?? JValue.CreateNull()))
                                    : row[column]?.DeepClone() ?? JValue.CreateNull()))));
                        }
                        else facts[property.Name] = property.Value.DeepClone();
                    }
                if (facts.Count > 0) result["knowledge"] = facts;
            }
            foreach (string key in new[] { "ownedUnitCounts", "ownedBuildingCounts" })
            {
                var counts = new JObject();
                if (source[key] is JObject all)
                    foreach (var value in all.Properties())
                        if (value.Value.Type == JTokenType.Integer && (value.Value.Value<int>() != 0 || value.Name == "Villager" || value.Name == "Scout")) counts[value.Name] = value.Value.DeepClone();
                result[key] = counts;
            }
            // Public IDs/names/availability/capability flags occur together in detached fact rows.
            // Avoid a duplicate map; knowledge still grants no execution support or authority.
            if (includeStrategy && source["strategyCapabilities"] != null) result["strategyCapabilities"] = source["strategyCapabilities"].DeepClone();
            return result;
        }
    }
}
# Full source: Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace OpenEmpires.Tests
{
    [Category("CommanderPhase5C")]
    public sealed class CommanderPhase5CProviderBudgetTests
    {
        [TestCase("Gather 200 Gold with three villagers from Wood.")]
        [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
        public async Task OrdinaryRequests_KeepCompleteModelPayloadBelowConservativeInputBudget(string utterance)
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            var body = JObject.Parse(transport.Body);
            var messages = (JArray)body["messages"];
            // Count model-visible content/roles, not HTTP JSON escaping that the model never sees.
            int byteUnits = 0;
            foreach (JObject message in messages) byteUnits += Encoding.UTF8.GetByteCount((string)message["role"])
                + Encoding.UTF8.GetByteCount((string)message["content"]);
            TestContext.WriteLine("Offline pack audit: " + new JObject {
                ["utterance"] = utterance, ["systemUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[0]["content"]),
                ["userUtf8Units"] = Encoding.UTF8.GetByteCount((string)messages[1]["content"]),
                ["estimateWithMargin"] = byteUnits + 256, ["estimateMethod"] = "UTF8 byte units plus256 message margin; not verified model tokenizer",
                ["actualPromptTokens"] = JValue.CreateNull() }.ToString(Formatting.None));
            Assert.That(byteUnits + 256, Is.LessThan(4000),
                "The whole outgoing model-facing request needs a conservative bound, not an output max_tokens or characters/4 claim.");
            Assert.That((string)messages[1]["content"], Does.Contain(utterance), "Never truncate the player goal to fit.");
        }

        [Test] public async Task TooLargeRequiredMeaning_IsRejectedWithoutDispatchingOrExecutingPrefix()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            string message = new string('\u754c', 1000); // Valid character length, too large for the full input-byte margin.
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(message,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("limit"));
        }

        [TestCase("Build a House, then send the next five villagers from my first Town Center to gather Wood from trees.")]
        [TestCase("Move all of my Wood villagers to the woodline left of my Town Center.")]
        public async Task RealProductionCatalogProjection_StillFitsCompoundAndComposedRequests(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var context = new CommanderContextBuilder().Build(simulation, manager, utterance);
                var transport = new CaptureTransport();
                var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
                var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance, context), CancellationToken.None);
                TestContext.WriteLine("pack sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates)
                    + "; estimate=" + provider.LastInputEstimate);
                if (transport.Body != null)
                {
                    string packedUser = (string)JObject.Parse(transport.Body)["messages"]?[1]?["content"];
                    string packedContext = packedUser.Substring("Context:".Length,
                        packedUser.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
                    var packedFacts = JObject.Parse(packedContext);
                    TestContext.WriteLine("context-unit-lengths=" + JsonConvert.SerializeObject(packedFacts.Properties()
                        .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                    if (packedFacts["knowledge"] is JObject observedKnowledge)
                        TestContext.WriteLine("knowledge-unit-lengths=" + JsonConvert.SerializeObject(observedKnowledge.Properties()
                            .Select(p => new { key = p.Name, bytes = Encoding.UTF8.GetByteCount(p.Value.ToString(Formatting.None)) })));
                }
                Assert.That(transport.Calls, Is.EqualTo(1), result.SafeExplanation + "; conservative estimate=" + provider.LastInputEstimate
                    + "; sections=" + JsonConvert.SerializeObject(provider.LastInputSectionEstimates));
                Assert.That(provider.LastInputEstimate, Is.LessThan(4000));
                TestContext.WriteLine("Production catalog estimate=" + provider.LastInputEstimate + "; actual tokens unknown; " + utterance);
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task MalformedUsageMetadata_DoesNotInvalidateOtherwiseValidSemanticReply()
        {
            var transport = new CaptureTransport { Usage = new JObject { ["prompt_tokens"] = -1,
                ["prompt_tokens_details"] = "not an object", ["cost"] = "unverified" } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, "Invalid observation metadata cannot poison valid semantic content.");
            Assert.That(Read(provider.LastUsage, "PromptTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "CachedTokens"), Is.Null);
            Assert.That(Read(provider.LastUsage, "Cost"), Is.Null);
        }

        [Test] public async Task ResponseUsage_IsRetainedAsTypedObservedMetricsIncludingCachedInput()
        {
            var transport = new CaptureTransport {
                Usage = new JObject { ["prompt_tokens"] = 900, ["completion_tokens"] = 60,
                    ["total_tokens"] = 960, ["cost"] = .002m,
                    ["prompt_tokens_details"] = new JObject { ["cached_tokens"] = 250 },
                    ["completion_tokens_details"] = new JObject { ["reasoning_tokens"] = 20 } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var usageProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(usageProperty, Is.Not.Null, "Production currently drops the usage envelope.");
            var usage = usageProperty.GetValue(provider);
            Assert.That(Read(usage, "PromptTokens"), Is.EqualTo(900));
            Assert.That(Read(usage, "CompletionTokens"), Is.EqualTo(60));
            Assert.That(Read(usage, "CachedTokens"), Is.EqualTo(250));
            Assert.That(Read(usage, "ReasoningTokens"), Is.EqualTo(20));
            Assert.That(Read(usage, "Cost"), Is.EqualTo(.002m));
        }

        [Test] public async Task MissingResponseUsage_RemainsUnknownNotZero()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var property = typeof(OpenRouterCommanderProvider).GetProperty("LastUsage");
            Assert.That(property, Is.Not.Null);
            Assert.That(Read(property.GetValue(provider), "PromptTokens"), Is.Null);
        }

        [TestCase("Set the Wood allocation to zero.", "SetResourceAllocation")]
        [TestCase("Send spearmen to guard my Gold mine.", "DefendArea")]
        [TestCase("Split all idle villagers between Food and Wood.", "partition-workers")]
        [TestCase("Recruit archers and send them to guard my Gold mine.", "EnsureUnitCount")]
        public async Task MixedAndAliasRequests_KeepRegisteredFormsVisible(string utterance, string required)
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(utterance,
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            string contract = (string)JObject.Parse(transport.Body)["messages"]?[0]?["content"];
            Assert.That(contract, Does.Contain(required));
        }

        [Test] public async Task GenericRequest_AdvertisesEveryStrictlyParsedSemanticFamily()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var request = new CommanderSemanticProviderRequest("Help me with my army and economy.",
                CommanderEconomyClarificationTests.Context());
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string contract = (string)JObject.Parse(pack.Json)["messages"]?[0]?["content"];
            foreach (CommanderSemanticNodeType type in Enum.GetValues(typeof(CommanderSemanticNodeType)))
                Assert.That(contract, Does.Contain(type.ToString()), type.ToString());
            foreach (string required in new[] { "count:1..20", "count:0..200", "count:1..200",
                "count:1..50", "targetAge:Feudal|Castle|Imperial|Next", "resourceAmount?:1..1000000",
                "MaximumQueue amount1..8", "constraints?<=4", "nodes:1..4", "message<=180",
                "sourceKind:Any", "currentResource?", "executionOrder:Independent|Sequential|DependencyGraph" })
                Assert.That(contract, Does.Contain(required), required);
            await provider.TranslateSemanticAsync(request, CancellationToken.None);
            Assert.That(transport.Calls == 1 || provider.LastInputEstimate >= 4000, Is.True,
                "An overfull all-family fallback must reject before HTTP, never send an oversized prompt.");
        }

        [Test] public async Task EmptyUsageObject_IsUnknownAndDoesNotInventPromptTokens()
        {
            var transport = new CaptureTransport { Usage = new JObject() };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastUsage.HasReportedUsage, Is.False);
            Assert.That(provider.LastUsage.PromptTokens, Is.Null);
        }

        [Test] public async Task Repair_RetainsBothAttemptEstimatesAndUsage()
        {
            const string invalid = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":\"3\"}]}";
            const string corrected = "{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":3}]}";
            var transport = new CaptureTransport { Replies = new[] { invalid, corrected },
                Usages = new[] { new JObject { ["prompt_tokens"] = 850, ["cost"] = .001m },
                    new JObject { ["prompt_tokens"] = 180, ["cost"] = .0002m } } };
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Train 3 spearmen.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.True, result.SafeExplanation);
            Assert.That(transport.Calls, Is.EqualTo(2));
            var attemptsProperty = typeof(OpenRouterCommanderProvider).GetProperty("LastSemanticAttempts");
            Assert.That(attemptsProperty, Is.Not.Null);
            var attempts = (System.Collections.IList)attemptsProperty.GetValue(provider);
            Assert.That(attempts, Has.Count.EqualTo(2));
            Assert.That(Read(attempts[0], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(attempts[1], "InputEstimate"), Is.GreaterThan(0));
            Assert.That(Read(Read(attempts[0], "Usage"), "PromptTokens"), Is.EqualTo(850));
            Assert.That(Read(Read(attempts[1], "Usage"), "PromptTokens"), Is.EqualTo(180));
            Assert.That(Read(Read(attempts[0], "Usage"), "Cost"), Is.EqualTo(.001m));
            Assert.That(Read(Read(attempts[1], "Usage"), "Cost"), Is.EqualTo(.0002m));
            Assert.That(Read(attempts[0], "ResultStage"), Does.Contain("schema"));
            Assert.That(Read(attempts[1], "ResultStage"), Does.Contain("valid"));
        }

        [Test] public void ReferencedAndAmbiguousCanonicalNames_SurviveKnowledgeRetrieval()
        {
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var relevant = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a House near my Town Center then train Spearman at the Barracks.", Civilization.English));
            var ids = new HashSet<string>();
            foreach (JObject row in (JArray)relevant["units"]) ids.Add((string)row["id"]);
            foreach (JObject row in (JArray)relevant["buildings"]) ids.Add((string)row["id"]);
            foreach (string id in new[] { "building:House", "building:TownCenter", "building:Barracks",
                catalog.FindUnit("Spearman").StableId })
                Assert.That(ids, Does.Contain(id), id);
            var ambiguous = JObject.Parse(CommanderKnowledgeRetrieval.Retrieve(catalog,
                "Build a dropoff.", Civilization.English));
            Assert.That(((JArray)ambiguous["nameInterpretations"]).ToString(Formatting.None), Does.Contain("dropoff"));
        }

        [Test] public void ReferencedFacts_PreserveColumnsNullsCapabilityAndBoundedPending()
        {
            var knowledge = new JObject { ["units"] = new JArray(
                new JObject { ["id"] = "unit:Spearman", ["name"] = "Spearman", ["age"] = 2,
                    ["producer"] = "Barracks", ["cost"] = new JObject { ["food"] = 50, ["wood"] = 20, ["gold"] = 0, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true },
                new JObject { ["id"] = "unit:Horseman", ["name"] = "Horseman", ["age"] = 2,
                    ["producer"] = JValue.CreateNull(), ["cost"] = new JObject { ["food"] = 0, ["wood"] = 0, ["gold"] = 30, ["stone"] = 0 },
                    ["availableForCivilization"] = true, ["commanderExecutable"] = true }) };
            var context = new CommanderContext(0, 0, new CommanderResourceSnapshot(0, 0, 0, 0),
                0, 0, 200, 1, "English", new List<CommanderBuildingSnapshot>(), new List<CommanderUnitSnapshot>(),
                new List<CommanderBuildingSnapshot>(), new List<string>(), new List<CommanderGoalSnapshot>(),
                new List<CommanderVisibleResourceSnapshot>(), new List<CommanderUnitOptionSnapshot>(),
                new List<CommanderWorkerAllocationSnapshot>(), new List<CommanderVisibleEnemyMilitarySnapshot>(),
                knowledge.ToString(Formatting.None), canonicalUnitIds: new[] { "unit:Spearman" });
            var pending = new CommanderPendingClarification(new CommanderWorkerAllocationDraft(
                CommanderWorkerAllocationMode.SelectedCount, CommanderWorkerCountMode.Exact, null,
                new CommanderWorkerSelector(CommanderWorkerState.Gathering, ResourceType.Wood),
                new CommanderResourceDestination(ResourceType.Food, ResourceSourceKind.Berries),
                200, CommanderResourceAmountMode.AdditionalGathered),
                "Gather 200 additional Food from Berries with Wood villagers", "How many villagers?", 1, 12);
            var request = new CommanderSemanticProviderRequest("three", context, pendingClarification: pending);
            CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096, out var pack, out _);
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            Assert.That(user, Does.Contain(pending.Serialize()));
            string compact = user.Substring("Context:".Length, user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var facts = (JObject)JObject.Parse(compact)["knowledge"];
            var columns = ((JArray)facts["unitsColumns"]).Select(x => (string)x).ToArray();
            var rows = (JArray)facts["units"];
            int id = Array.IndexOf(columns, "id"), producer = Array.IndexOf(columns, "producer");
            int cost = Array.IndexOf(columns, "cost"), capability = Array.IndexOf(columns, "capabilitySupported");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That((string)rows[0][id], Is.EqualTo("unit:Spearman"));
            Assert.That((string)rows[1][id], Is.EqualTo("unit:Horseman"));
            Assert.That(rows[1][producer].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((int)rows[0][cost][0], Is.EqualTo(50));
            Assert.That((bool)rows[1][capability], Is.False, "Knowledge must not grant unavailable execution capability.");
        }

        [Test] public async Task PreflightRejectionAfterASentRequest_ResetsAttemptCount()
        {
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
            Assert.That(provider.HasAttemptedRequest, Is.False);
        }

        [Test] public async Task CompletedAttemptSnapshot_RemainsImmutableAcrossLaterRequests()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new CaptureTransport());
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            var completed = provider.LastSemanticAttempts;
            Assert.That(completed, Has.Count.EqualTo(1));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest(new string('\u754c', 1000),
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(completed, Has.Count.EqualTo(1), "A later request may not erase an earlier HTTP attempt record.");
            Assert.That(provider.LastSemanticAttempts, Is.Empty);
        }

        [Test] public void NineExplicitCanonicalNames_SurviveRealContextAndPackedColumns()
        {
            const string utterance = "Train Villager, Spearman, Archer, Horseman, Scout, Knight, Crossbowman, Monk, and Longbowman.";
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            var ids = PackedKnowledgeIds(pack);
            foreach (string id in new[] { "unit:0", "unit:1", "unit:2", "unit:3", "unit:4",
                "unit:7", "unit:8", "unit:9", "unit:10" })
                Assert.That(ids, Does.Contain(id), id);
        }

        [Test] public void LateCanonicalName_AfterOldWordAndPhraseWindowsSurvivesRealPack()
        {
            string utterance = "Build " + string.Join(" ", Enumerable.Repeat("please", 100)) + " Market.";
            Assert.That(utterance.Length, Is.LessThanOrEqualTo(1024));
            var request = ProductionRequest(utterance);
            Assert.That(CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                out var pack, out string reason), Is.True, reason);
            Assert.That(PackedKnowledgeIds(pack), Does.Contain("building:Market"));
        }

        [Test] public void PendingGraphAndAmbiguousAlternatives_AreRetainedOrWholeRequestRefused()
        {
            var proposal = new JObject { ["outcome"] = "Clarify", ["message"] = "Which count?",
                ["pending"] = new JObject { ["executionOrder"] = "Independent", ["nodes"] = new JArray(
                    new JObject { ["type"] = "BuildStructure", ["structure"] = "Market",
                        ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }) },
                ["missingSlots"] = new JArray(new JObject { ["node"] = 0, ["path"] = "count",
                    ["type"] = "Count", ["alternatives"] = new JArray(1) }) };
            var parsed = CommanderSemanticJson.Parse(proposal.ToString(Formatting.None));
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            var pending = new CommanderPendingClarification(parsed.PendingGraphDraft,
                "Build a dropoff", "Which count?", 1, 9);
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                var builder = new CommanderContextBuilder();
                var overload = typeof(CommanderContextBuilder).GetMethod("Build", new[] {
                    typeof(GameSimulation), typeof(CommanderGoalManager), typeof(string), typeof(string) });
                Assert.That(overload, Is.Not.Null, "Current reply and bounded pending graph must both reach retrieval.");
                var context = (CommanderContext)overload.Invoke(builder, new object[] {
                    simulation, manager, "one", pending.Serialize() });
                var request = new CommanderSemanticProviderRequest("one", context, pendingClarification: pending);
                bool fit = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out string reason);
                if (!fit) Assert.That(reason, Does.Contain("required"));
                else
                {
                    var ids = PackedKnowledgeIds(pack);
                    Assert.That(ids, Does.Contain("building:Market"));
                    Assert.That((string)JObject.Parse(pack.Json)["messages"]?[1]?["content"], Does.Contain("dropoff"));
                }
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test] public async Task TooManyRequiredNames_RefuseWholeRequestBeforeHttp()
        {
            const string utterance = "Train Villager Spearman Archer Horseman Scout Knight Crossbowman Monk Longbowman "
                + "and build House Barracks TownCenter Mill LumberYard Mine ArcheryRange Stables Farm Market.";
            var transport = new CaptureTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(ProductionRequest(utterance), CancellationToken.None);
            Assert.That(transport.Calls, Is.Zero);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(result.SafeExplanation, Does.Contain("required").IgnoreCase);
        }

        [TestCase("{\"choices\":[]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{},{}]}", "response-invalid-envelope")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"x\"}}]}", "response-truncated")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"x\"}}]}", "response-incomplete")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"\"}}]}", "response-empty")]
        [TestCase("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":[{\"type\":\"image\"}]}}]}", "response-unsupported-parts")]
        [TestCase("{\"error\":{\"code\":500}}", "provider-error")]
        public async Task RejectedSuccessfulHttp_RecordsActualResultStage(string envelope, string stage)
        {
            var transport = new EnvelopeTransport(new CommanderHttpResponse(200, envelope));
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            var result = await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Nodes, Is.Empty);
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo(stage));
            Assert.That(result.SafeExplanation, Does.Not.Contain(envelope));
        }

        [Test] public async Task NullTransportResponse_RecordsTransportNullNotSuccess()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", new EnvelopeTransport(null));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("transport-null"));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.Null);
        }

        [Test] public async Task OversizedSuccessfulHttpBody_RecordsResponseOversize()
        {
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential",
                new EnvelopeTransport(new CommanderHttpResponse(200, new string('x', 65537))));
            await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                CommanderEconomyClarificationTests.Context()), CancellationToken.None);
            Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
            Assert.That(provider.LastSemanticAttempts[0].HttpStatusCode, Is.EqualTo(200));
            Assert.That(provider.LastSemanticAttempts[0].ResultStage, Is.EqualTo("response-oversize"));
        }

        [Test] public async Task OlderCancelledLateAttempt_DoesNotOverwriteNewerRequestMetrics()
        {
            var transport = new OverlapTransport();
            var provider = new OpenRouterCommanderProvider("fixture-not-a-credential", transport);
            using (var cancelled = new CancellationTokenSource())
            {
                var old = provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a House.",
                    CommanderEconomyClarificationTests.Context()), cancelled.Token);
                cancelled.Cancel();
                await provider.TranslateSemanticAsync(new CommanderSemanticProviderRequest("Build a Mill.",
                    CommanderEconomyClarificationTests.Context()), CancellationToken.None);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                transport.ReleaseOld();
                Assert.ThrowsAsync<TaskCanceledException>(async () => await old);
                Assert.That(provider.LastSemanticAttempts, Has.Count.EqualTo(1));
                Assert.That(provider.LastSemanticAttempts[0].Usage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastUsage.PromptTokens, Is.EqualTo(222));
                Assert.That(provider.LastHttpStatusCode, Is.EqualTo(200));
                Assert.That(Read(provider, "SemanticHttpAttemptCount"), Is.EqualTo(2L));
                var history = (System.Collections.IEnumerable)Read(provider, "RecentSemanticAttempts");
                var observed = history.Cast<CommanderProviderAttempt>().ToArray();
                Assert.That(observed, Has.Length.EqualTo(2));
                Assert.That(observed.Single(x => x.ResultStage == "caller-canceled").Usage.PromptTokens, Is.Null);
            }
        }

        private static CommanderSemanticProviderRequest ProductionRequest(string utterance)
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<SimulationConfig>();
            CommanderGoalManager manager = null;
            try
            {
                var simulation = new GameSimulation(config, 1, new[] { 0 }, Array.Empty<int>());
                simulation.SetPlayerCivilizations(new[] { Civilization.English });
                manager = new CommanderGoalManager(simulation, 0);
                return new CommanderSemanticProviderRequest(utterance,
                    new CommanderContextBuilder().Build(simulation, manager, utterance));
            }
            finally { manager?.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static HashSet<string> PackedKnowledgeIds(CommanderSemanticRequestPack pack)
        {
            string user = (string)JObject.Parse(pack.Json)["messages"]?[1]?["content"];
            string compact = user.Substring("Context:".Length,
                user.IndexOf("\nMemory:", StringComparison.Ordinal) - "Context:".Length);
            var knowledge = (JObject)JObject.Parse(compact)["knowledge"];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string table in new[] { "units", "buildings" })
            {
                if (!(knowledge[table] is JArray rows)) continue;
                var columns = ((JArray)knowledge[table + "Columns"]).Select(x => (string)x).ToArray();
                int idColumn = Array.IndexOf(columns, "id");
                foreach (JArray row in rows) ids.Add((string)row[idColumn]);
            }
            return ids;
        }

        private static object Read(object value, string name)
        { Assert.That(value, Is.Not.Null); var member = value.GetType().GetProperty(name); Assert.That(member, Is.Not.Null, name); return member.GetValue(value); }

        private sealed class CaptureTransport : ICommanderHttpTransport
        {
            internal string Body;
            internal int Calls;
            internal JObject Usage;
            internal string[] Replies;
            internal JObject[] Usages;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                Calls++; Body = body;
                var envelope = new JObject { ["choices"] = new JArray(new JObject {
                    ["finish_reason"] = "stop", ["message"] = new JObject {
                        ["content"] = Replies != null ? Replies[Calls - 1] : "{\"outcome\":\"Answer\",\"message\":\"Offline fixture.\"}" } }) };
                if (Usages != null) envelope["usage"] = Usages[Calls - 1];
                else if (Usage != null) envelope["usage"] = Usage;
                return Task.FromResult(new CommanderHttpResponse(200, envelope.ToString(Formatting.None)));
            }
        }

        private sealed class EnvelopeTransport : ICommanderHttpTransport
        {
            private readonly CommanderHttpResponse response;
            internal int Calls;
            internal EnvelopeTransport(CommanderHttpResponse response) => this.response = response;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            { Calls++; return Task.FromResult(response); }
        }

        private sealed class OverlapTransport : ICommanderHttpTransport
        {
            private readonly TaskCompletionSource<CommanderHttpResponse> old =
                new TaskCompletionSource<CommanderHttpResponse>();
            private int calls;
            public Task<CommanderHttpResponse> PostJsonAsync(Uri uri, string body,
                IReadOnlyDictionary<string, string> headers, CancellationToken token)
            {
                if (Interlocked.Increment(ref calls) == 1) return old.Task;
                return Task.FromResult(Reply(222));
            }
            internal void ReleaseOld() => old.TrySetResult(Reply(111));
            private static CommanderHttpResponse Reply(int prompt) => new CommanderHttpResponse(200,
                new JObject { ["usage"] = new JObject { ["prompt_tokens"] = prompt },
                    ["choices"] = new JArray(new JObject { ["finish_reason"] = "stop",
                        ["message"] = new JObject { ["content"] = "{\"outcome\":\"Answer\",\"message\":\"fixture\"}" } }) }
                .ToString(Formatting.None));
        }
    }
}
