# Phase 5C partial closeout — 2026-10-12

**PHASE 5C PARTIAL — focused command/UI repair verified; full regression, standalone Windows/Web builds and broader live acceptance deferred by the user.**

The user explicitly approved “Yes, close with a partial handoff” after being told that final regression and Windows/Web verification would remain deferred. This completes that revised closeout request, **not** all original Phase 5C release gates. Stop here; no next phase, full playthrough or additional paid call starts automatically. Phase5B remains unaccepted.

## Last problem: why commands sometimes failed

The retained `Logs/Editor-prev.log` shows the two screenshot Food failures received HTTP200 and complete model replies, then failed at semantic **envelope** parsing before an action node. Their exact malformed fields were not retained and remain unknown. Do not blame these failures on missing resources, workers or connectivity. Earlier separate agent attempts really did receive HTTP403/network failure; those first failures remain in the evidence directory.

The over-compressed ordinary contract had been replaced with the preserved detailed typed JSON instructions, keeping bounded canonical context. A fresh three-wording diagnostic then exposed another real model-fidelity failure: valid Clarify responses asked for a missing **Count**, although 6 or 8 workers were explicitly requested. This is not a parser rejection. The final narrow change adds a concrete ordinary AllocateWorkers example and clarifies that a supplied quantity, ordinary assignment without a resource target, and omitted Any eligibility/source must not become missing slots. No sentence-specific executor, relaxed parser, automatic fallback, new model, timeout increase or automatic retry was added.

First failures are retained in `evidence/live-food-shape-1/` and `live-food-shape-2/`. After the final change, `live-food-shape-3/safe-result.json` records:

| Original wording | First-attempt normalized result | Actual prompt / output tokens | Provider attempt time | Reported cost |
|---|---|---:|---:|---:|
| gather food with 5 villagers | AllocateWorkers SelectedCount Exact5 Food | 4178 / 51 | 7094ms | $0.000547675 |
| gather food with 8 villager | AllocateWorkers SelectedCount Exact8 Food | 4179 / 51 | 3081ms | $0.000086765 |
| gather wood with 6 villagers | AllocateWorkers SelectedCount Exact6 Wood | 4178 / 51 | 2316ms | $0.000086640 |

All three were HTTP200, no repair, no missing-count clarification. This small diagnostic is **parse-only**, not three live native gathering completions or universal language reliability. Earlier two diagnostic batches yielded 2/3 and 1/3 expected direct requests; green final results do not erase them.

The user explicitly relaxed the original <4000-token optimization in favor of reliable detailed instructions, permitting roughly6000–7000 input tokens if needed. The observed 4178–4182 tokens are **above the original cap**, not a sub4K certification. The 32768 limit is a UTF-8 payload safety ceiling, not a tokenizer. Cached input remains part of actual prompt totals. Web-gateway prompt usage is still unmeasured.

## Actual game proof, not just JSON

`build a house` passed through the **actual SampleScene Commander singleton UI → production Luna HTTP → strict parsing → deterministic admission → native placement/construction**. Job `c18ef01ad6c5469f85f69ef96b4bef34` passed1/1. One ordinary goal was admitted, exactly one PlaceBuildingCommand issued, completed Houses increased0→1, Wood200→150, and the goal completed at native tick766. The QA setup initially selects an accelerated match, but the test resets timeScale to1 before submission and observes ordinary native progression; it does not use an accelerated tick loop for this House.

House actual usage:4182 prompt/37 output tokens, HTTP200,5222ms attempt time, $0.000060590 reported cost. Full native test duration31.674s includes scene/setup/provider/construction and is not pure network latency. First-native-action timestamp was not captured separately and remains unknown.

An intermediate scene4 run actually admitted the ordinary House but failed because the test incorrectly demanded a preview for every request. Scene5 corrects **only the harness**: staged plans retain separate approval; ordinary single requests use their existing admission rules without manufacturing a second confirmation. Scene4 failure remains retained. A read-only Sol review found no production authority issue; it suggested stronger direct goal-reference correlation for future evidence. This test asserts one new goal, one completed House and one placement, but does not expose the exact UI-submission goal reference as a separate receipt.

## Focused final verification

Native XML leaves, not just MCP counters:

- Live semantic3-wording diagnostic:1/1 test, three correct first attempts; `evidence/final-fix-leaves-20261012-122637-569-32139cdf.xml`.
- Actual UI/native House:1/1; `evidence/final-fix-leaves-20261012-123725-332-4ee9cd21.xml`.
- Focused offline7/7: three detailed-contract wordings; native assignment quantity modes; invalid allocation/source rejection; Mine→yes local continuation; House→next-five→From trees retained graph with separate approval. `evidence/final-fix-leaves-20261012-124418-913-8a58ebfd.xml`, job `49432e50811e4f948a9224bcba14cd22`. The two continuation fixtures assert no second provider call; they are not live Luna tests.
- Top-left compact launcher/wide settings layout:1/1; `evidence/final-fix-leaves-20261012-125311-670-9c7cfdba.xml`, job `1f7b9928e2504079a31e95e4445851cc`. Geometry fixture, not standalone visual proof.
- Current compile finished; zero Unity console errors; git diff --check passed with line-ending warnings only. No full regression was run in this closeout.

These are separate focused gates; do not label them a full suite or aggregate them with older source snapshots.

## UI and carried-forward implementation

The compact launcher now contains the Commander button plus one short status, at the top-left beside the expanded dock. Routine voice/provider setup no longer overlaps the minimized button. Expanded Chat/Tasks, multiline composer, settings, cancellation and separate structural approval are preserved; settings use the wider surface. Initial duplicate readiness text was removed. Connection/access failures show safe transport explanations instead of all being called invalid interpretations. Responses do not force the dock open.

Actual match HUD before/after images are in `evidence/live-house-scene-5/scene-before.png`, `scene-preview.png`, `scene-after.png`. The parent visually inspected the populated final1520×656 capture: task says completed1/1, left player/resources HUD and right minimap remain outside the dock. This does not certify1280×720,1920×1080 or standalone/served-Web views.

Earlier scoped implementation is preserved, not retroactively declared fully accepted: typed compound/name/count/source clarification and zero-call local replies; independent worker/resource quantities; source/protection/human override; defense arrival/actor-state distinctions; Farm incumbent/capacity handling; bounded repeated construction and west-TC woodline; native garrison/ungarrison and fog-safe finite discovery; task cards/status/input ownership; atomic replay/native evidence and optional local metrics. See `task-1-core-report.md`, `task-1-fix-report.md`, `task-2-report.md` through `task-6-report.md`, `recovery-followup-report.md`, and `final-fix-report.md` for their exact scoped tests and first failures. Their historical claims are not final full regression/build evidence.

## Identity and accounting

Branch `unit_models_and_voice_control`; unchanged HEAD `4c9de96adbf6342fbe9e1490fa9b898a0f99caf6`. Dirty work preserved; no commit/push/reset/worktree. Unity6000.5.9f1, instance Open Empires@6d7310c7, active target **StandaloneWindows64**, returned to idle Edit Mode.

Runtime DLL SHA256 `850174ACCD3B65EF438CE05AC2BE2798BF35942CCF4E91870C3081E3A6BA53F0`; loaded Runtime MVID `75dce297-c92b-4bda-a67b-92302cb67a91`. Provider source SHA256 `55787D7F377F8E4B0249882EB9B0D5E5AD20CE4B2B1800DBB825A501683C4D85`; RequestPack `B03EA1FEA0471B16D4E1C518572EFFCEB2720C24BC843FDF57052CA9766C46D2`; CommanderChatUI `620F5D2EF12704B064772E34698E15A5E50C0521A9CFBC18624FA8254A1624B3`.

Generated compatibility identity was refreshed using the existing generator: computed and loaded source_sha256 both `9a283c0c7908ddeadd7df6df07a5936c465ae61f53f79164bf6db1a4e57fa82d`, protocol2. No command encoding change or multiplayer certification. `evidence/partial-closeout-source-manifest.json` records current relevant source/config/meta, Editor assemblies, closeout artifacts and changed legacy-test identities. Existing package artifacts, if listed by the reused generator, are **historical bytes only**, not fresh builds. Manifest consistency is not runtime acceptance.

Agent paid ledger now **15/200 total**: nine shape diagnostics, five House attempts (including cancelled/failed attempts) and the original parse-only House. Three permitted repairs were available per diagnostic but none used. Human screenshot calls are separate. Reported cost for the eleven newest HTTP200 attempts totals **$0.001774150**; earlier missing usage/cost remains unknown, not zero, so this is not total goal cost or an overall cost-per-fulfilled-objective certificate. No audio uploads or credential inspection.

Goal-service accounting sampled before final documentation:20,810,233 cumulative agent tokens and94,442 active seconds (~26h14m), including the long prior goal history. These are **not** OpenRouter model tokens or API billing, and final goal-service totals can be higher. The unexpectedly large cumulative usage is reported without presenting it as necessary or efficient work.

## Deferred checklist for the next AI / independent verifier

1. Read this report, `remaining_work.md`, `closure-matrix.md`, source manifest and first-failure ledgers. Revalidate current source with `./Docs/CommanderPhase5C/refresh-partial-closeout-manifest.ps1 -VerifyOnly`; do not reuse stale4/200 or0/10 historical notes as current accounting.
2. Re-discover tests; run the authorized replacement full **offline** EditMode and applicable offline PlayMode only after source freeze. Exclude ALL opt-in live/paid fixtures (including House and Food diagnostics) and native voice/external-service lanes; never reset/replay a used evidence path. The original Edit run stalled1743/1797:1723pass,19failed,1error,54missing; scoped repaired30/30 is not a replacement full pass.
3. Build fresh Windows64 and Web from the same frozen source, record Runtime DLL/wasm/data/framework/loader identities, and restore Windows64 afterward. No fresh package was produced here.
4. Verify populated minimized/Chat/Tasks/approval/settings/cancel at1280×720,1920×1080 and actual scaled Editor HUD, then standalone Windows and **served Web**. Test browser persistence/download and gateway/operator readiness; do not deploy remote services without authority.
5. Complete the remaining short integrated live checkpoints: original Mine/compound continuations, exact new births/patrol/scouting, finite resources with player-wide income, source/protection, repeated placement, Farms, defense, capacity-limited garrison, fog discovery, cancellation/reset and causal answers. Measure normalized semantics → admission → actual native result/card truth. Three successful assignment parses are not these native completions.
6. Broader model reliability/paraphrases, Windows direct and gateway token/cost/latency, and exact request→goal/first-native-action receipts remain incomplete. Keep historical malformed fields unknown. Do not loosen the parser or accept unrelated fallback actors to force green.

The revised **partial closeout** is complete once this handoff and its manifest consistency are verified. Original full Phase5C acceptance remains open by explicit deferral; no hostile audit was performed.
