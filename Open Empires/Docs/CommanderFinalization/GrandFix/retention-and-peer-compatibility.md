# Retention and peer compatibility — GrandFix checkpoint

2026-10-08. G21/G22 focused implementation delivered. G11/G12/G23 are **not**
closed by this report. Full Grand Release Finalization remains ACTIVE.

Source: `unit_models_and_voice_control`, HEAD
`8586764e644240f93c99e2afa1c7d41c697cc879`, changed working tree, Unity6000.5.9f1.
See `retention-checkpoint-20261008.json` for the exact source hashes and jobs.
Historical manifests and RED artifacts are preserved. No commit/push/deployment.

## Ownership lifecycle (G21)

`StrategicIntentIdProvider` keeps only live object claims. Allocation/observation
advance a monotonic high-water mark; a missing lower ID cannot register again.
Retirement requires reference-equal ownership, so cleanup of an old object cannot
remove a transferred confirmation. There is no cumulative tombstone dictionary.
Dispose poisons allocation/registration and clears claims. Runtime-scoped provenance
and existing approval checks remain required; an integer alone is never authority.

Owned request validation/detachment precedes allocation. Factory exceptions release
unbound slots. Failed translations, cancelled/reset requests and dismissed previews
retire their claims. A successful bridge transfer hands the claim to the pending
intent; confirmation preserves the new owner. Reset closes ownership before an
ignoring provider can bind its late result. Failed/denied incoming pipeline decisions
and aborted submissions retire unadmitted claims, including observer exceptions.

Active admitted roots remain owned across history churn and milestone continuation.
Terminal ownership is committed before fallible cleanup/publication. Planner disposal
always cancels owned child work and releases strategic reservations, even when the
normal player-control revision counter is exhausted. This teardown exception does
not weaken ordinary cancellation/replacement revision preflight. Already accepted
native game commands are not rewound or deleted.

Goal-manager disposal clears histories and worker bookkeeping and detaches goals'
runtime, candidate, dependency, producer, command and training-observation references.
This detachment occurs at actual disposal, not archive eviction: an active consumer
may still need an archived producer's exact result.

## Worker lifetime and override (G22)

Pruning now covers protection, controlled-worker, recent-gather, worker-reservation
and military-reservation stores. Trusted registry checks require live owned units
and the correct worker role. Active and garrisoned identities are both recognized;
garrisoning is not death and does not release a live reservation. Removal/death/
ownership loss drops stale entries. Expired temporary protection/gather leases are
pruned in deterministic order while live Commander control remains.

Unit IDs are monotonic in this registry, not recycled. Sticky takeover is maintained
by existing goal flags, separately from temporary worker leases. The result-bound
unit check at tick1050 and shared-worker check beyond tick1000 both pass without
replacement or reclaim. Exact results still reject foreign runtimes and unrelated
units; no provider-selected IDs or new network encoding were introduced.

## Measured collection counters (not a performance benchmark)

| Isolated workload | Before repair, retained owner claims | After repair |
|---|---:|---:|
| 128 rejected requests | 128 | 0; bounded planner history remains <=100 |
| 64 failed translations | 64 | 0 |
| Invalid owned request constructor | 1 | 0 |
| Reset of cancellation-ignoring translation | 1 | 0; late bind rejected |
| Disposal with16 created intents | 16 | 0; disposed allocation rejected |

The 64 dismissed-preview and32 denied-decision loops end with zero claims. Four
removed units exercising all five authority stores leave all five empty. Temporary
lease expiry leaves zero protection/gather entries but preserves live control.
Garrisoned reservations survive until explicit goal release. These are measured
collection counts under focused fixtures, **not** retained-byte, GC, FPS or long-match
claims. G26 workload/performance measurements remain open.

## Evidence and reproducible focused lanes

Complete NUnit XML files live beside this report:

- Initial RED `retention-red-dc7ed011.xml`:11 selected,2 passed/9 failed.
- Review RED `retention-review-red-4011b133.xml`:4 failed.
- Observer RED `retention-observer-red-dc8e9341.xml`:3 failed.
- Terminal RED `retention-terminal-red-580e0a32.xml`:2 failed.
- Disposal RED `retention-disposal-red-b4dc0528.xml`:2 selected,1 passed/1 failed;
  active continuation passed, revision-exhausted disposal left child work alive.
- Intermediate `retention-final-edit-3b43c519.xml`:125/125 passed,48.4232418s,
  before the final revision-exhaustion disposal repair; not final-source evidence.
- Final `retention-final-edit-c84fdb71.xml`:174/174 passed,0 failed/skipped,
  66.7791766s. Fixtures: retention16, Phase4B2 69, Phase4D1 53,
  Phase5A dynamic runtime23, Phase5A intent authority13.
- `retention-binding-edit-2e023782.xml`:3/3 passed,1.4547391s; exact produced set/
  tick1050 takeover, incompatible dependency, disposed-runtime rejection.
- `retention-native-play-c8647893.xml`:6/6 passed,5.250392s; native training/status,
  two quantity cases, requested TC repair, requested Archer combat, and native
  build/produce/exact-new Spearman patrol. Legitimate initial fixtures only; no
  forced post-submission completion or synthetic replacement of native behavior.

Use Unity MCP `run_tests`, `init_timeout:120000`, one runner, then poll the returned
`get_test_job` handle to terminal and preserve `TestResults.xml` before another run.
Final EditMode `test_names`: `CommanderGrandFixRetentionTests`, `CommanderPhase4B2Tests`,
`CommanderPhase4D1Tests`, `CommanderPhase5ADynamicRuntimeTests`,
`CommanderPhase5AIntentAuthorityTests` (all under `OpenEmpires.Tests`).
Additional exact-result test names are listed in the checkpoint JSON.
PlayMode fixtures: `CommanderGrandFixStatusPlayModeTests`,
`CommanderGrandFixQuantityPlayModeTests`, `CommanderGrandFixTargetPlayModeTests`,
`CommanderPhase4GResultBindingPlayModeTests` (same namespace).
Do not edit imported Assets during a live job. No full historical regression or
hostile audit was run in this implementation slice. Codegraph refs timed out;
bounded live source tracing was used without rebuilding/retrying the index.

## Peer/reconnect work remains (G11/G12/G23)

Later client-only G12 work is recorded in
[network compatibility checkpoint](network-compatibility-client-checkpoint.md).
It does not close the backend/genuine-peer/recovery requirements below; the
following paragraph describes what the earlier retention slice itself delivered.

No genuine peer certification, protocol negotiation repair, relay retention cap or
safe rejoin refusal is delivered by the retention changes. Native simulation checks
are not real clients/relay proof. Preserve existing restricted-source packets.

Next inventory must establish current handshake/build identity and whether recovery
requires replay from the initial world. Without verified snapshots, bound complete
history and refuse unrecoverable rejoin **before cloning** once its budget is exceeded.
Never return a tail as complete recovery, drop live traffic, or add a save system.
Verify bytes/frames, concurrent reconnects, expiry and cleanup with focused tests;
then genuinely matched/mismatched local peers where infrastructure permits.

## Remaining release gates

Independent full/hostile regression, fresh same-source Windows/Web builds, actual
packaged/served input and physical voice, quality corpus/candidate comparisons,
browser-local engine integration, setup/notices, measured performance and peers
remain separate. This report does not certify those gates. Paid semantic0/6,
cloudASR0/600 seconds, physical microphone0 and remote deployments0 unchanged.
