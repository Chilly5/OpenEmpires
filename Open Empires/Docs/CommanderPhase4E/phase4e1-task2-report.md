# Phase 4E.1 Task 2 — detached Luna semantic translation

## Scope

- Added `ICommanderSemanticProvider`, `CommanderSemanticProviderRequest`, and a bounded allow-list context projection.
- Added `OpenRouterCommanderProvider.TranslateSemanticAsync` using the existing endpoint, Luna model, external key, fakeable transport, deadline, and safe HTTP status handling. Existing tactical and strategic entry points remain present and use their original request builders.
- Added EditMode fake-transport tests. No live provider call or real key was used.

## RED / GREEN

- First RED attempt, Unity job `b0c129d539534e7e8c920bea7f95a796`: 0 runnable tests due an unsupported direct Newtonsoft reference in the new test assembly (`Logs/Editor.log:612199-612206`). The fixture was corrected to use Unity `JsonUtility`; this attempt is **not** counted as the feature RED.
- Intended RED, Unity EditMode job `99ba29db7cdd413daec5ee684b495141`: 0 runnable tests because Task 2 types/method were absent. `Logs/Editor.log:612901-612919` records CS0246 for `CommanderSemanticProviderRequest` / `ICommanderSemanticProvider` and CS1061 for `TranslateSemanticAsync`. This is the expected missing-feature compile RED.
- GREEN, Unity EditMode job `81d5c14edf1241b3baf5f61fd9a9e504`: `OpenEmpires.Tests.CommanderPhase4E1ProviderTests`, 10 total, 10 passed, 0 failed, 0 skipped, 0 error-console entries, duration 0.578 s. Full payload: `phase4e1-task2-green-81d5c14e.json`. An earlier job `3b0a1c4cdfe54a5383738069792ebbbc` ran before import and returned 0/0; it is not a pass.
- Follow-up behavioral RED, Unity EditMode job `d674a41e038b4495b55d5b9fc11e3a28`: 10 completed, 9 passed, 1 failed, 0 skipped. `LongPlayerMessage_IsRejectedBeforeTransport` failed because the provider accepted and sent a silently truncated player instruction. This established the partial-instruction hazard before the fix.
- An immediate post-fix job `294a33e23f194e44bb7eeb6be601f8b0` still used the old compiled Unity DLL and failed the same one test; it is recorded as stale import, not counted as GREEN.
- Final GREEN after forced Unity refresh/rebuild, EditMode job `6da10480987e4b95907e00027e6d03ac`: `OpenEmpires.Tests.CommanderPhase4E1ProviderTests`, 10 total, 10 passed, 0 failed, 0 skipped, 0 error-console entries, duration 0.169 s. Full payload: `phase4e1-task2-final-green-6da10480.json`.

## Self-review

- The request immediately projects `CommanderContext` to fixed keys: age, population/cap, sorted aggregate owned unit/building counts, and fixed capability names. It retains no context object, player/owner ID, tick, entity ID, tile coordinate, enemy snapshot, goal, simulation, or raw resource node. Arbitrary building strings are allow-listed before serialization; aggregate counts are capped.
- Player text over 1024 characters is rejected before HTTP; the request retains no truncated prefix that could change intent. Serialized context is capped at 8192 characters, and model text goes through `CommanderSemanticJson.Parse` (8192-character strict parser). The prompt requests one Task 1 schema node, advertises executable `EnsureUnitCount` targets as 1–200, and expressly disallows IDs, coordinates, commands, plans, provenance and approvals. This remains advisory data only.
- The key is used only as an Authorization header. Missing key, 429, timeout, malformed semantic JSON and oversized model output return safe rejected results; response bodies and key are not copied into errors. The semantic path makes one HTTP attempt and no retry.
- The only shared production refactor is extracting the existing OpenRouter POST/extract tail into `PostAndExtractTextAsync`; the legacy tactical and strategic request conversion paths are unchanged.
- `git diff --check` passed for the modified tracked provider file. Focused GREEN is proven; full regression and live OpenRouter behavior are not claimed here and remain for the integration gate.
