# Phase 4G implementation report

## Verdict

**READY FOR ANTIGRAVITY AUDIT** — the requested narrow implementation blockers are closed. AntiGravity owns exhaustive regression, standalone verification, hostile testing, and any small final fixes.

## Implemented in this slice

## Current narrow refresh — 2026-10-06

Fresh verdict: **READY FOR ANTIGRAVITY AUDIT**, not release acceptance. The previously recorded independent acceptance remains historical and was not repeated against this changed working tree.

Changes in this refresh:

- Simulation identity and production-path source-manager/goal lifecycle checks on typed result handles; read-only copies of captured IDs.
- Mandatory-result and same-manager dependency guards, with no silent selector fallback.
- Civilization-resolved unit matching for exact results and ordinary typed selectors.
- Explicit Scout production support in the existing intent/parser/provider capability adapters, using existing Stables rules and canonical gameplay data.
- Exact-building rally resolution without requiring unrelated units; omitted structure type derives from the bound building.
- Sticky result-specific human override, including after the normal protection timeout. General worker-authority behavior is unchanged.
- Strengthened the two existing focused result cases; fixed the incompatible-dependency fixture's `amount` typo to valid `count`, so rejection tests type compatibility rather than an unknown field.
- Strengthened the existing single PlayMode scenario by adding an owned pre-existing Spearman and proving exclusion.

Fresh evidence:

| Gate | Result | Job / detail |
|---|---|---|
| Seven original selected cases + two result cases | 9/9 passed, 0 skipped, 3.8637103 seconds | `d1766219a9b34da3b5ab637ec73bc976` |
| One controlled produce-three/patrol PlayMode scenario | 1/1 passed, 0 skipped, 0.7476932 seconds | `974e325de24b421f8d35af8f393887eb` |
| Actual training / binding | Barracks #1 trained new IDs 7,8,9; patrol used exactly those IDs; existing ID 6 excluded | Tick 901, producer total 4, baseline total 1 |
| Compilation | Unity 6000.5.9f1; final import/reload completed; error console empty | Current editor inspection, no standalone build |

The successful binding case also checks foreign-simulation ID collision rejection, HRE unit substitution, Scout parsing/admission, exact direct structure/rally resolution with no living military, and manual takeover retention at tick 1050. These are focused fixtures, not full Scout/building gameplay acceptance.

Small diagnostic repetitions of that same binding test established failures before fixes: foreign runtime acceptance (`f7ab3f1c6a004c20b771f26c0bd71cd2`), civilization mismatch (`858b55014910427aadca23d9a914c04f`), combined Scout/structure/civilization gaps (`2eff2dc1a7ee482b82435b318f54cda1`), and override reclaim (`1381934912b54b88a7957d2c8505a70c`). A stale pre-import run and a temporarily incompatible NUnit assertion were corrected; neither is counted as success evidence.

No full EditMode, full PlayMode, hostile suite, standalone build, native UI test or paid provider call ran in this refresh. Existing dirty provider/UI/voice/package changes were preserved. The hostile test file received only three mechanical constructor-call updates for the internal runtime-scoped binding signature; it was not executed.

Next owner: AntiGravity, starting with `antigravity-test-plan.md`. Pay particular attention to concurrent producer attribution and previously queued units; baseline exclusion is not queue-token provenance. See `known-limitations.md`. Do not begin another implementation phase automatically.

## Historical implemented slice

- Added bounded semantic action nodes for movement, scouting, patrol, rally, attack/defend/retreat, repair and research.
- Added detached unit/location selectors with owned/visible/worked-resource rules.
- Added deterministic `CommanderCapabilityExecutor` and `CommanderCapabilityGoal` using existing commands and CommandBuffer authority.
- Added `WorkedResource` construction anchoring for Mill-style contextual placement.
- Extended OpenRouter semantic instructions and bounded context capability descriptions.
- Added focused parser/admission tests.
- Added typed `resultFromNode` validation and runtime-local result bindings.
- Captured producer baselines and exact newly-created unit/building results.
- Added fail-closed handling for incompatible, missing, stale, destroyed, cross-manager and human-overridden results.
- Preserved ordinary selector fallback only when no result reference is requested.

## Historical focused evidence

Focused Unity evidence for this pass:

- EditMode: 9/9 passed on job `dc2c49217096465ab6906b638341376b` (the existing seven Phase 4G tests plus exact-unit binding and incompatible-dependency rejection).
- PlayMode: 1/1 passed on job `084413df808e48ab8bca347be7baec13`.
- Runtime proof: three Spearmen were trained from Barracks #1, exact new IDs `6,7,8` were captured, and the subsequent `PatrolCommand` used exactly `6,7,8` around the worked Gold node.
- Unity compilation completed with zero console errors after the final source changes.

This is deliberately not full regression or hostile acceptance evidence.

## Handoff

Start with `antigravity-test-plan.md`, then `execution-mapping.md`, `selector-model.md`, `known-limitations.md`, and the source manifest. Do not begin Phase 4H.
