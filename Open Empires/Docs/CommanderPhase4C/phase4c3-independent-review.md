# Phase 4C.3 independent review — fix round

Verdict: **conditionally acceptable for Task 2 full regression and boundary audit**. No remaining Critical or Important finding in the frozen Task 1 scope. This is a source/test/evidence review, not a fresh Unity run or full-suite result.

## Prior finding

- **Resolved (Minor, deterministic serialization):** `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIContextSerializer.cs:44-49` now includes `ThenBy(value => value.AvailableCapacity)` after the other production keys. Previously, two detached same-type/same-count entries with different idle capacities retained insertion order in `productionCapability`, changing the full provider request. `Assets/Tests/EditMode/CommanderPhase4C3Tests.cs:157-185` reproduces precisely this multiset reversal and compares complete `GeminiStrategicAIProvider.BuildRequestJson` strings. Saved RED XML `phase4c3-task1-review-production-tie-red-TestResults.xml:2` records 0/1; fix-round EditMode XML `phase4c3-task1-review-fix-editmode-TestResults.xml:2` records 10/10.

## Previously noted coverage gaps

- **Visible-threat isolation addressed:** `Assets/Tests/EditMode/CommanderPhase4C3Tests.cs:78-94,481-490` now confirms visible enemy count changes, then compares the complete safe-context JSON after normalizing only `visibleThreats`. Hidden and explored-only state still must leave complete provider request bytes equal (`:57-75`).
- **Actual chat transport strengthened:** `Assets/Tests/PlayMode/CommanderPhase4C3PlayModeTests.cs:134-168` injects one owned worker and two queued barracks units, inspects the captured HTTP body for exact worker-activity and production-pressure values, and checks no plan, intent, reservation, goal, command, or decision history before approval. Saved fix-round PlayMode XML `phase4c3-task1-review-fix-playmode-TestResults.xml:2` records 2/2. Army and plan values are checked through the separate provider/value and runtime tests, not individually in this transport test; that remains a non-blocking test-scope distinction, not an observed defect.

## Scope and gate

The three changed existing files were compared with `phase4c3-task1-before/`, not HEAD. The new insight values/builder and focused tests were inspected for owned-only inputs, immutable detached copies, integer arithmetic and overflow rejection, optional explicit serialization, and authority side effects. Current serializer/EditMode/PlayMode SHA-256 hashes match `phase4c3-task1-report.md:51-55`. The saved focused XML shows 10/10 EditMode and 2/2 PlayMode; the full Phase 4C.3 regression and protected-boundary audit remain Task 2 gates. No source or test file was changed by this review.
