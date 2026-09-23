# Phase 4C.4 final regression and gate evidence

## Final status

**READY FOR PHASE 4D.** The direct-player priority finding was fixed and
verified; fresh full regressions passed; the final 27-entry source manifest and
protected-boundary audit passed; independent review recommends readiness with
no remaining Critical or Important issue. Dynamic housing runtime completion
remains unproven but is documented as a non-blocking coverage limitation.
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

## Final full regressions

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
  demonstrated: the focused fixture prebuilt six Houses.
- Final source audit: changed21, new21, missing0; protected55/55 unchanged;
  gaps0; six documented `CommanderChatUI` host references; credential-shape0,
  assignment0. `.env` remains untracked and ignored.
- `phase4c4-final-source-hashes.json`: 27/27 current SHA-256 entries verified,
  covering 24 Task 1 paths plus the priority policy and two legacy compatibility
  test files. All 12 Task 1 before snapshots match. The Task 1 manifest remains
  a historical freeze; its `CommanderPhase4C4Tests.cs` entry predates the
  direct-priority tests. The priority policy before/current snapshots match.
- Independent final review recommends **READY FOR PHASE 4D** and records no
  remaining Critical or Important finding. Housing's runtime gap is
  non-blocking; the three unsupported objectives remain deferred.
