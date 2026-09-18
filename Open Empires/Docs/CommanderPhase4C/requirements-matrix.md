# Commander Phase 4C Requirements Matrix

Traceability baseline for the Phase 4C user brief and the architecture spec in
`Docs/superpowers/specs/2026-09-15-commander-phase4c-design.md`. This document
does not claim implementation, test, or runtime completion. “Verified” is used
only for discovery/design/baseline facts inspected in the checkout.

| Requirement | Expected authoritative evidence | Current status |
|---|---|---|
| Preserve provider -> validated DTO -> approval -> decision policy -> planner -> existing RTS execution | Current source review and final architecture/security review | Verified as specified design; implementation/runtime unverified |
| Do not bypass StrategicApprovalLayer, StrategicDecisionPolicy, or StrategicPlanner | Static audit plus focused/regression tests | Implementation/test unverified |
| Do not create direct commands, give providers simulation access, or add uncontrolled autonomy | Static dependency audit and command-buffer runtime assertions | Implementation/test/runtime unverified |
| Execute 4C.1 memory, then 4C.2 explanations, then 4C.3 richer context, then 4C.4 objective expansion as gated sub-phases | Phase-specific reports and gate results before each successor | Unverified |
| Astra handles architecture/design/security/integration/review/conflict work, not routine tests or documentation | Delegation plan and final review ledger | Design requirement verified in architecture spec; execution unverified |
| Sol handles medium features, interfaces, integrations, and non-trivial tests | Delegation ledger with implementation/test ownership | Design requirement verified in architecture spec; execution unverified |
| Luna handles routine tests/search/static analysis/documentation and must not change authority, planner, or execution paths | Delegation ledger, touched-file audit, and review | Design requirement verified in architecture spec; execution unverified |
| Before coding, record architecture plan, delegated tasks, dependency order, and risk assessment | Architecture spec and phase progress ledger | Verified in current design/progress documents |
| Each delegated task records Agent, Task, Expected output, and Validation | Phase-specific progress ledger | Unverified for implementation run |
| After implementation, Astra reviews architecture, tests, security, and integration | Final review record and requirements audit | Unverified |
| 4C.1 provides bounded, deterministic, local short-term conversational memory | `CommanderMemory`, `ConversationState`, `MemoryEntry` source and focused tests | Unverified |
| Memory stores no permanent profile, hidden state, cloud storage, embeddings, simulation objects, or commands | Type/static audit and behavioral leak tests | Unverified |
| Memory has bounded size, deterministic behavior, explicit reset, and clears between matches | Focused lifecycle tests and runtime reset evidence | Unverified |
| Memory records only value summaries; never retains mutable `StrategicDecisionRecord`, intent, plan, or submission graphs | Source review and immutable snapshot tests | Unverified |
| Player text remains untrusted and does not populate trusted outcome fields | Adversarial history tests and memory/request payload inspection | Unverified |
| Required 4C.1 test: `Memory_DoesNotLeakGameState` | Named EditMode/PlayMode result with emitted-request and hidden-state assertions | Unverified |
| Required 4C.1 test: `Memory_IsBounded` | Named test result proving count/text limits and eviction | Unverified |
| Required 4C.1 test: `Memory_ClearsBetweenMatches` | Named lifecycle test result | Unverified |
| Required 4C.1 test: `SameHistoryProducesSameContext` | Named deterministic-context test result | Unverified |
| 4C.2 provides grounded `CommanderExplanationService`, `ExplanationContext`, and `ExplanationResult` | Source review, decision-record projection, and focused tests | Unverified |
| Explanations use actual recorded reasons/outcomes and do not recompute or fabricate approval/policy | Explanation tests and no-mutation runtime evidence | Unverified |
| Required 4C.2 test: `Explanation_MatchesDecisionReason` | Named test result | Unverified |
| Required 4C.2 test: `ExplanationCannotModifyIntent` | Named test result | Unverified |
| Required 4C.2 test: `RejectedPlanHasReason` | Named test result | Unverified |
| 4C.3 adds optional detached own-player aggregates: income trends only if authoritative history exists; otherwise activity proxy, composition, bottlenecks, and plan progress | Context-builder source review and serialized provider payload | Unverified; optional/candidate wording preserved |
| Do not label stockpile deltas as income; unavailable data must be unavailable, not fabricated zero | Context model/source audit and serialization tests | Unverified |
| New context remains fog-safe and excludes hidden, explored-only, predicted, or remembered enemy information | Differential fog tests and payload inspection | Unverified |
| Required 4C.3 test: `ContextRemainsFogSafe` | Named test result | Unverified |
| Required 4C.3 test: `ContextSerializationDeterministic` | Named test result | Unverified |
| 4C.4 selects only objectives with inspected execution support; candidate list is not permission to invent all objectives | Objective feasibility assessment and approved sub-phase spec | Design requirement verified; scope/implementation unverified |
| Possible objective candidates: TechnologyRush, SiegePreparation, NavalExpansion, DefensiveTurtle | Source-backed feasibility report records selected and rejected candidates | Candidate nature verified; support unverified |
| Every selected objective has distinct intent, strict DTO/parser validation, registry template, feasibility quotation, milestones, policy/approval compatibility, provider interpretation, and end-to-end completion test | Objective implementation report and runtime tests | Unverified |
| Preserve existing objective enum numeric values by appending new types | Source review/static compatibility check | Unverified |
| All phases include focused tests and full EditMode/PlayMode regression, including Phase 3 and Phase 4A/4B coverage | Fresh Unity runner outputs and per-phase JSON evidence | Unverified |
| Static checks find no credential/API-key leaks, advisory CommandBuffer access, provider GameSimulation access, or provider-to-planner references | Static audit report over current source | Unverified |
| Final deliverable contains `Docs/CommanderPhase4C.md` sections A Architecture changes, B Agent delegation report, C New systems, D Safety boundaries, E Tests, F Runtime evidence, G Future improvements | Final report inspected for all seven sections | Unverified |
| Runtime evidence is current and tied to the implemented phase, not merely prior saved evidence | Fresh Unity runtime/test evidence and detailed logs | Unverified |
| Final verdict is `READY FOR PHASE 4D` only when every requirement is evidenced; otherwise `REQUIRES FIX PHASE` with concrete gaps | Final requirements audit and report verdict | Unverified |
| Successful Commander remembers recent conversations | Memory behavior tests and runtime evidence | Unverified |
| Successful Commander explains decisions from grounded reasons | Explanation tests and runtime evidence | Unverified |
| Successful Commander uses richer detached context to provide better strategic advice | Context serialization/provider tests and runtime evidence | Unverified |
| Successful Commander never cheats, bypasses authority, or directly controls simulation | Static safety audit and runtime command/authority assertions | Unverified |
| Current architecture spec and Phase 4C source baseline exist before implementation | `Docs/superpowers/specs/2026-09-15-commander-phase4c-design.md` and `Docs/CommanderPhase4C-source-baseline.json` | Verified (discovery/baseline only) |
