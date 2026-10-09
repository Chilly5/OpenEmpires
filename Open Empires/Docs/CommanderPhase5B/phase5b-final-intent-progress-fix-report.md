# Phase 5B final intent and progress fix — implementation handoff

2026-10-09 — **PHASE 5B FIX COMPLETE — READY FOR LUNA VERIFICATION**.
This closes the three targeted implementation defects. **Phase 5B remains unaccepted.**
Do not start another phase automatically.

## Source and scope

Project: `D:/unity_projects/OpenEmpires/Open Empires`; branch
`unit_models_and_voice_control`; HEAD `20da6eff6b63ebbf82cfd6570bbc416ce1d71832`.
Unity `6000.5.9f1`, instance `Open Empires@6d7310c7`. Existing dirty changes and
independent audit evidence were preserved. No reset, clean, commit, push, package
rebuild, hostile audit, multiplayer change or new phase.

Final [source identity](source-identity.json): **374 records**,
SHA256 `bfaeb894e89f4e326b73f2c763ac1f9fb95ce2bf70ba492ca6428f206f5ab39e`.
The manifest covers runtime scripts, project/package configuration, native data/
scenes and the listed Phase 5B/economy tests—not every asset, meta file or document.
Hash equality identifies source; it is not standalone certification.

## 1. Explicit worker restrictions and sequence

The old provider prompt mentioned IdleOnly but omitted its Request-node constraint
shape, left dependency use optional and contained conflicting New-count/worker/
dependency-limit instructions. The strict parser faithfully preserved supplied
data but could not recover omitted semantic restrictions.

OpenRouter now uses the explicit `ParseProviderResponse` boundary:

- Each Request construction node declares `builders:{state:Eligible|IdleOnly,count:1}`.
  IdleOnly normalizes into the existing typed PreferredWorkers constraint and
  survives DTO admission, structural scope checks and planner worker authority.
  Missing declarations, other builder counts and contradictory constraints reject.
- Compound Requests declare Independent, Sequential or DependencyGraph ordering.
  Sequential requires each predecessor's actual completion dependency. Missing or
  inconsistent declarations reject instead of executing an unordered substitute.
- Internal legacy typed parsing stays separate for existing DTO/native fixtures.
  DynamicPlan retains its constrained grammar and existing explicit approval path.
- The prompt documents exact constraint objects, eight dependency references,
  permitted semantic worker selectors and New counts without adding owned units.
  It does not contain sentence-specific execution checks.

The LLM still supplies bounded semantic data only. Entity/worker/tile selection,
ownership, fog, costs, prerequisites, command generation and authorization remain
game-owned. The model cannot approve its candidate. The live compound fixture
asserted zero goals/commands before explicit game-side approval.

The declarations prevent silent omission at the wire boundary; they do **not**
independently prove arbitrary natural-language interpretation. A model can still
emit a false but schema-valid Eligible/Independent declaration. Expanded independent
fidelity testing remains an acceptance gate; the exact audited wordings passed below.

## 2. Invalid Mill response: captured cause and correction

Bounded diagnostics now report allowlisted field paths and error categories without
values, arbitrary keys, raw replies, credentials or private context. Placement
anchor/relation/ordinal/gap/resource/source failures identify their fields. Other
generic failures include a bounded validation stage/node index; not every possible
malformed response has a uniquely named field.

The first authorized Mill baseline timed out and did not reproduce HTTP-200 schema
failure. A later live attempt with the audited wording did reproduce it:
`field=nodes[0].placement.ordinal;code=conflicting-declarations`.
The model emitted an ordinal on a resource anchor, although ordinal is supported
only for MyTownCenter. The prompt's flat combined placement form was replaced by
disjoint Town Center, Barracks and resource forms, explicitly prohibiting ordinal
on resource anchors. No malformed field was stripped or accepted, and no timeout
or repair permission was widened. The corrected Mill wording then passed and
reached native completion.

The old audit's 249-character raw reply was never retained. Its exact historical
field cannot be retroactively attributed to ordinal. The new captured failure and
its subsequent correction are the causal evidence for this pass; all earlier
failures and timeout records remain intact.

Review also found a compatibility gap: numeric-only repair eligibility used legacy
parsing, which rejected the new declaration fields. Provider repair eligibility
now uses the provider boundary; missing declarations cannot consume a numeric
repair attempt. The same numeric-string fields, one-repair limit and exact-template
comparison remain. First-attempt diagnostics survive the repair HTTP call.

## 3. Truthful per-request building progress

Placed/result-bound construction observes exact request results, so its card now
divides by request `Count`, not global `TargetTotal`. Ordinary global-total goals
retain their owned-total basis. Native completion rules were not changed to make
cards look complete.

Concurrent construction testing exposed a related preflight/native mismatch:
resource-relative planning checked only the footprint, whereas normal placement
also checks its border. Preflight now matches the native border and zero-border
exceptions. It retains the same bounded semantic candidate set; there is no
unrelated-location fallback or native validation weakening.

Coverage includes existing/repeated same-type buildings, two concurrent requests
with distinct native results, compounds, cancellation/global counting, blocked
IdleOnly retry, protected Gold and Human Stop protection.

## Evidence, including initial failures

RED/progress records are retained in [implementation checkpoint](phase5b-final-intent-progress-fix-progress.md):
the original progress RED, provider-boundary RED and
[seven diagnostic/repair compatibility failures](evidence/diagnostic-repair-compatibility-red.json).
The latter failed on missing field diagnostics and both wrong repair branches
before the corresponding production changes.

Final-source focused EditMode job `835c51870b594dfb8fb18800a1598a8b`:
**28/28 passed**, zero failed/skipped, 7.7900889 seconds.
[Full results](evidence/final-source-focused-results.json).
The native fixtures use controlled visible terrain/setup, then ordinary commands
and unshortened simulation construction ticks. They are not parser-only tests.

UI PlayMode job `2aa9c059c6224a7d943338de466c913b`: **1/1 passed**,
actual minimized badge and visible task-Cancel callback. This is narrow UGUI proof,
not exhaustive layout/DPI/player-session certification.

### Actual configured OpenRouter route and native results

No synthetic success response was used in these three cases. Each used the
production provider/transport, bounded controlled fixture context, game-side
approval/admission and normal native commands/ticks. No construction timer,
completion flag or resource value was changed during execution.

| Wording | Normalized result | Native outcome |
|---|---|---|
| Build a Lumber Yard near the woodline with one idle villager | VisibleResource/Wood/Tree; PreferredWorkers IdleOnly; count 1 | Completed tick 871; exactly one PlaceBuildingCommand; card 1/1 with a pre-existing Lumber Yard. |
| Build a Mill near berry bushes with one idle villager | VisibleResource/Food/Berries; PreferredWorkers IdleOnly; count 1 | Completed tick 631; exactly one PlaceBuildingCommand; card 1/1 with a pre-existing Mill. |
| Build a House near my Town Center, then assign one idle villager to wood | House/MyTownCenter; worker SelectedCount/Exact 1/Idle/Wood; dependsOn [0] | Both goals completed by tick 601; one placement and one gather command; no gather before actual House completion. |

[Lumber Yard capture](evidence/final-resource-confirmation/idle-lumber-confirmed.json),
[Mill capture](evidence/final-resource-confirmation/idle-mill-confirmed.json),
[compound capture](evidence/final-intent-live/then-compound-final.json).
Exact successful jobs: `1f201733f7c249d290845127584d86c9`,
`09a8378cba9e46dbac8f3ed5efee11b4`, `481e8ddfa8304d438606c82229ae841e`.
Their individual cases passed; unlike the baseline, none is a zero-case summary.

These fixtures exercise the real provider and native simulation, but are not
standalone player replay or physical UI approval-click evidence. The reported
model is the requested `openai/gpt-6-luna`; no rolling model snapshot is certified.

### Paid accounting

The user explicitly approved four attempts, then ten additional attempts with the
same disclosed endpoint/payload/credential boundary. The original four-attempt
ledger was not reset or raised. The additional scoped ledger conservatively caps
at six of the ten allowed; only **two** additional attempts were used.

Total for this implementation pass: **six actual HTTP attempts**, **zero repairs**:
five HTTP 200 responses (three successful, two safely rejected), and one terminal
timeout cancellation. All semantic HTTP reservations are terminal; submission
reservation bookkeeping is not an outstanding HTTP request. No more calls are
needed and the live opt-in is off. Original protected ledgers/credentials remain
untouched. These counters are attempts, not a claim about exact vendor billing.

An initial reviewer rejection made zero HTTP attempts. A separate accidentally
unfiltered test launch occurred when continuation cleared temporary orchestration
storage. Its unknown/interrupted outcome and correction are fully disclosed in
the checkpoint. It is **not** presented as a full regression pass. Subsequent
launches define explicit filters and assert nonzero/exact lengths in the same call.

## Source groups changed in this pass

- Provider instruction/strict response boundary and initial-trace retention:
  `Phase4A/OpenRouterCommanderProvider.cs`.
- Strict parser/provider declarations/privacy-safe diagnostics:
  `Phase4E/CommanderSemanticJson.cs`, new `CommanderSemanticResult.ProviderDeclarations.cs`,
  new `CommanderSemanticResult.Diagnostics.cs`, shared enum diagnostics in
  `Economy/CommanderSemanticResult.Economy.cs` and constraint-mode diagnostics in
  `Phase5A/CommanderSemanticResult.Constraints.cs`.
- Numeric-only provider repair compatibility: `Phase5A/CommanderSemanticSchemaRepair.cs`.
- Native-border preflight: `CommanderPlanner.cs`.
- Counter basis: `Phase5B/CommanderTaskBoardProjection.cs`.
- New contract/live fixtures and extended native resource-placement tests;
  generated metas, evidence, source identity and repository-root roadmap.

The preceding resource-placement/status implementation's other dirty files and
the TMP fallback asset were preserved; this list does not claim ownership of all
working-tree changes. Luna/Sol read-only inventories/review informed the name/
future-order verification and found the repair/diagnostic compatibility gaps.
Main integration and Unity verification remained parent-owned.

## Luna acceptance and remaining roadmap

1. Verify the final manifest and current Editor source. Existing Windows/Web
   packages predate these fixes; rebuild only in the next authorized acceptance
   workflow before treating packaged replay as current proof.
2. Re-play the three exact commands through the actual player UI. Inspect typed
   restrictions/order, use non-idle alternatives/no eligible idle workers, and
   confirm native placement, Human Stop, cancellation and repeated-building cards.
3. Test false-but-schema-valid interpretation, additional compound/mixed ordering,
   malformed/unknown fields, numeric-only repair and source-specific/fog blockers.
   Do not weaken assertions or reuse a later pass to erase a first failure.
4. Continue the existing Phase 5B clarification, resource-amount, future-unit,
   age/army and reset/stale-runtime acceptance checklist. Native content knowledge
   is not automatic execution support. Gather/Patrol finite watchers are not an
   unrestricted future-action scheduler.
5. No full historical regression or standalone hostile audit is certified here.
   Broader language, multiplayer/Rust, physical voice, save/load and unrelated
   product work remain outside this goal.

Goal accounting at completion: **2 hours 43 minutes 24 seconds**, **1,012,586 goal
tokens**. This is Codex goal accounting, separate from the six OpenRouter HTTP
attempts and not a vendor billing estimate. Goal status is complete.

**PHASE 5B FIX COMPLETE — READY FOR LUNA VERIFICATION. Phase 5B is not accepted.**
