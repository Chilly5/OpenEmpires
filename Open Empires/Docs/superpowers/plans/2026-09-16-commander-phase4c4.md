# Commander Phase 4C.4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Implementation requires recorded Phase4C.1, 4C.2 and 4C.3 gates.

**Goal:** Add two genuinely executable strategic objectives through the existing approval, planning and execution path.

**Architecture:** New strict templates produce finite preparation plans made from existing resource-allocation, structure-building and ensure-unit-count requests. Extend game-owned objective mappings, canonical feasibility/budgeting and whole-form interpretation together. No new command, executor, simulation mechanic or automatic evaluator rule.

**Tech Stack:** Existing Unity C#, NUnit/Unity Test Framework, Unity MCP. No dependencies or remote paid calls.

**Spec:** `Docs/superpowers/specs/2026-09-15-commander-phase4c4-design.md`. Verified source map: `Docs/CommanderPhase4C/phase4c4-integration-map.md`. Overall Phase4C user brief remains authoritative.

## Global Constraints

- No bypass of StrategicApprovalLayer, StrategicDecisionPolicy, or StrategicPlanner.
- No direct commands, simulation access from providers, or autonomous uncontrolled behavior.
- Add only RangedReinforcement and DefensiveTurtle. TechnologyRush, SiegePreparation and NavalExpansion remain unimplemented/deferred because the existing Commander executor does not support them.
- Ordinary AIRecommendation remains Normal authority; names do not confer Emergency. Existing source-aware confirmation and direct-player protection remain unchanged.
- No guessed cost constants; current game specifications supply costs. Target quantities and worker allocations are intentional design constants.
- Preserve old enum values and public API arities; preserve inherited source. No commits, pushes, merges, branches, packages, credentials, settings or unrelated edits.
- Sol implements integration and nontrivial tests; Luna performs routine validation/artifacts only; Astra reviews security/planner boundaries. One production writer/Unity runner, no worker-spawned agents.

## Task 1: Both objective templates and complete integration

**Agent:** Sol 5.6 high. **Output:** strict admission plus executable plans, focused RED/GREEN/runtime completion evidence and report. Both plans share registry/mapping/feasibility changes, so they form one reviewed integration task.

**Files:**

- Create `Assets/Scripts/AI/Commander/Strategic/RangedReinforcementPlan.cs` and `DefensiveTurtlePlan.cs`.
- Create `Assets/Scripts/AI/Commander/Strategic/RangedReinforcementPlanTemplate.cs` and `DefensiveTurtlePlanTemplate.cs`.
- Modify `Strategic/StrategicIntent.cs`, `Strategic/StrategicPlan.cs`, `Strategic/StrategicPlanTemplate.cs` for appended enums/registration only.
- Modify `Strategic/StrategicPlanner.cs`, `Strategic/StrategicPlanner.Feasibility.cs`, `Strategic/StrategicPipeline.cs`, and `Phase4B2/StrategicApprovalLayer.cs` for explicit mappings, canonical budget/worker fitting/recovery and feasibility. These are under `Assets/Scripts/AI/Commander/`.
- Modify `Phase4B1/StrategicAIJson.cs`, `Phase4B1/MockStrategicAIProvider.cs`, `Phase4B1/GeminiStrategicAIProvider.cs`, `Phase4B2/CommanderIntentRouter.cs` for the agreed allowlists/whole-form phrases.
- Modify `Phase4A/CommanderChatUI.cs` only to describe the two actual finite objectives accurately in preview. Preserve the explanation partial and all query-neutrality/reset hooks.
- Create `Assets/Tests/EditMode/CommanderPhase4C4Tests.cs` and `Assets/Tests/PlayMode/CommanderPhase4C4PlayModeTests.cs`.
- Report/snapshots/results: `Docs/CommanderPhase4C/phase4c4-task1-report.md`, `phase4c4-task1-before/`, phase-specific full detail artifacts. Snapshot additional existing test files before any intentionally required expectation update; explain why the old expected value changed. Never remove authority/regression assertions.

**Exact plan contract:**

| Plan | Constants | Ordered milestones (ID starts1; OrderIndex starts0) |
| --- | --- | --- |
| RangedReinforcementPlan | FoodWorkers8, WoodWorkers8, ArcherTarget10 | Economy(food8/wood8); Production(ensure1 ArcheryRange); Force(ensure10 Archers); Ready |
| DefensiveTurtlePlan | FoodWorkers8, WoodWorkers8, TowerCount2, SpearmanTarget8, ArcherTarget8 | Economy(food8/wood8); Production(ensure1 Barracks and1 ArcheryRange); Fortifications(build2 mandatory Towers); Force(ensure8 Spearmen and8 Archers); Ready |

Both constructors accept `(int ownerPlayerId, int sourceIntentId)` and call the existing StrategicPlan base constructor with the matching appended StrategicPlanType. Do not fill plan/milestone resource requirements with old guessed resource constants; canonical computation supplies them. Completion describes preparation, not automatic attacks or territory holding. Tower goal is not age-optional; producer goals reuse existing completed infrastructure through EnsureExisting.

```csharp
var economy = new StrategicMilestone(1, "Economy", 0);
economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Food, FoodWorkers));
economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Wood, WoodWorkers));
AddMilestone(economy);
var production = new StrategicMilestone(2, "Production", 1);
production.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
    BuildingType.ArcheryRange, 1, ensureExisting: true));
AddMilestone(production);
```

Template IDs are `ranged_reinforcement` and `defensive_turtle`; each implements the existing IStrategicPlanTemplate interface and accepts no parameters. Explicit enum validation/registry checks precede template creation. Do not add aliases for unsupported objectives.

### Steps

- [ ] Read spec/brief, source map, TDD/Unity guidance. Confirm all three prior gates and use latest full clean pair as baseline without rerunning. Save exact before-content/hashes of all existing touched files and create incremental report before edits.
- [ ] Add failing tests for both enum/template/objective paths with minimal compilable API skeletons if needed. Demonstrate assertion-level RED with saved job/detail artifacts; compilation errors alone do not count.
- [ ] Implement both concrete plans/templates using only existing request types and the exact table. Register templates and append enum members without renumbering old values. Assert milestone order/content and strict empty parameters with direct hand-checked expected values.
- [ ] Add both explicit cases to approval, pipeline and planner objective-to-plan mappings. Unknown values reject or throw according to the existing mapping boundary; never map to an existing unrelated plan.
- [ ] Add canonical QuoteObjective cases: ranged ensures ArcheryRange then trains ArcherTarget; turtle ensures Barracks/ArcheryRange, builds mandatory TowerCount, trains both unit targets. Preserve existing foundation/queued-unit/population/housing/age/producer helpers. Add an explicit default rejection to the objective switch so an unhandled enum cannot be accidentally capable at zero cost.

```csharp
case StrategicObjectiveType.RangedReinforcement:
    Build(BuildingType.ArcheryRange, 1, ensure: true);
    Train(CommanderIntentCatalog.ArcherUnitType, RangedReinforcementPlan.ArcherTarget);
    break;
case StrategicObjectiveType.DefensiveTurtle:
    Build(BuildingType.Barracks, 1, ensure: true);
    Build(BuildingType.ArcheryRange, 1, ensure: true);
    Build(BuildingType.Tower, DefensiveTurtlePlan.TowerCount);
    Train(CommanderIntentCatalog.SpearmanUnitType, DefensiveTurtlePlan.SpearmanTarget);
    Train(CommanderIntentCatalog.ArcherUnitType, DefensiveTurtlePlan.ArcherTarget);
    break;
default:
    Reject("Unsupported strategic objective.");
    break;
```

- [ ] Include both new concrete plan types in all three existing checks: submission-time canonical budget assignment, FitEconomyToAvailableWorkers, and insufficient-resource preparedEconomy/recovery handling. Do not broaden these checks to unrelated custom/legacy plans. Ensure milestone requirements come from actual queued/owned deficits and canonical costs, with funded foundations charged only once.
- [ ] Fit worker targets using the existing largest-target/first-order tie shrink. For the two new plans only, minimum target is0 when available workers are fewer than allocation buckets, otherwise1. Existing request/goal APIs admit zero; keep all old plan fitting behavior. Zero available workers remain infeasible. Test one-worker funded execution as well as sum of fitted targets.
- [ ] Extend strict JSON, mock, Gemini allowlist and router consistently. Phrases: `prepare ranged reinforcements` and `prepare fortified defenses`, normalized under the existing whole-form punctuation/whitespace policy. Unknown/mixed/hostile appended instructions and nonempty parameters reject. Model output remains only objective/parameters; it cannot choose owner/identity/source/priority/authority.
- [ ] Preview wording must identify ranged as10 archers plus required ArcheryRange; turtle as2 mandatory towers plus8 spearmen/8 archers and required producers. Explicitly describe finite preparation, not walls, keeps, garrisons, perimeter placement, autonomous holding or attack micro. Preview creates zero gameplay effects. Existing Approve/Confirm controls remain the only UI submission path.
- [ ] Add per-objective admission tests for canonical costs, available resources after reservations, zero workers, required age, producer/queue/population limits, funded foundation reuse, owned/queued unit deficits, explicit normal AI authority, emergency conflict, direct-player protection, exact confirmation/replay refusal. Keep original4 objective identities and behavior covered. Show every declared objective has explicit meaningful feasibility rather than relying on enum iteration alone.
- [ ] PlayMode for EACH objective: build a actual CommanderChatUI preview from the whole-form request; assert zero plans/goals/commands before approval; invoke actual Approve button; advance existing planner/goal/simulation execution within a declared tick bound; observe completed plan, infrastructure, unit targets and reservation release. Turtle must actually build its towers; ranged must actually build missing ArcheryRange and train a deficit. Fixtures must not pre-satisfy all new milestones. Report any timeout as failure, not completion.
- [ ] Add funded one-worker runtime case for the new fitting rule, and a below-required-age turtle rejection proving towers are not silently skipped. Do not modify execution/simulation code or suppress assertions/logs to make runtime pass.
- [ ] Run focused EditMode/PlayMode category CommanderPhase4C4, preserve full results/source hashes/compiler state. Self-review all boundary changes and report exact files, RED/GREEN, runtime completion ticks, canonical-cost evidence and concerns. Freeze; Luna receives full regression ownership.

## Task 2: Final objective gate and overall completion audit

**Agents:** Luna artifacts/full tests/static/report formatting; independent reviewer and Astra architecture/security review.

- [ ] Produce actual diff/new-source package versus Task1 snapshots, not HEAD. Independent task review covers strict admission, honest scope, all three planner integration checks and real runtime results; fixes use original implementer and scoped re-review.
- [ ] Run full EditMode and full PlayMode on frozen final source once, covering all previous phases. Preserve detailed unique-test XML/JSON, job IDs and source hashes. No overlapping restarts on transport timeouts.
- [ ] Audit provider/service references, command/network/executor/simulation frozen files and credential shapes. Explicit game-owned StrategicPlanner changes are authorized only for the scoped integration above; providers may not reference it. No secret values printed.
- [ ] Astra verifies both objectives actually complete through approved runtime path, prior phase gates remain valid and final full suites pass. Broad final review covers complete Phase4C changes and any ledger rulings/deferred observations.
- [ ] Luna updates `Docs/CommanderPhase4C.md` sections A-G with actual final architecture, delegation, new systems, safety, tests, runtime and deferred unsupported work. Astra performs requirement-by-requirement audit against original full user brief. Final verdict is exactly `READY FOR PHASE 4D` only when all requirements/gates are evidenced; otherwise continue fixes, or use `REQUIRES FIX PHASE` when handing off a genuine unresolved blocker. Do not mark the goal complete from an in-progress document.

## Preflight/self-review

| Pair/task | Shared interface / risk | Resolution |
| --- | --- | --- |
| Enums -> registry/parser/mappings | Declared value without implementation | Both explicit mappings/templates/quotes; unknown fail closed |
| Templates -> planner budget/fit/recovery | Concrete type checks omit new plans | All three checks listed and tested |
| Canonical quote -> existing execution | Guessed costs or double charging foundations | Existing game queries and hand-checked deficits |
| Worker fitting -> allocation requests | One worker cannot satisfy two positive targets | New-plan-only zero target, real execution test |
| Router/providers -> approval | Nominal preview or manufactured authority | Strict DTO/identity and actual Approve runtime tests |
| Task1 -> Task2 | Runtime proofs vs template-only tests | Both completed plans/infrastructure/units required |
| Task1 self-consistency | Two finite supported objectives only | No research/naval/siege executor additions |
| Task2 self-consistency | Final proof spans all four phases | Full suites plus original-brief audit and A-G report |

Primary risks are nominal unsupported objectives, authority escalation by naming, incomplete canonical budgeting/worker fitting, age-skipped towers and tests that bypass the UI. Explicit tests above address each. This plan is architecture-only until4C.3 stabilizes; recheck current source interfaces before dispatch.
