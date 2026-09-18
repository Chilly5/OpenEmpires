# Commander Phase 4A — LLM Intent Adapter and Local Chat

Verified 2026-09-11 in Unity 6000.5.9f1.

## Outcome

Phase 4A is implemented without modifying the frozen Phase 3 execution architecture. Natural-language input is translated to a tactical `CommanderIntentDTO`, revalidated at the trusted boundary, and submitted to the existing dispatcher. Providers cannot access the simulation, command buffer, networking, planner, or goal manager.

The implementation, deterministic/runtime tests, and a credentialed live Gemini smoke test are complete. The live provider translated `make 2 spearmen` into the expected tactical DTO and, before dispatch, created zero goals and enqueued zero commands. Phase 4A is **READY FOR PHASE 4B**.

## Architecture

```text
CommanderChatUI (local uGUI/TMP surface)
    -> CommanderAIIntentAdapter
        -> ICommanderAIProvider
            -> MockAIProvider OR GeminiAIProvider
        -> JSON cleanup and strict tactical schema parsing
        -> CommanderIntentDtoCodec.ValidateAndConvert
        -> CommanderIntentDispatcher.SubmitIntent
        -> existing tactical resolver / goal manager / planner
        -> existing CommandBuffer
        -> existing GameSimulation
```

- `ICommanderAIProvider` accepts only player text, a detached `CommanderContext`, and bounded conversation messages. It returns data (`CommanderAIProviderResult`) only.
- `MockAIProvider` deterministically recognizes the six Phase 4A tactical forms: Spearmen, Archers, Knights, Barracks, Wood villagers, and Food villagers.
- `GeminiAIProvider` posts to the Gemini `v1beta/models/{model}:generateContent` endpoint. It tries `gemini-flash-lite`; HTTP 400/404 falls back to `gemini-flash-latest`. The key is sent only in the `x-goog-api-key` header.
- Model output is cleaned, limited in size/depth, required to be one strict double-quoted JSON object, rejected on duplicate/unknown fields, converted to the Phase 3 DTO, and validated. The adapter validates the DTO again immediately before dispatch.
- Only `Tactical` plus `EnsureUnitCount`, `BuildStructure`, and `SetResourceAllocation` are accepted. Strategic output is rejected.
- `CommanderConversationHistory` retains the last 12 messages by default and is capped at 40. There are no embeddings, persistence, or vector storage.
- Context includes the local player's resources, population, units, buildings, worker allocation, visible resource nodes, available unit capabilities, and active goals. It is projected from the detached Phase 3 fog-safe snapshot and contains no enemy-state fields.
- `CommanderChatUI` creates a separate local Screen Space Overlay canvas at runtime. It never calls the multiplayer `ChatManager`, `NetworkManager`, or any network message path.

Google's current reference confirms the [generateContent endpoint and request fields](https://ai.google.dev/api/generate-content), [header-based API authentication](https://ai.google.dev/api), and the [latest-model alias convention](https://ai.google.dev/gemini-api/docs/models).

## Files

Production source added under `Assets/Scripts/AI/Commander/Phase4A/`:

- `CommanderAIProvider.cs`
- `MockAIProvider.cs`
- `GeminiAIProvider.cs`
- `CommanderAIIntentAdapter.cs`
- `CommanderChatUI.cs`

Tests added:

- `Assets/Tests/EditMode/CommanderPhase4ATests.cs`
- `Assets/Tests/PlayMode/CommanderPhase4APlayModeTests.cs`

Evidence added:

- `Docs/CommanderPhase4A-focused-editmode-results.json`
- `Docs/CommanderPhase4A-focused-playmode-results.json`
- `Docs/CommanderPhase4A-live-gemini-results.json`
- `Docs/CommanderPhase4A-regression-results.json`

Unity generated the corresponding `.meta` files and the `Phase4A` folder metadata. No Phase 3, simulation, command-buffer, or networking file was edited for Phase 4A. All new Phase 4A files are currently untracked in the existing dirty worktree; no commit was created.

## API key

Current state:

- The credential is configured locally at `D:\unity_projects\OpenEmpires\Open Empires\.env` as `OPENEMPIRES_GEMINI_KEY`.
- The existing `DotEnvLoader` successfully loaded that file after Unity was restarted.
- The credential is not set in the current process, user, or machine environment; the project-local `.env` is the active source.
- `.env` is ignored by the repository's parent `.gitignore` rule at line 47 and has no Git status entry. The key is not present in tracked files or evidence.
- `GEMINI_API_KEY` is intentionally not consumed by Phase 4A.

To replace the credential, edit this local file:

```text
D:\unity_projects\OpenEmpires\Open Empires\.env
```

with:

```text
OPENEMPIRES_GEMINI_KEY=replace-with-your-key
```

Replace only the value after `OPENEMPIRES_GEMINI_KEY=`, then restart Unity because the existing `DotEnvLoader` loads local values lazily and the Editor inherits environment variables at process launch. Alternatively, remove the file and set `OPENEMPIRES_GEMINI_KEY` in the process/user environment. Never add the `.env` file to Git.

Without a key, `CommanderAIProviderFactory` selects `MockAIProvider`, so the project and Commander chat remain functional and deterministic. `OPENEMPIRES_COMMANDER_PROVIDER=mock` can force that mode even when a key exists.

## Verification

- Credentialed live Gemini smoke test: **1/1 passed**, job `d00ea83f97cf4f5087cf6e9aeea16210`.
- Required Phase 4A EditMode fixture: **10/10 passed**, job `ba2e592a8d624c7ea70dd193e85e8308`.
- Phase 4A runtime PlayMode fixture: **2/2 passed**, job `56757446b334486f90df776568106907`.
- Full EditMode suite: **358/358 passed**, job `22a7b6005dc54401945b7988cf784be2`.
- Full PlayMode suite: **49/49 passed**, job `19188772727445b6a42eedc00e9c4d10`.
- C# compiler errors: **0**.
- Frozen Phase 3 source manifest: **10/10 SHA-256 matches**.
- Networking changes: **0**.
- Simulation/command-buffer changes: **0**.
- Determinism changes: **0**; execution remains in the existing ticked pipeline and both runtime tests emitted the normal sync-check stream.

The final focused EditMode run includes all required exact test names. Its deterministic Gemini test verifies cleanup/parsing and the requested 404 fallback without a network call. A temporary credentialed smoke fixture then exercised the production provider against the real service; it was removed afterward so routine tests remain keyless. The full suites passed after that removal and final compilation.

## Runtime evidence

```text
Player: make 10 spearmen
Mock JSON: {"intentCategory":"Tactical","intentType":"EnsureUnitCount","parameters":{"unit":"Spearman","count":10}}
Existing execution: EnsureUnitCountGoal -> CommanderPlanner -> CommandBuffer -> GameSimulation
Result: goal Completed, owned 10/10 living Spearmen.
```

```text
Player: put 8 villagers on wood
Mock JSON: {"intentCategory":"Tactical","intentType":"SetResourceAllocation","parameters":{"resource":"Wood","count":8}}
Existing execution: ResourceAllocationGoal -> CommanderPlanner -> CommandBuffer -> GameSimulation
Result: goal Completed, 8/8 villagers assigned to Wood.
```

## Final gate

**READY FOR PHASE 4B**

All implementation, boundary-safety, deterministic, runtime, live-provider, compiler, regression, and frozen-Phase-3 gates pass. The local secret remains ignored and untracked.
