# Task 1 fix round 2 implementation plan

> Execute inline only after the parent explicitly hands back sole source and Unity ownership. No subagents, commits, paid calls, build, deployment, full suite, or PlayMode.

**Goal:** Retain every required canonical reference through the real retrieval→context→pack chain, fail the complete request safely if it cannot fit, and accurately label every rejected successful-HTTP attempt.

**Architecture:** Use the existing catalog and content resolver to distinguish required exact names/IDs and ambiguous candidate sets from optional fuzzy suggestions. Keep a finite required-reference bound and validate its completeness again at packing, without truncating the utterance, pending graph, or action constraints. Result-stage changes remain inside the existing one-attempt HTTP lifecycle.

**Tech stack:** Unity C#, NUnit EditMode fixture transports, Newtonsoft JSON; no gateway/backend change expected.

**Requirements:** [fix2 brief](task-1-fix2-brief.md), [independent review](task-1-fix-review.md), [original correction brief](task-1-fix-brief.md). Preserve strict conservative `<4000` preflight, 15-second deadline, one eligible schema-only repair, immutable per-attempt metrics, and Task 2 source/behavior. Actual billed-token calibration remains a later paid task.

## File map

- `Assets/Scripts/AI/Commander/Phase5B/CommanderKnowledgeRetrieval.cs`: bounded full-input reference discovery, required-first row selection, ambiguity and completeness marker; no second catalog or execution authority.
- `Assets/Scripts/AI/Commander/CommanderContextBuilder.cs` and the single semantic submission call in `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs`: pass current reply, original request, and bounded pending serialization separately into retrieval. Preserve the read-only question path and Task 2 clarification logic.
- `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticProvider.cs` and `Assets/Scripts/AI/Commander/Phase5C/CommanderSemanticRequestPack.cs`: retain a compact retrieval-completeness signal through the detached context, validate required IDs/ambiguity candidates against actual packed rows, remove duplicate marker IDs from the outgoing prompt only after validation, and reject an incomplete/over-limit *whole* request before HTTP.
- `Assets/Scripts/AI/Commander/Phase4A/OpenRouterCommanderProvider.cs`: assign final redacted result stage before each 2xx rejection; keep existing metrics and transport bounds.
- `Assets/Tests/EditMode/CommanderPhase5CProviderBudgetTests.cs`: focused real-chain retrieval/budget/accounting tests. `CommanderPhase5AProviderContractTests.cs`: only rename two misleading example-promising test names or add a compact generated-contract/parser parity assertion; retain the existing concrete dynamic parser/compiler fixtures.

## Task 1 — Required-reference-first retrieval and whole-request refusal

- [ ] Add RED NUnit cases that build a real `CommanderContextBuilder` context and then `CommanderSemanticProviderRequest`/`CommanderSemanticRequestPack`: nine explicit distinct canonical unit/building references; a reference placed after word 96 and after phrase window 128 but within the 1024-character supported message; an ambiguous name with all offered alternatives; and a bounded pending graph reference that is absent from the current short reply. Decode packed `knowledge` columns and assert each required ID, cost, availability, executable/capability value, and ambiguous alternatives survives. No isolated synthetic pack-only proof substitutes for this chain.
- [ ] Add RED refusal cases: more required IDs/alternatives than the documented finite bound, and required rows that make the complete initial pack reach `>=4000` (or detached context exceed 8192). Assert zero fixture-transport calls, no executable nodes or prefix, and a precise safe context/input-limit explanation. The full utterance and pending proposal must never be cropped to make a partial request fit. Add ordinary real-catalog House→next-five-Wood and composed-movement controls that still dispatch, with section estimates and strict `<4000` assertions.
- [ ] Run only these named EditMode tests; retain the first RED full job JSON and exact discovery/failure counts.
- [ ] Implement full bounded-source scanning without `Substring(0,1024)`, first-96-word, or first-128-phrase *semantic* cutoffs: scan the entire already-bounded player message and bounded original/pending inputs in deterministic order. Resolve exact canonical IDs and aliases from the existing catalog first; include all ambiguous offered candidate IDs as required. Optional fuzzy suggestions may fill remaining slots only after required rows and must never evict them. Keep a finite required-ID ceiling (proposed 16) and a separate small optional ceiling (at most the present 8), with deterministic stable-ID ordering/tie breaks. Do not emit unrelated fuzzy `nameInterpretations` for ordinary exact-name requests; preserve meaningful ambiguous alternatives.
- [ ] Carry required IDs, ambiguity candidate IDs, and an overflow/incomplete flag as detached retrieval metadata, without treating retrieval as authorization. Ensure context serialization never silently replaces required knowledge to fit its 8192-character bound; if it cannot carry all required rows, retain a compact failure marker so packing rejects before HTTP. The pack must verify metadata against its *actual* selected rows after columnar projection; any missing required item or overflow marker yields the whole-request blocker, never `Unsupported` because a row was missed. Only after this check may redundant metadata be omitted from model-visible content to protect the 3946/3759 ordinary cases.
- [ ] Run the named cases GREEN, plus the existing 19-case `CommanderPhase5C` focused category and the two production-catalog budget fixtures. Preserve original quantities, negations, idle/current assignment, sources, order, and exact graph references in both accepted and refused cases.

## Task 2 — Truthful per-attempt stage after 2xx

- [ ] Add RED fixture-response tests for valid JSON with missing/multiple `choices`, `finish_reason:length`, another incomplete finish, empty content, unsupported parts, oversized content, provider `error`, and a null transport response. Assert one HTTP attempt, status 200 where applicable, distinct `ResultStage`, safe rejection, no leaked body, and no extra/repair request; keep existing two-attempt repair metrics tests.
- [ ] Set a specific stage immediately before each validation failure in `PostAndExtractTextAsync` (for example `response-invalid-envelope`, `response-truncated`, `response-incomplete`, `response-empty`, `response-unsupported-parts`, `response-oversize`, `provider-error`, `transport-null`). Reserve `response-extracted` for completed supported content and retain `http-failure`, timeout/cancel, and byte-limit labels. Avoid string-matching exception text or storing response content in snapshots.
- [ ] Run only named RED then GREEN stage/accounting controls. Preserve complete raw terminal results, and verify initial and repair snapshots remain immutable with nullable usage.

## Task 3 — Narrow regression and handback

- [ ] Rename the two old Phase 5A tests so their names describe registry/field parity, or add one small generated-contract assertion against the already-parsed `SharedWorkers`/exact-producers fixtures; do not restore large outgoing examples or delete existing deterministic compiler tests.
- [ ] After targeted import, confirm live Unity project path, compilation finished, zero console errors, nonzero discovered tests, and terminal results for the exact focused jobs. Recover exact handles on observer timeout; allow at most one 0-start initialization retry, with both raw artifacts retained. Run only the relevant 19-case Phase 5C and scoped old-provider controls; local Node tests only if gateway was changed (not planned). `git diff --check` and source SHA-256s are read-only final checks.
- [ ] Update `task-1-fix-report.md` with fix2 RED/GREEN IDs/counts, full raw JSON artifact paths, exact commands and output, 3946/3759 regression estimates or truthful revised values, file hashes, self-review, and remaining limits. Explicitly return sole source/Unity ownership to the parent. No actual-token, native gameplay, or whole-Phase-5C acceptance claim.

## Review focus

1. A ninth explicit supported ID must be present or cause precise *whole-request* refusal, never silent omission.
2. A late supported reference within 1024 characters must not vanish because earlier words/phrases exhausted a scan window.
3. Pending graph references and ambiguous candidate alternatives must be retained together, not inferred from only the latest reply.
4. Optional fuzzy rows must not turn an ordinary 3946-unit construction request into a false over-budget refusal.
5. A 200 response that is malformed, truncated, empty, or unsupported must never be recorded as successful semantic extraction.

**Preparation status:** Plan only. No source, Unity MCP, tests, network, or paid calls in this preparation turn.
