# Phase 4E requirements and evidence matrix

Source: user-provided Phase 4E objective, SHA-256 `E2626B5E396A5F0312D13616EFE0FB61EBC6FCE1DA1EEB9115E4C1023132C69F`. This is an initial coverage map, not a completion claim. `Pending` means no Phase 4E implementation/runtime evidence has been accepted yet.

| Gate | Objective sections | Required proof | Current status |
| --- | --- | --- | --- |
| 4E.0 baseline and investigation | 0–7 | Clean live branch/HEAD/status, exact Phase 4D source manifest and external report, hash verification, architectural inventory, no production edits before baseline, authority/delegation gates. | Baseline manifest and live-source inventory created; 56/56 Phase 4D hashes matched. External report located at the user-provided path and its verdict/HEAD/hash recorded. User approved the design with five clear footprint tiles, map-west left, and game-side-only concrete authority. Current-head baseline rerun passed EditMode 703/703 (`e105f0c02714420fb70e458c005db7b0`) and PlayMode 159/159 (`871466e022ea4457bc4f2ae080c82feb`), zero failed/skipped; complete payloads retained as `phase4e0-baseline-*.json`. Production work still pending. |
| 4E.1 natural interpretation | 8–9, 33–38 | Structured bounded provider semantics for unit/build/resource/strategic/desired/compound requests; strict reject cases; clear vs high-level vs ambiguous vs unsupported; offline controls retain precedence; prompt-injection and provenance tests. | Partial: strict tactical/strategic semantic DTO, provider and tactical host admission implemented; latest Task 3 categories 82/82. Task 4 guarded strategic staging has RED evidence and implementation under test; desired/compound interpretation and full 4E.1 gate remain pending. |
| 4E.2 references and placement | 10–14 | Owned/visible fog-safe selector tests; deterministic TC/Barracks/resource/recent selection; left/right/near/tile-distance convention; bounds, terrain, occupancy, footprint, known-area, path and tolerance tests; real spatial PlayMode proof. | Pending. |
| 4E.3 compound requests | 15–16 | Bounded acyclic typed nodes/edges/result refs, impossible/cyclic rejection, no script/tool authority, created Barracks identity carried game-side to dependent production, independent prep not serialized. | Pending. |
| 4E.4 desired-state age goal | 17–20, 39 | Actual civ landmark rule/cost/command integration; already-there, one/multi-age, deficit/prerequisite/queued/manual/cancel/reset/invalid tests; complete only after real age transition. | Source rules located; implementation and behavior proof pending. |
| 4E.5 dependency concurrency | 21–27, 40, 45–48, 62 | Before Barracks completes, resource/pop preparation begins; 10 living Spearmen eventually exist with ordinary commands; shared prerequisites deduplicated; resource/worker/human ownership safe; tick-based deterministic bounded reevaluation, retry and health/lifecycle integration; wood-stall root cause and RED/GREEN if fix needed. | Existing sequential planning and possible wood-stall causes mapped; no diagnosis or fix yet. |
| 4E.6 conversation and clarification | 28–32 | Bounded detached recent facts, unique follow-up, ambiguity clarification bound to request/generation/owner, stale reset rejection, no clarification of deterministically resolvable prerequisites. | Pending. |
| 4E.7 UI and real runtime | 41–44, 49–50, 53–54 | Ordinary typed UI, grounded acknowledgement/progress/blockers, manual play/cancel/follow-up, no JSON/console; all real scenarios A–J individually proved with exact test IDs and outputs. | Pending. |
| 4E.8 provider/build/final audit | 34–35, 51–52, 55–61, 63–69 | Controlled live provider corpus; security/static/performance audit; final EditMode >703 and PlayMode >159, zero failures/unexpected skips/inconclusive/compiler errors; Windows x64 standalone build created, launched and smoke-tested; final frozen hashes/audit/raw results/artifact hashes and report A–Q. | Pending. |

## Mandatory runtime scenarios (all pending)

| ID | Acceptance result |
| --- | --- |
| A | `hey I want 10 spearmen` produces 10 living Spearmen through normal prerequisites. |
| B | Distinct paraphrases produce equivalent desired-state semantics without phrase matching. |
| C | Barracks five tiles left of owned TC is built, used as the specific producer, and 10 living Spearmen result. |
| D | Missing Barracks, resources and population are prepared in safe overlap before serial completion would force discovery. |
| E | Natural Castle Age request from lower age reaches real simulation Castle Age legally. |
| F | `make 5 archers` then `make five more` resolves uniquely and correctly. |
| G | `make my base better` clarifies instead of selecting an arbitrary strategy. |
| H | Direct human command overrides active Commander worker use. |
| I | Held old provider reply after reset causes zero new-runtime mutations. |
| J | Hostile provider fields/authority attempt are rejected or ignored with no gameplay authority. |

The final acceptance package must also retain source/boundary manifests, raw complete Unity jobs, exact fully qualified test IDs, controlled live-provider results, standalone build location/hash and launch log, known limitations, and a report ending with exactly one Phase 4E verdict. No success verdict is inferred from this matrix.
