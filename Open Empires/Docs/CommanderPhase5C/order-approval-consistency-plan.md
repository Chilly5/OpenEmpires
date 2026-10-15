# Movement order and approval consistency plan — 2026-10-15

**Scope:** Existing-unit movement to a player marker. Keep result-bound new-unit movement, other capabilities, voice, packaging and multiplayer on their current paths. The goal objective is `C:/Users/RS/.codex/attachments/d27f3ebf-c9c8-4423-924c-3476f0919ee1/goal-objective.md`.

1. Add a failing focused test: an equivalent Request and DynamicPlan marker move produce no goal before approval, admit once after approval, and retain exact actors and marker.
2. Introduce a pure game-owned draft over the already validated and compiled effect graph. It records effect nodes, dependencies, constraints, selector and location semantics, marker snapshot and request identity; count effects rather than provider nodes. Use it for movement approval policy and candidate revision checking.
3. Route both marker movement forms through the same pending candidate. Reject automatic KnownIntent admission for this migrated slice at the manager boundary. Keep trusted typed input and other established routes intact.
4. Share movement vocabulary between guidance, strict parsers and relevant clarification slots without changing saved valid Request or DynamicPlan representations.
5. Replay available saved envelopes through production parsing, then run focused movement, authority and one offline UI fixture. Update source identity, `remaining_work.md`, and report scoped evidence and open gates.

No reset, clean, commit, push, build, full match, full suite or paid call. Stop at the 90-minute checkpoint with an honest partial handoff if needed.
