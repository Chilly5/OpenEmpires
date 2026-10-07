# DynamicPlan version1 data schema

Status: typed parser/compiler, game-side worker/location/producer binding and provider/UI exposure are implemented with focused evidence. Full Phase5A acceptance remains open; see execution-progress.md.

```json
{"outcome":"DynamicPlan","version":1,"nodes":[{"id":"farms","mechanic":"build","parameters":{"building":"building:Farm","count":4},"inputs":{},"dependsOn":[]}]}
```

Each node has exactly `id`, `mechanic`, `parameters`, `inputs`, `dependsOn`. IDs match `[A-Za-z_][A-Za-z0-9_-]{0,31}`. An input value names another graph node, never a runtime entity. Every typed input also requires an explicit dependency on its source. Forward declarations with valid dependencies are allowed; self/missing/cyclic/wrong-type references are rejected. Dependency-edge depth is root0, maximum5. Reference counting includes every dependency entry plus every input occurrence, maximum4 per node (the same source used in both places counts twice).

The envelope optionally carries a shared `constraints` array, maximum four entries with unique types drawn from five supported types. It reuses the strict KnownIntent reader: NoConstruction; PreferredWorkers/IdleOnly; MaximumQueue/amount1..8; ProtectedResource/resource/optionalamount0..200; ResourceSource/resource/sourceKind. ResourceSource must use a compatible resource/source pair and applies to implicit resource preparation; an explicit allocation destination must intersect compatibly. Only one ResourceSource is representable per candidate; independent multi-resource source constraints are unsupported. NoConstruction contradicts any explicit build root and covers new/resumed prerequisites; queuepolicy applies to production only. Worker eligibility and combined resource-floor losses are checked before any shared reservation. These fields are semantic restrictions, never approval/principal/provenance.

Bounds:12nodes,32768response characters,JSON depth12; canonical ID strings64characters, named enum/mechanic strings32. Aggregate declared source/effect counts<=200; partition views do not request extra entities and are not counted again. Individual construction<=20; unit selection<=50; simulation/population/canonical availability limits must additionally be checked by the trusted compiler. Existing wire Request (KnownIntent equivalent) and Answer (Question equivalent) retain8192characters/depth8 and their existing fast-path bounds.

## Current structural vocabulary

| Mechanic | Parameters | Inputs | Typed result |
|---|---|---|---|
| select-workers | count; optional state/currentResource (currentResource requires Gathering) | none | WorkerSet |
| partition-workers | offset,count | workers:WorkerSet | WorkerSet |
| select-units | exactly one canonical unit or supported kind; count | none | UnitSet |
| select-structures | canonical building,count | none | StructureSet |
| select-resources | resource,sourceKind,mode Visible/Worked,count | none | ResourceSet |
| resolve-location | relation,clearGapTiles; exactly one anchor or input | structures:StructureSet OR resources:ResourceSet OR semantic anchor MyTownCenter/MyBarracks | LocationIntent |
| build | canonical building,count | optional workers:WorkerSet/location:LocationIntent | StructureSet |
| allocate-workers | resource,sourceKind | workers:WorkerSet | AssignmentEffect |
| produce | canonical unit,count,quantityMode New/TargetTotal | optional producers:StructureSet | UnitResult |

The registry is schema metadata, NOT executable permission. No primitive is advertised to Luna until its real trusted adapter and necessary focused proof exist. Additional move/scout/patrol/rally/combat/repair/research follow-ups need real audited adapters or their preserved KnownIntent paths, not registry stubs.

Canonical IDs are source-native `unit:<integer>` and `building:<BuildingType enum name>`, not display-name guesses. Structural format checks do not prove existence/civilization/effective availability/mechanic support; the game-owned compiler must prove those before preview or admission. Resource/source combinations use existing ResourceSourceRules. No duplicate AI content/cost database.

Partition offset/count describes a view of one game-selected set. Nested ranges must stay within their parent. Parallel effect consumers cannot overlap a common worker source span; sequential reuse needs explicit dependency. Runtime identity, full set preflight/reservations and sticky takeover remain required compiler/goal work; structural disjointness alone is insufficient.

No conditions, expressions, loops, recursion, runtime/entity IDs, raw world/tile coordinates, authority/provenance fields, code, reflection calls or arbitrary methods are supported. Unknown/duplicate fields, numeric enum tokens, malformed unions and unsupported versions reject the whole graph. A valid prefix never executes independently of a later invalid node.

Runtime integration preserves request-wide negative/worker/source/producer/result constraints, side-effect-free normalized previews, one single-use plan-level confirmation, all-or-none initial work and ordinary one-command-per-planning-tick execution. Six scoped real Luna/normal-command scenarios now have passing evidence; consult execution-progress.md and the implementation report for exact artifacts and failure history. UnitSet is preflight-only in this DSL: no new dynamic patrol/scout/combat consumer is advertised. Existing KnownIntent graph result follow-ups remain available. Exhaustive acceptance is pending AntiGravity.
