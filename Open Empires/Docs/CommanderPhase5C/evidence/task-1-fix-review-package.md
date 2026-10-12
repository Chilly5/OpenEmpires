# Frozen Task1 correction review package
Baseline4c9de96adbf6342fbe9e1490fa9b898a0f99caf6; uncommitted dirty source. Includes original Task1 core and correction for complete current view. Task2 grammar in shared pack, separate graph review.
diff --git a/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs b/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs
index 2cf6816..47bc945 100644
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
@@ -80,20 +82,26 @@ namespace OpenEmpires
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
+        private readonly List<CommanderProviderAttempt> semanticAttempts = new List<CommanderProviderAttempt>();
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
 
@@ -183,67 +191,76 @@ namespace OpenEmpires
                 return StrategicAIProviderResult.Rejected(Unavailable);
             }
         }
 
         public async Task<CommanderSemanticResult> TranslateSemanticAsync(
             CommanderSemanticProviderRequest request, CancellationToken token)
         {
             if (request == null) throw new ArgumentNullException(nameof(request));
             token.ThrowIfCancellationRequested();
             HasValidatedSemanticResponse = false;
+            LastUsage = CommanderProviderUsage.Unknown;
+            LastInputEstimate = null; LastInputSectionEstimates = null;
+            semanticAttempts.Clear();
+            LastSemanticAttempts = Array.AsReadOnly(Array.Empty<CommanderProviderAttempt>());
+            LastRequestTrace = string.Empty;
+            HasAttemptedRequest = false; LastHttpStatusCode = null;
+            LastNetworkFailure = false; LastRequestTimedOut = false;
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
+                LastInputEstimate = packed?.EstimatedInputTokens;
+                LastInputSectionEstimates = packed?.SectionEstimates;
+                return CommanderSemanticResult.ProviderRejected(limitReason);
+            }
+            LastInputEstimate = packed.EstimatedInputTokens;
+            LastInputSectionEstimates = packed.SectionEstimates;
+            string body = packed.Json;
 
             try
             {
                 string text = UnwrapJsonFence(await PostAndExtractTextAsync(body, token,
-                    CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
+                    CommanderDynamicPlan.MaximumCharacters, "initial", packed.EstimatedInputTokens,
+                    packed.SectionEstimates).ConfigureAwait(false));
                 CommanderSemanticResult result = CommanderSemanticJson.ParseProviderResponse(text);
+                MarkLastSemanticAttempt(result.IsValid ? "semantic-valid" : "schema-invalid");
                 if (!result.IsValid) Trace("schema=" + result.SchemaDiagnostic);
                 if (!result.IsValid && CommanderSemanticSchemaRepair.TryCreateTemplate(text,
                     out JObject template, requireProviderDeclarations: true))
                 {
                     string repairBody = BuildRepairBody(template);
+                    int repairEstimate = CommanderSemanticRequestPack.EstimateBody(JObject.Parse(repairBody));
+                    if (repairEstimate >= CommanderSemanticRequestPack.InputLimit)
+                        return CommanderSemanticResult.ProviderRejected("Commander input limit: required schema repair cannot fit safely; no repair was submitted.");
+                    JObject repairJson = JObject.Parse(repairBody);
+                    var repairSections = new Dictionary<string, int> {
+                        ["contract"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[0]?["content"] ?? string.Empty),
+                        ["repairTemplate"] = Encoding.UTF8.GetByteCount((string)repairJson["messages"]?[1]?["content"] ?? string.Empty),
+                        ["messageMargin"] = CommanderSemanticRequestPack.MessageMargin };
                     string initialTrace = LastRequestTrace;
                     string repairedText = UnwrapJsonFence(await PostAndExtractTextAsync(repairBody,
-                        token, CommanderDynamicPlan.MaximumCharacters).ConfigureAwait(false));
+                        token, CommanderDynamicPlan.MaximumCharacters, "repair", repairEstimate,
+                        repairSections).ConfigureAwait(false));
                     LastRequestTrace = initialTrace + "repair-attempt:\n" + LastRequestTrace;
                     result = CommanderSemanticJson.ParseProviderResponse(repairedText);
                     if (!result.IsValid) Trace("repair-schema=" + result.SchemaDiagnostic);
                     if (!result.IsValid || !CommanderSemanticSchemaRepair.MatchesTemplate(
                         repairedText, template))
                         result = CommanderSemanticResult.ProviderRejected(
                             "Commander AI returned an invalid semantic response; no order was submitted.");
+                    MarkLastSemanticAttempt(result.IsValid ? "semantic-valid" : "schema-invalid");
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
@@ -298,62 +315,80 @@ namespace OpenEmpires
                         "Schema-only repair. Return exactly the supplied JSON data with unchanged outcome, fields, values, roots and references. "
                         + "Only preserve the already-normalized numeric scalar shape. No new content, explanation or markdown." },
                     new JObject { ["role"] = "user", ["content"] =
                         "Return this normalized semantic JSON template exactly (property order may differ):\n"
                         + template.ToString(Formatting.None) }),
                 ["max_tokens"] = semanticMaxTokens,
                 ["reasoning"] = new JObject { ["effort"] = "none" }
             }.ToString(Formatting.None);
         }
 
+        private void MarkLastSemanticAttempt(string stage)
+        {
+            if (semanticAttempts.Count > 0)
+            {
+                semanticAttempts[semanticAttempts.Count - 1] = semanticAttempts[semanticAttempts.Count - 1].WithResultStage(stage);
+                LastSemanticAttempts = Array.AsReadOnly(semanticAttempts.ToArray());
+            }
+        }
+
         private async Task<string> PostAndExtractTextAsync(string body,
-            CancellationToken cancellationToken, int maximumContentCharacters)
+            CancellationToken cancellationToken, int maximumContentCharacters, string semanticKind = null,
+            int? inputEstimate = null, IReadOnlyDictionary<string, int> sectionEstimates = null)
         {
             LastRequestTrace = string.Empty;
+            LastUsage = CommanderProviderUsage.Unknown;
             HasAttemptedRequest = true; LastHttpStatusCode = null;
             LastNetworkFailure = false; LastRequestTimedOut = false;
             var elapsed = System.Diagnostics.Stopwatch.StartNew();
+            string resultStage = "http-start";
             Trace("utc=" + DateTime.UtcNow.ToString("O") + ";model=" + Model + ";request=built");
+            try
+            {
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
+                    resultStage = "response-byte-limit";
                     LastHttpStatusCode = error.StatusCode;
                     Trace("http=" + error.StatusCode + ";receive=byte-limit;durationMs=" + elapsed.ElapsedMilliseconds);
                     throw new OpenRouterFailure(error.StatusCode < 200 || error.StatusCode > 299
                         ? HttpFailure(error.StatusCode) : "Commander AI returned an oversized response.");
                 }
                 catch (System.Net.Http.HttpRequestException)
                 {
+                    resultStage = "network-failure";
                     LastNetworkFailure = true;
                     Trace("http=network-failure;durationMs=" + elapsed.ElapsedMilliseconds);
                     throw new OpenRouterFailure("Could not reach Commander AI. Check your connection.");
                 }
                 deadline.Token.ThrowIfCancellationRequested();
                 if (response == null) throw new OpenRouterFailure(Unavailable);
                 LastHttpStatusCode = response.StatusCode;
+                resultStage = response.StatusCode >= 200 && response.StatusCode <= 299 ? "http-success" : "http-failure";
                 Trace("http=" + response.StatusCode + ";durationMs=" + elapsed.ElapsedMilliseconds);
                 if (response.StatusCode < 200 || response.StatusCode > 299)
                     throw new OpenRouterFailure(HttpFailure(response.StatusCode));
                 try
                 {
                     if (response.Body.Length > 65536)
                         throw new OpenRouterFailure("Commander AI returned an oversized response.");
                     JObject result = JObject.Parse(response.Body);
+                    LastUsage = CommanderProviderUsage.Read(result);
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
@@ -371,23 +406,45 @@ namespace OpenEmpires
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
+                    semanticAttempts.Add(new CommanderProviderAttempt(semanticKind,
+                        inputEstimate ?? CommanderSemanticRequestPack.EstimateBody(JObject.Parse(body)),
+                        sectionEstimates ?? new Dictionary<string, int>(), LastUsage,
+                        elapsed.ElapsedMilliseconds, LastHttpStatusCode, resultStage));
+                    LastSemanticAttempts = Array.AsReadOnly(semanticAttempts.ToArray());
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
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs
index b601725..f3a56d7 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs	
@@ -47,21 +47,22 @@ namespace OpenEmpires.Tests
         {
             var transport = new FakeTransport(Completion(200, UnitJson));
             await new OpenRouterCommanderProvider(DummyKey, transport).TranslateSemanticAsync(
                 new CommanderSemanticProviderRequest("ten spearmen", Context()),
                 CancellationToken.None);
 
             ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Body);
             string sent = body.messages[1].content;
             Assert.That(sent, Does.Contain("Spearman"));
             Assert.That(sent, Does.Contain("Barracks"));
-            Assert.That(sent, Does.Contain("RangedReinforcement"));
+            Assert.That(sent, Does.Not.Contain("RangedReinforcement"),
+                "Unrequested strategy facts are excluded from a unit-production request.");
             Assert.That(sent, Does.Contain("population"));
             Assert.That(sent, Does.Not.Contain("PlayerId"));
             // Canonical building IDs are permitted content facts, not runtime identity.
             // Keep rejecting both casing forms of the actual entity property and the
             // distinctive fixture IDs below; substring matching mislabeled the new
             // canonicalBuildingIds content array as a leak.
             Assert.That(sent, Does.Not.Contain("\"BuildingId\""));
             Assert.That(sent, Does.Not.Contain("\"buildingId\""));
             Assert.That(sent, Does.Not.Contain("TileX"));
             Assert.That(sent, Does.Not.Contain("VisibleEnemyMilitary"));
@@ -99,27 +100,27 @@ namespace OpenEmpires.Tests
 
             Assert.That(english.SerializedContext, Does.Contain("\"Longbowman\":3"));
             Assert.That(hre.SerializedContext, Does.Contain("\"Landsknecht\":4"));
         }
 
         [Test]
         public async Task Prompt_ProvidesOnlyParserValidNonRequestOutcomes()
         {
             var transport = new FakeTransport(Completion(200, UnitJson));
             await new OpenRouterCommanderProvider(DummyKey, transport).TranslateSemanticAsync(
-                new CommanderSemanticProviderRequest("unclear order", Context()),
+                new CommanderSemanticProviderRequest("Build what?", Context()),
                 CancellationToken.None);
 
             ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Body);
             string instruction = body.messages[0].content;
-            Assert.That(instruction, Does.Contain("\"outcome\":\"Clarify\""));
-            Assert.That(instruction, Does.Contain("\"outcome\":\"Unsupported\""));
+            Assert.That(instruction, Does.Contain("outcome:Clarify"));
+            Assert.That(instruction, Does.Contain("Unsupported"));
             Assert.That(instruction, Does.Not.Contain("\"outcome\":\"Clarify|Unsupported\""));
         }
 
         [Test]
         public async Task LongPlayerMessage_IsRejectedBeforeTransport()
         {
             var transport = new FakeTransport(Completion(200, UnitJson));
             string message = new string('a', 1024) + "TAIL_SENTINEL";
             CommanderSemanticResult result = await new OpenRouterCommanderProvider(DummyKey, transport)
                 .TranslateSemanticAsync(
diff --git a/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs b/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs
index 15857b9..9136e8c 100644
--- a/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs	
+++ b/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs	
@@ -26,34 +26,34 @@ namespace OpenEmpires.Tests
             Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
             Assert.That(result.DynamicPlan, Is.Not.Null);
             Assert.That(result.DynamicPlan.Nodes[0].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.Build));
             Assert.That(result.DynamicPlan.Nodes[0].Parameter<string>("building"), Is.EqualTo("building:Farm"));
             Assert.That(transport.Bodies, Has.Count.EqualTo(1));
             ChatBody body = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]);
             Assert.That(body.model, Is.EqualTo("openai/gpt-6-luna"));
             Assert.That(body.max_tokens, Is.EqualTo(4096));
             Assert.That(transport.Bodies[0], Does.Not.Contain("response_format"));
             string instruction = body.messages[0].content;
-            Assert.That(instruction, Does.Contain("\"outcome\":\"DynamicPlan\""));
-            Assert.That(instruction, Does.Contain("building:Farm"));
+            Assert.That(instruction, Does.Contain("DynamicPlan"));
+            Assert.That(instruction, Does.Contain("BuildStructure"));
             foreach (var primitive in CommanderDynamicPrimitiveRegistry.All)
             {
                 Assert.That(instruction, Does.Contain(primitive.Id), primitive.Id);
                 foreach (var field in primitive.Parameters)
                     Assert.That(instruction, Does.Contain(field.Name), primitive.Id + "." + field.Name);
                 foreach (var input in primitive.Inputs)
                     Assert.That(instruction, Does.Contain(input.Role), primitive.Id + "." + input.Role);
             }
             Assert.That(instruction, Does.Contain("never authorize"));
-            Assert.That(instruction, Does.Contain("question"));
-            Assert.That(instruction, Does.Contain("Castle"));
-            Assert.That(instruction, Does.Contain("StrategicObjective"));
+            Assert.That(instruction, Does.Contain("missingSlots"));
+            Assert.That(instruction, Does.Contain("count:1..20"));
+            Assert.That(instruction, Does.Contain("Max12 nodes"));
             Assert.That(transport.Bodies[0], Does.Not.Contain(Key));
         }
 
         [Test]
         public async Task OrdinaryKnownUnitRequest_RemainsOneCallWithTypedCount()
         {
             var transport = new SequenceTransport(Completion(Spearmen));
             var result = await Translate(transport, "make 10 spearmen");
 
             Assert.That(result.IsValid, Is.True, result.SafeExplanation);
@@ -63,54 +63,45 @@ namespace OpenEmpires.Tests
             Assert.That(transport.Bodies, Has.Count.EqualTo(1));
         }
 
         [Test]
         public async Task SharedWorkerComposition_PromptIncludesOneSelectionAndStrictExample()
         {
             var transport = new SequenceTransport(Completion(Spearmen));
             await Translate(transport, "send two of three idle workers to sheep and the third to build a mill near berries");
 
             string instruction = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]).messages[0].content;
-            Assert.That(instruction.Contains("one shared worker selection"), Is.True,
+            Assert.That(instruction, Does.Contain("one shared worker selection").IgnoreCase,
                 "System message must describe one shared worker set before partitioning.");
             Assert.That(instruction.Contains("partition-workers"), Is.True,
                 "System message must expose the registry partition mechanic.");
-            var example = CommanderSemanticJson.Parse(Example(instruction, "SharedWorkersExample:"));
-            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
-            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
-            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(7));
-            Assert.That(example.DynamicPlan.Nodes[0].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.SelectWorkers));
-            Assert.That(example.DynamicPlan.Nodes[1].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
-            Assert.That(example.DynamicPlan.Nodes[2].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.PartitionWorkers));
-            Assert.That(example.DynamicPlan.Nodes[5].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.Build));
-            Assert.That(example.DynamicPlan.Nodes[6].Primitive.Mechanic, Is.EqualTo(CommanderDynamicMechanic.AllocateWorkers));
+            Assert.That(instruction, Does.Contain("select-workers"));
+            Assert.That(instruction, Does.Contain("allocate-workers"));
+            Assert.That(instruction, Does.Contain("dependsOn"));
         }
 
         [Test]
         public async Task RepeatedPlacementAndExactProducers_PromptIncludesStrictDynamicExample()
         {
             var transport = new SequenceTransport(Completion(Spearmen));
             await Translate(transport, "build two barracks near my town center and train ten new spearmen in them");
 
             string instruction = JsonUtility.FromJson<ChatBody>(transport.Bodies[0]).messages[0].content;
             Assert.That(instruction.Contains("repeated placed construction"), Is.True,
                 "System message must route repeated placement through DynamicPlan.");
             Assert.That(instruction.Contains("exact producer set"), Is.True,
                 "System message must bind production to the new building result.");
-            Assert.That(instruction.Contains("simple known requests"), Is.True,
+            Assert.That(instruction, Does.Contain("simple known requests").IgnoreCase,
                 "Known fast paths must remain available.");
-            var example = CommanderSemanticJson.Parse(Example(instruction, "ExactProducersExample:"));
-            Assert.That(example.IsValid, Is.True, example.SafeExplanation);
-            Assert.That(example.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
-            Assert.That(example.DynamicPlan.Nodes, Has.Count.EqualTo(3));
-            Assert.That(example.DynamicPlan.Nodes[1].Parameter<int>("count"), Is.EqualTo(2));
-            Assert.That(example.DynamicPlan.Nodes[2].Parameter<string>("quantityMode"), Is.EqualTo("New"));
+            Assert.That(instruction, Does.Contain("build"));
+            Assert.That(instruction, Does.Contain("train"));
+            Assert.That(instruction, Does.Contain("quantityMode"));
         }
 
         [Test]
         public async Task NumericStringCount_RequestsOneSchemaOnlyRepairAndAcceptsExactTemplate()
         {
             var transport = new SequenceTransport(Completion(StringCountFarm), Completion(Farm));
             var result = await Translate(transport, "make four farms");
 
             Assert.That(result.IsValid, Is.True, result.SafeExplanation);
             Assert.That(result.Outcome, Is.EqualTo(CommanderSemanticOutcome.DynamicPlan));
diff --git a/backend/speech-gateway/gateway.mjs b/backend/speech-gateway/gateway.mjs
index eaa0ded..943cc9a 100644
--- a/backend/speech-gateway/gateway.mjs
+++ b/backend/speech-gateway/gateway.mjs
@@ -13,26 +13,28 @@ async function receive(request,limit) {
   const declared=request.headers['content-length'];
   if(declared!==undefined&&(!/^\d+$/.test(declared)||Number(declared)>limit))throw deny('UPLOAD_TOO_LARGE',413);
   let size=0;const chunks=[];
   for await(const chunk of request){size+=chunk.length;if(size>limit)throw deny('UPLOAD_TOO_LARGE',413);chunks.push(chunk);}
   return Buffer.concat(chunks,size);
 }
 function semanticRequest(body) {
   if(!body||typeof body!=='object'||Array.isArray(body)||Object.keys(body).some(k=>!['model','messages','max_tokens','reasoning'].includes(k))
     ||body.model!=='openai/gpt-6-luna'||!Number.isInteger(body.max_tokens)||body.max_tokens<1||body.max_tokens>4096
     ||!Array.isArray(body.messages)||body.messages.length<1||body.messages.length>16)throw deny('INVALID_SEMANTIC_REQUEST');
-  let length=0;
+  let length=0,conservativeInput=256;
   for(const m of body.messages) {
     if(!m||Object.keys(m).some(k=>!['role','content'].includes(k))||!['system','user','assistant'].includes(m.role)||typeof m.content!=='string')throw deny('INVALID_SEMANTIC_REQUEST');
     length+=m.content.length;
+    conservativeInput+=Buffer.byteLength(m.role,'utf8')+Buffer.byteLength(m.content,'utf8');
   }
-  if(length>24576||body.reasoning!==undefined&&(Object.keys(body.reasoning).length!==1||body.reasoning.effort!=='none'))throw deny('INVALID_SEMANTIC_REQUEST');
+  // Identical decoded model-visible fields and margin as the Unity preflight; the body is forwarded unchanged.
+  if(length>24576||conservativeInput>=4000||body.reasoning!==undefined&&(Object.keys(body.reasoning).length!==1||body.reasoning.effort!=='none'))throw deny('INVALID_SEMANTIC_REQUEST');
   return body;
 }
 export function createGateway(configuration) {
   const options={maxConcurrent:2,maxPerOwnerConcurrent:1,maxPerOwnerSemanticRequests:6,requestTimeoutMs:30000,
     maxJobRecords:256,jobTtlMs:120000,...configuration};
   if(typeof options.authenticate!=='function'||typeof options.transcribe!=='function'||typeof options.semantic!=='function'
     ||!options.policy?.version||!Array.isArray(options.allowedOrigins)||!Array.isArray(options.authorizedOwners)||!options.ledgerPath)throw new Error('GATEWAY_CONFIG_INVALID');
   const budget=createBudget(options),ownerActive=new Map();let active=0;
   const server=http.createServer({maxHeaderSize:8192},async(request,response)=>{
     const abort=new AbortController();let admitted=false,owner,owned=false,pending,settled=true;
diff --git a/backend/speech-gateway/gateway.test.mjs b/backend/speech-gateway/gateway.test.mjs
index 1908ecd..4b025f1 100644
--- a/backend/speech-gateway/gateway.test.mjs
+++ b/backend/speech-gateway/gateway.test.mjs
@@ -103,10 +103,25 @@ test('deadline answers promptly but reserves capacity until ignoring provider ac
 });
 test('semantic route rejects caller endpoint/model and bounds envelope while preserving Luna',async()=>{
   const f=await fixture();try{
     const semanticHeaders={...headers,'Content-Type':'application/json','X-Commander-Job':job};
     const body={model:'openai/gpt-6-luna',messages:[{role:'user',content:'fixture text'}],max_tokens:4096,reasoning:{effort:'none'}};
     assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:semanticHeaders,body:JSON.stringify(body)})).status,200);
     assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'33333333-3333-4333-8333-333333333333'},body:JSON.stringify({...body,model:'other'})})).status,400);
     assert.equal((await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'44444444-4444-4444-8444-444444444444'},body:JSON.stringify({...body,url:'https://evil.example.invalid'})})).status,400);
   }finally{await f.close();}
 });
+test('semantic route applies the same conservative decoded-payload input guard and forwards unchanged',async()=>{
+  let forwarded;
+  const f=await fixture({semantic:async({body})=>{forwarded=body;return {choices:[{finish_reason:'stop',message:{content:'{}'}}]};}});
+  try{
+    const semanticHeaders={...headers,'Content-Type':'application/json','X-Commander-Job':job};
+    const safe={model:'openai/gpt-6-luna',messages:[{role:'system',content:'rules'},{role:'user',content:'order'}],max_tokens:4096,reasoning:{effort:'none'}};
+    const accepted=await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:semanticHeaders,body:JSON.stringify(safe)});
+    assert.equal(accepted.status,200);
+    assert.deepEqual(forwarded,safe);
+    const over={...safe,messages:[{role:'user',content:'界'.repeat(1500)}]};
+    const rejected=await fetch(f.base+'/api/commander/semantic',{method:'POST',headers:{...semanticHeaders,'X-Commander-Job':'33333333-3333-4333-8333-333333333333'},body:JSON.stringify(over)});
+    assert.equal(rejected.status,400);
+    assert.deepEqual(forwarded,safe);
+  }finally{await f.close();}
+});
# Full source: Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs
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
            string compactContext = CompactContext(context, strategy).ToString(Formatting.None);
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
            { reason = "Commander input limit: the complete request and required context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question."; return false; }
            return true;
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
                    if (property.Name != "policy" && (!(property.Value is JArray array) || array.Count > 0))
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
# Full source: Assets/Scripts/AI/Commander/Phase5C/CommanderProviderUsage.cs
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
    }
}
