# Native guard-affinity RED job recovery

- Exact launched handle: `c735a72729b14f36bc01d8b55d3e48a8` (three focused tests: foreign invalid Stop, changed owner, zero health).
- Launch response: started/running. Initial 60-second exact-handle `get_test_job`: running, completed 0, total null, no test leaf or terminal result. Second 30-second exact-handle recovery: same running state with unchanged `last_update_unix_ms=1791733239898`.
- `mcpforunity://editor/state` after recovery: stale age about 95 seconds; `tests.is_running=false` but `current_job_id` is the exact handle. This is **unknown**, not missing or terminal. No relaunch, clear-stuck, editor restart, or source edit under this state.
- Console had only unrelated Package Manager auth errors, no script compile errors. Unity instance still listed `Open Empires@6d7310c7` on port 6400.

Next: recover the exact job terminal state when the bridge refreshes; save complete terminal payload before editing the guard.
