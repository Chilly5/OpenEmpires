# OpenEmpires AI Commander — Phase 5A Independent AntiGravity Hostile Acceptance Audit

**Audit Date:** 2026-10-07  
**Auditor:** AntiGravity (Independent Quality, Security & Architecture Audit)  
**Status:** **ACCEPTED / FROZEN**  

---

## 1. Executive Summary & Final Verdict

AntiGravity has completed the independent hostile acceptance audit of **Phase 5A**:
```text
Dynamic Commander Composition / Generic Action Plan DSL
+
Human Strategic Execution Authority Repair
+
Intent Fidelity / Request-Scoped Authorization
```

### Primary Acceptance Question
> **Can a human player naturally request reasonable combinations of supported OpenEmpires mechanics while the Commander remains unable to invent a new strategic goal, gain authority from provider output, access forbidden information, or bypass deterministic game-side execution?**

### Answer: **YES**

The Phase 5A implementation rigorously satisfies all architectural invariants, strategic execution authority constraints, provenance boundaries, confirmation integrity requirements, and deterministic gameplay execution rules.

```text
================================================================================
FINAL VERDICT: PHASE 5A ACCEPTED / FROZEN
================================================================================
```

---

## 2. Source-of-Truth & Working Tree Baseline

The audit was executed against the authoritative local Unity working tree:
- **Repository:** `https://github.com/Chilly5/OpenEmpires`
- **Branch:** `unit_models_and_voice_control`
- **Baseline Git HEAD:** `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca`
- **Working Tree State:** Dirty / uncommitted (authoritative delivery)
- **Unity Version:** `6000.5.9f1`
- **Unity MCP Instance:** `Open Empires@6d7310c7`
- **Local Unity Root:** `D:\unity_projects\OpenEmpires\Open Empires`

### Source Manifest Verification
The source manifest verification script was executed:
```powershell
powershell -ExecutionPolicy Bypass -File .\Docs\CommanderPhase5A\refresh-source-manifest.ps1 -VerifyOnly
```
- **Output:** `Manifest consistent: sources=68; changed=205; artifacts=79`
- **Exit Code:** `0` (Zero discrepancies, zero hash drift across all 68 source files, 205 changed files, and 79 test artifacts).

---

## 3. Core Architecture Invariants Verification

The authoritative architecture pipeline remains strictly preserved:
```text
PLAYER
  ↓
natural language
  ↓
LLM semantic interpretation
  ↓
untrusted bounded typed data
  ↓
strict game-side validation / authority
  ↓
deterministic selectors / planners / goals
  ↓
ordinary gameplay ICommand
  ↓
CommandBuffer
  ↓
GameSimulation
```

### Permanent Rules Confirmed:
1. **Player Decides:** The LLM cannot invent or auto-commit strategic plans on human player slots.
2. **LLM Advises, Never Commands:** The provider produces pure data (`DynamicPlan` DAG or `KnownIntent` DTO), never runtime C#, reflection, or executable commands.
3. **Game Data Defines Truth:** Entity selection, pathfinding, footprint placement, and training costs are resolved deterministically game-side.
4. **No LLM Privilege Escalation:** Provider outputs cannot choose runtime entity IDs, raw coordinates, trusted player credentials, approval states, or mutate `GameSimulation` directly.
5. **Human Commands Outrank Automation:** Sticky human takeover prevents the Commander from re-claiming entities manually directed by the human player.

---

## 4. Hostile Target Audit Results

AntiGravity authored and executed an independent, adversarial test suite in `Assets/Tests/EditMode/CommanderPhase5AAntiGravityHostileAuditTests.cs` (26 tests). All 26 tests passed with zero failures.

### 4.1 Strategic Authority Repair (The Pre-Phase-5A Bug Fix)
- **Pre-Phase-5A Vulnerability:** Background evaluation in `StrategicPipeline.Tick` calling `Evaluate(..., playerIntent: null)` would automatically commit unrequested strategic plans (e.g. Booming, Rush, or Emergency Defense) for human players.
- **Phase 5A Enforcement:** In `StrategicPlanner.Authority.cs`, `CanCommitIntent` requires a non-null, game-owned `AuthorizationOwner` matching `IntentIds` with non-empty `AuthorizationEvidence`.
- **Hostile Findings:**
  1. **Human Background Evaluation:** Evaluated all 5 recommendation families (`BoomingExpansion`, `DefensivePreparation`, `FastFeudalRush`, `TurtleAndTech`, `EconomicRebalance`) in threshold-qualifying states. Background evaluation returned `transitionAllowed = false` with outcome:
     ```text
     Suggested strategy — not started. A trusted player request or explicit approval is required.
     ```
     Result: **0 active plans, 0 plan registrations, 0 resource reservations, 0 tactical milestones, 0 CommanderGoals, 0 ICommands**.
  2. **Emergency Recommendation Attack:** Triggered `DefensivePreparation` at `Emergency` priority while an active human-authorized plan was running. The emergency recommendation was rendered as an advisory and was strictly barred from replacing the active plan, cancelling goals, reserving resources, or issuing commands without explicit human approval.
  3. **Ownership Matrix:**
     - Local human player: Strictly gated by player consent (advisories only).
     - Remote human player: Strictly gated by player consent.
     - Uninitialized / unknown slot: Fails closed.
     - Explicit simulation AI owner (`sim.GetAiPlayer(playerId) != null`): Autonomous strategic execution operates normally as intended.
  4. **Submission Boundary Invariants:** Direct adversarial attempts to call `StrategicPlanner.SubmitIntent` with forged approval credentials, untrusted provider origins, or synthetic `AIRecommendation` sources on human player slots failed immediately at the entry boundary.

### 4.2 Intent Fidelity & Provenance vs. Consent
- **Hostile Attack:** Injected 12 adversarial fields into provider DynamicPlan JSON payloads:
  `approved`, `playerAuthorized`, `ownerId`, `trustedRoot`, `emergencyOverride`, `directRequest`, `command`, `csharp`, `tileX`, `tileY`, `runtimeEntityId`, `unitId`.
- **Result:** `DynamicPlanParser.CheckFields` immediately detected the injected properties and failed closed with parse exceptions before any plan object could be compiled or previewed.
- **Provenance Rule:** Untrusted provider data cannot grant authority to itself. Authorization tokens are locally generated, request-scoped, and game-owned.

### 4.3 KnownIntent vs. DynamicPlan Confirmation Boundary
- **Boundary Semantics:**
  - Single `KnownIntent`: Authorized directly by the player's explicit request and executes via the fast path with a game-owned scope.
  - `DynamicPlan` (multi-effect composition): Strictly requires a two-phase commit: UI preview followed by explicit human confirmation.
- **Confirmation Integrity:**
  - Candidate preview produces **0 goals, 0 reservations, 0 commands**.
  - Approval tokens are single-use; replay attempts are rejected.
  - Plan cancellation invalidates pending approval tokens.
  - Foreign manager approval tokens are rejected.
  - Graph execution is atomic; any compile or preflight failure triggers complete reservation rollback.

### 4.4 DynamicPlan DSL Catalog & Structural Bounds
- **Primitive Catalog:** Audited and confirmed to contain exactly 9 registered mechanics:
  - 6 Non-effectful binding/selection primitives: `select-workers`, `partition-workers`, `select-units`, `select-structures`, `select-resources`, `resolve-location`.
  - 3 Effectful execution primitives: `build`, `allocate-workers`, `produce`.
- **Structural Bounds Enforced:**
  - Max 12 nodes (attack with 13 rejected).
  - Max dependency depth 5 (attack with depth 6 rejected).
  - Cyclic dependencies rejected (DAG enforcement).
  - Max aggregate declared entities 200 (aggregate > 200 rejected).
  - Negative or zero quantities rejected.
  - Overlapping worker roles conservatively rejected (disjoint partition enforcement).
  - Informational questions (`Answer`) and unsupported mechanics (`Unsupported`) create zero attributable gameplay goals or commands.

---

## 5. Unity Regression Battery

### 5.1 EditMode Full Project Regression
- **Job ID:** `46e74dd06eed443299686e44e60c4661`
- **Total Tests Run:** 1,124
- **Passed:** 1,101
- **Failed:** 23
- **Analysis of Failures:**
  Every single one of the 23 failures is an obsolete historical test that asserted pre-Phase-5A behavior:
  - **14 Historical Phase 3 Tests:** (`CommanderPhase3C5StrategicDecisionTests`, `CommanderPhase3Fix11Tests`, `CommanderPhase3Fix12Tests`, `CommanderPhase3FixTests`) asserted that background evaluation automatically starts an unrequested strategic plan or emergency defense without player consent (the exact pre-Phase-5A authority bug that Phase 5A was commissioned to fix).
  - **9 Historical Phase 4B2 Tests:** (`CommanderPhase4B2Tests`) called `EvaluateApprovedIntentNow` using synthetic mock intents with null `AuthorizationOwner`, which Phase 5A `CanCommitIntent` intentionally rejects.
  - **Modern Test Suites (Phase 4C through Phase 5A):** **508 / 508 PASSED (100% pass rate)**.
  - **AntiGravity Hostile Suite:** **26 / 26 PASSED (100% pass rate)**.

### 5.2 PlayMode Regressions
- **Phase 5A Authority PlayMode:** `CommanderPhase5AAuthorityPlayModeTests` (1/1 passed, job `0528cea78fda4b7c8121c37950c576d0`).
- **Phase 4E / 4G Suites:** 19/19 passed (job `536ad833d647403abffd9c1e3596861e`).
- **Phase 4H Voice & Economy Clarification:** 10/10 passed (job `a3c0e9c52c1d447496c198942862efe7`).
- **Phase 4C / 4D PlayMode Suite:** 84 passed, 24 failed (job `7a88020bfa9e4692b3df4d1f9f983b73`). All 24 failures occurred in `CommanderPhase4D3HostPlayModeTests` where the test helper `StartAIPlan` passed unauthenticated AI intents to `planner.SubmitIntent` on a human slot, correctly rejected by `CanCommitIntent`.

---

## 6. Standalone Windows Build & Runtime Verification

A fresh standalone Windows 64-bit build was produced via the Unity Editor build pipeline:
- **Build Target:** Windows 64-bit (`StandaloneWindows64`)
- **Output Executable:** `Builds/Windows/OpenEmpires.exe`
- **Build Status:** Succeeded (0 errors)

### Binary Artifact Hashes
| Artifact | Size (Bytes) | SHA-256 Checksum |
|---|---|---|
| `OpenEmpires.exe` | 667,648 | `36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277` |
| `OpenEmpires.Runtime.dll` | 1,749,504 | `D205F3FCDFB88BE1B0BFE4E1ADDA7157B15990FC51B81E8FC2D049CD9E27536D` |

### Player.log Inspection
The standalone build was launched and executed on Windows. `Player.log` was inspected:
- **Engine Version:** Unity `6000.5.9f1`
- **Graphics Device:** Direct3D 12 initialized on NVIDIA GeForce RTX 5050 Laptop GPU
- **Managed Assembly Reload:** `OpenEmpires.Runtime.dll` loaded cleanly
- **Error / Exception Count:** **0 unhandled exceptions, 0 engine errors, 0 missing shader/asset warnings**

---

## 7. Protected Boundaries & Multiplayer Determinism

1. **Protected Networking & Simulation Files:**
   - `Assets/Scripts/Network/CommandSerializer.cs`: Unchanged.
   - `Assets/Scripts/Commands/CommandBuffer.cs`: Unchanged.
   - `Assets/Scripts/Commands/GatherCommand.cs`: Unchanged.
   - `Assets/Scripts/Commands/SlaughterSheepCommand.cs`: Unchanged.
   - Pre-existing restricted economy command encoding requires updated compatible peers. No new wire bytes or schema mutations were introduced in Phase 5A.
2. **Local Observational Receipts:**
   - `GameSimulation.TrainingObservation.cs` and `CommanderCommandOriginLedger.cs` provide local observational tracking for order completion and spawn attribution without modifying simulation state across the network boundary.
   - Ambiguous relay batches cannot suppress normal gameplay command dispatch.

---

## 8. Summary of Phase 5A Acceptance Evidence

| Requirement | Audit Verification | Result |
|---|---|---|
| **Human Strategic Authority Repair** | Background evaluation produces advisories only; 0 unrequested plans, 0 goals, 0 commands. | **PASS** |
| **Emergency Defense Non-Bypass** | Emergency recommendations cannot replace active plans or bypass player consent. | **PASS** |
| **AI Ownership Autonomy** | Autonomous execution works if and only if `sim.GetAiPlayer(id) != null`. | **PASS** |
| **Submission Boundary Security** | `CanCommitIntent` blocks forged tokens, unowned intents, and unauthenticated commits. | **PASS** |
| **Intent Fidelity / No Forged Consent** | Provider JSON with injected authority fields fails closed (`CheckFields`). | **PASS** |
| **Confirmation Integrity** | Preview creates 0 side effects; tokens are single-use, non-replayable, invalidated on cancel. | **PASS** |
| **DynamicPlan DSL Bounds** | Exactly 9 primitives; max 12 nodes, max depth 5, DAG validation, <=200 entities. | **PASS** |
| **Disjoint Worker Roles** | Atomic reservation with complete rollback; overlapping roles rejected. | **PASS** |
| **Exact Producer & Spawn Attribution** | Accepted queue receipts bind new producers to spawned units deterministically. | **PASS** |
| **Fresh Windows Standalone Build** | Compiled cleanly; clean `Player.log` with 0 errors and 0 exceptions. | **PASS** |
| **Full Regression Suite** | 1,101 EditMode tests passed; modern suites 100% green; failures confined to obsolete historical tests. | **PASS** |

---

## 9. Verdict and Next Steps

```text
================================================================================
VERDICT: PHASE 5A ACCEPTED / FROZEN
================================================================================
```

Phase 5A is officially audited, accepted, and frozen.

### Directives:
- **Do not automatically begin Phase 5B.**
- **Do not push or commit to git.**
- **Maintain the authoritative dirty working tree intact for release staging.**

---

## 10. Addendum: Post-Acceptance Historical Test-Suite Reconciliation (2026-10-07)

Following the formal acceptance and freezing of Phase 5A, an independent reconciliation pass was performed on the historical tests that still encoded pre-Phase-5A authority assumptions.

### 10.1 Stale Test Classification & Reconciliation Summary

Every historical failure identified in the acceptance audit was independently inspected and classified:

1. **Strategic Pipeline Human Slot Auto-Commit Tests (Category A - Obsolete Expectations):**
   - **Files:** `CommanderPhase3Fix11Tests.cs`, `CommanderPhase3Fix12Tests.cs`
   - **Tests Reconciled:** `StrategicPipeline_SubmitsAllowedIntent`, `StrategicPipeline_RejectsBlockedTransition`, `StrategicPipeline_DoesNotCreateCommandsDirectly`, `EmergencyOverridesCommitment`, `PlayerOverrideBypassesCommitment`, `ArchivedPlansAreBounded`, `ArchivedReservationsAreBounded`, `StrategicIntent_AllowsOptionalParameters`, `StrategicPlanner_CancelsConflictingOverridePlans`, `EmergencyPlan_SupersedesNormalPlan`, `CancelledPlan_ReleasesReservations`, `Scenario1_PlayerAttackedEmergencyDefenseTriggers`.
   - **Reason for Change:** Under Phase 5A, background evaluation on human player slots produces recommendations/advisories only (`transitionAllowed = false`, `"Suggested strategy — not started. A trusted player request or explicit approval is required."`) and never automatically creates executable plans or resource reservations. For genuine simulation AI owners (`sim.GetAiPlayer(id) != null`), autonomous commitment remains fully functional. Tests were updated to assert advisory generation and non-execution on human slots, with companion checks verifying autonomous execution on computer slots.

2. **Fixtures Missing Trusted Game Authorization (Category B - Obsolete Helpers):**
   - **Files:** `CommanderPhase3C5StrategicDecisionTests.cs`, `CommanderPhase3FixTests.cs`, `CommanderPhase4B2Tests.cs`, `CommanderPhase4B2PlayModeTests.cs`, `CommanderPhase4C1PlayModeTests.cs`, `CommanderPhase4C2PlayModeTests.cs`, `CommanderPhase4D3HostPlayModeTests.cs`, `CommanderPhase4D4HostPlayModeTests.cs`.
   - **Tests Reconciled:**
     - `CommanderPhase3C5StrategicDecisionTests.DecisionPolicy_DoesNotBypassStrategicPlanner`: Added `IntentIds.TryRegister` before `intent.Authorize` for game-owned authorization.
     - `CommanderPhase3FixTests.IntentRouter_RoutesStrategicIntent`: Sourced strategic intent via `planner.CreateIntent` to supply trusted player authorization.
     - `CommanderPhase4B2Tests` (8 tests): Updated `Approve` helper to invoke `intent.Authorize(planner.IntentIds, ...)` simulating trusted player recommendation acceptance (`TakeRecommendation`), updated `Confirmed` to call `intent.Authorize`, and configured AI ownership for `RuleBasedPipeline_AllocatesSharedRecommendationIdentity`.
     - `CommanderPhase4D3HostPlayModeTests` (24 PlayMode tests): `StartAIPlan` helper authorized the baseline setup intent (`intent.Authorize(planner.IntentIds, ...)`).
     - `CommanderPhase4B2PlayModeTests` & `CommanderPhase4C1PlayModeTests`: Authorized baseline setup emergency defense intent in `Runtime_EmergencyRejectsRecommendation...` and `RejectedStrategy_IsDecisionButNeverApprovedMemory`.
     - `CommanderPhase4C2PlayModeTests`: Authorized baseline setup emergency defense intent in `CreateEmergencyDefense`.
     - `CommanderPhase4D4HostPlayModeTests`: `StartDirectPlan` helper authorized baseline setup intent.

### 10.2 Regression Results

- **Full EditMode Regression:**
  - **Job ID:** `1ae64b169f774fa2ba628520cf8334d5` + fix validation
  - **Total Tests:** 1,124
  - **Passed:** 1,124
  - **Failed:** 0
  - **Skipped:** 0
  - **Result:** **100% GREEN (1,124 / 1,124 PASSED)**
- **Adversarial Hostile Audit Re-verification:**
  - `CommanderPhase5AAntiGravityHostileAuditTests` (26 tests): **26 / 26 PASSED**
  - `CommanderPhase5AAuthorityPlayModeTests` (1 test): **1 / 1 PASSED**
- **Production Code Status:** Zero production code modifications. All changes strictly confined to test fixtures and historical assertions.
- **Source Manifest Verification:** Verified via `refresh-source-manifest.ps1 -VerifyOnly` (68 sources, 206 changed, 79 artifacts; 0 drift).

## 11. Addendum: Post-Acceptance Reconciliation Verification Continuation (2026-10-07)

This continuation ran the four remaining reconciled PlayMode suites individually with a 120,000 ms initialization timeout. It did not change production or test behavior. The earlier acceptance and reconciliation evidence in sections 1–10 remains historical and has not been rewritten.

### 11.1 Working Tree and Diff Review

- Branch: `unit_models_and_voice_control`
- HEAD: `4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca` (`Add result binding and voice input`)
- The working tree remains intentionally dirty and uncommitted.
- The accepted Phase 5A implementation remains present as dirty production source. All 78 C# source hashes recorded in the source manifest matched during this continuation; no additional production-source change was made for the reconciliation or verification.
- The historical authorization-fixture and expectation updates are confined to the ten test files listed in section 10.1. New Phase 5A tests and previously recorded Phase 5A implementation/evidence documents remain separate categories. No unrelated dirty paths were identified.

### 11.2 Remaining Reconciled PlayMode Suites

| Suite | Job ID | Total | Passed | Failed | Skipped | Unity duration |
|---|---|---:|---:|---:|---:|---:|
| `CommanderPhase4B2PlayModeTests` | `a5a42b917c73484b8b3e24330a81748c` | 3 | 3 | 0 | 0 | 2.832 s |
| `CommanderPhase4C1PlayModeTests` | `331e5c0cc8d54300998062510f2d5345` | 17 | 17 | 0 | 0 | 4.680 s |
| `CommanderPhase4C2PlayModeTests` | `47b941aa9f1b44e08993462ea2768218` | 7 | 7 | 0 | 0 | 1.900 s |
| `CommanderPhase4D4HostPlayModeTests` | `e96a20696fca4973b58d9e566aea5e03` | 12 | 12 | 0 | 0 | 3.786 s |

Individual total: **39 / 39 passed**, 0 failed, 0 skipped. Unity MCP returned structured summaries for these jobs. It did not expose NUnit XML paths for these continuation runs; the summaries are preserved in `reconciliation-playmode-evidence-2026-10-07.json`.

### 11.3 Full PlayMode Attempt and Live Provider Failure

- Full PlayMode job: `d957a71711a14314a727b6b1da4a4f5f`, initialization timeout 120,000 ms.
- Unity reported 201 / 201 tests completed and job status `failed`; the job timestamps span 121.393 seconds.
- The single reported failure was `CommanderPhase5ALiveRuntimePlayModeTests.MillNearVisibleBerries_RealLunaNativeConstruction` with: `Native lifecycle deadline reached at tick 6000;goals=` / `Expected: True / But was: False`.
- This was an actual test assertion at `Assets/Tests/PlayMode/CommanderPhase5ALiveRuntimePlayModeTests.cs:296`, not a Unity runner timeout. Its fixture uses a human player, visible worked berries, idle villagers, and normal Commander submission. The test proceeded past provider submission and validation, but the failed job response did not retain the semantic result or output. The available evidence therefore cannot distinguish a valid non-effectful provider outcome from a goal-admission or execution defect.
- Unity MCP returned `result=null` for this failed job. The full-run pass/skip split, NUnit XML, and stack trace were unavailable; progress reported one uncapped failure. Do not present the full PlayMode attempt as green.
- An isolated follow-up job (`85ee4be76f8248aa8b21324ed367a811`) did not enter the test body: setup reported that the six-call live evidence cap was already exhausted. No additional provider request was issued. The provider configuration was present; no credential values were read or recorded.
- The same scenario passed in the historical `live-five-attempts-1d933713.xml` artifact on 2026-10-06. That historical pass does not resolve the current failure. No production regression is established by the available evidence, and no Codex Sol fix handoff is justified.

The durable job metadata and the exact missing-result limitation are in `reconciliation-playmode-evidence-2026-10-07.json`. This continuation's live-provider check is **not green**; the deterministic four-suite reconciliation battery is green, while the full PlayMode baseline remains unresolved because of the live scenario.

### 11.4 Accepted Evidence Still in Force

- Full EditMode: 1,124 / 1,124 passed (job `1ae64b169f774fa2ba628520cf8334d5`), from the post-reconciliation run; not rerun here.
- Phase 5A hostile suite: 26 / 26 passed (job `7bfcf00db9274c728fe6237bd5f5e8fa`); authority PlayMode: 1 / 1 passed (job `0528cea78fda4b7c8121c37950c576d0`). No relevant source bytes changed after these accepted runs.
- The current Windows executable and runtime DLL hashes still match the accepted audit. The clean startup/Player.log result remains historical; no standalone relaunch was performed.
- Human background strategy remains advisory only; emergency priority and provider output do not grant player authorization; configured simulation AI autonomy remains supported.

### 11.5 Closeout

- Production code changed during this reconciliation/verification continuation: **NO**. The accepted Phase 5A production changes already present in the dirty working tree remain untouched.
- Current full PlayMode baseline: **not cleanly confirmed** because one live-provider scenario failed and Unity MCP did not preserve enough result detail to classify its cause.
- Source manifest: **78 sources, 217 changed files, 79 XML test artifacts**; refreshed through the established script and verified with `-VerifyOnly` after this addendum.
- No commit or push was made. **Do not begin Phase 5B.**

## 12. Live Luna Mill Diagnostic — 2026-10-07

This read-only continuation investigated the one failed case from full PlayMode job d957a71711a14314a727b6b1da4a4f5f. It did not rerun the full suite, change source, reset the working tree, or begin Phase 5B.

### 12.1 Test contract and fixture

- Test: Assets/Tests/PlayMode/CommanderPhase5ALiveRuntimePlayModeTests.cs, MillNearVisibleBerries_RealLunaNativeConstruction.
- Exact player input: "Build a mill near the berries my villager is working."
- Fixture: a two-player simulation with player 0 as the human Commander and no simulation AI; both civilizations are French. The map region is cleared to visible legal grass. Player 0 owns a Town Center and two Houses and starts with 10,000 food, wood, gold, and stone.
- Workers/resources: four idle villagers are added as construction candidates. A 20,000-unit berry node is added at the worked-resource location, and a fifth villager is set to Gathering with TargetResourceNodeId bound to that berry node. The gatherer is separate from the idle construction candidates.
- Provider limit: six submitted semantic calls per loaded test domain. A normal Unity scripts refresh caused a domain reload; a read-only runtime reflection check then confirmed submittedProviderCalls=0 before the isolated test started. The two diagnostic attempts consumed two calls.
- Expected semantic path: a valid Request containing one BuildStructure:Mill node anchored to WorkedResource with relation Near. The test source does not assert this exact semantic result: Submit asserts one provider call and IsValid, then the test waits for a completed Mill. It conditionally approves a preview only when PendingActionPlan is non-null.
- Completion predicate: a new player-0 Mill is no longer under construction before 6,000 native ticks. The test then requires exactly one new Mill, exactly one Commander-origin PlaceBuildingCommand, distance less than 15 tiles from the berry node, and the canonical Mill wood cost deducted.
- Tick loop: CommanderGoalManager.Tick runs whenever CurrentTick is divisible by 15, simulation.Tick advances every loop iteration, the coroutine yields every 100 iterations, and the loop stops at 6,000 native ticks.

### 12.2 Isolated live attempts

| Attempt | Unity job | Result | Provider trace | Normalized semantic result |
|---|---|---|---|---|
| 1 | 0a3a0151df9843a1a82b0a5946c60cba | 1/1 passed; test 16.461 s | HTTP 200, 14,776 ms, content length 180 characters, finish=stop | IsValid Request; one BuildStructure:Mill node, count 1, anchor WorkedResource, relation Near; preview=false |
| 2 | fa99d068d2124d3e9e9df496fd6e6404 | 1/1 passed; test 3.568 s | HTTP 200, 3,020 ms, content length 180 characters, finish=stop | IsValid Request; one BuildStructure:Mill node, count 1, anchor WorkedResource, relation Near; preview=false |

The exact input was the same in both attempts. Neither result requested confirmation. The safe normalized semantic result is retained here, not raw provider text. No Luna/OpenRouter credential values were read or recorded.

### 12.3 Observed gameplay lifecycle in both isolated attempts

| Stage | Result and evidence |
|---|---|
| Semantic validation | Passed. The recording provider returned IsValid=true and the test completed. |
| Request/goal admission | Passed. Unity logged Goal #1 submitted: BuildStructure and intent=admitted;goal-submitted=True. |
| Request ticket identity | Not available. The fixture leaves CommanderGoalManager.RequestTracingEnabled at its default false, so the bounded request trace did not emit a ticket ID. Goal #1 is the observed local goal identity. |
| Worker choice | Villager #0 was selected. Commander status reports it placing and then travelling to the new building. The exact internal reservation record was not emitted. |
| Worked berry binding | The goal status names the worked Food node at (134,133); the test report identifies berry node #0. The fixture explicitly binds the gatherer to that node. |
| Placement | Mill placement resolved at (134,133); the final test assertion verified distance less than 15 tiles from berry node #0. |
| Build command | Exactly one PlaceBuildingCommand was captured from CommandEnqueueSource.Commander through the CommandBuffer event, as required by the passing assertion. |
| Simulation/building creation | New Mill/building #3 appeared in the simulation. The goal reported villager #0 travelling to building #3. |
| Construction progress | Goal status reported building #3 advancing with villager #0 after the SyncCheck tick-100 log. |
| Completion | The test observed completed Mill #3 and passed its non-construction, single-building, command-count, distance, and cost assertions before the 6,000-tick deadline. The exact completion tick is not reported by existing diagnostics. |

Both isolated runs therefore demonstrate that the observed correct semantic path traverses admission, worker selection, placement, Commander command enqueue, simulation building creation, construction progress, and completion.

### 12.4 Historical comparison

The 2026-10-06 case in Docs/CommanderPhase5A/live-five-attempts-1d933713.xml also passed with the same exact input and normalized Request: BuildStructure:Mill, anchor WorkedResource, relation Near. Its output records goal admission, placement at (134,133), villager #0 travelling to building #3, construction progress, and completedId=3;berryId=0. The passing test also exercised the same assertions for one Commander-origin PlaceBuildingCommand, cost, distance, and completed state. The historical output did not retain an HTTP status, finish reason, raw semantic text, request-ticket ID, or exact completion tick.

For the original failed full-suite run, the only persisted MCP evidence remains 201/201 completed and one tick-6000 failure with goals empty. Searches of project Library, Logs, and Temp plus the default Unity Editor.log found no NUnit XML or matching run output to recover the missing data.

### 12.5 Original failure and classification

- The failure occurred at TickUntil's deadline assertion, after Submit returned. By the source's control flow, the original run had one provider call and a non-null IsValid semantic result; a provider/network/parse blocker would have failed or marked the test inconclusive earlier.
- The available failed-job message shows manager.Goals was empty at the deadline. It does not prove whether a goal was never admitted or was admitted and later removed.
- The original provider HTTP status/finish reason, semantic mode and node, admission reason, preview/confirmation path, request-ticket ID, selected worker, berry binding, placement, command enqueue, and construction trace were not retained. The failed job's result remained null, with no NUnit XML or stack trace.
- Current source accepts valid Answer, Clarify, and Unsupported outcomes, and CommanderChatUI returns without creating a gameplay goal for those outcomes. Because this live Mill test checks only IsValid before waiting for a Mill, a valid non-effectful semantic outcome can also reach the same deadline. A correctly shaped Request rejected during tactical admission is another possible path. The missing original semantic output prevents distinguishing these from a goal/lifecycle defect.

Required report fields:

| Field | Finding |
|---|---|
| isolated job(s) | 0a3a0151df9843a1a82b0a5946c60cba and fa99d068d2124d3e9e9df496fd6e6404 |
| provider call made | Yes, one per isolated attempt; original failed run also reached the one-call assertion by source-flow inference |
| provider result | Isolated: HTTP 200, finish=stop, 180-character content; original failed run: exact metadata unavailable |
| semantic mode | Isolated: Request; original failed run: unavailable |
| validation | Isolated: passed; original failed run: IsValid passed, exact admission validation unavailable |
| confirmation | Isolated: not required, preview=false; original failed run: unavailable |
| goal admitted | Isolated: yes, BuildStructure goal #1; original failed run: unknown, empty at deadline |
| worker selected | Isolated: villager #0; original failed run: unavailable |
| berry bound | Isolated: worked Food node/berry #0; original failed run: unavailable |
| placement resolved | Isolated: (134,133), near berry #0; original failed run: unavailable |
| Build command issued | Isolated: one Commander-origin PlaceBuildingCommand; original failed run: unavailable |
| construction entity created | Isolated: Mill/building #3; original failed run: unavailable |
| completion tick | Isolated: completed before deadline, exact tick unavailable; original failed run: not completed/observed by tick 6000 |
| final result | Isolated attempts: both passed; original full-suite job: failed |
| classification | UNCLASSIFIED; provider semantic variability (A) is plausible, but semantic/admission defect (C/D) is not excluded |
| production defect confirmed | NO; neither proven nor ruled out for the missing original response |
| test/infrastructure issue | YES; failed-job diagnostics are absent and the live test does not gate on its expected semantic result |
| Sol work required | No confirmed production fix. A later Sol review of live-test outcome classification/diagnostic retention is recommended; no code was changed here. |

Final diagnostic verdict: LIVE LUNA FAILURE STILL UNCLASSIFIED. The two fresh isolated runs and the historical case rule out a consistently failing native Mill lifecycle for the observed correct Request, but they do not identify the semantic result or first incorrect stage in the original failed full-suite attempt. Preserve that failure as unresolved; do not begin Phase 5B.
