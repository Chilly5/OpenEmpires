# Commander Phase 4B.1 — Strategic natural language interpretation

Implementation and verification date: 2026-09-12. **READY FOR PHASE 4B.2.** Phase 4B.1 is complete as an interpretation-only layer.

| Verification | Result |
|---|---|
| Focused Phase 4B.1 | 60/60 passed, 0 failed/skipped |
| Full EditMode | 434/434 passed, 0 failed/skipped |
| Full PlayMode | 49/49 passed, 0 failed/skipped |
| Existing Phase 4A tactical tests | 10 EditMode + 2 PlayMode passed |
| Existing Phase 4A.1 hardening | 16/16 passed |
| Final compiler check | 0 C# errors; refreshed editor idle after domain reload |
| Frozen Phase 3 manifest | 10/10 hashes unchanged |
| All pre-existing production scripts | 197/197 hashes unchanged, including Phase 4A |
| Tracked/new-deliverable key-pattern scans | 0 matches; .env ignored and untracked |
| Independent review | 0 Critical/Important findings; Minor test gap resolved |

Unity 6000.5.9f1, Unity MCP 10.2.0. Full EditMode job `e72e8d1b38ec44d1b4d177ac5f298793` took 382.6886069 seconds; PlayMode job `35dc90f7e33446e5867ddca3a719973a` took 25.9262059 seconds. The earlier interrupted full run is not counted: after the user reloaded Unity, its handle was confirmed missing and a fresh run completed successfully. See the detailed JSON artifacts and final summary for evidence.

## Interpreter and validation boundary

`IStrategicAIInterpreter` is separate from the tactical `ICommanderAIProvider`. Its only input is a `StrategicAIRequest`: player text, the existing Phase 3C detached `StrategicContext`, a caller-assigned intent ID, and bounded copies of existing conversation messages. Identity and creation tick are taken from trusted request data, never from model JSON.

Both `MockStrategicAIProvider` and `GeminiStrategicAIProvider` produce JSON and use the same `StrategicAIJson.Parse` boundary:

```text
Player text + existing StrategicContext + conversation
    -> mock or Gemini JSON
    -> whitespace / exact markdown fence cleanup
    -> strict dedicated StrategicIntentDTO parsing
    -> objective and parameter allowlist
    -> existing StrategicIntentValidator + private default registry
    -> validated StrategicIntent returned to the caller
```

Only AttackPreparation, DefensivePreparation, EconomicExpansion, and MilitaryReinforcement are accepted. Parameters may be omitted or empty; the only nonempty parameter form is `focus: cavalry` on AttackPreparation. Unknown objectives, tactical fields, command fields, ownership/priority fields, counts/resources/cheats, duplicate properties, multiple objects, malformed JSON and JavaScript extensions are rejected. Parse failures use fixed safe messages, with no echoed model data or parser exception details. DTO parameters and validation error collections are immutable.

The existing StrategicIntentValidator permits arbitrary names in its common parameter validation, so the new input boundary deliberately rejects extra parameters before invoking it. The frozen validator and templates are unchanged.

## Why the LLM cannot execute

Neither interpreter receives a simulation, planner, goal manager, dispatcher, command buffer, execution callback, or game-network object. There is no tool-call dispatch or polymorphic deserialization. Model text is data only. The existing default registry is used to find and validate a compatible template; its plan-creation method is never invoked, and the template is not exposed in the result. No provider registers the returned intent in planner history or submits it to a pipeline. Successful intents remain in `Created` status.

Phase 4B.1 does not connect UI or automatic strategic submission. A future phase must explicitly integrate through the existing strategic planner boundary.

## Context and provider behavior

The serializer projects the existing StrategicContext's food/wood/gold state, population, worker allocation, owned military, production capability, defensive building aggregates, current plan summaries and currently visible threats. It excludes resource-node coordinates, map data, free-text plan reasons, internal commands, game networking, and simulation objects. This is a serialization projection, not a second context builder. The frozen context offers production-building and defense aggregates, not a complete building inventory; those available aggregates are reused.

Gemini reuses Phase 4A HTTP transport, model constants, environment key loader, envelope extraction, and conversation types. Its separate system prompt specifies strategic JSON only and rejects unknown, tactical, mixed and malicious requests. The request has temperature 0 and JSON response MIME type. History is bounded and skips orphan model turns. HTTP 400/404 allows one existing-model fallback; authentication, quota and service failures are safe. A linked 15-second deadline distinguishes timeout from caller cancellation. API keys go only in the authentication header.

`StrategicAIInterpreterFactory.Create()` reuses `OPENEMPIRES_COMMANDER_PROVIDER` and the existing `OPENEMPIRES_GEMINI_KEY` loader. Explicit `Create("mock")` works offline. Consumers depend on the interface. OpenRouter was optional and is not implemented. No live API call is required or made by these tests: the actual Gemini provider is exercised using scripted HTTP responses.

## Demonstrations and usage

```csharp
// context is an existing StrategicContext snapshot, built by its existing owner.
IStrategicAIInterpreter interpreter = StrategicAIInterpreterFactory.Create("mock");
var request = new StrategicAIRequest("prepare cavalry attack", context, intentId: 41);
StrategicAIProviderResult result = await interpreter.InterpretStrategicIntentAsync(
    request, cancellationToken);
// Read result.Success, IntentDto, Intent, IntentJson, ExplanationText, ValidationErrors.
// No execution is performed.
```

Input: `prepare cavalry attack` (also `prepare a cavalry attack`).

```json
{"intentCategory":"Strategic","objectiveType":"AttackPreparation","parameters":{"focus":"cavalry"}}
```

Returns validated `AttackPreparation`, focus `cavalry`, status `Created`. Passing tests `StrategicText_CreatesAttackPreparationIntent`, `ShortExample_IsInterpreted`, and `StrategicProvider_CannotCreatePlans` assert **0 plans, 0 goals, 0 commands**, plus no planner intent registration or reservations.

Input: `expand economy`.

```json
{"intentCategory":"Strategic","objectiveType":"EconomicExpansion","parameters":{}}
```

Returns validated `EconomicExpansion`, status `Created`. Passing `ShortExample_IsInterpreted` and `StrategicProvider_CannotCreateCommands` assert **0 plans, 0 goals, 0 commands** and no execution.

## Tests and review

`CommanderPhase4B1Tests.cs` contains 60 passing cases. It covers every requested named test: the four `StrategicText_Creates...Intent` tests; `UnknownObjective_IsRejected`; `DirectCommandInjection_IsRejected`; `CheatParameter_IsRejected`; `TacticalIntent_IsRejected`; `StrategicProvider_CannotCreatePlans`; `StrategicProvider_CannotCreateCommands`; `StrategicContext_DoesNotExposeHiddenInformation`; and `SameInputProducesSameMockResult`.

Additional cases test exact short phrases, appended hostile instructions, immutable result parameters, malformed/duplicate/multiple JSON, strict field and value types, fences, real Gemini request serialization/fallback/parsing, status errors, missing candidates, odd history, transport exceptions, timeout and in-flight cancellation. Real simulation/planner fixtures enforce the zero-execution assertions. The visibility test changes hidden enemies, buildings, resources and enemy gold without changing serialized context, then reveals the living enemy and verifies that it appears in visible threats.

Independent review found no Critical or Important issues. Its Minor in-flight cancellation coverage finding was resolved. See `CommanderPhase4B1-review.md`.

The initial test failed because the provider was missing. During verification, the frozen EditMode assembly lacked a Newtonsoft reference, so tests use Unity JsonUtility helpers instead. The editor log caught the compiler error when MCP's console query returned zero; the test assembly was rebuilt before accepting results. Test fixture corrections initialized enemy health and accepted TaskCanceledException as an OperationCanceledException subtype. No production changes were required by these test corrections.

## Files

New production files under `Assets/Scripts/AI/Commander/Phase4B1/`:

- `StrategicAIInterpreter.cs`: request, interface, immutable DTO and result.
- `StrategicAIJson.cs`: cleanup, strict parsing, allowlist and existing validation.
- `MockStrategicAIProvider.cs`: deterministic offline translation.
- `StrategicAIContextSerializer.cs`: safe projection of existing context.
- `GeminiStrategicAIProvider.cs`: strategic prompt, HTTP translation and safe errors.
- `StrategicAIInterpreterFactory.cs`: provider selection.

Unity generated a `.meta` for each file and for the `Phase4B1` directory. New test: `Assets/Tests/EditMode/CommanderPhase4B1Tests.cs` and its `.meta`.

New documentation/evidence:

- `Docs/CommanderPhase4B1.md` (this report).
- `Docs/CommanderPhase4B1-results.json` (final verification summary).
- `Docs/CommanderPhase4B1-focused-results.json` (60-case detailed result).
- `Docs/CommanderPhase4B1-editmode-results.json` (full EditMode evidence).
- `Docs/CommanderPhase4B1-playmode-results.json` (full PlayMode evidence).
- `Docs/CommanderPhase4B1-frozen-source-hashes.json` (197-script pre-change SHA256 baseline).
- `Docs/CommanderPhase4B1-review.md` (independent review and resolution).
- `Docs/superpowers/specs/2026-09-12-commander-phase4b1-design.md`.
- `Docs/superpowers/plans/2026-09-12-commander-phase4b1.md`.

All existing production scripts and their prior uncommitted changes are preserved. No commit or push is included.
