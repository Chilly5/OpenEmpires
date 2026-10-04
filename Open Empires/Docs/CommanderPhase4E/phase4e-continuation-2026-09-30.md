# Phase 4E continuation evidence

The goal remains active. The phase is not ready for Phase 4F.

## Current verification

- Full EditMode before the Unicode edit: job `d4f0e1e8a5e24c2691d70b772e36d330`, 870/870 passed, zero failures/skips, 202.9856321 seconds. Complete payload archived alongside this note.
- Full PlayMode before the Unicode edit: job `5261f48cc5514fb3abdcd12450a5dde5`, terminal failed after 181/181 executed. One reported failure in `CancellationAtResolvingCreatesNoGoal_AndRetryWorks` was an unexpected Unity Package Manager authentication error. Backend returned `result: null`; exact aggregate pass/skip totals are unavailable. This is not a clean regression gate.
- Malformed Unicode RED: `2adfdabb53f44cfda1ad4884c949ba6d`, 41 tests executed, six malformed-surrogate tests failed because invalid payloads were accepted. The earlier 33-test run `648303f94ab84700b781ac3bd6e811f6` used stale imported tests and is not RED evidence.
- Unicode GREEN: `c44dac265c7943bc82dbf4e0f9f5211d`, 41/41 passed, zero failures/skips, 0.9974935 seconds. Both escaped and raw unpaired surrogates reject; valid raw and escaped supplementary Unicode passes. Raw evidence: `phase4e-unicode-red-green.json`.

`CommanderSemanticJson.CheckStrictSyntax` now validates decoded UTF-16 surrogate pairing before Json.NET can replace malformed escapes. This is a provider-boundary validation change only.

## Remaining concrete work

1. Resource reachability classification is now repaired. `PlanGather` receives a bounded route-check result from `SelectEconomyWorker`; checked candidates with no legal route enter the existing blocked/retry/timeout lifecycle, while worker-protection cases remain retryable. RED/GREEN evidence is in `phase4e-resource-route-red-green.json`; focused PlayMode job `d51d316139db43eabdf407803655d041` passed 11/11.
2. The provider spatial contract is now explicit: Luna is instructed to emit only the bounded semantic `placement` object and `producerFromNode` binding; coordinates, IDs, workers, tiles, commands, and goals remain forbidden. Focused transport coverage was added in `CommanderPhase4ATests`; Unity verification is pending because the editor test runner is stale.
3. Recheck manual-worker protection after its existing 900-tick lease and determine whether active manual gathering remains protected according to accepted policy.
4. Exercise the bounded live corpus beyond the four recorded calls.
5. Current-source full suites and build are now complete: EditMode 879/879 (`9af83fd99d2140e48dd90804855d8d21`), PlayMode 183/183 (`f321b28e3f774a0e9e62c384271c63f9`), and Windows build `build-d4843e59db` with 0 errors/74 warnings. Complete raw payloads and current hashes are archived.
6. Resume interactive standalone smoke and remaining natural-language scenarios. No Windows app input was issued during this continuation; the current player still needs visual launch/UI verification and scenario A–J standalone traces.

The read-only audit confirmed the valid compound fixture ticks normal construction and production to ten Spearmen; the old limitation claiming it only tests producer invalidation was corrected. Fog gates inspected in the context builder, resolver and planner were sound. These inspections do not establish complete standalone acceptance.
