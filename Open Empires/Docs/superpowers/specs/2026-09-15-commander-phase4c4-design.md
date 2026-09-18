# Commander Phase 4C.4: Executable objective expansion

Architecture contract only; no implementation until 4C.1, 4C.2 and 4C.3 have passed their gates. This preserves the original Phase4C objective-expansion requirement without adding unsupported gameplay.

## Selected objectives

`RangedReinforcement`: a finite ranged-force preparation plan. Milestones: allocate eight food and eight wood workers (using existing fitting to available workers); ensure one ArcheryRange; ensure ten Archers; ready. It differs from MilitaryReinforcement's two Barracks and mixed eight-spearman/six-archer target.

`DefensiveTurtle`: a finite fortified-force preparation plan, not autonomous territory holding. Milestones: allocate eight food and eight wood workers; ensure Barracks and ArcheryRange; build two mandatory Towers; ensure eight Spearmen and eight Archers; ready. The UI and explanation must explicitly describe these counts and limitations. It differs from DefensivePreparation's age-optional towers and spear-only target. There is no wall, keep, garrison, perimeter-placement or attack-micro promise. An age where towers are unavailable must not silently mark the tower milestone complete.

Each uses existing StrategicResourceAllocationGoalRequest, StrategicBuildStructureGoalRequest and StrategicEnsureUnitCountGoalRequest. No new tactical goal or command type, execution adapter, simulation mechanic, research/naval feature, or autonomous recommendation rule is added.

TechnologyRush is deferred because Commander lacks research/age progression goals. SiegePreparation is deferred because CommanderIntentCatalog/CommanderPlanner do not admit SiegeWorkshop or siege units despite latent simulation support. NavalExpansion is deferred because the gameplay model lacks the required naval structures/units. Adding names alone would not satisfy the user's execution-support requirement.

## Admission and identity

Append objective and plan enums to retain existing numeric identities. Register distinct strict templates; no parameters are accepted for these two objectives. Add explicit mappings in approval, pipeline and planner, with fail-closed behavior for unknown values. Every declared objective must have explicit non-accidental feasibility coverage: an unhandled switch arm must not yield capable/zero-cost approval.

Support whole-form chat phrases `prepare ranged reinforcements` and `prepare fortified defenses`, plus the explicit type names only where existing direct DTO routing convention supports names. The mock/Gemini interpreter and strict JSON parser must agree. Unknown/mixed/hostile requests remain rejected. Memory is advisory and cannot create or replay approval. All AI results use trusted allocated identity and AIRecommendation source; explicit confirmation follows existing source/priority rules.

## Feasibility and budget

Quote canonical current construction/training costs, available resources after reservations, owned/queued unit deficits, actual producer availability, age, workers, population/queue capacity, funded foundations and housing requirements. The quote belongs to the game-owned StrategicPlanner.Feasibility partial; providers see only the detached result.

For ranged reinforcement, ensure ArcheryRange and quote the deficit to ten archers. For turtle, ensure Barracks/ArcheryRange, quote two additional towers net of funded in-progress work under the existing build-goal semantics, then unit deficits to eight spearmen/eight archers. Completed existing producer buildings are reused. Do not charge paid foundations twice or charge full target armies when existing/queued units count toward ensure targets.

Include both new plans in the planner's canonical per-milestone budget calculation and existing FitEconomyToAvailableWorkers handling. Current source limits those operations by concrete plan type; new templates must not accidentally retain zero budgets or unfitted worker allocations. No new guessed resource-cost constants are permitted. Target counts/worker allocations are design constants; costs come from current game specifications.

Verified integration detail: there are three relevant concrete-plan checks in StrategicPlanner: submission-time canonical budget assignment, FitEconomyToAvailableWorkers, and the insufficient-resources `preparedEconomy` branch that defers spending while creating recovery gatherers. Both new plans must participate in all three; ordinary legacy/custom plans retain their behavior.

Worker fitting must not demand two simultaneous workers when only one exists. Existing resource allocation requests/goals explicitly admit target zero (`StrategicTacticalGoalRequest` and `CommanderGoalManager.SubmitResourceAllocation`), while the existing fitter stops shrinking at one per resource. For the two new plans only, permit a zero target when available workers are fewer than allocation buckets, retaining the existing largest-target/first-in-order tie rule. Thus total fitted targets never exceed available workers; no existing plan's fitting policy changes. Zero-owned-worker admission remains rejected by existing feasibility. Test a resource-funded one-worker plan through the existing executor, not only the fitted totals.

Execution continues to revalidate resources/placement/path requirements through the existing Commander executor. Snapshot feasibility cannot promise placement or future resource availability. Failure/wait reasons must be honest and explained as recorded.

## Priority and commitment

Ordinary approved AI requests remain Normal authority, including DefensiveTurtle. The new defensive objective must never inherit Emergency simply from its name. PlayerDirect remains above AIConfirmedPlayerCommand, above ordinary AIRecommendation as enforced by existing source-aware transition rules. Confirmed requests cannot replace direct-player plans. No change to the existing emergency-defense trigger/evaluator.

Initially treat both new plans as conflicting with existing active plans under the conservative default commitment policy. Do not invent compatibility exceptions solely to make tests green. Normal recommendations cannot replace protected player or emergency plans; source-aware explicit confirmation can replace permitted AI plans through existing policy/planner boundaries.

## Required verification

For EACH objective, test strict validation, template selection, meaningful milestone order/content, known canonical costs, age/worker/producer/population/resource rejection, owned/queued/foundation reuse, unknown enum fail-closed, accepted Normal source, emergency conflict, direct-player protection, exact confirmation and replay refusal. Check provider-to-DTO-to-approval-to-policy-to-planner integration, not a direct template call alone.

PlayMode must create an actual chat preview with zero new plan/goal/command; invoke actual Approve UI control; step only existing planner/goal/simulation execution; observe plan completion, required structures/units and reservation release. Turtle must actually build its towers rather than starting with enough existing objects to skip every new milestone. Ranged must actually provide the required production infrastructure and train its unit deficit. Use deterministic visible terrain/resource fixtures and bounded simulation ticks; report timeout/failure honestly.

Run all focused tests and full Phase3/4A/4B/4C regressions, source-boundary/credential audits, and independent architecture/integration review. A green template unit test cannot substitute for runtime completion. Do not claim READY FOR PHASE4D until both objectives and prior phases meet their gates and final A-G documentation is verified.

## Delegation

Sol implements and tests the objective templates and their medium-complexity integration. Astra owns admission/authority/planner boundary decisions and reviews the diff before the gate. Luna handles routine evidence/audit/report work and must not modify authority, planner boundaries, or execution paths. The exact implementation plan is written after 4C.3 final interfaces are available.
