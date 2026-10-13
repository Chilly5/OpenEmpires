# Phase 5C stabilization — 2026-10-13

**PHASE 5C STABILIZATION IMPLEMENTED — READY FOR VERIFICATION**

This verdict applies to this bounded source repair, not Phase 5B/5C acceptance or package certification.

## Authoritative checkout and scope

Unity root: `D:/unity_projects/OpenEmpires/Open Empires`; Git worktree root: `D:/unity_projects/OpenEmpires`.
Branch: `unit_models_and_voice_control`.
HEAD: `871a42ef24dea9ce179aa65f578c89320cef3e3d`.
Existing uncommitted work was preserved. Unity MCP confirmed this project, Unity 6000.5.9f1, instance `Open Empires@6d7310c7`.
Implementation/checkpoint window began approximately 08:22 UTC / 11:52 Tehran; handoff is before the 09:51 UTC / 13:21 Tehran limit.
No reset, clean, commit, push, voice-engine implementation, multiplayer work, fresh package build or next phase.

## Root causes and repairs

1. **Context packing contradicted the provider contract.** The contract referred to unit/structure/selector capabilities which final packing removed. These small capability projections now survive. Requested native identities retain producer facts and civilization replacement identities; required facts still refuse atomically on overflow rather than being truncated. Age/resources/producer/population readiness remains distinct from content existence, civilization availability and adapter support.
2. **Resolved categories and plurals were lost.** Exact shared resolver matches now carry category and canonical token into the production prompt. Archery resolves as Archery Range in building context. Native UI plural names generate aliases for the entire current roster, including Men-at-Arms, Landsknechte and Battering Rams. Native landmark definitions now expose King civilization availability without inventing a training adapter. Complete local catalogs are rebuilt from current simulation data. Context ownership already correctly uses CommanderGoalManager.PlayerId; it was inspected and retained.
3. **Location questions were not resumable construction drafts.** Added strict Location slots at movement.location and construction.placement.anchor, plus symbolic PlayerMarker construction. A real marker callback fills only one unresolved location locally, preserves remaining slots and the whole request, and re-enters normal admission without another provider call. Ordinary one-node requests retain automatic KnownIntent submission; compound/strategic requests retain explicit approval. Original ticket/runtime/owner correlation survives; successful admission consumes the snapshot and clears correlated pending state. Only cancelled, uncommitted candidates can be replaced under the same ticket. Marker movement cannot redirect admitted work.
4. **Representation guidance rejected ordinary counts and confused existing/new producers.** Unplaced ordinary BuildStructure supports counts 1–20. Repeated explicit placement uses the existing DynamicPlan graph. Existing producer restrictions advertise select-structures → produce; the contradictory exact-new-build requirement now applies only to new construction. Around a newly requested building retains the exact build → location → dependent build binding. Invalid provider data is never routed through a security-rejection fallback.
5. **Ordinary Lumber Yard defaults ignored worked woodlines.** The ordinary planner now reuses native visible woodline cluster ranking: actual owned gathering assignments, then proximity and stable ties, with legal-footprint and reachable-builder checks. No additional gathering order is inferred.

## Changed files in this pass

Production, relative to the Unity root (many already contained earlier uncommitted edits):
- `Knowledge/CommanderKnowledgeCatalog.cs`, `Phase5B/CommanderKnowledgeRetrieval.cs`, `Phase5C/CommanderSemanticRequestPack.cs`, `Phase4A/OpenRouterCommanderProvider.cs` under `Assets/Scripts/AI/Commander/`.
- `Economy/CommanderRequestGraphDraft.cs`, `Phase4E/CommanderSemanticRequest.cs`, `Phase4E/CommanderSemanticJson.cs`, `CommanderIntentValidator.cs`.
- `Phase5A/CommanderActionPlanCandidate.cs`, `CommanderRequestTicket.cs`, `CommanderGoalManager.Requests.cs`, `CommanderChatUI.ActionPlans.cs`, `CommanderGoalManager.DynamicLocations.cs`.
- `CommanderGoalManager.cs`, `Phase4A/CommanderChatUI.cs`, `Phase5C/CommanderChatUI.DestinationMarker.cs`, `CommanderPlanner.cs`, new `Phase5C/CommanderPlanner.DefaultResourcePlacement.cs` (+meta).
- Generated `Assets/Resources/Network/LockstepCompatibility.json`.

Tests: new `CommanderPhase5CStabilizationTests`, `CommanderPhase5CStabilizationNativeTests`, `CommanderPhase5CStabilizationLiveTests`, `CommanderPhase5CStabilizationUIPlayModeTests` (+meta); amended `CommanderGrandFixPresentationTests`, `CommanderPhase5CComposableLocationTests`, `CommanderPhase5CProviderBudgetTests`, and `CommanderEconomyClarificationTests.HintGate.cs`. Budget fixtures now test the actual request bound and restored concrete contract, rather than obsolete 4K-era assumptions. The pruning fixture still proves game-owned name confirmation after optional model hints are removed.

## Verification and actual outcomes

| Gate | Exact job | Outcome |
|---|---|---|
| Initial new regressions | `6ff0e1c72b0541d0bcdd21b604b36542` | 8 completed, 8 intended failures: missing capabilities/producers, wrong category and location draft |
| First repaired gate | `e69f51167ec74d9fb17f5656f67dd441` | 8/8 passed |
| Ordinary woodline default RED | `c96e4aeb46d64c558017248614256aa1` | Expected near worked woodline; actual placement 22 tiles away |
| Native/default checkpoint | `e218f74bb8fa40608fd0b54bfbe7213d` | 6/6 passed, 33.66 s |
| Broader affected checkpoint | `e0c55f2cbff040698ed170870d180519` | 268 completed; ten recorded failures, not a green aggregate |
| Native plural/category/reward RED | `bfa0932320fa4c628e062121ce48c735` | 13 completed; five intended failures |
| Existing-producer instruction RED | `9861a302112a420eb8a322ebae45daf4` | 13 completed; the existing/new producer contradiction failed |
| Final relevant EditMode/native gate | `6146eb56d1354dcfb8e0ab467d3f9f12` | **93/93 passed**, 106.69 s |
| First live production matrix | `0c6665dd9f9843e4b18eabdf4c1ee0c2` | Six HTTP200 initial calls; retained failures and lost-around binding, not accepted |
| Corrected live follow-up | `f43227bab956422eacde3edf511c19c9` | **1/1 test covering three wordings passed**, 12.14 s |
| Actual-scene UI/Play Again | `f3f11750530d4a5e883e87293fafe062` | **1/1 passed**, 9.48 s |

The final 93-case gate includes generated native identity/civilization/early-midgame context coverage; new-match refresh; typed location continuation and other unresolved slots; reset/late callback and immutable marker controls; existing negative/parser/budget gates; and normal native construction/training.

Native fixtures completed three Stables and three Archery Ranges with distinct legal footprints; ordinary Mill + eight Farms; exact-new-Mill anchored Farms; five new Barracks → fifty new Spearmen with fifty accepted native training receipts restricted to those exact five results; five existing Barracks → fifty new Spearmen excluding the sixth producer; and exact new-unit movement to marker/worked Gold with old-unit exclusion and human override protection. These are accelerated EditMode simulation ticks using normal commands and timers, not rendered standalone playthroughs or forced completion.

Live production prompt/context/parser results:
- Initial House count 1 and Stables count 3 compiled; English Longbowman availability was answered from the relevant data.
- After the repair: “Build 3 archery” produced ArcheryRange count 3; “Build 50 spearman out of 5 barracks” produced select-structures count 5 → produce Spearman count 50, TargetTotal, with no construction; Mill + eight Farms around it retained build → resolve-location from exact new Mill → Farm build count 8.
- These real-provider calls establish semantics and compilation; they did **not** admit or complete gameplay. Native completion evidence above is separate.
- Nine HTTP attempts in this pass, zero repairs. Preserved global ledger: **24/200 consumed, 176 remaining**. Initial action input usage 4,772–5,527 tokens; corrected follow-up 4,903 / 5,772 / 5,576. No credential was copied into these artifacts. See [initial results](evidence/stabilization-live/results.json), [follow-up results](evidence/stabilization-live-followup/results.json), and the preserved [global ledger](evidence/paid-attempt-ledger.json).

Actual SampleScene UI used a synthetic Input System mouse and the production terrain-raycast polling callback, not a physical mouse automation claim. With no pending request it rendered a marker and created zero goals/zero provider calls. With an exact-new-unit compound it retained a separate approval preview and one interpretation call. The actual visible MatchResultUI Play Again button callback recreated one minimized UI/new simulation, cleared markers/drafts/previews, and rejected an old-host callback. Screenshots: [world marker](evidence/stabilization-ui/actual-scene-marker.png), [approval preview](evidence/stabilization-ui/actual-scene-awaiting-approval.png); [receipt](evidence/stabilization-ui/result.json).

Earlier zero-initialization/discovery jobs and the first UI camera-ray fixture mismatch are retained as unsuccessful attempts. One UI attempt produced completed local artifacts but lost MCP initialization reporting; the final terminal passing job above resolves that evidence gap. No retries or provider/native completion timeouts were raised.

## Identity and remaining verification

Generated and loaded Editor source SHA-256:
`dcc0122a1d47615de5c25593b08a537ed5104e692b6e31471fbb58de9ae9918a`.
Protocol remains 2, command encoding `legacy-or-sourcekind-v1`.
See [identity receipt](evidence/stabilization-compatibility-identity.json), [test jobs](evidence/stabilization-test-jobs.json), and the refreshed existing source manifest. Source/hash consistency is not package or multiplayer proof.

No known remaining implementation gap was demonstrated in the targeted repairs after the final relevant gate and live follow-up. Remaining gates:
- **Physical/locked-cursor UI and real-provider → actual UI → native completion together:** next executable step is one short English match: submit “Build a lumber yard”; if it asks Where, use Set destination and a visible world click, then verify one admitted task/one completed structure and no repeat after another marker event. Preserve the remaining paid ledger for live interpretation.
- **Fresh Windows/Web packages and standalone/served-Web certification:** explicitly pending; next step is a separately authorized package/compatibility verification pass on this frozen source.
- **Four out-of-scope voice presentation assertions:** the broader gate failed `OnlinePolicyReviewAndExplicitConsent_DoNotRecordSubmitOrApprove` and the three `VisibleDurationLimit_IsFrozenForRecordingEvenIfSettingsChange` cases (3, 3.25, 3.1234567 seconds). No causality or voice fix is claimed. Next step is an isolated offline run of those four unchanged assertions to classify fixture expectations versus product behavior.

Do not mark Phase 5B/5C accepted or begin another phase.
