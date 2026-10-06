# Bounded worker clarification

Implementation contract, 2026-10-06. Passing evidence belongs in `focused-test-evidence.md`.

Clarification is one detached incomplete AllocateWorkers request, not a goal, strategic preview, result binding, or free-text conversation memory. Only Count and Destination may be missing. The provider's missingFields list must exactly match fields derived from the strict draft; executable Nodes are empty.

```json
{"outcome":"Clarify","message":"How many villagers should I put on food?","pending":{"type":"AllocateWorkers","mode":"SelectedCount","countMode":"Exact","workers":{"state":"Any"},"destination":{"resource":"Food","sourceKind":"Any"}},"missingFields":["Count"]}
```

The draft retains mode, count mode, worker eligibility/current resource and destination/source. Host state additionally retains original text (maximum 1024 characters), question (maximum 180), runtime generation, sequence and reply count. Host identities never enter the provider payload. Serialized pending context is a separate bounded semantic projection (maximum 4096 characters), not recent semantic memory.

## Transitions

| Input / event | Result |
| --- | --- |
| Valid Clarify with partial allocation | Retain one typed draft, display question; no gameplay goal. |
| Whole count such as 4, four, four villagers | Fill only missing Exact count locally, preserve all other criteria, then re-enter strict completed parsing/admission. No extra provider request. |
| Count supplied but destination still absent | Retain updated partial draft and ask for resource. |
| Destination/complex reply | Same semantic provider gets the separate bounded pending projection; resolved criteria cannot silently disappear/change. |
| cancel, never mind, nevermind, forget it | Clear pending before strategic cancellation handling; no goal created. |
| Clearly new standalone action, e.g. build 4 spearmen | Supersede pending and process the full message normally, never extract its embedded 4 as a worker count. |
| yes/no/those/there | Re-ask safely, do not invent criteria. |
| Invalid/unresolved continuation | Preserve prior draft and re-ask; after three continuation replies expire it. |
| ResetConversation / Initialize / match replacement / disposal | Clear pending; generation, runtime/provider/manager references and cancellation reject late results. |

Local count parsing accepts only a bounded whole numeric or supported small number-word reply, optionally followed by villager(s); it does not scan embedded digits or interpret economy actions. Invalid count bounds still fail strict allocation validation. There is no alternate voice economy route: finalized voice text uses SubmitMessageAsync.

Resolved mode and already-resolved count cannot disappear/change through slot filling. Worker eligibility corrections require the new state to be explicitly named; a changed currentResource requires an explicit `from <resource>` confirmation and cannot silently disappear. A destination/source replacement requires an explicit matching resource/source token in the current reply; otherwise retain the earlier facts and reject the update. Independent-turn detection is routing only and never creates commands. Unrecognized unrelated wording fails safely rather than coercing a different action into worker allocation; users can cancel or supply a new complete action.

Late noncooperative provider work is observed for faults but cannot restore pending state or admit a goal in another runtime. A completed compatible draft clears pending and passes the ordinary admission/dispatcher path, which still owns player authority, population bounds and execution.
