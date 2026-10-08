# GrandFix correctness and authorization — working report

2026-10-08, `unit_models_and_voice_control`, preserved HEAD
`8586764e644240f93c99e2afa1c7d41c697cc879`, dirty implementation checkout.
This is an evolving checkpoint report, not the final release verdict. Refer to
`grand-fix-source-manifest.json` for the latest committed-plus-working inventory;
historical checkpoint hashes identify their own tested bytes, not all later edits.

## Authority invariant

Player text/reviewed transcript → bounded untrusted semantic data → game-owned
scope/approval → strict deterministic compilation → existing planners/goals →
ordinary ICommand/CommandBuffer → GameSimulation. Provider output never supplies
trusted runtime identities, coordinates, commands, approval or executable code.
Content IDs and graph-local references are not runtime entity identities.

Single narrow KnownIntent retains its established automatic route. Dynamic and
compound effects still require the exact local approval. Preview is not consent;
transcript Send is not plan approval. Owner/runtime/request/generation/strategy
revision and live lease are checked independently of wording. Confirmation remains
single-use, stale/reset/cross-owner candidates fail closed, and human takeover wins.
No new autonomous human strategy or Phase 5B is authorized.

## Current slices and evidence

| Finding | Implemented contract | Focused evidence and limits |
|---|---|---|
| G02 | Age answers derive actual available canonical landmark transitions; distinguish current/already-reached age and transition versus cumulative cost | Canonical checkpoint RED→39/39 GREEN `68afcd1e`; final independent/package proof pending |
| G03 | Detached owned tactical snapshot, fresh local answers, bounded relevant request/step selection; questions neither admit work nor cancel existing work | [Status report](tactical-status-and-read-only-questions.md): 109 Edit/6 native Play `0ae9492f`/`d9492d31`; hidden generic/typed targets fog-gated, observation does not expire reservations/protection |
| G04 | Separate actor and typed target, canonical enemy-owner unit family, owned building selection and sticky dispatch identity | [Target report](named-target-fidelity.md): 104 Edit/3 native Play `676ccf9f`/`b2a386ec`; absent/lost/hidden target never becomes unrelated entity; ordinary combat may retarget after dispatch |
| G05 | Omitted radius means point anchor; explicit unsupported radius/parameter restrictions reject, not approximate; patrol is start-to-anchor, not perimeter | [Spatial report](spatial-truth-and-point-patrol.md): 113 Edit/6 native Play `0a1722d4`/`5d76f13f`; provider extraction/package/full circuit remain unverified |
| G06 | Whole-graph total/new result compatibility, fresh commit baseline, attributed receipts and pending-origin accounting, no old/human-unit fill | [Quantity report](production-quantity-preflight.md): 117 Edit/5 native Play `348cf452`/`1643bf1a`; material new-count changes need a new preview; ordinary resource waiting does not widen or invalidate scope |
| G07 | Explicit structural comparison v4, typed fields/constraints/dependencies/producer/results/dynamic program/quantity quotes separate from readable typed adapters | [Preview report](readable-approval-previews.md):137+39 Edit/6 native Play `9be02e17`/recovered`4e634fc5`/`c90c75d2`; complete scroll-card and Details-no-approval checks; packaged layout/input remains open |

Those are affected-source checkpoints, not a full historical regression or an
independent hostile audit. Subsequent edits require source-current consolidated
checks and same stabilized Windows/served Web builds. Test results, build success,
manifest byte consistency and physical/runtime acceptance are separate claims.

## Scope distinctions kept intact

- Placement “left” is map-west; building distance is approximate edge-to-edge
  footprint gap within bounded deterministic tolerance. Point radius rejection
  does not change legitimate building placement semantics.
- Typed target specificity is distinct from owned actor selection, visible location
  and exact prior-node results. Destroyed/changed-owner/stale/cross-runtime/human-
  controlled exact results cannot be substituted by a similar entity.
- Worker SelectedCount assigns the requested eligible workers, Additional adds
  that many, TargetTotal means desired total, AllMatching is a one-time snapshot.
  Shared frozen partitions cannot overlap and restrictions do not authorize extra
  effects. Future prerequisite preparation stays within the accepted effects.
- Total production counts existing units and eligible queued/in-flight production;
  exact-new result consumers require the quoted attributable cardinality. Human
  queues are not exact-new receipts. Missing/lost results remain truthful blockers.
- Questions use detached observations, not planner execution. Missing evidence is
  unavailable/clarification rather than an invented resource cost or target outcome.
- Preview may become more readable/localized without changing equality. Conversely,
  similar wording does not permit changed quantity, source, target, negative
  restriction, producer/result reference, dependency or candidate affinity.

## Explicitly open

Full G20 actionable errors and G30 unified setup guide; G10 safe provider setup;
G14 paid transaction/stage diagnostics; G21/G22 identity/worker cleanup;
G11/G12/G23 actual peer/protocol/reconnect policy; G26 measured workloads;
final UI/input/DPI/current-package verification; all mandatory measured Windows/
Web recognition/capture/gateway and browser-local requirements. No semantic or
online-ASR transaction is authorized before its run-wide evidence budget guard.

G01 remains operator credential invalidation/expiry confirmation, with no values
retrieved. G27 formations, G28 frontier autonomy and G29 world saves remain optional
explicit deferrals. Bridge/hidden-base/rear-of-another-group requests and unsupported
spatial/perimeter behavior must clarify or reject, not invent locations.
