# Full AI Commander Playthrough 2 — interrupted checkpoint

## Latest live continuation

The human explicitly answered **Resume the retry**. A second AI-disabled retry is now running, with evidence in `full-ai-playthrough-2/retry-no-enemy-ai-2/`, fresh 200-attempt cap, seed `-34609406`, English Mehmed versus English Akbar, ordinary timing/fog/resources, and the same tick-0 advantage. The historical interrupted checkpoint below remains preserved until the final report replaces it.

Native passes so far: exact Food5/Wood3/Gold2 allocation and income, Feudal Age, Barracks, six newly attributed Spearmen IDs78/79/81/83/85/86, two population Houses, and Lumber Yard. The defense follow-up issued a move to a non-walkable base tile; all six surviving units became idle2–4 units away while completion requires each within1 unit. It later failed its duration limit. Castle Age failed from depleted known Food sources. Mill/Farm recovery, next-N watcher and remaining gameplay are still being exercised. This is not completion.

**PARTIAL PLAYTHROUGH 2 — Computer Use interrupted; revised AI-disabled retry stopped before gameplay orders.**

This is a discovery experiment. Phase 5B is not accepted. Production fixes, full regression and package gates remain separate.

## 1. Match setup and starting advantage

Current Editor source: HEAD `de9354f91bd4f3ca15ce8e3c5c5efb7682f999f9`, manifest 376 records, aggregate `3059cfbf9ef7189e05c60b05fe4ee17cd00ffbb7b21e24dd4782517b45055676`. Manifest verifier passed during setup and interruption review. No production or civilization edits were made for this experiment.

The first Playthrough-2 session used the real menu: English Marco versus English Vlad, single-player 1v1, Medium enemy AI, Albion Lowlands, seed `-1215740081`. Simulation timing was 1×. Fog disabling, terrain reveal, vision cheat, God Mode, accelerated production/construction and God Powers were all off. See [settings](full-ai-playthrough-2/match-settings.json).

An isolated fixture compiled outside Assets ran once at **tick 0**, using the existing debug unit factory. It created exactly 20 player-owned Spearmen, IDs 40–59, each with normal 80 starting health, on distinct walkable tiles near the Town Center. Three completed Houses, IDs 2–4, raised capacity from 10 to 40. Population increased from 7 to 27, leaving 13 slots for ordinary training. The fixture disarmed immediately. Resources remained exactly 200 Food / 200 Wood / 100 Gold / 0 Stone. This advantage also provides three physical Houses and their vision; it is explicitly recorded rather than treated as a fair-match condition. See [exact startup record](full-ai-playthrough-2/startup-advantage.json), [fixture](full-ai-playthrough-2/StartingAdvantageFixture.cs) and [starting screenshot](full-ai-playthrough-2/starting-army.jpg).

After the user interrupted, the user authorized retrying with enemy AI disabled. The retry initialized with seed `-293006220` displayed in the menu. Its isolated fixture removed one enemy AI controller at tick 0 and recorded zero remaining controllers. Enemy units/buildings were retained. It also supplied exactly 20 Spearmen and three completed Houses, with unchanged standard resources and 27/40 population. See [retry startup record](full-ai-playthrough-2/retry-no-enemy-ai/startup-advantage.json). This revised condition can demonstrate feature execution but cannot demonstrate survival against an active strategic enemy AI.

## 2. Results and duration

The first session ended through a native Human `SurrenderVoteCommand` at tick **14212**, with winning team 1. At 30 ticks/second this is **7:53.73** simulation time. The surrender occurred after the user interrupted agent Computer Use; it was not an agent-controlled full-match finish. The terminal journal retained 17 Spearmen, three Villagers and surviving buildings, so this session does not establish an unwinnable state.

The AI-disabled retry was interrupted before any Commander request. Its latest retained sample was tick **904**, approximately 30 seconds, with eight Villagers, 20 Spearmen, one Scout, population 29/40, and no match result. A subsequent live MCP query verified **Edit Mode, no simulation, and no observer assembly**. The retry is no longer running and cannot be resumed from a live handle. See [interruption state](full-ai-playthrough-2/interruption-state.json).

## 3. Chronological Commander requests and actual results

| Session/order | Request | Actual result |
|---|---|---|
| First session, approximately 1:42 | Put five idle villagers on Food from sheep, three on Wood, and two on Gold. | One HTTP attempt; UI timeout before admission. No economy goals or gathering commands. |
| Approximately 3:14 | Use my Spearmen to defend my base. | HTTP 200, valid one-node Request; admitted CapabilityAction and one native Commander MoveCommand at tick 5865. Task kept waiting for selected units to reach the location. Survival/defense objective was not achieved. |
| Approximately 5:00 | Gather 400 Food and 200 Gold so I can afford Feudal Age. | HTTP 200, Clarify: asked how many Villagers for each resource and stated it could retain the two targets. No action admission. |
| Approximately 6:21 | Use six villagers for Food and two for Gold. | HTTP 200, valid two-node Request. Preview preserved Food 400/Gold 200 stockpile targets and worker counts 6/2. Pending explicit approval. Approval click was interrupted by Escape; no gathering goals or commands were admitted. |
| AI-disabled retry | None | No requests submitted before interruption. |

Full transcripts, safe provider traces, owned-state samples, cards and command sources are retained in the [first-session journal](full-ai-playthrough-2/passive-evidence.json) and [retry journal](full-ai-playthrough-2/retry-no-enemy-ai/passive-evidence.json). No direct test-helper Commander submission or admission was used.

## 4. Counts and paid accounting

First session: **four text submissions and four HTTP attempts**, consisting of one timeout and three HTTP-200 responses (two Request, one Clarify). No schema repair was observed. One action was admitted; none of the submitted gameplay objectives was demonstrated complete. One clarification follow-up retained the requested quantities in its unapproved preview. Preview correctness is not execution success.

The retry used **zero HTTP attempts**. The user's latest direct message granted a **fresh 200 paid-attempt cap** for retry. That allowance supersedes the earlier plan to use the original session's remaining 196. Existing ledgers remain preserved; original four attempts remain recorded separately. Before each new submission reserve two attempts for the existing initial/repair behavior and reconcile passive HTTP traces before further spending. See [authorization history](full-ai-playthrough-2/authorization.md) and [paid ledger](full-ai-playthrough-2/paid-http-ledger.json).

## 5. Manual interventions and why only three Villagers remained

The match began with six Villagers. Normal Town Center auto-production consumed the initial Food and produced four more, reaching ten. The opening economy request timed out, so those workers had no Commander gathering assignments. During visible enemy attacks, the retained count fell to seven at tick 11043 and to three by tick 12091. Idle economy, delayed instructions and insufficient effective protection contributed to the loss; the journal does not isolate a unique combat or pathfinding defect.

Agent manual gameplay interventions: **none** before interruption. Agent UI activity was limited to menu/chat entry and requests. After interruption, the journal observed three Human MoveCommands at ticks 12147, 12152 and 12158, then the Human surrender at 14212. Those events were outside agent control; intent/targets were not retained and are not invented here. Setup mutations are separately documented above.

The authorized approval click did not complete. The helper explicitly reported a physical Escape stop. Later UI access was reauthorized, then stopped again. The computer-use interruption is preserved as an operational boundary rather than classified as a Commander failure.

## 6. New features demonstrated and coverage still missing

Demonstrated beyond Playthrough 1: controlled startup population headroom permitted ordinary Town Center auto-production despite the 20 added military units; a defense request selected the 20 Spearmen and issued an ordinary MoveCommand; a clarification follow-up retained exact resource targets and worker counts in a preview.

Not demonstrated by this checkpoint: completed economy objectives, depletion recovery, Mill/Lumber Yard/House/military-building construction through Commander, Age 2 or Age 3, explicit new Villager training, next-N watching, exact new military assignment, rally behavior, scouting, attacking or agent-driven victory/defeat. The AI-disabled retry has not provided gameplay evidence for any of those features. They remain required work. Combat usability against active enemy AI will remain outside the revised retry conditions.

## 7. Confirmed failures and first observed failing layer

**Opening economy request timeout:** confirmed at the provider/UI deadline before semantic admission. The trace proves HTTP sent; final HTTP status is unavailable. This is an availability failure, not proof of a parser or planner bug.

**Defense task remained Working:** confirmed that the admitted capability issued one move and kept waiting for selected units to reach its resolved location. Villagers died while it remained active. This does not prove a specific pathfinding or combat bug; a targeted reproduction should inspect the resolved destination, formation, reachability and completion condition without hidden enemy data.

**Clarification required:** a vague but reasonable resource-target request did not choose counts itself. It asked a bounded clarification and retained the values after the response. This is a usability observation; exact preview preservation worked.

The previously confirmed Scout-loss/human-takeover explanation from Playthrough 1 was not reproduced here: no comparable Scout-defense request occurred. Existing failures remain visible and uncorrected.

## 8. UX frustrations and priorities

- **P1, operational:** a timed-out opening order can leave the entire economy idle while the match continues. Preserve the failure, provide actionable status and recovery controls, and diagnose provider availability; do not auto-admit late responses.
- **P1, investigation:** distinguish staging units at the base from ongoing protective coverage. A prolonged arrival task was difficult to interpret while Villagers were being lost. Determine whether destination/completion behavior is correct before proposing a production fix.
- **P2:** the count clarification added a full interaction cycle during combat. Clarification should remain bounded and preserve authority; improve its speed/discoverability rather than silently inventing strategic allocations.
- **P2:** the enlarged Commander panel obscured much of the base while entering orders. Approval switched to the compact panel, making rapidly changing focus/layout important to the operator.

No new P0 is established. No production bugs were fixed during this playthrough.

## 9. Comparison with Playthrough 1

Playthrough 1 completed two gameplay requests (Food/Wood split and House/Lumber Yard), then failed under attack and ended via agent normal surrender at 29:01. This first Playthrough-2 session had 20 extra Spearmen and capacity headroom, but its opening economy request timed out and the economy never began. Seven of ten Villagers were lost despite the army. Its user surrender at 7:53 is not evidence that the extra army worsened balance: source/operator/provider timing and map seed differed, and the session was interrupted.

The requested retry removes enemy strategic AI to allow broader feature discovery. Any later victory in that retry must be reported under those conditions and cannot substitute for an active-enemy survival test.

## 10. Next work and completion audit

The user-authorized AI-disabled retry should come next after explicit UI resumption. Start a fresh live match because the previous retry is terminal at the Editor/session level, preserve both journals, and use the latest fresh 200-attempt authorization. Exercise economy and construction, Age 2/3, future-unit orders, military production/result binding, scouting/attack, task controls and normal match termination. Keep failures visible and record interventions.

Setup and partial evidence/reporting are verified. Full gameplay coverage, a revised retry outcome, final counts, complete comparison and final closeout are **not achieved**. The goal remains active; this checkpoint is not a completion certificate.

**PARTIAL PLAYTHROUGH 2 — UI control stopped and the AI-disabled retry ended before Commander gameplay.**
