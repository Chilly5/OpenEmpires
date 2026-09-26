# Commander Phase 4E.1 Language Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admit ordinary-language single Commander requests through a bounded semantic provider response while preserving tactical game authority and strategic approval.

**Architecture:** A separate strict semantic JSON contract translates model text to data-only request nodes. The chat host uses it when its provider implements the new interface, while legacy/mock providers retain their current route during migration. Trusted game-side code validates and dispatches tactical nodes or stages strategic nodes in the existing approval bridge; later 4E plans extend the same contract with selectors, graph edges, and ReachAge.

**Tech Stack:** Unity 6000.5.9f1, C#, Newtonsoft.Json, NUnit EditMode/PlayMode, Unity MCP test runner.

**Spec:** `Docs/superpowers/specs/2026-09-26-commander-phase4e-design.md`, implementing user brief sections 8–9, 33–34, 36–38, 44, 48 and the 4E.1 portion of `Docs/CommanderPhase4E/phase4e-specification.md`.

## Global Constraints

- Model output is semantic data only; it never supplies trusted player/source, concrete entity IDs, coordinates, workers, commands, goals, plans, reservations, or approval.
- A model strategic objective is an AI recommendation requiring the existing Approve/Confirm path, never `StrategicIntent.FromPlayerInterpretation`.
- Existing offline lifecycle, explanation, memory controls, player ownership and stale-generation checks retain precedence.
- Raw model response <=8192 characters; JSON depth <=8; player message <=1024 characters; provider context <=8192 characters; no unknown properties, duplicate keys, comments, trailing JSON or numeric enum tricks.
- This slice admits only one node; the contract remains able to represent up to four nodes for later 4E.3 but a multi-node response is explicitly rejected by the 4E.1 host without partial admission.
- No live API call is used by deterministic automated tests. No key or secret enters source, tests, docs or tool output.
- Keep one production writer over the chat/provider/bridge files and one Unity test runner; record exact focused test IDs and terminal results before proceeding to 4E.2.
- Preserve existing Phase 4D full-suite baseline evidence: 703 EditMode and 159 PlayMode passed, zero failed/skipped.

## Review Focus

- A syntactically valid node with an unsupported unit/structure/strategy must reject before any goal or pending preview exists; Task 1 and Task 3 tests pin this.
- A provider response containing authority-like extra fields or a second JSON object must reject, not reinterpret; Task 1 tests pin this.
- An async provider result after host reset or owner/runtime replacement must not be admitted; Task 3 and Task 4 tests pin this.
- A strategic phrase must never enter generic tactical DTO's PlayerDirect conversion; Task 4 tests pin AIRecommendation provenance and pending approval.
- Provider timeout, HTTP failure or malformed content must leave controls usable and show safe bounded text; Task 2 and Task 3 tests pin this.

---

### Task 1: Strict single-node semantic contract

**Files:**
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs`
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs`
- Create: `Assets/Tests/EditMode/CommanderPhase4E1SemanticJsonTests.cs`

**Interfaces:**
- Produces `CommanderSemanticOutcome { Request, Clarify, Unsupported }`, `CommanderSemanticNodeType { EnsureUnitCount, BuildStructure, SetResourceAllocation, StrategicObjective }`, immutable `CommanderSemanticNode` with `Type : CommanderSemanticNodeType`, `UnitType : int?`, `BuildingType : BuildingType?`, `ResourceType : ResourceType?`, `Count : int?`, `StrategicObjectiveType : StrategicObjectiveType?`, and immutable `CommanderSemanticResult` with `IsValid : bool`, `Outcome`, `Nodes`, `SafeExplanation`. Invalid JSON is a safe rejected result (`IsValid=false`), not a fourth semantic outcome; only trusted parser code can set that flag.
- Produces `CommanderSemanticJson.Parse(string raw) : CommanderSemanticResult`. Parsed enums and numbers are data, not authority; semantic validation against current `CommanderContext` occurs at admission.
- Version-1 JSON shape: `{"outcome":"Request","nodes":[{"type":"EnsureUnitCount","unit":"Spearman","count":10}]}`; `BuildStructure` uses `structure` and `count`; `SetResourceAllocation` uses `resource` and `count`; `StrategicObjective` uses `objective`. `Clarify` and `Unsupported` have no nodes and optional `message` of at most 180 plain-text characters. No other fields are accepted.

- [ ] **Step 1: Write failing NUnit tests** for a valid Spearman node, a valid `RangedReinforcement` strategic node, Clarify, Unsupported, and rejection of duplicate/trailing/unknown/authority fields, comments, NaN, negative or >200 unit count, >20 structure count, unsupported enum, >4 nodes, and wrong fields for node type. Assert the returned typed result or `IsValid=false`, never a partially parsed request. Resource-worker count is 0–200; actual context population is checked again at admission.
- [ ] **Step 2: Run** `CommanderPhase4E1SemanticJsonTests` in Unity EditMode. **Expected:** compile/test RED because the new contract/parser is absent.
- [ ] **Step 3: Implement** the produced types and `Parse` signature. Use Json.NET's duplicate-property rejection and an explicit syntax/field/type allowlist; exact case-sensitive enum strings, no `Enum.TryParse` numeric acceptance. Cap collections before allocating retained nodes. Do not keep `JObject` or provider JSON in the result.
- [ ] **Step 4: Run** `CommanderPhase4E1SemanticJsonTests` in Unity EditMode. **Expected:** all new tests pass, zero skipped.
- [ ] **Step 5: Commit** only the Task 1 source/tests/meta files as `feat: add bounded Commander semantic contract`.

### Task 2: Detached Luna semantic translation

**Files:**
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs`
- Modify: `Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs`
- Create: `Assets/Tests/EditMode/CommanderPhase4E1ProviderTests.cs`

**Interfaces:**
- Consumes Task 1 `CommanderSemanticJson.Parse`.
- Produces `CommanderSemanticProviderRequest(string playerMessage, CommanderContext context)` whose serialized provider context exposes capability names, owned-type counts, age and population only, with no owner/player ID, entity ID, coordinate, enemy state or raw `GameSimulation`.
- Produces `ICommanderSemanticProvider.TranslateSemanticAsync(CommanderSemanticProviderRequest request, CancellationToken token) : Task<CommanderSemanticResult>`; `OpenRouterCommanderProvider` implements it using its existing transport, endpoint, model, external key, timeout and safe HTTP-error handling. The provider prompt states the exact Task 1 schema, supported *currently executable* single-node types, and the three semantic outcomes; it must not ask the model to choose game IDs/positions/commands.

- [ ] **Step 1: Write failing NUnit tests** with fake HTTP transport for a natural paraphrase returning the same typed unit request, detached context excluding IDs/coordinates/enemy data, omitted key, 429, timeout, malformed JSON and oversized output. Assert the outgoing body selects `openai/gpt-6-luna` and no secret is echoed.
- [ ] **Step 2: Run** `CommanderPhase4E1ProviderTests` in Unity EditMode. **Expected:** RED because the interface/method is absent.
- [ ] **Step 3: Implement** the request/interface, safe context serializer and OpenRouter semantic method. Keep legacy tactical/strategic methods intact. Limit player text/context before the network call, and parse/reject on return; do not retry or leak response bodies in errors.
- [ ] **Step 4: Run** `CommanderPhase4E1ProviderTests` in Unity EditMode. **Expected:** all new tests pass, zero skipped.
- [ ] **Step 5: Commit** only Task 2 source/tests/meta files as `feat: translate bounded semantic Commander requests`.

### Task 3: Tactical host admission without exact phrase gate

**Files:**
- Modify: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticAdmission.cs`
- Create: `Assets/Tests/EditMode/CommanderPhase4E1ChatTests.cs`

**Interfaces:**
- Consumes `ICommanderSemanticProvider` when the initialized provider implements it; otherwise preserves legacy route for existing mocks/tests until the final migration gate.
- Produces `CommanderSemanticAdmission.TryCreateTacticalIntent(CommanderSemanticNode node, CommanderContext context, out CommanderIntent intent, out string safeReason) : bool`. It constructs a **tactical-only** `CommanderIntentDTO` from typed fields and calls `CommanderIntentDtoCodec.ValidateAndConvert(dto, context)`; it requires a non-null tactical `Intent` and null `StrategicIntent`. The ordinary dispatcher remains the executor; no model-supplied owner/provenance survives.

- [ ] **Step 1: Write failing host tests** using a fake semantic provider for seven Spearman paraphrases, structure/resource nodes, unsupported unit, ambiguous/unsupported outcome, malformed response, busy state, stale reset, and no-goal admission for a two-node response. Check offline pause/status still bypasses provider and UI controls recover after provider failure.
- [ ] **Step 2: Run** `CommanderPhase4E1ChatTests` in Unity EditMode. **Expected:** RED because the host still exact-classifies and rejects paraphrases.
- [ ] **Step 3: Implement** the admission method and semantic host branch after offline controls; cap player text, capture trusted generation/owner/context before await, recheck host/simulation/owner/generation after await. Preserve existing adapter route for non-semantic providers. Do not route `StrategicObjective` through tactical DTO.
- [ ] **Step 4: Run** `CommanderPhase4E1ChatTests` and existing `CommanderPhase4A1HardeningTests` in Unity EditMode. **Expected:** all pass, zero skipped.
- [ ] **Step 5: Commit** only Task 3 source/tests/meta files as `feat: admit free-form tactical Commander goals`.

### Task 4: Guarded strategic recommendation staging

**Files:**
- Modify: `Assets/Scripts/AI/Commander/Phase4B2/StrategicAIApprovalBridge.cs`
- Modify: `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`
- Create: `Assets/Tests/EditMode/CommanderPhase4E1StrategicTests.cs`
- Create: `Assets/Tests/PlayMode/CommanderPhase4E1StrategicPlayModeTests.cs`

**Interfaces:**
- Consumes Task 1 `StrategicObjective` node and Task 2 provider; produces a read-only `StrategicAIApprovalBridge.Generation : int` and guarded `StrategicAIApprovalBridge.StageValidatedSemanticObjective(StrategicObjectiveType objective, int expectedOwner, int expectedGeneration) : StrategicAIProviderResult`. The host captures bridge generation after clearing prior pending state and before awaiting the semantic provider; the bridge compares it under lock before staging.
- The bridge owns new intent ID, fresh context and `AIRecommendation` source, validates via existing policy, sets pending only if no in-flight/pending conflict and owner/generation remain valid. The host uses the existing strategic preview/adaptation/Approve/Confirm path with no direct `StrategicPipeline` execution.

- [ ] **Step 1: Write failing EditMode/PlayMode tests** for a strategic paraphrase producing a pending recommendation, zero plan before approval, ordinary Approve/Confirm policy, wrong-owner/stale/busy rejection, and proof that generic PlayerDirect conversion is not called.
- [ ] **Step 2: Run** both focused test classes. **Expected:** RED because bridge staging API is absent.
- [ ] **Step 3: Implement** guarded staging and host route using existing source-plan capture/preview controls; share the current host's post-await owner/generation checks. Keep old strategic translation method for legacy tests and existing providers.
- [ ] **Step 4: Run** both focused classes plus `CommanderPhase4B1Tests` and `CommanderPhase4D1PlayModeTests`. **Expected:** all pass, zero skipped.
- [ ] **Step 5: Commit** only Task 4 source/tests/meta files as `feat: stage semantic strategy through approval bridge`.

### Task 5: Phase 4E.1 integration gate and evidence

**Files:**
- Create: `Docs/CommanderPhase4E/phase4e1-language-boundary-gate.md`
- Modify: `Docs/CommanderPhase4E/requirements-matrix.md`

**Interfaces:**
- Consumes all preceding tests and preserves full Phase 4D behavior.
- Produces exact focused test IDs/counts, full relevant Unity run IDs/counts, compile/console status, source scope review, and known deferred 4E.2+ capabilities. It makes no whole-4E completion claim.

- [ ] **Step 1: Run** all Phase 4E.1 focused EditMode and PlayMode classes once after source stabilizes. **Expected:** zero failed/skipped and exact job IDs captured.
- [ ] **Step 2: Run** relevant Phase 4A/B/D regressions and inspect Unity console compilation errors. **Expected:** zero failures and no compiler errors.
- [ ] **Step 3: Review** changed source for forbidden provider IDs/coordinates/commands/provenance, stale guards and source scope; write the evidence gate with exact results and remaining 4E.2–4E.8 work.
- [ ] **Step 4: Commit** only Task 5 evidence/docs as `docs: record Phase 4E.1 language gate`.
