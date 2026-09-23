# Phase 4C.2 final regression summary

Date: 2026-09-22

Phase 4C.2 gate: **PASSED**.

- Scoped rereview: **APPROVED**; both findings addressed, no new Critical/Important breakage.
- Focused EditMode `4568d656eaaa490798eb59bbad443533`: 18/18 passed.
- Focused PlayMode `a25af57761a44dc5bdc481ae98a18824`: 6/6 passed.
- Full EditMode `e828d0a94df44912a78e740277d896a8`: 530/530 passed, zero failures/skips, 443.6047501s.
- First full PlayMode `0611af991765467aa53cac17c69686fe` was environment-failed only by Package Manager OAuth logging from `api.unity.com` in `CommanderPhase3A1PlayModeTests`.
- Clean full PlayMode rerun `0a3decd672b648e8a87a2f8f7f923c31`: 75/75 passed, zero failures/skips, 32.0661847s.
- Frozen source hashes: 7/7 matched; mismatch count 0.
- Boundary audit: 55/55 frozen entries present/unchanged, zero gaps; 6 advisory host refs; zero credential/assignment matches; `.env` untracked and ignored.

This passes Phase 4C.2 only. The overall Phase 4C goal remains open and Phase 4C.3 is not admitted.
