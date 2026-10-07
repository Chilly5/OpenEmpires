# DynamicPlan validator

Final-source closeout: Edit48/48 (866e8777), Play5/5 (3284c57a), zero console errors. Canonical civilization replacement selection/production/context and truthful preview labels, plus bounded approval/admission trace correlation, are included in the focused evidence. All named artifacts/guideline/roadmap are synchronized and hashed. The final implementation report/source manifest supersede intermediate closeout counts below; no new paid provider reruns or exhaustive acceptance are claimed.

Status: Implementation handoff — focused evidence; exhaustive acceptance pending. Structural and shared-constraint behavior have focused evidence; exhaustive semantic, authorization, and runtime acceptance remains an audit gate.

## Envelope and bounded structure

`CommanderDynamicPlanParser.ParseDynamicTrusted` parses version 1 strict JSON into immutable typed nodes. A node has exactly `id`, `mechanic`, `parameters`, `inputs`, and `dependsOn`. Duplicate/unknown fields, unsupported versions/mechanics, malformed unions, non-integer numeric fields, invalid references, and trailing or malformed JSON reject the entire candidate. No valid prefix is independently executable.

Current limits: 32,768 response characters; 12 nodes; dependency depth 5 (root depth 0); 4 references per node counting each dependency and typed input occurrence; aggregate declared source/effect counts 200, excluding partition views; graph IDs `[A-Za-z_][A-Za-z0-9_-]{0,31}`; canonical ID strings at most 64 characters; mechanic/enum strings at most 32 characters; JSON depth 12. Construction count is at most 20 and selected-unit count at most 50, in addition to simulation/population/content bounds. Legacy Request/Answer parsing retains its existing 8,192-character and depth-8 limits.

References are graph-local symbolic node IDs, never runtime entity IDs. Inputs must match the producer result type and also name their source in `dependsOn`. Missing, self, cyclic, cross-graph, wrong-type, or otherwise invalid references reject. Forward references are permitted only when the graph dependency/reference rules validate. Conditions are not implemented and must not be advertised.

The optional shared `constraints` array reuses the existing strict semantic constraint reader and immutable DTO model; it allows no more than four distinct entries of types `NoConstruction`, `PreferredWorkers` (`IdleOnly`), `MaximumQueue`, `ProtectedResource`, and `ResourceSource`. Only one `ResourceSource` entry is currently supported per candidate. Resource/source enum compatibility is validated against `ResourceSourceRules`. These constraints are semantic restrictions, not authority, and must propagate through derived prerequisites and planner-internal preparation as well as direct graph effects.

## Validation stages

1. Strict envelope and primitive-field shape validation.
2. Graph structure, reference typing, cycles/depth/reference counts, and aggregate/per-mechanic bounds.
3. Canonical IDs, effective-player availability, supported mechanic, source/resource compatibility, and ordinary game-rule validation in trusted code.
4. Whole-graph constraint/root-effect matching and independently typed authority checks.
5. Side-effect-free compiler binding and normalized preview; approval then rechecks candidate freshness, owner/runtime identity, availability, and reservations before all-or-none initial admission.
6. Each later dispatch rechecks real targets, exact results, ownership, constraints, and sticky human override.

The parser establishes only stage 1–2. A structurally valid plan is not canonical, executable, authorized, or accepted. Existing focused structural/bounds evidence is recorded in the ledger; latest broader EditMode green does not replace the remaining stage-specific tests.

## Repair boundary

Provider handling allows one initial response and at most one schema-only repair. A repair is eligible only if changing integer-shaped numeric strings yields an already fully strict-valid template; the repaired response must itself strict-parse and equal that exact template ignoring JSON object property order. This cannot alter outcome, fields, values, roots, references, or effects. It is not a general semantic retry. Authority/injection/unknown-field/type/reference/transport/quota/truncation failures do not qualify; no fallback mode or third call is permitted.

The ledger records 79/79 provider/authority/UI/grounded-scope affected EditMode cases, additional constraint/parser and lifecycle-focused runs, and six scoped real Luna/native scenarios. Preparation-source/DTO/takeover passed 28/28 (`preparation-source-dto-takeover-green-9045d7dc.xml`); the cached-preparation `preserveGold` defect was independently reproduced then fixed, with retained 29/29 coverage (`preparation-preserve-green-337a21c9.xml`). Current-source parity passed 5/5 before that narrow fix and final updated-source PlayMode passed 5/5 (`final-focused-play-green-8d930473.xml`). Fresh read-only review found no remaining Important defect in the helper/DTO/compiler/manager slice. Exhaustive all-route freshness/constraint, regression, standalone and multiplayer proof remain audit gates; do not claim end-to-end certification from focused slices alone.
