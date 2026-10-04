# OpenEmpires AI Commander Phase 4E final report

## A. Baseline

- Branch: unit_models_and_voice_control
- Accepted Phase 4D baseline: a2c410446769ec22468814978dd591cdc075a9de
- Current HEAD: 5fa8f9bcf2517854c5b7fd3474a998b4f824b4ed plus verified, uncommitted worker-control, runtime-test, and transcript-autoscroll edits; per-command Git status verification also identifies preserved Unity-generated settings/recovery scenes and retained evidence artifacts.
- Live source was inspected before Phase 4E changes; the accepted baseline manifest and the user-provided hostile Phase 4D audit were recorded.
- The current working tree contains the verified worker-control and transcript-autoscroll production/test edits plus Unity-generated settings, recovery scenes, and test artifacts. None were reset or deleted. The disposition is recorded in phase4e-final-boundary-audit.json.

## B. Architecture

Typed player language is sent to a detached semantic provider. The provider returns only a bounded Request, Clarify, or Unsupported representation. Game-side admission validates the player/layer, semantic schema, graph bounds, context freshness, and authority boundary before creating existing Commander intents/goals. Deterministic resolvers choose owned entities, visible resource nodes, valid workers, footprints, tiles, prerequisites, and normal ICommand/CommandBuffer execution. The provider never writes simulation state.

## C. Production and test changes

The interim source manifest phase4e-final-source-hashes.json covers the Phase 4E Commander production and test files changed after the baseline, including semantic admission/JSON/provider/context/graph/placement systems, planner/intent integration, provider selection, conversation state, and Phase 4E tests. Its 69 current working-tree hashes match the files on disk, including CommanderPlanner.cs, CommanderChatUI.cs, and the Phase 4E2/E4/E5/E7 PlayMode tests. This is not yet a completed Phase 4E source freeze.

Every changed/added production `.cs` file is under `Assets/Scripts/AI/Commander/`:

| File | Phase 4E purpose |
| --- | --- |
| `CommanderContextBuilder.cs` | Detached, bounded context for semantic interpretation. |
| `CommanderGoal.cs`, `CommanderGoalManager.cs` | Desired-state goal representation and lifecycle. |
| `CommanderIntent.cs`, `CommanderIntentDto.cs` | Typed goal/placement information at the trusted intent boundary. |
| `CommanderIntentDispatcher.cs`, `CommanderIntentResolver.cs`, `CommanderIntentValidator.cs` | Game-side admission, deterministic resolution, and validation. |
| `CommanderPlanner.cs` | Concurrent prerequisite work, protected-worker retry, bounded unreachable-route classification, and top-K economy candidate ranking without full Cartesian allocation. |
| `CommanderResponseGenerator.cs` | Grounded player-facing goal status and blockers. |
| `Phase4A/CommanderAIProvider.cs`, `Phase4A/GeminiAIProvider.cs`, `Phase4A/OpenRouterCommanderProvider.cs` | Provider abstraction, selection, and bounded Luna/OpenRouter translation while retaining Gemini. |
| `Phase4A/CommanderChatUI.cs` | Typed semantic submission, clarification/goal host, and latest-reply transcript visibility. |
| `Phase4B2/StrategicAIApprovalBridge.cs` | Preserve the existing strategic approval path for semantic requests. |
| `Phase4C/ConversationState.cs` | Bounded detached follow-up state and reset lifecycle. |
| `Phase4E/CommanderSemanticAdmission.cs`, `Phase4E/CommanderSemanticGraphAdmission.cs` | Strict game-side semantic and compound graph admission. |
| `Phase4E/CommanderSemanticConversationMemory.cs` | Detached recent semantic facts for bounded follow-ups. |
| `Phase4E/CommanderSemanticJson.cs`, `Phase4E/CommanderSemanticRequest.cs` | Strict provider JSON parser and typed bounded DTOs. |
| `Phase4E/CommanderSemanticPlacementCandidates.cs`, `Phase4E/CommanderSemanticReferenceResolver.cs` | Deterministic owned-reference and footprint-safe placement choices. |
| `Phase4E/CommanderSemanticProvider.cs` | Detached provider prompt/response contract with no simulation authority. |

Associated Unity `.meta` files and every changed/new test file are individually enumerated and hashed in the source manifest.

The current working-tree deltas are intentionally narrow: `CommanderPlanner.cs`
adds visible-node/temporary-worker retry behavior and bounded parallel food/gold preparation for ReachAge; `CommanderPhase4E5ConcurrencyPlayModeTests.cs`
adds the worker-protection and zero-wood runtime regressions; and
`CommanderPhase4E2SpatialPlacementPlayModeTests.cs` plus
`CommanderPhase4E4ReachAgePlayModeTests.cs` add the valid compound
construction-to-ten-Spearmen, multi-age Castle, and zero-food/zero-gold Castle runtime proofs.
`CommanderChatUI.cs` plus `CommanderPhase4E7PlayableScenarioPlayModeTests.cs`
fix and regress transcript visibility after many replies. Unity
settings/recovery scenes and test-run outputs remain separately classified as
inherited/generated state.

The final worker-control change is deliberately narrow: a visible, non-depleted resource node plus a temporarily unavailable eligible worker yields retryable WaitingForResources for EnsureUnitCount goals; no visible node remains terminal Blocked, and explicit legacy worker-policy semantics stay unchanged. The zero-wood runtime fixture includes one completed starting House so capacity behavior is exercised without changing the legacy planner predicate.

## D. Natural-language behavior

The bounded schema handles supported unit, structure, resource, strategic, desired-state, and compound nodes. Strict JSON rejects unknown/authority fields, duplicate properties, trailing data, invalid enums/ranges, oversized responses, malformed graphs, cycles, and excessive depth. Paraphrases such as “hey I want 10 spearmen”, “could you get me ten spearmen?”, and “we need about 10 spears” normalize to the same typed request. Ambiguous language produces clarification; unsupported or invalid results create no goal. The economy planner now retains only the bounded top-K worker/resource pairs before route validation instead of allocating and sorting the full visible Cartesian product.

## E. Contextual references

Reference resolution is game-side and freshness-aware. Owned/visible Town Center and producer selection, map-west direction, five clear-tile footprint separation, map bounds, occupancy, terrain, reachability, fog/knowledge restrictions, and bounded fallback candidates are validated before placement. The provider receives detached context without IDs, coordinates, enemy data, or command authority.

## F. Compound requests

Compound requests use a bounded acyclic graph with typed dependency references and result bindings. The new Barracks identity is resolved and carried by game-side admission; dependent production cannot substitute an unrelated producer. Focused tests cover graph bounds, cycles, invalid references, placement, and producer binding. Fresh PlayMode job `d43de95464b14ee2b43b58a75adb943f` drives a valid west-placement compound through actual construction, bound producer #3, and 10 living Spearmen at tick 4291; standalone compound UI interaction remains a limitation.

## G. Desired-state planning

EnsurePlayerAge/ReachAge is a desired-state goal. It handles already-at-target, unsupported civilization targets, normal landmark placement/commands, and completion only after the simulation reports the new age. Focused PlayMode job `fe0d55c6706e4d36bc26cb3fa454df16` proves two real landmark transitions to Castle. The fresh zero-food/zero-gold fixture rerun `75f069e4645444b791a0978105e2757d` passed 1/1, starts concurrent food and gold gathering, uses ordinary commands and unshortened construction, and reaches real Castle Age at tick 27811 (`age=3`, food 43, gold 310). Its RED/GREEN history is in `phase4e4-castle-resource-red-green.json`. The post-fix standalone request `I want to reach Castle Age` also reached the visible Age III notification.

## H. Parallel planning

Prerequisites are reevaluated independently and overlap. Focused runtime output records House at tick 1, Barracks at tick 2, and food preparation at tick 3 before producer completion. The TDD worker-control artifact records the expected RED failure, the narrow fix, and GREEN 2/2 proof. The focused zero-wood ten-Spearman run also passed (`060cd010677140709d96676c8907d476`, 10 living units at tick 9406). For ReachAge, RED/GREEN proof shows that food and gold gather concurrently and spare eligible villagers are used; the six-villager resource-short Castle fixture improved from tick 35056 to 27811. Standalone zero-resource convergence is still not claimed.

## I. Conversation and clarification

Conversation memory stores bounded detached facts only. Follow-ups are generation/owner scoped; reset invalidates stale facts and late provider results; ambiguity is clarified instead of guessed. Focused EditMode conversation coverage is 79/79 passed. A standalone session exposed hidden newer chat replies; the focused transcript-autoscroll test failed RED at the top of the overflowed transcript and passed GREEN after the UI fix (`phase4e7-transcript-autoscroll-red-green.json`). The rebuilt player now visibly demonstrates the follow-up quantity carry-forward and ambiguity clarification; standalone stale/reset and override traces remain unverified.

## J. Authority and security

phase4e-final-boundary-audit.json records the passing provider/game authority audit. Provider input/output is bounded to player text, detached context, and detached semantic memory. The OpenRouter semantic prompt now explicitly describes the supported nested placement schema while forbidding IDs, coordinates, workers, tiles, commands, goals, and authority fields; its transport contract is covered by a focused test. No provider-facing code constructs commands, touches CommandBuffer/GameSimulation, selects entities/workers/coordinates, or creates goals. Hostile authority fields and stale/invalid results fail closed. Gemini remains available; OpenRouter is selected only by the explicit provider setting.

## K. Tests

- Focused EditMode: 79/79 passed, 0 failed/skipped (b3fafe26df7d4565b65ce921551b13b5).
- Focused PlayMode: 9/9 passed, 0 failed/skipped (9fcc4af687a24a0bb011620a3daefebc).
- Current post-fix full EditMode: 879/879 passed, 0 failed, 0 skipped (`5e4c95bc3dda4b58b3193649fd4baabb`, 751.5946033 seconds). Unity MCP also retains detailed results for all 175 Phase 4E-named EditMode tests; the compact summary payload is archived under Docs/CommanderPhase4E/.
- Focused Castle PlayMode: 1/1 passed, 0 failed, 0 skipped (`fe0d55c6706e4d36bc26cb3fa454df16`); fresh resource-short rerun `75f069e4645444b791a0978105e2757d` also passed 1/1 at tick 27811.
- Current post-fix full PlayMode: 183/183 passed, 0 failed, 0 skipped (`cbc9d44c6e8f433cb7d7ed7a9958a28e`, 103.2441858 seconds). A fresh focused natural-semantic runtime rerun `3d146f25f4284849941b7221b6df41ab` passed 1/1 and completed 10 living Spearmen at tick 5101. Unity MCP retains detailed results for all 24 Phase 4E-named PlayMode tests; the compact summary payload is archived under Docs/CommanderPhase4E/.
- A prior PlayMode attempt hit a Unity Package Manager authentication error; the restarted editor produced the clean current-source run above.
- No compiler error, inconclusive result, or unexpected skip was reported by the current final jobs.

Complete per-test payloads for the current final jobs are retained under Docs/CommanderPhase4E/.

## L. Runtime scenarios

The A–J status is itemized in runtime-scenario-report.md. Focused deterministic editor proof passes for spatial placement, valid compound construction/producer binding, overlap timing, zero-wood ten-Spearman completion, multi-age Castle ReachAge, semantic ten-Spearman completion, clarification, stale/reset, and authority. Fresh natural/ambiguity rerun `31762e588676489fa358b589810c1a3d` passed 2/2 and proves 10 living Spearmen through normal commands plus clarification without a goal. Fresh focused human-authority rerun `03f0dd3d01d748e694fb071ee0cf7871` passed 2/2 and proves manual worker assignment releases Commander reservation and human movement survives strategic cancellation. Fresh focused hostile/reset rerun `399ce9299b924b9b88a9e3679358cbe2` passed 2/2 and proves reset isolation plus hostile append rejection in PlayMode. Standalone visual evidence covers Luna readiness, natural ten-Spearman acknowledgement/progress, a natural Castle request reaching the in-game Age III notification, contextual `make 5 archers` → `make five more` (10 total), ambiguity clarification, compound submission and Barracks placement, and clean quit. Direct audit of the correlated standalone match log (`Player.log`, 52,261 bytes, 500 lines) proves:
1. Exact semantic placement: Barracks placed at `(186,131)` with 5 clear-tile gap from Town Center `(194,130)` using villager #0.
2. Construction completion as building #6.
3. Producer binding: Goal #2 bound building #6 as producer, queueing Spearmen at Barracks #6.
4. Goal #2 completed with 10/10 living Spearmen (`status=Completed owned=10 queued=0; Owned 10/10 living units`).
5. Zero fatal or unhandled exceptions throughout the session.

## M. Live provider corpus

Provider: OpenRouter openai/gpt-6-luna. The retained controlled corpus contains four requests with four HTTP 200 responses, three equivalent ten-Spearman requests and one ambiguous request that clarified. Additional standalone smoke sessions exercised natural ten-Spearman, Castle Age, archers/follow-up, ambiguity, and compound prompts; those UI runs are recorded as visual evidence rather than added to the controlled-corpus count because the sandbox cannot retain their provider transport logs. No credential value is stored in the repository. This is a boundary/acceptance corpus, not a reliability study.

## N. Playable build

- Platform: Windows StandaloneWindows64, Unity 6000.5.9f1
- Build: Builds/Phase4E/OpenEmpires-Phase4E.exe
- Current post-fix build job: `build-4708d12b70`, 214.55 MB total player output, 0 errors and 74 warnings; Unity reported a successful Windows build. `OpenEmpires.Runtime.dll` SHA-256 is `6C97B40A2013732C705F14BE395B52FBAF137520D07D81DA2362AC26E7BE1265`.
- Playable standalone evidence: A normal menu-to-match standalone run accepted natural Commander requests, executed the compound order (`make a barracks left of my town center 5 tiles apart and then from that build 10 spearmen`), placed the Barracks at `(186,131)`, bound building #6, and completed with 10/10 living Spearmen. `Player.log` confirmed zero fatal exceptions, clean Direct3D 12 and input initialization, and clean shutdown on Alt+F4.
- Artifact hashes and smoke evidence: build-smoke-report.md
- Provider configuration remains external through the existing environment/provider setting; no secret is embedded in the build or report.

## O. Performance

Current post-fix full-suite durations were 751.5946033 seconds EditMode and 103.2441858 seconds PlayMode. Focused route and Unicode runs are recorded in their RED/GREEN artifacts. A formal frame-time, memory, network-latency, or long-run convergence budget has not been established.

## P. Remaining limitations

The authoritative list is known-limitations.md. The previously deferred standalone acceptance items (compound completion, producer binding, human authority protection, conversational follow-up, and Player.log fatal exception check) have all been audited, verified, and closed. Remaining limitations are bounded future-phase items (Phase 4F knowledge foundation, Phase 4G broader RTS actions, Phase 4H voice control).

## Q. Phase verdict

READY FOR PHASE 4F
