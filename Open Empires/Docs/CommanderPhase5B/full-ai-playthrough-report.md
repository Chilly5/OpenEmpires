# Full AI Commander playthrough — completed match

2026-10-10 — **FULL AI PLAYTHROUGH COMPLETED — DEFEAT**

One normal English single-player 1v1 was played through the rendered Editor UI.
The economy was destroyed, and the match ended through the ordinary Settings →
Surrender control. The actual Defeat overlay appeared; this was not a simulated
result or headquarters-destruction victory. The earlier blocked preflight is
preserved [here](full-ai-playthrough/blocked-preflight-report.md).

## 1. Setup, duration and result

- English versus the displayed HRE opponent “Mansa”; Albion; displayed seed
  `1412823318`; player name “Akbar”. Default single-player difficulty was not
  changed or independently labeled.
- Current Unity Editor `6000.5.9f1`, instance `Open Empires@6d7310c7`, project
  `D:/unity_projects/OpenEmpires/Open Empires`, StandaloneWindows64.
- Branch `unit_models_and_voice_control`, HEAD
  `de9354f91bd4f3ca15ce8e3c5c5efb7682f999f9`. Main identity **376 records**,
  SHA256 `3059cfbf9ef7189e05c60b05fe4ee17cd00ffbb7b21e24dd4782517b45055676`,
  verified before and after play. Main plus seven supplemental hashes matched.
- Normal starting resources: 200 Food, 200 Wood, 100 Gold, zero Stone; one enemy
  AI system and enabled normal bootstrap. No resource grants, spawned units,
  disabled AI, direct admission/commands, timer edits or accelerated tick loops.
- Time scale was freshly observed **1.0**. The normal settings screen showed
  Disable Fog of War, 20x Production, 10x Construction and God Mode unchecked;
  God Powers OFF. Reveal Terrain was already checked and left unchanged: terrain
  visibility should not be confused with disabled unit fog. [Settings screenshot](full-ai-playthrough/normal-settings.jpg).
- Final game clock **29:01**, native match end tick **52253**, winning team **1**,
  surrendered player **0**. Roughly **31 minutes 13 seconds wall time** from
  observer attachment to the actual Human SurrenderVoteCommand; preflight/budget
  waiting and report-writing are separate. [Defeat screenshot](full-ai-playthrough/defeat.jpg).
- Final state: no living controllable owned units, no Food income, 23 Food,
  300 Wood, 150 Gold; still Age 1. The TC survived with 456 health at the last
  pre-surrender read. Defeat was by normal surrender, not fabricated TC destruction.

All gameplay input used Windows Computer Use. The [passive observer](full-ai-playthrough/PassivePlaythroughObserver.cs)
only read the existing provider trace and owned state, observed command events,
and wrote diagnostics. It did not replace the provider or issue commands. It is
detached. The normal Defeat screen remains visible. No second match started.

## 2. Chronological Commander instructions

Times below are approximate game times at first observed provider submission,
with native terminal observations sampled afterward. Screenshots, the complete
displayed transcript and state transitions remain in [passive evidence](full-ai-playthrough/passive-evidence.json)
and [final state](full-ai-playthrough/final-state.json).

| Time | Player request | Interpretation / approval | Actual effect and player outcome |
|---|---|---|---|
| 0:58 | “Put four idle villagers on Food from sheep and two idle villagers on Wood.” | Valid two-node Request; exact counts, idle eligibility, Sheep source and Wood preserved. Explicit visible approval. | Normal SlaughterSheep/Gather orders, resource income; both allocation goals completed by approximately 1:40. **Gameplay-successful** initial split. |
| 1:41 | “Build a House near my Town Center, and a Lumber Yard near the trees.” | Valid two-build Request; TC and visible Tree placement. Explicit visible approval. | Ordinary placements and construction; House #8 completed by sampled tick 7177, Lumber Yard #9 by 7477 (about 4:00–4:09). Population cap increased to 20 and Wood drop-off appeared. **Gameplay-successful**. |
| 2:25 | “Use my Scout to defend the villagers at my base.” | Accepted CapabilityAction without preview. | One ordinary MoveCommand; task stayed working, Scout was subsequently lost, then task failed. No effective protection was established. The failure falsely attributed loss of eligible units to player takeover. **Partially successful interpretation/command, failed objective**. |
| 3:39 | “Advance to Feudal Age.” | Correct Reach Age 2; immediate admission. | Planner gathered prerequisites and placed unfinished landmark #22. Enemy attacks removed builders; task failed by sampled tick 12960 (about 7:12), with a truthful no-reachable-owned-worker construction blocker. **Partially successful preparation; Age 2 never reached**. |
| 5:09 | “Send the next five Villagers from my Town Center to gather Food.” | Valid finite watcher; sole unambiguous TC, count 5, Food/Any, no training invented. Explicit visible approval. | Watched 0/5 births and assigned zero after economy collapsed. Cancelled through the normal visible task button at sampled tick 19758 (about 10:59). **Accepted but not gameplay-completed**; correct waiting and cancellation. |
| 7:02 | “Build a Barracks and train six Spearmen to defend my base.” | **Timed out** at the unchanged 15-second UI/provider deadline. | No Barracks/army goals or commands admitted. By the next observed state all controllable units were gone. **Failed interaction before admission**, not proof that military production is unimplemented. |
| 8:55 | “Why did my Scout defense fail, and can I recover with no villagers?” | Valid information-only Clarify; no gameplay. | Said it lacked tactical status and asked whether recovery meant without training/assigning villagers. Did not explain the already observed Scout loss or give actionable recovery advice. **Unhelpful informational follow-up**, no fabricated action. |

The enemy had already been harassing villagers early; population fell to zero by
the 7:55 screenshot. I inspected blockers, cancelled the now-useless watcher,
minimized Commander, waited while the normal match continued, then surrendered
when recovery remained impossible with zero controllable units and Food below
Villager cost. Later red units were visible near the buildings but were not
individually identified; no hidden enemy state was exported.

## 3. Counts and paid accounting

- **7 new text submissions:** six action objectives and one informational follow-up.
- **2 action requests fully achieved on first attempt** (the split and two buildings).
  Three valid action requests did not achieve their objectives: Scout defense and
  age-up failed; the future watcher was cancelled at 0/5. One action request timed out.
- Native admitted goals: **7**, comprising **4 completed, 2 failed, 1 cancelled**.
  Scope-level request counts are separate from child-goal counts.
- Provider outcomes: **5 valid Request, 1 valid Clarify, 1 UI timeout**;
  **0 invalid semantic responses observed**, **0 repeated/brute-force instructions**.
- Controls: **3 explicit plan approvals**, **1 task-card cancellation**. These are
  ordinary Commander workflow controls, not manual substitutes for its execution.
- Fresh authorized cap: **200 actual paid HTTP attempts**. Observed **7 attempts**,
  **6 HTTP 200**, **1 UI-level timeout**, **0 schema repairs observed**; **193 unused**.
  The timed-out attempt’s final HTTP response status was not exposed. It is consumed
  regardless of billing or server-side completion. [Paid ledger](full-ai-playthrough/paid-http-ledger.json),
  [human authorization](full-ai-playthrough/authorization.md).
- The existing production transport and credentials were used unchanged. No key,
  header, private request context or raw model response was exported. At closeout
  the UI had no pending submission, the match was over and active goals were zero.
  No claim about exact vendor billing is made.

## 4. Manual interventions

**One completed manual gameplay intervention: normal surrender.** It produced the
sole recorded Human command, SurrenderVoteCommand at tick 52253, after all units
were lost and economy could not restart. No manual unit movement, gathering,
building, training, combat or scouting command was issued.

The first settings click timed out without opening the menu or issuing a command;
fresh window recovery and one retry opened it. This was a Computer Use failure,
not a Commander HTTP timeout. Selecting English, starting 1v1, entering text,
approval, scrolling, cancellation and minimizing were normal setup/UI interaction.
No failed Commander objective was bypassed to make it appear successful.

## 5. What worked in normal gameplay

- Exact ordinary opening restrictions and counts were understood on the first call.
- Visible-resource construction chose real sites and completed normally, without
  debug terrain/resources or forced completion.
- Explicit approval prevented compound/future gameplay from starting prematurely.
- Feudal interpretation was correct and its incomplete construction was not
  reported as a completed age.
- The future watcher clearly described that it would not train units, remained at
  0/5 and cancelled through the real UI without taking a new paid call.
- First failures stayed visible; the military timeout did not create a phantom
  successful plan or a misleading army-completed card.

## 6. What felt frustrating or unreliable

The expanded panel covered much of the base, required scrolling to find cancellation,
and showed only a small slice of task history. Reading/approving plans consumed real
combat time. The Scout instruction said only “Understood”; its later status blamed
me for taking control even though there were no Human commands. The critical army
request failed during the raid. The recovery question then lacked the task context
needed for a useful answer. Watching an admitted future task wait at zero was truthful
but offered little help once the economy was gone.

My own play also contributed: I prioritized economy/age-up and requested military
production too late. Tool and reasoning latency slowed reactions compared with an
experienced human. This single loss does **not** establish that Commander cannot
win, that enemy balance is defective, or that every failed task is a production bug.

## 7. Confirmed bugs and first failing layers

### P1 — lost Scout is falsely labeled human takeover

Reproduction: normal 1v1; submit the Scout-defense wording above; issue no manual
unit orders; let the enemy eliminate the Scout. Goal #5 transitions from travel
to Blocked and then Failed with “The player took control of every eligible capability
unit.” Owned Scout absence and zero Human events before surrender contradict that
claim. **First failing layer: native capability observation/status reason.**

`CommanderPlanner.ObserveCapability`, around lines 138–149, counts only living,
owned, unprotected subjects, then reports human takeover whenever the count is zero.
Dead/missing subjects and human-protected subjects are conflated. Fix the reason
classification while retaining lost-actor/protection guards; do not reclaim or
substitute units to hide the failure. Actual defensive effectiveness before death
was not isolated, so no separate pathfinding/combat defect is asserted.

### P2 — causal task question lacks the available tactical context

Reproduction: after the preceding failed defense, ask the exact Why/recovery wording.
The reply admits no tactical status and asks about an unintended restriction.
**First observed boundary: read-only question classification/context projection.**
`CommanderTacticalStatusProjection.IsStatusQuestion` uses a limited keyword set;
the actual sentence lacks those keywords, so `SubmitReadOnlyQuestionAsync` supplies
no tactical snapshot. It is still correctly information-only and cannot start gameplay.
Improve bounded access to relevant owned task/actor observations for natural causal
questions; do not invent enemy information or give answers execution authority.

### P2 — cancellation needs scrolling in the default small task viewport

At the captured 1522×809 Editor window, Game scale 1.3x, the current watcher’s status
and wording were visible but Cancel task was below the viewport. Scrolling exposed
it, and physical clicking worked. **First layer: presentation/layout.** Keep the
current task’s action discoverable while retaining full objective/blocker text.

## 8. Priorities and limitations

- **P0:** none confirmed in this run; no production crash or authority bypass observed.
- **P1:** correct the lost-actor/human-control message. Treat critical-order availability
  as a release/UX risk: preserve timeout/attempt evidence and provide a clear explicit
  recovery/manual-control path. The one timeout is an operational failure, not an
  established parser defect; do not widen retries/timeouts or silently auto-execute
  a fallback to force a pass.
- **P2:** improve contextual read-only explanations and compact task action layout.
  These are separate from the existing progressive-gathering/full-regression gates.

Military construction/production could not be evaluated because its only request
timed out. Mill construction, map scouting, attack execution, later ages and late-game
combat were not reached; they must not be labeled “not implemented” from this match.
No newly requested gameplay primitive was proven unimplemented. Player-wide income,
exact future-result identity and recovery-search acceptance are not certified by this run.

## 9. Overall assessment and next action

Commander was useful for precise opening economy and construction, and bounded
approval/status controls were mostly truthful. It was not sufficiently helpful
under pressure in this run: defense did not protect the economy, the military
request timed out, and failure explanation was misleading or unavailable. Overall:
**useful opening assistant; unreliable situational support in this observed match.**

Give Sol the concrete status/context/layout findings, retain the operational timeout,
and keep gameplay evaluation separate from current-source regression and fresh
Windows/Web acceptance. A later authorized playthrough should react earlier to
raids and test a defensive opening, without erasing this defeat. No production/test
edit, reset, clean, commit, push, regression or rebuild occurred here. Existing work
was preserved, and the source identity still matches. **Phase 5B is not accepted.**
Stop after this report; no next match or implementation phase was started.
