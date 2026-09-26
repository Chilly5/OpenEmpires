# Commander Phase 4D.3 Task 3 — host integration report

Status: **DONE_WITH_CONCERNS** on 2026-09-25, including the independent-review test-adequacy follow-up below. This is the scoped host gate, not the Phase 4D.3 final gate. The Task 4 runtime/recovery proof, remaining review disposition, and fresh full EditMode/PlayMode suites remain. Production and test source are frozen for root; no staging, commit, reset, clean, package, setting, or credential change was made.

## Result and authority boundary

`CommanderChatUI` captures one owner-scoped active plan's detached `StrategicAdaptationSource` synchronously before `TranslateWithMemoryAsync`. It refuses multiple active plans at request start, and records the latest retained planner plan ID so a transient new plan cannot evade source-set staleness by becoming terminal. After translation, it verifies owner, pipeline instance, host generation, plan count/lineage, Task 2 freshness, and the exact bridge pending intent reference/ID before displaying a proposal. A no-plan recommendation retains ordinary Approve, but a plan appearing before preview or approval makes it stale.

For an active plan, the preview says the old plan is still active and that ordinary Approve cannot replace it; it notes that explicit **Confirm as command** can be refused or may coexist, rather than promising replacement. Ordinary Approve on a proposal is a no-op that preserves the pending bridge identity. Confirm recaptures current source and lineage before `strategicBridge.Confirm`, then uses the existing `StrategicApprovalLayer.Evaluate` → `StrategicPipeline.EvaluateApprovedIntentNow` decision/commitment/planner path. A stale request clears proposal and bridge pending, emits a bounded stale message, and causes no planner submission or gameplay command. Dismissal, a new player message, lifecycle action, reset, reinitialization, pipeline replacement, failure, and destruction clear adaptation state. No-plan pending recommendations retain the pre-existing preference/explanation-query UX.

The sole cross-task production change was root-approved after behavioral RED: Task 2's `StrategicAdaptationProposalBuilder.Build` now accepts only the already-supported `AttackPreparation` parameter `{focus:cavalry}` in addition to empty parameters. `targetCount`, budget, priority, and other unsupported parameters still fail closed; the planner, bridge, approval layer, goal manager, and Task 1 protected planner were not edited in Task 3. Task 2 value SHA-256 changed from accepted `D59A5EF6D4ED363C79B9F3AD75761673DC0DFA2ED095268EE17BA93DC5B4A497` to `85111D1E1C77F4E08BF1FFC3F130DBDA3F5D34D2BDA4E88A548F2DD332AA754A`.

## Files and diff

- Production: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`, `CommanderChatUI.StrategicControls.cs`, root-approved `Assets/Scripts/AI/Commander/Strategic/StrategicAdaptationProposal.cs`.
- New focused tests: `Assets/Tests/PlayMode/CommanderPhase4D3HostPlayModeTests.cs` and its `.meta`; one added value regression in `Assets/Tests/EditMode/CommanderPhase4D3Tests.cs`.
- Reconciled inherited tests: `Assets/Tests/PlayMode/CommanderPhase4B2PlayModeTests.cs`, `CommanderPhase4C1PlayModeTests.cs`, `CommanderPhase4C2PlayModeTests.cs`.
- Pre-review tracked host/inherited-test diff: `phase4d3-task3-tracked.diff` (SHA-256 `1284ADB777EC80962AE9C9C96DDCE4963ECB6735B6757A77C962CF9787779EF3`); the final C2 review delta is `phase4d3-task3-review-c2.diff` (SHA-256 `76A099C8DF8C689C01351FE493EED719A8B2E1B40D5AA7E634E200F0A8B68D96`). Together they describe the final tracked Task 3 source/test state. The new PlayMode file and already-untracked Task 2 files are identified by hashes below. `git diff --check` reported no whitespace errors; Git displayed only LF→CRLF warnings for pre-existing working-copy files.

The inherited-test contract updates are scoped, not blanket weakening:

| Test | Old assertion | New proof |
|---|---|---|
| 4B2 emergency/confirmation runtime | Ordinary Approve consumed an active-plan recommendation and returned emergency rejection. | Ordinary Approve returns null and retains exact pending intent; explicit Confirm then replaces the AI-owned emergency plan; direct-player protection remains asserted later in that test. |
| 4C1 rejected-decision memory | Active-plan host Approve produced a rejected decision/memory entry. | Host Approve is asserted no-op with same trusted pending intent; direct pipeline evaluation of that recommendation produces the rejected record, and memory still records `Rejected` with no approved-strategy entry. |
| 4C2 rejected-attack explanation | Host Approve against emergency defense recorded rejection and explained `AttackPreparation`. | The initial Task 3 workaround manually injected private explanation metadata after a direct pipeline call; independent review identified that as an inadequate host association test. Final fixture asserts ordinary Approve no-op and exact pending identity, then calls real host Confirm against an incompatible direct-player plan. The approval layer returns `Rejected`, host records that decision, and offline `why are we not attacking?` explains the same recorded reason and `AttackPreparation` with no private-field injection. A separate policy test preserves the emergency-defense rejection reason. See follow-up evidence below. |
| 4C2 explanation immutability | Relied on active-plan ordinary Approve to seed an earlier rejection, then kept a second active-plan recommendation across explanation questions. | Direct pipeline evaluation seeds the same emergency rejection after host Approve no-op; the emergency plan is cancelled, then a no-plan pending intent is retained across offline explanation queries. Plan/goal/reservation/command/provider/memory invariants remain asserted. Active adaptation on any new message is separately tested in Task 3. |

## RED → GREEN and terminal evidence

One Unity runner owned every job to terminal. All counts below are genuinely discovered tests; zero-discovery and test-job initialization failures were excluded.

| Gate | Job ID | Observed terminal result | Preserved payload |
|---|---|---|---|
| Initial host RED, before production | `15c99a826ec445ef8e7637dd425f7090` | 3 discovered, 3 failed: active-plan preview falsely said no plan; multiple-plan request called provider; revised source left bridge pending. The MCP job later aged out before an artifact copy, so the exact failure messages are retained in this report, not claimed as a saved native payload. | Tool transcript only |
| Initial host GREEN | `afff2121eaa649d587c941d80f55033f` | 3/3 passed | Tool transcript |
| Supported focus host RED | `9ce4d6215e1b446db3e892d4c84a059e` | 1 discovered, 1 failed (`PendingStrategicIntent` null). This job aged out before artifact copy; no full payload is claimed. | Tool transcript only |
| Supported focus value RED | `d1d61c50d14049a0a36460512d331ba8` | 1 discovered, 1 failed (`Build` returned null for supported focus). | `phase4d3-task3-red-focus-value-d1d61c50.json` (SHA-256 `0298024F062BA6D766DFF92AA5AC72A4C5317414349DE48C98189138275804C1`) |
| Supported focus value/host GREEN | `3577ea9408ff47ab8c6b34ce49024d2d`; `ca3ebd7754604f4b89bc91ead0767e45` | 1/1 EditMode; 1/1 PlayMode | Tool transcript |
| Transient source-set await RED | `1fe1e5c1e3b949a2a251505d07f50f36` | 2 discovered, 2 failed: no-source and one-source transient plan both left pending. | `phase4d3-task3-red-source-await-1fe1e5c1.json` (SHA-256 `77269E1FC997DF06F66BC2BBF5C097732832D28F8061F6D79F02E5B85E5C98BA`) |
| Transient source-set confirmation RED | `76a1bfd7a6b043e99bc5672425f4b84f` | 2 discovered, 2 failed: ordinary no-source Approve and one-source Confirm returned decision records after transient plan. | `phase4d3-task3-red-source-preview-76a1bfd7.json` (SHA-256 `5F3F6F4E5601A57B299FDE36F2A9872AF89063A0A931AAC47AC45CE29548A67E`) |
| Transient source-set GREEN | `ee722ea2230943b7b73c7d92f60a9c6c` | 4/4 passed | `phase4d3-task3-green-source-ee722ea2.json` (SHA-256 `D6B50720F635F72EA9FAF5E71BF250937690DC7B947C7A7D2420618BA7A1EA99`) |
| Initial affected regression | `a209339c2417461095fba05e9b6fe042` | 62 discovered, 5 failed: the five exact 4B2/4C1/4C2 IDs reported to root; original pending-query regression was fixed in production, three obsolete active-plan Approve expectations were reconciled as above. The job aged out before artifact copy. | Tool transcript only |
| Final Task 3 focused PlayMode | `882cc74c641d493d940c462b008ff0ae` | **26/26 passed, 0 failed/skipped** | `phase4d3-task3-focused-final-882cc74c.json` (SHA-256 `4530378C65E5E49CD4DF0FC73022E0FCA018BF8E7A80B1CACD68CF48C4A094BC`) |
| Final affected 4B2/4C/4D1/4D2 host PlayMode | `f140ad8c68044d10be8e86b515203063` | **62/62 passed, 0 failed/skipped** | `phase4d3-task3-affected-f140ad8c.json` (SHA-256 `2C3DF6AC69221005915C9DF4EE9F3A3E205AFF83BA228319A2E573A447430492`) |
| Final affected 4D1/4D2/4D3 EditMode | `d1e6c7871ac443609391c47163bd78af` | **112/112 passed, 0 failed/skipped** | `phase4d3-task3-edit-d1e6c787.json` (SHA-256 `A6E0BDDB4D81B6854CB4A80A0D50D4BDFAC96FE4BF0458788CDB52CB675E7344`) |
| Final 3C5 decision EditMode | `86c490d6b34f45939aad8b42681a3078` | **13/13 passed, 0 failed/skipped** | `phase4d3-task3-3c5-86c490d6.json` (SHA-256 `B15BCA1E0F89A1A73FF0ADF5DD85C671474D20CFED8BED7C7FC735D27C5F8D40`) |

The JSON files are the complete available MCP terminal payloads and include exact discovered full test IDs, individual states, outputs, job IDs, and summaries. No native XML was returned for these jobs. Early fixture failures `eb27574bf0ea4509b75db5c11ec3f1ef` and `1a610f7182454abfa9e373120077f547` did not establish the main active-plan behavior because the fixture had not reached a spending milestone. Jobs `b6ee665dbbec4b1bb11a3df9f66b2c72`, `7946c7a8957e465d844ba18d8fb4c944`, `20ba67f2af81404baef35a8fc2379acf`, `f0759bdb13b24ecbb50bab7df59f1be8`, `04a332c8d5e9475fabc81ea8532e09c8`, `90c14bff5dc04cbda2b95ef5a6a20d1d`, and `ceb6d281105e4040b8fa07325b05f32f` did not initialize tests during Unity reload. Jobs `48298df5dca7488bbbd681a38b2a6c5e`, `b0cbf58335364efca9396d579f52a6a0`, and `5fbb673d75624560871486dfe693fb3a` reported zero discovery. None is counted as pass/RED evidence.

The final focused test uses a real `StrategicPlanner` reservation created by the Ranged plan's Production milestone (150 Wood), not a synthetic reservation. It verifies pending has unchanged plan/revision/goals/reservation/decision history/command buffer, ordinary Approve leaves intent ID and old reservation untouched, and Confirm uses the exact ID and existing decision path, cancels the incompatible AI plan, releases its real reservation, and starts DefensiveTurtle. The remaining tests cover direct-player protection, compatible coexistence, stale source revision/cancel/complete/replacement/blocker change/pause-resume, transient source-set changes, owner/pipeline/reset/dismiss/new-message/failed-translation paths, and unsupported parameter rejection. Provider calls and no extra plan/goal/reservation/command/decision effects are asserted where applicable.

Unity console error query after the final focused run found zero compiler `error CS...` entries. `phase4d3-task3-console-errors.json` (SHA-256 `F0E84AF33102E5334E0E20530D79DDD01BB2F5DAF454153D1D93DAA4FE0C4F13`) includes 25 expected plan/goal/reservation cancellation diagnostics; these are not compile errors. `git diff --check` found no whitespace errors.

## Frozen source SHA-256

| File | SHA-256 |
|---|---|
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs` | `D999D806A54CF515AD98E223B618393C8CAF20004133BF9DF7C3CAA85734E8CD` |
| `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs` | `F5642EF3806645AE328336EE285FDD14301E0F3E9771CE20C0E92FC7C54A0A2B` |
| `Assets/Scripts/AI/Commander/Strategic/StrategicAdaptationProposal.cs` | `85111D1E1C77F4E08BF1FFC3F130DBDA3F5D34D2BDA4E88A548F2DD332AA754A` |
| `Assets/Tests/PlayMode/CommanderPhase4D3HostPlayModeTests.cs` | `9A1EC73A383FE9CAD57F838E2B1F4FD8F5870B1044B39AA9A9A3D6CED854EA4D` |
| `Assets/Tests/EditMode/CommanderPhase4D3Tests.cs` | `4DC7CE9A67A0BD4E793191911500F797DFFA85DF91195C95A5101D7ABC23AA3E` |
| `Assets/Tests/PlayMode/CommanderPhase4B2PlayModeTests.cs` | `6F1811CBF8744277F7B6095D4BCB9A6875C675DB61DD44ABAD62CDAA4A78E621` |
| `Assets/Tests/PlayMode/CommanderPhase4C1PlayModeTests.cs` | `F80F15F15EEDBCD48B37D0529E43DF51D57D826E8E70DC5BD3C41593D72DE819` |
| `Assets/Tests/PlayMode/CommanderPhase4C2PlayModeTests.cs` | `647BBE0E8E8FAA2A77C0A0286A1E9C1D778BE9987AFC58FDD1CF11211CADBBE1` |

## Residuals and handoff

- The independent Sol authority/staleness/test-adequacy review is still required; root owns that gate. The older initial host RED and initial affected RED terminal payloads aged out of Unity's job cache before artifact copy, so this report does not claim those as saved evidence. Later four-case source-set RED payloads and focused/affected GREEN payloads are preserved.
- This task does **not** prove full Phase 4D.3 completion or the natural temporary-resource/population recovery scenarios; those and fresh complete suites are Task 4. It does not claim to fix tactical Spearman wood gathering.
- Exact current dirty workspace state from Tasks 1/2 and earlier 4D.2 work was preserved. No Task 3 change was made to protected `StrategicPlanner.cs` or `CommanderGoalManager.cs`.

## Independent-review test-adequacy follow-up

The Sol review found that the inherited C2 attack-explanation test used a direct pipeline evaluation plus reflection writes to `pendingExplanationIntentId`/`pendingExplanationObjective`; that could pass even if the real host Confirm-to-explanation association broke. The final C2 fixture removes those reflection writes. It creates a real direct-player DefensivePreparation plan, translates cavalry AttackPreparation through the host, verifies ordinary Approve is a no-op retaining the exact pending intent, and calls `chat.ConfirmStrategicCommand()`. The bridge transfers the trusted ID to a new `AIConfirmedPlayerCommand` intent; the approval layer refuses it before selection because the direct-player plan has higher priority. Thus the actual result is `StrategicDecisionStatus.Rejected` with no selected intent or submission, not `TransitionRefused`. The host records the refusal, and the offline attack query must explain that same decision reason and identify `AttackPreparation`. The test checks history, memory status, absence of an extra approved-strategy entry, and unchanged plan/goal/reservation/command state. A separate scoped policy test keeps the original `Emergency defense has higher priority` rejection assertion.

| Follow-up gate | Job ID | Terminal result | Evidence |
|---|---|---|---|
| First host fixture attempt | `e16c10cfc2e6491bbc15845e0585f5e0` | 2 discovered, 1 failed on incorrect same-reference expectation: Confirm transfers the intent identity to a new trusted object. Fixture-assumption failure, not product RED. | Tool transcript |
| Second host fixture attempt | `8dcf581aa81742f2a501bfc037f6a68c` | 2 discovered, 1 failed because the test counted the already-approved direct-player setup plan as an unwanted new approved strategy. Fixture-assumption failure, not product RED. | Tool transcript |
| Focused host and policy | `bf016c66fd09475195095c5de89c10b1` | **2/2 passed**, zero failed/skipped. | `phase4d3-task3-review-focused-bf016c66.json` (SHA-256 `6B0E9A20BED33C96C251A73844EB0D2EF0B7CDA32C042B93AE2E789FF8041B21`) |
| Controlled host-objective mutation RED | `3a2d4c0d42984a28b353c8948cc00013` | 1 discovered, 1 failed: temporarily blanking only `pendingExplanationObjective` made the offline query report no rejection attributable to AttackPreparation. This proves the new test detects the reviewed host-association break; it is a mutation RED, not an existing product defect. The original production line was immediately restored and its SHA-256 verified identical (`D999D806A54CF515AD98E223B618393C8CAF20004133BF9DF7C3CAA85734E8CD`). | `phase4d3-task3-review-mutation-red-3a2d4c0d.json` (SHA-256 `3DA199BB22809B05922774F7AE512B9043D443B3971559C0BF4AF5ED6CF3CA96`) |
| Final affected C1/C2/4D3 PlayMode, including added identity assertions | `99a7445b3b334c869acd0249013d8726` | **50/50 passed**, zero failed/skipped. | `phase4d3-task3-review-affected-99a7445b.json` (SHA-256 `4517A5E2CB62A23A6704864BABCFD3080D49CBCC6A3C95A518D929A469E70638`) |

The bridge-reload job `2e5c28bc82da4b7aa7706a80ede353f3` did not initialize tests (0 discovered) and is not evidence. No native XML was returned for the follow-up jobs; the complete available MCP terminal payloads are saved above. A post-restoration console query returned zero error entries, and `git diff --check` returned no whitespace errors. No production source changed in the final follow-up; only the C2 tests and this report/evidence were added or revised. Full suites remain root-owned Task 4 work.
