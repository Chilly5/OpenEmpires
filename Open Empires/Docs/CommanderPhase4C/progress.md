# SDD ledger — plan: Docs/superpowers/plans/2026-09-15-commander-phase4c1.md

## Objective

Full Phase4C brief: attachment `691a2ccc-7545-452f-934d-6f5278f532ce/pasted-text-1.txt`. Overall goal remains all four controlled sub-phases and final A-G report. Phase4C.1 worker dispatched; do not duplicate its work or Unity runner.

## Process decisions

- Ruling: follow the user's continuous-execution instruction instead of routine skill approval pauses — architecture is documented before coding — if a design choice is wrong, local scoped rework may be needed.
- Ruling: preserve existing checkout, branch, and inherited work without commits — current Unity editor and prior Commander source are in this checkout — review requires explicit before/after snapshots rather than HEAD.
- Ruling: keep phase-specific ledger/brief/report artifacts under project Docs instead of the skill's repo-root scratch directory — repo-root is outside writable workspace and evidence must survive goal continuations — costs extra retained documentation, not gameplay behavior.
- Ruling: initial richer context uses observed activity/queue/composition/progress, not stock deltas labelled income — no verified authoritative income history yet — true income trends may remain a documented optional future improvement.

## Delegation

| Agent | Task | Expected output | Validation/status |
| --- | --- | --- | --- |
| phase4c_discovery (Luna) | Read-only integration mapping, then requirements matrix | Source methods, lifecycle/test pointers; full brief traceability | Complete; requirements-matrix.md includes exact named tests, orchestration, per-phase gates, A-G report and final outcome |
| phase4c_discovery (Luna, follow-up) | Reusable read-only boundary audit script | check-boundaries.ps1, usage guide, safe validation | Initial implementation reviewed; fix round1 requested for file-scope/filter errors, incomplete frozen inventory, Git error handling, exact-symbol matching, credential candidate labelling and baseline schema |
| phase4c_objective_feasibility (Sol) | Candidate objective support | Canonical execution compatibility assessment | Complete; existing Commander cannot execute research, siege or naval goals; towers/ranged units supported |
| audit_boundaries (Luna) | Pre-phase baseline | Sorted production/test hashes and prior evidence verification | Complete: 246 source/test files, 0 prior audit hash mismatches; prior 503/52 results verified, not fresh runs |
| phase4c1_implementation (Sol high) | 4C.1 Task1 | Integrated bounded memory, UI/provider/reset integration, RED/GREEN and full baseline/regression evidence | Running; sole production writer and Unity runner owner; report phase4c1-task1-report.md |

## Discovery conclusions reviewed by Astra

- Current provider serializer is explicit; snapshot additions must be included deliberately in request JSON.
- Current UI transcript survives Initialize; bridge ClearPending does not clear history; both need explicit reset handling.
- Pipeline EvaluationCompleted is the existing event hook. Record only copied primitive summaries; CreatedPlan, not policy selection, proves a strategy actually began.
- Current CommanderIntentCatalog supports only villagers/spearmen/archers/knights and House/Barracks/ArcheryRange/Stables/Tower/TownCenter. Existing plans use only resource allocation, structure construction, and ensure-unit-count goals.
- Ruling: Phase4C.4 will add RangedReinforcement (ten archers and required ArcheryRange) plus DefensiveTurtle (mandatory towers and mixed ranged/spear defense), using existing executor primitives — TechnologyRush/SiegePreparation/NavalExpansion lack current Commander execution support — names and quantities need explicit UI descriptions; this does not promise walls, garrisons, upgrades, perimeter placement, or naval/research behavior.
- Objective feasibility must reject unknown enum values explicitly; a missing switch arm must never produce a zero-cost approved objective.

## Current gates

Baseline EditMode job: `f858ad6eaf01466f9bf7544c4af70fb3`, worker reported 503/503 passed, 0 failed/skipped, 114.964 seconds. Transient MCP disappearance recovered using the same handle. Baseline PlayMode job: `6a30f5a8c7c94ada8d1dcc495d5142de`, worker reported 52 test bodies passed but suite FAILED due to an external Package Manager authentication error during `CommanderPhase3BPlayModeTests.CancelledState_DisposeAndLateProviderResponse`. This is not a clean baseline and predates Phase4C production changes. Detailed artifacts and final clean regression remain required.

Ruling: continue scoped TDD after the identified pre-existing Package Manager authentication-log failure — do not modify credentials, package settings, Commander assertions, or add log suppression — final full clean PlayMode is still required; if environmental noise persists, runtime proof remains incomplete rather than being waived.

Task1 before-content snapshot directory: `Docs/CommanderPhase4C/phase4c1-task1-before/Assets/...`, worker reports seven original integration files captured and will add any further file before editing it. These snapshots, not HEAD, are review bases.

Task1 checkpoint after worker usage interruption: tests and minimal Phase4C memory API stubs exist, but CommanderMemory methods remain no-op and ConversationState preference resolution always false. They are intentional RED-stage skeletons, NOT completed functionality. Baseline compact artifacts are `phase4c1-task1-baseline-editmode.json` and `phase4c1-task1-baseline-playmode.json`. The worker stopped with a usage-limit error; root subsequently verified current account usage showed available capacity and resumed the SAME `phase4c1_implementation` agent. Do not spawn a duplicate implementer or rerun the completed baseline solely because of this interruption. Current per-test RED/GREEN evidence is still missing and required.

Architecture follow-ons drafted only: `2026-09-15-commander-phase4c2-design.md` and `2026-09-15-commander-phase4c3-design.md`. No later-phase implementation has begun. Their detailed implementation plans must consume the final 4C.1 interfaces after its gate passes.

Task1 RED checkpoint: worker reports focused EditMode job `3f6bb2794532405ba23a7acca593c049`, eight tests run, six expected assertion failures for missing memory behavior and two stub-compatible passes. Earlier zero discovery was a test-only Newtonsoft reference compile error, fixed before this RED run; fresh test assembly and clean compiler verified by worker. Implementation is now in progress. Root independently observed editor readiness recover to idle/ready with no compilation/reload blockers. Full detailed RED/GREEN evidence remains part of the final task report contract.

Task1 implementation checkpoint: worker reports focused EditMode `b6b47ccfe8da42cdb401c1c2bfb22f98` 9/9 and PlayMode `e1c2aad328cf45ffb949c247c113fc46` 11/11 passed; intermediate PlayMode RED exposed duplicated current turn and stale strategic reinitialize publication, fixed. Source snapshot for review is in `phase4c1-review-package.md` and twelve actual embedded diff/full-source files in `embedded/`. Independent reviewer `phase4c1_review` (Sol high) is reviewing that checkpoint; do not overwrite it during fixes.

Root integration findings sent to original implementer (one fixwave):
1. Approval/policy rejection incorrectly labelled TransitionRefused because allowed=false also represents no selection; classification must examine actual decision status first.
2. Replacing strategic pipeline retains old session/owner data; reset on runtime change and enforce tactical/strategic owner consistency.
3. Tactical adapter ResetHistory only clears immediately; late provider response/cancellation handler can refill history and dispatch despite UI generation guard. Adapter-level generation/cancellation protection must stop stale history writes and execution.
4. At capacity, recording the current player turn before interpretation evicts a previously retained preference. Capture the prior bounded snapshot at request start, before appending the turn.

Ruling: interpretation uses the bounded memory retained at request start; ordinary live-memory eviction still applies afterward, with no hidden preference cache or restoration for later requests — otherwise current-turn insertion changes its own interpretation — a different desired timing would require a small ordering change and regression update.

2026-09-16 resume: original implementation agent restored through followup_task; same fix round remains active. Phase4C_evidence (Luna) assigned routine artifact inventory and checkpoint boundary audit, with no Unity runner ownership until the focused fixes freeze. Full regression will be delegated to Luna; Astra retains integration and security review. No completed baseline is repeated.

Interim full EditMode job `d026e6f4ecfa4ad397d0253a0cda0077` was lost during reload. Root called get_test_job: Unknown job_id, and read editor/state: idle/ready, tests not running. This is authoritative terminal/missing-handle evidence, NOT a passing regression. Original implementer was resumed (pending_init in agent list) to finish focused fixes and run a true final full pair, without rerunning obsolete checkpoint or pre-edit baselines.

Audit checker review: two correction rounds addressed wrong file roots/filter, incomplete frozen set, exact symbol matching, AQ-dot detector and Git error handling. Root safely ran it: changed7,new5,missing0,frozen55 all unchanged,credentialcounts0,env untracked+ignored; candidates confined to existing game-owned chat host. This is source-only checkpoint evidence, not a phase gate. Luna created `phase4c1-audit.json`; final detailed audit must be refreshed after fixwave. New architecture-only 4C.4 spec also written; later-phase implementation still gated.

- [x] User brief and requested Unity skill read.
- [x] Unity MCP live idle/ready confirmed; Unity 6000.5.9f1, instance Open Empires@6d7310c7.
- [x] Architecture, delegation, dependency order, risks recorded before coding.
- [x] 4C.1 plan preflight table and self-review recorded in plan.
- [x] Phase4C source baseline saved (246 files; HEAD c399658020914336154cdd4d27d64cad00b76db0).
- [x] Fresh full baseline tests run; EditMode clean, PlayMode external-log suite failure explicitly recorded above.
- [x] 4C.1 implementation and focused tests.
- [x] 4C.1 full regressions/static audit/review/runtime gate.
- [ ] 4C.2 design, implementation and all gates.
- [ ] 4C.3 design, implementation and all gates.
- [ ] 4C.4 selected supported objectives, implementation and all gates.
- [ ] Final current-state requirements audit and Docs/CommanderPhase4C.md A-G report.

Fix round 1 focused checkpoint: RED 1ad433fe194e4ed888d22df26c63ee4e (16 total, 10 passed, 6 intended failures) recovered from actual Unity XML after stale progress; a subsequent overlapping initialization failed and is not evidence. GREEN Edit c7ac8508e6cb4c8a829b1df293ffec6b (9/9), Play 6c29c14fb2ba4312a56720c2ddc1cbe4 (16/16), detailed artifacts and Task1 report now exist. Source frozen; phase4c_evidence (Luna) owns scoped fix-package generation and sole full regression runner. Independent scoped review and final gate remain pending.

Task 1: fix round 1/5 reviewed (4 addressed, 2 open). Full EditMode b13a3f11f32742a399644ec7e6bbcc88 failed 509/512: original four-argument bridge constructor no longer exists for reflection callers. Full PlayMode d75f39e8c7eb442295919098de7d9fec passed 68/68. Reviewer also confirmed new whitespace generic-attack guard bypass after removal of the UI normalized guard. Original four findings are addressed; phase remains ungated.

Task 1: fix round 2/5 dispatched to original Sol implementer, sole focused Unity runner. Restore original four-argument constructor and unique two-argument TranslateAsync; explicit memory uses a distinct method/core so reflection remains unambiguous. Normalize bridge generic-attack whitespace and test with a non-mock valid-result provider. Do not weaken existing Phase4B2 tests. Luna full rerun and scoped independent review follow the next freeze.

4C.2 gated implementation plan prepared at `Docs/superpowers/plans/2026-09-16-commander-phase4c2.md`; consumes unchanged memory interfaces and existing pipeline projections, not the bridge methods under repair. Self-review covers outcome provenance, query neutrality, reset and full gates. No 4C.2 implementation admitted yet. The previous continuation made concrete progress through four fixes, detailed test evidence, full-suite results, and two independently confirmed regression findings; no external blocker is currently established.

Task 1: fix round 2/5 reviewed (2 addressed, 0 open). Sol reviewer verified original 4-argument bridge constructor and unique 2-argument TranslateAsync plus separately named memory path; normalized whitespace guard with non-mock provider regression. No new code breakage; all four original findings remain addressed. Scoped spec and quality PASS. Focused Edit 79591d15af904ad1bad9d2115ededcd8: 78/78 (4B2+4C1); Play 06b0dd1130c64e84afb41dce30f4a35e: 20/20. Source frozen, Luna owns fresh full pair/audit; final 4C.1 gate still pending.

## Phase4C.1 gate passed, 2026-09-16

Task 1: complete (baseline c399658, working-tree changes only; review clean after two fix rounds). Task 2: complete (regression/static/runtime gate verified). No commits created.

- Full EditMode `2c350469769c460ea10838ddaacc0737`: 512/512 passed, zero failures/skips; full PlayMode `14031c0c376d450ca2a6f845f69ab555`: 69/69 passed, zero failures/skips. Root independently parsed complete round2 XML and verified all four required named EditMode tests and all 17 Phase4C1 PlayMode cases passed.
- Runtime evidence includes actual chat preference -> captured detached provider memory -> pending preview with no execution -> actual Approve UI control -> created plan/approved memory. Reset, destruction, owner replacement, deferred tactical/strategic responses, capacity eviction, strict whitespace routing, and pending-preserving queries passed.
- Independent Sol review: all six findings addressed, spec/quality PASS; no new code breakage.
- Final audit `phase4c1-fixround2-final-boundary-audit.json`: 55 protected source files unchanged, no missing source, zero credential-shape/assignment matches, env untracked and ignored, six existing game-owned UI host candidates only. Root compared all 12 changed/new source/test hashes to the actual files: zero mismatches.
- Complete versioned artifacts: `phase4c1-fixround2-full-editmode-TestResults.xml`, `phase4c1-fixround2-full-playmode-TestResults.xml`, `phase4c1-fixround2-final-regression-summary.md`, scoped fixround2 review package and task report. Earlier failed evidence remains preserved.

4C.2 Task1 is now admitted under `Docs/superpowers/plans/2026-09-16-commander-phase4c2.md`. Agent: fresh Sol implementation worker. Expected output: immutable explanation values/service, host-only provenance/current-progress projection, neutral chat queries, focused RED/GREEN and report. Validation: independent review, Luna full regression/static audit, Astra integration gate. No Phase4C.3/4 implementation is admitted yet; full goal remains active.

4C.2 worker identity: `phase4c2_implementation` (Sol high), resumed through followup_task after automatic continuation eviction. Incremental report and exact chat before-snapshot now exist. Ten focused EditMode tests/API skeletons added; initial zero-discovery job 83812a82d58841828e15cfb36411a70a is not test evidence. Assets imported and compilation clean; assertion RED job 8f973da9f3794c9ea14e2f55a54f2856 active at worker checkpoint. No duplicate runner.

4C.2 RED checkpoint: actual Unity XML for 8f973da9f3794c9ea14e2f55a54f2856 recovered despite initialization observation timeout, with 15 cases (1 passed, 14 expected assertion failures for missing reason/bounds/validation/rendering). Full artifact `phase4c2-task1-red-editmode-TestResults.xml`; report updated by sole implementation worker. This is test-first progress, not implemented/green functionality. Continue same worker; do not rerun RED merely because a handle was lost.

4C.2 value/service GREEN: 7a12c14450234bc88917de558b5cbbd0, 15/15, full XML saved by worker. Five real CommanderChatUI PlayMode host tests in RED job 50f477b64b874d8dabe9155a6e4f3152 at next checkpoint; compiler clean. Host integration/final focused+full/review gate remain pending.

Architecture preparation: 4C.3 gated plan `Docs/superpowers/plans/2026-09-16-commander-phase4c3.md` and verified projection map in its spec; no implementation admitted. Luna created honest in-progress A-G report `Docs/CommanderPhase4C.md` and read-only future objective path map `phase4c4-integration-map.md`. Final READY verdict remains pending full4C2/3/4 gates.

4C.4 gated implementation plan also prepared at `Docs/superpowers/plans/2026-09-16-commander-phase4c4.md`, with exact supported quantities/milestones, three planner integration points, strict parser/approval rules, one-worker runtime proof and whole-goal final audit. Plans3/4 self-reviewed for type/method consistency and placeholders; no later-phase implementation admitted.

Ruling: new 4C.4 plan worker fitting may use a zero allocation target only when workers are fewer than allocation buckets, preserving the existing deterministic shrink order and all old plan behavior — current allocation requests/goals admit zero while the old fitter cannot fit two positive targets onto one worker — if runtime proves a zero-target interaction unsuitable, revise only the new plan allocation sequencing rather than lowering the promised objective targets. Require resource-funded one-worker execution proof. Also verified three concrete-plan admission points (canonical budget, worker fit, prepared-economy/recovery branch) for later integration; no 4C.4 code changed.
