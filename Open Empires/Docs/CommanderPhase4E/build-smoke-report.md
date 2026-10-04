# Phase 4E Windows standalone build and smoke

Date: 2026-10-03

## Current-source build

- Unity: `6000.5.9f1`; target: `StandaloneWindows64`; enabled scene: `Assets/Scenes/SampleScene.unity`.
- Player: `Builds/Phase4E/OpenEmpires-Phase4E.exe`.
- Unity MCP build job `build-4708d12b70`: succeeded in 280.016 seconds, 0 errors, 74 warnings, 214.55 MB total output. This rebuild includes the bounded top-K economy candidate ranking fix, current Castle resource planner, strict Unicode validation, unreachable-route handling, and OpenRouter placement-contract changes. It received a fresh standalone smoke: menu → 1v1 → Luna readiness → exact natural input `hey I want 10 spearmen` → visible `Understood. Preparing 10 spearmen.` → House/Barracks preparation and changing resources → clean quit.
- The build output contains no `.env` file and a binary scan found no `sk-or-v1-` or Gemini-style key pattern. This is a bounded packaging check, not proof that arbitrary secrets could never be present.
- **Standalone launch/UI smoke and log verification completed (2026-10-04).** The latest player launched into the menu and a 1v1 match, displayed `OpenRouter Luna translator ready`, accepted typed natural requests including compound orders, displayed appropriate acknowledgements, created House/Barracks, trained units, and quit cleanly with Alt+F4. Direct inspection of `C:\Users\RS\AppData\LocalLow\DefaultCompany\Open Empires\Player.log` (52,261 bytes, 500 lines) confirmed zero fatal or unhandled exceptions, exact 5-clear-tile Barracks placement at (186,131), and 10/10 living Spearmen completed at bound Barracks #6.

| Latest artifact | SHA-256 |
| --- | --- |
| `OpenEmpires-Phase4E.exe` | `36C5C9F13481406382A8E9EF8FC0EA7CDF055C43BB12FC8FD545B07C199CD277` |
| `OpenEmpires-Phase4E_Data/Managed/OpenEmpires.Runtime.dll` | `6C97B40A2013732C705F14BE395B52FBAF137520D07D81DA2362AC26E7BE1265` |
| `OpenEmpires-Phase4E_Data/resources.resource` | `C8CFB26C682EA2346F2F187C3B9D5192D997076B85CD8468BAB653A8A4EDE25A` |
| `OpenEmpires-Phase4E_Data/boot.config` | `2474D68768E6AE7C393016C26237BA554C12FA22F4AE68501D324F6FD27D0EA1` |

## Earlier playable standalone evidence

The earlier build job `build-579c1df8fa` was launched into a normal single-player match through the menu. The Commander UI displayed `OpenRouter Luna translator ready`; the operator entered natural ten-Spearman requests. `Player.log` records Goal #1 submitting as `EnsureUnitCount`, placing a House and Barracks, gathering food, and completing with 10/10 living Spearmen. Two later ten-Spearman goals also completed at 10/10. The log showed Direct3D 12, PhysX, and input initialization, with no fatal exception in the inspected run. The post-fix smoke above adds direct visual evidence for the rebuilt artifact, while the current sandbox still cannot read its LocalLow Player.log.

- Player log snapshot: `C:\Users\RS\AppData\LocalLow\DefaultCompany\Open Empires\Player.log`, 148,079 bytes, SHA-256 `1E3C497233DB92207AAB915A7B5C70D3E7046A8F91996D97A3B286F1A7350954`.
- The earlier gameplay session exposed a transcript visibility defect: new replies could remain below the visible viewport. A focused RED/GREEN PlayMode regression now passes for the source fix, but only the newest build contains it; visual standalone verification remains pending.

## Correlated 2026-10-03 match verification (Audited 2026-10-04)

See `standalone-observation-20261003.md` for the interactive session. The actual match log was written to `C:\Users\RS\AppData\LocalLow\DefaultCompany\Open Empires\Player.log` (52,261 bytes, SHA-256 `BEA5EFE30030563462B2F4EC6B682627EBD22754B4B9A934FB407335F3F4ED60`). It confirms full completion of the compound request:
1. `Placing Barracks at (186,131) using exact semantic placement with 5 clear-tile gap and villager #0`
2. `Barracks construction complete at (186,131) as building #6`
3. `Queueing Spearman at Barracks #6` (bound producer #6)
4. `Goal #2: status=Completed owned=10 queued=0; Owned 10/10 living units`
5. Clean shutdown with 0 fatal/unhandled exceptions (`result=abandoned` upon Alt+F4 after 263,194 ms).

## Provider setup without embedding a key

Set `OPENEMPIRES_COMMANDER_PROVIDER` to `openrouter` and provide `OPENEMPIRES_OPENROUTER_KEY` through the launching process or a user-level environment setting outside the repository. Then launch `Builds/Phase4E/OpenEmpires-Phase4E.exe`. Do not put a key in build assets or documentation. The Gemini provider remains available when selected by the existing configuration path.

The standalone build satisfies the Phase 4E acceptance criteria.

