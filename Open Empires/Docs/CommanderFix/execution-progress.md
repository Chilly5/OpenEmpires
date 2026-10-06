# Execution ledger — plan: Docs/CommanderFix/economy-clarification-implementation-plan.md

Approved amendments 2026-10-06: SelectedCount for ordinary counted orders, Additional for extra workers, TargetTotal only explicit desired-total, AllMatching one-time snapshot. Native/network source-state changes require runtime proof first.

Ruling: Use the active dirty editor checkout rather than a new worktree — the user requested the live current project and its preserved Phase4G/4H changes — risk if wrong: evidence may drift; verify editor root and final hashes.
Ruling: Keep this durable ledger in the requested evidence folder; Windows/MCP verification cannot be executed by the Bash-only task-done scripts — record exact MCP job IDs and results here — risk if wrong: manual bookkeeping error; final consistency review checks it.

Pre-flight Task 1 -> Task 3: completed semantic criteria feed a new goal; ordinary commands unchanged initially.
Pre-flight Task 3 -> Task 2: runtime source-fidelity proof is a gate before conditional native changes; semantic source enum alone grants no command authority.
Pre-flight Task 1 -> Task 4: detached draft is not an executable completed node; completion returns through strict parser/admission.
Pre-flight Tasks 1/4 -> Task 5: provider grammar and bounded pending context must match actual strict parser fields.
Pre-flight Tasks 2–5 -> Task 6: final source and conditional native decision determine final evidence; full regression once only.

- [x] Task 1 typed semantic/intent path (complete; execution follows in Task 3).
- [ ] Task 3 deterministic existing-command allocation.
- [ ] Task 2 runtime gate and smallest conditional native repair.
- [ ] Task 4 clarification.
- [ ] Task 5 provider/runtime verification.
- [ ] Task 6 regression/docs/closeout.

Task 1 RED: job `e5c0845b6415404382f4b6c750037168`, 2/2 new tests failed on absent AllocateWorkers acceptance. XML preserved. Earlier job `3c821e88b99b4075af207bf5c6806772` ran zero tests against the previous assembly and is invalid evidence; fixed test-only Newtonsoft dependency by using literal JSON, without modifying asmdef/packages. Verified new fixture was loaded before the valid RED run.
Task 1 implementation: detached immutable worker criteria, strict node parsing, DTO roundtrip/admission and runtime validation added. Ordinary counted mode is SelectedCount; legacy SetResourceAllocation remains unchanged. Goal execution not implemented yet.
Ruling: AllocateWorkersIntent keeps the existing always-tactical CommanderIntent base contract rather than adding an unused intentLayer parameter — avoids implying strategic permission — risk if wrong: callers needing strategic economy must continue the existing approval route.
Task 1 GREEN: job `6937f2ec288446b2b8f479eaf9534700`, 3/3 passed: both new semantic/DTO tests and existing zero-worker legacy allocation test. Full XML preserved. Job `761555e1c372463aab1c0d093ae90bb6` failed initialization, zero tests started; not acceptance evidence. Fresh loaded enum/fixture and clean compiler console confirmed before the valid run. No full regression or paid provider test run yet.
