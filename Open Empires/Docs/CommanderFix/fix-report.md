# Targeted economy and clarification repair — handoff

Date: 2026-10-06. This is a post-4G/4H fix, not Phase4I. Verdict: **READY FOR FINALIZATION AUDIT**. No release/hostile/standalone acceptance is implied. Finalization audit must use the manifest's integrated source, not historical HEAD-only assumptions.

## Implemented

- Generic typed AllocateWorkers path with worker state/current-resource selectors and Food/Wood/Gold/Stone destinations, optional canonical source kind. No phrase-specific game action or provider-selected entities/coordinates.
- Correct quantity semantics: ordinary counted orders select/assign exactly N; “more” selects N additional eligible workers; explicit “ensure/keep” requests use at-least TargetTotal; all-idle requests use one bounded snapshot. Legacy strategic allocation and production-prerequisite policies remain separate.
- Deterministic full-request worker/target preflight, legal visible reachable sources, capacity-aware distribution, atomic full-set reservations before initial commands, one ordinary command per planning tick and actual selected-assignment completion.
- Sticky manual takeover: no reclaim or substitute after a human takes a selected worker, including after the normal protection lease expires. Exact initial shortage issues no partial batch.
- Canonical source projection without gameplay-data migration/duplicate costs. Existing owned sheep slaughter and carcass gather machinery reused. A real native Sheep-to-Berries depletion reproduction activated the approved conditional persistent source restriction and restricted version1 packet path; unrestricted binary legacy bytes remain unchanged.
- Detached typed missing Count/Destination draft, separate bounded provider continuation, local whole-count replies, cancellation/new-command escape, three-reply bound and runtime/reset/disposal/late-response guards. Explicit Idle/current-resource and destination corrections are guarded; unsolicited criteria loss rejects.
- Provider schema/capability/aggregate worker context exposure, safe live-Luna verification and unchanged voice text route.
- Two review defects fixed through behavioral red/green tests: automatic gather redirect lost the source restriction, and explicit Idle correction was rejected. A full-suite existing strategic busy-guard failure was then reproduced/fixed narrowly to preserve pause/cancel during held translation.
- Existing contextual Mill intent now forwards PlacementResourceType in the direct resolver; this is argument preservation, not new placement behavior.

## Evidence boundary

| Evidence | Result |
| --- | --- |
| Final review-focused EditMode | 15/15 passed |
| Relevant existing Commander suites | 204/204 passed |
| Single full EditMode, before final busy-guard repair | 998/998 passed, zero skips |
| Single full PlayMode, before final busy-guard repair | 193/194 passed; one existing lifecycle test failed |
| Post-repair affected PlayMode | 16/16 passed: lifecycle host class, three economy scenarios, exact produced-Spearmen patrol, shared voice |
| Post-repair affected EditMode | 38/38 passed: all 15 economy methods plus semantic chat class |
| Live OpenRouter/Luna | Five HTTP200/finish-stop requests, no retries; correct explicit count/Idle/source/Wood-to-Gold; gather-food → four completed locally without another provider request |
| Additional normal-command probes | AllMatching Idle/Berries completed; protected Wood transfer shortage blocked with zero commands/reservations |

The full PlayMode failure was `CommanderPhase4D1HostPlayModeTests.AppliedLifecycleMutation_InvalidatesInFlightInterpretation` (expected Paused, observed Active). Its root cause was the current chat host's premature busy guard. It passed after the narrow repair; the original failed full XML is preserved. Neither full suite was repeated, honoring the approved once-only policy. Earlier full results are not mislabeled as an exhaustive run of the final one-branch host change. See `focused-test-evidence.md` for job IDs, timing, XMLs and source boundaries.

The live probe proves real provider/chat admission and actual native assignment state, not harvesting deposits or a native UI match. The controlled PlayMode cases separately prove ordinary slaughter-to-carcass/source-depletion safety. Actual paid token/cost billing is unknown; no keys were copied to logs/docs.

## Repository and recovery

Active Unity root: D:/unity_projects/OpenEmpires/Open Empires. Branch: unit_models_and_voice_control. Original baseline: b96cf9b622f796df5c90f402b2bb16720c52f78e. Typed semantic checkpoint: 5fe6d4a. Runtime/test checkpoint: 097f01b.

Pre-existing dirty Phase4G/4H/provider/UI/package changes were preserved. Entirely new task-owned files and formerly clean changed files were committed; integrated hooks in previously dirty files were intentionally not blanket-staged. HEAD alone is not the tested source snapshot. Use `source-manifest.json`, `source-consistency.md` and the original dirty baseline record to recover the exact integrated checkout. Do not reset it to HEAD or overwrite the old Gemini credential to reproduce this fix.

## Decisions and costs

1. Use the live dirty editor checkout, not a new worktree. Cost: evidence can drift; root/instance/hashes must be checked.
2. Durable Windows/MCP ledger replaces Bash-only task scripts. Cost: manual bookkeeping; XML/count/hash consistency checks compensate.
3. AllocateWorkers uses the existing always-tactical intent base, not an unused intentLayer argument. Cost: strategic economy still needs its existing approval route.
4. Source resolver is a private partial-planner helper, not a separately stateful class. Cost: large selected sets can be expensive; bounded candidate/perimeter policy is documented for stress testing.
5. Pending quantity-policy/count rewrites are new requests; explicitly named worker/destination corrections are guarded. Cost: users may need cancel/restate for policy changes rather than silently rewriting a resolved count.

Fresh read-only GPT-6 Sol review assessed the implemented scope and found the two issues above. Its excluded live/runtime/regression/documentation judgments are owned by this root's separate evidence, not silently accepted on the reviewer's authority. No minor findings were deferred by that review.

## Remaining work for independent finalization audit

- Run independent exhaustive regression on the complete final dirty snapshot, especially the narrow post-full-run chat guard; do not reuse historical full XML as a final checkout certification.
- Verify standalone/native UI and real two-editor multiplayer/updated-peer compatibility where required for release. Mixed legacy receivers cannot enforce restricted packets; do not silently downgrade.
- Stress unusual generated terrain, large source populations, partial takeover timing, source disappearance between selection/execution, deposit/resume and concurrent source-capacity contention. Existing result-binding producer attribution/Scout/structure-rally priorities remain outside this economy fix.
- Extend canonical source classification only when future gameplay registrations introduce new food/resource subtypes. Do not turn catalog discovery into execution authority.
- Do not start Phase4I automatically. No Whisper troubleshooting, Computer Use or large standalone acceptance package was undertaken in this fix.

All required artifacts are in this folder: original root causes, schema/modes/source protocol, clarification transitions, test/live evidence, requirements matrix, execution ledger and final manifest/consistency records. Post-repair checks are 16/16 PlayMode and 38/38 EditMode; no full suite was repeated. The requirements matrix identifies proof by scope, and source-consistency records validate the final hash set.
