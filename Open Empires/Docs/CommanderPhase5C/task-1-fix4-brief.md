# Task1 fix round4 — game-owned similar-name confirmation

Read task-1-fix3-review.md first: prior mechanical findings addressed; new Important
near-name clarification is prompt-only. Source now exposes optional game-owned
interpretationOnly suggestions, but a provider can return actionable canonical
Request without confirmation. Fix only this bounded source/confirmation boundary.

Use the existing canonical resolver/catalog and typed pending-request machinery.
Do not attempt universal English entailment or authorize suggestions by provider
labels. A game-owned unresolved similar-name hint must not become an actionable
result solely because the provider emits its canonical candidate. Require explicit
local name confirmation/choice at that boundary. Preserve original full input and
strict validated proposal/graph, counts, modes, sources, restrictions, order,
dependencies, exact results and original owner/runtime/single-use request ticket.
Local yes/name/no uses ZERO extra HTTP; ordinary admission stays ordinary, compound/
future/DynamicPlan still requires its separate existing gameplay approval. One event
must not approve both stages. No silently substituted unrelated/cross-civ content,
new inferred terminal goal or manually maintained name/cost database.

Prefer adapting existing GraphDraft/PendingClarification over a second execution
model. Support the actually exposed actionable provider outcomes; don't guard only
one spelling while leaving an equivalent Request/DynamicPlan bypass. If no safe
typed proposal can be retained, fail closed with an explicit blocker while retaining
the original player draft; never request magic resubmission or drop another clause.
Exact names/established exact aliases and read-only Answers must remain unchanged.
Optional hints may be omitted from the model pack for budget, but that must not
erase a game-owned confirmation requirement. Preserve strict whole-payload<4000,
15s production timeout, one eligible repair, per-call immutable accounting and
late/cancel/runtime affinity. No new paid classifier/repair/fallback.

Fresh implementer owns fix4 after parent dispatch. Read current source before
choosing smallest generic adapter. Raise concrete architectural uncertainty early,
but do not spend this pass on another broad inventory/design document.

Test-first small fixtures through real provider/UI boundary: provider ignores hint
instruction and returns canonical actionable request -> zero goals/commands until
local confirmation; local yes keeps all original fields and uses no new HTTP; no
rejects proposal only; compound/future proposal retains graph and needs separate
approval; exact name unaffected; stale/reset/foreign/replay can't revive it;
unsupported/cross-civ candidate fails normal game checks. Cover any actual dynamic
outcome route if exposed. Label fixture proof, not live model/native acceptance.
Run only new cases plus directly affected prior budget/accounting/clarification
controls; do not repeat46/19/40 categories/full assembly/regression/builds.

One source writer/Unity runner; preserve all existing dirty work and Task2 safety.
No commits/reset/clean/worktrees/subagents/network/audio/paid calls/credentials/
multiplayer/Rust/builds/deploy. Use apply_patch; fresh actual compile/console/loaded
method checks, exact job recovery,120000init and one terminal0-start retry. No
restart/forcekill/observer-timeout duplication. Raw results after each run.
Append fix4 report to task-1-fix-report.md with exact changed files, RED/GREEN handles,
commands/results/hashes, self-review, limitations and explicit handback. Parent
scoped review; no whole-Phase5C claim. NativeTask3 still follows this gate.
