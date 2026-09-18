# Phase 4B.2 independent review ledger

Read-only reviewer: `/root/review_pipeline_boundary`; current dirty working source, not HEAD-only diff. No Unity runs, edits, or delegated reviews by the reviewer.

## Foundation and shared pipeline

Strengths: lifetime ID ownership, exact approved-intent selection, current-context revalidation, explicit source precedence, strict wire metadata exclusion.

- Important: already-funded house foundations were deducted twice, understating extra housing wood. Regression `FundedHouseFoundation_DoesNotEraseAdditionalHousingCost` failed in job `aead2adf4a7248cb87a51d7475e1428a`; production quote now marks the additional deficit as net of foundations. Full suite verification pending.
- No critical issues found in this bounded static review.

## Router, bridge, UI and compatibility

Strengths: execution-free translation bridge, request identity/owner/tick checks, exact once-only confirmation, fresh approval and existing pipeline submission.

Findings reproduced and fixed:

- Important: legacy strategic parser/DTO outputs use placeholder ID 1; strict lifetime ownership rejects later legitimate player requests. Add trusted unallocated-player interpretation metadata and materialize only those values through the shared allocator. Do not rename externally assigned or already approved IDs.
- Important: `StrategicRecommendation.ToStrategicIntent` defaults to PlayerDirect; explicit recommendation provenance is required to close the legacy player-override route.
- Important: old tactical completions can overwrite a reinitialized chat's current result. Preserve return to original caller but generation-guard UI publication.
- Minor: router rejects whitespace before final punctuation accepted by the tactical mock.

This is not a readiness verdict. Final review, full regression, PlayMode runtime evidence and dependency/hash audits remain required.

## Re-review

Reviewer rechecked all five findings in current source and regression bodies: housing, parser identity, recommendation source, stale tactical publication, punctuation. Scoped verdict: ready, no remaining concrete production defects found. A temporary constructor warning was independently rechecked and withdrawn as an inspection artifact; both evaluator parameters remain present.

Focused verification after fixes: 69/69 EditMode, 3/3 PlayMode. Final full regression: 503/503 EditMode and 52/52 PlayMode, detailed saved artifacts inspected by the primary agent. Static audit remains a separate recorded gate.

Final audit completed: all 189 non-edited baseline files unchanged, 14 scoped changes, zero forbidden dependencies and no detected credentials. A parser/DTO scope flag against HEAD was withdrawn after in-memory reversal of only the two factory edits reproduced the pre-phase baseline hashes. No production changes were needed for that audit correction.
