# Commander Phase 4C.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make Commander remember bounded recent conversations and explicit cavalry preference within one local match, without gaining gameplay authority.

**Architecture:** UI-owned ConversationState contains bounded immutable-value CommanderMemory. The strategic request carries a detached advisory snapshot; interpreter output still passes existing validation and explicit approval. Clear/reset/new runtime/destruction invalidate async publication.

**Tech Stack:** Existing Unity 6000.5.9f1 C#, Newtonsoft.Json, NUnit/Unity Test Framework, Unity MCP 10.2.0. No new dependencies.

**Spec:** `Docs/superpowers/specs/2026-09-15-commander-phase4c-design.md`, especially 4C.1 and global safety/lifecycle rules. Authoritative user brief: `C:/Users/RS/.codex/attachments/691a2ccc-7545-452f-934d-6f5278f532ce/pasted-text-1.txt`.

## Global Constraints

- No bypass of StrategicApprovalLayer, StrategicDecisionPolicy, or StrategicPlanner.
- No direct commands, simulation access from providers, or autonomous uncontrolled behavior.
- Only local bounded memory; no permanent profiling, hidden AI memory, cloud storage service, or embeddings.
- No hidden enemy information, simulation objects, or commands in memory.
- Preserve the original four strategic objectives and existing tactical behavior in 4C.1.
- No commits, pushes, merges, branch switches, package changes, or credential changes. Preserve inherited dirty/untracked work.
- Sol implements/integrates and writes non-trivial tests. Luna handles routine audit/regression/report work only. Astra owns architecture/security/integration decisions and reviews.
- One production implementation worker and one Unity test runner owner at a time. No worker-spawned agents.

## Task 1: Integrated match-local memory

**Agent:** Sol 5.6, high reasoning. **Expected output:** working memory and chat integration with focused EditMode and PlayMode evidence, plus a task report. **Validation:** test-first focused run, full EditMode/PlayMode regression, static audit, independent task review.

**Files:**

- Create `Assets/Scripts/AI/Commander/Phase4C/MemoryEntry.cs`, `CommanderMemory.cs`, `ConversationState.cs`.
- Create `Assets/Tests/EditMode/CommanderPhase4C1Tests.cs`, `Assets/Tests/PlayMode/CommanderPhase4C1PlayModeTests.cs`.
- Modify `Phase4A/CommanderChatUI.cs`, `Phase4B2/CommanderIntentRouter.cs`, `Phase4B2/StrategicAIApprovalBridge.cs`, `Phase4B1/StrategicAIInterpreter.cs`, `Phase4B1/MockStrategicAIProvider.cs`, `Phase4B1/GeminiStrategicAIProvider.cs` under Commander.
- If needed, modify the existing conversation history/adapter only for bounded reset/lifecycle integration; first locate its actual file. Do not refactor tactical execution.
- Evidence/report paths: `Docs/CommanderPhase4C/phase4c1-task1-report.md`, phase-specific JSON beside it, and `Docs/CommanderPhase4C/progress.md` (orchestrator owned).

**Interfaces:**

- `MemoryEntry`: immutable value with deterministic sequence, `MemoryEntryKind`, bounded text, optional typed cavalry preference; no object/delegate or runtime-state payload. Kinds distinguish player/commander conversation, preference, approved strategy, decision, explanation.
- `CommanderMemory(int capacity = 32)`: validate capacity 1..128, retain at most capacity entries, truncate each text to 512 characters, evict oldest. `Snapshot()` returns copied read-only entry collection; `Clear()` empties it and resets sequence. `ToJson()` stable invariant ordering. Expose safe record methods for conversation, cavalry preference, and primitive outcome/explanation summaries. Do not accept StrategicIntent/StrategicPlan/StrategicDecisionRecord/GameSimulation inputs.
- `ConversationState(int playerId, int capacity = 32)`: owns `CommanderMemory`, exposes PlayerId and detached snapshots, resolves retained explicit cavalry preference by scanning bounded entries, `Reset()` clears it. No secondary unlimited preference cache. UI creates a fresh state for a new player/runtime.
- `StrategicAIRequest`: add optional detached memory values to BOTH constructor overloads without breaking legacy calls; clone on construction. Legacy ConversationHistory remains supported. New value field has no identity/authority meaning.
- `StrategicAIApprovalBridge`: optional application-supplied memory snapshot provider (returns values only), or value snapshot parameter to TranslateAsync. Must not own a game service. Existing constructor calls stay valid. Clear/reset clears its private history and invalidates pending/inflight translation.
- `CommanderChatUI`: read-only `Conversation` property for visible/testable state plus public reset method used by `clear memory` and lifecycle. Memory query/preference routing happens before code that clears pending strategy/latest decision. Reset invalidates pending/inflight translation and clears histories/transcript/latest stale fields. Preference and show-memory queries themselves do not execute or clear pending strategy.

Only change these signatures where current source requires a concrete compatibility adjustment; report the reason and exact final interface before downstream tasks consume it.

### Steps

- [ ] Read applicable AGENTS.md, the task/spec, TDD skill and writing-good-tests reference, and Unity MCP skill/resources. Read only directly relevant current source. Before source edits confirm `CommanderPhase4C-source-baseline.json` exists and covers current files.
- [ ] Run full EditMode and PlayMode baseline once via Unity MCP, saving detailed results. Runner must be idle and compiler fresh. Prior 4B.2 saved evidence is not a fresh run. On a failure report it to the orchestrator before changing unrelated code.
- [ ] Add failing behavioral tests. For brand-new types, compile a minimal throwing/empty API skeleton if needed, then demonstrate assertion-level RED before implementing behavior. A compile failure alone is not RED proof. Example intended behavior:

```csharp
// These calls illustrate the required behavior; use the final record method names consistently.
var memory = new CommanderMemory(2);
memory.RecordConversation(CommanderConversationRole.Player, "first");
memory.RecordConversation(CommanderConversationRole.Player, "second");
var before = memory.Snapshot();
memory.RecordConversation(CommanderConversationRole.Player, "third");
CollectionAssert.AreEqual(new[] { "second", "third" },
    memory.Snapshot().Select(entry => entry.Text));
CollectionAssert.AreEqual(new[] { "first", "second" },
    before.Select(entry => entry.Text));
```

Required named tests: `Memory_DoesNotLeakGameState`, `Memory_IsBounded`, `Memory_ClearsBetweenMatches`, `SameHistoryProducesSameContext`. Behavioral leak test must inspect actual emitted request values and prove owned snapshots/mutable source changes and hidden enemy fixture data cannot appear through memory; static type checks supplement, not replace, runtime behavior.

- [ ] Implement immutable memory values and deterministic bounded store. Restrict preference to explicit supported cavalry value. Reject invalid enum/capacity/owner inputs. Ensure bounded collection snapshots and no live object references.
- [ ] Add failing integration tests using actual CommanderChatUI and existing game fixture. Submit `focus cavalry.` then `prepare attack`: assert remembered preference appears in captured provider request and produces AttackPreparation preview with cavalry focus; plan/goal/command counts stay unchanged until actual Approve control is invoked. Empty/evicted preference yields no guessed execution. Explicit cavalry request remains compatible. Show memory reveals the retained values; clear memory empties all conversation/request histories and pending state.
- [ ] Integrate memory with request construction, mock provider, Gemini request JSON, and UI. Keep all model-provided history in data fields below unchanged authority instructions. Do not duplicate raw turns into both legacy history and memory payload unnecessarily. Bound rendered transcript as well as provider history. Store actual accepted strategy summaries only after successful `StrategicDecisionRecord.Submission` acceptance; inspect the real submission API rather than guessing Selected means accepted.
- [ ] Add lifecycle tests for reset/reinitialization/player change/destruction and a provider ignoring cancellation. A response started before reset must not repopulate memory, transcript, LatestStrategicInterpretation, or pending intent. Ordinary query/preference messages must not auto-approve or erase a pending recommendation. Reject mixed/hostile whole-form messages as existing routing does.
- [ ] Verify focused EditMode and PlayMode GREEN with full per-test JSON, fresh assembly timestamps, and no compiler errors. Run the entire EditMode/PlayMode regression suites once on final task source. If a focused fix changes source after full regression, rerun affected focused tests; orchestrator determines whether full rerun is necessary to establish final phase gate.
- [ ] Self-review touched files for safety, compatibility, immutable projections, secret-free diagnostics, explicit reset semantics, and unrelated changes. Write report with exact source changes and interfaces; RED command/job/failure; GREEN job/counts; full regression jobs/counts; remaining concerns. Do not claim later phases complete.
