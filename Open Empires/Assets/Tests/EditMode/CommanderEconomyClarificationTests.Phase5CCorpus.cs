using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenEmpires.TestSupport;
using UnityEngine;

namespace OpenEmpires.Tests
{
    public sealed partial class CommanderEconomyClarificationTests
    {
        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCorrection_TypedCountsPreserveOtherSlotAndUseNoSecondCall()
        {
            foreach (string id in new[] { "count-correction-only", "amount-correction-only" })
            {
                Assert.That(TryResponse(id, out string response), Is.True);
                using (var fixture = new CorpusFixture(response))
                {
                    fixture.Seed(id);
                    await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                    Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                    string correction = id == "count-correction-only"
                        ? "Make that 4 villagers." : "Change the amount to 250.";
                    var local = fixture.Chat.PendingClarification.GraphDraft.Resolve(correction);
                    Assert.That(local?.PendingGraphDraft, Is.Not.Null, id + " local correction resolution");
                    await fixture.Chat.SubmitMessageAsync(correction);
                    Assert.That(fixture.Provider.Calls, Is.EqualTo(1), id);
                    var pending = fixture.Chat.PendingClarification?.GraphDraft;
                    Assert.That(pending, Is.Not.Null, id);
                    var node = (JObject)((JArray)pending.ToPendingJson()["nodes"])[0];
                    Assert.That((int)node["count"], Is.EqualTo(id == "count-correction-only" ? 4 : 3), id);
                    Assert.That((int)node["resourceAmount"], Is.EqualTo(id == "amount-correction-only" ? 250 : 200), id);
                    Assert.That((string)node["resourceAmountMode"], Is.EqualTo("Stockpile"), id);
                }
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusCancelReset_NegativeControlAndResetKeepLateYesInert()
        {
            Assert.That(TryResponse("cancel-reset-late-retry", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("cancel-reset-late-retry");
                await fixture.Chat.SubmitMessageAsync("Build a House by the visible gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No, cancel that proposal.");
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Cancelled"));
                await fixture.Chat.SubmitMessageAsync("Reset Commander.");
                Assert.That(LatestUiState(fixture.Chat), Is.EqualTo("Ready"));
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_NoPeriodRejectsOnlyRetainedProposal()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string response), Is.True);
            using (var fixture = new CorpusFixture(response))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a gold mine near gold.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("No.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals, Is.Empty);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusPunctuation_ResolvesRetainedGraphWithoutAnotherProviderCall()
        {
            Assert.That(TryResponse("mine-yes-original-request", out string mineResponse), Is.True);
            using (var fixture = new CorpusFixture(mineResponse))
            {
                fixture.Seed("mine-yes-original-request");
                await fixture.Chat.SubmitMessageAsync("Build a Mine near a visible gold deposit.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                var locallyResolved = fixture.Chat.PendingClarification.GraphDraft.Resolve("Yes.");
                Assert.That(locallyResolved?.IsValid, Is.True);
                Assert.That(fixture.Economy.Manager.TryCompileActionPlan(locallyResolved, out _, out string reason),
                    Is.True, reason);
                await fixture.Chat.SubmitMessageAsync("Yes.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Economy.Manager.Goals.OfType<BuildStructureGoal>()
                    .Any(goal => goal.StructureType == BuildingType.Mine), Is.True);
            }
            Assert.That(TryResponse("house-next-five-from-trees", out string houseResponse), Is.True);
            using (var fixture = new CorpusFixture(houseResponse))
            {
                fixture.Seed("house-next-five-from-trees");
                await fixture.Chat.SubmitMessageAsync("Build a House and have the next five villagers gather wood from trees.");
                Assert.That(fixture.Chat.PendingClarification?.GraphDraft, Is.Not.Null);
                await fixture.Chat.SubmitMessageAsync("From trees.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(fixture.Chat.PendingClarification, Is.Null);
                Assert.That(fixture.Chat.PendingActionPlan, Is.Not.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusDiscovery_TravelRevealsRequestedHostileAndStops()
        {
            Assert.That(TryResponse("finite-discovery-distinct-from-point-scout", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "finite-discovery-distinct-from-point-scout"))
            {
                Assert.That(fixture.Discovery.TargetInitiallyHidden, Is.True);
                Assert.That(fixture.Discovery.NoOrderControlStable, Is.True,
                    "Native fog bootstrap plus idle/no-order ticks must not reveal the hostile target.");
                await fixture.Chat.SubmitMessageAsync("Find the enemy Town Center with my Scout.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                fixture.TickBounded(1500);
                Assert.That(fixture.Commands, Does.Contain("MoveCommand"));
                Assert.That(fixture.Native.Any(entry => entry == "Move:Complete"), Is.True);
                Assert.That(fixture.Discovery.TravelRevealedCount(), Is.GreaterThan(0));
                Assert.That(fixture.Discovery.TargetEverVisibleAfterTravel, Is.True);
                Assert.That(fixture.Manager.Goals.Single().Status, Is.EqualTo(CommanderGoalStatus.Completed));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusIdleOnlyFixture_ParsesStrictBuilderSelection()
        {
            Assert.That(TryResponse("idle-only-protection", out string response), Is.True);
            var parsed = CommanderSemanticJson.ParseProviderResponse(response);
            Assert.That(parsed.IsValid, Is.True, parsed.SafeExplanation);
            Assert.That(parsed.Nodes.Single().Constraints.OfType<PreferredWorkersConstraint>().Count(), Is.EqualTo(1));
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusOrderedHistoricalSynthetic_RejectsAtStrictParseBoundary()
        {
            var oldResponse = Request("{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"WorkedResource\",\"resource\":\"Gold\",\"dependsOn\":[0],\"resultFromNode\":0}");
            var parsed = CommanderSemanticJson.ParseProviderResponse(oldResponse);
            Assert.That(parsed.IsValid, Is.False);
            Assert.That(parsed.SafeExplanation, Is.Not.Empty);
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusAssignedBuilder_NativeDeathIsNotConfusedWithHumanTakeover()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "death-versus-takeover"))
            {
                fixture.Seed("death-versus-takeover", deleteAssignedBuilder: true);
                Assert.That(fixture.BuilderAffinityVerified, Is.True);
                Assert.That(fixture.AssignedBuilderMissing, Is.True,
                    "The native delete must remove the already-attributed builder before the question.");
                int before = fixture.Commands.Count;
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.EqualTo("dead-or-missing"));
                Assert.That(fixture.Commands.Count, Is.EqualTo(before));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusUnattributedWorker_HumanStopDoesNotBecomeAnAssignedBuilderFact()
        {
            Assert.That(TryResponse("death-versus-takeover", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "unattributed-builder-control"))
            {
                fixture.Seed("unattributed-builder-control");
                Assert.That(fixture.BuilderAffinityVerified, Is.False);
                Assert.That(fixture.Native, Does.Not.Contain("PlaceBuildingCommand:processed"));
                await fixture.Chat.SubmitMessageAsync("What happened to the assigned builders?");
                Assert.That(QuestionFactClass(fixture.Provider.LastRequest), Is.Not.EqualTo("human-override"));
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusMalformedSubmission_RetainsRealTicketInsteadOfCaseLabel()
        {
            Assert.That(TryResponse("malformed-provider-json", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "malformed-provider-json"))
            {
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                Assert.That(fixture.Provider.Calls, Is.EqualTo(1));
                Assert.That(RequestCorrelation(fixture, "malformed-provider-json"), Does.StartWith("runtime:"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusReadOnlySubmission_HasNoExecutableTicketOrCaseLabel()
        {
            Assert.That(TryResponse("causal-information-owned-task-facts", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "causal-information-owned-task-facts"))
            {
                fixture.Seed("causal-information-owned-task-facts");
                await fixture.Chat.SubmitMessageAsync("Why is my troop production stuck?");
                Assert.That(fixture.Provider.LastRequest.IsReadOnlyQuestion, Is.True);
                Assert.That(RequestCorrelation(fixture, "causal-information-owned-task-facts"), Is.Null);
                Assert.That((string)NewTurn("causal-information-owned-task-facts", "Why is my troop production stuck?")["requestId"], Is.Null);
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusMalformedVerdict_AcceptedResponseCannotRemainObservedRejection()
        {
            string accepted = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
            using (var fixture = new CorpusFixture(accepted, "malformed-provider-json"))
            {
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                var row = NewTurn("malformed-provider-json", "Gather 200 gold with 3 villagers.");
                row["status"] = "observed";
                EvaluateFixture("malformed-provider-json", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [TestCase("{not-json")]
        [TestCase("faithful")]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusWrongVerdict_NonWrongResponseCannotRemainCertifiedWrong(string variant)
        {
            string response = variant == "faithful"
                ? Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile")) : variant;
            using (var fixture = new CorpusFixture(response, "schema-valid-wrong-provider-result"))
            {
                fixture.Seed("schema-valid-wrong-provider-result");
                await fixture.Chat.SubmitMessageAsync("Gather 200 gold with 3 villagers.");
                var row = NewTurn("schema-valid-wrong-provider-result", "Gather 200 gold with 3 villagers.");
                row["status"] = "observed";
                EvaluateFixture("schema-valid-wrong-provider-result", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusOrderedVerdict_ProcessedCallbackWithoutPatrollingActorIsGap()
        {
            Assert.That(TryResponse("ordered-production-exact-new-units", out string response), Is.True);
            using (var fixture = new CorpusFixture(response, "ordered-production-exact-new-units"))
            {
                fixture.Seed("ordered-production-exact-new-units");
                await fixture.Chat.SubmitMessageAsync("Train two spearmen, then send those new spearmen to patrol.");
                bool previewAtBase = fixture.Chat.PendingActionPlan?.Preview?
                    .IndexOf("base", StringComparison.OrdinalIgnoreCase) >= 0;
                await fixture.Chat.SubmitMessageAsync("Approve plan");
                fixture.TickBounded(Math.Min(8000, 4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000));
                var result = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>().Single().ResultUnitIds;
                Assert.That(result.Count, Is.EqualTo(2));
                var actor = fixture.Simulation.UnitRegistry.GetUnit(result[0]);
                Assert.That(actor.IsPatrolling, Is.True, "The adversarial control starts from real native patrol state.");
                actor.ClearPatrol(); // replay a native skip/loss despite a processed callback
                var row = NewTurn("ordered-production-exact-new-units", "Train two spearmen, then send those new spearmen to patrol.");
                row["status"] = "observed"; row["approved"] = true;
                row["previewMentionsOwnedBase"] = previewAtBase;
                row["newResultCount"] = 2; row["oldUnitExcluded"] = true;
                row["humanBirthExcluded"] = true; row["exactPatrolBinding"] = true;
                EvaluateFixture("ordered-production-exact-new-units", fixture, row);
                Assert.That((string)row["status"], Is.EqualTo("open-gap"));
                Assert.That((string)row["fidelity"], Is.EqualTo("mismatch"));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public void CorpusPatrolPoint_StaysBesideOwnedBaseAndBlocksWithoutAnyReachablePoint()
        {
            using (var fixture = new CorpusFixture("{not-json", "ordered-production-exact-new-units"))
            {
                fixture.Seed("ordered-production-exact-new-units");
                fixture.TickBounded(1); // native fog bootstrap from the owned TC
                var tc = fixture.Simulation.BuildingRegistry.GetAllBuildings()
                    .Single(building => building.PlayerId == 0
                        && fixture.Simulation.GetEffectiveBuildingType(building) == BuildingType.TownCenter);
                var intent = new CapabilityActionIntent(0, CommanderCapabilityActionType.PatrolArea,
                    new CommanderUnitSelector(CommanderUnitSelectorKind.UnitType, 1, 1),
                    new CommanderLocationSelector(CommanderLocationSelectorKind.PlayerBase));
                var executor = new CommanderCapabilityExecutor(fixture.Simulation);
                Assert.That(executor.TryCreateCommand(intent, out ICommand command, out string reason), Is.True, reason);
                var target = fixture.Simulation.MapData.WorldToTile(((PatrolCommand)command).TargetPosition);
                Assert.That(target.x < tc.OriginTileX || target.x >= tc.OriginTileX + tc.TileFootprintWidth
                    || target.y < tc.OriginTileZ || target.y >= tc.OriginTileZ + tc.TileFootprintHeight, Is.True);
                Assert.That(fixture.Simulation.MapData.IsWalkable(target.x, target.y, 0,
                    fixture.Simulation.BuildingRegistry), Is.True);

                // Remove every candidate inside the documented two-tile neighborhood;
                // the executor cannot widen the search or enqueue a prefix command.
                for (int x = tc.OriginTileX - 2; x < tc.OriginTileX + tc.TileFootprintWidth + 2; x++)
                    for (int z = tc.OriginTileZ - 2; z < tc.OriginTileZ + tc.TileFootprintHeight + 2; z++)
                        if (x < tc.OriginTileX || x >= tc.OriginTileX + tc.TileFootprintWidth
                            || z < tc.OriginTileZ || z >= tc.OriginTileZ + tc.TileFootprintHeight)
                            fixture.Simulation.MapData.Tiles[x, z] = TileType.Water;
                Assert.That(executor.TryCreateCommand(intent, out command, out reason), Is.False);
                Assert.That(command, Is.Null);
                Assert.That(reason, Does.Contain("complete route"));
                Assert.That(fixture.Simulation.CommandBuffer.FlushCommands(), Is.Empty);
            }
        }

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
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().Single();
                Assert.That(goal.Status, Is.EqualTo(CommanderGoalStatus.Blocked));
                Assert.That(goal.GarrisonShelter.GarrisonCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonProtectedCount, Is.EqualTo(1));
                Assert.That(goal.GarrisonUnaccommodatedCount, Is.EqualTo(1));
            }
        }

        [Test]
        [Category("CommanderPhase5CCorpus")]
        public async Task CorpusReplay_UsesProductionChatBoundaryAndPersistsAllDeclaredCases()
        {
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
            var declared = (JArray)JObject.Parse(File.ReadAllText(corpus))["cases"];
            Assert.That(declared.Count, Is.EqualTo(25));
            int declaredTurns = 0, replayedDeclaredTurns = 0;
            foreach (JObject specification in declared)
            {
                string caseId = (string)specification["id"];
                int count = ((JArray)specification["turns"]).Count;
                JObject record = JObject.Parse(File.ReadAllText(Path.Combine(directory, caseId + ".json")));
                int replayed = ((JArray)record["turnHistory"]).Count(entry =>
                    (string)entry["origin"] != "offline-fixture-control");
                Assert.That(replayed, Is.EqualTo(count), caseId + " declared turns");
                declaredTurns += count; replayedDeclaredTurns += replayed;
            }
            Assert.That(declaredTurns, Is.EqualTo(35));
            Assert.That(replayedDeclaredTurns, Is.EqualTo(35));
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
            JObject readOnly = JObject.Parse(File.ReadAllText(Path.Combine(directory, "causal-information-owned-task-facts.json")));
            Assert.That((string)mine["status"], Is.EqualTo("observed"));
            Assert.That((string)mine["fidelity"], Is.EqualTo("matched"));
            Assert.That((int)mine["providerCalls"], Is.EqualTo(1));
            Assert.That((string)house["status"], Is.EqualTo("observed"));
            Assert.That((string)house["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)house["futureObservedCount"], Is.EqualTo(5));
            Assert.That((int?)house["futureAssignedCount"], Is.EqualTo(5));
            Assert.That((int)house["providerCalls"], Is.EqualTo(1));
            Assert.That((string)malformed["errorStage"], Is.EqualTo("parse"));
            Assert.That((string)malformed["status"], Is.EqualTo("observed"));
            Assert.That((string)malformed["fidelity"], Is.EqualTo("not-evaluable"));
            Assert.That((bool?)malformed["semanticValid"], Is.False);
            Assert.That((string)malformed["errorCategory"], Is.EqualTo("malformed-rejected"));
            Assert.That((string)malformed["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)malformed["turnCorrelationId"], Does.StartWith("ui-turn:"));
            Assert.That((int)malformed["providerCalls"], Is.EqualTo(1));
            Assert.That((int)malformed["commandCount"], Is.Zero);
            Assert.That(((JArray)malformed["admittedFields"]).Count, Is.Zero);
            Assert.That((string)wrong["status"], Is.EqualTo("observed"));
            Assert.That((string)wrong["fidelity"], Is.EqualTo("wrong"));
            Assert.That((bool?)wrong["semanticValid"], Is.True);
            Assert.That((string)wrong["errorCategory"], Is.EqualTo("schema-valid-wrong-action"));
            Assert.That(((JArray)wrong["admittedFields"]).Count, Is.EqualTo(1));
            Assert.That((string)wrong["admittedFields"][0]["originalUtterance"],
                Is.EqualTo("Gather 200 gold with 3 villagers."));
            Assert.That((string)wrong["admittedFields"][0]["destinationResource"], Is.EqualTo("Wood"));
            Assert.That((int)wrong["admittedFields"][0]["count"], Is.EqualTo(2));
            Assert.That((string)readOnly["requestId"], Is.Null);
            Assert.That((string)readOnly["turnCorrelationId"], Does.StartWith("ui-turn:"));
            Assert.That((int)readOnly["commandCount"], Is.Zero);
            Assert.That((bool?)ordered["semanticValid"], Is.True);
            Assert.That((string)ordered["status"], Is.EqualTo("observed"));
            Assert.That((string)ordered["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)ordered["newResultCount"], Is.EqualTo(2));
            Assert.That((bool?)ordered["oldUnitExcluded"], Is.True);
            Assert.That((bool?)ordered["humanBirthExcluded"], Is.True);
            Assert.That((bool?)ordered["exactPatrolBinding"], Is.True);
            Assert.That((int?)ordered["nativePatrolActorCount"], Is.EqualTo(2));
            Assert.That((bool?)ordered["nativePatrolWaypointsMatch"], Is.True);
            Assert.That((bool?)ordered["nativePatrolDestinationMatch"], Is.True);
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
            Assert.That((string)additionalFood["fidelity"], Is.EqualTo("matched"));
            Assert.That((int?)additionalFood["resourceProgress"], Is.EqualTo(400));
            Assert.That((long?)additionalFood["gatheredFoodIncome"], Is.GreaterThan(400));
            Assert.That((bool?)additionalFood["ordinaryFoodGatherActive"], Is.True);
            Assert.That((string)countCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)amountCorrection["requestId"], Does.StartWith("runtime:"));
            Assert.That((string)cancelled["uiState"], Is.EqualTo("Ready"));
            Assert.That(((JArray)cancelled["normalizedFields"]), Is.Empty);
            Assert.That((string)cancelled["requestId"], Does.StartWith("runtime:"));
            Assert.That((bool?)mine["packAccepted"], Is.True);
            Assert.That((int?)mine["estimatedInputTokens"], Is.GreaterThan(0));
            Assert.That((int?)garrison["garrisonHousedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonProtectedCount"], Is.EqualTo(1));
            Assert.That((int?)garrison["garrisonUnaccommodatedCount"], Is.EqualTo(1));
        }

        // The provider is substituted only at the external semantic boundary. Every turn still
        // goes through the production CommanderChatUI and its normal admission/goal path.
        private sealed class CorpusProvider : ICommanderAIProvider, ICommanderSemanticProvider
        {
            private readonly Queue<string> responses;
            internal int Calls;
            internal CommanderSemanticResult LastResult;
            internal CommanderSemanticProviderRequest LastRequest;
            internal long FirstCallTicks;
            internal long LastReturnTicks;
            internal double? ParseMs;
            internal double? PackMs;
            internal int? InputEstimate;
            internal bool PackSucceeded;
            internal CorpusProvider(string response) => responses = new Queue<string>(new[] { response });
            public Task<CommanderSemanticResult> TranslateSemanticAsync(CommanderSemanticProviderRequest request,
                CancellationToken token)
            {
                if (FirstCallTicks == 0) FirstCallTicks = Stopwatch.GetTimestamp();
                LastRequest = request;
                Calls++;
                if (responses.Count == 0) throw new InvalidOperationException("Unexpected second fixture translation.");
                long packStart = Stopwatch.GetTimestamp();
                PackSucceeded = CommanderSemanticRequestPack.TryBuild(request, "openai/gpt-6-luna", 4096,
                    out var pack, out _);
                PackMs = Milliseconds(packStart, Stopwatch.GetTimestamp());
                InputEstimate = pack?.EstimatedInputTokens;
                long started = Stopwatch.GetTimestamp();
                LastResult = CommanderSemanticJson.ParseProviderResponse(responses.Dequeue());
                LastReturnTicks = Stopwatch.GetTimestamp();
                ParseMs = Milliseconds(started, LastReturnTicks);
                return Task.FromResult(LastResult);
            }
            public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request, CancellationToken token) =>
                Task.FromResult(CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.ProviderFailure,
                    "Legacy provider path is not part of the corpus."));
        }

        private sealed class CorpusFixture : IDisposable
        {
            internal readonly EconomyFixture Economy = new EconomyFixture();
            internal readonly DiscoveryFixture Discovery;
            internal GameSimulation Simulation => Discovery?.Simulation ?? Economy.Simulation;
            internal CommanderGoalManager Manager => Discovery?.Manager ?? Economy.Manager;
            internal readonly CorpusProvider Provider;
            internal readonly CommanderIntentDispatcher Dispatcher;
            internal readonly CommanderChatUI Chat;
            internal readonly List<string> Commands = new List<string>();
            internal readonly List<string> Native = new List<string>();
            internal long FirstNativeTicks;
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
            internal FixedVector3? PatrolTarget;
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
                Provider = new CorpusProvider(response);
                Dispatcher = new CommanderIntentDispatcher(Simulation, Manager);
                host = new GameObject("Phase5CCorpusProductionChat");
                Chat = host.AddComponent<CommanderChatUI>();
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
                {
                    PatrolSubjects = patrol.UnitIds?.ToArray();
                    PatrolTarget = patrol.TargetPosition;
                }
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
            }
            internal void Seed(string id, bool deleteAssignedBuilder = false)
            {
                if (Discovery != null) return;
                if (id == "mine-yes-original-request")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Worker(0); Economy.Node(ResourceType.Gold, 8);
                }
                else if (id == "house-next-five-from-trees")
                {
                    Economy.Worker(0);
                    var producer = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    producer.AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Wood, 25);
                }
                else if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
                {
                    for (int i = 0; i < 8; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Food, 8);
                }
                else if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only" || id == "additional-gathered-food-bounded-objective")
                {
                    for (int i = 0; i < 6; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 14);
                    if (id == "additional-gathered-food-bounded-objective")
                        Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                            Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Node(ResourceType.Food,
                        id == "additional-gathered-food-bounded-objective" ? 8 : 20);
                }
                else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                {
                    var wood = Economy.Node(ResourceType.Wood, 15);
                    Economy.Node(ResourceType.Food, 24);
                    for (int i = 0; i < 7; i++) Economy.Assigned(Economy.Worker(i), wood);
                }
                else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    var oldWood = Economy.Node(ResourceType.Wood, 18);
                    Economy.Simulation.MapData.AddResourceNode(ResourceType.Wood,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x - 14, Economy.Base.y + 4), 10000);
                    for (int i = 0; i < 3; i++) Economy.Assigned(Economy.Worker(i), oldWood);
                }
                else if (id == "idle-only-protection" || id == "two-houses-near-town-center"
                    || id == "cancel-reset-late-retry")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 3; i++) Economy.Worker(i);
                    Economy.Node(ResourceType.Gold, 18);
                }
                else if (id == "occupied-farms-and-capacity")
                {
                    var farmA = Economy.Farm(16); var farmB = Economy.Farm(24);
                    Economy.Farm(32); Economy.Farm(40);
                    Economy.Assigned(Economy.Worker(0), farmA);
                    Economy.Assigned(Economy.Worker(1), farmB);
                    for (int i = 2; i < 6; i++) Economy.Worker(i);
                }
                else if (id == "garrison-capacity-protected-remainder")
                {
                    var center = Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true);
                    center.GarrisonCapacity = 1; center.AutoProduceVillagers = false;
                    var protectedWorker = Economy.Worker(0);
                    for (int i = 1; i < 3; i++) Economy.Worker(i);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new StopCommand(0, new[] { protectedWorker.Id }), CommandEnqueueSource.Human);
                    Economy.Simulation.CommandBuffer.FlushCommands(); // retain the human protection lease
                }
                else if (id == "ordered-production-exact-new-units")
                {
                    Economy.Simulation.SetPlayerAge(0, 3);
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                        Economy.Base.x + 10, Economy.Base.y, false, true);
                    var old = Economy.Simulation.UnitRegistry.CreateUnit(0,
                        Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 9, Economy.Base.y + 7),
                        Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                    old.UnitType = 1; old.MaxHealth = old.CurrentHealth = 100;
                    OldSpearmanId = old.Id;
                }
                else if (id == "defense-arrival-native-receipt")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    for (int i = 0; i < 6; i++)
                    {
                        var unit = Economy.Simulation.UnitRegistry.CreateUnit(0,
                            Economy.Simulation.MapData.TileToWorldFixed(Economy.Base.x + 10 + i, Economy.Base.y + 8),
                            Fixed32.One, Fixed32.FromFloat(.4f), Fixed32.One);
                        unit.UnitType = 1; unit.MaxHealth = unit.CurrentHealth = 100;
                    }
                }
                else if (id == "schema-valid-wrong-provider-result") Economy.Worker(0);
                else if (id == "causal-information-owned-task-facts"
                    || id == "death-versus-takeover" || id == "unattributed-builder-control")
                {
                    Economy.Simulation.CreateBuilding(0, BuildingType.TownCenter,
                        Economy.Base.x, Economy.Base.y, false, true).AutoProduceVillagers = false;
                    if (id == "causal-information-owned-task-facts")
                    {
                        Economy.Simulation.CreateBuilding(0, BuildingType.Barracks,
                            Economy.Base.x + 10, Economy.Base.y, false, true);
                        Economy.Manager.SubmitEnsureUnitCount(1, 4, newProductionCount: 4);
                    }
                    else
                    {
                        Economy.Worker(0);
                        Economy.Manager.SubmitBuildStructure(BuildingType.House, 1);
                    }
                }
                var stock = Economy.Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                if (id == "amount-versus-worker-count" || id == "count-correction-only"
                    || id == "amount-correction-only") stock.Gold = 0;
                if (id == "causal-information-owned-task-facts") stock.Wood = 0;
                if (id == "unattributed-builder-control") stock.Wood = 0;
                if (id == "ordered-production-exact-new-units")
                {
                    var barracks = Economy.Simulation.BuildingRegistry.GetAllBuildings()
                        .Single(building => building.PlayerId == 0 && building.Type == BuildingType.Barracks);
                    Economy.Simulation.CommandBuffer.EnqueueCommand(
                        new TrainUnitCommand(0, barracks.Id, 1), CommandEnqueueSource.Human);
                    HumanTrainCommands++;
                    Economy.Simulation.Tick(); // native acceptance; the ordinary human birth remains queued first
                }
                if (id == "causal-information-owned-task-facts" || id == "death-versus-takeover"
                    || id == "unattributed-builder-control")
                {
                    Economy.Manager.Tick(0);
                    if (id == "death-versus-takeover")
                    {
                        Economy.Simulation.Tick(); // native placement must create the exact owned foundation first
                        var build = Manager.Goals.OfType<BuildStructureGoal>().Single();
                        var assigned = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        BuilderAffinityVerified = build.PlacedBuildingId >= 0
                            && build.AttributedBuildingIds.Contains(build.PlacedBuildingId)
                            && build.LastAcceptedBuilderUnitIds.SequenceEqual(new[] { assigned.Id })
                            && assigned.ConstructionTargetBuildingId == build.PlacedBuildingId
                            && Native.Contains("PlaceBuildingCommand:processed");
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            deleteAssignedBuilder
                                ? (ICommand)new DeleteUnitsCommand(0, new[] { assigned.Id })
                                : new StopCommand(0, new[] { assigned.Id }),
                            CommandEnqueueSource.Human);
                        Economy.Simulation.Tick(); // native human control, not an injected status flag
                        AssignedBuilderHumanProtected = Manager.ObserveHumanProtection(assigned.Id);
                        AssignedBuilderMissing = Economy.Simulation.UnitRegistry.GetUnit(assigned.Id) == null;
                        Economy.Manager.Tick(15);
                    }
                    if (id == "unattributed-builder-control")
                    {
                        var idle = Economy.Simulation.UnitRegistry.GetAllUnits()
                            .Single(unit => unit.PlayerId == 0 && unit.IsVillager);
                        Economy.Simulation.CommandBuffer.EnqueueCommand(
                            new StopCommand(0, new[] { idle.Id }), CommandEnqueueSource.Human);
                        Economy.Simulation.Tick();
                        Economy.Manager.Tick(15);
                    }
                    InitialGoalCount = Manager.Goals.Count;
                    InitialCommanderCommandCount = Commands.Count;
                }
            }
            internal int TickBounded(int bound)
            {
                for (int i = 0; i < bound; i++)
                {
                    if (i % 15 == 0) Manager.Tick(i);
                    if (caseId == "additional-gathered-food-bounded-objective"
                        && FoodCompletionTick == null
                        && Manager.Goals.OfType<AllocateWorkersGoal>()
                            .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                    {
                        FoodCompletionTick = i;
                        FoodIncomeAtCompletion = Simulation.ResourceManager.GetGatheredIncome(0, ResourceType.Food);
                        CommanderCommandsAtFoodCompletion = Commands.Count;
                    }
                    if (caseId == "house-next-five-from-trees")
                    {
                        var townCenter = Simulation.BuildingRegistry.GetAllBuildings()
                            .FirstOrDefault(building => building.PlayerId == 0
                                && building.Type == BuildingType.TownCenter);
                        if (!houseHumanQueueSeeded && Manager.Goals.OfType<BuildStructureGoal>()
                                .Any(goal => goal.Status == CommanderGoalStatus.Completed))
                        {
                            for (int birth = 0; birth < 5; birth++)
                            {
                                Simulation.CommandBuffer.EnqueueCommand(
                                    new TrainUnitCommand(0, townCenter.Id, 0), CommandEnqueueSource.Human);
                                HumanTrainCommands++;
                            }
                            houseHumanQueueSeeded = true;
                        }
                    }
                    Simulation.Tick(); Discovery?.ObserveAfterNativeTick();
                    if (caseId == "ordered-production-exact-new-units" && HumanBirthId < 0)
                    {
                        var newborn = Simulation.UnitRegistry.GetAllUnits()
                            .Where(unit => unit.PlayerId == 0 && unit.UnitType == 1
                                && unit.CurrentHealth > 0 && unit.Id != OldSpearmanId)
                            .OrderBy(unit => unit.Id).FirstOrDefault();
                        if (newborn != null) HumanBirthId = newborn.Id;
                    }
                }
                if (caseId == "additional-gathered-food-bounded-objective")
                    OrdinaryFoodGatherActive = Simulation.UnitRegistry.GetAllUnits().Any(unit =>
                        unit.PlayerId == 0 && unit.IsVillager && unit.CurrentHealth > 0
                        && unit.TargetResourceNodeId >= 0
                        && Simulation.MapData.GetResourceNode(unit.TargetResourceNodeId)?.Type == ResourceType.Food);
                return bound;
            }
            public void Dispose()
            {
                Simulation.CommandBuffer.CommandEnqueued -= OnCommandEnqueued;
                Simulation.NativeMoveProcessed -= OnMoveProcessed;
                Simulation.NativeUnitCommandProcessed -= OnUnitCommandProcessed;
                if (host != null)
                {
                    if (!Application.isPlaying) typeof(CommanderChatUI).GetMethod("OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Chat, null);
                    UnityEngine.Object.DestroyImmediate(host);
                }
                Dispatcher.Dispose(); Discovery?.Dispose(); Economy.Dispose();
            }
        }

        private sealed class DiscoveryFixture : IDisposable
        {
            private readonly SimulationConfig config;
            internal readonly GameSimulation Simulation;
            internal readonly CommanderGoalManager Manager;
            private readonly bool[,] initiallyUnexplored;
            private readonly BuildingData target;
            private readonly UnitData scout;
            private readonly Vector2Int scoutBaseline;
            internal readonly bool TargetInitiallyHidden;
            internal readonly bool NoOrderControlStable;
            internal bool TargetEverVisibleAfterTravel { get; private set; }
            internal DiscoveryFixture()
            {
                config = ScriptableObject.CreateInstance<SimulationConfig>();
                Simulation = new GameSimulation(config, 2, new[] { 0, 1 }, Array.Empty<int>());
                int x = Simulation.MapData.Width / 2, z = Simulation.MapData.Height / 2;
                for (int tx = x - 7; tx <= x + 7; tx++)
                    for (int tz = z - 7; tz <= z + 7; tz++)
                    {
                        Simulation.MapData.Tiles[tx, tz] = TileType.Grass;
                        Simulation.MapData.ForestDensity[tx, tz] = 0;
                        Simulation.MapData.FoundationCount[tx, tz] = 0;
                        Simulation.FogOfWar.SetVisible(0, tx, tz);
                    }
                // Discovery requires an owned Scout, not an owned completed TC. Omitting the TC
                // prevents its radius-20 building vision from substituting for Scout travel.
                scout = Simulation.UnitRegistry.CreateUnit(0, Simulation.MapData.TileToWorldFixed(x - 1, z),
                    Fixed32.One, Fixed32.One, Fixed32.One);
                scout.UnitType = 4; scout.CurrentHealth = scout.MaxHealth = 100; scout.State = UnitState.Idle;
                target = Simulation.CreateBuilding(1, BuildingType.TownCenter, x - 10, z - 9, false, true);
                Simulation.Tick(); // Native owner-vision bootstrap before the observed baseline.
                TargetInitiallyHidden = Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                    == TileVisibility.Unexplored;
                var scoutStart = Simulation.MapData.WorldToTile(scout.SimPosition);
                for (int i = 0; i < 30; i++) Simulation.Tick(); // no Commander/human orders
                var scoutAfterControl = Simulation.MapData.WorldToTile(scout.SimPosition);
                NoOrderControlStable = scoutAfterControl == scoutStart
                    && Simulation.FogOfWar.GetVisibility(0, target.OriginTileX, target.OriginTileZ)
                        == TileVisibility.Unexplored;
                scoutBaseline = scoutAfterControl;
                var stock = Simulation.ResourceManager.GetPlayerResources(0);
                stock.Food = stock.Wood = stock.Gold = stock.Stone = 2000;
                initiallyUnexplored = new bool[Simulation.MapData.Width, Simulation.MapData.Height];
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        initiallyUnexplored[tx, tz] = Simulation.FogOfWar.GetVisibility(0, tx, tz) == TileVisibility.Unexplored;
                Manager = new CommanderGoalManager(Simulation, 0);
            }
            internal int TravelRevealedCount()
            {
                int count = 0;
                for (int tx = 0; tx < Simulation.MapData.Width; tx++)
                    for (int tz = 0; tz < Simulation.MapData.Height; tz++)
                        if (initiallyUnexplored[tx, tz]
                            && Simulation.FogOfWar.GetVisibility(0, tx, tz) != TileVisibility.Unexplored) count++;
                return count;
            }
            internal bool TargetNowVisible => Simulation.FogOfWar.GetVisibility(0, target.OriginTileX,
                target.OriginTileZ) == TileVisibility.Visible;
            internal void ObserveAfterNativeTick()
            {
                if (Simulation.MapData.WorldToTile(scout.SimPosition) != scoutBaseline && TargetNowVisible)
                    TargetEverVisibleAfterTravel = true;
            }
            public void Dispose() { Manager.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
        }

        private static async Task<string> RunPhase5CCorpusAsync(string corpusPath, string directory)
        {
            JObject corpus = JObject.Parse(File.ReadAllText(corpusPath));
            if ((int?)corpus["schemaVersion"] != 1 || (string)corpus["executionState"] != "not-run")
                throw new InvalidOperationException("Unexpected corpus version or execution state.");
            JArray cases = corpus["cases"] as JArray;
            if (cases == null || cases.Count != 25) throw new InvalidOperationException("The bounded corpus must contain 25 cases.");
            string runId = Guid.NewGuid().ToString("D");
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject item in cases)
            {
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
                string correlationId = null;
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
                long started = Stopwatch.GetTimestamp();
                try
                {
                    using (var fixture = new CorpusFixture(response, id))
                    {
                        fixture.Seed(id);
                        row["fixtureKind"] = id == "malformed-provider-json" ? "malformed"
                            : id == "schema-valid-wrong-provider-result" ? "schema-valid-wrong" : "correct-synthetic";
                        for (int i = 0; i < turns.Count; i++)
                        {
                            string turn = (string)turns[i];
                            var before = Missing(fixture.Chat.PendingClarification);
                            var beforeFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            bool awaitingApproval = fixture.Chat.PendingActionPlan != null;
                            row["turnIndex"] = i;
                            row["turnText"] = turn;
                            row["draftBefore"] = before;
                            long turnStart = Stopwatch.GetTimestamp();
                            int callsBefore = fixture.Provider.Calls;
                            CommanderAIChatSubmission submission = await fixture.Chat.SubmitMessageAsync(turn);
                            row["uiSubmitMs"] = Milliseconds(turnStart, Stopwatch.GetTimestamp());
                            row["draftAfter"] = Missing(fixture.Chat.PendingClarification);
                            var afterFields = fixture.Chat.PendingClarification?.GraphDraft == null
                                ? new JArray() : ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            row["draftChanges"] = DraftDelta(before, (JArray)row["draftAfter"], beforeFields, afterFields);
                            row["providerCalls"] = fixture.Provider.Calls;
                            row["packAccepted"] = fixture.Provider.Calls == 0 ? JValue.CreateNull()
                                : (JToken)fixture.Provider.PackSucceeded;
                            CommanderSemanticResult observedSemantic = fixture.Chat.PendingActionPlan?.Interpretation
                                ?? (fixture.Provider.Calls > callsBefore ? fixture.Provider.LastResult : null);
                            row["semanticValid"] = observedSemantic?.IsValid;
                            row["semanticOutcome"] = observedSemantic?.Outcome.ToString()
                                ?? (submission?.Success == true ? "Request" : null);
                            if (observedSemantic != null) row["normalizedFields"] = NormalizeCorpus(observedSemantic);
                            else if (fixture.Chat.PendingClarification?.GraphDraft != null)
                                row["normalizedFields"] = ProjectGraphDraft(fixture.Chat.PendingClarification.GraphDraft);
                            else row["normalizedFields"] = new JArray();
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["ownedTaskFactCount"] = OwnedTaskFactCount(fixture.Provider.LastRequest);
                            row["readOnlyProviderRequest"] = fixture.Provider.LastRequest?.IsReadOnlyQuestion;
                            row["questionFactClass"] = QuestionFactClass(fixture.Provider.LastRequest);
                            if (id == "death-versus-takeover")
                            {
                                row["builderAffinityVerified"] = fixture.BuilderAffinityVerified;
                                row["assignedBuilderHumanProtected"] = fixture.AssignedBuilderHumanProtected;
                            }
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
                            if (observedCorrelation != null) correlationId = observedCorrelation;
                            else if (fixture.Provider.Calls > callsBefore
                                && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true)
                                correlationId = null;
                            row["requestId"] = correlationId == null ? JValue.CreateNull() : (JToken)correlationId;
                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
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
                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
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
                                ["draftBefore"] = before.DeepClone(),
                                ["draftAfter"] = row["draftAfter"].DeepClone(),
                                ["draftChanges"] = row["draftChanges"].DeepClone(),
                                ["approvalRequired"] = row["approvalRequired"].DeepClone(),
                                ["approved"] = row["approved"].DeepClone(),
                                ["commandTypes"] = row["commandTypes"].DeepClone(),
                                ["taskStates"] = row["taskStates"].DeepClone(),
                                ["uiSubmitMs"] = row["uiSubmitMs"].DeepClone(),
                                ["packingMs"] = row["packingMs"].DeepClone(),
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
                            row["requestId"] = correlation == null ? JValue.CreateNull() : (JToken)correlation;
                            row["turnCorrelationId"] = "ui-turn:" + fixture.Chat.LastSubmittedUiTurnId;
                            row["approvalMode"] = "offline-fixture-control-not-declared-player-turn";
                            row["approved"] = fixture.Chat.PendingActionPlan == null
                                && fixture.Manager.Goals.Count > 0;
                            row["admittedFields"] = AdmittedFields(fixture);
                            row["uiState"] = LatestUiState(fixture.Chat);
                            row["taskStates"] = GoalStates(fixture);
                            turnHistory.Add(new JObject { ["index"] = turns.Count, ["text"] = "Approve plan",
                                ["origin"] = "offline-fixture-control", ["requestId"] = correlation,
                                ["turnCorrelationId"] = row["turnCorrelationId"].DeepClone(),
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
                            : id == "two-houses-near-town-center" ? 6000
                            : id == "house-next-five-from-trees" ? Math.Min(8000,
                                fixture.Simulation.Config.HouseConstructionTicks
                                + 5 * fixture.Simulation.Config.VillagerTrainTimeTicks + 4000)
                            : id == "additional-gathered-food-bounded-objective" ? 7000
                            : id == "all-wood-west-tc-explicit-move" ? 1500
                            : id == "defense-arrival-native-receipt" ? 1500
                            : id == "garrison-capacity-protected-remainder" ? 500
                            : id == "finite-discovery-distinct-from-point-scout" ? 1500
                            : id == "ordered-production-exact-new-units" ? Math.Min(8000,
                                4 * fixture.Simulation.Config.SpearmanTrainTimeTicks + 4000) : 60;
                        int ticks = fixture.TickBounded(tickBound);
                        row["tickMode"] = ticks == 0 ? "not-run" : "accelerated-fixture";
                        row["ticks"] = ticks;
                        row["commandCount"] = fixture.Commands.Count;
                        row["commandTypes"] = new JArray(fixture.Commands);
                        row["nativeResults"] = new JArray(fixture.Native);
                        row["taskStates"] = GoalStates(fixture);
                        row["fogNewlyExploredCount"] = fixture.Discovery == null ? JValue.CreateNull()
                            : (JToken)fixture.Discovery.TravelRevealedCount();
                        var garrison = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>()
                            .FirstOrDefault(goal => goal.Action.ActionType == CommanderCapabilityActionType.GarrisonUnits);
                        row["garrisonHousedCount"] = garrison?.GarrisonShelter == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonShelter.GarrisonCount;
                        row["garrisonProtectedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonProtectedCount;
                        row["garrisonUnaccommodatedCount"] = garrison == null ? JValue.CreateNull()
                            : (JToken)garrison.GarrisonUnaccommodatedCount;
                        var future = fixture.Manager.Goals.OfType<WatchFutureUnitsGoal>().FirstOrDefault();
                        row["futureObservedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.ObservedCount;
                        row["futureAssignedCount"] = future == null ? JValue.CreateNull()
                            : (JToken)future.AssignedCount;
                        row["humanTrainCommands"] = id == "house-next-five-from-trees"
                            || id == "ordered-production-exact-new-units"
                            ? (JToken)fixture.HumanTrainCommands : JValue.CreateNull();
                        if (id == "ordered-production-exact-new-units")
                        {
                            var resultIds = fixture.Manager.Goals.OfType<EnsureUnitCountGoal>()
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
                            CaptureNativePatrolFacts(fixture, row, resultIds);
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
                            ? (JToken)fixture.FoodIncomeAtCompletion.Value : JValue.CreateNull();
                        row["commanderCommandsAtFoodCompletion"] = fixture.CommanderCommandsAtFoodCompletion.HasValue
                            ? (JToken)fixture.CommanderCommandsAtFoodCompletion.Value : JValue.CreateNull();
                        row["ordinaryFoodGatherActive"] = id == "additional-gathered-food-bounded-objective"
                            ? (JToken)fixture.OrdinaryFoodGatherActive : JValue.CreateNull();
                        row["firstNativeActionMs"] = fixture.FirstNativeTicks == 0 ? JValue.CreateNull()
                            : (JToken)Milliseconds(started, fixture.FirstNativeTicks);
                        EvaluateFixture(id, fixture, row);
                        evidence.RecordPhase5CTurn(row);
                    }
                }
                catch (Exception error)
                {
                    row["status"] = "failed";
                    row["errorStage"] = "fixture-runner";
                    row["errorCategory"] = error.GetType().Name;
                    row["gap"] = "First integrated fixture failure; inspect focused NUnit output and the preserved case artifact.";
                    try { evidence.RecordPhase5CTurn(row); }
                    catch (InvalidOperationException)
                    {
                        var boundedFailure = NewTurn(id, original);
                        boundedFailure["status"] = "failed";
                        boundedFailure["errorStage"] = "fixture-runner";
                        boundedFailure["errorCategory"] = error.GetType().Name;
                        boundedFailure["gap"] = "The richer record exceeded the evidence allow-list or size bound; inspect the earlier atomic checkpoint.";
                        evidence.RecordPhase5CTurn(boundedFailure);
                    }
                }
            }
            return directory;
        }

        private static JObject NewTurn(string id, string original) => new JObject
        {
            ["schemaVersion"] = 1, ["caseId"] = id, ["requestId"] = JValue.CreateNull(),
            ["turnCorrelationId"] = JValue.CreateNull(),
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
            ["commandCount"] = 0, ["commandTypes"] = new JArray(), ["nativeResults"] = new JArray(),
            ["taskStates"] = new JArray(), ["draftBefore"] = new JArray(), ["draftAfter"] = new JArray(),
            ["draftChanges"] = new JArray(), ["normalizedFields"] = new JArray(),
            ["admittedFields"] = new JArray(), ["turnHistory"] = new JArray(),
            ["fogNewlyExploredCount"] = JValue.CreateNull(),
            ["garrisonHousedCount"] = JValue.CreateNull(), ["garrisonProtectedCount"] = JValue.CreateNull(),
            ["garrisonUnaccommodatedCount"] = JValue.CreateNull(),
            ["futureObservedCount"] = JValue.CreateNull(), ["futureAssignedCount"] = JValue.CreateNull(),
            ["humanTrainCommands"] = JValue.CreateNull(),
            ["resourceProgress"] = JValue.CreateNull(), ["gatheredFoodIncome"] = JValue.CreateNull(),
            ["foodCompletionTick"] = JValue.CreateNull(), ["foodIncomeAtCompletion"] = JValue.CreateNull(),
            ["commanderCommandsAtFoodCompletion"] = JValue.CreateNull(),
            ["ordinaryFoodGatherActive"] = JValue.CreateNull(),
            ["ownedTaskFactCount"] = JValue.CreateNull(), ["readOnlyProviderRequest"] = JValue.CreateNull(),
            ["questionFactClass"] = JValue.CreateNull()
        };

        private static bool TryResponse(string id, out string response)
        {
            response = null;
            if (id == "mine-yes-original-request")
            {
                var mine = new JObject { ["type"] = "BuildStructure", ["structure"] = "Mine", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 },
                    ["placement"] = new JObject { ["anchor"] = "VisibleResource", ["relation"] = "Near",
                        ["resource"] = "Gold", ["sourceKind"] = "GoldMine" } };
                response = GraphDraft(new JArray(mine), "Independent", 0, "structure", "Confirmation", new JValue("Mine"));
                return true;
            }
            if (id == "house-next-five-from-trees")
            {
                var house = new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } };
                var watch = new JObject { ["type"] = "WatchFutureUnits", ["unit"] = "Villager", ["count"] = 5,
                    ["producer"] = "TownCenter", ["producerOrdinal"] = 1, ["action"] = "Gather",
                    ["resource"] = "Wood", ["dependsOn"] = new JArray(0) };
                response = GraphDraft(new JArray(house, watch), "Sequential", 1, "sourceKind", "SourceKind", new JValue("Tree"));
                return true;
            }
            if (id == "malformed-provider-json") { response = "{not-json"; return true; }
            if (id == "schema-valid-wrong-provider-result")
            {
                response = Request(AllocationWithAmount("2", "Wood", "Tree", 200, "Stockpile"));
                return true;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string mode = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                response = Request(AllocationNode(new[] { mode, "Exact", "4", "Any", "", "Food", "Any" }));
                return true;
            }
            if (id == "amount-versus-worker-count")
                response = Request(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
            else if (id == "count-correction-only" || id == "amount-correction-only")
            {
                var allocation = JObject.Parse(AllocationWithAmount("3", "Gold", "GoldMine", 200, "Stockpile"));
                allocation["mode"] = "TargetTotal";
                ((JObject)allocation["destination"]).Remove("sourceKind");
                response = GraphDraft(new JArray(allocation), "Independent", 0, "destination.sourceKind",
                    "SourceKind", new JValue("GoldMine"));
            }
            else if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
                response = Request(AllocationNode(new[] { "SelectedCount",
                    id == "all-current-wood-to-berries" ? "AllMatching" : "Exact",
                    id == "all-current-wood-to-berries" ? "" : "6",
                    "Gathering", "Wood", "Food", "Berries" }));
            else if (id == "additional-gathered-food-bounded-objective")
                response = Request(AllocationWithAmount("4", "Food", "Berries", 400, "AdditionalGathered"));
            else if (id == "idle-only-protection")
                response = Request("{\"type\":\"BuildStructure\",\"structure\":\"House\",\"count\":1,"
                    + "\"builders\":{\"state\":\"IdleOnly\",\"count\":1},"
                    + "\"constraints\":[{\"type\":\"PreferredWorkers\",\"mode\":\"IdleOnly\"}]}");
            else if (id == "all-wood-west-tc-explicit-move" || id == "all-wood-west-tc-explicit-gather")
                response = WestWoodline(id == "all-wood-west-tc-explicit-move");
            else if (id == "two-houses-near-town-center")
                response = "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
                    + "{\"id\":\"site\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"Near\",\"clearGapTiles\":1},\"inputs\":{},\"dependsOn\":[]},"
                    + "{\"id\":\"houses\",\"mechanic\":\"build\",\"parameters\":{\"building\":\"building:House\",\"count\":2},\"inputs\":{\"location\":\"site\"},\"dependsOn\":[\"site\"]}]}";
            else if (id == "ordered-production-exact-new-units")
                response = "{\"outcome\":\"Request\",\"executionOrder\":\"Sequential\",\"nodes\":["
                    + "{\"type\":\"EnsureUnitCount\",\"unit\":\"Spearman\",\"count\":2,\"quantityMode\":\"New\"},"
                    + "{\"type\":\"PatrolArea\",\"unitSelector\":\"Spearman\",\"count\":2,\"location\":\"PlayerBase\",\"dependsOn\":[0],\"resultFromNode\":0}]}";
            else if (id == "occupied-farms-and-capacity")
                response = Request(AllocationNode(new[] { "SelectedCount", "Exact", "4", "Any", "", "Food", "Farm" }));
            else if (id == "defense-arrival-native-receipt")
                response = Request("{\"type\":\"DefendArea\",\"unitSelector\":\"Spearman\",\"count\":6,\"location\":\"PlayerBase\"}");
            else if (id == "garrison-capacity-protected-remainder")
                response = Request("{\"type\":\"GarrisonUnits\",\"unitSelector\":\"Villagers\",\"countMode\":\"AllMatching\",\"location\":\"PlayerBase\",\"structure\":\"TownCenter\"}");
            else if (id == "finite-discovery-distinct-from-point-scout")
                response = Request("{\"type\":\"DiscoverEnemy\",\"unitSelector\":\"Scout\",\"count\":1,\"location\":\"VisibleEnemy\",\"target\":{\"kind\":\"BuildingType\",\"structure\":\"TownCenter\"}}");
            else if (id == "cancel-reset-late-retry")
                response = GraphDraft(new JArray(new JObject { ["type"] = "BuildStructure", ["structure"] = "House", ["count"] = 1,
                    ["builders"] = new JObject { ["state"] = "Eligible", ["count"] = 1 } }),
                    "Independent", 0, "structure", "Confirmation", new JValue("House"));
            else if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
                response = "{\"outcome\":\"Answer\",\"message\":\"Owned task status is unavailable in this fixture.\"}";
            return response != null;
        }

        private static string AllocationWithAmount(string count, string resource, string source, int amount, string mode)
        {
            string node = AllocationNode(new[] { "SelectedCount", "Exact", count, "Any", "", resource, source });
            return node.Substring(0, node.Length - 1) + ",\"resourceAmount\":" + amount
                + ",\"resourceAmountMode\":\"" + mode + "\"}";
        }

        private static string WestWoodline(bool move) => "{\"outcome\":\"DynamicPlan\",\"version\":1,\"nodes\":["
            + "{\"id\":\"all\",\"mechanic\":\"select-workers\",\"parameters\":{\"state\":\"Gathering\",\"currentResource\":\"Wood\",\"countMode\":\"AllMatching\"},\"inputs\":{},\"dependsOn\":[]},"
            + "{\"id\":\"woodline\",\"mechanic\":\"resolve-location\",\"parameters\":{\"anchor\":\"MyTownCenter\",\"relation\":\"MapWest\",\"clearGapTiles\":1,\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},\"inputs\":{},\"dependsOn\":[]},"
            + (move
                ? "{\"id\":\"effect\",\"mechanic\":\"move-workers\",\"parameters\":{},"
                : "{\"id\":\"effect\",\"mechanic\":\"allocate-workers\",\"parameters\":{\"resource\":\"Wood\",\"sourceKind\":\"Tree\"},")
            + "\"inputs\":{\"workers\":\"all\",\"location\":\"woodline\"},\"dependsOn\":[\"all\",\"woodline\"]}]}";

        private static JArray Missing(CommanderPendingClarification pending)
        {
            if (pending?.GraphDraft != null)
                return new JArray(pending.GraphDraft.MissingSlots.Select(slot => slot.Node + ":" + slot.Path + ":" + slot.Type));
            if (pending?.Draft != null)
                return new JArray(pending.Draft.MissingFields.Select(field => field.ToString()));
            return new JArray();
        }
        private static JArray DraftDelta(JArray before, JArray after, JArray beforeFields, JArray afterFields)
        {
            var changes = new JArray(before.Values<string>().Except(after.Values<string>(), StringComparer.Ordinal));
            for (int i = 1; i < Math.Min(beforeFields.Count, afterFields.Count); i++)
            {
                if (!(beforeFields[i] is JObject original) || !(afterFields[i] is JObject current)) continue;
                foreach (var field in original.Properties())
                    if (field.Name != "dependencies" && !JToken.DeepEquals(field.Value, current[field.Name]))
                        changes.Add("node" + (i - 1) + "." + field.Name + ":"
                            + field.Value.ToString(Formatting.None) + "->" + current[field.Name]?.ToString(Formatting.None));
            }
            return changes;
        }
        private static JArray NormalizeCorpus(CommanderSemanticResult result)
        {
            var fields = new JArray();
            if (result == null || !result.IsValid) return fields;
            foreach (var node in result.Nodes.Take(4))
                fields.Add(new JObject { ["type"] = node.Type.ToString(), ["building"] = node.BuildingType?.ToString(),
                    ["unit"] = node.UnitType, ["resource"] = node.ResourceType?.ToString(), ["count"] = node.Count,
                    ["quantityMode"] = node.QuantityMode.ToString(), ["anchor"] = node.PlacementAnchorSelector?.ToString(),
                    ["anchorOrdinal"] = node.PlacementAnchorOrdinal, ["relation"] = node.PlacementRelation?.ToString(),
                    ["placementSource"] = node.SourceKind?.ToString(), ["unitSelector"] = node.UnitSelector?.ToString(),
                    ["locationSelector"] = node.LocationSelector?.ToString(),
                    ["producerOrdinal"] = node.ProducerOrdinal, ["producerFrom"] = node.ProducerFromNode,
                    ["resultFrom"] = node.ResultFromNode, ["futureAction"] = node.FutureAction?.ToString(),
                    ["workerMode"] = node.WorkerAllocation?.Mode.ToString(),
                    ["workerCountMode"] = node.WorkerAllocation?.CountMode.ToString(),
                    ["workerCount"] = node.WorkerAllocation?.Count,
                    ["workerState"] = node.WorkerAllocation?.Workers.State.ToString(),
                    ["workerCurrentResource"] = node.WorkerAllocation?.Workers.CurrentResource?.ToString(),
                    ["destinationResource"] = node.WorkerAllocation?.Destination.Resource.ToString(),
                    ["destinationSource"] = node.WorkerAllocation?.Destination.SourceKind.ToString(),
                    ["resourceAmount"] = node.WorkerAllocation?.ResourceAmount,
                    ["resourceAmountMode"] = node.WorkerAllocation?.ResourceAmountMode?.ToString(),
                    ["dependencies"] = new JArray(node.DependsOn) });
            if (result.PendingGraphDraft != null)
            {
                foreach (var field in ProjectGraphDraft(result.PendingGraphDraft)) fields.Add(field);
            }
            if (result.DynamicPlan != null)
            {
                var program = result.DynamicPlan.Nodes.ToList();
                foreach (var step in program.Take(12))
                {
                    var parameters = new JObject();
                    foreach (var field in step.Primitive.Parameters)
                        if (step.Parameters.TryGetValue(field.Name, out object value))
                            parameters[field.Name == "clearGapTiles" ? "clearGap" : field.Name] = value is int count ? (JToken)count
                                : value is string name && name.Length <= 64 ? (JToken)name : "unavailable";
                    var inputs = new JObject();
                    foreach (var input in step.Inputs)
                        inputs[input.Key] = program.FindIndex(node => node.Id == input.Value);
                    fields.Add(new JObject { ["mechanic"] = step.Primitive.Mechanic.ToString(),
                        ["parameters"] = parameters, ["inputs"] = inputs,
                        ["dependencies"] = new JArray(step.DependsOn.Select(dep => program.FindIndex(node => node.Id == dep))) });
                }
            }
            return fields;
        }
        private static JArray ProjectGraphDraft(CommanderRequestGraphDraft draft)
        {
            var fields = new JArray { new JObject { ["draftNodeCount"] = draft.NodeCount,
                ["missingSlots"] = MissingSlots(draft) } };
            var pendingNodes = (JArray)draft.ToPendingJson()["nodes"];
            foreach (JObject node in pendingNodes.Take(4))
                fields.Add(new JObject { ["draftType"] = (string)node["type"],
                        ["structure"] = (string)node["structure"], ["count"] = (int?)node["count"],
                        ["resource"] = (string)node["resource"],
                        ["resourceAmount"] = (int?)node["resourceAmount"],
                        ["resourceAmountMode"] = (string)node["resourceAmountMode"],
                        ["workerMode"] = (string)node["mode"], ["countMode"] = (string)node["countMode"],
                        ["workerState"] = (string)node["workers"]?["state"],
                        ["workerCurrentResource"] = (string)node["workers"]?["currentResource"],
                        ["destinationResource"] = (string)node["destination"]?["resource"],
                        ["destinationSource"] = (string)node["destination"]?["sourceKind"],
                        ["producerOrdinal"] = (int?)node["producerOrdinal"],
                        ["dependencies"] = node["dependsOn"] is JArray dependencies
                            ? new JArray(dependencies.Values<int>()) : new JArray() });
            return fields;
        }
        private static JArray AdmittedFields(CorpusFixture fixture)
        {
            var admitted = new JArray();
            foreach (var goal in fixture.Manager.Goals.Take(12))
            {
                var projection = new JObject { ["type"] = goal.GetType().Name, ["status"] = goal.Status.ToString(),
                    ["originalUtterance"] = goal.RequestAuthority?.OriginalInput };
                if (goal is BuildStructureGoal build)
                {
                    projection["building"] = build.StructureType.ToString(); projection["count"] = build.Count;
                    projection["anchor"] = build.PlacementAnchorSelector?.ToString();
                    projection["anchorOrdinal"] = build.PlacementAnchorOrdinal;
                    projection["relation"] = build.PlacementRelation?.ToString();
                    projection["resource"] = build.PlacementResourceType?.ToString();
                    projection["source"] = build.PlacementSourceKind?.ToString();
                }
                if (goal is AllocateWorkersGoal workers)
                {
                    var allocation = workers.Allocation;
                    projection["mode"] = allocation.Mode.ToString(); projection["countMode"] = allocation.CountMode.ToString();
                    projection["count"] = allocation.Count; projection["state"] = allocation.Workers.State.ToString();
                    projection["currentResource"] = allocation.Workers.CurrentResource?.ToString();
                    projection["destinationResource"] = allocation.Destination.Resource.ToString();
                    projection["destinationSource"] = allocation.Destination.SourceKind.ToString();
                    projection["resourceAmount"] = allocation.ResourceAmount;
                    projection["resourceAmountMode"] = allocation.ResourceAmountMode?.ToString();
                }
                admitted.Add(projection);
            }
            return admitted;
        }
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
            long? request = fixture.Chat.LastSubmittedSemanticRequestId;
            return request > 0 ? "runtime:" + request.Value : null;
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
        private static void CaptureNativePatrolFacts(CorpusFixture fixture, JObject row, IReadOnlyList<int> resultIds)
        {
            int active = 0;
            bool waypointsMatch = resultIds.Count == 2 && fixture.PatrolTarget.HasValue;
            bool destinationMatch = waypointsMatch;
            foreach (int id in resultIds)
            {
                UnitData actor = fixture.Simulation.UnitRegistry.GetUnit(id);
                bool current = actor != null && actor.PlayerId == 0 && actor.CurrentHealth > 0
                    && actor.State != UnitState.Dead && actor.IsPatrolling;
                if (current) active++;
                bool route = current && actor.PatrolWaypoints != null && actor.PatrolWaypoints.Count == 2
                    && actor.PatrolWaypoints[1] == fixture.PatrolTarget.Value;
                waypointsMatch &= route;
                destinationMatch &= route && (actor.FinalDestination == actor.PatrolWaypoints[0]
                    || actor.FinalDestination == actor.PatrolWaypoints[1]);
            }
            row["nativePatrolActorCount"] = active;
            row["nativePatrolWaypointsMatch"] = waypointsMatch;
            row["nativePatrolDestinationMatch"] = destinationMatch;
        }

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
            string states = string.Join("|", fixture.Manager.Goals.Select(goal => goal.GetType().Name + ":" + goal.Status));
            bool hasNative(string name) => fixture.Native.Any(entry => entry.StartsWith(name, StringComparison.Ordinal));
            bool hasCommand(string name) => fixture.Commands.Contains(name);
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
                bool rejected = oneCall && fixture.Provider.LastResult?.IsValid == false
                    && noEffect && fixture.Chat.PendingClarification == null
                    && fixture.Chat.PendingActionPlan == null;
                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
                row["fidelity"] = rejected ? "not-evaluable" : "mismatch";
                row["status"] = rejected ? "observed" : "open-gap";
                row["errorStage"] = "parse";
                row["errorCategory"] = rejected ? "malformed-rejected" : "malformed-rejection-regressed";
                if (!rejected) row["gap"] = "The malformed response did not produce exactly one invalid parse with no clarification, approval, goal or Commander command.";
                return;
            }
            if (id == "schema-valid-wrong-provider-result")
            {
                var admitted = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                var allocation = admitted?.Allocation;
                bool wrongButAdmitted = oneCall && fixture.Provider.LastResult?.IsValid == true
                    && fixture.Provider.LastResult.Outcome == CommanderSemanticOutcome.Request
                    && fixture.Manager.Goals.Count == 1 && admitted != null
                    && admitted.RequestAuthority?.OriginalInput == (string)row["utterance"]
                    && allocation.Mode.ToString() == "SelectedCount"
                    && allocation.CountMode.ToString() == "Exact" && allocation.Count == 2
                    && allocation.Destination.Resource == ResourceType.Wood
                    && allocation.Destination.SourceKind == ResourceSourceKind.Tree
                    && allocation.ResourceAmount == 200
                    && allocation.ResourceAmountMode.ToString() == "Stockpile";
                row["semanticValid"] = fixture.Provider.LastResult?.IsValid;
                row["fidelity"] = wrongButAdmitted ? "wrong" : "mismatch";
                row["status"] = wrongButAdmitted ? "observed" : "open-gap";
                row["errorStage"] = wrongButAdmitted ? "semantic-fidelity" : "fixture-verdict";
                row["errorCategory"] = wrongButAdmitted ? "schema-valid-wrong-action" : "wrong-response-diagnostic-regressed";
                row["gap"] = wrongButAdmitted
                    ? "Synthetic Wood/count-2 result differs from declared Gold/count-3/amount-200 request; actual admission is recorded separately."
                    : "The deliberately wrong fixture was not both schema-valid and admitted as the exact Wood/count-2 action; do not certify a semantic mismatch.";
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
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                var admitted = goal?.Allocation;
                Mark(oneCall && fixture.Chat.PendingClarification == null && goal != null
                    && admitted.Mode.ToString() == "TargetTotal"
                    && admitted.CountMode.ToString() == "Exact"
                    && admitted.Count == (id == "count-correction-only" ? 4 : 3)
                    && admitted.ResourceAmount == (id == "amount-correction-only" ? 250 : 200)
                    && admitted.ResourceAmountMode.ToString() == "Stockpile"
                    && admitted.Destination.Resource == ResourceType.Gold
                    && admitted.Destination.SourceKind == ResourceSourceKind.GoldMine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"],
                    "local-continuation", "typed-correction-or-admission-not-retained");
                return;
            }
            if (id == "death-versus-takeover" || id == "causal-information-owned-task-facts")
            {
                bool grounded = (string)row["questionFactClass"] ==
                    (id == "causal-information-owned-task-facts" ? "wood-shortage" : "human-override");
                Mark(oneCall && fixture.Manager.Goals.Count == fixture.InitialGoalCount
                    && fixture.Commands.Count == fixture.InitialCommanderCommandCount
                    && fixture.Provider.LastResult?.Outcome == CommanderSemanticOutcome.Answer
                    && fixture.Provider.LastRequest?.IsReadOnlyQuestion == true
                    && (int?)row["ownedTaskFactCount"] > 0 && grounded
                    && (id != "death-versus-takeover" || fixture.BuilderAffinityVerified
                        && fixture.AssignedBuilderHumanProtected),
                    "read-only-answer", "owned-task-cause-not-observed", terminal: false);
                if (!grounded) row["gap"] = "Owned task facts were seeded, but this fixture did not prove the requested native cause.";
                return;
            }
            if (id == "mine-yes-original-request")
            {
                var goal = fixture.Manager.Goals.OfType<BuildStructureGoal>().SingleOrDefault();
                Mark(oneCall && goal?.StructureType == BuildingType.Mine
                    && goal.RequestAuthority?.OriginalInput == (string)row["utterance"]
                    && fixture.Chat.PendingClarification == null, "admission", "original-mine-ticket-lost");
                if ((string)row["fidelity"] == "matched" && goal.Status != CommanderGoalStatus.Completed)
                { row["status"] = "open-gap"; row["errorStage"] = "native-placement";
                  row["errorCategory"] = "mine-placement-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "house-next-five-from-trees")
            {
                var history = (JArray)row["turnHistory"];
                var fields = history.Count > 1
                    ? (JArray)history[1]["normalizedFields"] : (JArray)row["normalizedFields"];
                bool graph = fields.Count >= 2 && (string)fields[0]["type"] == "BuildStructure"
                    && (string)fields[0]["building"] == "House"
                    && (string)fields[1]["type"] == "WatchFutureUnits"
                    && (int?)fields[1]["count"] == 5 && (int?)fields[1]["producerOrdinal"] == 1
                    && (string)fields[1]["resource"] == "Wood"
                    && (string)fields[1]["placementSource"] == "Tree"
                    && ((JArray)fields[1]["dependencies"]).Values<int>().SequenceEqual(new[] { 0 });
                Mark(oneCall && graph && (bool?)row["approved"] == true
                    && fixture.Manager.Goals.Count == 2 && hasNative("PlaceBuildingCommand:processed")
                    && (int?)row["humanTrainCommands"] == 5
                    && (int?)row["futureObservedCount"] == 5 && (int?)row["futureAssignedCount"] == 5
                    && fixture.Native.Count(name => name == "GatherCommand:processed") >= 5,
                    "native-future-births", "five-attributed-births-not-complete");
                if ((string)row["fidelity"] == "matched" && !states.Contains("WatchFutureUnitsGoal:Completed"))
                { row["status"] = "open-gap"; row["errorStage"] = "native-future-births";
                  row["errorCategory"] = "five-attributed-births-not-complete"; row["gap"] = states; }
                return;
            }
            if (id == "finite-discovery-distinct-from-point-scout")
            {
                Mark(oneCall && fixture.Discovery != null && fixture.Discovery.TargetInitiallyHidden
                    && fixture.Discovery.NoOrderControlStable && hasCommand("MoveCommand")
                    && hasNative("Move:Complete") && fixture.Discovery.TravelRevealedCount() > 0
                    && fixture.Discovery.TargetEverVisibleAfterTravel,
                    "native-discovery", "travel-fog-not-revealed");
                if ((string)row["fidelity"] == "matched" && !states.Contains("CommanderCapabilityGoal:Completed")
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
                var resultIds = producer?.ResultUnitIds ?? Array.Empty<int>();
                CaptureNativePatrolFacts(fixture, row, resultIds);
                Mark(oneCall && fixture.Provider.LastResult?.IsValid == true
                    && (bool?)row["approved"] == true && (bool?)row["previewMentionsOwnedBase"] == true
                    && fixture.HumanTrainCommands == 1 && fixture.HumanBirthId >= 0
                    && producer?.Status == CommanderGoalStatus.Completed && patrol != null
                    && (int?)row["newResultCount"] == 2
                    && (bool?)row["oldUnitExcluded"] == true
                    && (bool?)row["humanBirthExcluded"] == true
                    && (bool?)row["exactPatrolBinding"] == true
                    && (int?)row["nativePatrolActorCount"] == 2
                    && (bool?)row["nativePatrolWaypointsMatch"] == true
                    && (bool?)row["nativePatrolDestinationMatch"] == true
                    && hasCommand("TrainUnitCommand") && hasCommand("PatrolCommand")
                    && hasNative("PatrolCommand:processed"),
                    "native-result-binding", "exact-new-patrol-chain-not-observed", terminal: false);
                if ((string)row["fidelity"] != "matched")
                    row["gap"] = "Check ordinary human queue, exactly two Commander-attributed newborn results, owned-base preview, exact native patrol subjects, both persistent routes and current destinations independently.";
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
            if (id == "garrison-capacity-protected-remainder")
            {
                var goal = fixture.Manager.Goals.OfType<CommanderCapabilityGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Blocked
                    && hasNative("GarrisonCommand:processed") && goal.GarrisonShelter?.GarrisonCount == 1
                    && goal.GarrisonProtectedCount == 1 && goal.GarrisonUnaccommodatedCount == 1,
                    "native-garrison", "capacity-or-protected-remainder-missing");
                return;
            }
            if (id == "occupied-farms-and-capacity")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Status == CommanderGoalStatus.Completed
                    && goal.Allocation.Count == 4 && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Farm
                    && fixture.Commands.Count(name => name == "GatherCommand") == 2
                    && fixture.Native.Count(name => name == "GatherCommand:processed") == 2,
                    "native-farm-allocation", "incumbent-or-capacity-count-mismatch");
                return;
            }
            if (id == "amount-versus-worker-count")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Count == 3
                    && goal.Allocation.ResourceAmount == 200
                    && goal.Allocation.ResourceAmountMode?.ToString() == "Stockpile"
                    && goal.Allocation.Destination.Resource == ResourceType.Gold
                    && goal.Status == CommanderGoalStatus.WaitingForResources
                    && hasNative("GatherCommand:processed"),
                    "native-resource-objective", "amount-worker-slots-collapsed-or-premature-completion");
                return;
            }
            if (id.StartsWith("worker-mode-", StringComparison.Ordinal))
            {
                string expected = id == "worker-mode-additional-four" ? "Additional"
                    : id == "worker-mode-target-total-four" ? "TargetTotal" : "SelectedCount";
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == expected
                    && goal.Allocation.CountMode.ToString() == "Exact" && goal.Allocation.Count == 4
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "quantity-mode-or-four-worker-effect-missing");
                return;
            }
            if (id == "all-current-wood-to-berries" || id == "exact-current-wood-to-berries")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && goal != null && goal.Allocation.Mode.ToString() == "SelectedCount"
                    && goal.Allocation.CountMode.ToString() == (id == "all-current-wood-to-berries" ? "AllMatching" : "Exact")
                    && goal.Allocation.Workers.State.ToString() == "Gathering"
                    && goal.Allocation.Workers.CurrentResource == ResourceType.Wood
                    && goal.Allocation.Destination.Resource == ResourceType.Food
                    && goal.Allocation.Destination.SourceKind == ResourceSourceKind.Berries
                    && goal.Status == CommanderGoalStatus.Completed && hasNative("GatherCommand:processed"),
                    "native-worker-allocation", "all-exact-source-or-destination-mismatch");
                return;
            }
            if (id == "two-houses-near-town-center")
            {
                Mark(oneCall && (bool?)row["approved"] == true && fixture.Commands.Count(name => name == "PlaceBuildingCommand") == 2
                    && fixture.Native.Count(name => name == "PlaceBuildingCommand:processed") == 2
                    && states.Contains("BuildStructureGoal:Completed"), "native-construction", "two-distinct-build-results-missing");
                return;
            }
            if (id == "defense-arrival-native-receipt")
            {
                Mark(oneCall && hasCommand("MoveCommand") && fixture.Native.Count(name => name == "Move:Complete") == 6
                    && states.Contains("CommanderCapabilityGoal:Completed"), "native-arrival", "six-exact-arrivals-missing");
                return;
            }
            if (id == "all-wood-west-tc-explicit-move")
            {
                Mark(oneCall && hasCommand("MoveCommand") && !hasCommand("GatherCommand")
                    && hasNative("Move:Complete") && states.Contains("CommanderCapabilityGoal:Completed"),
                    "native-movement", "explicit-move-rewritten-or-incomplete");
                return;
            }
            if (id == "all-wood-west-tc-explicit-gather")
            {
                Mark(oneCall && hasCommand("GatherCommand") && !hasCommand("MoveCommand")
                    && hasNative("GatherCommand:processed") && states.Contains("AllocateWorkersGoal:Completed"),
                    "native-gather", "explicit-gather-rewritten-or-incomplete");
                return;
            }
            if (id == "additional-gathered-food-bounded-objective")
            {
                var goal = fixture.Manager.Goals.OfType<AllocateWorkersGoal>().SingleOrDefault();
                Mark(oneCall && hasCommand("GatherCommand") && hasNative("GatherCommand:processed")
                    && goal?.Status == CommanderGoalStatus.Completed
                    && goal.ResourceProgress == 400
                    && (long?)row["foodIncomeAtCompletion"] == 400
                    && (long?)row["gatheredFoodIncome"] > 400
                    && (int?)row["commanderCommandsAtFoodCompletion"] == fixture.Commands.Count
                    && (bool?)row["ordinaryFoodGatherActive"] == true,
                    "native-resource-objective", "completion-or-post-target-gather-not-observed");
                return;
            }
            Mark(oneCall && fixture.Provider.LastResult?.IsValid == true && fixture.Manager.Goals.Count > 0,
                "admission", "expected-goal-not-admitted");
        }
        private static double Milliseconds(long start, long end)
            => Math.Round((end - start) * 1000.0 / Stopwatch.Frequency, 3);
    }
}
