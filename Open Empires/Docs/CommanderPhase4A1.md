# OpenEmpires AI Commander Phase 4A.1 Hardening Report

Date: 2026-09-12

## Outcome

Phase 4A.1 production hardening is implemented and verified. The change remains a reliability layer around the existing tactical AI-provider boundary. Phase 4B was not started, strategic LLM intents were not added, and the Phase 3 planning, networking, simulation, command-buffer, authority, and execution paths were not changed.

The runtime path remains:

```text
Player
 |
CommanderChatUI
 |
AI Provider
 |
Validated Tactical DTO
 |
Existing Commander Pipeline
 |
Simulation
```

## Implementation

- `CommanderAIIntentAdapter.SubmitAsync` now creates a linked request cancellation source and applies a 15-second default provider timeout. Timeout cancellation is limited to the AI request. Caller and UI-lifecycle cancellation remain distinct and retain their existing cancellation response.
- A timed-out request returns `Commander AI request timed out. Please try again or use offline commands.`, resets submission state, and allows `CommanderChatUI` to re-enable its controls.
- `GeminiAIProvider.TranslateAsync` maps HTTP 429, 401/403, and 5xx responses to the required safe messages. Transport exceptions also return a stable safe message without exposing exception details, response bodies, headers, or credentials.
- Gemini request serialization removes leading non-player history entries before emitting `contents`. Odd history capacities remain supported without exceeding the existing history capacity.
- Hostile natural-language and multiple-payload regressions prove that provider text cannot bypass tactical DTO validation or gain simulation authority.

## Changed files

- `Assets/Scripts/AI/Commander/Phase4A/CommanderAIIntentAdapter.cs`
- `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`
- `Assets/Scripts/AI/Commander/Phase4A/GeminiAIProvider.cs`
- `Assets/Tests/EditMode/CommanderPhase4A1HardeningTests.cs`
- `Assets/Tests/EditMode/CommanderPhase4A1HardeningTests.cs.meta`
- `Docs/CommanderPhase4A1.md`
- `Docs/CommanderPhase4A1-results.json`

The Phase 4A directory and earlier Commander documentation were already untracked in the inherited worktree. This report identifies only files created or modified for Phase 4A.1; unrelated user changes were preserved.

## New tests

- `SlowProvider_IsCancelledWithoutStoppingSimulation`
- `CallerCancellation_RemainsDistinctFromRequestTimeout`
- `TimedOutSubmission_UnlocksCommanderChatUI`
- `OnDestroy_CancelsLifecycleWithoutReportingTimeout`
- `GeminiHttpFailure_ReturnsSafeStatusSpecificMessage` (429, 401, 403, 500, and 503 cases)
- `GeminiTransportFailure_DoesNotExposeInternalException`
- `OddConversationCapacity_SerializesFromFirstUserTurn`
- `HostileNaturalLanguage_IsRejectedWithoutGameAuthority` (prompt injection, resource cheating, direct simulation injection, and API-secret extraction cases)
- `MultipleJsonPayloads_AreRejectedWithoutGameAuthority`

## Verification

Verification was rerun after the Unity MCP update and the results below are from Unity MCP 10.2.0.

| Gate | Result | Job / evidence |
| --- | --- | --- |
| Phase 4A.1 focused EditMode | 16 passed, 0 failed, 0 skipped | `bc0c0754b807400baf3d02ba14199d55` |
| Full EditMode | 374 passed, 0 failed, 0 skipped | `f1f2c450bb424630a37d41c5ad1af922` |
| Full PlayMode | 49 passed, 0 failed, 0 skipped | `f2c6140c83aa4b8087598ef6c872860a` |
| Fresh script compilation | 0 C# errors | Unity console query returned 0 `error CS` entries |
| Phase 3 frozen-source hashes | 10 of 10 match | Recomputed SHA-256 against `CommanderPhase3Fix13-source-hashes.json` |
| Direct authority scan in Phase 4A | 0 files | No direct CommandBuffer, networking, chat, or game-command references |
| Strategic support scan in Phase 4A | 0 files | No StrategicPipeline, StrategicPlanner, or EvaluatePlayerIntentNow references |
| Tracked Gemini-key-shape scan | 0 matches | Git tracked-file scan |

Detailed machine-readable results are recorded in `Docs/CommanderPhase4A1-results.json`.

## Architecture and secret protection

- All 10 Phase 3 frozen-file hashes still match the Phase 3 Fix 1.3 manifest.
- No networking, simulation, CommandBuffer, planner-authority, or execution-flow file was modified for Phase 4A.1.
- The Phase 4A adapter continues to accept only validated tactical DTOs and explicitly rejects strategic interpretations.
- No API key is present in tracked files. The local `.env` file is untracked and ignored by `.gitignore` line 47.

Phase 4A.1 passes its hardening gates. Phase 4B remains intentionally out of scope.
