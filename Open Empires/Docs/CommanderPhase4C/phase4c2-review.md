# Commander Phase 4C.2 Task 1 independent review

## Verdicts

**Spec verdict: FAIL**

**Quality verdict: CHANGES REQUIRED**

## Findings

1. **Important — A recorded `NoDecision` event is conflated with complete absence of evidence.** The host deliberately projects a real `NoDecision` event and copies its decision ID, historical tick, outcome reason, and objective into `ExplanationContext` (`Docs/CommanderPhase4C/phase4c2-review-package.md:775-813`, especially lines 786-787 and 809-812). The renderer then returns only `No recorded decision is available.` for every `NoDecision` context (`Docs/CommanderPhase4C/phase4c2-review-package.md:1029-1033`), discarding the copied ID, tick, and recorded reason. This violates the binding requirements to preserve the original outcome/reason/ID/tick, cover all six outcomes with their actual meaning, and allow an initially empty source to describe an observed `NoDecision`. The same conflation makes empty-context `LastRejection` and `AttackReason` responses claim that the latest recorded outcome was `NoDecision` even when no event was recorded (`Docs/CommanderPhase4C/phase4c2-review-package.md:999-1011`). The focused outcome test hard-codes this conflation by passing a decision ID, tick, and reason while expecting the unavailable message (`Docs/CommanderPhase4C/phase4c2-review-package.md:1147-1159`). Distinguish a truly empty context from an observed `NoDecision`; render the observed event's bounded reason and historical metadata, while all decision-derived queries over a truly empty context report evidence as unavailable.

2. **Minor — The focused tests do not exercise all required source-copy and host-projection cases.** The value test proves collection copying only by clearing the caller's list (`Docs/CommanderPhase4C/phase4c2-review-package.md:1185-1205`), and the host mutation test proves that asking questions does not mutate pending intent or gameplay state (`Docs/CommanderPhase4C/phase4c2-review-package.md:1401-1450`). Neither mutates an original decision/intent/plan after host projection and proves the captured explanation remains unchanged, as required by the binding verification contract. In addition, the six-outcome test constructs `ExplanationContext` directly (`Docs/CommanderPhase4C/phase4c2-review-package.md:1147-1159`), so it does not verify the host classification branches for `TransitionRefused`, `PlannerRejected`, or `SelectionNotSubmitted` at `Docs/CommanderPhase4C/phase4c2-review-package.md:775-793`. Static inspection shows primitive copying, so this is an evidence-quality gap rather than a demonstrated authority defect.

## Cannot verify from the supplied package

- Fresh full EditMode and PlayMode regression remain outside Task 1; the implementation report explicitly leaves them to Task 2 (`Docs/CommanderPhase4C/phase4c2-task1-report.md:99-103`), and the static evidence report likewise says they remain an orchestrator gate (`Docs/CommanderPhase4C/phase4c2-evidence-report.md:91-95`).
- The final focused jobs are accepted as recorded evidence, but their raw final XML was not emitted into the workspace (`Docs/CommanderPhase4C/phase4c2-task1-report.md:49-51`).

## Strengths

- The explanation value/service boundary is deterministic, bounded, immutable, and free of simulation, planner, pipeline, provider, command, callback, delegate, and Unity-object references (`Docs/CommanderPhase4C/phase4c2-review-package.md:836-1114`).
- Whole-form query routing happens before pending-state mutation, is invariant/offline, and preserves hostile appended text for the existing rejection route (`Docs/CommanderPhase4C/phase4c2-review-package.md:290-328`, `720-751`, and `823-830`).
- Synchronous submission provenance is captured around approval/evaluation and cleared in `finally`; rejection projection never uses `ActivePlanType` or prose (`Docs/CommanderPhase4C/phase4c2-review-package.md:381-400` and `775-812`).
- Current-plan queries call only `CaptureContext()` and immediately copy bounded primitive plan state, while focused PlayMode evidence verifies no query-side plan/history/goal/command advancement (`Docs/CommanderPhase4C/phase4c2-review-package.md:754-773` and `1453-1487`).
- Reset, initialization/session replacement, and destruction clear copied explanation state without rehydrating decision history (`Docs/CommanderPhase4C/phase4c2-review-package.md:220-277`, `412-435`, `502-523`, and `815-821`).
