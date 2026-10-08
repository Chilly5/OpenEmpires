# G06 production quantity and exact-new results

Status: IMPLEMENTED — VERIFIED BY LISTED FOCUSED EVIDENCE (2026-10-08).
This is not whole-goal or packaged cross-platform completion.

## Meaning and canonical observations

`EnsureUnitCount` defaults to `quantityMode: TargetTotal`. Living owned units
(including garrisoned units) and matching queued/in-flight Commander orders count
toward that total. Explicit `quantityMode: New` means exactly the requested number
of newly produced, request-attributed units, independent of old units or human
queues. Natural “more/additional/new” requests use New; this does not change worker
allocation's separate SelectedCount/Additional/TargetTotal/AllMatching contract.

The semantic node, strict JSON, DTO round-trip, immutable intent, ordinary
KnownIntent registration and DynamicPlan compiler preserve these meanings. No
provider runtime ID, balance database, binary command envelope, gather policy or
multiplayer wire encoding was added. Unit families use existing civilization
resolution; costs, production compatibility, population and prerequisites stay in
the normal gameplay/planner path.

Detached game-owned production expectations quote each root's newly produced
count from current owned/queued observations and guaranteed ancestor production.
Sequential totals “two, then three” therefore quote two new, then one new—not
two plus three. Exact unit consumers must match their source's full new-result
cardinality. Four existing plus a desired total of five cannot provide five new
units. Mismatch rejects the **whole** candidate before goals, worker reservations,
commands or prerequisite construction are admitted.

Ruling: effectful same-family parallel production combined with an exact-result
total is ambiguous under the current independent-goal executor. Reject atomically
with a request for explicit ordering/clarified counts; do not secretly serialize
the graph or convert a total to New. Already satisfied/no-op sibling totals remain
allowed. Cost if wrong: parallel total/result compositions need a future explicit
shared allocation contract; this checkpoint does not falsely promise such results.
Independent explicit-New roots retain their separate receipt identities.

## Approval, commit and ongoing execution

- Structural scope version3 includes each production node's quoted new count.
  Approval reprojects current facts; commit independently recompiles before any
  registration/reservation consumption. Material new-count changes require a new
  preview. Preview includes the expected newly produced count for total roots.
- A queue becoming a living unit without changing the required new count does not
  invalidate consent. Resource waiting alone does not change the quote. Explicit
  New is not changed by unrelated growth in the old population.
- Frozen result-producing total goals account for prior ancestor contributions.
  Changed unrelated contribution blocks **further** production rather than issuing
  extra units or substituting old/human results. Already issued ordinary commands
  may finish; completed attributable outputs can satisfy the original exact count
  only when the requested living total is also satisfied. Already sent commands
  are not claimed to be reversible.
- Only exact issuer/runtime/player/type/producer receipts enter the result. A
  result-total waits for other queued units to satisfy the total before completing.
  Lost/cancelled receipts, exact producer loss and human takeover retain existing
  fail-closed behavior. Dependent actions use RequiredNewProductionCount rather
  than the old target-minus-baseline approximation.
- Pending boxed command origins count as in-flight before native acceptance, for
  exact quantity, all matching totals, producer queue depth and population. This
  closes the planning-before-acceptance duplicate at the installed maximum input
  delay15/planning interval15. It is deterministic delayed-command evidence, not
  actual two-peer certification. Identity copies/unboxing cannot replace an original
  registered command; ordinary network encoding remains unchanged.

## Retained evidence / corrections

- Initial `quantity-red-61b3c331.xml`: 14 cases, 1 passed/13 failed,
  15.2456808 seconds; mismatch, queue baseline, New vocabulary/round-trip and
  approval/commit drift gaps reproduced.
- First affected `quantity-first-affected-961a31fd.xml`: 47 cases, 45 passed/2
  failed,31.403679 seconds. Two historical fixtures used totals while describing
  new results (Scout2→one result; two new Spearmen with old/human queues). They now
  explicitly request New with the intended result count; product gates not weakened.
- `quantity-known-and-overlap-red-5ffe3ddc.xml`: three actual failures covering
  ordinary KnownIntent losing New, sequential quote mismatch and parallel promises.
- `quantity-delay-fixture-0e60daf4.xml`: three fixture failures retained. OfType
  unboxed/reboxed the command, losing exact object identity; corrected to preserve
  the original ICommand reference. Native fixture's nonexistent ProducedUnitId
  assertion was also corrected to the actual receipt Issuer/IsCompleted contract.
- `quantity-delay-red-64585cdd.xml`: all three delayed-acceptance cases failed by
  issuing a second command (New1, total-result1 and New2 with queue limit1).
- `quantity-final-edit-134109e4.xml`: affected90/90 passed,77.7159801 seconds.
  `quantity-dynamic-green-f8d0a757.xml`: compiler/runtime26/26 passed,
  28.8098362 seconds. Both are intermediate source snapshots, not final-source proof.
- `quantity-noop-red-2d485484.xml`: a valid no-op sibling was over-rejected; fixed
  by checking actual quoted production before classifying overlap.

Read-only Luna inventory and Sol review were used. Sol's three important findings
were independently reproduced and fixed through the retained RED cases. No
re-review/full hostile audit, paid vendor HTTP/ASR, microphone or deployment.

Final source-matched EditMode `348cf45224c14bd7a3c2c48e39ad4245`: **117/117 passed**,
0 failed/skipped,103.8719285 seconds (`quantity-final-edit-348cf452.xml`). Includes
21 quantity,13 scope,29 named-target,14 capability,3 dynamic compiler,23 dynamic
runtime,8 grounded-scope and6 production-binding cases. Not full regression.

Final PlayMode `1643bf1a593748e6bf8849fd94711512`: **5/5 passed**,0 failed/skipped,
2.4855822 seconds (`quantity-final-play-1643bf1a.xml`). New scenarios use ordinary
native training with no accelerated completion, post-submission spawning or forced
flags. Four initial canonical Spearmen plus one human training order accepted before
preview lead to exactly one Commander training order for total6 and its one exact
new patroller, with six living Spearmen. Sequential total2 then total3 produces only
the final goal's one new result for patrol. Previous exact-produced-Spearmen patrol
and named TC-repair/Archer-attack scenarios also pass on this source. Initial terrain,
resources, age, canonical unit initialization and TC auto-production-off fixtures
are recorded in the test source. No real microphone/provider/browsers/peers involved.

Exact23 source/test hashes are in `quantity-checkpoint-20261008.json`. Earlier G04
and other intermediate hashes remain historical; they are not rewritten to imply
unchanged source. Final affected/native runs exercise this changed quantity source.
G21 retirement must include added normal-goal receipt/origin state.
Independent full regression, genuine peers, packaged Windows and served Web
quantity scenarios remain separate gates under the complete GrandFix brief.
