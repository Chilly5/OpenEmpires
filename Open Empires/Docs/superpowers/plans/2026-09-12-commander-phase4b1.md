# Commander Phase 4B.1 Implementation Plan

> For agentic workers: execute the plan task-by-task with Superpowers test-driven development and review checkpoints.

**Goal:** Translate strategic language into a validated StrategicIntent with no execution.

**Architecture:** Separate strategic interpreter interface and immutable DTO. Both mock and Gemini use one strict parser plus the existing StrategicIntentValidator. Reuse StrategicContext and Phase 4A transport/history/key infrastructure without changing those files.

**Tech Stack:** Existing C#, Newtonsoft.Json, NUnit, Unity MCP 10.2.0.

**Spec:** `Docs/superpowers/specs/2026-09-12-commander-phase4b1-design.md` and its referenced user attachment.

## Global constraints

- Phase 3 and Phase 4A are frozen. All existing production scripts remain byte-identical.
- Do not connect automatic execution. Creating StrategicIntent is the final action.
- Only AttackPreparation, DefensivePreparation, EconomicExpansion, MilitaryReinforcement.
- Never store secrets in tracked files; use the existing environment/.env loader.
- Do not mutate the inherited dirty worktree's unrelated files or commit them.

## Task 1: Interpretation boundary

Create `Assets/Scripts/AI/Commander/Phase4B1/StrategicAIInterpreter.cs`, `StrategicAIJson.cs`, `MockStrategicAIProvider.cs`; create `Assets/Tests/EditMode/CommanderPhase4B1Tests.cs`.

- [x] Write/run a failing integration test for the missing interpreter; then use typed tests for all four objectives and required security cases.
- [x] Request signature: `StrategicAIRequest(string playerMessage, StrategicContext context, int intentId, IReadOnlyList<CommanderConversationMessage> conversationHistory = null)`.
- [x] Interface: `Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(StrategicAIRequest request, CancellationToken cancellationToken)`.
- [x] Parser: `StrategicAIJson.Parse(string rawText, StrategicAIRequest request)`; exact schema, duplicate detection, limited size/depth, no coercion or arbitrary properties. Reject command/cheat/tactical/unknown objective data; allow only cavalry focus on attack.
- [x] Call `new StrategicIntentValidator().Validate(intent, request.Context.PlayerId, registry)` after strict parsing; return only accepted data without exposing the template.
- [x] Test literal input/output pairs and zero plans/goals/commands in real fixtures; deterministic mock output and injected malformed JSON.

## Task 2: Context and Gemini

Create `StrategicAIContextSerializer.cs`, `GeminiStrategicAIProvider.cs`, `StrategicAIInterpreterFactory.cs` in the new Phase4B1 folder; extend the same test file.

- [x] Test a real Gemini provider with scripted HTTP transport: success, 404 fallback, safe 401/403/429/5xx, missing candidates, invalid JSON, transport failure, cancellation and timeout.
- [x] Explicitly project allowed StrategicContext fields. Verify hidden enemy/resource changes do not change serialized request context and visible threats do.
- [x] Use `ICommanderHttpTransport`, `CommanderHttpClientTransport`, `DotEnvLoader`, GeminiAIProvider model/key constants, CommanderConversationMessage; never wrap the tactical translator.
- [x] Build strategic system prompt, bounded history beginning with user, temperature zero, JSON MIME output. Keys go only in authentication headers.
- [x] Factory chooses mock or Gemini by configuration; accepts no execution services.

## Task 3: Verification and handoff

- [x] Focused `run_tests(mode: EditMode, test_names: [OpenEmpires.Tests.CommanderPhase4B1Tests])`, poll same job.
- [x] Request independent code review and resolve actionable findings.
- [x] Full EditMode and PlayMode runs, compiler check, original Phase 3 manifest and all-existing-script baseline comparison, secret scan.
- [x] Write `Docs/CommanderPhase4B1.md`, `Docs/CommanderPhase4B1-results.json` and frozen source evidence; include both requested demonstrations and all files.
- [x] Audit every user requirement before readiness decision and goal completion.
