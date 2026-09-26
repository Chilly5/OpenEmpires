# SDD ledger — plan: Docs/superpowers/plans/2026-09-26-commander-phase4e1-language-boundary.md

Baseline: `dedf0e4bf7df6e4d02a2aad49f1a6201e8410f26`; full current-head Unity EditMode 703/703 and PlayMode 159/159, zero failed/skipped.

Ruling: Execute in the current non-main Unity checkout rather than a linked worktree — the live Editor and MCP runner are attached to this checkout, and an isolated checkout would not provide the required runtime evidence — cost if wrong: changes are visible in the user's current feature branch before final acceptance. Preserve unrelated edits and use narrow commits.

Ruling: Keep the SDD ledger and task briefs in `Docs/CommanderPhase4E/` rather than a generated ignored scratch directory — the Windows-hosted Bash entrypoint is not a reliable repository tool and the ongoing goal requires durable evidence across continuations — cost if wrong: minor documentation noise in the branch.

Ruling: Task 3's tactical admission uses the existing `CommanderIntentDtoCodec.ValidateAndConvert(dto, context)` and requires a tactical intent with no strategic intent — this is the live context validator and avoids inventing a second simulation validation path — cost if wrong: later semantic fields may require an explicit game-side validator extension.

Pre-flight shared-interface scan:

| Tasks | Interface check | Finding |
| --- | --- | --- |
| 1→2 | `CommanderSemanticJson.Parse` and result type used by provider | Names and return type match. |
| 1→3 | Typed node and outcome used by game-side admission | Names and fields match; actual context limits must be rechecked. |
| 1→4 | Strategic typed objective used by bridge | Name matches; bridge must supply identity/provenance. |
| 2→3 | Semantic provider request/result used by host | Names match; host must retain legacy route for non-semantic providers. |
| 2→4 | Provider result used for strategic staging | Host stages only after owner/generation recheck. |
| 3→4 | Both tasks touch `CommanderChatUI.cs` | Sequential ownership only; Task 4 follows accepted/reviewed Task 3. |
| 1–5 | Each task's tests and files against own output | No self-contradiction found in the plan's first scan. |

Task 1: in progress (strict semantic JSON contract); production writer: GPT-6 Sol subagent; Unity runner: root.
Task 1 RED: Unity MCP EditMode job `a98e92f4eaee40a591b7bc7d7cf47ce6` (exact-method retry `5203efe151824d74b3d24991eb75ad8b`) discovered 0 runnable tests because the new assembly failed to compile, as expected before the contract exists. `Logs/Editor.log:609495-609524` reports CS0103 on `CommanderSemanticJson`, `CommanderSemanticOutcome`, and `CommanderSemanticNodeType` from the new test file; this is the expected missing-feature failure, not a baseline regression. Production code had not been written. The runner's 0/0 `Passed` summary is **not** test success.
Task 1 GREEN: after an import/domain reload temporarily disconnected Unity MCP (no job ID/result was created for that attempt), the exact focused EditMode run `80cf31a3531d4daf8d3c630f27d728a4` passed 31/31, 0 failed, 0 skipped, duration 0.3769673 s. Full result payload: `phase4e1-task1-green-80cf31a3.json`; Unity error console reported 0 entries. Commit and independent review pending.
Task 1 initial commit: `e39d987` (`feat: add bounded Commander semantic contract`). A Sol reviewer could not return a verdict because of a Codex usage-limit error; a Luna reviewer found that the result's internal constructor can set `IsValid=true` outside the parser. Controller source review additionally found that all defined `BuildingType` values, including unsupported `Landmark`, were accepted by `ParseBuilding`. These are confirmed review findings, not accepted behavior.
Task 1 fix RED: two new tests were added before production changes. Unity MCP job `8696813c3ffb4c0187fc34761870233d` remained in a stale `running 0/31` state after a domain reload, while the Editor was idle and the Unity Test Runner had actually finished. Its authoritative `TestResults.xml` reports at 2026-09-26 16:23:40 UTC: 33 total, 31 passed, 2 failed, 0 skipped. Failures are `Parse_RejectsDefinedButUnsupportedStructure` (expected invalid, got valid) and `SemanticResult_HasNoAssemblyAccessibleConstructorOrValiditySetter` (assembly-accessible validity construction still exists). Raw XML preserved as `phase4e1-task1-fix-red-8696813c.xml`. Production fix and GREEN pending; do not treat the MCP handle's stale progress as a terminal result.
Task 1 fix GREEN: after read-only editor state showed `tests.is_running=false` and the XML proved the RED run had finished, Unity MCP `run_tests(clear_stuck:true)` cleared the orphaned handle. Focused EditMode job `43613b32d7e941dd9ad661a0b2da177a` passed 33/33, zero failed/skipped, duration 0.7775014 s; error console 0. Complete payload is `phase4e1-task1-fix-green-43613b32.json`. The narrow fix commit is `69471e5` (`fix: enforce semantic parser validity and structure boundary`); scoped independent re-review pending.
Task 1: complete (commits `dedf0e4..69471e5`, first review found two bounded issues; fix round 1/5 addressed 2/2; scoped re-review approved; focused final 33/33). This closes the parser task only, not provider/chat/gameplay admission or Phase 4E.
Task 2: in progress (detached Luna semantic provider); one production writer, root remains the sole Unity runner.
