# Commander Phase 4C — A–G report

Status: **READY FOR PHASE 4D.** Phases 4C.1–4C.4 passed their focused, review,
full-regression, static-boundary, runtime, and requirements gates. The final
independent review found no remaining Critical or Important issue. Dynamic
housing completion remains an explicitly documented, non-blocking runtime
coverage limitation; `TechnologyRush`, `SiegePreparation`, and `NavalExpansion`
remain deferred and unsupported.

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
  found no new Critical or Important issues; dynamic housing is explicitly a
  remaining coverage limitation.
- **4C.4 final post-fix full regression:** Full EditMode job
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
- **Dynamic housing runtime completion is unproven.** The focused runtime
  fixture prebuilds six Houses; no claim is made that runtime constructed
  housing was demonstrated. No Phase 4D readiness claim follows from the full
  regressions alone.

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

Final verdict: **READY FOR PHASE 4D.** The direct-player priority fix passed its
focused GREEN test; fresh full EditMode 568/568 and PlayMode 86/86 passed; final
source hashes and protected-boundary audit passed; and independent review found
no remaining Critical or Important finding. Dynamic housing runtime coverage
remains unproven but is a documented non-blocking limitation; the three
unsupported objectives remain explicitly deferred.
