# Task 6 Fix Round 1 scoped re-review — 2026-10-12

### Finding Verdicts

- **Actual request correlation on rejected/read-only submissions — ADDRESSED.** `Assets/Scripts/AI/Commander/Phase4A/CommanderChatUI.cs:51,483` exposes the locally minted semantic ticket after creation, while `CommanderChatUI.Presentation.cs:139-146` resets it per UI turn and exposes a separate observation revision. `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:936-944,1360-1364` records `runtime:1` for the malformed reply and a null executable `requestId` plus `ui-turn:1` for read-only questions; `Assets/Tests/Shared/ScenarioEvidenceRecorder.cs:77-83` accepts those as distinct safe fields. The current saved JSON has exactly those values, and the correlation/artifact XML is 3/3 Passed.
- **Malformed/schema-valid-wrong negative gates — ADDRESSED.** `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:1424-1458` now makes malformed rejection contingent on one invalid parse with no pending state, goal or Commander command, and makes the wrong result contingent on a valid, exactly divergent Wood/Tree/count-two admission retaining the Gold/count-three original utterance. Failures become `open-gap`/`mismatch`. The adversarial malformed, valid-faithful and invalid-wrong cases at `:218-277`, plus persisted-record assertions at `:387-409`, cover both paths; current artifacts show the expected one malformed and one valid-wrong diagnostic. Saved verdict XML is 3/3 Passed.
- **Exact new-result patrol actor state — ADDRESSED.** `Assets/Tests/EditMode/CommanderEconomyClarificationTests.Phase5CCorpus.cs:1381-1401,1545-1570` checks both exact producer-attributed actors remain alive, owned and `IsPatrolling`, with two waypoints matching the game-side command target and a current native destination on the route; the old/Human-born exclusions and `Executing` (not Completed) status remain. The current artifact records 2 actors and true route/destination facts. The fix also repairs the observed occupied-anchor failure only for PatrolArea: `Assets/Scripts/AI/Commander/Phase4G/CommanderCapabilityExecutor.cs:117-126,156-213` chooses a deterministic known, walkable point within two tiles of the same footprint only when every selected actor has complete outward and return paths, otherwise emits no command. Adversarial cleared-route/no-route XML is 2/2 Passed; directly affected exact-binding/determinism controls are 2/2 Passed after fixture terrain/visibility/spawn corrections.

### New Breakage in the Fix Diff

- **Critical/Important:** None found in the nine changed sources. The patrol resolver is fail-closed, limited to the original anchor and exact actor set; the three old fixture adjustments change terrain visibility/forest density or synthetic spawn position, not authority or result assertions.
- **Minor — evidence path typo:** `Docs/CommanderPhase5C/task-6-report.md:86` omits the final `7` from the correlation job/XML basename. The actual saved file is `evidence/task-6-fix1-correlation-green-12bd37d1ebfc4d0f9f10812e0bb058f7.xml`; its SHA256 is the reported `A7150BE94B60D7703C6157C27B660647A79C59FF8B68701183EBDD060C1660A2`, and it contains 3/3 Passed leaves. Correct the handle/path in the report so the immutable evidence can be found literally.

### Checks and Evidence Limits

- Verified the 79,627-byte incremental review package SHA256 `05D2B188E98890193F95035F8EDEDEEA0FDC24F7482D38C258EA505A41566DA4` and all 24 current source identities against the saved manifest; none differ. The current corpus has 25 cases, 35 declared turns, four separately marked offline approval controls, 23 matched cases, one malformed negative, one wrong negative, zero open gaps or temporary files, zero HTTP attempts and no non-null actual usage/cost. Saved corpus XML is 1/1 Passed despite its MCP tracker initialization-loss report. The other covering XMLs are correlation 3/3, verdict 3/3, patrol 2/2 and direct controls 2/2. No test, MCP job, build or network call was rerun.

### Out-of-Scope Observations

- Live Luna English fidelity, actual Windows-direct/Web-gateway usage and cost, three bounded paid checkpoints, standalone Windows/served-Web populated screenshots, one full regression and final builds remain open Task 7 gates. Offline fixture and Editor/native-test evidence do not establish those results. Earlier Task 1–5 code and historical failed runs were not re-reviewed or erased.

### Verdict

**Fix round: All three findings addressed; no new Critical/Important breakage.** Scoped spec compliance and code quality are acceptable for the Task 6 fix. Correct the minor report handle, then keep Task 7's release and live-evidence gates explicitly open.
