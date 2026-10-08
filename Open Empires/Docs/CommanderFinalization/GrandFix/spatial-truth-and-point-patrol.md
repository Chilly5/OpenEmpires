# G05 — spatial truth and point patrol

Focused implementation checkpoint, 2026-10-08. This is not the GrandFix release
verdict or a packaged Windows/Web certification.

## Contract

PatrolArea uses the ordinary PatrolCommand: each selected owned unit patrols
between its starting position and a deterministic game-side point anchor. It is
not a circular route, perimeter, radius coverage, or formation. Move/defend/scout
remain their existing normal point mechanics. The provider cannot supply tiles,
coordinates, hidden enemy bases, bridge positions, or another group's rear anchor.

CommanderLocationSelector.RadiusTiles is nullable. Omission means no radius
promise, rather than a synthetic four-tile default. An explicitly supplied radius
is unsupported and rejected by both intent validation and command creation before
effects. DTO projection refuses to discard one. Structural scope comparison v4
distinguishes omitted radius from any explicitly supplied value; authorization
still does not use preview text.

Semantic JSON rejects unknown radius/perimeter fields. The legacy/direct DTO
CapabilityAction path rejects nonempty parameters bags, even if a key appears
harmless: there is no supported tactical parameter-bag schema. Malformed parameters
are invalid JSON, not an empty bag. Empty tactical bags remain compatible; valid
strategic parameter objects remain supported. This closes nested radius restrictions
being silently converted to a point action, without changing strategic semantics.

Point patrol preview/reason state the start-to-anchor route and do not invent a
radius or perimeter. Explicit invalid typed radius may appear as an unsupported
constraint in developer rendering, but cannot become an admitted action.

## Separate valid building distances

Existing placement semantics are unchanged: left is map-west/negative X, and
clearGapTiles describes approximate footprint-to-footprint separation. The game
chooses the owned anchor, footprint, valid placement and bounded deterministic
tolerance. Failure is a blocker, not unrelated placement. A five-tile building gap
is not a five-tile patrol radius.

## Evidence and verification scope

- Initial RED `4392fc2b`: 11 cases, 4 passed and 7 failed;
  `spatial-red-4392fc2b.xml` retained.
- First affected GREEN `3fdc731a815348beaa16e5701b556b8d`: 106/106 EditMode,
  97.8025777 seconds; `spatial-first-green-3fdc731a.xml` retained. This predates
  the direct DTO review fix and cannot certify that fix.
- Read-only Sol review `g05_spatial_review` identified the ignored direct DTO
  parameter bag; no second concrete scoped defect. Review is not runtime proof.
- Direct DTO RED `fc009cad250f4fd5be7226e7a93ae0a9`: 7 cases, 2 passed and
  5 failed. Object restrictions, array and string bags were accepted as point
  patrols. `spatial-dto-red-fc009cad.xml` retained; empty tactical and strategic
  compatibility passed.
- Final affected GREEN `0a1722d4f88145e6af1127474d866a8b`: 113/113 EditMode,
  100.1200033 seconds, zero failed/skipped; `spatial-final-edit-0a1722d4.xml`.
  Suites: SpatialTruth (18), Scope (13), Phase4GCapability (14), Phase4GHostileAudit
  (39), Target (29). This is affected regression, not an independent hostile audit.
- Native GREEN `5d76f13fbfb54725a161178b4cbba6f2`: 6/6 PlayMode,
  2.4222143 seconds, zero failed/skipped; `spatial-final-play-5d76f13f.xml`.
  Suites: GrandFix Status (1), Quantity (2), Target (2), Phase4G ResultBinding (1).
  The last scenario uses ordinary construction/training and dispatches a point
  PatrolCommand with exactly the three newly produced Spearmen, excluding the
  pre-existing unit. It verifies command/native production attribution, not a
  completed patrol circuit or physical player input. No forced post-order results.
  No full historical regression was run.

## Remaining gates

Natural-language preservation by the actual provider is not proved by prompt
wording or strict JSON tests. Independently test radius/perimeter/circle/bridge/
hidden-base/rear-group requests and require clarification or unsupported outcome,
not an approximate point action. Paid semantic guard remains a prerequisite to
any live-provider call. Packaged Windows and served current-source Web previews,
ordinary patrol/input scenarios, and independent exhaustive verification remain
open. No real microphone, online ASR, deployment or paid calls were used here.
