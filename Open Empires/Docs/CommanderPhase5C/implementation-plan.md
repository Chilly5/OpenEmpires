# Phase 5C Implementation Plan

> For agentic workers: serialized subagent-driven-development, task by task. Exactly one designated source writer and Unity runner; other inventories/reviews are read-only. See progress.md for ownership.

**Goal:** implement the complete Phase5C brief, not another audit-only checkpoint.
**Architecture:** reuse existing typed semantic graphs, deterministic planners,
native commands, structural authority and UGUI/TMP; add bounded adapters at their
existing seams. Provider packing/usage is observational and grants no authority.
**Tech Stack:** Unity6000.5.9f1, Input System1.20.0, UGUI2.5.0/TMP, Newtonsoft JSON;
existing Node speech-gateway for Web semantic forwarding.
**Spec:** [spec.md](spec.md) and its full user brief.

## Global constraints

- Strictly fewer than4000 complete model-facing input tokens per initial/repair.
- Conservative estimate and returned actual usage recorded separately; missing usage unknown.
- Preserve15s production timeout; one eligible strict repair; no silent retry/model fallback.
- One final offline EditMode/applicable PlayMode pass after source stabilizes; one Windows/Web build.
- No commit/push/reset/clean, remote deployment, credentials inspection or voice/multiplayer redesign.
- Latest user approval: maximum10 phase paid attempts inclusive of repairs/timeouts; no historical budget reuse.
- Exact actor/result identity, fog, canonical facts, human priority and approval remain game-owned.

## Review focus

1. Mixed-family compound requests must retrieve every relevant contract without dropping negation/order.
2. A local yes resolving a name must not also approve a compound plan or replay a stale request.
3. Full Farms with eligible incumbents must not cause false Idle-based rejection or evict protected actors.
4. Native formation stopping positions and loss must remain distinct from arrival and human takeover.
5. Web/request repairs must obey the same input cap/accounting even when actual usage is absent.

### Task 1: reproducible request accounting and compact packing (checkpoint A)

Files: provider `Phase4A/OpenRouterCommanderProvider.cs`; new `Phase5C/CommanderSemanticRequestPack.cs`
and `CommanderProviderUsage.cs`; existing semantic request/vocabulary/registry; gateway
`../backend/speech-gateway/gateway.mjs` only if a forwarding/usage change proves needed.
Tests: new `CommanderPhase5CProviderBudgetTests.cs`; existing gateway semantic tests.
Interfaces: consume `CommanderSemanticProviderRequest`; produce
`CommanderSemanticRequestPack.TryBuild(request,out pack,out reason)`, immutable packed
JSON/section estimates and nullable typed usage observations; initial+repair share budget.

- [ ] Capture production body locally with fixture transport; save section sizes/unknown actual usage.
- [ ] RED: ordinary request too large; returned usage discarded; oversized/repair guard absent.
- [ ] Implement compact registration-driven contract/relevant canonical/context slices with full meaning.
- [ ] GREEN: captured requests under conservative margin, no oversized dispatch/prefix execution;
  usage/repair/gateway parity and malformed/missing metrics tests; no paid setup probe.
- [ ] Keep model/provider choice; verify actual endpoint structured-output support before enabling it.

### Task 2: typed graph clarification and single-stage continuations (checkpoint B)

Files: `Economy/CommanderPendingClarification.cs`, semantic clarification codec,
`Phase4A/CommanderChatUI.Clarification.cs`, submission lifecycle/action-plan route.
Tests: new `CommanderPhase5CConversationTests.cs`, existing economy/clarification/UI fixtures.
Interfaces: bounded typed pending graph with missing slot/proposal, alternatives,
original text, immutable resolved fields/edges/results, request revision/owner/runtime.
Local completion returns a strict validated semantic result, never approval authority.

- [ ] RED Mine→yes and House→future-five→From trees through actual SubmitMessageAsync.
- [ ] Extend existing drafts/graphs, local name/source/count/yes/no resolution, explicit corrections.
- [ ] GREEN zero extra provider calls, compound retained, one stage per control, stale/reset/replay
  rejected; new commands supersede draft without cancelling accepted work.

### Task 3: native correctness and causal facts (checkpoint B)

Files: `CommanderPlanner.cs`, `Phase4G/CommanderCapabilityExecutor.cs`,
`Economy/CommanderPlanner.Economy.cs`, tactical status/read-only context and UI reply projection.
Tests: `CommanderPhase5CNativeTests.cs`, existing exact-result/gathering/authority fixtures.
Interfaces: shared resolved legal movement objective and native arrival observation;
joint capacity-aware frozen-worker selection; bounded owned task facts, not simulation dump.

- [ ] RED occupied-Farm eligible incumbents, blocked defense center/formation arrival,
  death falsely labeled takeover, relevant causal task facts omitted.
- [ ] Reuse native route/formation/capacity/authority; distinguish missing/dead/foreign/
  garrisoned/protected; no substitution of exact results or distant-idle false arrival.
- [ ] GREEN genuine native commands/results and interruption/IdleOnly/path controls.

### Task 4: bounded action/location and repeated-build adapters (checkpoint C)

Files: existing semantic/dynamic registrations, graph admission, capability executor/
location selectors, native goal observation and repeated-placement planner.
Tests: Phase5C native/semantic fixtures and current dynamic/result-binding fixtures.
Interfaces: relative visible source selector (source+map-west+owned anchor); bounded
Garrison/Ungarrison and finite discovery goals using existing commands; repeated anchored
build count through existing graph/placement/result accounting.

- [ ] RED ALL Wood workers→west-TC trees, anchored two Houses, garrison capacity,
  unseen-base discovery versus visible point scouting.
- [ ] Implement ordinary adapters; distinct sites/costs/timers/N progress; truthful
  partial shelter; bounded fog-derived scouting without hidden-entity targeting.
- [ ] GREEN native completion/loss/exhaustion/human/cancel/reset/owner/fog controls.

### Task 5: modern populated UI and explicit recovery (checkpoint C)

Files: `CommanderChatUI.cs`, presentation/task-board/action-plan/settings partials;
new Phase5C UI partial only when it keeps changes focused; existing input ownership.
Tests: `CommanderPhase5CUIPlayModeTests.cs`; Windows/served-Web populated screenshots.
Interfaces: objective/status/approval projections from validated request and task facts;
latest request status independent of selected older card; Retry/Edit/Cancel single-use lifecycle.

- [ ] RED cancel visibility, conflated current/old status, timeout draft recovery,
  double Enter/approval/input leakage and minimum-size layout.
- [ ] Charcoal380–460px adaptive dock, Chat/Tasks tabs, readable bubbles/cards,
  fixed multiline composer, always-visible Cancel/approval, separate settings/history.
- [ ] GREEN1280×720,1920×1080,scaled Editor populated states; minimized startup,
  no forced opening/disabled tasks; voice/PTT review routes and online consent preserved.

### Task 6: durable integrated scenario runner (checkpoint D)

Files: new `Phase5C/CommanderScenarioEvidence.cs` if needed for production metrics;
versioned bounded corpus/runner in tests/Docs; reuse real UI/provider/admission/simulation.
Interfaces: scenario record(request ID,utterance,normalized fields,draft delta,approval,
native result/task state,section+actual usage,error stage,elapsed), safe durable per-scenario files.

- [ ] Offline corpus covers every closure-matrix interaction and schema-valid wrong outcomes.
- [ ] Native bounded deterministic scenarios cover results/capacity/destinations/recovery.
- [ ] Three10–15min-total live/UI checkpoints within authorized10-attempt cap;
  Windows and served Web, no whole match/audio/duplicate jobs or lock-losing journal.

### Task 7: final freeze, regression/builds and handoff

- [ ] One final source-matched offline EditMode+applicable PlayMode; durable IDs/results,
  no duplicate run after observation timeout; classify every failure.
- [ ] Refresh compatibility/full relevant source+legacy-test manifest; build Windows
  and Web once each; identify runtime DLL/wasm/source, populated final UI evidence.
- [ ] Final independent read-only review, requirement audit, exact counts/cost/input/latency,
  first-attempt versus retry results, report/root roadmap; no Phase5B acceptance/next phase.
- [ ] Close only with requested implemented or specific partial verdict. All open requirements stay open.

## Execution rulings

The user supplied the full architecture and explicitly authorized implementation;
no repeated design approval is required for ordinary work. Inline execution is the
single-writer method. Live paid authorization is independently pending. Skill
worktree/commit/full-suite-each-task defaults yield to this brief's live-checkout,
no-commit and one-final-regression requirements; evidence/ledger remains durable in Docs.
