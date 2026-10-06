# Economy / clarification root-cause investigation

Date: 2026-10-06. Baseline investigation preserved below; production repair and focused/live verification now implemented. Final regression/closeout status is tracked in `execution-progress.md` and `focused-test-evidence.md`.

## Scope and source baseline

Targeted post-4G/4H acceptance repair, not Phase 4I. Voice/STT remains out of scope except a later shared-text-path check. Existing dirty production, UI, voice, package, tests and handoff documentation are preserved.

Branch: `unit_models_and_voice_control`.
HEAD: `b96cf9b622f796df5c90f402b2bb16720c52f78e`.
Complete pre-edit status: [baseline-git-status.txt](baseline-git-status.txt).
Fresh safe reproduction: [baseline-reproduction-2026-10-06.json](baseline-reproduction-2026-10-06.json).

## Fresh reproduction before any production edits

Four real OpenRouter/Luna calls were made through a temporary CommanderChatUI host and public SubmitMessageAsync. The probe used a detached simulation like existing chat tests; manager ticks were not run. This proves translation/chat/admission behavior, NOT in-game gathering or a standalone acceptance result.

| Case | Fresh observed behavior | Conclusion |
| --- | --- | --- |
| A: gather food from sheep | Unsupported: "I can’t specify gathering from sheep with the available actions." | Reproduces the source-selector schema gap. |
| B: gather food, then 4 | First asks count. Follow-up creates EnsureUnitCount / "Preparing 4 villagers." | Still broken, with a more dangerous wrong-action variant than the reported repeated question. |
| C: gather food with four idle villagers | Admits a resource-allocation target of 4; no idle selector survives. | Count extraction succeeded in this sample. The exact repeated-question symptom did not recur; idle semantics are structurally unrepresentable. |

All four HTTP responses were 200, finish=stop; no timeout, truncation or invalid JSON classification. The provider traces and safe transcripts are captured. Credentials were neither printed nor copied into artifacts. Current tracing did not retain actual billed tokens/cost; these are unknown.

Additional in-memory parser probes: basic Food/count=4 is valid; the same allocation with workerState=Idle, sourceKind=Sheep, or currentResource=Wood is rejected by the existing allow-list.

## Answers required by the brief

| Question | Live-source finding |
| --- | --- |
| Generic allocation exists? | Yes: SetResourceAllocation, with resource and count. It is not a complete worker-order model. |
| Numeric count representable / parsed? | Yes, integer 0..200 in semantic JSON; parser to admission to DTO retains it. |
| Idle worker selector representable? | Not in current semantic allocation. Legacy intent constraints have PreferredWorkers/IdleOnly, but the semantic schema cannot supply it. |
| Workers currently gathering X representable? | No allocation selector field; no previous-resource constraint in semantic schema. |
| Destination resource representable? | Yes: Food/Wood/Gold/Stone. |
| Destination source kind representable? | No. Sheep/Berries/Farm/Tree/GoldMine/StoneMine are absent from allocation JSON. |
| Food from sheep or berries? | Cannot request either subtype. Generic Food mixes nodes; live sheep require a distinct canonical slaughter path. |
| Gold from GoldMine? | Gold is expressible, but no explicit typed source-kind assertion. Canonical map-generated Gold nodes are mines. |
| Allocation exposed to Luna? | Coarse SetResourceAllocation form appears in the system instruction. actionCapabilities omits allocation in the projected capability array, although the instruction lists it separately. |
| Does Luna receive count/idle/source schema? | Receives resource/count only. "Do not add fields" prevents representing idle/source criteria faithfully. |
| Does clarification preserve original semantic request? | No. Clarify allows only outcome/message; its nodes are empty. Only the displayed question is stored. |
| Does short next reply merge with it? | No typed merge. It goes through a fresh provider call with PlayerMessage="4". |
| Is semantic memory sufficient? | No. It contains accepted unit/building facts and clarification text, not resolved economy fields or missing slots. |

## Responsible layers and source evidence

Paths below are repository-relative; line locations refer to the original investigated pre-fix snapshot, not the subsequently changed runtime source.

- `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs:14-29,78-96`: allocation node exists; no worker-state, previous-resource, source-kind, or quantity-mode fields.
- `.../CommanderSemanticJson.cs:169-173`: allocation permits only type/resource/count/dependsOn. Non-Request outcomes allow only outcome/message and discard executable nodes.
- `.../CommanderSemanticAdmission.cs:44-53`: allocation hardcodes ResourceAllocationMode.SetExact and retains resource/count only.
- `Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs:33,39-46,163-169`: matching limited schema; memory provided as untrusted prior facts; no typed incomplete economy request.
- `.../CommanderChatUI.cs:262-264,399-414,454-461`: each message enters fresh semantic translation; Clarify records only SafeExplanation.
- `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticConversationMemory.cs:9-29,95-100`: accepted unit/building facts and question text only.
- `.../CommanderSemanticProvider.cs:69-91`: bounded context excludes existing worker-allocation snapshots.
- `Assets/Scripts/AI/Commander/CommanderIntent.cs:34-50,76-85,132-146`: lower layers already support SetExact/Increase and IdleOnly constraints.
- `.../CommanderGoalManager.cs:153-164`: Increase derives a live destination-worker baseline game-side.
- `.../CommanderPlanner.cs:358-365`: legacy allocation is an at-least desired-total goal (does not remove surplus workers), not an arbitrary direct transfer action.
- `.../CommanderPlanner.cs:917-965,1171-1230`: reusable generic gather planner and stable bounded worker/resource pair selection, but no source-kind selector.
- `.../CommanderWorkerAuthority.cs:24-63,113-158`: goal reservations and existing 900-tick protection after human commands.
- `Assets/Scripts/Commands/GatherCommand.cs:3-17` and `SlaughterSheepCommand.cs:3-10`: existing ordinary ICommand boundaries.
- `Assets/Scripts/Core/GameSimulation.cs:1850-1896,2694-2763`: slaughter requires exact sheep ownership, then creates a Food carcass and starts gathering.
- `Assets/Scripts/Map/ResourceNodeData.cs:6-33`: canonical coarse ResourceType, farm linkage and IsFarmNode/IsCarcass flags; no canonical universal resource-source enum.
- `Assets/Scripts/Map/MapData.cs:374-408`: normal, carcass and farm node construction.
- `Assets/Scripts/Map/MapRenderer.cs:963,1039,1062,1077`: map-generation source associations: trees/Wood, berries/Food, gold/Gold, stone/Stone.
- `Assets/Scripts/Core/GameSimulation.cs:3277-3399`: normal gather owns farm capacity and path/adjacency assignment. Its fallback is coarse-resource-based and must be considered when proving explicit source constraints; preselection alone is not a complete proof against execution-time redirection.

## Why the three reported failures occur

A: Sheep is not a valid destination subtype in the exposed allocation model. Luna's Unsupported response is consistent with that actual capability boundary, not a transport failure.

B: The incomplete request is not persisted as typed state. The next number is a separate language request with only a question in semantic memory. It can yield another question or an unrelated unit-production intent. A fresh run proved the latter.

C: A plain numeric count is not structurally lost by the current parser. This reproduction retained 4, but the schema cannot retain Idle. The previously observed count question is not proven to be a count-serialization bug. The inadequate representational schema/prompt leaves the model unable to return the complete requested worker intent; capture fresh post-fix semantic output to establish both count and state, rather than guessing about model reasoning.

## Existing rules to preserve

Human commands release reservations and protect workers. Only living owned villagers with eligible queues/control may be selected. Known visible, non-depleted reachable targets are required; selection must be deterministic and capacity-aware.

Sheep are units and must be owned before slaughter; neutral/allied/enemy sheep cannot be treated as a valid owned destination. Farms have one-worker capacity. Carcasses are explicitly tagged. Berries are the current generated non-farm/non-carcass Food nodes; this is a projection of current registration data, not permission to label every future Food kind as berries.

Do not migrate canonical data or use language-selected entity IDs/coordinates. Source-constrained resolution must not silently fall back to another food subtype. Existing strategic prerequisite gathering is a different policy from direct player worker orders.

## Approved architecture and implementation

Repair the generic model, not phrases: typed worker criteria, typed resource-source projection, deterministic legal-worker/target selection, and bounded pending clarification with resolved fields and missing slots. Preserve legacy SetResourceAllocation/strategic behavior; prefer an isolated direct AllocateWorkers path rather than changing old desired-state semantics silently.

The user approved this architectural approach and implementation plan on 2026-10-06, with ordinary counted orders interpreted as SelectedCount and native serialization conditional on runtime proof. The contract and verification gates are in [economy-clarification-design.md](economy-clarification-design.md) and the [implementation plan](economy-clarification-implementation-plan.md).

The clarification state should be owned by the chat/runtime, not reused strategic approval state or general result-reference memory. Simple count replies can fill a typed missing count locally; other replies require a bounded continuation/new-request decision. Every completed request still goes through existing strict admission and normal Commander/CommandBuffer execution.

The generic AllocateWorkers path, source selectors and typed pending clarification are implemented. Fifteen focused EditMode methods, three controlled PlayMode scenarios and five live Luna requests have passing evidence. These are not standalone/UI proof or a finalization verdict. The shared-infrastructure final regression and documentation/consistency gates remain tracked separately.

## Additional native root cause established during implementation

An actual processed Sheep carcass GatherCommand switched to a berry node when its carcass depleted after the goal had completed. `task2-runtime-gate-sheep-to-berries-red.xml` preserves this reproduction. Native automatic retargeting knew only coarse Food, so preselection alone could not preserve explicit source-kind semantics. This triggered the approved conditional native restriction and restricted version1 packet path, retaining unrestricted binary legacy bytes.

The fresh scoped review then found an automatic redirect calling ClearCommandQueue, which reset the new restriction. The failing redirect regression and subsequent green focused set prove preservation on that automatic path. A second review regression proved that explicit Idle criteria corrections were initially rejected; guarded explicit worker corrections now pass while unsolicited loss/change still rejects.
