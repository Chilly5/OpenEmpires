# Phase 4C boundary-audit usage

Run from the Unity project root (or pass `-Root`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/CommanderPhase4C/check-boundaries.ps1
```

The script is read-only and writes no result file. It emits JSON containing
current SHA-256 changes/new/missing paths against
`Docs/CommanderPhase4C-source-baseline.json`, selected frozen-boundary hashes,
forbidden advisory references with path/line/symbol only, credential-shape
counts without values, and `.env` tracked/ignored booleans. It excludes `.env`
from scanning and excludes the audit script itself to avoid detector-regex
self-matches. `CommanderChatUI`, `StrategicContextBuilder`, and
`StrategicPlanner` are reported as documented game-owned boundary exceptions;
the audit does not claim the entire Commander tree is simulation-free.

Output while another worker is changing sources is an in-flight diagnostic,
not final Phase 4C or 4C.1 evidence. The orchestrator must interpret whether a
reference is an allowed host boundary or an advisory violation and must save
any final evidence through the phase gate process.
