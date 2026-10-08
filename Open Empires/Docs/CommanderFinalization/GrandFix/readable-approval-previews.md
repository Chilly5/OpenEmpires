# G07 — readable approval previews

Focused implementation verified, 2026-10-08. The independent structural authorization
comparator is already version 4; wording does not determine authority. This slice
changes presentation adapters only, not game data, planners, command encoding,
approval affinity or the automatic KnownIntent policy.

## Player-facing contract

The preview lists every effect before any work starts. Quantities distinguish
desired total (with the current expected newly-produced quote) from fixed new
production. Worker SelectedCount, Additional, TargetTotal and AllMatching use
different ordinary descriptions; AllMatching is a one-time eligible snapshot.

Dynamic composition uses numbered effect steps, selections and locations instead
of provider symbolic IDs, primitive IDs, raw parameter keys or result syntax.
Each effect points to its frozen worker group, placement, producer and dependencies.
Partition descriptions state the positions in their parent selection, so the
two-on-Sheep/third-on-Mill assignment is understandable and non-overlapping.
Pure selection nodes still disclose count, eligibility, ownership/visibility,
current resource, exact source type and dependency. Placement includes the real
anchor, map relation, footprint gap and bounded tolerance/blocker policy.

Exact producer/result links name the prior building or newly produced unit family,
step and count where applicable, and explicitly forbid unrelated substitutions.
Negative/shared constraints disclose no new/resumed construction, idle-only
preparation workers, protected gathering floors, producer queue cap and restricted
resource sources. Preparation remains bounded to canonical prerequisites; no
unrelated strategy is authorized.

The renderer reads detached typed candidate values only. It does not select or
reserve workers, observe hidden targets, run the planner, issue commands, or approve
anything. Unsupported typed cases throw rather than invent descriptions. The
existing 8192-character limit remains; no silent truncation. Runtime identity and
single-use/stale/reset/cross-owner approval checks remain game-owned and unchanged.

Compact approval already uses a masked viewport, vertical scroll content, wrapping
plain text and content sizing. Details opens the existing expanded conversation,
not approval. Source/UI-component evidence is distinct from actual packaged/DPI
usability: the latter remains an explicit integration gate.

## Current evidence

- RED `d683c2babacb488e82dfa9b92ba133d4`: 10/10 failed for raw enum/ID wording,
  missing understandable bindings and worker quantity semantics; complete XML
  `preview-red-d683c2ba.xml` preserved.
- First affected `7e3f453060e8419286a7bedc56b3ed48`: 129/130 passed;
  `preview-first-green-7e3f4530.xml` preserved. The remaining assertion expected
  the internal `TownCenter` label; updated to canonical friendly “Town Center.”
  Structural target equality/difference assertions remain intact.
- One scoped read-only Sol review `g07_readable_review` completed. Confirmed findings: legacy
  Increase with omitted count must disclose its actual +1 default; result-bound
  rally must not describe default Barracks when it targets another built structure;
  shared resource-source restrictions absent from effect copies still need display;
  typed omitted placement gaps mean one tile; multiple exact new producers require
  all buildings; selected structures must be visible and completed as well as owned.
- Follow-up RED `9babf89c7abb465a8ba68e10e9119e0d`: 4 cases, 1 passed and
  3 failed, 6.613075 seconds; `preview-review-red-9babf89c.xml`. Wrong rally,
  omitted +1 and imprecise selected-group-center descriptions reproduced.
  The real UI-component scroll/details-no-approval case passed.
- Follow-up RED `5b9ff4bb83644e3aa3ef5c6888e6fdec`: 4/4 failed;
  `preview-shared-red-5b9ff4bb.xml`. Shared source, default gap, all-producer count
  and selected structure visibility omissions reproduced. All scoped review fixes
  are in one implementation pass, with no re-review or whole-repository hostile audit.
- Final affected GREEN `9be02e17ef7f4bf3b7cd115682290970`: 137/137 EditMode,
  145.3924647 seconds, zero failed/skipped; `preview-final-edit-9be02e17.xml`.
  Covers ReadablePreview (16), GroundedScope (8), Scope (13), SpatialTruth (18),
  Target (29), Quantity (21), Presentation (32). All review fixes and the real
  scroll-card/details-no-approval case pass.
- Additional dynamic compiler/runtime/authority XML: 39/39, 38.4926345 seconds,
  zero failures; `preview-core-recovered-4e634fc5.xml`. The launched MCP job
  `4e634fc5da244d76b2f1103432d78eeb` became unknown after an interruption/reload.
  Recovery confirmed editor idle/no runner, and retained durable XML with exactly
  the requested three fixture names, start `2026-10-08 11:12:25Z`, end
  `2026-10-08 11:13:04Z`. XML is the authority for the 39-case result; its own format
  does not encode the lost MCP handle. No duplicate run or source edit occurred.
- Native GREEN `c90c75d2715e4f1689c9fe56625ac57f`: 6/6 PlayMode,
  2.5509765 seconds, zero failed/skipped; `preview-final-play-c90c75d2.xml`.
  Status (1), Quantity (2), Target (2), Phase4G ResultBinding (1): normal construction,
  training, exact produced-unit patrol dispatch, requested repair/combat and grounded
  progress continue on changed source without forced post-order outcomes.
- `preview-checkpoint-20261008.json` pins the tested source; current working
  manifest is refreshed separately. Byte consistency is not release acceptance.
  A fixture-only TMP reference error was caught before any
  run; test uses the existing real component through reflection, no new dependency.

## Remaining gates

Actual Windows/current-source served Web approval scrolling/legibility at 720p,
1080p and available high-DPI settings; physical input/voice preview separation;
independent exhaustive authority and presentation verification. No paid semantic
HTTP, online ASR, microphone capture, deployment or full historical regression is
part of this checkpoint. The full GrandFix objective remains active.
