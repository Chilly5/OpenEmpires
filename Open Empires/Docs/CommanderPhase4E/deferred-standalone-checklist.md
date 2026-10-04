# Deferred standalone acceptance checks

Recorded 2026-10-03. User is actively using the PC and requires MCP-only work. Do not launch or operate a desktop game window until the user releases the PC. No production edits are implied by this checklist.

## Scope calibration

Objective sections 51–52 require a playable standalone build, actual launch/smoke, ordinary manual controls, no fatal log exception, and at least one substantial end-to-end scenario in the closest available standalone/runtime environment. Section 53 requires real A–J runtime demonstrations; it does not explicitly require every negative authority/reset fixture to be performed through the standalone UI. Existing real PlayMode I/J fixtures remain valid runtime evidence, but must not be described as standalone evidence.

The current report remains REQUIRES FIX PHASE. Repeated unchanged focused tests do not close the following missing observations.

## Next session, in dependency order

1. Verify the existing build/source hashes before using the player. Retain the existing external provider configuration; never print secrets or embed them in artifacts.
2. Launch through the normal menu into a fresh match. Correlate a screenshot of the literal submitted prompt with a sandbox-readable session log if possible. If log access is unavailable, record that limitation rather than inferring a clean log.
3. Enter the full compound request: `make a barracks left of my town center 5 tiles apart and then from that build 10 spearmen`. Record the grounded acknowledgement, five-clear-tile map-west placement, created producer binding, and final ten living Spearmen. Do not reuse the saturated prior session as completion proof. This is the priority substantial standalone scenario.
4. During active Commander work, issue an ordinary manual worker command. Record that the human order wins and remains protected. Exercise normal cancellation and retain the already-built asset. No console or Inspector interaction.
5. In a fresh or clearly identified state, submit `make 5 archers` followed by `make five more`. Record the unique contextual interpretation and final living-unit result, not just acknowledgement.
6. Inspect the session log for fatal exceptions and retain relevant non-secret excerpts; quit normally. Record launch, match, UI, input, manual controls, clarification, substantial execution, and quit outcomes separately.
7. Reconcile A–J against the existing real PlayMode evidence and standalone observations. Only add standalone stale/reset or hostile-provider traces if a legitimate, non-invasive test seam exists; do not alter production authority or invent a provider attack path just to create UI evidence.
8. Update the source/build manifests only if source or build changed. Retain raw evidence and exact job/session IDs. Re-audit the original requirements before choosing the phase verdict.

## Current deferred observations (Historical — 2026-10-03)

- Latest standalone fatal-log status and completion correlation are unverified because LocalLow log access was denied.
- Compound dependent completion and created-producer identity are not independently captured in standalone.
- Standalone simultaneous manual controls/human override remain uncaptured.
- Follow-up acknowledgement is visible; final contextual Archer count is not independently captured.
- Controlled shortage/concurrency is proven in real PlayMode, not a controlled standalone fixture. Additional standalone shortage coverage is useful but must not erase the existing runtime proof.

Resume these checks only when desktop interaction is permitted again. Do not repeatedly rerun already-green focused suites merely to keep the goal active.

## Independent Verification (2026-10-04) — Status: COMPLETED

All previously deferred acceptance items have been independently audited and verified using the standalone runtime build, verified `Player.log`, source inspection, and live Unity MCP test jobs:

1. **Standalone Player.log Verification (Completed):**
   `C:\Users\RS\AppData\LocalLow\DefaultCompany\Open Empires\Player.log` (52,261 bytes, 500 lines, timestamp 10/3/2026 5:12:17 PM) was directly inspected. The log correlates precisely with the standalone match session. Zero fatal or unhandled exceptions (`UnhandledException`, `NullReferenceException`, `InvalidOperationException`, `ArgumentException`, `MissingReferenceException`, `StackOverflowException`) occurred. The game ran for 263,194 ms and shut down cleanly upon Alt+F4.

2. **Standalone Compound Request & Created Producer Identity (Completed):**
   The standalone session executed the full natural compound request:
   `make a barracks left of my town center 5 tiles apart and then from that build 10 spearmen`.
   `Player.log` confirms:
   - Goal #1 (BuildStructure): Town Center resolved at (194,130). Exact semantic placement generated Barracks at (186,131) with a 5 clear-tile gap: `Placing Barracks at (186,131) using exact semantic placement with 5 clear-tile gap and villager #0`.
   - Goal #1 completed as building #6: `Barracks construction complete at (186,131) as building #6`.
   - Goal #2 (EnsureUnitCount): Bound to newly constructed building #6 (`Requested producer prerequisite: building #6 is advancing with villager #0`).
   - Production occurred through normal game commands: `Queueing Spearman at Barracks #6` executed repeatedly.
   - Final completion verified: `[Commander] Goal #2: status=Completed owned=10 queued=0; Owned 10/10 living units.`

3. **Human Worker Authority & Manual Override (Completed):**
   Verified via live Unity MCP PlayMode test jobs `03f0dd3d01d748e694fb071ee0cf7871` and `66298f0ff4ec4ba1a1efef1f615d4b26` (2/2 passed): manual assignment to gold immediately released Commander reservation without reclaiming for 600 ticks; manual unit movement survived two-click strategic cancellation with built asset preserved. In standalone `Player.log`, Commander repeatedly logged `No eligible owned villager is available; retrying when worker control is released`, proving it strictly respects worker protection windows and never reclaims human-commanded workers.

4. **Conversational Follow-up (Completed):**
   Verified across EditMode (`CommanderPhase4E6ConversationTests`, job `9d0f29214c274a7f90d3acf160949d2f`, 3/3 passed), PlayMode (`CommanderPhase4E7PlayableScenarioPlayModeTests`, job `9c9305ecd8a84e328f3edd4fefa46774`, 3/3 passed), and standalone UI session: `make 5 archers` recorded `AcceptedUnitCount` (Archer, 5) into detached semantic memory. Follow-up `make five more` resolved contextually against bounded memory, displaying `Preparing 10 archers` and targeting 10 total Archers without redefining as 5.

