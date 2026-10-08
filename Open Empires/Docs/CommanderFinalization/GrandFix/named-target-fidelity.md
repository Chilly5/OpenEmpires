# G04 named-target fidelity checkpoint

Status: IMPLEMENTED — VERIFIED BY LISTED FOCUSED EVIDENCE (2026-10-08).
This is not a packaged Windows/Web or whole-GrandFix completion claim.

## Contract and canonical sources

Actor `unitSelector` remains separate from optional `target`. Both semantic JSON
and legacy DTO JSON accept one strict target object:

```json
{"kind":"BuildingType","structure":"TownCenter"}
{"kind":"UnitType","unit":"Archer"}
```

Building names are exact existing `BuildingType` content identifiers, not runtime
IDs. Resolution compares `GameSimulation.GetEffectiveBuildingType` so gameplay's
landmark adapters remain authoritative. Unit names use the existing bounded
Commander unit catalog; `ResolveCivUnitType` uses the **target owner's** civilization
(e.g. an enemy English Longbowman matches the canonical Archer family). There is
no duplicated cost, civilization, or stat database. Content discovery does not
grant a new gameplay mechanic.

Repair accepts a building target and owned villager actors at PlayerBase. Attack
accepts a unit/building target and VisibleEnemy location; existing actor validation
still applies. Targets on other actions, unknown kinds/names, additional fields,
numeric/default enum strings, null target objects and provider runtime identities
are rejected. Omission retains existing genuinely generic repair/attack behavior;
the provider instructions require preserving every explicitly named target.
Ambiguous `those`/selected-group requests unsupported by this contract must clarify,
not fabricate a runtime group. Target-result binding is not added in this slice;
existing `resultFromNode` still binds **actors/producers**, not the enemy/repair target.

## Deterministic selection and loss

- Named repair: select the lowest-ID owned building of the requested effective
  type **before** checking damage/completion. A healthy or unfinished first Town
  Center blocks instead of silently choosing another damaged Town Center. Generic
  repair continues selecting the lowest-ID eligible damaged owned building.
- Named enemy: choose the nearest currently visible living hostile matching type
  to the lowest-ID owned Town Center; ties use target ID. A unit target cannot
  substitute a building or a different unit family; a building target cannot
  substitute a unit. Neutral, owned, allied, dead/destroyed and hidden targets
  remain excluded.
- First successful game-side target resolution on a runnable capability goal is
  pinned before actor selection/reservation. The receipt retains exact runtime
  object identity and original owner. Blocked retries verify registry identity,
  owner, type, health/completion and enemy visibility. Loss blocks; another matching
  entity is not substituted. The receipt is never provider data or a command schema.
- The ordinary command carries the resolved target ID. Commander never reissues
  an order toward a replacement after `CommandIssued`. Base-game combat may
  naturally retarget afterward; this is **not** a lasting “only attack Archers” policy.

Ruling: dependent actions resolve a canonical target when their prerequisites are
ready, not at the moment the whole graph is submitted. A prior production wait
does not imply an unexpressed concrete enemy identity. Once a target is resolved,
including an actor-blocked wait, it is sticky. Cost if wrong: a request intending a
particular currently seen group cannot be represented by this family selector and
needs clarification/future trusted selection support rather than a false promise.

## Authority and presentation

Typed target values survive parsing, detached node construction, tactical admission,
legacy DTO round-trip and immutable intent construction. Structural scope version2
compares presence, kind, unit content ID and building content ID independently of
display. Changing TownCenter to House requires different scope. The current preview
includes target kind/type; the broader readable-preview work (G07) remains open.
Existing normal command encoding/network serialization is unchanged.

## Evidence so far

- `target-red-b845ebdd.xml`: 20 cases, 6 passed/14 failed. Named targets rejected at
  parsing, reproducing the missing contract; rejection cases already passed.
- `target-first-green-attempt-16219d54.xml`: 18/20 passed. Two fixture errors were
  retained: default English enemy requires its Longbow replacement rather than raw
  Archer2, and the preview helper belongs to CommanderPlanPreview, not Candidate.
  Fixture civilizations/reflection were corrected; product checks were not weakened.
- `target-loss-red-2cb2bb8a.xml`: 21/23 passed. Both actor-blocked ownership-loss
  cases issued a substitute command, proving the missing pinning behavior.
- `target-affected-green-88a04cd4.xml`: 97/97 passed, 87.5426782 seconds, 0 fail/skip.
  Target/scope/capability/hostile/grounded-scope affected suites, not full regression.

Narrow read-only Sol review found no confirmed target-authority defect. Its
dependency-wait selection caveat is resolved by the explicit ruling above. Codegraph
MCP refs timed out after300 seconds; CLI doctor was unavailable on PATH. Main and
read-only Luna mapped current source with bounded rg/file reads; no repeated index
rebuild or claim of complete graph coverage.

Final affected EditMode `676ccf9f6cb845c987977b331c4eb895`: **104/104 passed**,
0 failed/skipped, 95.9358964 seconds (`target-final-edit-676ccf9f.xml`). Breakdown:
29 target cases, 13 structural-scope, 14 capability, 39 Phase4G hostile, 8 grounded
scope and 1 actual semantic-request transport/schema test. This is affected-area
regression, not the full historical suite or a new exhaustive hostile audit.

Final PlayMode `b2a386ec14c14075af66f1e7960a8cd0`: **3/3 passed**, 0 failed/skipped,
2.190386 seconds (`target-final-play-b2a386ec.xml`). Two new native simulation
scenarios demonstrate ordinary repair increasing the requested Town Center's
health while leaving the earlier damaged House unchanged, and ordinary combat
damaging the requested Archer while leaving the nearer villager unchanged. The
existing build/produce/patrol scenario also demonstrates exact produced-Spearmen
binding remains intact. Initial fixtures record grass/visibility, clear terrain
occupancy/holes, canonical trained unit initialization, resources and damaged
buildings. TC auto-production is disabled; attack scenario disables TC fire to
isolate the requested unit. No completion/target/health writes after submission.

Exact 16 source/test hashes are recorded in `target-checkpoint-20261008.json`.
Independent full
regression, same-source packaged Windows and served Web scenarios remain required
under the full brief. No paid/vendor provider HTTP, ASR, microphone or deployment
was used. The one provider-schema test uses an injected transport. G21's later
heavy-goal retirement must include the newly retained target object receipt.
