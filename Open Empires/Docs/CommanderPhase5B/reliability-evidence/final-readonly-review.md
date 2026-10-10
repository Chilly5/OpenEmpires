# Final read-only review and dispositions

Independent Sol review found no critical ownership/authority blocker and confirmed
the evidence-backed restoration of live-garrison claim retention.

1. Recovery repeatedly examined only the nearest four targets, potentially hiding
   a farther reachable source. Human authorized the narrow progressive recovery
   fix and focused verification, explicitly not another full regression without
   separate approval. RED `10458678f8294a4591f88223c03615d8` reproduced failure
   after1800 blocked ticks. GREEN `56e0a39e69c84fa49d6c56220d60a97b`:11 passed,
   zero failed/skipped,23.2806485 seconds. Native farther-source recovery credited
   400 Food at tick7006 with original four workers; maximum observed path-candidate
   checks per planner tick6 (asserted bound16). All-unreachable sweep remained
   blocked and expired after1800 ticks, issuing no recovery command.
2. Income attribution question: human explicitly intends player-wide Food income.
   Source and worker constraints restrict orders, not resource credits. No new
   delivery-accounting/native/network machinery is needed or implemented.

Follow-up read-only review found no concrete blocker in this diff: initial
allocation remains nearest-bounded; recovery advances offsets and revalidates
routes, retains found indices across later-worker searches, and does not reset a
proven blocker's timeout. Review itself ran no tests/API calls and made no edits.

## Bounds and behavior

Only finite-objective recovery opts into progression. Each worker examines at
most the existing configured3..5 candidates per planner observation (default4);
the frozen selection is at most200. Each candidate uses the existing footprint
adjacent-route checks and, for Sheep, the additional actual-tile route check.
This bounds candidate checks, not a hard wall-clock duration. Existing pathfinder
bounds still apply. Candidate ordering remains squared-distance, kind, then ID.
O(original workers) integer cursors are retained, not rejected-source sets or
cached route/visibility authority. Found candidates are revalidated each retry;
capacity/visibility changes rebuild the candidate list. Stable eligible lists are
eventually traversed. Arbitrarily changing candidate lists are not a guaranteed
search-completion contract. Successful recovery and runtime-reference release
clear cursors. No worker substitution, fog bypass, schema/network/protocol change.

The complete1605/1605 run `35a494d0965d49d28a41fcd114f65595` succeeded before
this approved patch. It is historical broad evidence, not full regression of the
changed source. No third complete suite was launched. No additional paid requests.

Post-patch generated identity controls `fa925517145e4f99a3b14c0336f69214` passed
13/13,0failed/skipped,24.2475635sec. Fresh376-record3059cfbf manifest verification
and diff whitespace check succeeded. The runtime compatibility source hash is
914079d0; protocol/encoding/recovery policy unchanged. Only Package Manager
auth/update errors were observed in the later console, not C# compile errors.
