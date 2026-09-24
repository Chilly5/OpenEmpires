# Phase 4C.4 final regression and gate evidence

## Final status

**READY FOR PHASE 4D.** The subsequent scoped population-cap correction passed
fresh full EditMode 571/571 and PlayMode 87/87. The 27-entry source manifest
and protected-boundary audit passed; independent review found no Critical or
Important defect. Dynamic housing completed through real chat approval in
PlayMode. Earlier results below are retained as historical evidence.
`TechnologyRush`, `SiegePreparation`, and `NavalExpansion` remain unsupported
and deferred.

## Direct-player priority fix

- RED job `57a6f0186d67487cb80bcd311dede12c`: 2/2 failures reproduced
  `ArgumentOutOfRangeException` for new objective enum values 4 and 5 in
  `StrategicDecisionPolicy.GetPriorityLevel`. XML
  `phase4c4-direct-priority-red.xml`, SHA-256
  `1758F9B1991D77FFC6F4C7FB0F2E6982CA9306EB9893C23E7CD3A812B4BC71B4`.
- GREEN job `0165321d560f4a1ab1608b0f6678579c`: 2/2 passed. XML
  `phase4c4-direct-priority-green.xml`, SHA-256
  `0004F1D10437FBF4A16235EA2CDE7941BB5807FC8835086EE47582E97B927A69`.

## Prior full regressions (historical)

- EditMode job `5c24beac55114fe68b5e0f3ff6e7ea88`: **568/568 passed**, zero
  failures/skips; all 568 unique test names passed. XML
  `phase4c4-full-editmode-postfix-results.xml`, SHA-256
  `D2FA8CB42DFC5D14C840DE2C4E89C1087CF36C8469203303BB72EA022F4704B7`.
- PlayMode job `8a6b556afc76436abbbe993866864004`: **86/86 passed**, zero
  failures/skips; all 86 unique test names passed. XML
  `phase4c4-full-playmode-postfix-results.xml`, SHA-256
  `65C83287373175808F42C1BF0AD52E204A89D4ED956F8D1F633148BC2FD8122B`.
  JSON `phase4c4-full-playmode-postfix-results.json`, SHA-256
  `479A798FECF02CAC9F15EAF10CF20831EDF7A810E292AC7C1125DE2AD2B8A9F0`.
- Prior pre-priority-fix runs (EditMode 566/566, PlayMode 86/86) are historical
  only and superseded by the final pair above. Initial full EditMode stale-test
  failures and the test-only compatibility update are documented in the A–G
  report and progress ledger; production behavior was not changed for those
  compatibility assertions.

## Focused runtime, boundary, and review

- After the funded-Tower review fix, focused EditMode passed 26/26 and focused
  PlayMode passed 9/9, including one/two funded Tower foundations, depletion
  recovery, resource-funded one-worker fitting, and completion of both
  objectives through the actual chat/approval UI. Dynamic housing was not
  demonstrated at that earlier gate: the fixture prebuilt six Houses. The
  subsequent housing test below removed those six Houses.
- Final source audit: changed21, new21, missing0; protected55/55 unchanged;
  gaps0; six documented `CommanderChatUI` host references; credential-shape0,
  assignment0. `.env` remains untracked and ignored.
- `phase4c4-final-source-hashes.json`: 27/27 current SHA-256 entries verified,
  covering 24 Task 1 paths plus the priority policy and two legacy compatibility
  test files. All 12 Task 1 before snapshots match. The Task 1 manifest remains
  a historical freeze; its `CommanderPhase4C4Tests.cs` entry predates the
  direct-priority tests. The priority policy before/current snapshots match.
- The earlier independent review recommended readiness at its gate. The later
  housing proof and fresh regressions supersede its runtime limitation; the
  three unsupported objectives remain deferred.

## Final scoped population-cap correction

- The Antigravity audit found that feasibility rejected at the current cap
  before counting future House capacity. Only that premature rejection was
  removed; the existing maximum-population and fail-closed checks remain.
- Test-first RED job `03e03bbe10b047288e3c743ae848668d` reproduced the
  defect. The focused EditMode job `ce5c2db6cb3149edbe05d1d5d13f7a83`
  passed **31/31**; focused PlayMode XML passed **10/10**. The separate
  dynamically constructed House test passed **1/1** via job
  `21fc37ac99be449f9dd78bf263652ee2`.
- Complete EditMode job `024a2c805b33467b947965ae57cc309c` passed
  **571/571**; `phase4c4-housing-full-editmode.xml` SHA-256
  `771A0EFB388EB21B0B478817E50DD58E70A4BC27B915F88AEEC1EDA11A1B2B66`.
  Complete PlayMode job `4fe992b52c814dc5a36e67fa3c854389` passed
  **87/87**; `phase4c4-housing-full-playmode.xml` SHA-256
  `63ED603142539BE6ED6BAFE4BB6824B8C34C0EA9192EEF8891D678FB0D466B28`.
  Both full runs had zero failures, skips, and inconclusive tests.
- The UI-approved PlayMode case began at population 10/10 with no House,
  observed House foundation/completion, subsequent Archer training, ten
  Archers, completed plan, and released reservations. The two reported stray
  scene/meta pairs were verified as unreferenced test/recovery artifacts and
  removed; recoverable in Git at `205c4fa`.
- Updated `phase4c4-final-source-hashes.json` matches 27/27 files; SHA-256
  `76D571C3FB9FED87206712D5FD559FE162F1E034754CA2E3B8A765174A4E245F`.
  `phase4c4-housing-final-boundary-audit.json` SHA-256
  `A0B76274D4D7F0E4ED9110C24CB7F95D0CFBAC2B56343E745C0D16D455E68122`
  reports protected55/55 unchanged and zero audit gaps. Independent final
  review found no Critical or Important code defect.
- Known narrow-scope accounting caveat: the feasibility quote includes a
  dynamic House, but plan reservations cover only static milestones. The
  funded runtime path passed; planner-budget redesign was outside this fix.
