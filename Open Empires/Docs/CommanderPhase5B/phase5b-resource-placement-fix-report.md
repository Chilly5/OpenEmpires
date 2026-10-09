# Phase 5B resource placement and truthful status fix

Date: 2026-10-09. Implementation closeout; **Phase 5B acceptance remains Luna's responsibility.**

## Baseline and scope

Checkout `D:/unity_projects/OpenEmpires/Open Empires`, branch `unit_models_and_voice_control`, HEAD `20da6eff6b63ebbf82cfd6570bbc416ce1d71832`. Initial worktree changes were `remaining_work.md` and the untracked realistic-playtest audit/evidence. They were preserved. No reset, clean, commit, push, multiplayer encoding changes, new phase, paid provider call or full regression. Unity MCP instance `Open Empires@6d7310c7`, Unity `6000.5.9f1`, was live and idle at entry.

This fixes the common construction anchor mismatch and task-status presentation. It does not redo Phase 5B acceptance or claim that old packaged binaries contain this fix.

## Root cause and reproduction

The ordinary BuildStructure contract could represent a worked resource but not a merely visible resource. Consequently the audited Lumber Yard requests were normalized into an unnecessary worked-resource requirement. Both failed in resource-anchor resolution, **before builder selection**. This was not a demonstrated omitted-worker-count defect.

Controlled tests started with the same visible, unworked Wood node, sufficient resources, and a living idle villager. The audit's payload was supplied directly, not obtained from a new live LLM call. Both original wordings were retained as request provenance. Captured fields: `anchor=WorkedResource`, `relation=Near`, `resource=Wood`; constraints omitted versus `PreferredWorkers IdleOnly`. First failing stage: `AnchorUnavailable`, reason “No visible resource node currently worked by an owned villager matches the request”; no PlaceBuildingCommand. The original audit contains the real packaged-player observation; these fixtures reproduce its inferred normalized path without claiming fresh provider JSON.

## Fix and changed files

The ordinary path now supports `VisibleResource` separately from `WorkedResource`:

- Woodline: `VisibleResource / Wood / Tree`.
- Berry bushes: `VisibleResource / Food / Berries`.
- Explicitly worked berries: `WorkedResource / Food / Berries`, still strict.

Optional typed `sourceKind` prevents berries being replaced with a carcass/farm. Existing visible-resource selection resolves the nearest matching visible, nondepleted source to the oldest owned Town Center; squared center distance and resource ID break ties. It does not search hidden nodes. If this chosen anchor has no legal nearby placement it blocks, rather than switching to an unrelated location. The existing Near candidate generator is reused: nominal one clear tile, cardinal sides, bounded gaps 0–3 and lateral offsets ±2, with native footprint/buildability, visibility, map bounds and builder reachability checks. Concrete resource, tile and builder IDs stay game-side.

Omitted workers use the existing eligible-builder policy. Ordinary placement chooses one builder, so `PreferredWorkers IdleOnly` preserves “one idle villager.” Protected-resource floors and human protection remain effective during retries. No gather command is inserted to manufacture a worked anchor. Ordinary PlaceBuildingCommand and native construction advance the building; no completion flags are forced.

Runtime changes, grouped by responsibility:

- `Phase4A/OpenRouterCommanderProvider.cs`, `Phase4E/CommanderSemanticProvider.cs`: provider contract and detached capability declaration.
- `Phase4E/CommanderSemanticRequest.cs`, `CommanderSemanticJson.cs`, `CommanderSemanticAdmission.cs`: typed selector/source and strict parsing/admission.
- `CommanderIntent.cs`, `CommanderGoal.cs`, `CommanderIntentDto.cs`, `CommanderIntentValidator.cs`, `CommanderIntentResolver.cs`, `CommanderGoalManager.cs`: preserved source/placement through DTO round-trip, validation and ordinary goal creation.
- `Phase5A/CommanderScopeEquivalence.cs`, `CommanderGoalManager.Requests.cs`: structural source equality and authority version 7; scope mutation cannot swap berries for a farm.
- `Phase4E/CommanderSemanticReferenceResolver.cs`, `CommanderPlanner.cs`: reuse visible/strict-worked selectors and the existing construction planner.
- `Phase5A/CommanderActionPlanCandidate.cs`, `CommanderPlanPreview.Descriptions.cs`: readable resource/source preview.
- `CommanderResponseGenerator.cs`, `Phase4A/CommanderChatUI.cs`, `CommanderChatUI.Presentation.cs`, `Phase5A/CommanderChatUI.ActionPlans.cs`, `Phase5B/CommanderTaskBoardProjection.cs`, `CommanderChatUI.TaskBoard.cs`: Accepted acknowledgement, separate Awaiting approval/Working/Blocked/Completed/Failed presentation, silent current-request top-status refresh and blocker as the first card row. New independent messages/reset clear tracking; unrelated active tasks remain untouched.

The reported compound completion mismatch was tested before changing completion aggregation. Existing `allComplete` semantics were retained: the actual compound Mill→worker assignment does not complete before native Mill completion. Only blocker selection now prioritizes a blocked/failed sibling's actionable reason.

Tests changed/added: `Assets/Tests/EditMode/CommanderPhase5BResourcePlacementFixTests.cs`, `CommanderPhase5BTruthfulStatusFixTests.cs`, the existing task-board Pending-label assertion, and `Assets/Tests/PlayMode/CommanderPhase5BTaskBoardPlayModeTests.cs`. Unity-generated TMP fallback and editor settings changes were retained, not cleaned/reset.

## Focused evidence

- Initial RED `e2eff7ee5b84457aae017bf0ee94ad07`: 12 cases, 8 expected parser failures; paired audit reproduction and House/Barracks controls passed.
- RED `e2cebc430da04effb77af26183994c3e`: 20 cases, 8 failures, including lost DTO placement and six intended status/UI failures. A worked-resource fixture failed because it fabricated Gathering state; corrected to a real ordinary GatherCommand plus a separate eligible idle builder.
- A stale-assembly zero-test result and a TMP test-assembly compile error were **not** counted as proof; reflection corrected the harness, then real RED was observed.
- Intermediate `48f1c829585747e99edd7e7e54b45875`: 92 cases, one obsolete `Waiting` label expectation; intentionally updated to `Accepted`, not an execution assertion weakened.
- Final affected EditMode `3e25b86b8e324425a19f94b95fc9b715`: **95/95 passed**, zero failed/skipped, 69.6441816 seconds. Exact test names/results and filtered native command/status traces are in [EditMode evidence](resource-placement-fix-editmode-results.json). Only repetitive SyncCheck lines were removed from captured output.

Affected fixtures: resource-placement fix, truthful-status fix, Phase5B task board/provider context, Phase4E2 semantic-placement contract, GrandFix structural scope and readable preview. Not a historical full suite.

Native Lumber Yard and Mill A/B cases each placed building #1 at `(137,136)` with builder #0, bound that exact foundation, and completed at simulation tick 631 for both omitted and IdleOnly constraints. The native construction loop did not shorten construction timers or force completion. House/Barracks controls also completed through normal ticks. Additional cases cover explicit worked-source recovery, protected Gold, missing source/builder, hidden source, human protection, idle-only retries, incompatible/injected placement fields, no legal nearby footprint, DTO source preservation, source mutation rejection and native compound completion.

The controlled simulations are **native game-system fixtures**, not a fresh standalone match or live Luna translation. Tests initialize terrain, resources, visibility and health; thereafter production commands and simulation systems execute construction normally.

Final PlayMode `64bd8bbb44b74d5da273729c09f8aeb4`: **3/3 passed**, zero failed/skipped, 1.5260103 seconds. [PlayMode evidence](resource-placement-fix-playmode-results.json) contains exact tests: active-goal badge/cancel without forced opening, reset/fresh-card cancellation, and blocked-reason-first-row geometry wholly inside the initial viewport. An earlier two-test run predated import of the new geometry test and was not used as geometry proof. One narrow read-only Luna source review found no critical/important issue; it did not claim runtime/provider verification.

The refreshed [source identity](source-identity.json) records current fix baseline HEAD and original implementation baseline separately: **370 records**, aggregate SHA256 `beabf07553a67113b82833cffaa0cb2cc6b7e7690907d254ce326952a7bb06cb`. `refresh-source-identity.ps1 -VerifyOnly` passed after final PlayMode. Diff whitespace check passed with Windows CR-at-EOL handling; a diagnostic override that treated CRLF as trailing whitespace was discarded, not fixed by rewriting unrelated files. Final console had zero error entries; editor idle/EditMode; active target `StandaloneWindows64 / Standalone / Player`. Hash equality proves source identity, not provider or packaged gameplay acceptance.

**PHASE 5B FIX IMPLEMENTED — READY FOR LUNA RE-PLAYTEST**

## Remaining independent Luna checks

1. Use this source in the editor or rebuild packages first. Existing `Builds/CommanderPhase5B-Windows` and Web packages predate this fix; **no Windows/Web build was rerun in this narrow pass**. Runtime code uses existing platform-neutral paths and no new package/API dependency, but updated packaged compatibility is not certified here. Keep Windows64 active.
2. Repeat the exact Lumber Yard omitted/one-idle wording in equivalent controlled native player states. Record actual provider normalized anchor/source/constraints, goal ID, native command, worker, placement and native completion.
3. Repeat Mill/visible berries, explicitly worked berries and protected-Gold wordings. Check visible-but-unreachable or crowded anchors produce honest blockers instead of dropped restrictions.
4. Check Accepted is not mistaken for construction underway, approval preview remains readable, current request transitions to Working/Blocked/Failed/Completed, and the actionable blocker is visible without scrolling at real player resolutions. Minimized startup must remain minimized.
5. Repeat compound build→assign, cancellation, manual takeover and match-reset cases alongside unrelated active tasks. No generic pause was added. A terminal request evicted from retained history can leave its last top-status text historical, but cannot attach it to another request.
6. Maintain existing authorized provider limits (maximum six semantic HTTP attempts, including repair attempts), with exact accounting and no credentials in evidence. This fix run made **zero paid calls**. Do not relabel older package/test evidence as fresh provider proof.

Stop after this handoff; do not automatically begin another phase or mark Phase 5B accepted.
