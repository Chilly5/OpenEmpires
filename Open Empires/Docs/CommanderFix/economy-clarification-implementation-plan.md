# Commander Economy and Clarification Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair generic worker allocation and typed clarification without changing accepted production, strategic authority or voice behavior.

**Architecture:** Add an isolated AllocateWorkers semantic/intent/goal path with deterministic worker/source selection. Carry an optional source restriction through ordinary gather/slaughter execution so source fidelity survives native retargeting. Keep bounded pending clarification separate from executable nodes and conversation memory.

**Tech Stack:** Existing Unity 6000.5.9f1, C#, NUnit/Unity Test Framework, Unity MCP, existing OpenRouter Luna provider and ordinary CommandBuffer/GameSimulation path; no new package dependency.

**Spec:** [Approved design](economy-clarification-design.md). Also read `economy-clarification-root-cause.md`, preserved baseline JSON, and the active goal brief at `C:/Users/RS/.codex/attachments/846fe947-c2f2-4f38-94b3-73f43c531070/pasted-text-1.txt`.

## Global Constraints

- Not Phase 4I; no voice/STT work except the shared text-path regression. No hostile audit or general Commander rewrite.
- Provider supplies criteria only; no entity/player IDs, positions, commands or direct simulation mutation. Preserve strategic approval and legacy SetResourceAllocation behavior.
- Count integer 1..200; admission also checks the owning simulation's population bound. AllMatching is a bounded one-time snapshot, not a continuing policy.
- Pending original text <=1024 characters; question <=180 characters; one pending request; at most three continuation turns. Reset/runtime/disposal invalidate it.
- Human authority remains highest. Initial worker/target preflight and reservations are all-or-none; later interrupted execution is reported honestly, not rolled back or silently substituted.
- Preserve one ordinary ICommand per planning tick. Use deterministic fixed-point/integer ordering and existing bounded path-check policy.
- Root owns design, core implementation and integration; GPT-6 Luna read-only agents handle bounded inventories, test enumeration, documentation checks and evidence consistency. No simultaneous core-file writers.
- Existing branch `unit_models_and_voice_control`, original baseline HEAD `b96cf9b622f796df5c90f402b2bb16720c52f78e`, design-only HEAD `74e5716`. Preserve all pre-existing dirty changes and local credentials. No blanket staging, reset, package upgrade or unsolicited branch/PR.
- Approximately 15 focused EditMode methods and three PlayMode scenarios. Relevant Commander suites, then full EditMode and PlayMode once on final source because native gathering/serialization is shared. No repeated full regression during development.

## Review Focus

1. A manual order between reservation and execution, or after a protection lease expires, must interrupt this goal without reclaiming/replacing that worker (Task 3).
2. A sheep dies or a node depletes between selection and command processing: restricted native fallback must not become berries; a later human order must remove the old restriction (Task 2).
3. Inadequate idle workers or farm capacity must yield zero initial commands/reservations; another matching farm must be considered before blocking (Task 3).
4. `build 4 spearmen` while awaiting a count is a new request, whereas `four villagers` fills only the count; ambiguous replies cannot loop forever (Task 4).
5. A delayed provider response after match replacement cannot restore old pending state or admit any old goal (Task 4).

---

## File map and ownership

All source paths below are relative to `D:/unity_projects/OpenEmpires/Open Empires`; namespace `OpenEmpires`, test namespace `OpenEmpires.Tests`. Append enum members to preserve existing numeric values.

| Responsibility | New files | Existing integration points |
| --- | --- | --- |
| Typed completed worker orders | `Assets/Scripts/AI/Commander/Economy/CommanderWorkerAllocation.cs`, `AllocateWorkersIntent.cs`, `AllocateWorkersGoal.cs` | `CommanderIntent.cs`, `CommanderGoal.cs`, `CommanderIntentDto.cs`, `CommanderIntentValidator.cs`, `CommanderIntentResolver.cs`, `CommanderIntentCatalog.cs`, `CommanderResponseGenerator.cs` under `Assets/Scripts/AI/Commander/` |
| Canonical source classification and native fidelity | `Assets/Scripts/Resources/ResourceSourceKind.cs`, `ResourceSourceRules.cs` | `Assets/Scripts/Commands/GatherCommand.cs`, `SlaughterSheepCommand.cs`, `QueuedCommand.cs`; `Assets/Scripts/Units/UnitData.cs`; `Assets/Scripts/Core/GameSimulation.cs`; `Assets/Scripts/Resources/ResourceGatheringSystem.cs`; `Assets/Scripts/Network/CommandSerializer.cs` |
| Deterministic preflight/execution | `Assets/Scripts/AI/Commander/Economy/CommanderResourceSourceResolver.cs`, `CommanderPlanner.Economy.cs` | `CommanderPlanner.cs`, `CommanderGoalManager.cs`, `CommanderWorkerAuthority.cs`, `CommanderContextBuilder.cs` |
| Draft/continuation model | `Assets/Scripts/AI/Commander/Phase4E/CommanderPendingClarification.cs`, `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs` | `Phase4E/CommanderSemanticRequest.cs`, `CommanderSemanticJson.cs`, `CommanderSemanticAdmission.cs`, `CommanderSemanticGraphAdmission.cs`, `CommanderSemanticProvider.cs`; `Phase4A/CommanderChatUI.cs`, `OpenRouterCommanderProvider.cs` |
| Focused evidence | `Assets/Tests/EditMode/CommanderEconomyClarificationTests.cs`, `Assets/Tests/PlayMode/CommanderEconomyClarificationPlayModeTests.cs` | Reuse fixture construction/disposal patterns from `CommanderPhase4E1ChatTests.cs` and existing simulation/authority tests; do not modify accepted tests to weaken assertions |
| Documentation | `Docs/CommanderFix/economy-semantic-schema.md`, `clarification-state.md`, `focused-test-evidence.md`, `fix-report.md`, `source-manifest.json`, `source-consistency.md` | Existing root-cause report; `Docs/CommanderPhase4G/implemented-capabilities.md`, `known-limitations.md`; root `remaining_work.md` |

Unity-generated `.meta` files belong to their new source/test files. Do not hand-generate duplicate GUIDs. Confirm exact live file locations before editing; only narrow integration hooks in large existing files, not wholesale restructuring.

## Verification procedure used by every task

Use `mcp__unityMCP__run_tests` with `mode: "EditMode"`, `test_names` equal to the fully qualified methods named in that task, `include_details: true`, `include_failed_tests: true`. Prefix each method with `OpenEmpires.Tests.CommanderEconomyClarificationTests.`. Preserve returned job ID; poll only that ID with `get_test_job`, `wait_timeout: 30`. Red means the new assertion fails for the missing behavior, not an infrastructure failure. Green means terminal completed results with zero failures; save complete result XML and hashes at closeout.

After source changes, explicitly refresh assets/request script compilation and confirm the current test assembly and error-free console before using test results. Do not trust a green result from a stale assembly. Unimplemented new types may initially produce a compile failure; add only the minimum interface scaffold needed to obtain a behavioral red before implementation.

Each task ends with a scoped checkpoint: review `git diff`, stage only entirely new task-owned files or verified task-only hunks, inspect `git diff --cached`, then commit. Never stage a whole pre-existing dirty file merely because this task also touched it; if safe hunk isolation is unavailable, leave the integrated source uncommitted and record it rather than capturing unrelated user work.

### Task 1: Completed typed worker orders and strict admission

**Files:** Typed-order row and completed-node integration points in the file map; focused EditMode test file.

**Interfaces:** New enums `CommanderWorkerAllocationMode { TargetTotal, Additional, SelectedCount }`, `CommanderWorkerCountMode { Exact, AllMatching }`, `CommanderWorkerState { Any, Idle, Gathering }`. Immutable constructors `CommanderWorkerSelector(CommanderWorkerState state, ResourceType? currentResource = null)` and `CommanderResourceDestination(ResourceType resource, ResourceSourceKind sourceKind = Any)` expose get-only State/CurrentResource and Resource/SourceKind. `CommanderWorkerAllocation(CommanderWorkerAllocationMode mode, CommanderWorkerCountMode countMode, int? count, CommanderWorkerSelector workers, CommanderResourceDestination destination)` exposes Mode/CountMode/Count/Workers/Destination. `CommanderSemanticNode.WorkerAllocation` is optional and meaningful only for new AllocateWorkers. `AllocateWorkersIntent(int playerId, CommanderWorkerAllocation allocation, CommanderIntentLayer intentLayer = CommanderIntentLayer.Tactical, IReadOnlyList<CommanderConstraint> constraints = null)` derives from CommanderIntent. Source enum defined by Task 2 can be introduced as its interface scaffold here. Existing graph/admission/DTO method signatures remain stable.

- [ ] Write `SemanticMappings_PreserveWorkerAndSourceCriteria` and `InvalidAllocationSchema_RejectsBoundsFieldsAndIncompatibleSources`. Use five semantic fixtures and the existing parser/admission/DTO codec, not a phrase parser. Assertion excerpts:
  ```csharp
  Assert.That(food.Count, Is.EqualTo(4)); Assert.That(food.Mode, Is.EqualTo(CommanderWorkerAllocationMode.TargetTotal));
  Assert.That(idleFood.Workers.State, Is.EqualTo(CommanderWorkerState.Idle)); Assert.That(idleFood.Count, Is.EqualTo(4));
  Assert.That(idleSheep.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep)); Assert.That(idleSheep.Count, Is.EqualTo(4));
  Assert.That(transfer.Workers.CurrentResource, Is.EqualTo(ResourceType.Wood)); Assert.That(transfer.Destination.Resource, Is.EqualTo(ResourceType.Gold)); Assert.That(transfer.Count, Is.EqualTo(3));
  Assert.That(allBerries.CountMode, Is.EqualTo(CommanderWorkerCountMode.AllMatching)); Assert.That(allBerries.Count, Is.Null);
  Assert.That(allBerries.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Berries));
  Assert.That(invalidResults, Has.All.Property("IsValid").False);
  ```
  Invalid fixtures cover duplicate/unknown fields, provider IDs/coordinates, 0/201/noninteger counts, owning population bound, Food/Tree, Gold/Sheep, currentResource without Gathering, constrained TargetTotal, AllMatching with count or Any, and producer/result references on AllocateWorkers. Roundtrip every valid field without granting execution to discovered content.
- [ ] Run these two exact focused methods; obtain behavioral red for missing AllocateWorkers support.
- [ ] Implement types and narrow parser/admission/intent-codec/validator/resolver branches. Reject malformed combinations atomically. Preserve old SetResourceAllocation forms and all existing enum values. Goal submission can be scaffolded without execution until Task 3.
- [ ] Rerun the same two methods; require green and unchanged legacy-node acceptance.
- [ ] Scoped checkpoint: `feat: add typed commander worker allocation semantics`.

### Task 2: Source restriction across ordinary native commands

**Files:** Canonical source/native-fidelity row; Task 1 consumes ResourceSourceKind. Test file.

**Interfaces:** `ResourceSourceKind { Any = 0, Sheep, Berries, Farm, Tree, GoldMine, StoneMine }`. `ResourceSourceRules.Matches(ResourceNodeData node, ResourceSourceKind sourceKind) -> bool`, and `IsCompatible(ResourceType resource, ResourceSourceKind sourceKind) -> bool` are pure projections over current authoritative flags/types, not a duplicated gameplay database. Add `SourceKind` to GatherCommand/SlaughterSheepCommand (default Any), QueuedCommand and active `UnitData.GatherSourceKind`. Keep existing constructor calls valid with an optional last parameter; queue factory overloads also default Any. Existing CommandSerializer ToJson/FromJson/Serialize/Deserialize signatures stay unchanged.

- [ ] Write `SourceConstraints_RoundtripJsonBinaryQueuedAndLegacyDefaults` and `SourceConstraints_SurviveFallbackDepletionAndClearOnHumanOrder` using actual simulation command processing. Assertion excerpts:
  ```csharp
  Assert.That(((GatherCommand)jsonRoundtrip).SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
  Assert.That(((SlaughterSheepCommand)binaryRoundtrip).SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
  Assert.That(legacyGather.SourceKind, Is.EqualTo(ResourceSourceKind.Any)); Assert.That(queued.SourceKind, Is.EqualTo(ResourceSourceKind.Farm));
  Assert.That(worker.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Sheep)); Assert.That(worker.TargetResourceNodeId, Is.Not.EqualTo(closerBerryId));
  Assert.That(workerAfterHumanGather.GatherSourceKind, Is.EqualTo(ResourceSourceKind.Any));
  ```
  Cover mixed-command batches, old binary/JSON fixtures, malformed/unknown restricted envelopes, queue promotion, full farm/depletion retarget, slaughter-to-carcass continuity, deposit/resume, and queued/nonqueued human replacement. Restricted unavailable targets stop/wait, never coarse-Food fallback.
- [ ] Run both exact methods; require red before implementing fidelity.
- [ ] Implement canonical matching: Sheep carcass flag, Farm flag + linked owned completed building where legality is checked, ordinary registered Food minus carcass/farm for current Berries, canonical Wood/Gold/Stone for remaining kinds. Live Sheep validity uses actual IsSheep/ownership, not node classification. Thread restriction through assignments, automatic redirect, depletion/farm fallback, queued execution and slaughter/carried-food continuation; clear/replace on later ordinary human orders, preserve across automatic deposit.
- [ ] Implement explicit binary restricted-envelope version 1 only when any gather/slaughter restriction is non-Any: negative marker `int.MinValue`, version, tick, count, then versioned command payloads including source-kind byte. Unrestricted batches retain exact legacy encoding; new decoder reads both. Reject unknown version/invalid enum/truncation atomically. JSON missing field defaults Any, present invalid fields reject. Never silently downgrade a restricted batch; document that old receivers cannot enforce new restrictions and matching updated peers are required. Confirm the negative marker is outside valid simulation ticks before using it.
- [ ] Rerun both methods; require green with original unrestricted behavior preserved.
- [ ] Scoped checkpoint: `fix: preserve resource source constraints through native gathering`.

### Task 3: Deterministic full preflight and bounded execution

**Files:** Deterministic preflight/execution row, AllocateWorkersGoal and response branches. Tests.

**Interfaces:** `AllocateWorkersGoal` stores allocation, captured baseline, frozen assignments, issued group cursor and sticky human-interruption state. Internal immutable `CommanderWorkerAssignment` stores game-side WorkerId, typed `CommanderWorkerTargetKind { ResourceNode, OwnedSheep }`, TargetId, Resource and SourceKind; never serialize it to the provider. Internal `CommanderResourceSourceResolver.TryResolveAssignments(GameSimulation simulation, CommanderWorkerAuthority authority, int playerId, int goalId, IReadOnlyList<int> workerIds, CommanderResourceDestination destination, out IReadOnlyList<CommanderWorkerAssignment> assignments, out string blocker) -> bool` includes other-goal reservations when computing source capacity. Make CommanderPlanner partial; new private `PlanWorkerAllocation(AllocateWorkersGoal goal) -> CommanderPlan` preserves CommanderPlan's single command. Add `CommanderWorkerAuthority.TryReserveWorkers(IReadOnlyList<int> workerIds, int goalId, int currentTick) -> bool` (check all, then reserve all as Gatherer). `CommanderGoalManager.SubmitWorkerAllocation(AllocateWorkersIntent intent) -> AllocateWorkersGoal` is used only after ordinary validation.

- [ ] Write `QuantityModes_KeepBaselineTransferAndAllMatchingDistinct`, `WorkerSelection_RespectsOwnershipAuthorityAndCurrentResource`, `TargetResolution_RejectsUnknownExhaustedUnreachableAndWrongSources`, `ReservationPreflight_DistributesCapacityOrIssuesNothing`, and `HumanTakeover_InterruptsWithoutReclaimAfterLease`. Assertion excerpts:
  ```csharp
  Assert.That(additionalSelectedIds.Count, Is.EqualTo(4)); Assert.That(retrySelectedIds, Is.EqualTo(additionalSelectedIds));
  Assert.That(woodToGoldIds.Count, Is.EqualTo(3)); Assert.That(woodToGoldIds, Is.SubsetOf(originalWoodIds));
  Assert.That(allMatchingIds, Is.EquivalentTo(initialEligibleIdleIds)); Assert.That(allMatchingIds, Does.Not.Contain(laterIdleId));
  Assert.That(selectedIds, Is.SubsetOf(eligibleOwnedIds)); Assert.That(selectedIds, Does.Not.Contain(humanProtectedId));
  Assert.That(shortageCommands.Count, Is.Zero); Assert.That(shortageReservations.Count, Is.Zero);
  Assert.That(distributedFarmIds.Distinct().Count(), Is.EqualTo(4));
  Assert.That(afterTakeoverCommanderIds, Does.Not.Contain(takenOverId)); Assert.That(replacementIds.Count, Is.Zero);
  ```
  Include 4 requested/2 idle shortage; three Wood requested/only two eligible; allied/enemy/dead/builder/garrisoned/queued/other-goal workers; existing human Food assignment counted but never commandeered; known/visible/reachable/nondepleted owned sources; live neutral/allied/enemy sheep rejection; four one-worker farms with first full; empty and >bound AllMatching; deterministic repeat with shuffled registry order; takeover after 900 ticks and after partial issuance.
- [ ] Run the five exact methods to behavioral red.
- [ ] Implement quantity computation and eligible-worker selection reusing canonical states, reservations, protected floors and existing path-check budgets. Capture Additional baseline/AllMatching once. TargetTotal is at-least and never removes surplus. Exclude matching already-destination workers from Additional/SelectedCount. Preflight all selected assignments and target capacity before acquiring atomic worker reservations; report shortage/search bound without arbitrary fallback.
- [ ] Integrate planner/manager one-command-per-tick progression. Freeze selection before first command; reserve the entire selection first; revalidate each remaining group before enqueueing; advance cursor only on actual enqueue. Completion observes selected matching assignments. Observe ordinary human enqueue events to mark this goal interrupted permanently for taken-over workers; release reservations through existing terminal/cancel/disposal cleanup. Never substitute other workers to complete an interrupted selection.
- [ ] Rerun the five methods; require green and normal ICommand/CommandBuffer evidence, not direct assignment mutation.
- [ ] Scoped checkpoint: `fix: plan deterministic worker allocation with human authority`.

### Task 4: Typed pending drafts and real chat continuation

**Files:** Draft/continuation row; parser/result/provider integration hooks; tests.

**Interfaces:** Detached `CommanderWorkerAllocationDraft` has the same semantic fields as completed allocation, but nullable Count/Destination only. `CommanderClarificationField { Count, Destination }`. `CommanderPendingClarification` holds copied draft, derived MissingFields, original text, safe question, host sequence/runtime generation, and turn count; host identities are not provider JSON. `CommanderClarificationReplies.TryParseCount(string reply, out int count) -> bool` accepts whole short integer/count-word optionally followed by villagers, not embedded digits. Add optional `CommanderSemanticResult.PendingDraft` and derived missing fields for Clarify only (Nodes empty). Optional `CommanderSemanticProviderRequest.SerializedPendingClarification` contains bounded semantic draft/original/question/reply without runtime IDs. New ChatUI partial owns one pending state; existing SubmitMessageAsync remains the shared public entry point.

- [ ] Write `CountReplies_FillPendingSlotsWithoutProduction`, `MultiFieldReplies_CancelOrBoundAmbiguousContinuation`, `IndependentProduction_SupersedesWorkerClarification`, and `ResetRuntimeAndLateReply_CannotResumeOldPending`. Assertion excerpts:
  ```csharp
  Assert.That(completed.Count, Is.EqualTo(4)); Assert.That(completed.Destination.SourceKind, Is.EqualTo(ResourceSourceKind.Sheep));
  Assert.That(completed.Workers.State, Is.EqualTo(CommanderWorkerState.Idle)); Assert.That(productionGoals.Count, Is.Zero);
  Assert.That(multiFieldCompleted.Count, Is.EqualTo(3)); Assert.That(multiFieldCompleted.Destination.Resource, Is.EqualTo(ResourceType.Gold));
  Assert.That(goalsAfterCancel.Count, Is.Zero); Assert.That(pendingAfterThreeUnsuccessfulTurns, Is.Null);
  Assert.That(independentProductionCount, Is.EqualTo(4)); Assert.That(workerGoalsAfterProduction.Count, Is.Zero);
  Assert.That(oldRuntimeGoalsAfterLateResponse.Count, Is.Zero); Assert.That(newRuntimePending, Is.Null);
  ```
  Exercise `4`, `four`, `four villagers`, Food/Sheep/Idle retention, multi-field `send villagers -> to gold -> three`, cancellation variants before strategic cancel handling, `yes/no/those/there`, malformed/contradictory drafts, resolved-field omission or non-explicit changes, independent `build 4 spearmen`, absent pending, conversation reset, Initialize, match replacement and disposal with a noncooperative late provider.
- [ ] Run all four exact methods to red.
- [ ] Implement strict partial-draft parsing, allowed missing Count/Destination derivation and immutable copies. Completed drafts re-enter normal completed-node parsing/admission. In ChatUI handle pending cancellation/count slots before strategy lifecycle shortcuts. Locally fill only the expected count; for destination/complex replies send separate bounded pending context to the same provider. Independent requests supersede, ambiguous responses re-ask within three turns, unauthorized loss/change of resolved fields rejects. Keep completed count from triggering a duplicate question.
- [ ] Integrate generation/reference/cancellation checks at every async completion; clear pending on all reset/init/disposal/runtime paths. Keep memory/recent results/strategy state separate. Do not add an English economy action parser or alternate voice submission.
- [ ] Rerun the four methods; require green through actual SubmitMessageAsync, not helper-only success.
- [ ] Scoped checkpoint: `fix: retain typed economy clarification across chat replies`.

### Task 5: Provider exposure and three runtime scenarios

**Files:** Provider/context integration points; both new test files. Update schema and clarification docs alongside this deliverable.

**Interfaces:** Extend existing provider capability projection with AllocateWorkers and bounded detached assignment counts; never entity/target IDs. Context and prompt expose precisely the Task 1/4 vocabulary. Keep provider transport/credential discovery unchanged. Existing finalized/mock voice transcript still invokes SubmitMessageAsync.

- [ ] Write `ProviderProjectionAndSchema_ExposeAllEconomyFields` and `SharedTextAndRegressionRoutes_PreserveExistingBehavior`. Assertion excerpts:
  ```csharp
  Assert.That(providerBody, Does.Contain("AllocateWorkers").And.Contain("AllMatching").And.Contain("currentResource").And.Contain("sourceKind"));
  Assert.That(serializedPending, Does.Not.Contain("runtimeGeneration").And.Not.Contain("entityId"));
  Assert.That(explicitIdleCountQuestions, Is.Zero); Assert.That(voiceAllocation, Is.EqualTo(typedAllocation));
  Assert.That(spearmanGoalCount, Is.EqualTo(4)); Assert.That(archerGoalCount, Is.EqualTo(5)); Assert.That(questionCreatedGoals, Is.Zero);
  ```
  Pin existing production, worked-berries Mill placement and counters-spearmen Q&A routing; unchanged legacy tactical/strategic capabilities remain covered by existing Commander suites. Voice test uses one finalized/mock `put four villagers on food` transcript.
- [ ] Run the two exact EditMode methods to red; implement schema/instructions with few examples, consistent capability context and bounded pending field; rerun to green.
- [ ] Write PlayMode methods in `CommanderEconomyClarificationPlayModeTests`: `FourIdleWorkersToFood_UsesNormalCommands`, `FourIdleWorkersToOwnedSheep_PreservesSourceThroughExecution`, `GatherFoodThenCount_UsesRealChatContinuation`. Assert respectively exactly four eligible selected IDs/ordinary gather commands; four owned Sheep assignments despite closer berries plus source-depletion and human-takeover safety; actual chat count question then `4` creates worker goal and four matching assignments, no production goal. Tick normal simulation; observe processed commands and assignments, not preselection only.
- [ ] Run those three fully qualified PlayMode names together, `init_timeout: 120000` milliseconds. Preserve exact job IDs, terminal counts, XML, command/source/selected-ID evidence and final-source hashes. Test-only providers may stabilize these scenarios; they are not real-Luna evidence.
- [ ] Run mandatory live Luna/OpenRouter requests through existing provider/chat with safe typed traces: `put four idle villagers on food`; `gather food from sheep with four idle villagers`; `move three villagers from wood to gold`; `gather food -> four`. Also check observed failure-C wording `gather food with four idle villagers` retains explicit count/Idle. Use one request per explicit case and one initial clarification call with local count fill; bound retries to diagnosed failures, never loop paid calls. Record HTTP/finish status, typed output, admission and observed execution separately; do not print headers/keys or claim costs without measured usage.
- [ ] Scoped checkpoint: `test: verify economy semantics chat and native runtime scenarios`.

### Task 6: Final verification and honest closeout

**Files:** Documentation row. No new feature work. Read-only Luna agents may enumerate actual test names, compare docs to source, compute/check manifests and inspect evidence consistency; root owns final acceptance.

- [ ] Personally map brief sections 1..64 to code/tests/docs. Resolve only relevant remaining blockers; do not expand to Phase 4I, Whisper or an exhaustive hostile audit.
- [ ] On final source, run relevant existing Commander suites once using exact names discovered from the live test inventory. Then run full EditMode and full PlayMode once (unfiltered mode calls; PlayMode init_timeout 120000). Narrowly repair failures, disclose source changes and invalidate affected results; no recycling earlier green jobs against changed source. Persist XML/results even for failures.
- [ ] Complete required five documents: root causes including actual reproduced count-retention discrepancy; schema/modes/source compatibility; pending transitions/bounds/cancellation/reset; precise focused/live/runtime/regression evidence and omissions; final report/verdict. Update Phase4G capability/limitation docs and root roadmap only for genuinely proven results, preserving prior results as historical. Document restricted-packet version/peer compatibility and all unproven standalone/UI evidence explicitly.
- [ ] Generate final manifest of changed runtime/test/doc files with SHA-256, branch/HEAD and dirty-source status; cross-check every test evidence record references the correct source snapshot. Avoid manifest self-hashing loops; manifest excludes itself and consistency summary. Store safe provider traces without secrets.
- [ ] Root checks final diff, current console/compiler state, selected/issued IDs, no unrelated changes, requirements matrix, counts and hashes. Scoped checkpoint: `docs: close out targeted economy clarification repair`.
- [ ] Report exactly `READY FOR FINALIZATION AUDIT` only if mandatory mappings, continuation, authority, source fidelity, real-provider and required regression gates pass. Otherwise report `REQUIRES FURTHER ECONOMY/CLARIFICATION FIX` with precise missing gates. Mark the goal complete only when all required work is actually achieved; stop without starting another phase.

## Plan self-review and execution handoff

Root self-review completed: spec coverage, unambiguous steps, type/signature consistency, all five Review Focus cases and proportionality. Coverage: design §§1–3 -> Task 1; §§4–5 -> Tasks 2–3; §6 -> Task 4; §7 -> Task 5; §8 -> Tasks 5–6. Clarified resolver access to other-goal reservations and typed constructors during self-review. Fifteen focused EditMode methods total (2+2+5+4+2), three PlayMode scenarios; test input loops avoid exploding the test budget. No production repair has been implemented or verified at this planning gate.

Preserve the user's execution method: root/native implementation with cheap read-only Luna support, not per-task core writers. Await human review that this plan captures the approved specification before implementation.
