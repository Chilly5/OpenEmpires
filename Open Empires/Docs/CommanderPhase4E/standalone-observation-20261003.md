# Standalone observation and log-routing check — 2026-10-03

This is partial evidence, not a scenario-completion result.

## Directly observed interactive session

Computer Use selected the `OpenEmpires-Phase4E.exe` window (window ID `6426878`). The normal menu reached Single Player → 1v1. The match displayed `OpenRouter Luna translator ready`.

The exact typed input `hey I want 10 spearmen` was visibly echoed in the Commander transcript and answered with `Understood. Preparing 10 spearmen.` House and Barracks foundations were visible while resource totals changed.

Later observations showed an active strategy at `Production Capacity`, temporarily blocked with two queued units, and population growing through 21/30, 25/30 and 30/30. At the final observation (match clock 3:49), another foundation was still progressing. This does **not** prove an unrecoverable population stall, ten living Spearmen, or completion of the tactical goal. The displayed strategic health must not be substituted for the tactical goal's status.

Source inspection identifies `Production Capacity` as a milestone of `MilitaryReinforcementPlan`. The existing `StrategicPipeline.Tick` evaluates deterministic recommendations, and `RuleBasedStrategicDecisionPolicy` can select military reinforcement without a provider request. Therefore the active strategy alone is not evidence that the provider selected or approved it. Correlated goal/plan traces are needed before attributing interference or diagnosing a defect.

The targetable session received Alt+F4. No log-backed clean-shutdown claim is made.

## Separate restricted-process log checks

Unity's documented `-logFile` argument successfully routed logs into the workspace. The following files are retained under `Builds/Phase4E/`:

| File | Bytes | SHA-256 |
| --- | ---: | --- |
| `phase4e-log-routing-check-20261003.log` | 3603 | `B3E798A051CD1DB093507C3749CF7FB0C79F9D920D9033720CC475843D202FE4` |
| `standalone-interactive-20261003.log` | 6577 | `5BE747AA60179EEBC4398A71D27F2E87D7F94F1533CDBBAA734C16474FBA98AA` |

These shell-launched processes were **not** the Computer Use targetable match above. They initialized Unity/Direct3D, encountered network connection failures and a `PlayerPrefsException` from `NetworkManager.ProbeAllRegions`, and were terminated by exact process ID (14956 and 18668). Neither log contains the match's Commander goal trace. They must not be presented as that match's logs, provider results, zero-exception smoke proof, or gameplay completion proof. The preference failure is consistent with restricted-process writes, but its cause has not been independently reproduced outside the sandbox.

## Next correlated acceptance run

Launch a single interactive player with a unique writable log filename:

```powershell
& 'D:\unity_projects\OpenEmpires\Open Empires\Builds\Phase4E\OpenEmpires-Phase4E.exe' -logFile 'D:\unity_projects\OpenEmpires\Open Empires\Builds\Phase4E\acceptance-unique-run.log'
```

Use a fresh filename for each run. Preserve external provider configuration; do not add keys to arguments, logs, or documentation. Confirm that the selected window belongs to this exact launch before counting any goal trace. Record the input transcript, goal completion/living count, created producer for compound requests, and normal manual-command override. The default log does not itself record literal player text, so retain the observed prompt separately.

Reference: [Unity Player command-line arguments](https://docs.unity.com/en-us/engine/6000.3/manual/unity-editor/command-line-arguments/player).

## Fresh compound observation

A second targetable standalone match accepted the exact text:

```text
make a barracks left of my town center 5 tiles apart and then from that build 10 spearmen
```

The UI visibly echoed the complete request and replied `Compound Commander order submitted.` Within the first minute, a Barracks foundation appeared to the map-west of the Town Center and additional construction/economy work was visible. At match clock 3:02, the player had reached 27/30 population and the UI showed an active strategic resource wait after a plan transition. The transcript did not provide an independently readable dependent-goal completion or producer identity, so this run strengthens submission/placement evidence only; it does not prove the mandatory standalone 10-Spearman compound result.
