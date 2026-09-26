# Phase 4D.3 Task 2 — detached adaptation value, scoped gate

Status: accepted at the Task 2 scoped gate on 2026-09-25. This is not a Phase 4D.3 or Phase 4D final gate; host integration, runtime recovery proof, fresh full suites, and final authority audit remain pending.

## Changed files and boundary

- `Assets/Scripts/AI/Commander/Strategic/StrategicAdaptationProposal.cs` and `.meta`: immutable scalar/list copies for source plan identity, health blocker, exact old milestone targets and canonical budget, proposed fixed-template targets, and a separately labeled feasibility quote; pure capture/build/freshness operations. No stored planner, simulation, Unity object, command, delegate, or live plan/health/intent object.
- `Assets/Tests/EditMode/CommanderPhase4D3Tests.cs` and `.meta`: the 17 required/edge proposal cases, then four review-driven regressions for unversioned budget/target changes and quote order/duplicates (21 total).
- No host, bridge, planner, goal-manager, package, setting, or credential change in Task 2.

`Capture` copies the actual current milestone tactical requests, including worker targets fitted by the planner, and the approved budget from the live plan synchronously. `Build` validates a supported pending intent and copies proposed template targets without submitting it. `IsFresh` checks source identity, lifecycle, milestone, blocker children/deficits, old canonical budget, and old exact targets. The proposal remains advisory; the quote is not an approved budget.

## Review and RED→GREEN evidence

The original implementer created the value and first 17 tests and saved native Unity XML: focused 17/17 (`phase4d3-task2-focused-final-1fd91da2.xml`), affected 4D1/4D2 90/90 (`phase4d3-task2-affected-final-73652cd8.xml`), and decision 3C5 13/13 (`phase4d3-task2-affected-3c5-669dd9ea.xml`). Its original RED job/artifact was not preserved in this checkout, so it is not claimed as recorded evidence. Root independently reran the original focused suite, job `c05fd017b5ec4c059aeb726e3b650b7b`, 17/17, and the affected 4D1/4D2/3C5 suites, job `3b30303aa57343fc84e8f97bdeaaeb41`, 103/103, against the pre-review Task 2 source.

An independent Sol read-only review found one Important stale-token gap and a quote canonicalization gap: public `StrategicPlan.AddBudgetRequirement` and a tactical target mutation can change displayed old values without advancing plan revision, while the first `IsFresh` compared only identity/blocker data; reversed quote-cost input order produced different JSON and duplicate resource entries were accepted. Root reproduced these against the old code before changing production:

| Focused RED job | Observed failure | Saved terminal payload |
|---|---|---|
| `8f423da857fd4239b949db6a4f68224d` | Old proposal remained fresh after canonical budget changed with unchanged revision; 1/1 expected failure. | `phase4d3-task2-review-red-budget-8f423da8.json` |
| `081ac2cae8334f89a2539fe259b0ebab` | Old proposal remained fresh after worker target changed with unchanged revision; 1/1 expected failure. | `phase4d3-task2-review-red-target-081ac2ca.json` |
| `6aa2a2d9a9ac49b3b9de95205b116ea5` | Reversed semantically equal quote costs changed JSON; duplicate resource quote returned a proposal; 2/2 expected failures. | `phase4d3-task2-review-red-quote-6aa2a2d9.json` |

Root then added exact value comparison for old budgets/targets (job `53886e230b0a4e31a7552bf3e98d84dd`, 2/2 GREEN), sorted detached quote costs by resource type and rejected duplicates (job `5a18c161b5fe405abfecb68ee58414c1`, 2/2 GREEN). Final current-source focused-plus-affected EditMode job `9ab867ed29c844c09be2751a292292db` discovered 124 tests and passed 124/124, failed/skipped 0. Complete terminal payload with all discovered IDs/results is `phase4d3-task2-review-final-124-9ab867ed.json` (SHA-256 `FE8ECF054D741C5D4A929F1A0BA0AD2E7FBC5BC90E48C27CFB2AFFADF5C1E811`). Unity console query for `error CS` returned zero entries after the final run; expected cancellation log entries are not compiler errors. `git diff --check` found no whitespace errors (only Git's LF→CRLF warnings on existing tracked files).

Current source SHA-256: proposal `D59A5EF6D4ED363C79B9F3AD75761673DC0DFA2ED095268EE17BA93DC5B4A497`; tests `F0855ECB92D49251C7664493C4FD1E280E24567151B22F76E95E86D3586F4B0D`. The independent reviewer found no direct execution-authority crossing. A Minor test limitation remains: `AdaptationProposal_DoesNotCallPlanner` observes plan/goal/reservation counts rather than instrumenting every possible read-only planner call; direct source inspection shows the builder has no planner dependency or call. Whole-phase review will revisit this boundary.
