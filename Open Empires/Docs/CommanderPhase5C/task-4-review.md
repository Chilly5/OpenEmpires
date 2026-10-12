# Task 4 review

### Spec Compliance

- ❌ Issues found. The four requested adapter families are present, but result-bound Garrison can select unrelated villagers (`Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs:12–36`) and an exact Garrison command can become ordinary fallback after its pending guard expires (`Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:44–51,117`). The repeated-build preflight can reject a legal two-site plan (`Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs:71–92`). These violate exact identity/target and valid repeated-placement behavior.
- ⚠️ Cannot verify from this diff: actual endpoint input-token usage (the reported 3716 is a conservative estimate, not returned usage); populated UI/task-card truth; travel-driven FogOfWar revelation during discovery; the later final offline pass and Windows/Web build. These are later integrated gates, not evidence that Task 4 omitted its scoped code. No paid, build, UI, or native-match run was performed for this review.

### Strengths

- The composed location keeps owned TC, map-west relation, visible Wood/Tree source, and frozen ALL current-Wood workers separate; explicit Move uses native `MoveCommand`, while Gather keeps the native allocation route (`Assets/Scripts/AI/Commander/Phase5A/CommanderBoundLocation.cs:90–149`; `Assets/Scripts/AI/Commander/CommanderPlanner.DynamicMove.cs:8–27`).
- Two Houses use existing native placement/construction and exact result attribution; the focused fixture asserts two completed, distinct owned Houses (`Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs:290`). Garrison reports actual housed/protected/unaccommodated counts and building-wide egress (`Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs:93–164`).
- Discovery uses bounded known-frontier waypoints rather than hidden target coordinates, and completion checks a visible requested hostile type (`Assets/Scripts/AI/Commander/CommanderPlanner.Discovery.cs:13–18,99–175`). Saved raw results confirm the reported representative 14/14 and final affected 2/2 EditMode gates; no aggregate-suite claim is inferred.

### Issues

#### Critical (Must Fix)

- None identified.

#### Important (Should Fix)

- `Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs:12–36`: `PlanShelterCapability` runs before the common `PlanCapability` result-binding/runtime-affinity checks and freezes villagers from all owned units. A Garrison node with `resultFromNode` is accepted for a produced-villager source, yet the planner ignores its exact result IDs and may garrison different villagers. Either reject that result link in parser/admission or honor the producer result with the same identity, dependency, runtime, and human-override checks. Focused outside-diff check: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs:156–194` permits this villager link; the existing goal wiring sets `RequiresResultBinding`/`ResultSourceGoal`.
- `Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:44–51,58–76,117`: pending exact markers expire after 256 simulation ticks, but a delayed original or reconstructed Garrison command is then processed with `exact == null` as an ordinary command. If the requested shelter is full/unavailable, native fallback may use another shelter despite the approved exact target. Expiration must also make that command fail closed, or prove and enforce that a pending command can never execute after expiration.
- `Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs:71–92`: `HasFullDynamicSiteSet` greedily commits the first candidate and never backtracks. A central legal House site can overlap two separated legal sites; when other sites are blocked, it reports no full set although the two separated sites satisfy the request. Use a bounded exact two-site search (or bounded N-site search) before refusing the plan. Focused outside-diff check: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticPlacementCandidates.cs:54–96` generates offset Near candidates and ranks central/exact positions first, so the conflicting ordering is possible.

#### Minor (Nice to Have)

- None identified.

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** The adapters and focused native-simulation evidence are substantive, but the three Important cases can reject valid construction or break exact-result/exact-shelter promises. Review used the frozen incremental package (SHA-256 `BE5EC05DE157550E5CBBE8A550F6BFC27338C14FC50876497D0D4E40094EFC53`) through EOF and saved raw results only; no tests were rerun.
