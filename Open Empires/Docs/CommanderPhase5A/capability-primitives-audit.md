# Phase 5A mechanic inventory

2026-10-06 read-only inventory of baseline4b0ebc3. Inventory is not implementation/runtime proof.

## Reusable paths

- `CommanderGoalManager.SubmitBuildStructure` and `CommanderPlanner.PlanStructure/PlanBuilding`: normal cumulative construction and repeated goals; existing scalar exact building results.
- `CommanderPlanner.PlanWorkedResourceBuilding`: actual owned villager target relation and visible-resource placement, not guessed proximity. Existing relation filters Food broadly, so berries-specific constraints need explicit source projection.
- `Economy/CommanderWorkerAllocation`, `CommanderPlanner.Economy`: richer Idle/Gathering/currentResource criteria, exact/additional/total/all snapshots, owned Sheep Slaughter/Gather, full preflight/reservations and sticky override. These select independently today, not a shared partition.
- `ResourceSourceRules`: canonical native Sheep/Berries/Farm/Tree/GoldMine/StoneMine classification. Reuse rather than another resource database.
- `CommanderSemanticGraphAdmission`, existing graph commit and `CommanderResultBinding`: dependencies, mandatory exact results, simulation/manager identity, read-only IDs and sticky manual takeover.
- Phase4F GameKnowledgeCatalog/EffectivePlayerKnowledge/KnowledgeSlice: detached canonical facts; capability discovery remains separate from execution support.

## Concrete gaps

Historical inventory below describes the starting snapshot, not current missing implementation. Phase 5A subsequently added native Farm support, exact producer collections, atomic shared worker partitions and source-specific worked-resource placement. See implemented-primitives.md and phase5a-implementation-report.md for the current evidence/limitations. The native Farm path uses its actual border-zero tile rule and linked food node; the inventory's suggested influence gate was not assumed authoritative.

Farm is real gameplay content but absent from Commander building parsing/capability exposure. Farm-specific tile legality and Mill/TownCenter influence must use real simulation construction rules, not only add it to a list. Farm construction creates/links a farm resource node and one-worker lifecycle.

`producerFromNode` presently requires BuildStructure count1 and consumes scalar `PlacedBuildingId`. Build2Barracks -> produce10 needs exact collection results and producer-constrained scheduling. It must wait for both newly built buildings and never search existing Barracks to compensate.

Three idle workers -> two Sheep + one Mill cannot rely on independent selectors/reservations as semantic partition proof. Select one bounded set, partition stable IDs into disjoint subsets and preflight all roles before initial admission/dispatch.

Existing worked-resource placement means Food, not specifically Berries. Add trusted canonical source filtering for visible/worked berries without hidden-state access or silent food-source substitution.

KnownIntent graph admission currently lacks request-effect grounding/compound confirmation. Registry adapters must not inherit authority from provider roots, dependency edges or labels.

## Test hooks

Extend existing Phase4G contextual Mill/canonical tests, Phase4E3 graph contract/admission tests and economy fixture paths. Keep production adapters in the existing goal/planner flow. Source inventory was performed by a read-only Luna agent; root independently verifies each relevant member before changing it.
