# Commander Phase 4D.1 — Player Strategic Plan Controls

## Intent and scope

The player can inspect, pause, resume, and intentionally cancel an approved strategy without granting the provider lifecycle authority. This is the first gate of the four-phase Phase 4D brief at `C:/Users/RS/.codex/attachments/6ef5fd8d-d949-4b52-9c96-f84d02fb0968/pasted-text-1.txt`. The current uncommitted Phase 4C housing fix is the source baseline recorded in `Docs/CommanderPhase4D/phase4d-source-baseline.json`. No Phase 4D.2–4D.4 production behavior belongs in this gate.

## Existing architecture and decisions

- `StrategicPlan` already has a `Paused` status, a monotonically allocated per-planner ID, owner ID, creation tick, milestones, child-goal IDs, and reservation IDs. `StrategicPlanner.CancelPlan(int)` already releases plan reservations and cancels plan-owned nonterminal goals. Keep that public signature for compatibility.
- The planner supports up to four compatible plans. A bare “current strategy” command selects only when exactly one nonterminal plan is owned by the trusted local Commander player. With zero plans, report no active plan; with multiple, fail closed and ask for a specific plan through a per-plan UI affordance. Do not change legacy concurrency policy in 4D.1.
- Text controls are strict, normalized whole-form commands handled locally before any tactical/strategic provider call: `pause [current] strategy`, `resume [current] strategy`, `cancel [current] strategy`, `strategy status`, and `current strategy status`. Only one optional terminal `.` or `?` is stripped consistently. Mixed or incomplete forms are rejected by the existing route; they do not become controls.
- Host-origin actions use trusted `Conversation.PlayerId` and the current `StrategicPlanner`; no player identity comes from text or AI output. Requests carry copied primitive identity: owner, plan ID, creation tick, and plan revision. A stale request fails closed if any differs at application. UI callbacks also capture the current host generation/pipeline identity so the same plan ID in a later match cannot be targeted.
- A revision records strategic lifecycle changes (status, milestone transition, plan-owned child terminal/progress transition relevant to control, or reservation transition), not arbitrary simulation ticks. Increment deterministically and checked. Status queries are detached observations; they do not revise or reserve.

## Authority and lifecycle

`CommanderChatUI` recognizes exact text and invokes a validated control boundary on the planner. This boundary rechecks the captured token, current planner instance, owner, terminal state, and action. Provider responses and detached recommendations have no access to it. Existing approval/policy/planner path remains the only way to start or replace a strategy. `CancelPlan(int)` remains an internal-compatible primitive, not a host authorization API.

Pause applies on the main-thread deterministic tick boundary: no further plan milestone advances, plan-owned goals are not evaluated to produce new commands, no plan-owned goals or strategic reservations are created. Already-enqueued atomic simulation actions may finish; manual goals and manual commands continue normally. Completed progress and existing strategic reservations remain. A plan-owned goal's active-time duration, blocked retry/timeout, and construction/economy cooldown anchors are shifted by the paused tick delta on resume, with sentinel/overflow handling, so a long pause does not cause immediate artificial expiry. On resume the *same* plan and child goals continue; no duplicate goal or reservation submission occurs. A child completion realized by an in-flight atomic action is observed after resume, then normal milestone progression may occur.

Cancel terminates only the captured plan; it reuses deterministic plan-owned goal cancellation and plan-keyed reservation release. It neither deletes completed world assets nor refunds funded actions, and it does not cancel unrelated player/Commander goals or manual commands. Pending/late provider translations are invalidated through the existing bridge generation/cancellation mechanism when a lifecycle action changes plan state. Conversation-only memory reset does not cancel a live plan; match teardown/reinitialization clears host control state and destroys the old runtime.

## UI and feedback

Add a small current-plan status line and separate Pause, Resume, and Cancel controls to the existing Commander panel. If multiple plans are active, a bounded selector cycles through their displayed plan IDs; bare text controls still fail closed. Enabled state derives from the current authoritative plan snapshot, never a stale cached reference. Cancel is two-step: first click arms a clearly labeled confirmation for the captured plan identity, second deliberate click applies only if the token still matches. Any plan change or reset disarms it. A status query and all control results use deterministic bounded text, with no provider call or gameplay command creation.

## Safety and compatibility

No new strategic objective, networking message, package, credential path, command-buffer path, or autonomous execution. The local host remains bound to its `CommanderGoalManager.PlayerId`. Reuse the existing approval and replacement semantics. The protected `CommanderGoalManager` (and possibly `CommanderGoal`) is an intentional exception only if needed to suspend plan-owned goals without suspending manual ones; record before/after hashes, exact diff, focused regression, and independent authority review before accepting that change. The frozen 55-file boundary list is retained separately.

## Verification and gate

Write behavioral RED tests first for all eleven named 4D.1 tests in the Phase 4D brief, plus multiple-active-plan fail-closed, stale same-ID/new-runtime, long-pause timeout preservation, and manual-goal continuity. Save actual RED output. Focused GREEN must be 100%, compiler/import error count zero, independent Sol-class review clean of Critical/Important findings, relevant Commander regressions green, and static boundary audit classified. PlayMode must use real chat approval for RangedReinforcement pause/tick/resume/completion and DefensiveTurtle partial-progress/cancel, including reservation and unrelated-command assertions. Only after this gate may 4D.2 production code begin.
