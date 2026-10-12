### Finding Verdicts

- **Older timeout advertises unavailable Retry/Edit/Cancel controls** — ADDRESSED. Both timeout branches now call one reporter (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:520`, `:697`, `:722-729`). For the latest turn it preserves the existing recovery offer and button instruction; for an older turn, `OfferRequestRecovery` leaves those controls hidden and the transcript instead identifies an earlier order with the available composer/Send route (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.Presentation.cs:291-296`; `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:725-728`). The focused older-order → newer-question → timeout test asserts the newer `Completed` status, no admission, hidden controls, accurate instruction, and exactly one manual re-entry attempt (`Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs:609-646`).

### New Breakage in the Fix Diff

- None identified. The change is confined to the shared timeout reporter and its causal fixture (`Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:514-522`, `:690-729`; `Assets/Tests/PlayMode/CommanderPhase5CUIPlayModeTests.cs:609-646`).

### Out-of-Scope Observations

- ⚠️ Native-size and real-match HUD/input, Windows and served-Web player output, measured complete sub-4K prompts, phase paid-attempt ledger, final offline suites, and builds remain separate cross-task gates; this re-review does not verify them (`Docs/CommanderPhase5C/task-5-report.md`).
- Check: the frozen incremental package hash is `4242FB90A3BC6724BC6FF52BAE3687E60BC459A28E695C743C8D51E2A0F98B62`. Saved terminal JSON shows the causal test **1/1** and directly affected timeout/cancel/order tests **3/3**, with zero failures or skips (`Docs/CommanderPhase5C/evidence/task-5-raw/f24a2a8edf924a6b929bfe0071d00f3b.json`; `Docs/CommanderPhase5C/evidence/task-5-raw/4dd007e61416417b8bcf2b2d3d9a6f67.json`). No test, MCP job, build, HTTP call or source edit was run by this reviewer.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.

**Spec compliance:** ✅ For this scoped Task 5 re-review. **Task quality:** Approved for the scoped fix; phase-level acceptance remains open.
