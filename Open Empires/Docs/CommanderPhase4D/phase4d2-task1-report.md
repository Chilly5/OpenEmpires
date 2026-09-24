# Phase 4D.2 Task 1 report — detached plan health

## Agent, task, expected output

Agent `/root/phase4d2_core_impl` was the sole Task 1 production writer and Unity test runner. Scope was the detached value DTO, planner-only `CapturePlanHealth(int trustedPlayerId, int planId)`, and focused EditMode tests. Host/UI, provider integration, PlayMode, and Tasks 2–3 were not touched. The root had confirmed Phase 4D.1 scoped Gates 1–5 PASS before dispatch. The existing dirty Phase 4C/4D.1 files and scene churn were preserved; no stage, commit, cleanup, credentials, or paid API call.

## RED and diagnostic history

- Initial compilable interface skeleton returned `null`; test-first focused job `8c022360a95948afae9404e5877b29f9` discovered four tests and failed `PlanHealth_IsDeterministic` at `Expected: not null; But was: null`. Its `ResourceWaiting` fixture also failed its precondition because RangedReinforcement's first milestone was Active, not waiting; fixture changed to CavalryPressure. The old job payload is no longer retrievable from Unity after bridge reload; this assertion is from the observed terminal response, not a saved native XML.
- Before that, jobs `2c05474ca076485788a31a3143b0525c` and `cb417ad8b874448fbe1f54fb344af50c` each discovered **zero** tests before import and do not count as RED or GREEN.
- The first four-test GREEN was `b01a702400424cf1bd5dac349eb9e611`, 4/4 on an earlier source. It is not current-source evidence.
- An expanded Unity MCP call lost its handle while the editor executed; the Editor log showed TestRunner's final interaction-mode restoration. That unattributed run is diagnostic only. Subsequent job `e0e051aea3044742ad9cd269c79f0257` also lost the bridge; root independently verified editor idle before assigning runner ownership back. Neither is claimed as GREEN.
- Expanded job `446396b3bad94c16bae6efb87885ef42` discovered 22 tests and failed five on a shared fixture: Production, not Force, remained current. Diagnostic job `825e18c2857647219eeb0260eef6f820` showed the production child was `Blocked` because no visible reachable Archery Range build location was found. The fixture was corrected to create the Archery Range before plan submission, avoiding tactical blockage. Jobs `06ff98c43b864e2a9f79d034efe4e516` and `a885162512384818b522b9c9110d791e` failed to initialize after refresh; their terminal result was not a test failure. `40ae079e132c494790f2b2a400be6dc3` then passed 22/22.
- Adding five edge tests produced job `0f045a19d28a4e77971b340bd5eb5903`, 27 discovered with one fixture failure: the blocked-duration test set a pause tick ahead of simulation's observed tick. The fixture set the simulation tick to 20, producing `513df12f57d64785be01cd104b5a85fb`, 27/27.
- A new behavioral test `ActiveWithoutExecutionOrProgress_IsUnknown` produced meaningful RED in job `2ffeafef619e4b088cda6cfd073d2c12`: 28 discovered, expected Unknown but got Healthy. Its terminal payload is saved as `phase4d2-task1-2ffeafef619e4b088cda6cfd073d2c12.json`. Production then required executing/planning or completed progress evidence before returning Healthy.

## Implementation

Created `StrategicPlanHealthSnapshot.cs` and `StrategicPlanner.Health.cs` with `.meta`. Capture validates owner, exact plan ID, disposition, plan/milestone enums, and only then builds a fresh owned `CommanderContext` from `goalManager.Simulation`. It does not accept caller context. It copies current milestone children from `RequiredChildGoals`, historical completion from `CompletedChildGoals`, finer retained status from `GetGoal`, owner resource/population/production values, exact unit-registry living counts, owned non-destroyed building queues, and active reservation totals. The DTO exposes immutable scalar/value properties and read-only copied lists; explicit `ToJson()` omits mutable plan/goal/context objects, status-reason and outcome strings. Classification uses typed statuses, terminal/paused ranking, population predicates, and ID-ordered secondary facts. Blocked duration uses widened tick arithmetic. No health path calls Tick, tactical Plan, provider, command buffer, or a mutating simulation operation.

## GREEN and compiler evidence

Final source focused job `b1663c301f944ef2844fd62425571298` passed **28/28**, zero failed/skipped. The complete terminal payload, including all discovered test IDs, is `phase4d2-task1-b1663c301f944ef2844fd62425571298.json`; SHA-256 `7DE7159A04164F0D18A37E6D2257AD814A3DFFF2E57E8CBBFE93C8C24B2C58F7`. A preceding post-fix job `56012c38d7f44b01b83c195679aa9125` passed 28/28 before the ID-ordering self-review adjustment. `4bb07f380b064f4781fbf2e7288c454f` failed to initialize after refresh, then the final job completed. Unity `read_console(types=[error])` returned zero errors after import. No native NUnit XML was returned by the Unity MCP test tool; the full JSON terminal payload is preserved instead.

Final SHA-256:

- `StrategicPlanHealthSnapshot.cs`: `0B6F23463A9208F37C9096617AAD301D8C68C7D80B0F1E47B4D4967DC79B95D3`
- `StrategicPlanner.Health.cs`: `8B683E92FB71354F169A46151227E30C3E5A07D82393F010016CD43C30A11B76`
- `CommanderPhase4D2Tests.cs`: `6EDFB66B30BB1F0B3AD7FC06E33871CB76EF877836B1E627D620D6BEEB1B31A7`

## Self-review and remaining concerns

The Task 1 test group verifies deterministic recapture, zero plan/simulation/reservation changes, provider non-invocation, terminal and paused categories, fog independence, detached values, unknown/foreign/disposed plan rejection, archived child eviction, resource summary separation, population queue and garrison predicates, max-cap non-House wording, mixed blocker ranking, malformed plan enum, unresolved requirements, and paused blocked-duration boundary. No existing protected or Phase 4C source was edited, so no protected before/after hash waiver was used.

Independent review is still required by root. Focused EditMode success is **not** the 4D.2 full EditMode/PlayMode gate, and no host/UI or in-game runtime observation was performed here. Remaining coverage gaps: all detailed queue threshold/above-cap variants, overflow injection, explicit status-enum corruption, and large-bound stress were not separately exercised. The root should assess whether the current resource-deficit semantics and bounded strings need tightening before Task 1 acceptance. If the reviewer finds an Important issue, Task 1 must remain open until corrected and freshly verified.

## Independent-review correction pass (supersedes the preceding open concerns)

Root's independent Task 1 review identified four Important findings. This pass remained limited to the three Task 1 source/test files, their existing `.meta`, and report/evidence. No protected, host/UI, Phase 4C, reservation, tactical execution, or plan lifecycle source changed.

1. The `EnsureUnitCountGoal` health mirror checked capacity even when living matching units already met the target. `CommanderPlanner.PlanUnits` returns Completed before its capacity check. Added `SatisfiedUnitTarget_OverCapQueueDoesNotInventPopulationWait`; capture now requires `ownedCount < TargetTotal` before classifying population wait, while still copying the raw queue/cap evidence.
2. `MilestoneName` copied an arbitrary string. Added `MilestoneName_IsDeterministicallyBounded`; the snapshot now truncates to at most 96 UTF-16 code units, avoiding a cut after a high surrogate, and serializes only that copied bounded value.
3. The first 128 child detail records could not describe all retained current-milestone statuses. Added a bounded, immutable `RetainedChildStatusCounts` projection covering every required child ID, with each defined `CommanderGoalStatus` plus a nullable Unknown bucket for retained invalid status. Missing goals remain represented by `MissingHistoryCount`, not invented fine statuses. `LiveStatusCounts_IncludeChildrenBeyondDetailLimitAndInvalidStatus` exercises 132 required IDs, a retained status beyond detail limit, and malformed status.
4. `MilestoneDeficit` subtracted this plan's own reservation from usable stock. Added `OwnReservation_DoesNotCreateMilestoneDeficit`; deficit now subtracts only **other plans'** active reservations from owned stock. `Available` continues to reflect the existing global reservation rule; plan-owned and global reservation totals remain separate.

Additional specified cases now covered: `QueuedProduction_IsExpectedWaiting`, `PopulationAndMilestoneResourceWait_AreBothPreserved`, `BlockedChild_OutranksMilestoneResourceWait`, and `BlockedDuration_IntMaxBoundaryAndMissingPauseAnchorFailClosed`. The snapshot reflection test includes the new status-count DTO. The implementation uses only typed evidence and remains read-only.

### Review RED, GREEN, and job evidence

Compilable skeleton-only API was added for the new status-count projection before behavior. A first import had test compile error `CS1503` at `CommanderPhase4D2Tests.cs:417` because NUnit `Does.Not.Contain` selected the string overload; that assertion was changed to `Is.Not.Member`. Jobs `a9a988dbc3ec441d9142f2fbb1a88cd5` and `8c5ccd062ad741869838d3d009bb9691` discovered only the previous 28 tests during that compile failure and are **not** RED/GREEN evidence for the review tests. After compilation, job `912f2f33325848f08e27207b021e29a4` discovered all 35 then-current tests and failed the four exact review behaviors: population returned `WaitingForPopulation` on a satisfied target; name length 500 exceeded 96; status-count lookup found no record; and deficit was 300 when own reserved stock fully funded the 300 requirement. Full terminal RED payload: `phase4d2-task1-review-red-912f2f33325848f08e27207b021e29a4.json`, SHA-256 `9F25CFD2BD5A6651CC0EEDF68BC5B21C105385245B4AF43144F9887ED1A4A011`.

After correction, job `be1662ddf57b4181a22b79b10cb37400` passed 35/35. The final boundary test was then added. A post-refresh job `6a82cc67b06740b5a42d800da8ebddbf` terminated with `Test job failed to initialize (tests did not start within timeout)`, with zero tests run; no overlapping job was launched. Final current-source focused job `0d4a5f26f1bf49dc89ad3f4f950f2095` passed **36/36**, zero failed/skipped; complete terminal payload `phase4d2-task1-review-green-0d4a5f26f1bf49dc89ad3f4f950f2095.json`, SHA-256 `35BF79545FED12315824CC649F5F6857D765594B6FB19037B73966A2A723DD2F`.

A subsequent serial affected EditMode regression job `dfe34ad522e940bda0501dadae0096a6` passed **75/75** across `CommanderPhase4D1Tests` and `CommanderPhase4D2Tests`, zero failed/skipped. Full terminal payload `phase4d2-task1-review-affected-dfe34ad522e940bda0501dadae0096a6.json`, SHA-256 `8DA3097F4A50B4B2F259A82D26168CF269C0E23643BCAE203A2F29FB23C1BCA2`. Final `read_console(types=[error])` returned zero entries. No native NUnit XML was emitted by these Unity MCP jobs.

Final review-fix source SHA-256:

- `StrategicPlanHealthSnapshot.cs`: `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408`
- `StrategicPlanner.Health.cs`: `E47E9BC8C5467D6293EA74782DDCEAB4571195B7B6AB6BA406FA63CC52345253`
- `CommanderPhase4D2Tests.cs`: `08B1F77429106B33578BEB31CFFC02A974594347F6F253BF3B5A16566318600E`

Self-review: the four Important findings are addressed in source and each had observed behavioral RED followed by current-source GREEN. The new status-count list has a fixed enum-derived size and counts all current milestone required IDs regardless of the 128-detail cap; status Unknown is explicit for invalid retained status. Remaining limits: this is Task 1 focused/affected EditMode evidence, not an independent rereview or the 4D.2 full EditMode/PlayMode/runtime gate. Root owns those later checks before phase acceptance.

## Second rereview correction: malformed status and signed queue deficit

The independent Task 1 rereview found two further issues in the existing planner-health projection. `CommanderGoalStatus` invalid values were converted to nullable Unknown for child evidence, but the unit-specific population branch still used the raw goal's `!IsTerminal` result, which is true for an undefined enum. In addition, copied `RemainingOrders` was clamped to zero rather than mirroring the tactical planner's signed `TargetTotal - owned - matchingQueued` arithmetic. Both observations were verified against `StrategicPlanner.Health.cs` and `CommanderPlanner.PlanUnits` before editing.

Test-first additions: `MalformedUnitGoalStatus_CannotAssertPopulationWait` puts an undefined status on an unfinished unit goal exactly at population cap, then asserts the child and primary category are Unknown and neither primary nor secondary claims population waiting. `OverqueuedUnitGoal_PreservesSignedRemainingOrders` queues target plus two matching units, asserts the copied result is `-2`, and checks the `occupied > cap` population predicate remains unchanged. Focused RED job `acd68038f5a245279686dd7441ff8872` discovered 38 tests and failed exactly these two assertions (`WaitingForPopulation` vs Unknown; `0` vs `-2`). Its complete terminal payload is `phase4d2-task1-rereview-red-acd68038f5a245279686dd7441ff8872.json`, SHA-256 `E05B53A4F584D8301FE5EAAA885518C4D0F3DDE230EC6C246F27C9E0AA71E495`. The preceding post-refresh job `68486a9553c54f27a459e085ab679dd4` terminated with a test-initialization timeout and ran zero tests; it is not RED evidence.

The smallest production change requires `status.HasValue` before any unit-specific population classification and preserves the raw checked signed remaining-orders calculation. If an invalid retained status is the only current evidence, completed earlier milestones no longer promote the primary category to Healthy; it remains Unknown. No lifecycle, tactical Plan, reservation, command, or simulation code changed.

Post-change focused job `08368043318a470d9fa8fedc99463f6b` passed **38/38**, zero failed/skipped. Full payload: `phase4d2-task1-rereview-green-08368043318a470d9fa8fedc99463f6b.json`, SHA-256 `1B20DDCAF9628B5664914947FF4CE70053D060941F4778EB2F788B4B1721900D`. The first post-refresh job `7974e321c7af49b3b7b06d3f39640fda` failed test initialization with zero tests, then the terminal focused GREEN was run serially. A subsequent serial D1+D2 affected EditMode job `812c5707b9ab4a82893a0a4320395fc2` passed **77/77**, zero failed/skipped; complete payload `phase4d2-task1-rereview-affected-812c5707b9ab4a82893a0a4320395fc2.json`, SHA-256 `12699A8CCA3BA333D3B4B506ED6A8B51E6F4BE7DF455830EE8FA0F082231D278`. Final Unity `read_console(types=[error])` returned zero entries.

Current source SHA-256 after this correction:

- `StrategicPlanHealthSnapshot.cs`: `A4D73120B5ACAA8D5AEDAB24F48F8511DBF25432B0BA29FCD7735F1E0177A408` (unchanged)
- `StrategicPlanner.Health.cs`: `EAFDFC54C8B98757FBDDC40554C6E60256A5100B23A34D6FAD738ACA7F88EC9B`
- `CommanderPhase4D2Tests.cs`: `E1D4AC8B18A8AE493C870F9F13E8BE4930600D7F57FED828EC308B79DC2ABB89`

Self-review: the malformed status cannot produce a unit-specific population fact; negative overqueue evidence is copied without altering the tactical capacity predicate. No protected or host/UI file was edited. Remaining work is independent rereview and the full 4D.2 EditMode/PlayMode/runtime gate, owned by root; this Task 1 focused result alone does not establish phase completion.
