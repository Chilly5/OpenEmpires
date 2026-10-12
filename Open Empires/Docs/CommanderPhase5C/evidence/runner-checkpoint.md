# Unity runner checkpoint

- Project: `D:/unity_projects/OpenEmpires/Open Empires`
- Unity instance: `Open Empires@6d7310c7` (Unity `6000.5.9f1`)
- Test class: `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests` (EditMode)
- Test job ID: `b47820791b0742d190a2f6d7f16a4f26`
- Started with `init_timeout=120000`, `include_details=true`, `include_failed_tests=true`.
- Terminal status: `failed`; 8/8 completed, 3 failures (2 real production-context oversized/rejected requests; malformed usage metadata poisoned a valid reply). The endpoint exposed 5 passes by subtraction but omitted its `result` summary and all test output.
- Exact reported failure messages and raw terminal job data are persisted in `production-context-and-usage-red.json`.
- Numeric production-context estimates were not returned by Unity's job API; they are recorded as unavailable, not guessed.
- Pre-run editor state: compilation complete (`is_compiling=false`, no domain reload pending), editor ready, no active test job.
- Pre-run Unity console error query: zero entries.
- Post-run console captured one `InvalidOperationException` from `OpenRouterCommanderProvider` at line 417 during malformed-usage test; compilation remained observed as false, though the post-run editor-state snapshot itself was stale.
- Do not duplicate this job or launch another run; poll this exact ID to terminal.

## Fix validation rerun

- Root imported four source fixes and requested compilation; no tests were running.
- Active instance: `Open Empires@6d7310c7`, Unity `6000.5.9f1`.
- Fresh editor resource confirmed compilation complete (`is_compiling=false`, no domain reload pending), editor ready; Unity console error query returned zero entries.
- Test class: `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests` (EditMode), requested once with `init_timeout=120000`, details and failed tests enabled.
- Test job ID: `0d8c6f6761e94699bd88be56c3dd1210`.
- Terminal status: `failed`; Unity TestJobManager auto-failed initialization at the 120000 ms limit before any tests began (`completed=0`, `total=null`). Test discovery count is unknown, not zero and not a pass.
- Exact terminal job payload and timeout warning are persisted in `production-context-and-usage-green.json` (the filename reflects the requested evidence slot; its JSON status is the actual `failed` result).
- Re-pinned the same active instance and re-polled the same job ID; terminal state was unchanged. No replacement job was started.
- Post-timeout console warning: `[TestJobManager] Job 0d8c6f6761e94699bd88be56c3dd1210 failed to initialize within 120000ms, auto-failing`.
- Root authorized one bounded same-class retry after terminal confirmation. Retry job ID: `b863eeaa150a4464859e8666f7f3b2a8`, requested once with the same 120000 ms initialization timeout and detail flags.
- Retry terminal status: `failed`, 8/8 completed, 2 reported failures: production compound request conservative estimate `6472`; composed movement request estimate `6416`; both were rejected by the production input-limit guard (expected one HTTP transport call, got zero). The malformed-usage failure from the first run was absent from the retry's failure list. Six passes are inferred by subtraction; endpoint omitted `result.summary/results`.
- First-attempt initialization timeout is preserved alongside retry evidence in `production-context-and-usage-green.json`; actual final job status remains `failed`, not green.

## Lossless-pack validation

- Root imported the lossless fact/alternative table-encoding and updated focused-test sources and requested compilation; no job was active.
- Active instance: `Open Empires@6d7310c7` / Unity `6000.5.9f1`.
- Fresh state confirmed compile complete (`is_compiling=false`, no domain reload pending), assets not updating, editor ready. Console errors: zero.
- Requested only `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests` in EditMode with `init_timeout=120000`, details and failed tests enabled.
- Test job ID: `3aec4f7c9fba420990274e3b37c07af4`; latest status: `running`. Poll this exact handle; one initial attempt only, except one bounded retry if this attempt terminally fails before tests start.
- Terminal status: `failed`, 8/8 completed, 2 reported failures: compound production-context request conservative estimate `4507`; composed movement request estimate `4499`; both were refused by the input-limit guard (expected one transport call, got zero).
- The job endpoint omitted `result.summary/results`; six passes are inferred by subtraction from 8 completed and 2 failures. No retry was needed because tests ran to completion.
- Console error/warning query after run: zero entries. Full terminal payload and exact failure messages are in `provider-pack-lossless-focused.json` (actual status recorded as `failed`).

## Combined-facts validation

- Root imported merged fact/capability rows and updated focused test source; no job active.
- Active instance `Open Empires@6d7310c7`, Unity `6000.5.9f1`. Fresh state: `is_compiling=false`, no domain reload pending, assets not updating, editor ready. Pre-run console errors: zero.
- Ran only `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests` in EditMode (`init_timeout=120000`, details and failed tests enabled).
- Job ID: `6cd08e1f118c446a8bf0681147bd51ed`; latest status `running`. Track this exact job; one initial run, and at most one retry only if a terminal initialization failure occurs before tests start.
- Terminal status: `failed`, 8/8 completed, 2 reported failures: production compound request estimate `4080`; production movement request estimate `4133`; both input-limit rejections (expected one transport call, got zero).
- The first status poll hit a transient bridge disconnect and the instances resource briefly showed zero instances. After `Open Empires@6d7310c7` reappeared, re-pinned it and polled the same job ID to terminal; no new job was started.
- Endpoint omitted `result.summary/results`; six passes inferred by subtraction from 8 completed and 2 failures. Post-run error-only console query returned zero; combined warning query returned 30 obsolete-API warning entries.
- Full raw terminal payload and diagnostics persisted in `provider-pack-combined-facts-focused.json`; no retry needed because all 8 cases completed.

## Final-core validation

- Root imported the research-schema relevance / duplicate-flag minimization refinement and requested compilation.
- Fresh editor state before run: `Open Empires@6d7310c7`, Unity `6000.5.9f1`, `is_compiling=false`, no domain reload pending, editor ready. Pre-run console error query: zero entries.
- Requested only `OpenEmpires.Tests.CommanderPhase5CProviderBudgetTests` in EditMode with `init_timeout=120000`, details and failed tests enabled. The tool call timed out waiting for Unity and returned no job ID; editor state then showed `running_tests` and recovered exact handle `50fa52759f9e4d2c99a9f6928102a955`. No second run was launched.
- Terminal job result: `succeeded`, 8/8 passed, 0 failed, 0 skipped, 0.8022549 seconds. Production catalog conservative estimates: compound `3931`; movement `3867`. Ordinary request `estimateWithMargin`: `2042` and `2735`; actual prompt tokens unknown.
- Post-run console error query: zero entries. Full per-test output and terminal summary saved in `provider-pack-final-core-focused.json`.
