# Targeted economy/clarification verification

Date: 2026-10-06. Unity 6000.5.9f1, instance Open Empires@6d7310c7, active project D:/unity_projects/OpenEmpires/Open Empires. These records concern the dirty integrated checkout, not an independently clean checkout of HEAD.

## Final-source focused and relevant checks

| Scope | Job / artifact | Result |
| --- | --- | --- |
| 15 economy/clarification EditMode methods after review fixes | 04861f19c80b4a4f8c06fd3c8e13098c / focused-editmode-final-green XML | 15/15 passed, 12.6726179 s |
| Relevant existing Commander classes | 0b2cd7fc4a834dffb099180fb294f7c5 / relevant-commander-green XML | 204/204 passed, 28.38498 s |
| Three new controlled PlayMode scenarios (before two final review fixes) | 72342bb4cce24dcbafb53bd899e8dc26 / focused-playmode-green XML | 3/3 passed, 1.2277716 s; final full PlayMode will verify changed snapshot |
| Narrow delayed-runtime/disposal and Mill/production/Q&A regression | 0b8b8d6c4ced416bb826c7e0f48491d9 / narrow-disposal-placement-green XML | 2/2 passed, 10.9831857 s |
| Expanded worker/quantity/source/capacity vectors | 8c2d5362e3c84bb68d255d634c3604a7 / expanded-worker-green XML | 4/4 passed, 20.6261431 s |

The relevant existing classes were confirmed loaded before execution: CommanderPhase1Tests, CommanderPhase3ATests, CommanderIntentParserTests, CommanderIntentValidationTests, CommanderIntentResolverTests, CommanderIntentIntegrationTests, CommanderResponseGeneratorTests, CommanderPhase4E1SemanticJsonTests, CommanderPhase4E1ChatTests, CommanderPhase4E1ProviderTests, CommanderPhase4E3GraphAdmissionTests, CommanderPhase4GCapabilityTests.

Focused cases cover strict schema/DTO admission, all four quantity meanings, ownership/current-resource/queued/garrisoned/protected/other-goal exclusions, empty/>200 all-matching sets, exact shortages, unavailable/depleted/hidden/unreachable/wrong-source rejection, complete worker reservation before first ordinary command, actual four-Farm native processing, sticky human takeover beyond protection lease, native source restriction through depletion/queue/slaughter/automatic redirect, checksum/updated-peer equivalence, legacy bytes and malformed restricted serialization. Chat cases use actual SubmitMessageAsync for simple count, multi-field continuation, cancellation, ambiguous bounded replies, independent production, malformed/contradictory drafts, unsolicited source loss/change, explicit Idle correction and delayed results across reset/Initialize/match replacement/disposal.

The three PlayMode cases use a controlled simulation and semantic test provider, not Luna: ordinary gather for Food; ordinary owned-sheep slaughter then carcass, competing berries and depletion/human Stop; and real chat missing-count question followed by local 4 without a production goal. One finalized/mock voice transcript uses the unchanged shared text submission path. Fixture setup is not proof of naturally generated-map availability or native UI behavior.

## Live Luna, five paid requests and no retry loop

`live-luna-economy-2026-10-06.json` retains safe transcripts, typed allocations, HTTP/finish traces, normal command types and actual game-side selected IDs/current targets. Every request returned HTTP200, finish stop. Actual billed tokens/cost were not retained and are unknown.

| Request | Typed result and actual assignment |
| --- | --- |
| put four idle villagers on food | SelectedCount Exact4 Idle Food/Any; workers 1,2,3,4, normal SlaughterSheep selected a legitimate closer owned source |
| gather food from sheep with four idle villagers | SelectedCount Exact4 Idle Food/Sheep; same four workers, restricted ordinary slaughter |
| move three villagers from wood to gold | SelectedCount Exact3 Gathering/currentResource Wood Gold/Any; workers 0,1,2, ordinary Gather to node2 |
| gather food with four idle villagers | Exact4/Idle retained; no duplicate numeric question |
| gather food → four | Typed Food/Any draft, then local Exact4 completion; provider trace unchanged, zero production goals |

This live probe precedes the final two narrow review changes. It establishes provider extraction, actual public chat admission and native assignment state; it does not prove resource deposits, standalone builds, multiplayer sessions between two editors, or UI interaction. Later focused/full regression verifies the changed native redirect and continuation guards. No credentials occur in these artifacts.

## Red/green provenance and launcher anomalies

Task1 valid red e5c0845b6415404382f4b6c750037168: 2 failures; green 6937f2ec288446b2b8f479eaf9534700: 3 passes including legacy zero allocation. Task3 exact red 25b97238cf9d4ff980c57e333fef532a: 5 unsupported execution failures; green a1d479650d0144fd8d85d7b4cab3311e: 7 passes. Task2 valid red 1374c4690cc44439a5c44355ab48aa97: typed source missing and actual Sheep-to-Berries depletion, followed by the expanded 13/13 green b93cefec44b44d399dd40f157c9a6c38. Task4 saved red b4ec3009b6c44f4ca885a89d9d7f2f55: 4 pending failures; then the same 13/13 green. Task5 red 8ffe1afa09eb45a5b5ef31e4a364a45d: missing economy provider projection and worked-resource Mill forwarding omission, repaired in subsequent focused checks.

Review regressions: f855752db04f47548e8207930c156856 proved automatic redirect dropped Sheep restriction; 81a19daa870c4036ba11c9b5d71d1f4a proved explicit Idle correction was rejected. Both pass in final 15/15 run. The disposal fixture initially used SendMessage, which produces ShouldRunBehaviour assertions for an EditMode never-Awake host; it now invokes the real idempotent disposal callback directly.

Several MCP jobs lost callback state across reload despite actual saved XML runs. Their XMLs and exact distinctions are preserved in execution-progress.md; zero-test or initialization-only jobs are not passing evidence. The final focused/relevant rows above have terminal MCP counts, not inferred success from launch messages. Package Manager OAuth errors observed after editor restart are unrelated environment logs, not compiler errors or evidence of provider failure.

## Single final shared regression

Full EditMode job d057b3e7b584423d9ccbee5ec166d8a7 ran exactly once: 998/998 passed, zero skips, 204.4193745 s. Full PlayMode job f05d455496f54e44ab79141d10d01bcb ran exactly once: 193/194 passed, zero skips, 102.5177777 s. Full XMLs are retained unchanged.

The sole full PlayMode failure was CommanderPhase4D1HostPlayModeTests.AppliedLifecycleMutation_InvalidatesInFlightInterpretation, reproduced alone RED in d3f8e8cee7b54e858762ce4916fca4a0. A premature chat busy guard prevented an existing whole-form pause from invalidating held interpretation. The current chat file was already dirty at baseline; the fix does not assume original fault attribution. A narrow preservation change now lets existing strategic controls run while busy if no worker draft is pending, without starting another provider request.

Both full runs predate that final one-branch chat fix and are historical full-snapshot results, not a claim that the entire final checkout received a second full run. Per the user's once-only policy, affected host/economy/result-binding/voice PlayMode checks are verified narrowly afterwards; independent finalization audit should run its own final-checkout regression. Do not summarize this package as “full PlayMode green.” Post-fix records below establish the repaired branch separately.

`post-fix-runtime-probes.json` additionally records actual normal-command AllMatching Idle/Berries completion for IDs0,1,2,3 and the protected-Wood floor case (request3 transfers from five workers, protected minimum3): Blocked, zero commands/reservations, all five retain Wood. These are controlled runtime probes, not another full suite or paid provider run.

Post-lifecycle affected PlayMode job `2ebbbf151b9342129211913496a5f987`: terminal 16/16 passed, zero skips, 4.4079114 s. This covers the entire existing Phase4D1 host class, all three new economy scenarios, prior exact-produced-Spearmen patrol scenario and existing voice-text scenario. Initial impact launch `11243795b0ab4dbf93f91bcdb5d47825` lost callbacks despite saved XML16/16; retained as launcher anomaly and confirmed by the fresh terminal job. Full suites were not repeated.
Post-lifecycle affected EditMode job `ef23e98d21024d5495859b7a81a9a96e`: terminal 38/38 passed, zero skips, 19.4614454 s; all 15 economy methods and the existing semantic chat class. Full suites were not repeated. No compiler errors remained; the last source change is the guarded existing lifecycle handling branch in CommanderChatUI.
