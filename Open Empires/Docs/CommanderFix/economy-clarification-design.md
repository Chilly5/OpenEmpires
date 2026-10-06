# Targeted economy and clarification repair — design specification

Date: 2026-10-06. Status: implementation approved with user amendments: ordinary counted orders use SelectedCount; native/network source restriction is conditional on runtime proof. Execution in progress, not verified yet.

## 1. Purpose and boundary

Repair natural worker orders and missing-field follow-ups after Phase 4G/4H. This is not Phase 4I. The source inventory and real-Luna pre-fix reproduction are recorded in `economy-clarification-root-cause.md` and `baseline-reproduction-2026-10-06.json`.

The approved approach is an isolated `AllocateWorkers` semantic/intent/goal path, typed worker and resource selectors, and structured pending clarification. Preserve existing `SetResourceAllocation` desired-state goals, automatic production-prerequisite gathering, strategic approval, accepted tactical capabilities, Q&A, and voice/STT behavior.

Luna supplies criteria, not authority. Completed semantic data still passes strict parsing, admission and Commander validation. Game-side code resolves concrete owned workers and legal targets, then submits ordinary commands through CommandBuffer. No provider entity IDs, coordinates, players, reservations, callbacks or command payloads.

The alternative of adding all semantics to the old allocation goal was rejected because its at-least desired-total behavior and strategic prerequisite policy must remain compatible. A phrase-specific gather parser was rejected because it would not repair the generic model.

## 2. Typed semantic contract

Add a node `AllocateWorkers`, with these fields:

- `mode`: `TargetTotal`, `Additional`, or `SelectedCount`.
- `countMode`: `Exact` or `AllMatching`.
- `count`: integer 1..200 when Exact; omitted when AllMatching. Admission also checks the owning simulation's population bound.
- `workers`: an allow-listed object with `state` = `Any`, `Idle`, or `Gathering`; optional `currentResource` = Food/Wood/Gold/Stone, permitted only with Gathering.
- `destination`: an allow-listed object with `resource` = Food/Wood/Gold/Stone; `sourceKind` = `Any`, `Sheep`, `Berries`, `Farm`, `Tree`, `GoldMine`, or `StoneMine`. Omission means Any.
- Existing bounded `dependsOn` is permitted. No producer/result reference is introduced for allocation in this repair.

Validate enum names, field combinations, integer types/bounds, duplicate/unknown properties and depth using the existing strict JSON discipline. Reject Food/Tree, Gold/Sheep, and other incompatible resource/source combinations. Reject currentResource without Gathering, AllMatching with a numeric count, and a constrained worker selector with TargetTotal. AllMatching uses SelectedCount and requires Idle or Gathering, not an unqualified all-owned-workers order.

Example completed node:

```json
{
  "type": "AllocateWorkers",
  "mode": "SelectedCount",
  "countMode": "Exact",
  "count": 4,
  "workers": { "state": "Idle" },
  "destination": { "resource": "Food", "sourceKind": "Sheep" }
}
```

These are semantic enums/projections, not a duplicate canonical gameplay database. Keep all existing legacy node forms valid and unchanged.

## 3. Quantity meaning

| Player meaning | Mode and deterministic behavior |
| --- | --- |
| Put 4 villagers on food, no explicit worker eligibility | SelectedCount/Exact: select and assign exactly four eligible workers for this request, even when six workers are already gathering Food. |
| Make sure I have / keep 4 villagers on food | TargetTotal/Exact: desired total, using the existing at-least convention; do not remove surplus workers. |
| Put 4 more villagers on food | Additional/Exact: assign four additional eligible workers not already assigned to the matching destination. Capture the baseline once game-side; retries cannot repeatedly add four. |
| Put four idle villagers on food | SelectedCount/Exact: select and assign four eligible idle workers; existing busy Food workers cannot satisfy the requested four idle workers. |
| Move three villagers from wood to gold | SelectedCount/Exact with Gathering/Wood: transfer three matching workers, not Food workers or arbitrary idle workers. |
| Send idle villagers to berries | SelectedCount/AllMatching with Idle: snapshot all eligible matching workers, within the explicit bound; no numeric clarification. |
| Put more villagers on food | Additional/Exact draft with missing count: ask how many additional villagers. Do not invent a quantity. |
| Send idle villagers to work | AllMatching Idle draft with missing destination: ask which resource. Preserve the all-idle meaning; do not invent an economy distribution. |

Additional excludes workers already assigned to the same destination/source. SelectedCount selects exactly the requested number of eligible workers, and may reassign eligible workers already on that destination; it never becomes a target-total no-op. A different requested Food subtype can require a transfer. TargetTotal counts living owned active assignments to the requested destination, including assignments it must not take over. It never gains control of those existing workers merely by counting them.

AllMatching captures its eligible set once, not an ongoing policy that commandeers future idle workers. If the matching set exceeds the bound, block and explain the bound rather than silently truncating it. An empty set produces an explicit no-eligible-workers result, not a numeric clarification.

## 4. Worker authority and planning

Use the existing worker authority/reservation infrastructure and canonical gathering states. Any permits currently reassignable idle/gathering workers, not builders, soldiers, garrisoned/dead units or arbitrary queued tasks. Gathering/currentResource uses actual target/carried-resource assignment information, not text or unit appearance.

Filter for owning player, living villager type, requested state/resource, eligible command queue, human-protection lease and another goal's reservations. Preserve existing protected-resource floors when applicable.

Before the first command, resolve the complete requested worker/target assignment and acquire reservations atomically. If fewer than the requested number are eligible or there is inadequate legal target capacity, issue no partial initial batch; block with the available count/reason. Do not silently use busy workers to satisfy Idle or another resource to satisfy Gathering/Wood.

Order candidates with deterministic integer/fixed-point scores: existing eligibility priority, distance, worker ID, target kind and target ID. Bound expensive path checks using the existing planning-budget pattern. A bounded search failure is reported as such, not proof that the entire map is unreachable.

Record selected/issued worker IDs only internally for execution evidence. Completion requires observing the selected workers' actual legal matching assignments, not merely seeing any N workers on Food. Track issued commands so a retry cannot duplicate the batch.

A human command releases/protects its worker through existing authority. The allocation goal records that takeover and does not reacquire or replace that selected worker to finish the same order, even after the ordinary lease expires. Report the interrupted/blocking result; a new player order is a new decision. If takeover occurs after some assignments already executed, report that actual partial/interrupted state rather than claiming initial atomicity means execution is transactionally reversible.

## 5. Canonical resource/source projection

Project from existing gameplay state; do not migrate canonical data or add an AI-maintained fact table:

- Any: legal known nodes yielding the requested ResourceType, with deterministic ranking. For generic Food, also consider legal owned sheep through the slaughter path.
- Sheep: living owned `IsSheep` units via SlaughterSheepCommand; existing Food carcasses via GatherCommand. Current carcass creation is the canonical sheep-slaughter path.
- Farm: non-depleted linked farm nodes whose actual owned building is complete, living and usable; one-worker capacity.
- Berries: currently registered ordinary Food nodes, excluding farm/carcass flags; inventory establishes that current map registration produces these from berry sources. Do not infer new future food kinds as berries without updating authoritative registration/projection.
- Tree, GoldMine, StoneMine: canonical current Wood, Gold and Stone node registrations, respectively.

Require known visible, existing, non-depleted targets, legitimate ownership/control rules, and a reachable legal gathering/slaughter route. Reject neutral, allied-but-not-owned and enemy live sheep. Do not issue a sheep-conversion or capture order implicitly. Preserve the game's slaughter-to-carcass transition; there is no invented drop-off/proximity prerequisite.

Model farm capacity and reserved assignments during preflight so workers can be distributed across multiple matching farms/nodes. Try other matching legal sources within the bounded candidate set when the first source is full. A specific-source request never becomes generic Food as fallback.

### Execution-time source fidelity

Current native gather/retarget code can redirect by coarse ResourceType. First implement the semantic path with existing ordinary gather/slaughter commands, then test runtime source fidelity, including depletion/retargeting. Static suspicion alone does not authorize native/network changes.

Only if runtime evidence proves existing machinery cannot preserve an explicit source constraint, carry the smallest necessary bounded restriction through the existing gather/slaughter command path and active worker order. Default unrestricted behavior preserves legacy/manual commands. Restricted orders filter execution-time redirection, farm substitution, depletion retargeting and sheep-to-carcass continuation by the same canonical source projection. If no matching legal source remains, stop/wait with an explicit blocker rather than silently using berries instead of sheep.

This is a narrowly guarded extension of existing commands, not a new direct-mutation path or global gather-policy rewrite. Any added command field must roundtrip through both existing serializer forms and queued-command handling; maintain an explicit compatible default for legacy payloads. Clear/replace restrictions when a later human order takes control, following normal queue/override semantics. If the existing binary protocol needs version handling, make that change explicit and cover it; never silently reinterpret old packets. These changes trigger the final shared-infrastructure regression gate.

## 6. Structured clarification

Add a detached partial allocation DTO distinct from executable nodes. A Clarify outcome may include one allow-listed allocation draft and a bounded missing-field list; its executable Nodes remain empty. Derive/verify missing fields against the typed draft rather than trusting provider labels alone. Completed drafts must go back through the ordinary completed-node parser/admission gate.

Example:

```json
{
  "outcome": "Clarify",
  "message": "How many villagers should I put on food?",
  "pending": {
    "type": "AllocateWorkers",
    "mode": "SelectedCount",
    "countMode": "Exact",
    "workers": { "state": "Any" },
    "destination": { "resource": "Food", "sourceKind": "Any" }
  },
  "missingFields": ["Count"]
}
```

PendingClarification stores a host-generated request sequence, owning runtime/conversation generation, original player text (existing 1024-character bound), copied resolved semantic fields, missing Count/Destination fields, last safe question and continuation-turn count. Only one pending request exists; at most three continuation turns are allowed. This is not strategic approval state, recent-result memory or free-text transcript authority.

For an expected count slot, locally recognize a whole short integer/count-word answer (such as 4, four, or four villagers), validate its bound, and fill only Count. Preserve Food/Sheep/Idle/mode and every other resolved field. This local slot parser is not a general language-to-game command interpreter.

For destination/more complex short replies, use the same semantic provider with a bounded typed pending projection, original request/question and new reply. It returns a completed allocation, another validated draft, cancellation, or an independent new request. Resolved fields cannot disappear silently during continuation. A changed resolved field needs explicit player correction, otherwise reject/re-ask within the turn bound.

Pending-specific slot filling and cancellation are checked before unrelated strategic lifecycle shortcuts, so cancel while answering a worker question clears that draft rather than cancelling a strategy. Whole-form cancel/never mind/forget it clears pending state and creates no goal. A clearly independent command such as build 4 spearmen supersedes pending clarification and enters the normal provider/admission path; never extract its 4 as the old worker count. Yes/no/there/those cannot become an invented count or target. Without a compatible pending request, short replies follow normal safe clarification behavior. After the third unsuccessful continuation, clear the draft and explain that a fresh request is needed; do not loop indefinitely.

Clear pending state on conversation reset, match/runtime replacement, Initialize and disposal. Reuse existing generation, provider/simulation/manager/dispatcher identity and cancellation checks to reject stale async responses. A late response from Match A must not create or complete state in Match B.

## 7. Provider integration and UX

Expose AllocateWorkers, count modes, worker eligibility and source kinds consistently in system instructions and bounded provider capability projections. Use only a few representative examples; do not add a long English phrase list. Include detached worker-allocation counts when useful; baselines and IDs remain game-side decisions.

Keep pending clarification in a separate bounded provider field, not overloaded into general semantic memory. Do not put runtime IDs or Unity references in serialized pending context. Clarification messages remain plain text within the existing 180-character message bound.

The real provider must extract four/Idle/Food/Sheep and three/Gathering/Wood/Gold correctly from the required requests. A valid completed count cannot trigger a duplicate count question. Diagnose incomplete provider output using safe typed output and prompt/schema evidence; do not claim a hardcoded phrase workaround is a fix.

Typed input, Send/Enter and a finalized voice transcript all use the same SubmitMessageAsync path. No voice-specific economy interpreter or Whisper work.

## 8. Verification and closeout

Preserve the pre-fix evidence. Use roughly 8-15 focused EditMode tests grouped around:

1. The five required semantic mappings: put 4 villagers on food; put four idle villagers on food; gather food from sheep with four idle villagers; move three villagers from wood to gold; send idle villagers to berries. Assert their quantity modes, counts/AllMatching, worker state/current resource, destination/source and schema/DTO roundtrips.
2. Unknown/incompatible fields, count bounds and no concrete provider authority.
3. Numeric and word-count follow-ups retaining Food/Sheep/Idle.
4. Multi-field continuation, cancellation, ambiguous replies and independent production escape.
5. Reset/runtime replacement and late-response rejection.
6. Owned/alive/idle/current-resource filtering, human protection and reservations.
7. Specific-source legality, ownership, depletion, reachability, capacity/distribution and deterministic selection.
8. Native restricted command/serializer/queued-order fidelity and compatible legacy defaults.

Run three focused PlayMode/runtime scenarios with actual command evidence: four idle workers to Food; four idle workers to owned Sheep while closer berries exist; gather food -> count question -> 4 through the real chat path. Record selected IDs, targets/source kinds, ordinary commands and observed assignment states. Include a targeted human takeover/depletion assertion rather than claiming preselection proves source fidelity.

Real Luna/OpenRouter calls are mandatory for the three explicit required requests and gather food -> four. Capture safe typed output and final chat/execution evidence; never keys. Keep calls bounded and conservative.

Check routing for build 4 spearman, make 5 archers, Mill near worked berries, counters-spearmen Q&A, and one finalized/mock voice transcript to the shared text path. No voice-specific debugging.

Run relevant Commander suites after focused tests. Because the strict source guard touches native gathering/serialization and shared semantic admission, run full EditMode and PlayMode once on final source, not repeatedly during development. Failed focused cases are rerun narrowly while repairing; a final source-changing fix after the full run invalidates the affected final verification and must be disclosed/rechecked proportionately.

Required documentation in Docs/CommanderFix: root-cause report, economy-semantic-schema.md, clarification-state.md, focused-test-evidence.md and fix-report.md. Update implemented-capabilities.md, known-limitations.md and repository-root remaining_work.md with actual results. Preserve historical evidence as historical; do not attach old green jobs to new source hashes.

Final verdict is exactly READY FOR FINALIZATION AUDIT only after the brief's mappings, clarification, authority, ordinary-command execution, source constraints and regressions are genuinely proven. Otherwise report REQUIRES FURTHER ECONOMY/CLARIFICATION FIX and list the missing gates. Do not start Phase 4I or mark the active goal complete based only on this specification.
