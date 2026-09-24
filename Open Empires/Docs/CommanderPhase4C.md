# Commander Phase 4C — A–G report

Status: **READY FOR PHASE 4D** after the final scoped population-cap correction.
Fresh full regressions passed 571/571 EditMode and 87/87 PlayMode. A UI-approved
PlayMode run constructed a House dynamically before training the required
force. The final independent review found no Critical or Important issue.
`TechnologyRush`, `SiegePreparation`, and `NavalExpansion` remain deferred and
unsupported. The earlier 568/568 and 86/86 results below are historical.

## A. Architecture

- Phase 4C.1 introduced bounded match-local immutable memory and detached
  request snapshots while preserving the approval, policy, pipeline, planner,
  and executor authority path.
- Phase 4C.2 added deterministic explanations from copied strategic outcomes
  and current-plan primitives. Host projections are detached; read-only query
  routing preserves pending recommendations.
- Phase 4C.3 added owned-state insights and explicit provider serialization for
  detached worker, army, production, and plan-progress observations, with
  fog-safe and capture-side-effect coverage.
- Phase 4C.4 admits only `RangedReinforcement` and `DefensiveTurtle` through
  existing approval/planning/execution paths. Both use canonical costs,
  submission-time budgets, worker fitting, and prepared-economy/resource
  recovery. The tower target is frozen at submission so previously funded
  foundations cannot inflate the goal or charge.

## B. Delegation and ownership

- Work followed the documented single-production-writer and single-Unity-runner
  constraint. The retained ledger names some agents/tasks but not every
  implementation worker or reviewer; those identities are marked as not
  recorded rather than inferred.

| Agent (as recorded) | Task | Expected output | Validation and evidence |
|---|---|---|---|
| Sol — `phase4c1_implementation` | 4C.1 bounded memory and provider/UI/lifecycle integration | Memory types, detached request paths, lifecycle handling, focused RED/GREEN, task report | `phase4c1-task1-report.md`; focused and full EditMode/PlayMode gates; `phase4c1-fixround2-final-boundary-audit.json`; 4C.1 gate record in `progress.md` |
| Sol — `phase4c2_implementation` | 4C.2 explanation service and chat-host integration | Immutable explanation values/service, detached outcome/current-plan projections, neutral queries, tests/report | `phase4c2-task1-report.md`, `phase4c2-evidence-report.md`; focused Edit 18/18, Play 6/6; full Edit 530/530, Play 75/75; scoped rereview and audit in `progress.md` |
| 4C.3 Task 1 implementer — identity not stated in retained report | Owned-state insights and actual provider integration | Detached worker/army/production/progress context, deterministic serialization, tests and task report | `phase4c3-task1-report.md`; independent review `phase4c3-independent-review.md`; full Edit 540/540 and Play 77/77; final regression summary |
| Sol — `phase4c4_implementation_gpt6` | Strictly admit and execute selected supported objectives | Ranged Reinforcement and Defensive Turtle intent/template/feasibility/plans, runtime tests and task report | `phase4c4-task1-report.md`, `phase4c4-independent-review.md`; focused Edit 26/26, Play 9/9; post-fix full Edit 568/568 and Play 86/86; final manifest/audit |
| Sol — `phase4c4_direct_priority_fix` | Repair new-objective handling in `StrategicDecisionPolicy.GetPriorityLevel` | Handle new objective enum values without breaking direct-player priorities; focused GREEN | RED `57a6f0186d67487cb80bcd311dede12c` 2/2; GREEN `0165321d560f4a1ab1608b0f6678579c` 2/2; final full pair covers the fix |
| Sol — independent review (including final gate review) | Review scoped changes, integration/safety, and final readiness | Findings, fix/re-review status, final recommendation | Phase review artifacts; final review recommends READY FOR PHASE 4D with no remaining Critical/Important findings |
| Astra — architecture/discovery review; Luna — `phase4c_discovery`, `audit_boundaries`, `phase4c_evidence` | Architecture review; requirements mapping, read-only audits, evidence packaging and routine documentation | Design/feasibility review, requirements matrix, audit tools/results and evidence docs | `progress.md` delegation ledger, `requirements-matrix.md`, `check-boundaries.ps1`, audit/evidence artifacts; final current-source audit metrics and manifest |
| Luna — final documentation pass | Reconcile A–G, progress, requirements traceability and final summary | Current-state docs with exact evidence, limitations and verdict | This A–G report, `progress.md`, `requirements-matrix.md`, and `phase4c4-final-regression-summary.md`; checked against final post-fix XML/hashes and audit |
| Root | Orchestrate gated phases and own final source/test/boundary/release decision | Final review, regression coordination, current-hash and protected-boundary audit, readiness ruling | `progress.md`; 27/27 current manifest, 12/12 Task 1 snapshots, 55/55 protected entries unchanged, and final READY decision |

- This documentation update does not change production or test code and does
  not run Unity.

## C. New systems and phase status

- **4C.1 — gated passed:** bounded `MemoryEntry`, `CommanderMemory`, and
  `ConversationState`; detached tactical/strategic memory requests; lifecycle,
  reset, capacity, owner, cancellation, and stale-response protection; preserved
  reflection-compatible bridge APIs.
- **4C.2 — gated passed:** immutable deterministic explanation values/service,
  host-only provenance/current-plan projection, and neutral read-only chat
  queries.
- **4C.3 — gated passed:** detached owned-state insights and explicit provider
  payload serialization with fog-safety and capture-side-effect coverage.
- **4C.4 — all gates passed:** strict whole-form admission and
  feasibility for the two
  supported objectives, approval and replay protections, canonical budgeting,
  one-worker fitting, and runtime completion/recovery coverage. Scoped review's
  funded-Tower timing finding was fixed and rereviewed. Root's final review
  also found a direct-player priority gap: new objective values (4) and (5)
  caused `ArgumentOutOfRangeException` in `StrategicDecisionPolicy.GetPriorityLevel`
  (RED job `57a6f0186d67487cb80bcd311dede12c`, 2/2 failures; XML
  `phase4c4-direct-priority-red.xml`, SHA-256
  `1758F9B1991D77FFC6F4C7FB0F2E6982CA9306EB9893C23E7CD3A812B4BC71B4`). The
  policy fix passed focused GREEN job `0165321d560f4a1ab1608b0f6678579c`,
  2/2 (XML SHA-256
  `0004F1D10437FBF4A16235EA2CDE7941BB5807FC8835086EE47582E97B927A69`);
  final independent review recommends READY FOR PHASE 4D with no remaining
  Critical/Important findings.

## D. Safety boundaries

- Providers receive copied context only. No provider authority, direct commands,
  uncontrolled autonomy, hidden information, future prediction, fabricated
  reasons, or bypass of `StrategicApprovalLayer`, `StrategicDecisionPolicy`, or
  `StrategicPlanner` is introduced.
- No resource stock delta is labeled as income. Explanation values remain
  detached from simulation/planning graphs, and queries do not clear, approve,
  replace, or execute pending recommendations.
- Existing public APIs and tactical/strategic behavior remain preserved. The
  initial Phase 4C.4 full EditMode run found two stale pre-existing expectations
  after the new objectives were added: an exact four-objective list expectation
  and a template lookup that rejected optional parameters. A **test-only
  compatibility update** brought those assertions in line with the supported
  objective catalog and template parameter contract; no production behavior was
  changed for these two failures. The final full run is green.
- Final post-fix source audit: changed21, new21, missing0; protected55/55
  unchanged; gaps0; six documented `CommanderChatUI` host references;
  credential-shape0 and assignment0. Final source manifest
  `phase4c4-final-source-hashes.json` matches 27/27 live hashes, including all
  24 Task 1 paths plus the priority policy and two legacy compatibility test
  files. The Task 1 manifest is historical: its `CommanderPhase4C4Tests.cs`
  entry predates the direct-priority regression cases. All 12 Task 1 before
  snapshots match; the priority policy before/current snapshots match. `.env`
  remains untracked and ignored.
- No credentials, package settings, authentication behavior, log suppression,
  commits, pushes, merges, or unrelated source changes were made.

## E. Test, review, and static evidence

- **4C.1:** Full EditMode 512/512 and PlayMode 69/69 passed. Final artifacts:
  `phase4c1-fixround2-full-editmode-TestResults.xml`,
  `phase4c1-fixround2-full-playmode-TestResults.xml`, and
  `phase4c1-fixround2-final-regression-summary.md`. Independent review passed
  after two fix rounds; final boundary audit retained.
- **4C.2:** Focused EditMode 18/18 and PlayMode 6/6; full EditMode 530/530 and
  clean full PlayMode 75/75 passed. Scoped rereview approved; source hashes 7/7
  matched and frozen-boundary audit 55/55 passed. The earlier PlayMode run's
  external Package Manager OAuth failure is preserved, not counted as passing
  evidence.
- **4C.3:** Full EditMode 540/540 and PlayMode 77/77 passed; all seven frozen
  source/test hashes matched; protected boundary audit 55/55, zero gaps; six
  advisory host references; zero credential/assignment matches. See
  [4C.3 final regression](CommanderPhase4C/phase4c3-final-regression-summary.md).
- **4C.4 focused and review:** Final focused EditMode 26/26 and PlayMode 9/9
  passed after the independent review's funded-Tower fix. The final PlayMode
  XML records all nine passes, including funded foundations completing before
  Fortifications and post-approval resource depletion. Independent rereview
  found no new Critical or Important issues; dynamic housing was a coverage
  limitation at that historical gate and is proven in the final round below.
- **4C.4 prior post-fix full regression:** Full EditMode job
  `5c24beac55114fe68b5e0f3ff6e7ea88` passed **568/568**, zero failures/skips;
  all 568 test names are unique and Passed. XML SHA-256
  `D2FA8CB42DFC5D14C840DE2C4E89C1087CF36C8469203303BB72EA022F4704B7`, file
  `phase4c4-full-editmode-postfix-results.xml`. Full PlayMode job
  `8a6b556afc76436abbbe993866864004` passed **86/86**, zero failures/skips;
  all 86 are unique and Passed. XML SHA-256
  `65C83287373175808F42C1BF0AD52E204A89D4ED956F8D1F633148BC2FD8122B`, file
  `phase4c4-full-playmode-postfix-results.xml`; accompanying JSON file
  `phase4c4-full-playmode-postfix-results.json` SHA-256
  `479A798FECF02CAC9F15EAF10CF20831EDF7A810E292AC7C1125DE2AD2B8A9F0`.
- **Historical pre-priority-fix pair:** EditMode 566/566, XML SHA-256
  `23BCE787D289DAD52BEAF525AA724CB08A3153C57BE6F63FB718EB27867B2817`; PlayMode
  `d4d765ae57f44b8db380443e4104dc40`, 86/86, XML SHA-256
  `2C4446792ADD0B72034393399D91ACAE8FB83BABEBA58384B35472DDE64F68F4`. These
  results predate the priority fix and are superseded for current validation.
- The initial full EditMode result with two stale expectation failures remains
  preserved in `phase4c4-full-editmode-initial-failed-job.json`. The test-only
  compatibility update adjusted legacy objective-list and optional-template
  assertions; it did not change production behavior.

## F. Runtime evidence and limits

- The 4C.1 runtime path demonstrated preference capture, detached provider
  memory, pending preview without execution, explicit approval, and created-plan
  / approved-memory behavior, plus reset, destruction, owner replacement, late
  responses, capacity eviction, strict routing, and pending-preserving queries.
- 4C.3 runtime coverage is included in its full PlayMode regression.
- 4C.4 PlayMode drove actual whole-form chat through the real approval UI and
  existing planner/goal/simulation ticks. Ranged completed with an
  ArcheryRange and ten Archers; Defensive Turtle completed with Barracks,
  ArcheryRange, two Towers, eight Spearmen, and eight Archers. Coverage also
  includes one-worker allocation, below-age rejection, replay/direct-player
  protections, funded Tower foundations, and resource depletion followed by
  gathering and completion. Reservations were asserted released.
- **Dynamic housing runtime completion is proven in PlayMode.** The new
  10/10-population fixture has no prebuilt House; real chat approval produces
  a plan, a House foundation and completed House, subsequent Archer training,
  ten Archers, completed plan, and released reservations. The nine older
  PlayMode cases explicitly retain their six-House fixture.

### Final scoped Antigravity population-cap round

- Root cause: `StrategicPlanner.Feasibility.cs` rejected current-cap objectives
  before its existing future-capacity/House calculation. Only that premature
  two-line rejection was removed. Maximum-population and deterministic worker,
  age, resource, construction, and unsupported-objective checks remain.
- Test-first RED job `03e03bbe10b047288e3c743ae848668d` reproduced both
  current-cap failures with “No population capacity is available.” After the
  fix, EditMode cases cover 700-wood Ranged feasibility (including one canonical
  House), funded-House no-double-charge at 650 wood, zero-worker rejection, and
  the existing explicit maximum-population and unavailable-age cases.
- Focused EditMode job `ce5c2db6cb3149edbe05d1d5d13f7a83`: **31/31**;
  `phase4c4-housing-focused-editmode.xml` SHA-256
  `39A3E8E7E865FBDEB7147B0C3BC322B68516260CF73B5FAA4BE335497345055C`.
  Focused PlayMode: **10/10**; `phase4c4-housing-focused-playmode.xml`
  SHA-256 `7B8609A14AE36D8BB8823408B99951F8DFB0996F3C9114F7891CEA705A757178`.
  Separate housing PlayMode job `21fc37ac99be449f9dd78bf263652ee2`:
  **1/1**, XML SHA-256
  `CBB630538842838139F5D8C5C7F43236FDA0E10B41A7401BD8FE6A963B0DBFB0`.
- Fresh complete EditMode job `024a2c805b33467b947965ae57cc309c`:
  **571/571**; `phase4c4-housing-full-editmode.xml` SHA-256
  `771A0EFB388EB21B0B478817E50DD58E70A4BC27B915F88AEEC1EDA11A1B2B66`.
  Fresh complete PlayMode job `4fe992b52c814dc5a36e67fa3c854389`:
  **87/87**; `phase4c4-housing-full-playmode.xml` SHA-256
  `63ED603142539BE6ED6BAFE4BB6824B8C34C0EA9192EEF8891D678FB0D466B28`.
  Both full XMLs contain unique test names and zero failed, skipped, or
  inconclusive cases. A focused PlayMode MCP job callback timed out after Unity
  wrote the successful 10/10 XML; the later complete PlayMode job reported
  succeeded through MCP.
- Updated `phase4c4-final-source-hashes.json` matches 27/27 current files;
  its SHA-256 is
  `76D571C3FB9FED87206712D5FD559FE162F1E034754CA2E3B8A765174A4E245F`.
  The changed feasibility source SHA-256 is
  `F736FDC674EB89C761FBDCA7B7CBB41A5FA5228E519A94E2592C1A89778EA14A`.
  `phase4c4-housing-final-boundary-audit.json` SHA-256
  `A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122`
  reports changed21/new21/missing0, protected55/55 unchanged, gaps0, six
  documented host references, and zero credential-shape/assignment matches.
  Approval, provider, policy, planner authority, tactical execution, commands,
  networking, packages, and authentication were not changed. Independent
  final-round review found no Critical or Important defect.
- The two reported scene/meta pairs were verified as byte-identical,
  unreferenced TestRunner-only/recovery artifacts outside Build Settings and
  removed. Git commit `205c4fa` retains recoverable copies.
- Limitation outside this narrowly authorized fix: the feasibility quote
  includes 50 wood for a dynamic House, but the strategic plan's static
  milestone budget/reservation still omits that opportunistic House. The
  fully funded integration proof is unaffected; no planner-budget or authority
  redesign was made.

## G. Supported scope and deferred work

Supported 4C.4 objectives:

- `RangedReinforcement`: eight food/eight wood worker allocation; one
  ArcheryRange; ten Archers; then ready.
- `DefensiveTurtle`: eight food/eight wood allocation; Barracks and
  ArcheryRange; two mandatory Towers; eight Spearmen and eight Archers; then
  ready. This is finite fortified-force preparation, not territory holding or
  perimeter automation.

Still deferred and unsupported:

- `TechnologyRush`: no Commander research/age-progression goal.
- `SiegePreparation`: no admitted SiegeWorkshop or siege-unit support.
- `NavalExpansion`: no required naval structure/unit execution support.
- Walls, keeps, garrisons, upgrades, perimeter placement, attack micro, and
  silent optional Towers are not promised. Genuine income trends also remain
  future work because authoritative detached income history is unavailable.

Final verdict: **READY FOR PHASE 4D.** The scoped population-cap fix passed
focused EditMode 31/31, focused PlayMode 10/10, full EditMode 571/571, and full
PlayMode 87/87. Dynamic housing is proven through UI approval; source hashes,
protected-boundary audit, and independent review passed. The three unsupported
objectives remain explicitly deferred.
