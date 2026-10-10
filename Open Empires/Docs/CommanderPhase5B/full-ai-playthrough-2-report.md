# Full AI Commander Playthrough 2 — final discovery report

**FULL AI PLAYTHROUGH 2 COMPLETED — DEFEAT.** Normal human surrender at **115:30**, in Age3, with 52 Villagers and 26 Spearmen alive. The user requested closeout before the final patrol/cancellation/attack checks. Coverage is partial; this is neither a fair-match balance test nor Phase5B acceptance.

## 1. Setup and exact starting advantage

Current Editor source remained HEAD de9354f91bd4f3ca15ce8e3c5c5efb7682f999f9, manifest **376 records**, aggregate **3059cfbf9ef7189e05c60b05fe4ee17cd00ffbb7b21e24dd4782517b45055676**, verified at setup and closeout. Existing production/test changes were preserved. This task made no production, civilization or Commander-authority edits, and ran no regression, rebuild, reset, clean, commit or push.

| Session | Conditions | Outcome |
|---|---|---|
| Initial extra-army session | English Marco vs English Vlad, Medium enemy AI, Albion Lowlands, seed −1215740081 | Interrupted; user surrender7:53.73; four HTTP attempts. |
| First authorized AI-disabled retry | English Gediminas, displayed seed −293006220 | Stopped after about30 seconds, before requests; no terminal result retained. |
| Main discovery retry | English Mehmed vs English Akbar, Albion Lowlands, seed −34609406, enemy strategic AI disabled | 21 attempts, Age3, human surrender115:30.03. |

The original brief required enemy AI. The human explicitly changed that to **disable enemy AI and try again**, then granted a **fresh200-attempt allowance**. The initial Medium setting does not represent active difficulty once its controller is removed.

Each isolated fixture ran once at **tick0**, compiled outside Assets. It added **exactly20 player-owned Spearmen**, IDs40–59, with normal80 health, on distinct walkable tiles near the Town Center. Three completed Houses raised capacity **10→40** and population **7→27**, leaving13 training slots. The advantage includes physical Houses and their vision. Civilization definitions were unchanged.

Resources remained **200 Food /200 Wood /100 Gold /0 Stone** before and after setup. The revised fixture removed one enemy AI controller at tick0 and verified zero remaining; enemy units/buildings remained. It disarmed immediately. Timing was1×, with fog disabling, terrain reveal, vision cheat, God Mode and accelerated production/construction off. No later resource grants or gameplay cheats were applied by the agent.

Evidence: [main startup](full-ai-playthrough-2/retry-no-enemy-ai-2/startup-advantage.json), [settings](full-ai-playthrough-2/retry-no-enemy-ai-2/match-settings.json), [fixture](full-ai-playthrough-2/retry-no-enemy-ai-2/StartingAdvantageFixture.cs), [authorization history](full-ai-playthrough-2/authorization.md). Earlier sessions remain preserved in the parent and retry-no-enemy-ai folders.

## 2. Final outcome and duration

The native journal records **Human SurrenderVoteCommand at tick207901**, match_over=true, match_end_tick207901, winning_team1. At30 ticks/second, duration is **115:30.03**. Observer attachment16:08:02.689Z to surrender18:05:38.169Z was approximately **117m35s wall time**. Slow agent UI interaction, provider failures and observation contributed; this is not efficient-player timing.

Final controllable units: **52 Villagers,26 Spearmen, one Scout and one King**, population80/80, Age3. Resources: Food31180, Wood10249, Gold2500, Stone2560. This was **not an economy wipeout or proof of an unwinnable native match**. The human ended the run while requesting the report. Later MCP state showed a different fresh Age1 simulation; the agent did not surrender that match.

The result was recovered from the temporary passive journal. Computer Use was stopped, so **no final Defeat overlay screenshot was captured**. Native command/state evidence establishes the result. See [closeout state](full-ai-playthrough-2/retry-no-enemy-ai-2/closeout-state.json) and [native journal](full-ai-playthrough-2/retry-no-enemy-ai-2/passive-evidence.json).

## 3. Chronological requests and actual results

Times are first observed HTTP trace ticks, approximately within five seconds. All orders used the real UI and configured openai/gpt-6-luna. No Commander helper submission/admission was used.

| # / time | Request | Actual result |
|---|---|---|
| 1 /1:45 | Put five idle villagers on Food from sheep, three on Wood, and two on Gold. | Approved; exact5/3/2 allocation completed; native income delivered. |
| 2 /4:39 | Build a Mill near the berries and a Lumber Yard near the trees. | Timeout before admission. |
| 3 /6:54 | Advance to Feudal Age. | Gathered prerequisites, paid cost, completed landmark; Age2 around9:38. |
| 4 /9:28 | Build a House, then send the next five Villagers from my Town Center to gather Wood. | Clarify asked trees versus any Wood; no admission. |
| 5 /11:46 | From trees. | Clarify asked what Villagers should do with trees; compound intent lost. |
| 6 /14:52 | Build a Barracks and train six new Spearmen, then send only those new Spearmen to defend my base. | Approved; Barracks, six exact new units and two population Houses completed. Defense movement later failed duration limit. |
| 7 /18:20 | Advance to Castle Age. | Timeout before admission. |
| 8 /22:08 | Exact repeat of #2 after timeout. | Approved. Lumber Yard completed; Mill failed after visible berries depleted. |
| 9 /29:25 | Exact repeat of #7 after timeout. | Admitted; failed because no explored non-depleted Food source was known. |
| 10 /34:38 | Build a Mill near my Town Center and four Farms, then use four villagers to gather700 additional Food from the farms. | Timeout before admission. |
| 11 /38:51 | Send the next five Villagers from my Town Center to gather Wood from trees. | Approved; waited0/5 at cap, later completed exactly five births. Does not certify failed House-first ordering. |
| 12 /45:01 | Exact repeat of #10 after timeout. | Approved; Mill then four Farms completed. Explicit allocation failed despite ordinary Farm income. |
| 13 /49:49 | Build two Houses near my Town Center. | HTTP200 Request; game-side semantic admission rejected it. |
| 14 /54:22 | Gather200 additional Gold using four idle villagers. | Timeout before admission. |
| 15 /58:36 | Train six new Villagers. | Exact attributed production completed; normal population House prepared; existing watcher completed. |
| 16 /63:00 | Exact repeat of #14 after timeout. | Four idle workers assigned; actual200 additional Gold delivered; completed. |
| 17 /70:11 | Advance to Castle Age after resource recovery. | Timeout before admission. |
| 18 /76:39 | Use my Scout to find the enemy base. | Timeout before admission. |
| 19 /84:16 | Exact repeat of #17 after timeout. | Landmark completed; native Age3 around86:53. |
| 20 /101:52 | Exact repeat of #18 after timeout. | ScoutArea/VisibleEnemy admitted; no visible legal destination, no command; failed after1800 blocked ticks. |
| 21 /106:22 | Set my Barracks rally point near my Town Center. | Native SetRallyPointCommand; completed setting verified. |

The intended next production/patrol order was interrupted while typing and **not submitted**. No attack or new causal failure-answer request was submitted before closeout. Six timeout retries used identical wording. Castle also had a separate reactivation after actual resource recovery. Original failed goals remain failures.

## 4. Counts and paid accounting

Main retry: **21 submissions /21 HTTP attempts**, **15 HTTP200 and six UI timeouts**, zero schema repairs observed. Final HTTP status for timed-out attempts is unknown; all consume allowance conservatively. This is not vendor billing proof.

HTTP200 outcomes: **13 Request, two Clarify**. One Request was rejected before admission. **Seven submissions achieved full gameplay objectives**, three partially achieved them, and three failed/rejected. **Five full objectives succeeded on first submission**: split, Feudal, standalone watcher, new-Villager training and rally. Retry success is not first-attempt success.

Native goals: **19 admitted,14 completed, five failed, zero cancelled** before match cleanup. **Five explicit approvals** (#1,6,8,11,12). Native Commander commands: Gather28, Move1, PlaceBuilding12, SetRallyPoint1, SlaughterSheep1, TrainUnit12.

Fresh allowance: **21/200 used;179 remaining**. Initial enemy-enabled session used four separately authorized attempts; interrupted retry used zero. **25 total task attempts**, with original ledgers intact. See [retry ledger](full-ai-playthrough-2/retry-no-enemy-ai-2/paid-http-ledger.json), [aggregate ledger](full-ai-playthrough-2/paid-http-ledger.json), [goal milestones](full-ai-playthrough-2/retry-no-enemy-ai-2/goal-milestones.json).

## 5. Manual interventions and interruptions

The main retry contains **no Human unit/economy/combat commands**. Its only Human gameplay event is terminal surrender. Agent UI actions included chat, approvals, camera/minimap navigation and focus recovery. No emergency manual Spearman defense was used.

In the initial enemy-enabled session, six starting Villagers became ten through ordinary Town Center auto-production. The opening economy timed out; enemy attacks reduced them to seven at tick11043 and three by12091. Three remained because seven of the ten were lost, not because setup spawned only three. After interruption that journal recorded three Human MoveCommands and surrender; their targets/intent were not retained.

Physical Escape stopped Computer Use several times, followed by explicit reauthorization. Separate click/type helper timeouts required fresh observation; no uncertain input caused a duplicate provider submission. Final interruption prevented patrol/control checks and overlay capture.

Passive instrumentation encountered **four Windows error1224 file-lock exceptions**. Live writes moved to a fresh temporary folder with the same in-memory journal and HTTP IDs; gameplay was unchanged. After Editor transition the observer assembly was absent, so it was no longer running. Final JSON was copied back and the pre-relocation version retained. These errors are instrumentation limitations, separate from production gameplay failures.

## 6. Demonstrated features and remaining coverage

Demonstrated beyond Playthrough1:

- Age2 and Age3 through normal prerequisites, costs and native landmarks.
- Barracks, Mill, four Farms, Lumber Yard and population Houses; the anchored two-House request itself failed.
- Exact six-new Spearman IDs78,79,81,83,85,86, excluding all20 starting units. The dependent movement used that exact set, but arrival failed. [Evidence](full-ai-playthrough-2/retry-no-enemy-ai-2/exact-new-unit-results.json).
- Future-five observed/assigned IDs96–100. Separately requested new Villagers97–102 distinguished an ordinary auto-produced birth. [Evidence](full-ai-playthrough-2/retry-no-enemy-ai-2/future-five-and-new-six.json).
- Actual additional gathered Gold rather than existing stockpile.
- Sustainable Farm income after natural Food depletion, with the explicit allocation task still failed.
- Native rally setting; post-setting birth/movement behavior remains untested.

Not demonstrated: attack execution, strategic-enemy combat in the revised retry, patrol/cancellation, post-rally births, new causal failure answers, or Scout-loss/takeover retest. No comparable lost-Scout defense occurred. Prior takeover defect remains unresolved. The successful standalone watcher does not repair the failed compound conversation.

## 7. Confirmed bugs and first failing layers

**P1 — defense destination/completion mismatch.** Goal7 moved the six produced Spearmen to (191,145), a non-walkable tile. They stayed owned, healthy and Idle2.03–3.97 units away. CommanderPlanner.AllUnitsAt requires every eligible unit within1 unit of the original point; it never accepted the native stopped positions and failed the36000-tick duration limit. No Human takeover occurred. First failure: target-resolution/native-completion integration. [Observation](full-ai-playthrough-2/retry-no-enemy-ai-2/defense-arrival-observation.json).

**P1 — occupied Farm capacity and worker selection.** Four completed Farms had native builders1–4 gathering. The Any/Eligible four-worker step recorded no selected IDs or assignments and failed capacity/reachability. Source prioritizes Idle workers and counts non-selected Farm workers against one-worker slots. Choosing idle workers can therefore leave every target occupied by other eligible workers. The failure is initial selection/capacity, not demonstrated recovery failure. Reachability was not independently isolated. Progress was680/700 at failure; later ordinary income exceeded the target while the task remained Failed. Preserve exact counts, ownership, human protection and path validation in repairs. [Evidence](full-ai-playthrough-2/retry-no-enemy-ai-2/farm-allocation-blocker.json).

**P1 — compound clarification loses intent.** The House→next-five Wood request asked for a source, then “From trees” prompted another question about what to do with trees. Generic Clarify without a typed pending draft stores clarification text; typed continuation only supports one worker-allocation draft. First observed failure: semantic conversation continuity. Full response objects were not retained, so an exact omitted JSON field is unproven.

**P2 — anchored multi-build contract mismatch.** Two-Houses-near-Town-Center was a parsed Request rejected by admission. Source CommanderSemanticAdmission rejects placement anchors with count other than1, while the provider vocabulary broadly allows counts1–20. This is consistent with the observed rejection; precise emitted fields are unproven because node JSON was not retained. Safe rejection worked; capability communication/representation needs review.

**Scouting limit.** Finding an unseen base became ScoutArea/VisibleEnemy, then truthfully blocked without a command. Known visible locations do not provide frontier discovery. This is a capability/intent-fidelity limitation, not permission to supply hidden targets. [Action/blocker](full-ai-playthrough-2/retry-no-enemy-ai-2/scouting-blocker.json).

**Operational availability.** Six deadlines failed before admission. A credential-free public OpenRouter check timed out; the next responded200 and Castle succeeded. Connectivity/provider availability contributed. No timeout, retry or schema guard was changed. No new P0 established.

## 8. UX priorities

**P1:** repair base destinations/completion receipts, Farm selection and compound continuation; diagnose availability and provide explicit recovery without late automatic execution. Stalled defense and repeating clarification are poor combat-time guidance.

**P2:** expose anchored multi-build limits; distinguish discovery from known-location movement; keep failures and task actions discoverable in the small viewport. The expanded panel obscures the base, and focus recovery is costly. Slow agent operation and instrumentation also contributed to duration; this is not an ordinary APM benchmark.

## 9. Comparison with Playthrough1

Playthrough1 ended29:01 after economy wipeout and seven attempts. It completed the split and House/Lumber Yard but not age progression, explicit military production or future births.

The initial extra-army session still lost seven of ten Villagers while the economy request timed out and defense was delayed. The main retry retained the economy and reached Age3 after enemy AI was disabled. Its healthy80-population surrender is **not evidence of combat defeat or worse balance**. AI conditions, seeds, latency and manual ending differ. See [Playthrough1](full-ai-playthrough-report.md) and [archived interrupted checkpoint](full-ai-playthrough-2/interrupted-checkpoint-report.md).

## 10. Recommendation and closeout

**Targeted Sol fixes should come next**, beginning with defense destination/completion, Farm selection and compound clarification. Retest exact cases with real UI/native evidence, then run active-enemy survival/scouting/attack and missing patrol/rally checks. Keep prior takeover, regression and package gates open.

The human requested finish/report at final interruption. Closeout retains authorization/setup, verifies native terminal result, reconciles submitted requests/counts, separates bugs/blockers, documents comparison and updates remaining_work.md. **Coverage gaps are not certified or substituted.** No new phase/match was started for closeout. Phase5B remains unaccepted.

**FULL AI PLAYTHROUGH 2 COMPLETED — DEFEAT**

