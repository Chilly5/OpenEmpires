# OpenEmpires Commander Product Completeness Audit

Date: 2026-10-07  
Verdict: **READY FOR RELEASE FINALIZATION WORK**  
Phase 5B required: **NO**  
Phase 4E / 4F / 4G / 4H / 5A: **ACCEPTED / FROZEN**

## 1. Decision and release scope

Commander has the gameplay families needed for a bounded RTS Commander release: production, economy, construction, age advancement, research, movement, point scouting, combat, station defense, patrol, retreat, repair, producer rally, questions, conversation, strategic controls, voice, and bounded composition. The existing Request graph already combines production with actions and construction with bound production. Phase 5A adds shared workers, symbolic locations, exact new producers, and exact new production.

This is sufficient to stop broad Commander feature development. It is not Release Candidate ready. Current source contains specific factual and target-fidelity defects, approval presentation problems, voice lifecycle gaps, and release provisioning/verification gaps. They are enumerated in [release-gap-matrix.md](release-gap-matrix.md) and grouped into six follow-up packages below. No fixes were implemented in this audit.

The release scope must be stated accurately: session-based Windows skirmishes; supported canonical unit/structure adapters; bounded selectors and graphs; point scouting with normal vision; defense by stationing units that use normal combat; no general scripting, arbitrary map coordinates, cross-group formations, autonomous frontier exploration, or world save/load promise. This describes current source rather than silently reducing a prior universal-composition promise. The accepted phase documents already define bounded capability contracts.

Does Phase 5B need to exist? **NO — move to release/finalization work.** DynamicPlan does not have to duplicate every Phase 4G action. An unused dynamic UnitSet result is an intentional representation limit; the overall product still has result-bound tactical actions through Request. A future positioning or autonomous-exploration phase would require a separately chosen product promise. Neither is automatically required by this audit.

## 2. Source of truth and audit method

The live workspace was inspected, including source, tests, retained JSON/XML reports, package/configuration structure, the local packaged model, binary hashes, and sibling backend relay source. Three independent read-only inspections covered capabilities/composition, voice/provider packaging, and save/multiplayer/lifetime behavior; their conclusions were reconciled against source. Codegraph orientation did not return within the bounded observation and was terminated; direct source searches and reads supplied the evidence. No AGENTS.md was found in the workspace or inspected ancestors.

No production or test file was edited. No tests, real provider requests, microphone recording, build, standalone launch, peer session, credential rotation, Git commit, push, branch switch, restore, or cleanup was performed in this audit. Existing evidence was assessed against its actual scope. UI behavior is source-inspected; a new rendered/manual UI session was not performed.

### 2.1 Beginning Git outputs

~~~text
git status
On branch unit_models_and_voice_control
Your branch is up to date with 'origin/unit_models_and_voice_control'.

nothing to commit, working tree clean

git status --short
[empty output]

git branch --show-current
unit_models_and_voice_control

git rev-parse HEAD
8586764e644240f93c99e2afa1c7d41c697cc879

git log -1 --oneline
8586764 phase5A
~~~

The user-provided historical HEAD was 4b0ebc3d7fefa7eb970f1446baff3a4c1c0331ca. The authoritative workspace has since been committed at 8586764; this audit did not make that commit. The starting tree was clean, rather than the historically dirty tree. The local checkout remains authoritative.

Unity project metadata confirmed D:/unity_projects/OpenEmpires/Open Empires, Unity 6000.5.9f1, Windows Standalone target, idle editor, and no running test job. Packages/manifest.json and packages-lock.json were inspected. The Whisper package is pinned to e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687.

### 2.2 Manifest and artifact provenance

The established Phase 5A verifier reported **HEAD changed after snapshot**. A separate explicit comparison found **zero byte mismatches** across its 78 source records, 217 changed-file records, protected-boundary records, and 79 XML artifacts before this audit wrote documentation. Thus the commit identity changed while the recorded bytes matched. The historical manifest is preserved; it has not been rewritten to pretend that its old HEAD is current.

The old generator derives source lists from dirty paths. Running it on a clean committed checkout would omit those committed source files. A new audit-source-manifest.json captures the current committed source/configuration and the audit documents independently of dirty-file enumeration. It identifies bytes and provenance; it is not a release certificate.

Current binary hashes match the accepted Phase 5A record:

| Artifact | SHA-256 |
|---|---|
| Builds/Windows/OpenEmpires.exe | 36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277 |
| Builds/Windows/OpenEmpires_Data/Managed/OpenEmpires.Runtime.dll | D205F3FCDFB88BE1B0BFE4E1ADDA7157B15990FC51B81E8FC2D049CD9E27536D |

The default Player.log was 1,310 bytes, modified 2026-10-07T07:56:02.0262896Z. A values-free count found zero error/exception/assertion lines and zero unhandled/fatal patterns. This remains historical startup evidence; no fresh scenario or launch was performed.

## 3. Evidence index

Source references below use the current workspace. The cited test files substantiate what is covered; their mere existence is not a new execution result.

| Ref | Source / test and relevant inspected lines |
|---|---|
| S01 | [CommanderIntentCatalog.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/CommanderIntentCatalog.cs:38>): supported units; 47 supported structures |
| S02 | [CommanderPlanner.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/CommanderPlanner.cs:443>): ReachAge; 589 production; 619 required new results; 638 population; 658 age; 685 exact producers; 959 worked construction |
| S03 | [CommanderPlanner.Economy.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Economy/CommanderPlanner.Economy.cs:19>): quantity modes; 41 workers; 79 source-specific commands |
| S04 | [CommanderCapabilityExecutor.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs:88>): action commands; 130 exact cardinality; 184 locations; 239 visible enemies; 262 rally; 285 repair; 306 research |
| S05 | [CommanderSemanticGraphAdmission.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticGraphAdmission.cs:160>): result links; 212 producer links |
| S06 | [CommanderSemanticJson.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs:12>): graph bounds; 62 outcome handling; 243 structure selector restricted to rally |
| S07 | [CommanderDynamicPlan.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicPlan.cs:79>): nine primitives; 169 program bounds |
| S08 | [CommanderDynamicCompiler.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderDynamicCompiler.cs:101>): effect lowering; 179 produce; 285 Near gap |
| S09 | [CommanderGoalManager.DynamicLocations.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.DynamicLocations.cs:43>): location/UnitSet consumer boundary; 166 frozen unit selection |
| S10 | [CommanderGoalManager.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/CommanderGoalManager.cs:232>): new production; 290 producer binding; 299 unit results; 512 dependency gate; 580 ordinary enqueue; 791 disposal |
| S11 | [CommanderChatUI.Questions.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Questions.cs:155>): age facts; 167 costs; 228 live activity |
| S12 | [LandmarkDefinitions.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Buildings/LandmarkDefinitions.cs:83>): Castle cost; 109 Imperial cost; 249 civilization choices |
| S13 | [CommanderSemanticProvider.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs:20>): bounds; 50 allow-listed context; 78 fields; 110 size check |
| S14 | [CommanderChatUI.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:407>): runtime affinity/deadline; 430 question facts; 582 admission; 769 reset; 819 normal Send; 942 UI |
| S15 | [CommanderActionPlanCandidate.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderActionPlanCandidate.cs:128>): preview; 168 symbolic detail; 211 RenderIntent; 237 capability radius |
| S16 | [CommanderGoalManager.Requests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase5A/CommanderGoalManager.Requests.cs:94>): narrow automatic KnownIntent scope; 107 complete scope comparison; 193 local confirmation |
| S17 | [StrategicPlanner.Authority.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicPlanner.Authority.cs:7>): actual computer owner; 11 trusted commit; 29 live root |
| S18 | [CommanderVoiceInputController.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4H/CommanderVoiceInputController.cs:96>): timeout/await; 108 stale result; 189 cancel; 235 dispose |
| S19 | [CommanderChatUI.Voice.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4H/CommanderChatUI.Voice.cs:56>): initialization; 84 shared submission; 147 Escape; 237 cancel; 247 preview field |
| S20 | [WhisperCommanderSpeechToTextProvider.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4H/WhisperCommanderSpeechToTextProvider.cs:127>): conversion; 140 locked inference; 158 cancellation; 186 disposal |
| S21 | [CommanderWorkerAuthority.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/CommanderWorkerAuthority.cs:14>): ownership collections; 100 pruning; 138 human/Commander command observations |
| S22 | [StrategicIntentIdProvider.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Strategic/StrategicIntentIdProvider.cs:12>): lifetime ownership registry; [StrategicAIInterpreter.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4B1/StrategicAIInterpreter.cs:29>) binds request context |
| S23 | [GameBootstrapper.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Core/GameBootstrapper.cs:66>): teardown; 158 fresh simulation; 162 local Commander; 325 multiplayer command flush |
| S24 | [CommandSerializer.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Network/CommandSerializer.cs:1213>): existing restricted-source packet; 1438 version-1 decode |
| S25 | [CommanderCommandOriginLedger.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Core/CommanderCommandOriginLedger.cs:13>): bounds; 80 batch equivalence; 102 ambiguity; 140 cleanup |
| S26 | [GameSimulation.TrainingObservation.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Core/GameSimulation.TrainingObservation.cs:55>): local queue receipt observation; 110 original command identity |
| S27 | [NetworkManager.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Network/NetworkManager.cs:746>): received owner ordering |
| S28 | [relay.rs](<D:/unity_projects/OpenEmpires/backend/src/services/relay.rs:201>): history growth; 496 reconnect clone; [ws.rs](<D:/unity_projects/OpenEmpires/backend/src/api/ws.rs:640>) reconnect payload |
| S29 | [UnitCombatSystem.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Units/UnitCombatSystem.cs:333>): idle auto-engagement; 284 leash; [UnitMovementSystem.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/Units/UnitMovementSystem.cs:182>) arrival |
| S30 | [FogOfWarSystem.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/FogOfWar/FogOfWarSystem.cs:55>): normal vision; [CommanderContextBuilder.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/CommanderContextBuilder.cs:40>) visibility filter |
| S31 | [CommanderSemanticConversationMemory.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticConversationMemory.cs:75>): detached follow-ups |
| S32 | [CommanderChatUI.StrategicControls.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.StrategicControls.cs:397>): exact plan-health queries and owned controls |
| S33 | [OpenRouterCommanderProvider.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs:193>): one shape repair; 281 timeout; 303 response bound; 370 metadata-only trace |
| S34 | [CommanderVoiceSettings.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4H/CommanderVoiceSettings.cs:68>): recording settings; [CommanderAudioConverter.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Scripts/AI/Commander/Phase4H/CommanderAudioConverter.cs:13>) default limit |
| T01 | [CommanderPhase4GHostileAuditTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase4GHostileAuditTests.cs:608>): age test currently expects the incorrect 1200/600 string |
| T02 | [CommanderEconomyClarificationTests.Execution.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderEconomyClarificationTests.Execution.cs:12>): quantity modes, workers, sources, capacity preflight |
| T03 | [CommanderPhase5ADynamicCompilerTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase5ADynamicCompilerTests.cs:68>): exact two-new-producer plan; 18 berry Mill; 38 partition |
| T04 | [CommanderPhase5AProductionBindingTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase5AProductionBindingTests.cs:163>): preexisting/human production excluded; 121 loss/cancel behavior |
| T05 | [CommanderPhase4GResultBindingPlayModeTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/PlayMode/CommanderPhase4GResultBindingPlayModeTests.cs:55>): baseline one, target four, exactly three produced patrol units |
| T06 | [CommanderPhase5AAntiGravityHostileAuditTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase5AAntiGravityHostileAuditTests.cs:1>) and [AuthorityPlayModeTests](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/PlayMode/CommanderPhase5AAuthorityPlayModeTests.cs:1>) |
| T07 | [CommanderPhase4HVoicePlayModeTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/PlayMode/CommanderPhase4HVoicePlayModeTests.cs:43>): mock capture/provider; [AudioFixtureTests](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase4HAudioFixtureTests.cs:48>) prerecorded native fixtures |
| T08 | [CommanderPhase4GCapabilityTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/EditMode/CommanderPhase4GCapabilityTests.cs:289>): exact result set |
| T09 | [CommanderPhase5ALiveRuntimePlayModeTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/PlayMode/CommanderPhase5ALiveRuntimePlayModeTests.cs:124>): Mill; 247 semantic checks; 288 deadline |
| T10 | [CommanderPhase3Fix13PlayModeTests.cs](<D:/unity_projects/OpenEmpires/Open Empires/Assets/Tests/PlayMode/CommanderPhase3Fix13PlayModeTests.cs:161>): paired in-process simulations, not network peers |

## 4. Current capability matrix

These statuses describe production implementation, not certification. Findings are identified by G IDs in the gap matrix.

| Capability | Classification | Source/test evidence and release boundary |
|---|---|---|
| Natural production requests | IMPLEMENTED WITH DOCUMENTED LIMITATION | S01/S02; Villager, Spearman, Archer, Scout, Knight and supported civilization replacements. A count is normally a desired total. |
| Unit desired-state production | FULLY IMPLEMENTED | S02/T04; owned/queued totals, normal producer/housing/resource preparation, age blocker. |
| Worker allocation | FULLY IMPLEMENTED | S03/T02; deterministic ownership, eligibility, reservation and destination resolution. |
| SelectedCount economy | FULLY IMPLEMENTED | S03/T02; exact selected count. |
| Additional economy | FULLY IMPLEMENTED | S03/T02; adds the requested eligible workers. |
| TargetTotal economy | FULLY IMPLEMENTED | S03/T02; computes deficit against current destination assignments. |
| AllMatching economy | FULLY IMPLEMENTED | S03/T02; one bounded snapshot, not an indefinite future-worker policy. |
| Idle worker selection | FULLY IMPLEMENTED | S03/T02; idle/unqueued eligibility and human protection. |
| Source-restricted gathering | FULLY IMPLEMENTED | S03/S24/T02; native restriction retained through ordinary commands. |
| Sheep gathering | FULLY IMPLEMENTED | S03/T02; normal SlaughterSheepCommand with exact worker/source rules. |
| Construction | IMPLEMENTED WITH DOCUMENTED LIMITATION | S01/S02/T03; House, Barracks, ArcheryRange, Stables, Mill, Farm, Tower, TownCenter adapters; not every base-game building. |
| Farm construction | FULLY IMPLEMENTED | S01/S02/T09; canonical cost/footprint/native food-node behavior. |
| Contextual construction | IMPLEMENTED WITH DOCUMENTED LIMITATION | S05/S08/T03; typed anchors/relations, legal footprint/pathability, bounded placement. |
| Worked-resource construction | FULLY IMPLEMENTED | S02/T03/T09; owned gather relation to visible nondepleted resource. |
| Production prerequisites | FULLY IMPLEMENTED | S02; canonical producer derived by planner, exact requested producers preserved. |
| Population prerequisites | FULLY IMPLEMENTED | S02; canonical House/capacity preparation within allowed authority. |
| Resource prerequisites | FULLY IMPLEMENTED | S02/S03; legitimate canonical preparation, concurrent age food/gold paths and source constraints. |
| Age progression | IMPLEMENTED WITH DOCUMENTED LIMITATION | S02/S12; real landmark transitions, only available civilization targets, explicit request required. |
| Technology/research | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/T01; canonical technology/age/cost/building checks; missing research prerequisites block rather than infer age/building work. |
| Movement | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/T08; bounded owned-unit selectors and supported game-side locations. |
| Scouting | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/S30; point movement, not autonomous unknown-map coverage or a route around a hidden enemy base. |
| Vision | FULLY IMPLEMENTED | S30; moving scouts/units reveal through normal FogOfWar simulation. |
| Attack | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04; visible enemy attack works, but a named enemy type/group cannot currently be encoded faithfully (G04). |
| Defense | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/S29; station at anchor, normal idle aggro/leash/return; no Commander replenishment or explicit perimeter policy. |
| Patrol | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/T05; ordinary persistent PatrolCommand, supported anchor rather than arbitrary circular route. |
| Retreat | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04; owned or damaged military to supported safe/base anchor. |
| Repair | PARTIALLY IMPLEMENTED | S04/S06; repair command works, but named Town Center selection is absent and first damaged building wins (G04). |
| Rally points | IMPLEMENTED WITH DOCUMENTED LIMITATION | S04/S05; one completed producer or one exact bound new producer; troop assembly is Move, not producer rally. |
| Knowledge Q&A | PARTIALLY IMPLEMENTED | S11/S12/T01; costs/catalog queries exist, but age answers contradict canonical costs/availability (G02). |
| Live-state Q&A | PARTIALLY IMPLEMENTED | S11/S13/S32; counts and strategy health exist; general tactical blocker question lacks resource/goal/reason projection (G03). |
| Compound requests | IMPLEMENTED WITH DOCUMENTED LIMITATION | S05/S06/S10/T05; four-node Request DAG and separately bounded dynamic composition. |
| Result binding | FULLY IMPLEMENTED | S05/S10/T04/T08; runtime/owner/type/freshness checks with no substitution. |
| Producer binding | FULLY IMPLEMENTED | S05/S02/T03/T04; exact new producer set, including two buildings. |
| Produced-unit result binding | FULLY IMPLEMENTED | S10/S25/S26/T04/T05; accepted order/spawn attribution, excludes human/unrelated/preexisting units. Quantity preflight needs G06. |
| Conversation follow-ups | IMPLEMENTED WITH DOCUMENTED LIMITATION | S31; bounded accepted semantic facts/counts, not durable arbitrary entity-group identities across turns. |
| Clarification | FULLY IMPLEMENTED | S03/S14; typed pending worker draft, local count reply, validated corrections, three-reply bound and reset isolation. |
| DynamicPlan composition | IMPLEMENTED WITH DOCUMENTED LIMITATION | S07/S08/S09/T03; nine primitives, shared workers, exact new production/building sets; no tactical UnitSet consumer. |
| Strategic requests | IMPLEMENTED WITH DOCUMENTED LIMITATION | S17/S14; supported strategy objective families, local preview/approval and commitment rules. |
| Strategic recommendation | FULLY IMPLEMENTED | S17/T06; human recommendations are advisory; actual simulation AI autonomy preserved. |
| Strategic approval | FULLY IMPLEMENTED | S16/S17/T06; trusted local authorization, single-use identity and fresh source checks. |
| Strategic adaptation | FULLY IMPLEMENTED | S14/S32; active-source identity/revision/freshness and explicit replacement rules. |
| Pause/resume/cancel | IMPLEMENTED WITH DOCUMENTED LIMITATION | S32/S16; strategic controls and request-wide confirmed-plan cancel. Already issued gameplay is not rolled back; no generic tactical pause promise. |
| Plan health | FULLY IMPLEMENTED | S32; bounded owned progress/blocker/recovery snapshots and controls. |
| Voice/STT | IMPLEMENTED WITH DOCUMENTED LIMITATION | S18/S19/S20/T07; local microphone/Whisper, preview, same text route. Provisioning, hardware proof and lifecycle findings remain. |
| Human override | FULLY IMPLEMENTED | S21/T06/T08; manual commands release/protect units; exact results cannot silently reclaim/substitute them. |
| Fog/hidden-state protection | FULLY IMPLEMENTED | S30/S04/S13/T06; visible-only selectors and allow-listed detached provider context. |
| Reset/stale provider protection | FULLY IMPLEMENTED | S14/T06; deadline race plus generation/provider/world/manager/dispatcher/owner checks. Voice host reinitialization has a distinct G15 exception. |

## 5. Intended player experience

Representable does not mean every wording has been verified with the live provider. No live calls were made in this audit.

| Player request | Current reasonable behavior / gap |
|---|---|
| make ten spearmen | Ensure desired total ten, including baseline/queued population; explicitly new production is different. |
| put four idle villagers on food | SelectedCount four, idle/unqueued, normal visible food destination; clarify if count/source/eligibility is ambiguous. |
| put four more villagers on food | Additional four, preserving the difference from a total of four. |
| make sure I have four villagers on food | TargetTotal four; assign only the deficit. |
| put all idle villagers on food | AllMatching idle snapshot; does not claim future villagers automatically. |
| gather food from sheep | Source-restricted Sheep gathering; missing quantity can clarify rather than invent an all-workers request. |
| make four farms | Four native Farms with canonical wood/footprint/food nodes; live retained proof exists. |
| build a mill near the berries | Visible-resource dynamic selection/location/build can represent it; do not incorrectly require already worked berries for every interpretation. |
| build a mill near berries my villagers are working | Known worked-resource request and dynamic route; visible owned worked binding; two recent isolated live passes. |
| build two barracks near TC and use them to make ten spearmen | Exact build-result producer collection and dependent production; desired-total versus ten-new must be explicit. |
| move my scout around their base for vision | Point scouting can reveal vision. A route around a hidden/named enemy base is not representable; clarify/unsupported rather than fabricate coordinates. |
| move those spearmen near the bridge | Produced-unit binding within a graph exists; arbitrary cross-turn group identity and bridge anchor do not. No native gameplay bridge representation was found. |
| defend this gold | Station selected owned military at a visible/worked Gold node, then normal aggro/leash combat. Not indefinite replenishment. |
| attack those archers | Attack a visible enemy is implemented; target enemy-type/group specificity is missing. Do not silently attack a nearer villager instead (G04). |
| patrol around my base | Patrol toward supported base anchor; no circular perimeter/radius implementation. Presentation must match execution (G05). |
| repair my Town Center | Native repair exists, but the current schema cannot specify TC and executor picks the first damaged building. G04 must fix or explicitly reject this specificity. |
| research the archer upgrade | Resolve/clarify a supported canonical technology; current age/resources/completed research building required. Do not invent an archer-only technology or auto-age. |
| I want to reach Castle Age | Explicit ReachAge desired state, real canonical landmark/resource path. |
| How do I reach Castle Age? | Information route is non-effectful, but current answer has wrong cost. G02. |
| What counters spearmen? | Bounded qualitative archer/cavalry advice exists; numeric/effective combat advice should remain tied to canonical data rather than universal balance claims. |
| Why haven't you finished making my army? | Natural tactical explanation is not reliably grounded: provider context omits active goals, reasons, resource amounts and queued production. G03. |
| 15 spearmen and set their rally point to nearest worked gold around TC | Clarify whether this means assembling new troops or setting producer rally. Ensure→Move can assemble exact produced units at visible worked Gold nearest the lowest-ID TC, with correct baseline/new quantity. It does not enforce a surrounding radius/formation. SetRallyPoint changes a producer, not a unit group. |

## 6. Composition architecture and significant mixed requests

Request and DynamicPlan are complementary. Request is a four-node DAG with at most eight dependency references and depth four. It binds producerFromNode and resultFromNode game-side. DynamicPlan is a twelve-node/depth-five typed DAG with a 200-entity aggregate bound; select-workers, partition-workers, select-units, select-structures, select-resources, resolve-location, build, allocate-workers, and produce are its only mechanics. The compiler lowers only build/allocate/produce effects. Dynamic UnitSet/UnitResult and Location cannot feed a new movement/combat effect.

| Mixed request | Representation today | What actually works |
|---|---|---|
| five spearmen then send them to bridge | Request Ensure→Move result binding | Produced-unit action works for supported anchors; bridge is not a grounded native target. |
| three spearmen then patrol gold | Request Ensure→Patrol resultFromNode | Works with correct target-total baseline plus three new units. T05 has one old unit, desired total four, consumer count three. G06 must preflight cardinality. |
| archers positioned behind spearmen | Not composable faithfully | Requires a separate unit-set anchor and rear relation; current RelativeToSelectedUnits is the action subjects' own centroid. Future P3 scope. |
| barracks then rally its units near TC | Request Build→SetRallyPoint building result | Works for one new barracks' producer rally. Troop assembly must use a unit action instead. |
| stable then knights from that stable | Request Build Stables→Ensure Knight producerFromNode | Exact new producer works; required age must exist or be explicitly requested separately. |
| two barracks, ten new spearmen only from them | Dynamic build→produce New, also known producer graph | Exact complete producer set and goal-attributed new outputs; covered by T03/T04. |
| repair TC and move damaged units home | Independent repair + Move/Retreat nodes | Damaged-unit retreat works. Named repair target is a P1 fidelity fix. |
| infantry upgrade then spearmen | Research→Ensure dependency | Works for supported canonical technology and existing research prerequisites. |
| units then defend worked resource | Ensure→DefendArea result binding | Exact produced set moves to worked resource, then normal simulation combat supplies meaningful defense. |
| new scouts then exploration | Ensure Scout→ScoutArea result binding | Point scouting/vision works; autonomous frontier exploration is outside the current contract. |

These supported combinations do not need phrase-specific execution code: provider output is a bounded typed program, then existing planners execute it. Local explanation/query recognition and control utterances do have regex/exact-form handling; that is not a substitute for new gameplay actions.

Major composition findings are quantity compatibility, target fidelity, location presentation, and honest unsupported outcomes. Extending every action into DynamicPlan would not automatically solve bridge grounding, behind relations, repair specificity, or exploration semantics. It would add a second representational surface. That expansion is optional P3 work, rather than a mandatory Phase 5B.

## 7. DynamicPlan limitation review

| Limit | Classification / owner | Release judgment |
|---|---|---|
| UnitSet has no dynamic effect consumer | NOT AN ISSUE / DOCUMENTATION | Accepted nine-primitive scope; existing Request unit-result actions remain. Expose unsupported dynamic combinations honestly. |
| One ResourceSource constraint per candidate/list | NOT AN ISSUE / DOCUMENTATION | Bounded declared contract; multi-resource source restrictions are future expansion if a concrete use case is chosen. |
| Maximum four shared constraints | NOT AN ISSUE / DOCUMENTATION | Explicit bounded validation; reject excess rather than drop constraints. |
| No conditions | NOT AN ISSUE / DOCUMENTATION | Bounded DAG, not general scripting; conditions/loops are P3 future promises. |
| Near clear gap fixed at one tile | NOT AN ISSUE / DOCUMENTATION | Explicit supported relation, negative tests reject unsupported gaps; no silent widening. |
| No deterministic English entailment | NOT AN ISSUE / DOCUMENTATION | Structural root/provenance validation cannot prove arbitrary English meaning. Compound/dynamic preview is the local consent step; simple KnownIntent keeps the accepted automatic path. Do not claim universal semantic faithfulness. |

## 8. Save/load

No base-game world save/load implementation or Commander restoration entrypoint was found under Assets/Scripts. Fresh simulation construction and match teardown are present (S23). Settings persistence, Commander memory JSON export, command serialization, relay reconnect history and origin-ledger replay correlation are not world saves.

| State during hypothetical save | Current persistence / load behavior |
|---|---|
| Active tactical goal | Runtime-only; no world-save hook or goal DTO restore. |
| Active or paused strategic plan | Runtime-only; no restoration of progress/paused state. |
| Worker/resource reservation | Runtime planning accounting; cancellation/disposal release or abandon it with the runtime. |
| Pending clarification | Match-local draft, cleared on initialize/reset/destruction. |
| Pending DynamicPlan/strategy approval | Match-local candidate and fresh authorization; not serialized. |
| Conversation memory | Bounded detached match-local facts; JSON export is not a load service. |
| Result-bound compound request | Runtime entity/receipt/original-command identity; no durable origin restore contract. |
| Provider request in flight | Transient task with generation/deadline guards; cannot be persisted. |

There is no ordinary loading operation to classify as corruption, duplication, or safe resume. A new match creates a fresh runtime and loses the prior automation; normal teardown invalidates local state. Do not claim tested save/load cancellation or restoration.

For a clearly documented session-skirmish release, Commander save persistence is **NOT AN ISSUE / DOCUMENTATION**. Resumable single-player matches would be a new base-game requirement, P3 until chosen; it would then become a P1 implementation gate. It is not a mandatory Commander Phase 5B.

Minimal future architecture: implement a versioned value-only world snapshot first. The simplest safe Commander policy is visible cancellation on load and fresh requests. If resumption is promised, persist only committed typed roots, bounded plan progress, stable entity/result references and constraints. Restore into a fresh runtime; revalidate ownership, entity existence, producer identity and constraints; rebuild reservations and mint fresh local authority through a trusted restoration service. Pending approvals should be discarded or restored as drafts requiring fresh confirmation. Never serialize HTTP tasks, cancellation tokens, delegates, Unity/live object references, provider secrets, stale async responses, or boxed-command identity.

## 9. Multiplayer and determinism

The local Commander is constructed for Network.LocalPlayerId (S23). Planners enqueue ordinary gameplay commands; multiplayer flush stamps local ownership and sends for the delayed tick. Received owner batches are ordered before normal simulation dispatch (S27). Configured simulation AI uses its existing AI buffer; neither local player 0 nor a remote human is considered AI autonomy (S17).

Phase 5A adds local receipts/correlation, not network command bytes. Source-restricted Gather/Slaughter binary version-1 encoding predates Phase 5A (S24). Compatible peers are required; no current build/protocol negotiation was found to certify mixed old/new clients. Release packaging/version policy is a P1 multiplayer gate, not a reason to add provenance bytes.

The origin ledger bounds future ticks/batches, compares complete ordinary payload/order/owner, and fails closed on ambiguous attribution (S25). Queue receipts and actual spawn observations are local (S26). They neither grant simulation authority nor suppress normal gameplay on failed correlation. Exact-result goals block on lost attribution rather than repeat potentially executed production.

| Action family | Architecture / local evidence | Actual peer certification |
|---|---|---|
| Worker reassignment and source gathering | Ordinary Gather/Slaughter commands; source/authority tests | NOT VERIFIED |
| Construction / production / compound goals | Place/Construct/Train; exact producer/order/result tests | NOT VERIFIED |
| Age progression | Normal landmark/resource commands; retained native fixtures | NOT VERIFIED |
| Move / scout / attack / patrol / rally | Normal Move/Attack/Patrol/Rally commands; Phase 4G fixtures | NOT VERIFIED |
| Cancellation / replacement / pause / resume | Local owned control changes future issuance; already sent commands remain | NOT VERIFIED with delayed peer batches |
| Late provider / reset / follow-up | Local generation/runtime checks; local fixtures | NOT VERIFIED across actual match/peer teardown |
| Voice-originated command | Same text route and ordinary commands | NOT VERIFIED with native microphone and peers |

The historical 1,500-tick paired simulation test (T10) is in-process deterministic evidence, not two-peer certification. A public multiplayer RC requires P1 MANUAL QA: real peers, equal checksums/logs, local/remote human and configured AI ownership, foreign/human queues, delayed commands, destroyed bound entities, cancellation/replacement/pause, source restrictions, voice and reset/late-response cases. A deliberately solo/private RC can defer that gate; it must not advertise multiplayer certification.

Backend reconnect history grows and is cloned/sent wholesale (S28). Its deployment memory/latency and client rejoin correctness are not measured. Source establishes the retention mechanism, not a deployed crash. Define the reconnect/history policy before multiplayer public distribution; do not simply truncate history if the client depends on replay from initial world state.

## 10. Provider deployment and secret handling

ICommanderAIProvider and ICommanderSemanticProvider keep provider translation separate from game execution. Input 1,024 characters, context 8,192, semantic memory 4,096, clarification 4,096 and question facts 512 are bounded (S13). Strict envelopes/nodes, response content limits, finish-reason handling, one narrowly matched numeric-shape repair and linked HTTP timeout are present (S33). The host races providers that ignore cancellation and rejects late responses by generation/provider/world/manager/dispatcher/owner identity (S14). Unknown JSON/authority fields fail closed.

The legacy semantic envelope permits 8,192 characters; the dynamic envelope permits 32,768, with the separate graph/type/count bounds. The HTTP body check is 65,536 characters, and semantic max_tokens defaults to 4,096 (configuration range 1,024–4,096). Provider HTTP and normal host deadlines default to 15 seconds. A schema-only repair can make one additional HTTP request; the live fixture's six-call counter counts submitted semantic translations, not guaranteed HTTP transaction count. Future evidence must record initial/repair transactions separately rather than infer paid requests from that fixture counter.

The HTTP transport buffers the complete response before the OpenRouter body-size check. The semantic result is bounded, but receive allocation is not bounded by that later check. G19 recommends a streaming byte cap, retaining existing cancellation and safe error categories.

| Deployment | Current assessment |
|---|---|
| Private developer build | Direct provider access with developer-supplied external configuration is appropriate. No backend is automatically required. |
| Open-source source release | Provide redacted configuration instructions, ignored secrets, pinned dependencies/model provisioning and reproducible build steps. A consumer can supply their own key. |
| Public downloadable binary with BYOK | Can use direct provider access if credential ownership/setup/storage is documented and usable. Inspect distribution for secrets; do not bundle a reusable developer key. |
| Public binary funded by an application-owned key | A reusable credential embedded/shipped to clients is not defensible. Use a separately authenticated controlled service/proxy with quotas, abuse controls and secret isolation. This backend is conditional on that product choice. |

Current source has loader/constructor/environment structure, not an end-user Commander credential setup experience or chosen public deployment contract. No hardcoded reusable OpenRouter key was found by the redacted source scan. This is not full binary or Git-history certification and does not prove every possible opaque secret is absent.

### Credential incident check

The previous Unity process-token incident was not re-read. No process command lines, environment dumps, .env contents, header values or actual secret values were requested. A redacted generic literal scan covered 1,047 tracked project text files plus root/backend text. A separate Unity process-field-label scan covered 1,206 tracked text files and the current Git diff: zero matching process-token labels, zero diff matches. Only counts/path/label metadata were permitted; no matched value was emitted.

No source/Docs/JSON/XML/remaining_work copy of that incident is established. Label scans cannot detect every unlabeled opaque value, historical Git object or binary. Previous exposed session/license credentials should be rotated or confirmed expired/revoked separately, without reproducing them. This audit did not rotate or verify their validity. G01 records the historical P0 incident's external closeout rather than asserting a newly discovered production credential leak.

## 11. Voice and Whisper release readiness

The source path is microphone → local Whisper → transcript → SubmitMessageAsync → the same Commander route (S18/S19/S20). Voice carries no special gameplay authority. Preview defaults on; text submission of a dynamic or strategic request still uses its ordinary confirmation stage. Push-to-talk, record toggle, recording/transcribing/preview states, cancel/reset and error paths exist.

Concrete lifecycle findings (G15–G18) remain: preview cancellation clears controller text but leaves the input field; Dispose marks disposed before calling a Cancel that immediately returns; public chat reinitialization reuses an old voice session; configured recording length up to 60 seconds is silently converted with the default first-15-second limit. Native GetText holds a lock through inference; token cancellation cannot interrupt it, and disposal can wait for that lock on the main thread. Source proves the mechanism; an actual long stall duration has not been measured.

The source and packaged ggml-tiny.bin are both 77,691,713 bytes and match SHA-256 BE07E048E1E599AD46341C8D2A135645097A538221678B7ACDD1B1919C6E1B21. A historical model checksum is already recorded. The model is ignored and local; the clone instructions do not provide a pinned download/provision command or verification gate. The release needs reproducible provisioning, missing-model guidance and build prerequisite checks (G08).

The binding package, whisper.cpp and OpenAI code/model weights use MIT terms; copies need the appropriate copyright/permission notices. The inspected root THIRD_PARTY_NOTICES and current build inventory omit the Whisper additions. Add notices for the exact distributed binding/native/model artifacts before public distribution (G09). Primary references: [pinned whisper.unity license](https://raw.githubusercontent.com/Macoron/whisper.unity/e951e4a4c6e44c781b1d36bb8dc5bf1b7bae9687/LICENSE.MD), [whisper.cpp license](https://raw.githubusercontent.com/ggml-org/whisper.cpp/master/LICENSE), [OpenAI Whisper license and weights statement](https://github.com/openai/whisper#license). Version-specific native provenance still belongs in packaging review.

The current tiny model is multilingual with English decoding forced; it is not tiny.en. Official [model documentation](https://github.com/openai/whisper#available-models-and-languages) describes the English-specific tiny.en/base.en alternatives. Select any replacement after measured physical recordings on supported Windows hardware; published GPU speed ratios are not Unity CPU latency evidence.

Physical microphone acceptance remains NOT VERIFIED. Prerecorded native fixtures and mock capture/voice routing tests are useful, but do not prove device permissions, PTT, actual speech quality, preview edit/cancel/retry, or packaged voice → approved gameplay. G13 is a P1 manual gate for a voice-enabled RC.

## 12. Fresh clone and packaging

| Item | Current evidence / remaining work |
|---|---|
| Unity version | ProjectVersion.txt pins 6000.5.9f1; put that in current top-level setup. |
| Packages | Manifest/lock present; Whisper commit pinned; review release resolution including other Git packages and editor-only tools. |
| Whisper native package | Locally imported; package declarations present. Fresh import/build reproducibility not executed. |
| Model / StreamingAssets | Current source/build model matches historical hash, but ignored model lacks documented provisioning command. |
| OpenRouter setup | Developer loader and provider integration documentation exist; public credential ownership/onboarding is undecided. |
| Ignored secrets | Ignore rules exist; no secret contents read. Distribution and history clearance remain scoped release gates. |
| Commander configuration | Provider factories/bootstrap exist; supported content/selectors should be discoverable in the release guide. |
| Voice configuration | Model/language/record settings exist; recording duration policy and documentation need alignment. |
| Build procedure | Existing successful Windows artifact and historic build evidence; a fresh empty-cache/clone reproduction is not claimed. |
| Tests | Many retained fixtures/reports; paid live category currently contaminates a full offline baseline and diagnostics can disappear. |
| Public Windows package | Model/native/notices, credential setup, build identity and launch/runtime proof need a reproducible release checklist. |

A new clone was not created, imported, built or tested. The provisioning gap is established from an ignored required model and missing documented acquisition/verification path, not from an invented failed clone experiment.

## 13. UX and error handling

Text input, bounded history/autoscroll, provider status, voice state, clarification, plan approval, recommendations, active strategy status, blockers and owned pause/resume/cancel are present (S14/S19/S32). Rich text is disabled on transcript/status. The normal input UI caps at 240 characters even though the semantic API permits 1,024. Actual rendered layout/manual use is historical, not newly verified.

The dynamic approval preview exposes raw primitive IDs, parameter names, result links and selector enums (S15). A player should understand actors, effects, counts, dependencies, bindings, constraints and consent without learning the DSL. This is a P1 functional approval presentation issue (G07), not a broad UI redesign. RenderIntent is also used for authority scope equality (S16): changing its human text carelessly could collapse distinct effects. Preserve a complete deterministic structural comparison/fingerprint separately from a readable display.

| Error / transition | Current user-facing behavior | Finding |
|---|---|---|
| Provider unavailable / auth / rate limit | Safe category explanation, no gameplay admission | Existing behavior; release setup must make retry/credential action clear. |
| Provider timeout | Host returns promptly and rejects late result; says retry/offline | Existing safeguard. |
| Malformed / truncated / schema-invalid output | Safe rejection; truncated finish rejected, unknown fields fail closed | Existing safeguard; add bounded stage trace to tests. |
| Unsupported mechanic | Unsupported/Clarify or generic admission failure | Do not drop requested specificity or silently replace target. |
| Dynamic validation rejection | Stage logged; user often sees generic translation failed safely | G20: report actionable bounded reason, keeping private internals/secrets out. |
| No legal placement | Planner blocked/wait status with reason | Surface real blocker rather than imply completed construction. |
| Missing resources / no eligible workers | Waiting/blocker or exact-selection preflight rejection | Existing planner semantics; G03 should explain tactical reasons. |
| Human takeover | Request-bound worker/result refuses reclaim/substitution | Existing authority; readable explanation needed. |
| Stale request / reset during provider | Discard, no late admission; new runtime guards | Existing provider safeguard. |
| Missing Whisper model | Voice unavailable/error | G08: actionable provisioning instructions. |
| Microphone unavailable | MIC_START_FAILED/error path | Real device/permission behavior needs G13. |
| Whisper failure / slow inference | Error or cooperative cancel; native await can remain pending | G15–G18 lifecycle/latency package. |
| Cancel voice preview | Controller clears; input still holds transcript | G15: normal Send can submit the cancelled text later. |

No generic pending-state leak or false success is claimed without a source path. Specific silent truncation, cancelled-preview residue, generic validation messaging and ungrounded answers are documented; cosmetic wording/rebinding-label drift is P3.

## 14. Long-session robustness and performance

| Collection / work | Current bound / lifecycle |
|---|---|
| Goals / archives | 64 active, 50 archived; explicit eviction. |
| Strategic plans / history | 4 active, 50 archived, 100 intent history; reservation archive 100 default. |
| Decisions / advisories | Decision history 20 default; advisories remember 32 plans and emit at most four per observation. |
| Conversation / semantic memory | 12 history turns; memory 32 default /128 max /512 chars; semantic 8 default /32 max /180 chars. |
| Transcript | 64 messages, 32,768 characters per message. |
| Request tickets / approvals / clarification | Monotonic ticket ID, one claim; no all-ticket collection; one displayed preview/draft/submission. |
| Dynamic program/results | 12 nodes, depth five, 200 aggregate entities; training-origin table 512. |
| Origin ledger / traces | 32 future ticks, 256 commands/batch; provider metadata trace capped around 2,000 characters. |
| Voice audio | Default 15 seconds, configured recording up to 60; clip cleanup exists, policy mismatch remains. |

Source-confirmed retention exceptions: S22 owns all strategic identity claims for the runtime, including failed request contexts; S21 prunes reservations but leaves historical dead-unit protection/control/gather entries; S28 retains complete relay history. They grow with cumulative work, not merely current population. Heap growth rate/user impact is unmeasured; no crash or cross-match leak is asserted. Preserve anti-replay/ownership guarantees while retiring heavy objects and dead references.

| Cost | Frequency | Assessment |
|---|---|---|
| UI selected-plan/advisory projection | LateUpdate / deduplicated simulation observations | Per-frame opportunity, not a measured bottleneck. |
| Fog and normal gameplay | Simulation tick | Existing base-game cost; Commander uses normal commands. |
| Goal planning / selection / placement / dependencies | Every 15 simulation ticks, retries bounded | Worker/resource sorting and limited route tests; Near candidate search bounded. |
| Strategic evaluation | Events plus 900-tick default cooldown | Coalesced work; emergency path still respects authority. |
| CommanderContext / canonical knowledge / civ projection | Submission and strategic context captures | Scans units/buildings/resources/goals and rebuilds detached catalog. No measured optimization requirement. |
| Provider projection / JSON serialize/parse | Provider request/result only | Character/node bounds; transport receive allocation gap G19. |
| Result attribution | Command send/receive and production events | Batch ambiguity comparison O(batch squared), capped at 256. |
| Archive cleanup | Registration/terminal lifecycle | Bounded lists; identity/dead-worker exceptions above. |
| Native STT / relay reconnect history | Audio event / server tick and reconnect | Relevant measured follow-up for identified mechanisms. |

No new profiling or arbitrary soak test was demanded. P3 profiling should measure specific frame/allocation costs at supported population/goals and native inference before optimizing. G21–G23 lifetime policy work is justified by actual source retention, not the absence of a long run.

## 15. Security and authority review

| Invariant | Current-source conclusion |
|---|---|
| LLM cannot create ICommand / call CommandBuffer / mutate simulation | Provider interfaces return semantic results. Existing game-side admission/planners create/enqueue ordinary commands. |
| Provider cannot choose player identity or approval | Strict fields and local request/candidate/owner/generation evidence; strategic human requests need trusted approval. |
| Provider cannot inject runtime IDs / coordinates | Typed symbolic references and selectors; IDs/placements resolved from owned visible local evidence. Stable canonical content IDs are not runtime entity IDs. |
| Hidden information excluded | Context visibility gates, allow-listed semantic projection and visible target resolution. Strategic context derives visible aggregates, not hidden armies. |
| Human overrides Commander | Manual observations release/protect actors; bound results cannot substitute/reclaim. |
| Late provider response after reset | Generation and full runtime affinity plus cancellation checks; no admission. |
| Voice has no extra authority | Plain transcript enters same SubmitMessageAsync; normal reset safe, public voice-host reinitialization exception G17 remains. |
| DynamicPlan arbitrary code / unknown JSON | Fixed nine primitives, strict schemas/bounds/types/DAG; no eval/arbitrary calls. |
| Prompt content bypass | Text cannot manufacture local approval or structural scope; arbitrary English meaning is not deterministically provable. Preserve accepted KnownIntent/preview contracts. |
| Proposed finalization changes | Readable previews must not weaken authority equality; selectors remain owned/visible; identity pruning preserves non-reuse; proxy must isolate app-owned keys; saved drafts must not restore approval. |

No new P0 Commander authority breach was established. The voice reinitialization source exception is narrower than normal scene teardown: chat starts once, ordinary scene replacement destroys the host, and disposed guards reject late results. Its public reuse path still needs a targeted fix.

## 16. Live Luna reliability and evidence quality

T09 Submit asserts one call, a non-null result and IsValid, then the Mill case waits for construction. Valid Answer/Clarify/Unsupported can pass that helper and create no goal. The native deadline therefore does not identify which layer failed. No test edits were made.

Full PlayMode job d957a71711a14314a727b6b1da4a4f5f completed 201 tests and reported one Mill deadline failure; its result payload/semantic trace was lost. Later isolated jobs 0a3a0151df9843a1a82b0a5946c60cba and fa99d068d2124d3e9e9df496fd6e6404 passed after normal domain reload. Both were HTTP 200/finish stop, valid Mill-near-worked-resource Request, admitted goal, worker/berry/placement/ordinary command and completed native Mill. Exact ticket/completion tick were not recorded. The failure remains historical and unclassified; it is not proof of deterministic gameplay regression.

G14 calls for an optional paid integration lane, explicit expected semantic-effect checks before lifecycle, and retained bounded stage evidence: outcome/node/normalized effect, admission result, local goal/request identity, HTTP/finish category, first failed lifecycle stage. Preserve the six-call guard and real-provider/native assertions. Do not brute-force a pass or make non-effectful output satisfy a Mill acceptance assertion.

| Evidence | Status | Exact scope / provenance |
|---|---|---|
| Phase 4E acceptance | HISTORICAL GREEN | 879/879 EditMode and 183/183 PlayMode in phase4e-final-report; earlier source snapshot. |
| Phase 4F acceptance | HISTORICAL GREEN | 906/906 EditMode, 183/183 PlayMode, hostile 27/27; canonical-data foundation, not later Q&A truth proof. |
| Phase 4G acceptance | HISTORICAL GREEN | 953/953 EditMode, 184/184 PlayMode, hostile 33/33; T01 shows an incorrect fact can be encoded in a green test. |
| Phase 4H acceptance | HISTORICAL GREEN | Reported 969 EditMode /190 PlayMode, dedicated voice/audio/routing evidence; not physical-microphone certification. |
| Phase 5A acceptance | CURRENT GREEN | Accepted/frozen authority/composition evidence carried forward by matching recorded source bytes; not whole-product RC certification. |
| Current full EditMode | CURRENT GREEN | Accepted reported 1124/1124, job 1ae64b169f774fa2ba628520cf8334d5; matched source, no new run here. |
| Current full PlayMode | PARTIAL | 201 completed, one actual live Mill failure, failed-job result/pass-skip split unavailable; no all-green aggregate. |
| Reconciled PlayMode groups | CURRENT GREEN | Four jobs, 3/3 +17/17 +7/7 +12/12 =39/39 on that continuation; not the full baseline. |
| Phase 5A hostile | CURRENT GREEN | 26/26, 7bfcf00db9274c728fe6237bd5f5e8fa, recorded accepted snapshot. |
| Phase 5A authority | CURRENT GREEN | 1/1, 0528cea78fda4b7c8121c37950c576d0, recorded accepted snapshot. |
| Economy / clarification | HISTORICAL GREEN | Typed modes/source/slot fixtures; five real Luna calls, zero retries in CommanderFix live-luna-economy; later source-matched final affected checks also exist. |
| Result binding | CURRENT GREEN | Phase 5A exact production/source fixtures plus current-source final focused artifacts; G06 identifies uncovered incompatible-count admission. |
| Narrow Phase 4G binding runtime | HISTORICAL GREEN | d1766219 edit 9/9; 974e325d PlayMode1/1, produced/patrol IDs7,8,9 excluded old6, tick901; controlled JSON, not live UI. |
| Current-source voice/economy/result affected XML | CURRENT GREEN | 0cde2f04:5/5; final-source 3284c57a:5/5; focused edit866e8777:48/48; separate snapshots/filters, not summed. |
| Live Luna | ENVIRONMENT-DEPENDENT | Historic scenarios plus two isolated Mill passes; original failed output unavailable, no reliability rate claimed. |
| Windows standalone build | HISTORICAL GREEN | Successful accepted build; current executable/runtime hashes match. No rebuild. |
| Player.log | HISTORICAL GREEN | Current retained startup log counts zero errors/unhandled; no new gameplay proof. |
| Multiplayer | NOT VERIFIED | Local/paired-simulation fixtures and relay source are not actual-peer certification. |
| Manual UI | PARTIAL | Prior 4E standalone visual scenarios; current 5A approval/voice layout not newly exercised. |
| Physical microphone | NOT VERIFIED | Native prerecorded and mock tests do not prove physical device→packaged gameplay. |
| Original obsolete authority expectations | SUPERSEDED | Reconciled historical fixture assumptions; preserve failed history rather than sum snapshots. |

CURRENT GREEN here means recorded accepted evidence matched the source bytes audited, not that this audit ran it. Every row retains its own filter/date/snapshot scope. Do not add these counts into a fictitious single suite.

## 17. Findings and follow-up work

The gap matrix assigns exactly one severity and owner to each finding, plus release gates, code requirement, solution and small/medium/large scope. P1 items concern factual/target fidelity, understandable approval, provisioning/deployment and missing real release proof. P2 items are concrete lifecycle/transport/presentation/retention work. P3 items are optional new product promises or measured optimization. Accepted boundaries and deliberate DSL limitations are classified NOT AN ISSUE.

The six precise work packages follow. They are plans for later reviewed Sol prompts, not implementation authorization from this audit.

### WP1 — Truthful questions, progress and approval

- Problem: age answers contradict current landmark data; natural army-blocker questions have no tactical observation; complex approval exposes DSL internals; generic admission errors obscure actionable causes.
- Why it matters: users must be able to trust factual advice and understand what approval authorizes.
- Findings / severity: G02, G03, G07 are P1; G20/G30 are P2.
- Exact source areas: CommanderChatUI.Questions.cs, CommanderSemanticProvider.cs, CommanderContext/ContextBuilder, CommanderChatUI.Explanations/StrategicControls, CommanderActionPlanCandidate/CommanderPlanPreview, CommanderGoalManager.Requests.cs, canonical Knowledge/LandmarkDefinitions; T01 and question/preview/authority fixtures.
- Architecture constraints: canonical costs/availability by civilization/current age; bounded detached owned progress/reason/resource/queue facts; no hidden information/runtime references. Separate readable preview from a complete deterministic authority comparison. All facts remain read-only.
- Acceptance criteria: Castle/Imperial answers match available landmarks, including unsupported/already-reached cases; army explanations identify actual blocker or explicitly lack evidence; previews describe actors/counts/order/exact producer/new-unit scope/constraints in player language; different semantic scopes cannot compare equal; validation errors describe a bounded remedy.
- Tests required: independent canonical-value assertions across civilizations, read-only question effects, unsupported age, current tactical blockers, context size/fog exclusions, preview-versus-scope equivalence and negative mutation tests. Replace the incorrect T01 expectation with canonical truth rather than weakening it.
- Real-runtime evidence required: packaged UI question → correct facts; blocked production → actual explanation; shared-worker and producer-bound previews understood and approved/dismissed without unintended work.
- Must not change: authority/provenance, automatic narrow KnownIntent contract, confirmation for compound/dynamic/strategy, canonical balance, hidden-state policy.
- Dependencies: release scope/content guide; any new observation must fit existing context bounds.
- Estimated scope: medium.

### WP2 — Faithful action targets and compound quantities

- Problem: named repair/enemy targets cannot be represented, radius metadata is ignored, and incompatible desired-total/result counts can be approved then block.
- Why it matters: existing actions must affect the thing the player requested and distinguish total population from newly produced units.
- Findings / severity: G04/G06 P1; G05 P2.
- Exact source areas: CommanderCapability.cs/Executor, CommanderSemanticRequest/Json/Admission/GraphAdmission, CommanderIntentDto/Validator, CommanderPlanner production/binding, CommanderGoalManager results and dynamic compiler interfaces; T04/T05/T08.
- Architecture constraints: owned/visible typed target filters; game-side IDs only; no arbitrary coordinates, fallback target, result substitution, extra units, hidden target knowledge, or inferred strategic/age work.
- Acceptance criteria: repair a requested owned TC even when an earlier damaged House exists, or explicitly reject unrepresentable specificity; attack requested visible enemy type/group or clarify rather than choose a different enemy; executable point/radius meaning matches preview; exact-new versus desired-total result cardinality is preflighted with fresh baseline, including queued/human production.
- Tests required: named-target ambiguity/ownership/fog cases; repair House-before-TC; nearest non-Archer versus requested Archer; radius unsupported/implemented cases; baseline zero/nonzero, unrelated queued outputs, multiple exact producers, lost/human-controlled results, stale approval and whole-graph atomic rejection.
- Real-runtime evidence required: normal commands with captured target/result identities for repair, attack and build→produce→patrol/assembly with preexisting units/producers. Verify no unrelated actors are commanded.
- Must not change: result loss fails closed; human override; ordinary command/network route; existing nine-primitives scope unless a separate explicit expansion is approved.
- Dependencies: WP1 readable previews; current canonical selector vocabulary.
- Estimated scope: medium.

### WP3 — Voice lifecycle, provisioning and hardware acceptance

- Problem: ignored model lacks reproducible provisioning; notices/hardware proof are incomplete; preview cancellation, disposal, reinitialization and recording/inference limits have specific gaps.
- Why it matters: a fresh developer/end user must be able to install and use the advertised local voice path reliably.
- Findings / severity: G08/G09/G13 P1; G15/G16/G17/G18 P2; G25 P3 documentation alignment.
- Exact source areas: Phase4H VoiceInputController, ChatUI.Voice, Whisper provider/model locator, AudioConverter, VoiceSettings, UnityMicrophoneAudioCapture; public ChatUI.Initialize; Packages manifest/lock; StreamingAssets setup, root README/THIRD_PARTY_NOTICES and voice docs.
- Architecture constraints: microphone consent/preview stays local; transcript enters SubmitMessageAsync; approvals unchanged. Cancelling voice must not erase unrelated user edits. Native wrapper lifetime must use verified package APIs; cancellation/disposal cannot free an actively used native context.
- Acceptance criteria: preview discard clears only its owned draft; Dispose actually invalidates/cancels/releases CTS; Initialize invalidates an old voice session before adopting a new runtime; deadline returns control and observes late faults/results safely; teardown does not synchronously wait for a long native inference; recording/transcription duration policy is consistent and visible. Pinned model acquisition verifies existing SHA; missing model is actionable; exact licenses/notices accompany the package.
- Tests required: cancel/edit/normal Send, Dispose during inference, cancellation-ignoring STT, old voice completion after host Initialize, slow native-wrapper lifecycle, duration boundaries, callbacks/buffer bounds and model hash/provision failure.
- Real-runtime evidence required: empty checkout/cache provision → Windows build → real microphone → transcript edit/preview/cancel/retry → confirmed ordinary gameplay; PTT/rebinding, no-device/permission failure, silence/noise and reset during recording/inference. Voice→DynamicPlan and voice→strategy must still await their normal approval.
- Must not change: no cloud STT dependency or new gameplay authority; no unmeasured switch to a larger model.
- Dependencies: WP1 consent display, WP4 release credential setup for voice-originated AI translation.
- Estimated scope: medium.

### WP4 — Provider distribution and bounded transport

- Problem: public credential ownership/onboarding is undecided; the HTTP body is fully buffered before its response bound; the old operator credential incident needs external closeout.
- Why it matters: developer configuration is not automatically a safe usable public package.
- Findings / severity: G10 P1; G19 P2; G01 historical P0 external incident.
- Exact source areas: provider factory, DotEnvLoader structure, OpenRouterCommanderProvider, CommanderHttpClientTransport in GeminiAIProvider.cs, build/release setup documentation and distribution inventory. Do not read credential contents.
- Architecture constraints: either BYOK with user-owned credentials or a separately authenticated controlled service if an app-owned key funds requests; no reusable app secret shipped to clients; logs/prompts/results never include headers/keys. A proxy is conditional, not mandatory for source/private releases.
- Acceptance criteria: one documented deployment model per artifact, usable setup/disable/error path, no included secret files/literals, streamed byte-limited receive/cancellation and preserved HTTP/finish/parse categories; external confirmation of old session invalidation/expiry without token disclosure.
- Tests required: injected transport oversized/chunked body, timeout/auth/rate-limit/truncation/envelope cases, one bounded schema repair, reset/late completion, metadata redaction. No paid requests needed to prove these contracts.
- Real-runtime evidence required: a fresh end-user setup walkthrough and secret-safe distribution inspection; controlled service authentication/quota/error smoke only if that model is chosen.
- Must not change: provider remains semantic-only; no general semantic repair that invents effects; no audit agent rotation or retrieval of old secrets.
- Dependencies: deployment decision and release packaging; optional external service work requires its own reviewed scope.
- Estimated scope: medium for BYOK finalization; a hosted service would be a separate large deployment package.

### WP5 — Trustworthy acceptance lanes and multiplayer release gates

- Problem: paid semantic variability is conflated with native failure, failed-job output is lost, source manifests depend on dirty paths, and actual peers/version parity remain uncertified.
- Why it matters: release gates must identify the failing layer and the exact bytes/mode they certify.
- Findings / severity: G11/G12/G14 P1; G24 P2.
- Exact source areas: CommanderPhase5ALiveRuntimePlayModeTests, Unity test-run evidence capture/filters, manifest tooling/docs, network/relay version deployment and existing ledger/authority/result fixtures. Production planner behavior is outside the test-infrastructure scope.
- Architecture constraints: keep live/provider lane separate from deterministic offline baseline; no fake live success, raised cap, weakened lifecycle predicate or retries to conceal failure. IDs/traces come from local trusted diagnostics; no provider IDs/headers/raw secrets. Peer compatibility preserves pre-existing source-restricted version-1 encoding without Phase5A provenance bytes.
- Acceptance criteria: expected semantic effect checked before native wait; bounded outcome/node/effect/admission/goal/HTTP-finish/first-stage output persists for pass/fail/inconclusive; full offline runs retain XML/JSON with actual counts; source snapshots include committed files/HEAD, not only dirty paths. Multiplayer RC deploys identical compatible builds and obtains actual equal-checksum peer evidence.
- Tests required: harness negative outcomes Answer/Clarify/Unsupported/parse/validation/admission/lifecycle stall, lost result artifact path, source manifest on clean and dirty checkout; existing deterministic authority/binding/command tests after fixes.
- Real-runtime evidence required: one bounded live integration acceptance run when justified; actual two peers covering workers/source gathering, build/produce/compound/age, movement/scout/attack/patrol/rally, human takeover, pause/resume/cancel/replacement, delayed batches, late provider/reset/follow-up and voice-originated command. Exercise local human, remote human and configured AI. Capture build IDs/logs/checksums.
- Must not change: historical failures remain failures; no arithmetic sum across snapshots; no statement of network certification from two in-process simulations.
- Dependencies: WP1–WP4 fixes frozen, compatible relay deployment; solo-only RC can explicitly defer the multiplayer gate.
- Estimated scope: large for the full multiplayer/public evidence package; test-observability component is medium.

### WP6 — Bounded ownership and match-lifetime retention

- Problem: strategic identity claims retain request/intent objects after archive eviction, historical worker IDs are not comprehensively pruned, and relay history grows/clones without an explicit reconnect bound.
- Why it matters: bounded visible archives do not bound these retained objects over a long match.
- Findings / severity: G21/G22/G23 P2.
- Exact source areas: StrategicIntentIdProvider/StrategicAIRequest/approval bridge/planner archive lifecycle; CommanderWorkerAuthority/UnitRegistry; backend relay.rs/ws.rs reconnect protocol.
- Architecture constraints: keep immutable ownership, monotonic non-reused identities, stale/replay rejection and current outstanding claims. Retiring heavy objects must not authorize an old ID. Worker pruning must retain live human protection. Relay history truncation requires a defined world/rejoin contract, not discarded prerequisite frames.
- Acceptance criteria: bounded active ownership and minimal retired bookkeeping; cancelled/failed request snapshots collect; dead historical units leave protection/control/gather stores; all callbacks detach on runtime disposal; relay reconnect/history policy is explicit and bounded or reconnect is explicitly unsupported.
- Tests required: repeated successful/rejected/cancelled requests, weak-reference collection, identity replay after retirement, repeated unit creation/death and human commands, live reservations retained, match disposal; backend reconnect/history boundaries if changed.
- Real-runtime evidence required: targeted counter/allocation observations for the proven growth paths and match teardown; server reconnect memory/payload observation if multiplayer reconnect is promised. No arbitrary soak duration required.
- Must not change: request-scoped authority, manual takeover, result attribution, normal command execution or replay determinism.
- Dependencies: lifetime protocol design; multiplayer reconnect policy for the backend part. Base-world save/load is not silently introduced.
- Estimated scope: medium.

## 18. Manual QA and external infrastructure

Required for the relevant release mode: physical Windows microphone/voice workflow; actual multiplayer peers and compatible build deployment; new current-source approval/error/target-fidelity UI scenarios; clean provisioning/build/startup; end-user provider setup. Keep native inference latency, distribution licenses and session credential invalidation separately evidenced. Provider availability/billing is external; this audit made zero paid calls.

No Commander implementation is blocked merely by the absence of a broad soak, arbitrary backend, larger STT model, world saves, or a future positioning/exploration family. Evidence gates are attached to an explicit current release promise or a source-confirmed defect.

## 19. Roadmap and audit closeout

The top entry of remaining_work.md now directs: Phase 5A ACCEPTED / FROZEN; next Release Finalization; no Phase 5B. Historical phase information is retained. The gap matrix and six packages are the implementation-prompt input after review.

The only intended changes are this audit, the gap matrix, the current audit evidence manifest, and the roadmap entry. Final branch/HEAD and file/hash checks are recorded in audit-source-manifest.json. No production/test changes, commits or pushes are part of the audit.

Audit requirement coverage:

| Brief sections | Completion evidence |
|---|---|
| 1–5 workflow/phase/source/credentials | Sections 1–3, 10; Git output and byte comparison; redacted scans |
| 6–8 primary/secondary decision and severity | Sections 1, 17; gap matrix fixed severity/owner fields |
| 9 capability matrix | Section 4, every requested capability row |
| 10 player experience | Section 5, all named examples and showcase |
| 11 cross-family composition | Section 6, ten mixed families with actual route/limit |
| 12 DSL limits | Section 7, all six limits classified |
| 13 save/load | Section 8, base support and all eight transient-state cases |
| 14 multiplayer/determinism | Section 9, architecture/local/peer distinction and every action group |
| 15 provider/deployment | Section 10, bounds/repair/errors and three distribution models |
| 16 live reliability | Section 16 and WP5, original failure retained, two isolated passes distinguished |
| 17 voice | Section 11 and WP3, route/state/lifecycle/model/licenses/hardware evidence |
| 18 fresh clone | Section 12 and WP3/WP4/WP5, required ignored model provisioning |
| 19–20 UI/errors | Section 13, current source behavior and specific gaps |
| 21–22 lifetime/performance | Section 14, proven retention versus unmeasured costs/frequencies |
| 23 authority | Section 15, invariant review and constraints on every proposed path |
| 24 credential incident | Section 10, labels/counts only, limitations and external rotation recommendation |
| 25 evidence inventory | Section 16, statuses/filter/source provenance without combined counts |
| 26 required documents | This file and release-gap-matrix.md |
| 27 work packages | Section 17, six packages with all required design/acceptance fields |
| 28–29 Phase5B/roadmap | Sections 1 and 19; current roadmap entry |
| 30–32 verdict/discipline | READY FOR RELEASE FINALIZATION WORK; final response follows requested headings; STOP |

**STOP AFTER THIS AUDIT. Do not implement these packages or begin Phase 5B without a later reviewed instruction.**
