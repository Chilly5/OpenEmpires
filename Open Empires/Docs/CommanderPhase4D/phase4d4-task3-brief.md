# Phase 4D.4 Task 3 brief — sub-phase gate and final Phase 4D audit

This brief is extracted from `Docs/superpowers/plans/2026-09-25-commander-phase4d4.md` Task 3 and the original Phase 4D objective. Do not begin final source freeze before Task 2 scoped review passes. This is an evidence-and-audit task with possible narrowly scoped missing runtime tests, not permission to widen strategic behavior.

**Files:** Update `Docs/CommanderPhase4D/progress.md`, `requirements-matrix.md`, task reports and ledger; create `phase4d4-gate-report.md`, `phase4d-final-source-hashes.json`, `phase4d-final-boundary-audit.json`, `Docs/CommanderPhase4D.md`, and a compact external Antigravity audit package. Add real PlayMode scenario tests only where required proof is missing.

- Freeze current source; verify all ten required advisory IDs discovered/passed, no failures/skips/inconclusive or compiler errors, and independent scoped review plus relevant Commander regression. Mark 4D.4 gate only if focused, import, review, regression, and boundary gates all pass.
- Run fresh complete EditMode, then complete PlayMode sequentially on final source. Retain exact terminal IDs/counts, native XML if available (else complete terminal payload), source/artifact hashes, and console state. Do not reuse historical 4D.3 results as final-source proof.
- Audit real PlayMode scenarios A–F requirement by requirement and add missing real scenarios before claiming them. Do not convert pure/unit evidence into runtime proof. The read-only preflight in `phase4d4-orchestration-ledger.md` found explicit gaps: D needs long-run no old-plan resurrection; E needs Plan A request → cancel/replace A → Plan B → stale request; F needs lifecycle/health/adaptation/advisory and execution checks after a held provider reply crosses reset.
- Verify protected baseline/current hashes, intentional authority-file drifts, source-scope and credential/forbidden-reference candidates without displaying secret values, `.env` state, packages/settings, and temporary/recovery-scene provenance. Make the source manifest machine-verifiable and verify every entry against live disk.
- Have an independent Sol whole-Phase4D reviewer check architecture, authority, stale async, ownership, resource safety, fog, test adequacy, manifest, and static audit. Close Critical/Important findings with RED→GREEN plus new full suites where shared authority changes.
- Write `Docs/CommanderPhase4D.md` with sections A–G and exact final line `READY FOR PHASE 4E` only if every gate and scenario is proved; otherwise end `REQUIRES FIX PHASE` with precise open items. Prepare source-verifiable Antigravity package but do not wait for external audit or begin Phase 4E.

## Binding final requirements

- The final manifest must cover every changed/new Phase 4D production and test file and record its own SHA-256 externally. The final boundary audit must include protected counts, intentional changes, audit gaps, credential pattern counts, forbidden-reference candidates, `.env` tracked/untracked/ignored state, package/settings changes, and unexpected files.
- Final full suites require zero failed, skipped, or inconclusive tests and all required named Phase 4D tests discovered with unique names where applicable.
- `Docs/CommanderPhase4D.md` sections A–G must contain architecture, delegation, new systems, safety boundaries, tests, exact runtime evidence, and limitations/deferred work. End with exactly one `READY FOR PHASE 4E` or `REQUIRES FIX PHASE`.
- READY also requires architecture/authority, runtime A–F, stale async, ownership, reservation, source manifest, static/security, and no open Critical/Important findings. Unsupported objectives, tactical Spearman wood diagnosis, and Phase4E remain out of scope.
- Do not stage, commit, reset, clean, delete inherited scenes, move credentials, or change packages/settings without a demonstrated need and root ruling. One Unity runner at a time.
