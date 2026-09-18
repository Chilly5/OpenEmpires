# Commander Phase 4C — A–G report

Status: **IN_PROGRESS**. This is the required current-state report, not a
READY verdict and not an overall completion claim. Phase 4C.1 has passed its
recorded gate; Phase 4C.2 Task 1 is admitted and in progress; 4C.3 and 4C.4
remain planned and unimplemented.

## A. Architecture

- Phase 4C.1 adds bounded, match-local immutable memory and detached request
  snapshots while preserving the existing approval, policy, pipeline, planner,
  and executor authority path.
- Providers receive copied context/memory values only; they do not receive
  simulation, planner, command, decision-graph, or delegate references.
- Reset, reinitialize, owner-change, destruction, cancellation, and late
  response paths clear or reject stale local state.
- Phase 4C.2 is specified as a host-only projection of copied strategic
  outcomes and current plan primitives into a deterministic offline explanation
  service. See [4C.2 design](superpowers/specs/2026-09-15-commander-phase4c2-design.md).

## B. Agent delegation

- Sol: production integration and nontrivial tests; current 4C.2 Task 1
  implementation worker.
- Luna: discovery, requirements matrix, read-only boundary audit, evidence
  packaging, regression, and artifact maintenance.
- Astra: architecture, security, integration, and independent review.
- One production writer and one Unity runner owner; no worker-spawned agents.

## C. New systems

Implemented and gated in 4C.1:

- `MemoryEntry`, `CommanderMemory`, and `ConversationState` with bounded,
  immutable copied values and explicit lifecycle clearing.
- Tactical/strategic detached memory request paths, contextual mock behavior,
  untrusted-memory labeling for Gemini payloads, and generation/cancellation
  protection against stale publication.
- Explicit turn-start memory capture, capacity eviction behavior, owner/runtime
  consistency checks, and reflection-compatible bridge constructors/method
  shapes.

Admitted but not complete:

- 4C.2 deterministic explanations: immutable explanation values/service,
  host-only provenance/current-plan projection, neutral read-only chat queries,
  and focused evidence. No 4C.2 completion is claimed.

Planned only:

- 4C.3 richer observed context; see [4C.3 design](superpowers/specs/2026-09-15-commander-phase4c3-design.md).
- 4C.4 selected supported objectives; see [4C.4 design](superpowers/specs/2026-09-15-commander-phase4c4-design.md).

## D. Safety boundaries

- No provider authority, direct commands, autonomous uncontrolled behavior, or
  bypass of `StrategicApprovalLayer`, `StrategicDecisionPolicy`, or
  `StrategicPlanner`.
- No hidden information, future prediction, fabricated reasons, or resource
  stock deltas labeled as income.
- Explanation values/services remain detached from simulation and planning
  object graphs; query routing must not clear, approve, replace, or execute a
  pending recommendation.
- Existing public APIs and tactical/strategic behavior remain preserved.
- Boundary audit: 55 protected files unchanged; 7 changed and 5 new scoped
  source/test files; no missing files; zero credential-shape or assignment
  matches. Six references are documented `CommanderChatUI` host candidates;
  none are explanation-service/value-type candidates. `.env` is untracked and
  ignored.
- No credentials, package settings, authentication behavior, log suppression,
  commits, pushes, merges, or unrelated changes were made.

## E. Tests and static evidence

Phase 4C.1 final fix-round-2 evidence:

- Full EditMode job `2c350469769c460ea10838ddaacc0737`: 512/512 passed, zero
  failures/skips. XML: `Docs/CommanderPhase4C/phase4c1-fixround2-full-editmode-TestResults.xml`.
- Full PlayMode job `14031c0c376d450ca2a6f845f69ab555`: 69/69 passed, zero
  failures/skips. XML: `Docs/CommanderPhase4C/phase4c1-fixround2-full-playmode-TestResults.xml`.
- XML validation found 512/512 unique EditMode test IDs and 69/69 unique
  PlayMode IDs. Phase4C1 coverage was 9 EditMode and 17 PlayMode cases.
- Focused fix-round-2 evidence: EditMode `79591d15af904ad1bad9d2115ededcd8`
  78/78; PlayMode `06b0dd1130c64e84afb41dce30f4a35e` 20/20. Detailed artifacts
  are preserved under `Docs/CommanderPhase4C/`.
- Independent review passed after two fix rounds; no new code breakage was
  reported.
- Refreshed audit: `Docs/CommanderPhase4C/phase4c1-fixround2-final-boundary-audit.json`.

4C.2 focused/full tests are pending its own implementation, review, and gate.

## F. Runtime evidence

- The gated 4C.1 runtime path demonstrated preference capture, detached
  provider memory, pending preview without execution, explicit approval, and
  created-plan/approved-memory behavior through the existing UI path.
- Runtime coverage also verified reset, destruction, owner replacement, late
  tactical/strategic responses, capacity eviction, strict whitespace routing,
  and pending-preserving queries.
- These are 4C.1 evidence only. No 4C.2, 4C.3, or 4C.4 runtime proof is
  claimed yet.

## G. Future improvements and deferred objectives

Selected future objectives for 4C.4, only after 4C.1–4C.3 gates:

- `RangedReinforcement`: allocate eight food/eight wood workers, ensure one
  ArcheryRange, ensure ten Archers, then ready.
- `DefensiveTurtle`: allocate eight food/eight wood workers, ensure Barracks
  and ArcheryRange, build two mandatory Towers, ensure eight Spearmen and eight
  Archers, then ready. It is finite fortified-force preparation, not territory
  holding or perimeter automation.

These choices are specified in [4C.4](superpowers/specs/2026-09-15-commander-phase4c4-design.md)
and based on existing executor support. They do not promise walls, keeps,
garrisons, upgrades, perimeter placement, attack micro, or silently optional
towers.

Deferred and currently unsupported:

- `TechnologyRush`: no Commander research/age-progression goal.
- `SiegePreparation`: no admitted SiegeWorkshop or siege-unit support in the
  Commander catalog/planner.
- `NavalExpansion`: no required naval structures/units in the gameplay model.

The three deferred candidates remain unavailable; names or enum aliases alone
would not constitute execution support. Genuine income trends also remain a
future improvement because no authoritative detached income history is
available; resource stock deltas will not be relabeled as income.

Final verdict: **PENDING** completion of 4C.2, 4C.3, selected 4C.4, their
reviews/regressions/runtime gates, and the final requirements audit.
