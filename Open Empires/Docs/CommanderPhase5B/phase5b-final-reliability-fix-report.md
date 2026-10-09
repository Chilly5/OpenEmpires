# Phase 5B final reliability fix — implementation checkpoint

2026-10-09. **ACTIVE / PARTIAL. Phase 5B is not accepted.** This is the current
implementation handoff, not a completion claim. The original independent audit,
its first failures and prior paid ledgers remain intact.

The earlier MCP blockage was recovered after user reactivation. Old PlayMode job
`437238419a4e42479dd0adba3690d88f` is confirmed terminal initialization failure,
zero tests started. A subsequent bounded run passed6/6 and is preserved separately.
Six new paid attempts are now explicitly authorized; one has run (HTTP200, valid
Clarify rather than Request). The original malformed-field reply did not recur,
so its exact offending field remains unknown, not guessed or silently repaired.
MCP was subsequently disconnected again while the user reopened it. A fresh
30-second wait ended with instance_count0; no second paid request was sent.
Original scope, remaining gates and all first failures remain intact.

## Root causes and changes

- `CommanderPlanner.Economy.cs`: finite resource objectives previously prepared
  their worker/source commands only once. Recovery now resolves stranded original
  workers against visible, reachable, authorized sources with the original source
  constraint, count and gathered-income baseline. Delivering workers finish their
  native delivery. There are no replacement workers or invented resource credits.
  Native sheep-to-carcass travel is now recognized only for the same recorded sheep
  assignment; it no longer produces a false unrelated-busy blocker. Human takeover
  and worker authority are checked before that transition exception.
- `CommanderSemanticJson.cs`: numerical target ages 2/3/4, as integers or strings,
  canonicalize to Feudal/Castle/Imperial. `Next` retains its relative meaning.
- `OpenRouterCommanderProvider.cs`: generic age, completion-order and producer
  ordinal instructions are clarified. Existing provider declarations, dependency
  validation and game-side approval remain mandatory. **Actual real-provider
  fidelity is not yet verified on these edits.**
- `CommanderSemanticResult.Diagnostics.cs`: recognized diagnostic names now include
  `resourceAmount`, `resourceAmountMode` and `countMode`. Wrong-type tests expose
  those bounded field names without exporting arbitrary provider values. The old
  Berries reply was not retained; its exact bad field is still unknown. No
  field-specific workaround or schema relaxation has been made.
- `CommanderWorkerAuthority.cs`: garrison storage is no longer an active Commander
  worker reservation. Human protection still resolves garrisoned workers.
- `StrategicPipeline.cs`: rejected uncredentialed evaluation cannot retire a
  bridge-owned displayed preview. Cleanup applies only to an authorized consumed
  intent; credentials and single-use checks remain intact.
- Legacy tests: reflection calls now supply the resolver's SourceKind argument;
  timeout UI lookup uses the actual chat control bindings instead of colliding
  with voice controls; canonical Spearmen/civilization-unit/capability expectations
  and supported Trebuchet coverage are reconciled with existing content contracts.
  Unknown content still rejects. Changed legacy test hashes are in
  [the supplement](reliability-evidence/legacy-fixture-source-supplement.json).
- `CommanderPhase5BReliabilityGatheringTests.cs` and
  `CommanderPhase5BReliabilitySemanticTests.cs`: native and strict-contract coverage.

## Evidence and exact outcomes

| Evidence | Outcome and scope |
|---|---|
| Baseline `2d605262a15249779941b0ce491d2b70` | All 20 audited failures reproduced before repairs. Failed-job aggregate result is null; do not infer passed/skipped counts. |
| Recovery RED `8be686649f264d2d9e8959351819e196` | Both native depleted-source completion failures reproduced; an additional capability fixture ID error was corrected at the test layer. |
| Semantic RED `8bd54f7dfd9b40c7b32b5ca2958b78c8` | Numerical-age/native-age and bounded diagnostic failures reproduced. See individual records, not an invented aggregate. |
| Focused GREEN `5dd90f0d02234951be48526617fdfae0` | 23 passed, 0 failed, 0 skipped; 21.2722466 seconds. Earlier source checkpoint. |
| Compatibility `a1b40d33efb3438094f030fa6f8b1516` | 13 passed, 0 failed, 0 skipped; 23.3756438 seconds. Earlier generated identity; must regenerate after the later Economy edit. |
| House fixture `69a578a004db491882421cb036e9e0cf` | Invalid test JSON used RequestBuild/building/builders.mode instead of existing BuildStructure/structure/builders.state. Preserved as fixture failure, not production RED. |
| Transition RED `265cdd6577fb455c9e86aa138459edfb` | Three cases completed, one failure: falsely Blocked while original workers were MovingToSlaughter on the same sheep. No aggregate pass/skip claim. |
| Native GREEN `e3daba90eb3543228cc0d61abad5008f` | **9 passed, 0 failed, 0 skipped**, 16.8667477 seconds on the later production source. |
| PlayMode `437238419a4e42479dd0adba3690d88f` | Recovered terminal initialization failure, completed0, total null; tests did not start. Not a game failure or a pass. |
| PlayMode `eb074b0b3bf94e51914b7a13ef8a0319` | **6 passed, 0 failed, 0 skipped**, 2.9121792 seconds. Exact TaskBoard/NativeObservations/Phase5A Authority fixtures. No-audio-listener warnings are retained; no voice acceptance claim. |
| Live Berries400 attempt1 | HTTP200, valid Clarify, no nodes or admission, zero repair. This is a failed fidelity case, not a Request/native pass. Exact old malformed field was not reproduced. |

Detailed records are under [reliability-evidence](reliability-evidence/).
`native-order-producer-transition-green.json` retains all nine native outcomes;
only noisy SyncCheck lines were removed from test output.

## Actual native gameplay proof

- Any/Food: nearby sheep was actually slaughtered, then ordinary Gather commands
  reached visible berries; **400 additional credited Food**, original workers
  `[0,1,2,3]`, four Commander commands, tick4351 including 90 post-completion ticks.
  No new Commander command after completion, and no false native-transition blocker.
- Explicit Berries: first150 source actually depleted; the same four workers
  recovered to Berries and reached400. With no remaining Berries, available Sheep
  were not substituted and the goal reported/finally timed out on the real blocker.
- Human Stop, loss and cancellation: no recovery command or replacement worker.
- Age3: two normal native landmark placements, actual player Age3 at tick7561.
- House→Wood: normal House placement/construction, zero Gather before actual House
  completion, then one existing worker assigned to Wood; both goals completed at
  tick541, two Commander commands, zero training commands.
- First TC: five actual native births from the bound first producer assigned at
  tick2026; assigned IDs exactly equal its birth-event IDs. A second TC actually
  produced a competing birth that did not enter that set. Watcher issued zero Train
  commands.

These are controlled native simulation fixtures: ordinary timers, commands and
credited income, advanced in bounded tick loops. Terrain/resources/completed TC
and starting age/build resources are setup fixtures, not evidence of autonomous
map exploration or earning prerequisites. They are **not** paid English
interpretation, standalone package or rendered-layout acceptance.
Already issued ordinary gathering orders may continue after a task completes;
completion stops new Commander actions, not native orders by a synthetic rollback.

## Identity and remaining gates

Main identity refreshed and verified: **376 records**, aggregate SHA256
`fd12743ac150c7a2db26cc9cc36ba8cff7547959454bde170f7ea20e6fada2ef`.
Coverage remains runtime scripts, project/package configuration, listed native
data/scenes and Phase5B/economy tests, not every asset/meta/document/legacy test.
Changed legacy tests have the supplemental hashes above. Compatibility data was
regenerated with the existing supported menu after the latest Economy edit; its
source_sha256 is `c1b075dcef7c2604e535cced9294162fb8289e09b33dced357a76ffc6f9187f0`.
Final runtime match verification and regeneration after any subsequent production
edit remain required; no compatibility check may be bypassed. Identity equality
alone is not gameplay proof.

1. Reconnect MCP after the user's editor reopening. The old unverified job has been
   recovered and a later bounded PlayMode run is green; no duplicate active run
   was launched based on observation timeout. Observers, not editors/tests, were
   terminated. No force-kill.
2. Real-provider diagnosis now has separate human authorization for six total
   attempts, including permitted repair. **One used, five remain.** See
   [authorization](reliability-evidence/live-six/authorization.md), preserved first
   [response](reliability-evidence/live-six/scenarios.json) and separate paid ledger.
   Continue with bounded typed diagnostics; correct only an evidenced generic
   mismatch. Preserve strict validation and all old ledgers. Check Age3,
   only-after-finish, Any additional Food and natural
   first-TC ordinal. A bare future-unit request lacking an action should genuinely
   clarify; use an explicit Wood follow-up for the actionable producer scenario.
3. After final source changes: regenerate compatibility identity, refresh/verify
   source records, run **one complete offline `OpenEmpires.EditModeTests`** regression,
   and relevant existing hostile checks. Report exact pass/fail/skip from actual
   results. No full regression has run in this new goal yet.
4. Verify cancellation/reset, idle restrictions, result attribution, task cards and
   approval on final source; distinguish retained historical evidence from fresh
   results. Then close this report and root roadmap against every brief item.
5. Windows/Web packages and packaged acceptance remain separate, stale release
   gates. No rebuild, multiplayer/voice work, reset/clean/commit/push or next phase.

Independent Luna re-verification must repeat each previously failed wording through
the actual Commander UI, explicitly approve strategic previews, inspect native
command timing and exact result IDs, test unavailable/fogged alternatives and
explicit-source exhaustion, and compare task progress with actual gathered income.
Do not treat this checkpoint or narrow green results as Phase5B acceptance.
