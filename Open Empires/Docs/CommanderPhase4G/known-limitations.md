# Known limitations

## Targeted economy/clarification repair boundary, 2026-10-06

- Generic AllocateWorkers now represents count/mode, Idle/Gathering/current resource and explicit resource source; it is not the old coarse desired-total allocation. Earlier support wording did not establish these semantics.
- The source projection uses current canonical Food/Farm/carcass registrations, not a universal future food-content taxonomy. Future new resource subtypes require authoritative classification updates.
- Exact requests preflight the full selected set, block initial shortages, freeze selected workers and do not substitute after human takeover. AllMatching is a one-time snapshot; TargetTotal does not remove surplus or run indefinitely.
- Candidate path checks are bounded per selected worker (existing 3..5 setting); a legal target outside the bounded candidate search may yield an explicit blocker. Large-world performance and unusual generated terrain deserve independent stress testing.
- Typed clarification supports missing Count/Destination, at most three replies. Local count words cover zero through twenty or whole decimal integers (validated 1..200), optionally villager(s). Other forms use the same provider's bounded continuation. Quantity-policy changes require a new request; explicitly named worker/destination corrections are guarded.
- Source restrictions persist through native queues/automatic redirect/depletion and clear on normal replacement. Restricted packets require updated peers; do not run mixed-version multiplayer assuming enforcement by older clients.
- Safe live Luna evidence proves extraction/chat admission/normal assignment targets, not harvesting deposits, standalone UI or a two-editor network session. Controlled PlayMode/full regression and real-provider evidence are separate in `../CommanderFix/focused-test-evidence.md`.
- This targeted fix does not perform an exhaustive hostile audit, a standalone build, manual Computer Use, Phase4I or additional Whisper debugging. Independent finalization audit still owns those applicable release gates.

This is the bounded Phase 4G implementation handoff. It is ready for AntiGravity audit, not a claim that every project test has been rerun.

## Current refresh boundary, 2026-10-06

- Prior AntiGravity acceptance and Phase 4H records are preserved as history. They do not certify this changed source snapshot. This pass does not rerun that audit or advance another phase.
- The single PlayMode scenario uses a deterministic semantic fixture and a controlled simulation, with resources/terrain established by test setup. It exercises normal training and patrol command execution, not a live paid-provider response, native UI run or standalone executable.
- Full Scout production-to-scouting and construction-to-rally gameplay scenarios remain for AntiGravity. The focused binding case checks Scout parsing/admission and direct exact-structure command resolution; it does not claim end-to-end proof for those two scenarios.
- Existing result capture uses post-submission baseline exclusion plus stable ordering, not queue-item provenance tokens. Concurrent independent production/construction of the same type, pre-existing queued units finishing after admission, and overlapping producer graphs require priority hostile testing. Do not infer causal attribution to a particular training order from baseline exclusion alone.
- Result-specific override is conservative before capture and sticky afterwards; an invalidated consumer can eventually fail under the existing blocked timeout. There is no automatic reclaim or fallback.
- The nearby provider/UI/voice working-tree changes present at the start were preserved, not expanded or exhaustively verified by this Phase 4G pass. In particular, the updated provider Scout capability expectation is not a fresh provider-suite pass.

- Full EditMode/PlayMode regression, standalone build, and hostile provider verification were intentionally not run in this pass.
- The required PlayMode result scenario passed for three produced Spearmen patrolling a worked Gold node. AntiGravity should still exercise the analogous Scout and exact-produced-structure/rally paths.
- Existing Phase 4E graph limits remain four nodes, eight total dependency references and depth four. Conditional, looping, persistent production policies and general cancellation graphs are not implemented.
- No generic formation, escort, garrison/ungarrison, live-state explanation, counter-answer, or broad natural-language Q&A surface was added.
- `VisibleEnemy` searches only currently visible state; it never searches unexplored or merely historical objects.
- Result binding is intentionally fail-closed. Research results are not a producer kind and cannot be referenced by a later node in this slice.
- Patrol is a persistent objective and remains `Executing` while active; movement completion is simulation-observed, not inferred from command enqueue alone.
- Capability discovery remains separate from execution support. New catalog content does not automatically become an executable action.
