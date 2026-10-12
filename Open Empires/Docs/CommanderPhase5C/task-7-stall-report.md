# Task 7 full offline EditMode stall — partial, not a completed gate

Observed through 2026-10-12 05:04:10 UTC. This is the sole actual full EditMode job. The first dispatch returned `Could not connect to Unity` without a job; direct native TestJobManager/SessionState inspection found no corresponding active or recent full job and the capture arm unconsumed. Under the parent's explicit recovery ruling, the same 1,797-ID filter was submitted once more, returning job `e2b46c85ca834f0a9bc1523d400da96f`. No other full EditMode job or PlayMode job was launched.

## Frozen scope and evidence

- TestRunner discovery: 1,801 EditMode + 256 PlayMode = 2,057 fullNames. Include 1,797 EditMode + 244 PlayMode; exclude only 4 native-Whisper and 12 live external/paid cases. Ten bounded loopback HTTP cases included. Exact [filter](evidence/task-7-freeze-filter-20261012T043123Z.json) SHA-256 `ca2586362aff8a2aef7372d67d6e98a9502c3eaaa348eea1c4a451a68215c3c1`.
- Windows generated compatibility `source_sha256=d73a2ebf4c2b20d141ecb84815058907f304690049fc7834e0169c3299983157`; Current matched computed input identity before run. Generated JSON SHA-256 `195317aa5e21d1cbaad3e44efbd767ce013a1de20f2cab11a51b601707fc5a4d`.
- [Frozen source/compiled manifest](evidence/task-7-freeze-source-manifest-20261012T0435Z.json) SHA-256 `6599b35c9905d355a937ca03af0b70d4924756d0ee90e7be68951b1d8e093acf`, 2,006 records, -VerifyOnly byte-consistent before run. Five loaded DLL hashes matched the parent preflight identity; post-import TestRunner fullName strings matched exactly.
- Active native capture: `evidence/final-fix-leaves-20261012-044311-438-e0e001e2.json`. [Immutable 1,743-leaf snapshot](evidence/task-7-full-edit-partial-1743-leaves-20261012T0504Z.json) SHA-256 `c479d68ad6b8a7218ffa9b5cd7bf71507a0eefcf47463cf85522c1599a863449`. No same-stem XML exists. Snapshot: 1,723 Passed, 19 Failed, 1 Failed:Error, 0 skipped; all 1,743 captured fullNames belong to the filter, with zero duplicates/extras and 54 expected IDs not captured. Callback `finished=false`.
- Last leaf at 04:53:18 UTC: `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests.NullTransportResponse_RecordsTransportNullNotSuccess`. Next expected: `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests.OlderCancelledLateAttempt_DoesNotOverwriteNewerRequestMetrics`. The active JSON remained unchanged for over ten minutes. `get_test_job` first returned progress to 415, then timed out (`Timeout receiving Unity response`), and a later same-handle call closed (`Connection closed before reading expected bytes`). The editor resource became unresponsive/partial. None is terminal result proof.

## Nonpassing leaves recorded before the stall

Native capture bounds each message at 500 characters; the text below is exact within that capture bound. Full names and states remain in the immutable JSON.

1. `OpenEmpires.Tests.CommanderGrandFixEvidenceLaneTests.RejectedCompletion_RecordsSafeFinishCategory("length","length")` — Failed
   -   Expected: String containing "finish=length"   But was:  <string.Empty> 

2. `OpenEmpires.Tests.CommanderGrandFixEvidenceLaneTests.RejectedCompletion_RecordsSafeFinishCategory("private-finish-marker","other")` — Failed
   -   Expected: String containing "finish=other"   But was:  <string.Empty> 

3. `OpenEmpires.Tests.CommanderGrandFixPresentationTests.ReadableApprovalCard_PreservesWholeScrollableTextAndDetailsDoesNotApprove` — Failed
   -   Expected: not null   But was:  null 

4. `OpenEmpires.Tests.CommanderPhase4ATests.OpenRouterSemanticRequest_TransmitsSupportedPlacementSchema` — Failed
   -   Expected: String containing "\"placement\""   But was:  "{"model":"openai/gpt-6-luna","messages":[{"role":"system","content":"JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; after/then=completion dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new; a/an=

5. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(400,"request was rejected")` — Failed
   -   Expected: String containing "request was rejected"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

6. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(401,"authentication")` — Failed
   -   Expected: String containing "authentication"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

7. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(402,"credits")` — Failed
   -   Expected: String containing "credits"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

8. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(403,"forbidden")` — Failed
   -   Expected: String containing "forbidden"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

9. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(404,"model unavailable")` — Failed
   -   Expected: String containing "model unavailable"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

10. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(408,"timed out")` — Failed
   -   Expected: String containing "timed out"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

11. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(429,"rate-limited")` — Failed
   -   Expected: String containing "rate-limited"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

12. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.HttpFailures_AreDistinctAndDoNotEchoUpstream(503,"provider unavailable")` — Failed
   -   Expected: String containing "provider unavailable"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

13. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.LengthFinishReason_RejectsEvenIfContentLooksValid` — Failed
   -   Expected: String containing "truncated"   But was:  "Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question." 

14. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.NaturalParaphrase_ReturnsSameTypedUnitRequest("I want a total of ten spears in my army.")` — Failed
   -   Commander input limit: the complete request and required canonical context cannot fit safely; nothing was submitted. Edit the request or resolve its pending question.   Expected: True   But was:  False 

15. `OpenEmpires.Tests.CommanderPhase4E1ProviderTests.OneCompleteJsonFence_ParsesButAmbiguousPayloadDoesNot` — Failed
   -   Expected: True   But was:  False 

16. `OpenEmpires.Tests.CommanderPhase4GCapabilityTests.CapabilityGoal_MilitaryCommandUsesNormalCommandBufferAndRemainsExecuting` — Failed
   -   Expected: Executing   But was:  Blocked 

17. `OpenEmpires.Tests.CommanderPhase5AAntiGravityHostileAuditTests.PrimitiveCatalog_ExactlyNineRegisteredPrimitives` — Failed
   -   Expected: 9   But was:  10 

18. `OpenEmpires.Tests.CommanderPhase5AProviderContractTests.DynamicFarm_UsesRegistryVocabularyAndReturnsTypedPlan` — Failed
   -   select-workers   Expected: String containing "select-workers"   But was:  "JSON only,strict fields/enums; never authorize IDs,coords,commands,hidden/extra goals. Keep counts,negations,actors,sources,restrictions,order,exact refs;unknown unknown. Request{outcome:Request,nodes:1..4,executionOrder:Independent|Sequential|DependencyGraph}; after/then=completion dependsOn:[prior0-based]; resultFromNode=exact-new,producerFromNode=explicit-new; a/an=1,number words,left=MapWest. Clarify{outcome:Clari

19. `OpenEmpires.Tests.CommanderPhase5AProviderContractTests.RepeatedPlacementAndExactProducers_PromptIncludesStrictDynamicExample` — Failed
   -   System message must route repeated placement through DynamicPlan.   Expected: True   But was:  False 

20. `OpenEmpires.Tests.CommanderPhase5AProviderContractTests.SharedWorkerComposition_PromptIncludesOneSelectionAndStrictExample` — Failed:Error
   - System.ArgumentOutOfRangeException : Index was out of range. Must be non-negative and less than the size of the collection. Parameter name: index

## Classification and recovery boundary

- Confirmed by native failure messages: eleven Phase4E1 provider fixture cases expected injected HTTP/semantic outcomes but instead received input-limit preflight rejection (`complete request and required canonical context cannot fit safely; nothing was submitted`). No external/paid call was made by these injected fixtures.
- Additional recorded failures span evidence-lane finish text (2), approval presentation (1), placement/prompt contract, capability status, and stale primitive count. See each exact message above; causal fixes are not established by this partial run.
- The next async overlap fixture's source awaits a second provider request before releasing its first transport Task. Given the confirmed preflight-rejection cluster, a first request rejected before transport would make the second request the fixture's first pending transport call and could hang. This is a source-based inference, not a native stack trace or proven cause.
- Parent ruled: human must cancel the current Unity Test Runner job (or close/reopen preserving work) to recover. Do not force-kill/restart, clear-stuck as if it stopped the test, source-edit, dispatch another Edit run, or start PlayMode/builds while this job is active/unknown. After human action, confirm authoritative native terminal/missing job state and retain any further XML/JSON; no automatic second full run is authorized. Paid ledger remains 0/10.
