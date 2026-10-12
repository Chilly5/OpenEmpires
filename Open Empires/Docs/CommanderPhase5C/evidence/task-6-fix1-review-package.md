# Task6 fix round1 incremental review
Exact pre-fix1 bytes vs current source; unchanged HEAD4c9de96adbf6342fbe9e1490fa9b898a0f99caf6. Additional executor and affected fixture bytes snapshotted before their edits. No commits.
## Assets\Scripts\AI\Commander\CommanderGoal.cs
Before SHA256:CF3BCBC96005DE0B9F4B42F7220F7D91AA97EEA8E0B506A86637601430B0D985
After SHA256:CF3BCBC96005DE0B9F4B42F7220F7D91AA97EEA8E0B506A86637601430B0D985
No delta.
## Assets\Scripts\AI\Commander\CommanderGoalManager.cs
Before SHA256:D09934D21DA93FFB5CC8F929DE65278A58C625F4F5E9EE8E1BEAEB4686FF8082
After SHA256:D09934D21DA93FFB5CC8F929DE65278A58C625F4F5E9EE8E1BEAEB4686FF8082
No delta.
## Assets\Scripts\AI\Commander\CommanderTacticalStatusSnapshot.cs
Before SHA256:682B3588D374D458661D0D16526CD09C5AF8BAAE4A87AC149DED400448259C90
After SHA256:682B3588D374D458661D0D16526CD09C5AF8BAAE4A87AC149DED400448259C90
No delta.
## Assets\Scripts\AI\Commander\Economy\CommanderRequestGraphDraft.cs
Before SHA256:FF564A3B1876C94B7CCDD380BFB3553E8A7C1F9FC3F407211D49FF8A7C7591AF
After SHA256:FF564A3B1876C94B7CCDD380BFB3553E8A7C1F9FC3F407211D49FF8A7C7591AF
No delta.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.Clarification.cs
Before SHA256:28F940BADAC21A33BF20B6C99DE4E9716C498B2EBDC9C5B2871F352E7A55430A
After SHA256:28F940BADAC21A33BF20B6C99DE4E9716C498B2EBDC9C5B2871F352E7A55430A
No delta.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.cs
Before SHA256:FFBE9B01FB297F87347E7DA793C5D69562C76C42B9D03DC5DFD2DEDA211B0CAF
After SHA256:A31496660F9B6D1D96F23022A602BD92946B01098DE6BBC8DC025926B2B8D898
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"
index 2019d5d..8e1452a 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.cs"	
@@ -40,20 +40,22 @@ namespace OpenEmpires
         private Coroutine bootstrapWait;
         private readonly CommanderIntentRouter intentRouter = new CommanderIntentRouter();
         private StrategicAIApprovalBridge strategicBridge;
         private StrategicPipeline strategicPipeline;
         private Button approveStrategyButton;
         private Button confirmStrategyButton;
         private Button dismissStrategyButton;
         private bool submitting;
         private CancellationTokenSource activeSemanticRequest;
         private int semanticRequestRevision;
+        // Local observation only. A read-only question never receives an executable ticket.
+        internal long? LastSubmittedSemanticRequestId { get; private set; }
         private bool retainSubmittedDraft;
         private int runtimeGeneration;
         private StrategicAdaptationProposal pendingAdaptationProposal;
         private StrategicPipeline pendingStrategicPipeline;
         private int pendingStrategicGeneration;
         private int pendingStrategicOwner;
         private int pendingStrategicIntentId;
         private int pendingStrategicLastPlanId;
         private bool pendingStrategicHadNoPlan;
 
@@ -471,20 +473,21 @@ namespace OpenEmpires
                 try
                 {
                     bool IsCurrentScope() => this != null && runtimeGeneration == generation
                         && semanticRequestRevision == requestRevision
                         && !lifetime.IsCancellationRequested
                         && ReferenceEquals(semanticGoalManager, manager)
                         && ReferenceEquals(semanticSimulation, simulation)
                         && ReferenceEquals(semanticDispatcher, dispatcher)
                         && ReferenceEquals(semanticProvider, provider);
                     var requestTicket = capturedPending?.RequestTicket ?? manager.BeginSemanticRequest(message, generation, IsCurrentScope);
+                    LastSubmittedSemanticRequestId = requestTicket?.Id;
                     if (capturedPending != null && (!ReferenceEquals(pendingClarification, capturedPending)
                         || request.IsCancellationRequested || requestTicket == null
                         || capturedPending.OriginalText != requestTicket.OriginalInput
                         || !requestTicket.BindLocalContinuation(manager, generation, IsCurrentScope)))
                     {
                         pendingClarification = null;
                         AppendLine("Commander", "The retained request is no longer current; please submit a new request.");
                         return null;
                     }
                     semanticStage = "context-projection";
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.EvidenceCapture.cs
Before SHA256:90FF148818A2717135B1FF9D5377EE7B73D79D6107A705E42EA8A0B7EE0304CC
After SHA256:90FF148818A2717135B1FF9D5377EE7B73D79D6107A705E42EA8A0B7EE0304CC
No delta.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.EvidenceCapture.cs.meta
Before SHA256:63012AFA25653C3E5EDFCF52B20D70B56E4E3C048895B25A66DEFC0CB121B264
After SHA256:63012AFA25653C3E5EDFCF52B20D70B56E4E3C048895B25A66DEFC0CB121B264
No delta.
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.Presentation.cs
Before SHA256:B75BEACE203D55C20D4B795580082FE88E9BDCDB6CF7B1A83C75E794BC45B7AD
After SHA256:1661344FADD3B6B3F29312E1470ED995AA701035D7BBF7838BD54955B5D5EE47
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"
index feab37f..2eb7471 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4A\\CommanderChatUI.Presentation.cs"	
@@ -132,23 +132,26 @@ namespace OpenEmpires
         {
             latestRequestState = state ?? "Ready";
             latestRequestDetail = detail ?? string.Empty;
             UpdatePresentation();
         }
 
         private long BeginPresentationTurn()
         {
             currentRequestGoalId = 0;
             recoveryAvailable = false;
+            LastSubmittedSemanticRequestId = null;
             return ++presentationTurnRevision;
         }
 
+        internal long LastSubmittedUiTurnId => presentationTurnRevision;
+
         private bool IsLatestPresentationTurn(long turn) => turn == presentationTurnRevision;
 
         private void PublishLatestRequestState(long turn, string state, string detail)
         {
             if (IsLatestPresentationTurn(turn)) SetLatestRequestState(state, detail);
         }
 
         private void OpenSettingsMenu()
         {
             HideVoiceSetup();
```
## Assets\Scripts\AI\Commander\Phase4A\CommanderChatUI.ReadOnlyQuestions.cs
Before SHA256:A8D5DD169EB9344FFC908B989EE6B4E796FB3FA7B9D42627262F4173D498D4EE
After SHA256:A8D5DD169EB9344FFC908B989EE6B4E796FB3FA7B9D42627262F4173D498D4EE
No delta.
## Assets\Scripts\AI\Commander\Phase4G\CommanderCapabilityExecutor.cs
Before SHA256:F3D3EB16466F8370D0683EA1F21629371841AE8E6EBFDD4F80AE1176E0C2D90C
After SHA256:AAB19EE1B4FDAA64E78773CCC7F6864D79E8FA1F2302DC3C10445FBC497B3306
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"
index e94bc13..bf04325 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Scripts\\AI\\Commander\\Phase4G\\CommanderCapabilityExecutor.cs"	
@@ -1,13 +1,14 @@
 using System;
 using System.Collections.Generic;
 using System.Linq;
+using UnityEngine;
 
 namespace OpenEmpires
 {
     // Resolves semantic selectors against current, owned and visible simulation state.
     // No provider-authored entity id, worker id or coordinate is accepted here.
     internal sealed class CommanderCapabilityExecutor
     {
         private readonly GameSimulation simulation;
 
         public CommanderCapabilityExecutor(GameSimulation simulation)
@@ -107,22 +108,28 @@ namespace OpenEmpires
             switch (intent.ActionType)
             {
                 case CommanderCapabilityActionType.MoveUnits:
                 case CommanderCapabilityActionType.DefendArea:
                 case CommanderCapabilityActionType.RetreatUnits:
                 case CommanderCapabilityActionType.ScoutArea:
                     command = new MoveCommand(intent.PlayerId, unitIds, position);
                     reason = "Resolved a bounded owned-unit movement action.";
                     return true;
                 case CommanderCapabilityActionType.PatrolArea:
+                    if (!TryResolvePatrolPoint(intent.PlayerId, intent.LocationSelector, resource,
+                        unitIds, position, out position))
+                    {
+                        reason = "No known walkable patrol point with a complete route for every exact selected unit exists near the requested anchor.";
+                        return false;
+                    }
                     command = new PatrolCommand(intent.PlayerId, unitIds, position);
-                    reason = "Resolved a point patrol route from each selected unit's starting position to a visible game-side anchor.";
+                    reason = "Resolved a point patrol route from each selected unit's starting position to a known walkable point beside the selected game-side anchor.";
                     return true;
                 case CommanderCapabilityActionType.SetRallyPoint:
                     return TryRally(intent.PlayerId, intent.StructureType, position, resource,
                         binding.HasValue && resolvedBinding.Kind == CommanderResultKind.Building
                             ? resolvedBinding.BuildingId : -1,
                         out command, out reason);
                 case CommanderCapabilityActionType.AttackTarget:
                     if (enemyUnit != null)
                     {
                         command = new AttackUnitCommand(intent.PlayerId, unitIds, enemyUnit.Id);
@@ -136,20 +143,114 @@ namespace OpenEmpires
                         return true;
                     }
                     reason = "No visible enemy target is available.";
                     return false;
                 default:
                     reason = "Unsupported Commander capability.";
                     return false;
             }
         }
 
+        // A semantic anchor may be the occupied center of a TC or resource. Native patrol
+        // needs a point it can reach in both directions; search only the anchor footprint
+        // plus two tiles, in deterministic nearest-first order. Never substitute another anchor.
+        private bool TryResolvePatrolPoint(int playerId, CommanderLocationSelector selector,
+            ResourceNodeData resource, int[] unitIds, FixedVector3 anchor, out FixedVector3 point)
+        {
+            point = default;
+            if (unitIds == null || unitIds.Length == 0) return false;
+            Vector2Int anchorTile = simulation.MapData.WorldToTile(anchor);
+            int left = anchorTile.x, bottom = anchorTile.y, right = left + 1, top = bottom + 1;
+            if (selector.Kind == CommanderLocationSelectorKind.PlayerBase)
+            {
+                BuildingData tc = simulation.BuildingRegistry.GetAllBuildings()
+                    .Where(b => b != null && b.PlayerId == playerId && !b.IsDestroyed
+                        && simulation.GetEffectiveBuildingType(b) == BuildingType.TownCenter)
+                    .OrderBy(b => b.Id).FirstOrDefault();
+                if (tc == null || simulation.MapData.WorldToTile(tc.SimPosition) != anchorTile) return false;
+                left = tc.OriginTileX; bottom = tc.OriginTileZ;
+                right = left + tc.TileFootprintWidth; top = bottom + tc.TileFootprintHeight;
+            }
+            else if (resource != null)
+            {
+                left = resource.TileX; bottom = resource.TileZ;
+                right = left + resource.FootprintWidth; top = bottom + resource.FootprintHeight;
+            }
+            var candidates = new List<Vector2Int>();
+            for (int x = left - 2; x < right + 2; x++)
+                for (int z = bottom - 2; z < top + 2; z++)
+                {
+                    if (x >= left && x < right && z >= bottom && z < top) continue;
+                    candidates.Add(new Vector2Int(x, z));
+                }
+            foreach (Vector2Int tile in candidates.OrderBy(candidate =>
+                (candidate.x - anchorTile.x) * (candidate.x - anchorTile.x)
+                + (candidate.y - anchorTile.y) * (candidate.y - anchorTile.y))
+                .ThenBy(candidate => candidate.x).ThenBy(candidate => candidate.y))
+            {
+                if (!simulation.MapData.IsWalkable(tile.x, tile.y)
+                    || !simulation.MapData.IsWalkable(tile.x, tile.y, playerId, simulation.BuildingRegistry)
+                    || !IsKnownPatrolTile(playerId, tile)) continue;
+                bool allReachable = true;
+                foreach (int id in unitIds)
+                {
+                    UnitData unit = simulation.UnitRegistry.GetUnit(id);
+                    if (unit == null || unit.PlayerId != playerId || unit.CurrentHealth <= 0
+                        || unit.State == UnitState.Dead) { allReachable = false; break; }
+                    Vector2Int start = simulation.MapData.WorldToTile(unit.SimPosition);
+                    if (start == tile || !simulation.MapData.IsWalkable(start.x, start.y, playerId, simulation.BuildingRegistry)
+                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, start, tile,
+                            out List<Vector2Int> outward, playerId, simulation.BuildingRegistry)
+                        || !IsKnownPatrolPath(playerId, start, outward)
+                        || !GridPathfinder.TryFindCompletePath(simulation.MapData, tile, start,
+                            out List<Vector2Int> returning, playerId, simulation.BuildingRegistry)
+                        || !IsKnownPatrolPath(playerId, tile, returning))
+                    { allReachable = false; break; }
+                }
+                if (!allReachable) continue;
+                point = simulation.MapData.TileToWorldFixed(tile.x, tile.y);
+                return true;
+            }
+            return false;
+        }
+
+        private bool IsKnownPatrolPath(int playerId, Vector2Int start, List<Vector2Int> path)
+        {
+            Vector2Int from = start;
+            foreach (Vector2Int to in path)
+            {
+                int x = from.x, z = from.y;
+                int dx = Mathf.Abs(to.x - x), dz = Mathf.Abs(to.y - z);
+                int sx = x < to.x ? 1 : -1, sz = z < to.y ? 1 : -1;
+                int error = dx - dz;
+                while (true)
+                {
+                    if (!IsKnownPatrolTile(playerId, new Vector2Int(x, z))) return false;
+                    if (x == to.x && z == to.y) break;
+                    int twice = 2 * error;
+                    bool stepX = twice > -dz, stepZ = twice < dx;
+                    if (stepX && stepZ && (!IsKnownPatrolTile(playerId, new Vector2Int(x + sx, z))
+                        || !IsKnownPatrolTile(playerId, new Vector2Int(x, z + sz)))) return false;
+                    if (stepX) { error -= dz; x += sx; }
+                    if (stepZ) { error += dx; z += sz; }
+                }
+                from = to;
+            }
+            return IsKnownPatrolTile(playerId, start);
+        }
+
+        private bool IsKnownPatrolTile(int playerId, Vector2Int tile)
+        {
+            TileVisibility visibility = simulation.FogOfWar.GetVisibility(playerId, tile.x, tile.y);
+            return visibility == TileVisibility.Visible || visibility == TileVisibility.Explored;
+        }
+
         private bool TryResolveUnits(int playerId, CommanderUnitSelector selector,
             IReadOnlyList<int> boundIds, out int[] ids)
         {
             if (boundIds != null)
             {
                 if (boundIds.Count != selector.Count) { ids = null; return false; }
                 var exact = new List<int>(boundIds.Count);
                 for (int i = 0; i < boundIds.Count; i++)
                 {
                     int id = boundIds[i];
```
## Assets\Scripts\AI\Commander\Phase5A\CommanderRequestTicket.cs
Before SHA256:96CA372B5926A30F549A25D969A412054E63B2BDD3C41C1B302B3FA8DA473CEB
After SHA256:96CA372B5926A30F549A25D969A412054E63B2BDD3C41C1B302B3FA8DA473CEB
No delta.
## Assets\Tests\EditMode\CommanderEconomyClarificationTests.Chat.cs
Before SHA256:7FCBA69A488B69D324AC5B6E2A0CE3D849E3A8C6579761115ADC15D8E6F7E406
After SHA256:7FCBA69A488B69D324AC5B6E2A0CE3D849E3A8C6579761115ADC15D8E6F7E406
No delta.
## Assets\Tests\EditMode\CommanderEconomyClarificationTests.Graph.cs
Before SHA256:71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA
After SHA256:71854326E2624ACE778FAD56222ECB1AA55A709148FC8EFF7DA1BA37F7AC03CA
No delta.
## Assets\Tests\EditMode\CommanderEconomyClarificationTests.Phase5CCorpus.cs
Before SHA256:681D9A9CDFA43F0A5E09EA54C32077EB517105DF9B70BE55E7B6313095AED31D
After SHA256:FC2BA9991D394DF043B1F9519D8CEC1B99D4472E536D1F7AFCF6D959ABA3DD69
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderEconomyClarificationTests.Phase5CCorpus.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderEconomyClarificationTests.Phase5CCorpus.cs"
index d3a7605..76a5e20 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderEconomyClarificationTests.Phase5CCorpus.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderEconomyClarificationTests.Phase5CCorpus.cs"	
@@ -185,20 +185,148 @@ namespace OpenEmpires.Tests
             {
                 fixture.Seed("unattributed-builder-control");
                 Assert.That(fixture.BuilderAffinityVerified, Is.False);
                 Assert.That(fixture.Native, Does.Not.Contain("PlaceBuildingCommand:processed"));
                 await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                 Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.Not.EqualTo("human-override"));
                 Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
             }
         }
 
+        [Test]
+        [Category("CommanderPhase5CCorpus")]
+        public async Task CorpusMalformedSubmission_RetainsRealTicketInsteadOfCaseLabel()
+        {
+            Assert.That(TryResponse("malformed-provider-json", out string response), Is.True);
+            using (var fixture = new CorpusFixture(response, "malformed-provider-json"))
+            {
+                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
+                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
+                Assert.That(RequestCorrelation(fixture, "malformed-provider-json"), Does.StartWith("runtime:"));
+            }
+        }
+
+        [Test]
+        [Category("CommanderPhase5CCorpus")]
+        public async Task CorpusReadOnlySubmission_HasNoExecutableTicketOrCaseLabel()
+        {
+            Assert.That(TryResponse("causal-information-owned-task-facts", out string response), Is.True);
+            using (var fixture = new CorpusFixture(response, "causal-information-owned-task-facts"))
+            {
+                fixture.Seed("causal-information-owned-task-facts");
+                await fixture.Chat.SubmitMessageAsync("Why is my troop production stuck?");
+                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
+                Assert.That(RequestCorrelation(fixture, "causal-information-owned-task-facts"), Is.Null);
+                Assert.That((string)NewTurn("causal-information-owned-task-facts", "Why is my troop production stuck?")["requestId"], Is.Null);
+            }
+        }
+
+        [Test]
+        [Category("CommanderPhase5CCorpus")]
+        public async Task CorpusMalformedVerdict_AcceptedResponseCannotRemainObservedRejection()
+        {
+            string accepted = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
+            using (var fixture = new CorpusFixture(accepted, "malformed-provider-json"))
+            {
+                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
+                var row = NewTurn("malformed-provider-json", "Gather 200 gold with 3 villagers.");
+                row["status"] = "observed";
+                EvaluateFixture("malformed-provider-json", fixture, row);
+                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
+                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
+            }
+        }
+
+        [TestCase("{not-json")]
+        [TestCase("faithful")]
+        [Category("CommanderPhase5CCorpus")]
+        public async Task CorpusWrongVerdict_NonWrongResponseCannotRemainCertifiedWrong(string variant)
+        {
+            string response = variant == "faithful"
+                ? Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile")) : variant;
+            using (var fixture = new CorpusFixture(response, "schema-valid-wrong-provider-result"))
+            {
+                fixture.Seed("schema-valid-wrong-provider-result");
+                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
+                var row = NewTurn("schema-valid-wrong-provider-result", "Gather 200 gold with 3 villagers.");
+                row["status"] = "observed";
+                EvaluateFixture("schema-valid-wrong-provider-result", fixture, row);
+                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
+                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
+            }
+        }
+
+        [Test]
+        [Category("CommanderPhase5CCorpus")]
+        public async Task CorpusOrderedVerdict_ProcessedCallbackWithoutPatrollingActorIsGap()
+        {
+            Assert.That(TryResponse("ordered-production-exact-new-units", out string response), Is.True);
+            using (var fixture = new CorpusFixture(response, "ordered-production-exact-new-units"))
+            {
+                fixture.Seed("ordered-production-exact-new-units");
+                await fixture.Chat.SubmitMessageAsync("Train two spearmen, then send those new spearmen to patrol.");
+                bool previewAtBase = fixture.Chat.PendingActionPlan?.Preview?
+                    .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
+                await fixture.Chat.SubmitMessageAsync("Approve plan");
+                fixture.TickBounded(Math.Min(8000, 4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000));
+                var result = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().Single().ResultUnitIds;
+                Assert.That(result.Count, Is.EqualTo(2));
+                var actor = fixture.Simulation.UnitRegistry.GetUnit(result[0]);
+                Assert.That(actor.IsPatrolling, Is.True, "The adversarial control starts from real native patrol state.");
+                actor.ClearPatrol(); // replay a native skip/loss despite a processed callback
+                var row = NewTurn("ordered-production-exact-new-units", "Train two spearmen, then send those new spearmen to patrol.");
+                row["status"] = "observed"; row["approved"] = true;
+                row["previewMentionsOwnedBase"] = previewAtBase;
+                row["newResultCount"] = 2; row["oldUnitExcluded"] = true;
+                row["humanBirthExcluded"] = true; row["exactPatrolBinding"] = true;
+                EvaluateFixture("ordered-production-exact-new-units", fixture, row);
+                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
+                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
+            }
+        }
+
+        [Test]
+        [Category("CommanderPhase5CCorpus")]
+        public void CorpusPatrolPoint_StaysBesideOwnedBaseAndBlocksWithoutAnyReachablePoint()
+        {
+            using (var fixture = new CorpusFixture("{not-json", "ordered-production-exact-new-units"))
+            {
+                fixture.Seed("ordered-production-exact-new-units");
+                fixture.TickBounded(1); // native fog bootstrap from the owned TC
+                var tc = fixture.Simulation.BuildingRegistry.GetAllBuildings()
+                    .Single(building => building.PlayerId == 0
+                        && fixture.Simulation.GetEffectiveBuildingType(building) == BuildingType.TownCenter);
+                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
+                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
+                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
+                var executor = new CommanderCapabilityExecutor(fixture.Simulation);
+                Assert.That(executor.TryCreateCommand(intent, out ICommand command, out string reason), Is.True, reason);
+                var target = fixture.Simulation.MapData.WorldToTile(((PatrolCommand)command).TargetPosition);
+                Assert.That(target.x < tc.OriginTileX || target.x >= tc.OriginTileX + tc.TileFootprintWidth
+                    || target.y < tc.OriginTileZ || target.y >= tc.OriginTileZ + tc.TileFootprintHeight, Is.True);
+                Assert.That(fixture.Simulation.MapData.IsWalkable(target.x, target.y, 0,
+                    fixture.Simulation.BuildingRegistry), Is.True);
+
+                // Remove every candidate inside the documented two-tile neighborhood;
+                // the executor cannot widen the search or enqueue a prefix command.
+                for (int x = tc.OriginTileX - 2; x < tc.OriginTileX + tc.TileFootprintWidth + 2; x++)
+                    for (int z = tc.OriginTileZ - 2; z < tc.OriginTileZ + tc.TileFootprintHeight + 2; z++)
+                        if (x < tc.OriginTileX || x >= tc.OriginTileX + tc.TileFootprintWidth
+                            || z < tc.OriginTileZ || z >= tc.OriginTileZ + tc.TileFootprintHeight)
+                            fixture.Simulation.MapData.Tiles[x, z] = TileType.Water;
+                Assert.That(executor.TryCreateCommand(intent, out command, out reason), Is.False);
+                Assert.That(command, Is.Null);
+                Assert.That(reason, Does.Contain("complete route"));
+                Assert.That(fixture.Simulation.CommandBuffer.FlushCommands(), Is.Empty);
+            }
+        }
+
         [Test]
         [Category("CommanderPhase5CCorpus")]
         public async Task CorpusGarrison_CapacityAndProtectedRemainderAreNativeFacts()
         {
             Assert.That(TryResponse("garrison-capacity-protected-remainder", out string response), Is.True);
             using (var fixture = new CorpusFixture(response))
             {
                 fixture.Seed("garrison-capacity-protected-remainder");
                 await fixture.Chat.SubmitMessageAsync("Garrison all my villagers in my Town Center.");
                 fixture.TickBounded(500);
@@ -217,47 +345,86 @@ namespace OpenEmpires.Tests
             var run = typeof(CommanderEconomyClarificationTests).GetMethod("RunPhase5CCorpusAsync",
                 BindingFlags.NonPublic | BindingFlags.Static);
             Assert.That(run, Is.Not.Null, "The declared 25-case corpus needs an executable production-chat replay.");
             string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
             string corpus = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "phase5c-replay-corpus.v1.json");
             string directory = Path.Combine(projectRoot, "Docs", "CommanderPhase5C", "evidence", "task-6-runs",
                 Guid.NewGuid().ToString("N"));
             string result = await (Task<string>)run.Invoke(null, new object[] { corpus, directory });
             Assert.That(result, Is.EqualTo(directory));
             Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(25));
+            var declared = (JArray)JObject.Parse(File.ReadAllText(corpus))["cases"];
+            Assert.That(declared.Count, Is.EqualTo(25));
+            int declaredTurns = 0, replayedDeclaredTurns = 0;
+            foreach (JObject specification in declared)
+            {
+                string caseId = (string)specification["id"];
+                int count = ((JArray)specification["turns"]).Count;
+                JObject record = JObject.Parse(File.ReadAllText(Path.Combine(directory, caseId + ".json")));
+                int replayed = ((JArray)record["turnHistory"]).Count(entry =>
+                    (string)entry["origin"] != "offline-fixture-control");
+                Assert.That(replayed, Is.EqualTo(count), caseId + " declared turns");
+                declaredTurns += count; replayedDeclaredTurns += replayed;
+            }
+            Assert.That(declaredTurns, Is.EqualTo(35));
+            Assert.That(replayedDeclaredTurns, Is.EqualTo(35));
             JObject mine = JObject.Parse(File.ReadAllText(Path.Combine(directory, "mine-yes-original-request.json")));
             JObject house = JObject.Parse(File.ReadAllText(Path.Combine(directory, "house-next-five-from-trees.json")));
             JObject malformed = JObject.Parse(File.ReadAllText(Path.Combine(directory, "malformed-provider-json.json")));
             JObject wrong = JObject.Parse(File.ReadAllText(Path.Combine(directory, "schema-valid-wrong-provider-result.json")));
             JObject ordered = JObject.Parse(File.ReadAllText(Path.Combine(directory, "ordered-production-exact-new-units.json")));
             JObject countCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "count-correction-only.json")));
             JObject amountCorrection = JObject.Parse(File.ReadAllText(Path.Combine(directory, "amount-correction-only.json")));
             JObject additionalFood = JObject.Parse(File.ReadAllText(Path.Combine(directory, "additional-gathered-food-bounded-objective.json")));
             JObject cancelled = JObject.Parse(File.ReadAllText(Path.Combine(directory, "cancel-reset-late-retry.json")));
             JObject garrison = JObject.Parse(File.ReadAllText(Path.Combine(directory, "garrison-capacity-protected-remainder.json")));
+            JObject readOnly = JObject.Parse(File.ReadAllText(Path.Combine(directory, "causal-information-owned-task-facts.json")));
             Assert.That((string)mine["status"], Is.EqualTo("observed"));
             Assert.That((string)mine["fidelity"], Is.EqualTo("matched"));
             Assert.That((int)mine["providerCalls"], Is.EqualTo(1));
             Assert.That((string)house["status"], Is.EqualTo("observed"));
             Assert.That((string)house["fidelity"], Is.EqualTo("matched"));
             Assert.That((int?)house["futureObservedCount"], Is.EqualTo(5));
             Assert.That((int?)house["futureAssignedCount"], Is.EqualTo(5));
             Assert.That((int)house["providerCalls"], Is.EqualTo(1));
             Assert.That((string)malformed["errorStage"], Is.EqualTo("parse"));
+            Assert.That((string)malformed["status"], Is.EqualTo("observed"));
+            Assert.That((string)malformed["fidelity"], Is.EqualTo("not-evaluable"));
+            Assert.That((bool?)malformed["semanticValid"], Is.False);
+            Assert.That((string)malformed["errorCategory"], Is.EqualTo("malformed-rejected"));
+            Assert.That((string)malformed["requestId"], Does.StartWith("runtime:"));
+            Assert.That((string)malformed["turnCorrelationId"], Does.StartWith("ui-turn:"));
+            Assert.That((int)malformed["providerCalls"], Is.EqualTo(1));
+            Assert.That((int)malformed["commandCount"], Is.Zero);
+            Assert.That(((JArray)malformed["admittedFields"]).Count, Is.Zero);
+            Assert.That((string)wrong["status"], Is.EqualTo("observed"));
             Assert.That((string)wrong["fidelity"], Is.EqualTo("wrong"));
+            Assert.That((bool?)wrong["semanticValid"], Is.True);
+            Assert.That((string)wrong["errorCategory"], Is.EqualTo("schema-valid-wrong-action"));
+            Assert.That(((JArray)wrong["admittedFields"]).Count, Is.EqualTo(1));
+            Assert.That((string)wrong["admittedFields"][0]["originalUtterance"],
+                Is.EqualTo("Gather 200 gold with 3 villagers."));
+            Assert.That((string)wrong["admittedFields"][0]["destinationResource"], Is.EqualTo("Wood"));
+            Assert.That((int)wrong["admittedFields"][0]["count"], Is.EqualTo(2));
+            Assert.That((string)readOnly["requestId"], Is.Null);
+            Assert.That((string)readOnly["turnCorrelationId"], Does.StartWith("ui-turn:"));
+            Assert.That((int)readOnly["commandCount"], Is.Zero);
             Assert.That((bool?)ordered["semanticValid"], Is.True);
             Assert.That((string)ordered["status"], Is.EqualTo("observed"));
             Assert.That((string)ordered["fidelity"], Is.EqualTo("matched"));
             Assert.That((int?)ordered["newResultCount"], Is.EqualTo(2));
             Assert.That((bool?)ordered["oldUnitExcluded"], Is.True);
             Assert.That((bool?)ordered["humanBirthExcluded"], Is.True);
             Assert.That((bool?)ordered["exactPatrolBinding"], Is.True);
+            Assert.That((int?)ordered["nativePatrolActorCount"], Is.EqualTo(2));
+            Assert.That((bool?)ordered["nativePatrolWaypointsMatch"], Is.True);
+            Assert.That((bool?)ordered["nativePatrolDestinationMatch"], Is.True);
             Assert.That((bool?)ordered["previewMentionsOwnedBase"], Is.True);
             Assert.That((string)countCorrection["status"], Is.EqualTo("observed"));
             Assert.That((string)amountCorrection["status"], Is.EqualTo("observed"));
             Assert.That((string)countCorrection["fidelity"], Is.EqualTo("matched"));
             Assert.That((string)amountCorrection["fidelity"], Is.EqualTo("matched"));
             Assert.That((string)((JArray)countCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
             Assert.That((string)((JArray)amountCorrection["admittedFields"])[0]["mode"], Is.EqualTo("TargetTotal"));
             Assert.That((int)countCorrection["providerCalls"], Is.EqualTo(1));
             Assert.That((int)amountCorrection["providerCalls"], Is.EqualTo(1));
             Assert.That((string)additionalFood["status"], Is.EqualTo("observed"));
@@ -330,20 +497,21 @@ namespace OpenEmpires.Tests
             internal int InitialGoalCount;
             internal int InitialCommanderCommandCount;
             internal int HumanTrainCommands;
             internal int? FoodCompletionTick;
             internal long? FoodIncomeAtCompletion;
             internal int? CommanderCommandsAtFoodCompletion;
             internal bool OrdinaryFoodGatherActive;
             internal int OldSpearmanId = -1;
             internal int HumanBirthId = -1;
             internal int[] PatrolSubjects;
+            internal FixedVector3? PatrolTarget;
             internal bool BuilderAffinityVerified;
             internal bool AssignedBuilderHumanProtected;
             internal bool AssignedBuilderMissing;
             private readonly string caseId;
             private bool houseHumanQueueSeeded;
             private readonly GameObject host;
             internal CorpusFixture(string response, string id = null)
             {
                 caseId = id;
                 if (id == "finite-discovery-distinct-from-point-scout") Discovery = new DiscoveryFixture();
@@ -354,21 +522,24 @@ namespace OpenEmpires.Tests
                 Chat.Initialize(Provider, Simulation, Manager, Dispatcher);
                 Simulation.CommandBuffer.CommandEnqueued += OnCommandEnqueued;
                 Simulation.NativeMoveProcessed += OnMoveProcessed;
                 Simulation.NativeUnitCommandProcessed += OnUnitCommandProcessed;
             }
             private void OnCommandEnqueued(ICommand command, CommandEnqueueSource source)
             {
                 if (source == CommandEnqueueSource.Commander && Commands.Count < 32)
                     Commands.Add(command.GetType().Name);
                 if (source == CommandEnqueueSource.Commander && command is PatrolCommand patrol)
+                {
                     PatrolSubjects = patrol.UnitIds?.ToArray();
+                    PatrolTarget = patrol.TargetPosition;
+                }
             }
             private void OnMoveProcessed(NativeMoveProcessingReceipt receipt)
             {
                 if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                 if (Native.Count < 32) Native.Add("Move:" + receipt.Result);
             }
             private void OnUnitCommandProcessed(NativeUnitCommandObservation observation)
             {
                 if (FirstNativeTicks == 0) FirstNativeTicks = Stopwatch.GetTimestamp();
                 if (Native.Count < 32) Native.Add(observation.ExecutingCommand.GetType().Name + ":processed");
@@ -689,21 +860,21 @@ namespace OpenEmpires.Tests
                 string id = (string)item["id"];
                 if (!uniqueIds.Add(id)) throw new InvalidOperationException("Duplicate corpus case.");
                 var turns = item["turns"] as JArray;
                 if (turns == null || turns.Count < 1 || turns.Count > 4)
                     throw new InvalidOperationException("Unbounded corpus turns.");
                 string original = (string)turns[0];
                 string path = Path.Combine(directory, id + ".json");
                 var evidence = new ScenarioEvidenceRecorder(path, runId);
                 var row = NewTurn(id, original);
                 var turnHistory = (JArray)row["turnHistory"];
-                string correlationId = "case:" + id;
+                string correlationId = null;
                 evidence.RecordPhase5CTurn(row); // durable before any provider/UI work
                 if (!TryResponse(id, out string response))
                 {
                     row["status"] = "open-gap";
                     row["gap"] = "No independent synthetic provider fixture or seeded native setup is declared for this case.";
                     row["errorStage"] = "fixture-coverage";
                     row["errorCategory"] = "not-executed";
                     evidence.RecordPhase5CTurn(row);
                     continue;
                 }
@@ -757,38 +928,43 @@ namespace OpenEmpires.Tests
                             row["approvalRequired"] = fixture.Chat.PendingActionPlan != null;
                             if (id == "ordered-production-exact-new-units")
                             {
                                 row["fixtureAssumption"] = "The utterance omits patrol destination; this valid offline fixture supplies the bounded owned-base PlayerBase selector.";
                                 row["previewMentionsOwnedBase"] = fixture.Chat.PendingActionPlan?.Preview?
                                     .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
                             }
                             row["approved"] = awaitingApproval && fixture.Chat.PendingActionPlan == null
                                 && fixture.Manager.Goals.Count > 0;
                             string observedCorrelation = RequestCorrelation(fixture, id);
-                            if (observedCorrelation != "case:" + id) correlationId = observedCorrelation;
-                            row["requestId"] = correlationId;
+                            if (observedCorrelation != null) correlationId = observedCorrelation;
+                            else if (fixture.Provider.Calls > callsBefore
+                                && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true)
+                                correlationId = null;
+                            row["requestId"] = correlationId == null ? JValue.CreateNull() : (JToken)correlationId;
+                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
                             row["uiState"] = LatestUiState(fixture.Chat);
                             row["packingMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.PackMs.HasValue
                                 ? (JToken)fixture.Provider.PackMs.Value : JValue.CreateNull();
                             row["estimatedInputTokens"] = fixture.Provider.InputEstimate.HasValue
                                 ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull();
                             row["parseMs"] = fixture.Provider.Calls > callsBefore && fixture.Provider.ParseMs.HasValue
                                 ? (JToken)fixture.Provider.ParseMs.Value : JValue.CreateNull();
                             row["admissionMs"] = fixture.Provider.Calls == callsBefore || fixture.Provider.LastReturnTicks < turnStart ? JValue.CreateNull()
                                 : (JToken)Milliseconds(fixture.Provider.LastReturnTicks, Stopwatch.GetTimestamp());
                             row["commandCount"] = fixture.Commands.Count;
                             row["commandTypes"] = new JArray(fixture.Commands);
                             row["nativeResults"] = new JArray(fixture.Native);
                             row["taskStates"] = GoalStates(fixture);
                             row["status"] = "observed";
                             turnHistory.Add(new JObject { ["index"] = i, ["text"] = turn,
                                 ["requestId"] = row["requestId"].DeepClone(),
+                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
                                 ["uiState"] = row["uiState"].DeepClone(),
                                 ["providerCalls"] = fixture.Provider.Calls,
                                 ["packAccepted"] = fixture.Provider.Calls > callsBefore
                                     ? (JToken)fixture.Provider.PackSucceeded : JValue.CreateNull(),
                                 ["estimatedInputTokens"] = fixture.Provider.Calls > callsBefore && fixture.Provider.InputEstimate.HasValue
                                     ? (JToken)fixture.Provider.InputEstimate.Value : JValue.CreateNull(),
                                 ["semanticValid"] = row["semanticValid"].DeepClone(),
                                 ["semanticOutcome"] = row["semanticOutcome"].DeepClone(),
                                 ["normalizedFields"] = row["normalizedFields"].DeepClone(),
                                 ["admittedFields"] = row["admittedFields"].DeepClone(),
@@ -804,29 +980,31 @@ namespace OpenEmpires.Tests
                                 ["transportMs"] = JValue.CreateNull(),
                                 ["parseMs"] = row["parseMs"].DeepClone(),
                                 ["admissionMs"] = row["admissionMs"].DeepClone() });
                             evidence.RecordPhase5CTurn(row); // atomic checkpoint after every declared turn
                         }
                         if (fixture.Chat.PendingActionPlan != null && turns.Count == 1)
                         {
                             string correlation = correlationId;
                             long approvalStart = Stopwatch.GetTimestamp();
                             await fixture.Chat.SubmitMessageAsync("Approve plan");
-                            row["requestId"] = correlation;
+                            row["requestId"] = correlation == null ? JValue.CreateNull() : (JToken)correlation;
+                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
                             row["approvalMode"] = "offline-fixture-control-not-declared-player-turn";
                             row["approved"] = fixture.Chat.PendingActionPlan == null
                                 && fixture.Manager.Goals.Count > 0;
                             row["admittedFields"] = AdmittedFields(fixture);
                             row["uiState"] = LatestUiState(fixture.Chat);
                             row["taskStates"] = GoalStates(fixture);
                             turnHistory.Add(new JObject { ["index"] = turns.Count, ["text"] = "Approve plan",
                                 ["origin"] = "offline-fixture-control", ["requestId"] = correlation,
+                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
                                 ["uiState"] = row["uiState"].DeepClone(),
                                 ["providerCalls"] = fixture.Provider.Calls, ["approved"] = row["approved"].DeepClone(),
                                 ["admittedFields"] = row["admittedFields"].DeepClone(),
                                 ["taskStates"] = row["taskStates"].DeepClone(),
                                 ["uiSubmitMs"] = Milliseconds(approvalStart, Stopwatch.GetTimestamp()) });
                             evidence.RecordPhase5CTurn(row);
                         }
                         int tickBound = id == "malformed-provider-json" ? 0
                             : id == "causal-information-owned-task-facts" || id == "death-versus-takeover" ? 0
                             : id == "mine-yes-original-request" ? 1500
@@ -872,20 +1050,21 @@ namespace OpenEmpires.Tests
                                 .SingleOrDefault()?.ResultUnitIds ?? Array.Empty<int>();
                             row["newResultCount"] = resultIds.Count;
                             row["oldUnitExcluded"] = fixture.OldSpearmanId >= 0
                                 && !resultIds.Contains(fixture.OldSpearmanId);
                             row["humanBirthExcluded"] = fixture.HumanBirthId >= 0
                                 && !resultIds.Contains(fixture.HumanBirthId);
                             row["exactPatrolBinding"] = fixture.PatrolSubjects != null
                                 && resultIds.Count == 2
                                 && fixture.PatrolSubjects.OrderBy(value => value)
                                     .SequenceEqual(resultIds.OrderBy(value => value));
+                            CaptureNativePatrolFacts(fixture, row, resultIds);
                         }
                         var resourceGoal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().FirstOrDefault();
                         row["resourceProgress"] = resourceGoal == null ? JValue.CreateNull()
                             : (JToken)resourceGoal.ResourceProgress;
                         row["gatheredFoodIncome"] = id == "additional-gathered-food-bounded-objective"
                             ? (JToken)fixture.Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food)
                             : JValue.CreateNull();
                         row["foodCompletionTick"] = fixture.FoodCompletionTick.HasValue
                             ? (JToken)fixture.FoodCompletionTick.Value : JValue.CreateNull();
                         row["foodIncomeAtCompletion"] = fixture.FoodIncomeAtCompletion.HasValue
@@ -916,21 +1095,22 @@ namespace OpenEmpires.Tests
                         boundedFailure["gap"] = "The richer record exceeded the evidence allow-list or size bound; inspect the earlier atomic checkpoint.";
                         evidence.RecordPhase5CTurn(boundedFailure);
                     }
                 }
             }
             return directory;
         }
 
         private static JObject NewTurn(string id, string original) => new JObject
         {
-            ["schemaVersion"] = 1, ["caseId"] = id, ["requestId"] = "case:" + id,
+            ["schemaVersion"] = 1, ["caseId"] = id, ["requestId"] = JValue.CreateNull(),
+            ["turnCorrelationId"] = JValue.CreateNull(),
             ["utterance"] = original, ["turnIndex"] = 0, ["status"] = "started",
             ["evidenceMode"] = "offline-production-chat-fixture", ["providerCalls"] = 0,
             ["httpAttempts"] = 0, ["packAccepted"] = JValue.CreateNull(),
             ["fidelity"] = "not-evaluated", ["semanticValid"] = JValue.CreateNull(),
             ["estimatedInputTokens"] = JValue.CreateNull(), ["actualPromptTokens"] = JValue.CreateNull(),
             ["actualCompletionTokens"] = JValue.CreateNull(), ["actualCachedTokens"] = JValue.CreateNull(),
             ["actualReasoningTokens"] = JValue.CreateNull(), ["reportedCost"] = JValue.CreateNull(),
             ["attemptCosts"] = new JArray(), ["packingMs"] = JValue.CreateNull(),
             ["transportMs"] = JValue.CreateNull(), ["parseMs"] = JValue.CreateNull(),
             ["admissionMs"] = JValue.CreateNull(), ["firstNativeActionMs"] = JValue.CreateNull(),
@@ -1172,41 +1352,61 @@ namespace OpenEmpires.Tests
         private static JArray MissingSlots(CommanderRequestGraphDraft draft)
             => new JArray(draft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
         private static JArray GoalStates(CorpusFixture fixture)
             => new JArray(fixture.Manager.Goals.Take(16).Select(goal =>
                 goal.GetType().Name + ":" + goal.Status));
         private static string LatestUiState(CommanderChatUI chat) =>
             (string)typeof(CommanderChatUI).GetField("latestRequestState",
                 BindingFlags.Instance | BindingFlags.NonPublic).GetValue(chat);
         private static string RequestCorrelation(CorpusFixture fixture, string id)
         {
-            long request = fixture.Chat.PendingClarification?.RequestId
-                ?? fixture.Chat.PendingActionPlan?.RequestId
-                ?? fixture.Manager.Goals.FirstOrDefault()?.RequestAuthority?.RequestId ?? 0;
-            return request > 0 ? "runtime:" + request : "case:" + id;
+            long? request = fixture.Chat.LastSubmittedSemanticRequestId;
+            return request > 0 ? "runtime:" + request.Value : null;
         }
         private static int? OwnedTaskFactCount(CommanderSemanticProviderRequest request)
         {
             if (request == null) return null;
             try { return (JObject.Parse(request.SerializedContext)["tacticalStatus"]?["requests"] as JArray)?.Count; }
             catch (Newtonsoft.Json.JsonException) { return null; }
         }
         private static string QuestionFactClass(CommanderSemanticProviderRequest request)
         {
             if (request == null || !request.IsReadOnlyQuestion) return null;
             string facts = request.QuestionFacts ?? string.Empty;
             if (facts.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0) return "wood-shortage";
             if (facts.IndexOf("dead or missing", StringComparison.OrdinalIgnoreCase) >= 0) return "dead-or-missing";
             if (facts.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0) return "human-override";
             if (facts.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0) return "unavailable";
             return string.IsNullOrEmpty(facts) ? "empty" : "other-owned-status";
         }
+        private static void CaptureNativePatrolFacts(CorpusFixture fixture, JObject row, IReadOnlyList<int> resultIds)
+        {
+            int active = 0;
+            bool waypointsMatch = resultIds.Count == 2 && fixture.PatrolTarget.HasValue;
+            bool destinationMatch = waypointsMatch;
+            foreach (int id in resultIds)
+            {
+                UnitData actor = fixture.Simulation.UnitRegistry.GetUnit(id);
+                bool current = actor != null && actor.PlayerId == 0 && actor.CurrentHealth > 0
+                    && actor.State != UnitState.Dead && actor.IsPatrolling;
+                if (current) active++;
+                bool route = current && actor.PatrolWaypoints != null && actor.PatrolWaypoints.Count == 2
+                    && actor.PatrolWaypoints[1] == fixture.PatrolTarget.Value;
+                waypointsMatch &= route;
+                destinationMatch &= route && (actor.FinalDestination == actor.PatrolWaypoints[0]
+                    || actor.FinalDestination == actor.PatrolWaypoints[1]);
+            }
+            row["nativePatrolActorCount"] = active;
+            row["nativePatrolWaypointsMatch"] = waypointsMatch;
+            row["nativePatrolDestinationMatch"] = destinationMatch;
+        }
+
         private static void EvaluateFixture(string id, CorpusFixture fixture, JObject row)
         {
             if (fixture.Provider.Calls > 0 && !fixture.Provider.PackSucceeded)
             {
                 row["status"] = "open-gap"; row["fidelity"] = "not-evaluable";
                 row["errorStage"] = "packing"; row["errorCategory"] = "offline-request-pack-rejected";
                 return;
             }
             bool oneCall = fixture.Provider.Calls == 1;
             bool noEffect = fixture.Manager.Goals.Count == 0 && fixture.Commands.Count == 0;
@@ -1216,33 +1416,53 @@ namespace OpenEmpires.Tests
             void Mark(bool success, string stage, string category, bool terminal = true)
             {
                 row["fidelity"] = success ? "matched" : "mismatch";
                 if (success) return;
                 row["status"] = terminal ? "observed" : "open-gap";
                 row["errorStage"] = stage;
                 row["errorCategory"] = category;
             }
             if (id == "malformed-provider-json")
             {
-                bool rejected = oneCall && fixture.Provider.LastResult?.IsValid == false && noEffect;
-                row["fidelity"] = "not-evaluable";
+                bool rejected = oneCall && fixture.Provider.LastResult?.IsValid == false
+                    && noEffect && fixture.Chat.PendingClarification == null
+                    && fixture.Chat.PendingActionPlan == null;
+                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
+                row["fidelity"] = rejected ? "not-evaluable" : "mismatch";
+                row["status"] = rejected ? "observed" : "open-gap";
                 row["errorStage"] = "parse";
-                row["errorCategory"] = rejected ? "malformed-rejected" : "malformed-unexpected-effect";
+                row["errorCategory"] = rejected ? "malformed-rejected" : "malformed-rejection-regressed";
+                if (!rejected) row["gap"] = "The malformed response did not produce exactly one invalid parse with no clarification, approval, goal or Commander command.";
                 return;
             }
             if (id == "schema-valid-wrong-provider-result")
             {
+                var admitted = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
+                var allocation = admitted?.Allocation;
+                bool wrongButAdmitted = oneCall && fixture.Provider.LastResult?.IsValid == true
+                    && fixture.Provider.LastResult.Outcome == CommanderSemanticOutcome.Request
+                    && fixture.Manager.Goals.Count == 1 && admitted != null
+                    && admitted.RequestAuthority?.OriginalInput == (string)row["utterance"]
+                    && allocation.Mode.ToString() == "SelectedCount"
+                    && allocation.CountMode.ToString() == "Exact" && allocation.Count == 2
+                    && allocation.Destination.Resource == ResourceType.Wood
+                    && allocation.Destination.SourceKind == ResourceSourceKind.Tree
+                    && allocation.ResourceAmount == 200
+                    && allocation.ResourceAmountMode.ToString() == "Stockpile";
                 row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
-                row["fidelity"] = "wrong";
-                row["errorStage"] = "semantic-fidelity";
-                row["errorCategory"] = "schema-valid-wrong-action";
-                row["gap"] = "Synthetic Wood/count-2 result differs from declared Gold/count-3/amount-200 request; actual admission is recorded separately.";
+                row["fidelity"] = wrongButAdmitted ? "wrong" : "mismatch";
+                row["status"] = wrongButAdmitted ? "observed" : "open-gap";
+                row["errorStage"] = wrongButAdmitted ? "semantic-fidelity" : "fixture-verdict";
+                row["errorCategory"] = wrongButAdmitted ? "schema-valid-wrong-action" : "wrong-response-diagnostic-regressed";
+                row["gap"] = wrongButAdmitted
+                    ? "Synthetic Wood/count-2 result differs from declared Gold/count-3/amount-200 request; actual admission is recorded separately."
+                    : "The deliberately wrong fixture was not both schema-valid and admitted as the exact Wood/count-2 action; do not certify a semantic mismatch.";
                 return;
             }
             if (id == "cancel-reset-late-retry")
             {
                 Mark(oneCall && noEffect && fixture.Chat.PendingClarification == null
                     && fixture.Chat.PendingActionPlan == null && LatestUiState(fixture.Chat) == "Ready",
                     "local-continuation", "stale-proposal-effect");
                 return;
             }
             if (id == "count-correction-only" || id == "amount-correction-only")
@@ -1321,33 +1541,38 @@ namespace OpenEmpires.Tests
                     && !states.Contains("CommanderCapabilityGoal:Blocked") && !states.Contains("CommanderCapabilityGoal:Failed"))
                 { row["status"] = "open-gap"; row["errorStage"] = "native-discovery";
                   row["errorCategory"] = "finite-outcome-not-reached"; row["gap"] = states; }
                 return;
             }
             if (id == "ordered-production-exact-new-units")
             {
                 var producer = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().SingleOrDefault();
                 var patrol = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                     .SingleOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.PatrolArea);
+                var resultIds = producer?.ResultUnitIds ?? Array.Empty<int>();
+                CaptureNativePatrolFacts(fixture, row, resultIds);
                 Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                     && (bool?)row["approved"] == true && (bool?)row["previewMentionsOwnedBase"] == true
                     && fixture.HumanTrainCommands == 1 && fixture.HumanBirthId >= 0
                     && producer?.Status == CommanderGoalStatus.Completed && patrol != null
                     && (int?)row["newResultCount"] == 2
                     && (bool?)row["oldUnitExcluded"] == true
                     && (bool?)row["humanBirthExcluded"] == true
                     && (bool?)row["exactPatrolBinding"] == true
+                    && (int?)row["nativePatrolActorCount"] == 2
+                    && (bool?)row["nativePatrolWaypointsMatch"] == true
+                    && (bool?)row["nativePatrolDestinationMatch"] == true
                     && hasCommand("TrainUnitCommand") && hasCommand("PatrolCommand")
                     && hasNative("PatrolCommand:processed"),
                     "native-result-binding", "exact-new-patrol-chain-not-observed", terminal: false);
                 if ((string)row["fidelity"] != "matched")
-                    row["gap"] = "Check ordinary human queue, exactly two Commander-attributed newborn results, owned-base preview, and native patrol subjects independently.";
+                    row["gap"] = "Check ordinary human queue, exactly two Commander-attributed newborn results, owned-base preview, exact native patrol subjects, both persistent routes and current destinations independently.";
                 return;
             }
             if (id == "idle-only-protection")
             {
                 Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                     && fixture.Manager.Goals.OfType<BuildStructureGoal>().Any(goal => goal.UseIdleWorkersOnly)
                     && hasNative("PlaceBuildingCommand:processed"),
                     "native-construction", "idle-only-selector-or-placement-missing");
                 return;
             }
```
## Assets\Tests\EditMode\CommanderEconomyClarificationTests.Phase5CCorpus.cs.meta
Before SHA256:3251487A38630DA404951D2E260E2D7286D67CBF8B5213A83FBDE90227520660
After SHA256:3251487A38630DA404951D2E260E2D7286D67CBF8B5213A83FBDE90227520660
No delta.
## Assets\Tests\EditMode\CommanderGrandFixSpatialTruthTests.cs
Before SHA256:165DA9B481C15C8452CC109B0B3E786A1385FF726E4EAEDEC1329ABFB7461E88
After SHA256:26C9BF275C577F8606A573B68A33CEDAE31488F8EABA00FC97B047B400294533
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderGrandFixSpatialTruthTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderGrandFixSpatialTruthTests.cs"
index c2699a3..e1262ef 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderGrandFixSpatialTruthTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderGrandFixSpatialTruthTests.cs"	
@@ -46,20 +46,23 @@ namespace OpenEmpires.Tests
         }
         [Test]public void ExplicitLegacyFour_IsNotEquivalentToOmittedPointSemantics()
         {
             var method=typeof(CommanderIntent).Assembly.GetType("OpenEmpires.CommanderScopeEquivalence").GetMethod("SameIntent",BindingFlags.Static|BindingFlags.NonPublic);
             Assert.That(method.Invoke(null,new object[]{Point(CommanderCapabilityActionType.PatrolArea),Radius(CommanderCapabilityActionType.PatrolArea,4)}),Is.False);
         }
         [Test]public void UnsupportedRadius_CannotDisappearDuringDtoProjection()
         { Assert.Throws<ArgumentException>(()=>CommanderIntentDtoCodec.FromIntent(Radius(CommanderCapabilityActionType.PatrolArea,5))); }
         [Test]public void PointPatrol_UsesNormalPatrolCommandWithNoAroundPromise()
         {
+            int x=sim.MapData.Width/2,z=sim.MapData.Height/2;
+            for(int tx=x-8;tx<=x+8;tx++)for(int tz=z-8;tz<=z+8;tz++)
+                sim.FogOfWar.SetVisible(0,tx,tz);
             Assert.That(new CommanderCapabilityExecutor(sim).TryCreateCommand(Point(CommanderCapabilityActionType.PatrolArea),out var command,out var reason),Is.True);
             Assert.That(command,Is.TypeOf<PatrolCommand>());Assert.That(reason.ToLowerInvariant(),Does.Not.Contain("around"));
         }
         [TestCase("radius")][TestCase("radiusTiles")][TestCase("perimeter")]
         public void ProviderExplicitSpatialRestrictions_RejectRatherThanDropUnknownConstraint(string field)
         {
             var result=CommanderSemanticJson.Parse("{\"outcome\":\"Request\",\"nodes\":[{\"type\":\"PatrolArea\",\"unitSelector\":\"Military\",\"count\":1,\"location\":\"PlayerBase\",\""+field+"\":5}]}");
             Assert.That(result.IsValid,Is.False);Assert.That(manager.Goals,Is.Empty);Assert.That(sim.CommandBuffer.FlushCommands(),Is.Empty);
         }
         [Test]public void BuildingFootprintGap_RemainsSeparateFromUnsupportedActionRadius()
```
## Assets\Tests\EditMode\CommanderPhase4GCapabilityTests.cs
Before SHA256:05894F0D8B28E80F5DE6D209E4AC18C2FF7BB5D49884E247DA183A473874FFC1
After SHA256:D8042FFCA37D0AEC35EF4AEAEC36D4EC53CD3B909F0F034C1A8C04664DCEAEE7
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase4GCapabilityTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase4GCapabilityTests.cs"
index 94a6fac..176c1ff 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase4GCapabilityTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase4GCapabilityTests.cs"	
@@ -710,30 +710,37 @@ namespace OpenEmpires.Tests
 
         [Test]
         public void ResultBinding_UsesOnlyTheExactProducedUnitSet()
         {
             var config = ScriptableObject.CreateInstance<SimulationConfig>();
             var simulation = new GameSimulation(config, 1, new[] { 0 }, System.Array.Empty<int>());
             try
             {
                 int x = simulation.MapData.Width / 2;
                 int z = simulation.MapData.Height / 2;
+                for (int tx = x - 12; tx <= x + 12; tx++)
+                    for (int tz = z - 12; tz <= z + 12; tz++)
+                    {
+                        simulation.MapData.Tiles[tx, tz] = TileType.Grass;
+                        simulation.MapData.ForestDensity[tx, tz] = 0f;
+                        simulation.FogOfWar.SetVisible(0, tx, tz);
+                    }
                 simulation.CreateBuilding(0, BuildingType.TownCenter, x, z, false, true);
                 UnitData unrelated = simulation.UnitRegistry.CreateUnit(0,
                     simulation.MapData.TileToWorldFixed(x + 1, z + 1), Fixed32.One,
                     Fixed32.FromFloat(.4f), Fixed32.One);
                 unrelated.UnitType = 2;
                 var produced = new List<int>();
                 for (int i = 0; i < 3; i++)
                 {
                     UnitData spearman = simulation.UnitRegistry.CreateUnit(0,
-                        simulation.MapData.TileToWorldFixed(x + 2 + i, z + 2), Fixed32.One,
+                        simulation.MapData.TileToWorldFixed(x + 2 + i, z + 6), Fixed32.One,
                         Fixed32.FromFloat(.4f), Fixed32.One);
                     spearman.UnitType = 1;
                     spearman.MaxHealth = spearman.CurrentHealth = 100;
                     produced.Add(spearman.Id);
                 }
 
                 var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                     new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 3, 1),
                     new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                 var binding = CommanderResultBinding.ForUnits(produced, 7, 0, simulation);
```
## Assets\Tests\EditMode\CommanderPhase4GHostileAuditTests.cs
Before SHA256:917366229CFFE252226369681BFEE2D8EB888E62AE5765BBEA2ABCE1EA4F07B9
After SHA256:384A064344257F3AA04DC3EE0FA7E5E56687E839B916E38AD7FA10F0A51BC2D2
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase4GHostileAuditTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase4GHostileAuditTests.cs"
index 079f5d4..1790f7b 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase4GHostileAuditTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase4GHostileAuditTests.cs"	
@@ -45,20 +45,21 @@ namespace OpenEmpires.Tests
             chat.Initialize(provider, simulation, manager, dispatcher);
             return chat;
         }
 
         private void SetupBaseMap(int cx, int cz)
         {
             for (int tx = cx - 25; tx <= cx + 25; tx++)
                 for (int tz = cz - 25; tz <= cz + 25; tz++)
                 {
                     simulation.MapData.Tiles[tx, tz] = TileType.Grass;
+                    simulation.MapData.ForestDensity[tx, tz] = 0f;
                     simulation.FogOfWar.SetVisible(0, tx, tz);
                 }
             simulation.CreateBuilding(0, BuildingType.TownCenter, cx, cz, false, true);
         }
 
         private string GetLastExplanation(CommanderChatUI chat)
         {
             return chat.Conversation?.Snapshot().LastOrDefault(e => e.Kind == MemoryEntryKind.Explanation)?.Text ?? string.Empty;
         }
 
@@ -835,21 +836,21 @@ namespace OpenEmpires.Tests
         [Test]
         public void Determinism_IdenticalGraphAndState_EmitsIdenticalCommand()
         {
             int cx = simulation.MapData.Width / 2;
             int cz = simulation.MapData.Height / 2;
             SetupBaseMap(cx, cz);
 
             for (int i = 0; i < 5; i++)
             {
                 var u = simulation.UnitRegistry.CreateUnit(0,
-                    simulation.MapData.TileToWorldFixed(cx + i, cz + 3), Fixed32.One,
+                    simulation.MapData.TileToWorldFixed(cx + i, cz + 6), Fixed32.One,
                     Fixed32.FromFloat(.4f), Fixed32.One);
                 u.UnitType = 1;
                 u.CurrentHealth = u.MaxHealth = 100;
             }
 
             var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                 new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 2, 1),
                 new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
 
             var executor1 = new CommanderCapabilityExecutor(simulation);
```
## Assets\Tests\EditMode\CommanderPhase5CScenarioArtifactTests.cs
Before SHA256:3662572D16C453B2D54FC3189948264892ED431D7FE8DEA654F38AEC1D5FA524
After SHA256:CB83AFA56E226F3C98FAF761F867C1F77CD1CCB646DEFC12A1BE4B5B5F972857
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5CScenarioArtifactTests.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5CScenarioArtifactTests.cs"
index a926ed8..ace4eb6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\EditMode\\CommanderPhase5CScenarioArtifactTests.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\EditMode\\CommanderPhase5CScenarioArtifactTests.cs"	
@@ -33,12 +33,37 @@ namespace OpenEmpires.Tests
                 unsafeRecord["entityId"] = 42;
                 Assert.That(() => write.Invoke(recorder, new object[] { unsafeRecord }),
                     Throws.TypeOf<TargetInvocationException>());
                 Assert.That(Directory.GetFiles(directory, "*.json").Length, Is.EqualTo(1));
             }
             finally
             {
                 if (Directory.Exists(directory)) Directory.Delete(directory, true);
             }
         }
+
+        [Test]
+        public void PerTurnArtifact_AllowsDistinctObservationTurnWithNoExecutionTicket()
+        {
+            string directory = Path.Combine(Path.GetTempPath(), "phase5c-observation-" + Guid.NewGuid().ToString("N"));
+            try
+            {
+                string path = Path.Combine(directory, "causal-information-owned-task-facts.json");
+                var recorder = new OpenEmpires.TestSupport.ScenarioEvidenceRecorder(path, Guid.NewGuid().ToString("D"));
+                recorder.RecordPhase5CTurn(new JObject
+                {
+                    ["schemaVersion"] = 1, ["caseId"] = "causal-information-owned-task-facts",
+                    ["requestId"] = JValue.CreateNull(), ["turnCorrelationId"] = "ui-turn:1",
+                    ["utterance"] = "Why is my troop production stuck?", ["status"] = "observed"
+                });
+                var saved = JObject.Parse(File.ReadAllText(path));
+                Assert.That(saved["requestId"].Type, Is.EqualTo(JTokenType.Null));
+                Assert.That((string)saved["turnCorrelationId"], Is.EqualTo("ui-turn:1"));
+                Assert.That((string)saved["caseId"], Is.EqualTo("causal-information-owned-task-facts"));
+            }
+            finally
+            {
+                if (Directory.Exists(directory)) Directory.Delete(directory, true);
+            }
+        }
     }
 }
```
## Assets\Tests\EditMode\CommanderPhase5CScenarioArtifactTests.cs.meta
Before SHA256:A5A54BB2A06405947A5364B1E499957078BC2496ABCF393DDEB3487C4DB52CE4
After SHA256:A5A54BB2A06405947A5364B1E499957078BC2496ABCF393DDEB3487C4DB52CE4
No delta.
## Assets\Tests\PlayMode\CommanderPhase5CUIPlayModeTests.cs
Before SHA256:896DA4229E66A75810AD835FD434CC906D97CB5D56576F35D8EB4FDFE695770F
After SHA256:896DA4229E66A75810AD835FD434CC906D97CB5D56576F35D8EB4FDFE695770F
No delta.
## Assets\Tests\Shared\ScenarioEvidenceRecorder.cs
Before SHA256:69F5E6B4418D53AC618C8E2FE7BEE60FBC3EE60699C2188C0A8899D0F254C6FC
After SHA256:BA20AAD736DEB0DACFCAE0B42AA81C175F302E56DF7D253A960707A9372D85B9
```diff
diff --git "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs" "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs"
index 3065cd6..27833b6 100644
--- "a/D:\\unity_projects\\OpenEmpires\\Open Empires\\Docs\\CommanderPhase5C\\evidence\\task-6-fix1-baseline\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs"	
+++ "b/D:\\unity_projects\\OpenEmpires\\Open Empires\\Assets\\Tests\\Shared\\ScenarioEvidenceRecorder.cs"	
@@ -4,33 +4,34 @@ using System.Linq;
 using System.Text;
 using System.Text.RegularExpressions;
 using Newtonsoft.Json;
 using Newtonsoft.Json.Linq;
 
 namespace OpenEmpires.TestSupport
 {
     public sealed class ScenarioEvidenceRecorder
     {
         private static readonly string[] Phase5CTurnFields = {
-            "schemaVersion", "caseId", "requestId", "utterance", "turnText", "turnIndex", "status", "evidenceMode", "uiState",
+            "schemaVersion", "caseId", "requestId", "turnCorrelationId", "utterance", "turnText", "turnIndex", "status", "evidenceMode", "uiState",
             "fixtureKind", "semanticValid", "semanticOutcome", "fidelity", "normalizedFields", "admittedFields", "turnHistory",
             "draftBefore", "draftAfter", "draftChanges", "approvalRequired", "approved", "approvalMode",
             "providerCalls", "httpAttempts", "packAccepted", "commandCount", "commandTypes", "nativeResults",
             "taskStates", "estimatedInputTokens", "actualPromptTokens", "actualCompletionTokens",
             "actualCachedTokens", "actualReasoningTokens", "reportedCost", "attemptCosts",
             "uiSubmitMs", "packingMs", "transportMs", "parseMs", "admissionMs",
             "firstNativeActionMs", "fogNewlyExploredCount", "garrisonHousedCount", "garrisonProtectedCount",
             "garrisonUnaccommodatedCount", "futureObservedCount", "futureAssignedCount", "humanTrainCommands",
             "resourceProgress", "gatheredFoodIncome", "foodCompletionTick", "foodIncomeAtCompletion",
             "commanderCommandsAtFoodCompletion", "ordinaryFoodGatherActive",
             "fixtureAssumption", "previewMentionsOwnedBase", "newResultCount", "oldUnitExcluded",
-            "humanBirthExcluded", "exactPatrolBinding",
+            "humanBirthExcluded", "exactPatrolBinding", "nativePatrolActorCount",
+            "nativePatrolWaypointsMatch", "nativePatrolDestinationMatch",
             "builderAffinityVerified", "assignedBuilderHumanProtected",
             "ownedTaskFactCount", "readOnlyProviderRequest", "questionFactClass",
             "errorStage", "errorCategory", "gap", "tickMode", "ticks"
         };
         private readonly string path,run;
         private string lastStage="setup",firstFailure,firstCategory;
         private JObject diagnostics=new JObject{["initial_http_attempts"]="unavailable",["repair_http_attempts"]="unavailable",["http_status"]="unavailable",["finish_category"]="unavailable",["attribution"]="unavailable"};
         public void SetDiagnostics(int initial,int repair,int status,string finish,int[] goals,string[] commands,int[] buildings,int[] units)
         {
             if(initial<0||initial>6||repair<0||repair>6||initial+repair>6||status<0||status>599
@@ -69,21 +70,23 @@ namespace OpenEmpires.TestSupport
         // A distinct bounded per-turn projection. The caller supplies only detached, allow-listed
         // observations; the recorder never serializes provider envelopes or simulation objects.
         public void RecordPhase5CTurn(JObject safeTurn)
         {
             if(safeTurn==null || safeTurn.Count>Phase5CTurnFields.Length)
                 throw new InvalidOperationException("Invalid Phase 5C turn record.");
             foreach(var field in safeTurn.Properties())
                 if(!Phase5CTurnFields.Contains(field.Name) || !SafePhase5CValue(field.Value,0))
                     throw new InvalidOperationException("Unsafe Phase 5C turn field.");
             if((int?)safeTurn["schemaVersion"]!=1 || !SafeSlug((string)safeTurn["caseId"])
-                || !SafeRequestId((string)safeTurn["requestId"])
+                || (safeTurn["requestId"]?.Type!=JTokenType.Null && !SafeRequestId((string)safeTurn["requestId"]))
+                || (safeTurn["turnCorrelationId"]?.Type!=JTokenType.Null
+                    && safeTurn["turnCorrelationId"]!=null && !SafeRequestId((string)safeTurn["turnCorrelationId"]))
                 || safeTurn["utterance"]?.Type!=JTokenType.String
                 || ((string)safeTurn["utterance"]).Length>1500
                 || !new[]{"started","observed","failed","open-gap"}.Contains((string)safeTurn["status"]))
                 throw new InvalidOperationException("Invalid Phase 5C turn identity or state.");
             string stage=(string)safeTurn["errorStage"];
             if(!string.IsNullOrEmpty(stage) && firstFailure==null)
             {firstFailure=stage;firstCategory=(string)safeTurn["errorCategory"] ?? "unclassified";}
             var root=new JObject { ["schema"]="openempires-phase5c-scenario-turn@1",["run_id"]=run,
                 ["first_failing_stage"]=firstFailure,["first_failure_category"]=firstCategory };
             foreach(var field in safeTurn.Properties())root[field.Name]=field.Value.DeepClone();
```
## Docs\CommanderPhase5C\evidence\phase5c-replay-corpus.v1.json
Before SHA256:41FEAF3C22FF04384194B3D529781B215979A65CD66ED5FC32DE563F371E03F2
After SHA256:41FEAF3C22FF04384194B3D529781B215979A65CD66ED5FC32DE563F371E03F2
No delta.
