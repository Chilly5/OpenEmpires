# Commander Phase 4C Requirements Matrix

Final requirement-by-requirement traceability for the Phase 4C brief and
architecture spec. Evidence is current through the post-priority-fix freeze.
The final independent review recommends **READY FOR PHASE 4D**. Dynamic
housing runtime completion is a documented non-blocking coverage limitation;
`TechnologyRush`, `SiegePreparation`, and `NavalExpansion` are explicitly
deferred rather than represented as supported.

| Requirement | Authoritative evidence | Final status |
|---|---|---|
| Preserve provider → validated DTO → approval → decision policy → planner → existing RTS execution | Source review, Phase 4C reports, focused and full regressions, independent final review | **PASS**; authority path retained |
| Do not bypass `StrategicApprovalLayer`, `StrategicDecisionPolicy`, or `StrategicPlanner` | Current-source boundary review and 4C.4 direct-player GREEN plus full EditMode/PlayMode regressions | **PASS**; priority handling for the two new objective values fixed and verified |
| No direct provider commands, simulation access, or uncontrolled autonomy | Final source manifest/boundary audit and runtime approval/command assertions | **PASS**; no provider authority path added |
| Execute 4C.1 memory, 4C.2 explanations, 4C.3 richer context, then 4C.4 selected objectives as gated phases | Phase reports and gate records in `progress.md`; per-phase regression summaries | **PASS**; 4C.1–4C.4 gates complete |
| Astra owns architecture/design/security/integration/review/conflict work rather than routine tests/docs | Architecture spec and progress/delegation ledger; independent final review record | **PASS**; final review recommends ready |
| Sol owns medium features, interfaces, integrations, and non-trivial tests | Task and review artifacts under `Docs/CommanderPhase4C/` | **PASS**; actual assignments and outputs recorded |
| Luna owns routine tests/search/static analysis/docs and does not change authority/planner/execution paths | Task ledger, before snapshots, final source manifest and touched-file audit | **PASS**; scope constrained and verified |
| Before coding, record architecture, delegation, dependency order, and risks | `Docs/superpowers/specs/2026-09-15-commander-phase4c-design.md` and `progress.md` | **PASS** |
| Record Agent, Task, Expected output, and Validation for delegated work | Delegation table and task reports in `progress.md` and phase evidence files | **PASS** |
| Independent review of architecture, tests, security, and integration after implementation | `phase4c1`/`phase4c2`/`phase4c3` reviews, `phase4c4-independent-review.md`, final root review | **PASS**; no remaining Critical/Important finding |
| 4C.1 bounded deterministic local conversational memory | Memory source, named tests, focused/full results, 4C.1 report | **PASS** |
| Memory stores no permanent profile, hidden state, cloud data, embeddings, simulation objects, or commands | Immutable/detached type review, serializer and boundary audit, lifecycle/runtime tests | **PASS** |
| Memory is bounded/deterministic, resets explicitly, and clears between matches | Focused EditMode/PlayMode lifecycle/capacity/reset evidence; 4C.1 runtime report | **PASS** |
| Memory retains value summaries, not mutable decision/intent/plan/submission graphs | Source and immutable snapshot review, focused tests, final source audit | **PASS** |
| Player text remains untrusted and cannot populate trusted outcome fields | Adversarial history/request tests and payload inspection in 4C.1/4C.3 evidence | **PASS** |
| Required `Memory_DoesNotLeakGameState` | Named test result in 4C.1 regression artifacts | **PASS** |
| Required `Memory_IsBounded` | Named test result for bounds and eviction | **PASS** |
| Required `Memory_ClearsBetweenMatches` | Named lifecycle/runtime test result | **PASS** |
| Required `SameHistoryProducesSameContext` | Named deterministic-context test result | **PASS** |
| 4C.2 grounded `CommanderExplanationService`, `ExplanationContext`, `ExplanationResult` | Source review and focused/full 4C.2 evidence | **PASS** |
| Explanations use recorded outcomes, do not recompute/fabricate policy, and do not mutate pending state | Explanation tests, host query PlayMode coverage, current source review | **PASS** |
| Required `Explanation_MatchesDecisionReason` | Named focused test result | **PASS** |
| Required `ExplanationCannotModifyIntent` | Named focused test result | **PASS** |
| Required `RejectedPlanHasReason` | Named focused test result | **PASS** |
| 4C.3 detached own-player aggregates; income history only if authoritative, otherwise activity proxies/composition/bottlenecks/progress | Context-builder source, deterministic payload serialization and 4C.3 report | **PASS**; no income trend is fabricated |
| Never label stockpile deltas as income; unavailable values remain unavailable | Context source and serialization tests; static review | **PASS** |
| New context is fog-safe and excludes hidden, explored-only, predicted, or remembered enemy state | Differential fog tests, payload inspection, full 4C.3 results | **PASS** |
| Required `ContextRemainsFogSafe` | Named EditMode/PlayMode result | **PASS** |
| Required `ContextSerializationDeterministic` | Named test result and provider payload evidence | **PASS** |
| 4C.4 selects only objectives with inspected execution support | `phase4c4-integration-map.md`, implementation/review evidence, runtime completion tests | **PASS**; only Ranged Reinforcement and Defensive Turtle admitted |
| Candidate objective list does not authorize unsupported objectives | Feasibility assessment and final A–G deferred-scope section | **PASS**; TechnologyRush, SiegePreparation, NavalExpansion deferred |
| Each selected objective has distinct intent, strict parsing/DTO validation, template, feasibility, milestones, authority compatibility, provider interpretation, and completion test | 4C.4 source/tests, focused runtime evidence, post-fix full regressions | **PASS** |
| Preserve existing objective enum numeric values by appending new types | Source compatibility review and tests; post-fix priority regression | **PASS** |
| Focused and full EditMode/PlayMode regressions cover Phase 3 and 4A/4B/4C | Per-phase summaries; final 4C.4 EditMode 568/568, PlayMode 86/86, all unique and Passed | **PASS**; hashes in `phase4c4-final-regression-summary.md` |
| Static audit for credential/API-key leaks, command-buffer access, provider simulation access, provider-to-planner references | `phase4c4-final-source-hashes.json` and final boundary audit | **PASS**; credential-shape0, assignment0, protected55/55 unchanged, gaps0 |
| Final report includes A–G sections for architecture, delegation, systems, safety, tests, runtime, future work | `Docs/CommanderPhase4C.md` inspected | **PASS** |
| Runtime evidence is current and tied to implemented phase | 4C.1/4C.3 runtime evidence, 4C.4 final focused PlayMode XML, final full PlayMode XML | **PASS**; dynamic housing caveat explicitly recorded |
| Final verdict is READY only when requirements have evidence; otherwise identify concrete gaps | Updated matrix, final source audit, full results, independent final review | **PASS — READY FOR PHASE 4D** |
| Successful Commander remembers recent conversations | 4C.1 memory tests/runtime evidence and final regression record | **PASS** |
| Successful Commander explains decisions from grounded reasons | 4C.2 explanation tests and runtime integration evidence | **PASS** |
| Successful Commander uses richer detached context for strategic advice | 4C.3 context/provider tests and runtime payload evidence | **PASS** |
| Commander does not cheat, bypass authority, or directly control simulation | Static audit, approval/command assertions, final independent review | **PASS** |
| Current architecture spec and source baseline exist before implementation | `Docs/superpowers/specs/2026-09-15-commander-phase4c-design.md` and `Docs/CommanderPhase4C-source-baseline.json` | **PASS** |

## Final audit and known limitations

- Final current-source manifest `phase4c4-final-source-hashes.json`: 27/27 live
  SHA-256 entries match. It covers all 24 Task 1 paths plus
  `StrategicDecisionPolicy` and two legacy compatibility test files. All 12
  Task 1 before snapshots match; the priority policy before/current snapshots
  match. The historical Task 1 manifest has one stale test hash for
  `CommanderPhase4C4Tests.cs`, predating the direct-priority regression cases;
  it is not used to claim current-source verification.
- Final source audit: changed21, new21, missing0; protected55/55 unchanged;
  gaps0; six documented host references; credential-shape0 and assignment0.
  `.env` remains untracked and ignored.
- Dynamic housing completion was not exercised in the focused runtime fixture,
  which prebuilt six Houses. Independent review found this non-blocking: the
  supported objectives completed in runtime and the housing helper path is
  covered without a separate dynamic-housing PlayMode requirement.
- `TechnologyRush`, `SiegePreparation`, and `NavalExpansion` remain deferred
  and are not included in the readiness claim.
