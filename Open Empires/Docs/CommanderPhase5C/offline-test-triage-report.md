# Legacy offline-test transport triage

Read-only source triage for Task7 preparation (2026-10-11). This is a candidate
classification, **not** the frozen full-name NUnit discovery list. No tests,
builds, MCP, network, credentials, or runtime were invoked.

## Offline-safe candidates

- `CommanderPhase5AProviderContractTests` — `Assets/Tests/EditMode/CommanderPhase5AProviderContractTests.cs:12,172,209`; `Translate` passes the test `SequenceTransport` into `OpenRouterCommanderProvider`, whose helper only returns queued fixture replies. Provider construction alone does not enable dispatch to production HTTP.
- `CommanderPhase5BFinalIntentContractTests` — `Assets/Tests/EditMode/CommanderPhase5BFinalIntentContractTests.cs:14,48,119`; test-created provider receives the class-local `Transport` double, not `CommanderHttpClientTransport`.
- `CommanderEconomyClarificationTests` provider/hint-gate parts — `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Provider.cs:13,24`; `...HintGate.cs:376,391-403`; `EconomyTransport`/`HintTransport` return in-memory responses and are explicitly injected into the provider. Other partial-class fixtures use sequence/mock semantic providers (e.g. `...Chat.cs:156-169`).
- `CommanderGrandFixProviderSetupTests` — `Assets/Tests/EditMode/CommanderGrandFixProviderSetupTests.cs:13,149-176`; all routes use local `HttpFixture`/`GatewayFixture` doubles. Despite simulated network-failure cases and an `.invalid` endpoint, no real transport is constructed or dispatched.
- `CommanderGrandFixOnlineVoiceTests` — `Assets/Tests/EditMode/CommanderGrandFixOnlineVoiceTests.cs:11,19-20,88-108`; synthetic consent/policy/session and WAV encoding go through local `FixtureTransport`. Its “upload” is an in-memory fixture call, not audio hardware or HTTP.
- `CommanderPhase4HVoicePlayModeTests` — `Assets/Tests/PlayMode/CommanderPhase4HVoicePlayModeTests.cs:13,60-64,250-269`; uses `MockCommanderSpeechToTextProvider`, `PlayModeTestAudioCapture`, and `SemanticMockProvider`; no microphone/native audio provider.
- `CommanderGrandFixEvidenceLaneTests` — `Assets/Tests/EditMode/CommanderGrandFixEvidenceLaneTests.cs:14-28,92-104,220-228`; real filesystem operations are limited to uniquely named temp fixtures guarded and deleted in teardown; transport is a local fake even where URI is `example.invalid`. Safe transport-wise, but retain only if Task7 accepts this bounded temp-file fixture behavior.

## Exclude from the offline regression candidate set

- `CommanderPhase5ALiveRuntimePlayModeTests` — entire fixture, `Assets/Tests/PlayMode/CommanderPhase5ALiveRuntimePlayModeTests.cs:15-19,41-91`; paid lane with opt-in/budget checks, constructs `CommanderHttpClientTransport`, then calls through the budgeted wrapper. Setup also writes scenario evidence. Do not rely on opt-in guards as the exclusion boundary.
- `CommanderPhase5BFinalLivePlayModeTests` — entire fixture, `Assets/Tests/PlayMode/CommanderPhase5BFinalLivePlayModeTests.cs:17,30-48,75`; explicit paid Editor opt-in, configured provider plus production HTTP transport; tests persist evidence under Docs. Exclude regardless of whether credentials/configuration are present.
- `CommanderGrandFixHttpTests` — `Assets/Tests/EditMode/CommanderGrandFixHttpTests.cs:16-18,86-96,109-180`; real production `CommanderHttpClientTransport` sends HTTP to a locally opened TCP loopback listener. No vendor/paid request, but it is still actual HTTP/socket execution, so exclude under the no-real-HTTP constraint.
- `CommanderPhase4HAudioFixtureTests` — entire fixture, `Assets/Tests/EditMode/CommanderPhase4HAudioFixtureTests.cs:11-15,28-36,63-140`; reads WAV files and initializes/transcribes with native `WhisperCommanderSpeechToTextProvider`, with native release in teardown. Not microphone/cloud, but it is real audio/native inference and should not enter the offline regression.

## Uncertainty / handoff

- `Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs` and any concurrent/new Phase 5C fixtures were deliberately not inspected per Task4 ownership and the brief. Task7 must revalidate all new/changed source after freeze.
- No full NUnit discovery was performed; this report does not certify any test filter or the complete test universe. Task7 must freeze exact full names after source freeze and verify setup/helper paths again.

## Parent scope clarification

The triage itself forbade executing any HTTP. That does not impose a blanket ban
on loopback-only fixtures in the later final offline regression. Task7 must inspect
`CommanderGrandFixHttpTests` for a safe asynchronous harness and exclusively local
routing, then include it if applicable or document the actual reason for excluding
it. Paid/external-provider and native-audio exclusions remain distinct. This report
is not authorization to run anything now.
