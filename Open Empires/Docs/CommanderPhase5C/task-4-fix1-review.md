### Finding Verdicts

- **Result-bound Garrison could select unrelated villagers** — ADDRESSED. The common human-override/dependency/runtime checks now precede shelter planning (`Assets/Scripts/AI/Commander/CommanderPlanner.cs:78–94`); admission restricts Garrison result links to equal-count explicit new Villager production (`Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs:179–188`); the planner copies and validates the producer's exact `ResultUnitIds` rather than reselecting owned villagers (`Assets/Scripts/AI/Commander/CommanderPlanner.Garrison.cs:38–69`). The focused exact-ID case is in `Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs:467`.
- **Expired exact-Garrison marker could become ordinary fallback** — ADDRESSED. Expired pending records remain bounded tombstones (`Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:43–52`); identical unattributed payloads are suppressed, while only a separately proven local Human origin is exempt (`Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:72–93`). An expired original is rejected before native dispatch (`Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:103–113`). Direct, reconstructed, and uniquely verified relay delays plus proven-Human replacement are covered at `Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs:389–463`. This is local single-player affinity, not a multiplayer/replay claim.
- **Greedy repeated-build preflight could reject a legal two-site plan** — ADDRESSED. The preflight now backtracks over mutually compatible legal sites with an explicit 8192-node limit, and the resulting ordered set drives the actual native placements (`Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs:8,43–58,82–129`). The native counterexample at `Assets/Tests/EditMode/CommanderPhase5ADynamicRuntimeTests.cs:333` completed two distinct attributed Houses after bypassing the conflicting central candidate.

### New Breakage in the Fix Diff

- None found at Critical, Important, or Minor severity. The bounded tombstone capacity can refuse later exact orders when no safe retirement has been proved; this is the explicit fail-closed disposition, not a silent ordinary fallback (`Assets/Scripts/Core/GameSimulation.ExactGarrison.cs:49–51`). The site-search budget can honestly refuse a difficult plan without starting a prefix (`Assets/Scripts/AI/Commander/Phase5A/CommanderPlanner.DynamicPlacement.cs:45–53,105–108`).

### Out-of-Scope Observations

- None.

### Verdict

**Spec compliance:** Scoped fix requirements addressed. **Task quality:** Fix round approved. **Fix round:** All findings addressed, no new Critical/Important breakage.

Saved terminal evidence was inspected, not rerun: focused `5699544e823f49b69f9380b6211f37fb` passed 6/6 and final affected `3a5900d59c934846be3441fde8357267` passed 4/4; these are not an aggregate. The frozen eight-file package SHA-256 matched `0335B92F0C64C97AFCB2D9DC4A353107A4B9910380E06EBC23DD9DFCFEECE6E6` and was read through EOF. No tests, builds, MCP, network, or paid calls were run for this review.

Provenance check: the `CommanderSemanticGraphAdmission.cs` pre-fix snapshot was reconstructed after its omission from clean HEAD, not preserved before edit. The package discloses this; the parent independently verified the clean blob and eight-line addition. This limitation is not represented as a pre-touch snapshot.
