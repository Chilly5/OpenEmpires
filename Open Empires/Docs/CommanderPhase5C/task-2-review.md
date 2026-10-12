# Phase 5C Task 2 review

## Spec Compliance

- **❌ Issues found.** The bounded partial graph, strict parser probes, original-ticket continuation, ordinary-result admission, and distinct compound/future approval are implemented (`Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs:28-85`; `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:451-471,526-553`). The explicit **known-field correction** requirement is not: a reply such as “a Mill instead” cannot change the proposed Mine's `structure` while retaining the other fields. `Resolve` only matches the offered value, and every nonlocal response to a retained graph is rejected (`CommanderRequestGraphDraft.cs:87-129`; `CommanderChatUI.Clarification.cs:79-93`). This is acknowledged in `task-2-report.md` but remains a Task 2 requirement.
- **❌ Zero-HTTP continuation is incomplete.** When a graph reply is not recognized locally (for example `yes` to an offered source question, or a corrected name), `localGraph` is null and `RequestSemanticTranslation` is called (`CommanderChatUI.cs:467-471`). Its result cannot update or resolve the retained graph because the handler accepts only `localGraphResult` (`CommanderChatUI.Clarification.cs:79-93`). This can spend a provider attempt and wait up to the unchanged timeout for a response that is necessarily discarded. Retained graph replies should be handled locally as resolution or a bounded re-ask; only a genuine independent command should start a new translation.
- **⚠️ Cannot verify from this task's evidence:** runtime replacement, foreign-owner, and replayed **draft** rejection. The new graph tests cover reset and orphan `yes` (`Assets/Tests/EditMode/CommanderEconomyClarificationTests.Graph.cs:155-172`), while the reported five authority controls exercise approval/retained approved graphs, not a foreign or replayed pending draft (`Docs/CommanderPhase5C/evidence/task-2-authority-green.json`). Add focused draft-boundary tests before claiming that RED→GREEN item closed. Actual endpoint input-token usage is also unknown; the strict conservative estimate is preserved, not hard-cap certification (`CommanderSemanticRequestPack.cs:16-49`). Task 1 accounting and Tasks 3–7 native/UI gates are separate.

## Strengths

- The graph proposal is bounded to 1–4 nodes/slots, rejects unknown graph/slot fields, probes every offered alternative through the existing strict Request parser, and does not store temporary completion values as facts (`CommanderRequestGraphDraft.cs:28-85`).
- A local reply changes one missing leaf and reparses the remaining draft; ambiguous replies do not select a slot (`CommanderRequestGraphDraft.cs:87-115`). The House→future-five test checks order/dependency/count/producer/source retention and no goals before a later approval (`CommanderEconomyClarificationTests.Graph.cs:45-77`).
- Plan preparation now uses the ticket's original input, and compound/future requests enter preview instead of immediate admission (`CommanderChatUI.cs:526-553`). The source/amount test checks `400`, `AdditionalGathered`, and current Wood criteria (`CommanderEconomyClarificationTests.Graph.cs:129-153`).
- The saved EditMode results confirm 40/40 before the final multi-slot extension, then 9/9 affected tests and 5/5 existing authority controls after it (`Docs/CommanderPhase5C/evidence/task-2-final-focused-green.json`, `task-2-multislot-final-green.json`, `task-2-authority-green.json`). These are fixture/application checks, not live-model or native-runtime proof.

## Issues

### Critical (Must Fix)

- None found in the reviewed Task 2 diff.

### Important (Should Fix)

- `Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs:117-129`; `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs:79-93` — Explicit corrections of a known graph field are impossible. Implement a constrained correction path that diffs only the named field against the retained untrusted graph, validates the entire result, and preserves the original ticket/other fields; cover it with a correction test.
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:467-471`; `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Clarification.cs:79-93` — An unrecognized continuation makes a provider call even though that provider's result is always rejected for an active graph. Keep graph clarification local and re-ask/expire without HTTP; retain the existing independent-command supersession route.

### Minor (Nice to Have)

- `Assets/Scripts/AI/Commander/Economy/CommanderRequestGraphDraft.cs:20-26,147-153` — `MissingSlots` exposes read-only collections of mutable `JToken` alternatives. The stored graph is cloned, but the advertised slot alternatives are not deeply immutable. Return detached values or private immutable primitives so later callers cannot mutate a validated pending draft.

## Assessment

**Task quality: Needs fixes.** The main graph parsing and approval boundary are cleanly reused, and the reported focused checks pass, but the explicit correction behavior is absent and the local continuation path can make pointless paid calls. The missing draft-specific identity tests leave that safety claim unverified.
