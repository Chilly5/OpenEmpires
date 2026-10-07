# DynamicPlan parser draft — not implementation

This folder preserves a proposed new-file-only patch for Phase 5A. It has **not** been applied to `Assets`, compiled, tested, advertised to the provider, connected to the existing semantic entrypoint, previewed, or executed. Root integration must begin with failing tests through that existing entrypoint. No registry entry is an executable capability until a real typed adapter is installed and verified.

The parser is structural only. `unit:<int>` and `building:<enum>` syntax is bounded and typed, but canonical existence, effective-player civilization/availability, content capability, ownership, exact result freshness, and authority must be checked in the trusted compiler before candidate preview or work. Do not pass a catalog or simulation handle through the untrusted provider interface. An unknown canonical ID may parse structurally and must fail at that trusted gate.

The draft bounds the response to 32,768 characters, JSON reader depth to 12, graph node IDs to 32 characters, canonical ID strings to 64 characters, enum/mechanic strings to 32 characters, nodes to 12, combined dependency plus typed-input occurrences to four per node, dependency-edge depth to five, and aggregate declared source/effect counts to 200. Partition views are excluded from the aggregate because they do not request additional entities. The root compiler must retain stricter applicable Phase 4G per-content limits, including build count <= 20 and capability-selection count <= 50 where applicable; the draft's generic 200 field bound is not permission to relax those limits.

No conditions are in the draft schema. There are no coordinates, runtime/entity IDs, provider authority fields, expressions, arbitrary methods, reflection dispatch, loops, or executable code. The registry intentionally advertises nothing yet. Unknown fields, duplicate JSON keys, incompatible input types, missing dependency links, cycles, and parallel overlapping worker-use spans are rejected structurally. Actual worker identities, partition disjointness at dispatch, resource visibility/worked ownership, distinct placement, producer/result attribution, reservations, cancellations, and single-use approval remain root compiler/goal work.

Design decisions still for root integration:

- Confirm which `select-units.kind` values have real typed compiler adapters; do not expose unsupported selector names in provider vocabulary.
- Decide whether `resolve-location.clearGapTiles` should be required as drafted or optional with the existing deterministic default of one.
- Confirm the per-node and aggregate count policy against existing Phase 4G per-content bounds, especially repeated structures and producer-constrained production.
- Decide whether graph IDs may contain non-whitespace Unicode/symbols as drafted, or use a smaller ASCII identifier grammar.
- Add focused RED tests for the draft's four required scenarios and hostile syntax/type/reference/bound cases before applying it. Run affected compile/EditMode checks after integration; this documentation action provides no acceptance evidence.
