# Task brief: strict single-node semantic contract

Read with the approved design and Phase 4E.1 plan Global Constraints. This file is the implementation brief for Task 1 only.

+### Task 1: Strict single-node semantic contract

**Files:**
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticRequest.cs`
- Create: `Assets/Scripts/AI/Commander/Phase4E/CommanderSemanticJson.cs`
- Create: `Assets/Tests/EditMode/CommanderPhase4E1SemanticJsonTests.cs`

**Interfaces:**
- Produces `CommanderSemanticOutcome { Request, Clarify, Unsupported }`, `CommanderSemanticNodeType { EnsureUnitCount, BuildStructure, SetResourceAllocation, StrategicObjective }`, immutable `CommanderSemanticNode` with `Type : CommanderSemanticNodeType`, `UnitType : int?`, `BuildingType : BuildingType?`, `ResourceType : ResourceType?`, `Count : int?`, `StrategicObjectiveType : StrategicObjectiveType?`, and immutable `CommanderSemanticResult` with `IsValid : bool`, `Outcome`, `Nodes`, `SafeExplanation`. Invalid JSON is a safe rejected result (`IsValid=false`), not a fourth semantic outcome; only trusted parser code can set that flag.
- Produces `CommanderSemanticJson.Parse(string raw) : CommanderSemanticResult`. Parsed enums and numbers are data, not authority; semantic validation against current `CommanderContext` occurs at admission.
- Version-1 JSON shape: `{"outcome":"Request","nodes":[{"type":"EnsureUnitCount","unit":"Spearman","count":10}]}`; `BuildStructure` uses `structure` and `count`; `SetResourceAllocation` uses `resource` and `count`; `StrategicObjective` uses `objective`. `Clarify` and `Unsupported` have no nodes and optional `message` of at most 180 plain-text characters. No other fields are accepted.

- [ ] **Step 1: Write failing NUnit tests** for a valid Spearman node, a valid `RangedReinforcement` strategic node, Clarify, Unsupported, and rejection of duplicate/trailing/unknown/authority fields, comments, NaN, negative or >200 unit count, >20 structure count, unsupported enum, >4 nodes, and wrong fields for node type. Assert the returned typed result or `IsValid=false`, never a partially parsed request. Resource-worker count is 0–200; actual context population is checked again at admission.
- [ ] **Step 2: Run** `CommanderPhase4E1SemanticJsonTests` in Unity EditMode. **Expected:** compile/test RED because the new contract/parser is absent.
- [ ] **Step 3: Implement** the produced types and `Parse` signature. Use Json.NET's duplicate-property rejection and an explicit syntax/field/type allowlist; exact case-sensitive enum strings, no `Enum.TryParse` numeric acceptance. Cap collections before allocating retained nodes. Do not keep `JObject` or provider JSON in the result.
- [ ] **Step 4: Run** `CommanderPhase4E1SemanticJsonTests` in Unity EditMode. **Expected:** all new tests pass, zero skipped.
- [ ] **Step 5: Commit** only the Task 1 source/tests/meta files as `feat: add bounded Commander semantic contract`.
