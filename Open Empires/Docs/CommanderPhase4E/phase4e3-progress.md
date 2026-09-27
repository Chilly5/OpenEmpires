# Phase 4E.3 progress — bounded compound and dependent requests

## Implemented slice

- Semantic JSON now accepts a bounded graph of up to four typed nodes, with optional `dependsOn` node indices and `producerFromNode` for unit goals.
- Parser validation rejects dangling/duplicate/self references, cycles, excessive dependency depth/references, incompatible producer types, and provider authority fields. Producer references normalize into the dependency set.
- `CommanderSemanticGraphAdmission` performs a second game-side safety gate and computes deterministic dependency-first order. Strategic nodes are not admitted through the tactical graph path.
- `CommanderGoalManager.SubmitSemanticGraph` validates the complete graph before publishing any goal, then commits all goals and links dependent unit goals to the exact build goal. The normal single-node path remains unchanged.
- `EnsureUnitCountGoal` and `CommanderPlanner` require a linked build result when present. Training waits for the new producer, revalidates owner/type/alive/compatibility, and blocks instead of falling back to another producer.
- `CommanderIntentDispatcher` and `CommanderChatUI` route bounded multi-node tactical results through the atomic graph path while preserving provider generation, owner, cancellation, timeout, and strategic approval guards.
- The OpenRouter/Luna semantic prompt now describes only bounded node-index dependencies and forbids IDs, coordinates, workers, tiles, commands, callbacks, and arbitrary workflow fields.

## Focused evidence

Environment: Unity `6000.5.9f1`, editor instance `Open Empires@6d7310c7`, restarted/idle before verification.

| Scope | Job ID | Result |
|---|---|---:|
| Phase 4E.3 EditMode graph contract + admission | `7760b412589743869be101326f649aeb` | 14/14 passed |
| Phase 4E.1 host/provider/security regression | `da93e566691c4a7e8b55a1a702f8bfb2` | 71/71 passed |
| Phase 4E.2 EditMode spatial regression | `dd8880627a9247c7852d0367daba34d3` | 68/68 passed |
| Phase 4E.3 focused PlayMode compound proof | `c03b5574dd984902b8e0172f016ee201` | 1/1 passed |
| Phase 4E.2 PlayMode spatial suite (including compound proof) | `d4876944634f4c369f4e0c0fd643f1af` | 3/3 passed |

The PlayMode proof created the new west-of-Town-Center Barracks through the ordinary placement command, bound its real building ID (`#2` in that fixture), waited while it was under construction, and then emitted a Spearman training command targeting that same ID. No live provider/API call was needed for this deterministic proof.

## Deliberate scope boundary

This slice provides bounded graph admission, exact producer binding, and the required end-to-end chain. It does not yet claim Phase 4E.4 desired-state goals, Phase 4E.5 broad concurrent preparation/projection, Phase 4E.6 observability, Phase 4E.7 live provider corpus evidence, or Phase 4E.8 final security/performance audit. Resource/population preparation remains governed by existing goal scheduling; only the producer dependency is gated here.
