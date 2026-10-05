# Known limitations

This is the bounded Phase 4G implementation handoff. It is ready for AntiGravity audit, not a claim that every project test has been rerun.

- Full EditMode/PlayMode regression, standalone build, and hostile provider verification were intentionally not run in this pass.
- The required PlayMode result scenario passed for three produced Spearmen patrolling a worked Gold node. AntiGravity should still exercise the analogous Scout and exact-produced-structure/rally paths.
- Existing Phase 4E graph limits remain four nodes, eight total dependency references and depth four. Conditional, looping, persistent production policies and general cancellation graphs are not implemented.
- No generic formation, escort, garrison/ungarrison, live-state explanation, counter-answer, or broad natural-language Q&A surface was added.
- `VisibleEnemy` searches only currently visible state; it never searches unexplored or merely historical objects.
- Result binding is intentionally fail-closed. Research results are not a producer kind and cannot be referenced by a later node in this slice.
- Patrol is a persistent objective and remains `Executing` while active; movement completion is simulation-observed, not inferred from command enqueue alone.
- Capability discovery remains separate from execution support. New catalog content does not automatically become an executable action.
