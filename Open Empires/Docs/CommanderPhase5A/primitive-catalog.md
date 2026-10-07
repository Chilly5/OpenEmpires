# Dynamic primitive catalog

Final-source closeout: Edit48/48 (866e8777), Play5/5 (3284c57a), zero console errors. Canonical civilization replacement selection/production/context and truthful preview labels, plus bounded approval/admission trace correlation, are included in the focused evidence. All named artifacts/guideline/roadmap are synchronized and hashed. The final implementation report/source manifest supersede intermediate closeout counts below; no new paid provider reruns or exhaustive acceptance are claimed.

Status: Implementation handoff — focused evidence; exhaustive acceptance pending. Catalog metadata does not authorize exposing an unimplemented primitive.

The source registry is `CommanderDynamicPrimitiveRegistry` in `Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs`. It is the schema source of truth for stable mechanic IDs, parameter/input types, result kinds, and effect classes. Keep catalogs, provider vocabulary, and projections derived from this registry and game knowledge; do not maintain a parallel provider whitelist, name-to-ID map, or hand-entered cost table. Registry presence alone does not prove an executable adapter.

| Mechanic | Typed result | Purpose / current boundary |
|---|---|---|
| `select-workers` | WorkerSet | Bounded owned-worker selection by supported state/current-resource criteria. |
| `partition-workers` | WorkerSet | Offset/count view of one previously selected set; runtime roles must remain disjoint. |
| `select-units` | UnitSet | Bounded canonical unit/kind preflight. It currently has no DynamicPlan effect consumer; do not advertise it as a follow-up adapter. |
| `select-structures` | StructureSet | Bounded canonical owned-structure source for location/producer binding. |
| `select-resources` | ResourceSet | Canonical resource/source-kind plus Visible/Worked selection; no proximity/hidden-state inference. |
| `resolve-location` | LocationIntent | Semantic relation/anchor with bounded clear gap; runtime geometry and reachability still govern. |
| `build` | StructureSet | Normal construction goal, including canonical Farm support subject to real simulation rules. |
| `allocate-workers` | AssignmentEffect | Normal worker allocation against a typed worker source and canonical resource destination. |
| `produce` | UnitResult | Normal production goal; exact producer and newly produced result attribution must be preserved. |

`WorkerSet`, `StructureSet`, `ResourceSet`, `LocationIntent`, `AssignmentEffect`, and `UnitResult` are typed symbolic values, not runtime entity identifiers. `UnitSet` exists structurally but has no supported DynamicPlan effect consumer today. The compiler must switch on typed mechanic enums and trusted adapters, not reflection, generated code, or registry string dispatch. Unsupported move/scout/patrol/rally/combat/repair/research follow-ups remain unavailable through DynamicPlan unless an audited adapter is added; their existing KnownIntent paths are separate.

Shared request constraints are not additional registry primitives. The existing strict semantic parser, immutable plan model, compiler and DTO projection carry at most four distinct types: `NoConstruction`, `PreferredWorkers` (`IdleOnly`), `MaximumQueue`, `ProtectedResource`, and `ResourceSource`. Only one `ResourceSource` restriction is currently supported per candidate. It constrains explicit allocation/build effects and implicit preparation; do not silently fall back to a different source. The existing ordinary KnownIntent graph result binding remains available; this does not create a new DynamicPlan patrol/follow-up adapter.

Useful source-owned mappings: `CommanderDynamicCompiler` lowers executable nodes to existing `BuildStructureIntent`, `AllocateWorkersIntent`, and `EnsureUnitCountIntent`; selectors and semantic locations bind through runtime adapters. `GameKnowledgeCatalog`, effective-player knowledge, and `ResourceSourceRules` supply canonical content/source facts. Dynamic metadata is structural, not proof of civilization availability, placement legality, affordability, capacity, or runtime freshness.

Before advertising a primitive, require a real adapter, fail-closed validation and focused evidence for full preflight, constraints, lifecycle/cancellation, ownership and stale/lost results as applicable. Six representative Luna/native scenarios now have scoped passing evidence, including new Barracks-to-new-Spearmen binding. The cached-preparation `preserveGold` defect was reproduced and fixed, then passed 29/29 retained regression tests; a fresh read-only source review found no remaining Important issue in the helper/DTO/compiler/manager slice. Source review also finds selected/future-reference freshness logic implemented, though exhaustive matrices remain for audit. Keep those broader regression/native/multiplayer gates pending.
