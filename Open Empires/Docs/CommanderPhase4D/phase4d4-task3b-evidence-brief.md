# Task 3B — final-source evidence worker brief

Start only after root accepts Task 3A's independent D/E/F scenario rereview. The root freezes the source hashes and owns the Phase 4D.4/final verdict; this worker gathers mechanical evidence, not a readiness judgment. One Unity test runner at a time. Do not modify production code, tests, packages/settings, credentials, scenes, or unrelated files; do not stage/commit/reset/clean.

1. Verify current Unity editor/import/compile state and zero `error CS` diagnostics. Use Unity MCP, not prior historical results. Record Unity instance/version, source hash snapshot, and exact job IDs.
2. Run **complete EditMode, then complete PlayMode sequentially** against the same frozen source. Poll each job to terminal. Save each complete terminal payload and native NUnit XML if returned, preserving bytes and SHA-256. Parse total/passed/failed/skipped/inconclusive and unique full test IDs; zero discovery is not a pass. Confirm all ten required 4D.4 advisory IDs and all named Phase 4D.1–4D.3 IDs from the requirements matrix/spec are discovered. Do not truncate payloads or infer missing details from summary counts.
3. Provide a scenario A–F evidence index mapping each spec assertion to the exact real PlayMode test ID, test-source assertion, and terminal pass. D/E/F must use the accepted strengthened Task 3A tests. A/B/C older scoped passes are not final-source evidence until these full suites pass.
4. Run read-only baseline verifier and inventory the final `git status --short`, baseline-to-HEAD source/test diff, untracked relevant source/tests, `.env` tracked/ignored status without reading contents, package/settings/scene status, and unexpected scratch/recovery artifacts. Report candidates, do not delete or relabel user files.
5. Hand root exact paths, hashes, counts, IDs, current-source snapshot, and any anomaly. Do not claim READY. If full suite fails, report the first failures and stop so root can triage before any rerun.

Required 4D.4 IDs: `Advisory_DoesNotExecuteAnything`, `Advisory_DoesNotCallProvider`, `Advisory_EmitsOnMeaningfulTransition`, `Advisory_DoesNotRepeatEveryTick`, `Advisory_RecoveryEmitsOnce`, `Advisory_CompletionEmitsOnce`, `Advisory_ResetClearsDeduplication`, `Advisory_IsBounded`, `Advisory_IsPlanVersionSafe`, `Advisory_RemainsFogSafe`.

Reference: original Phase 4D spec §§17–29 and `Docs/superpowers/plans/2026-09-25-commander-phase4d4.md`. Prior 4D.3 full suites 689/689 and 145/145 are historical, not substitutes for this final-source run.
