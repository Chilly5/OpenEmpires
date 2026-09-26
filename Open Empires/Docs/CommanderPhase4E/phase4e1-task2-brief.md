# Task brief: detached Luna semantic translation

Read with the approved design, Phase 4E.1 plan Global Constraints, and `phase4e1-provider-audit.md`. This file is Task 2 only.

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
