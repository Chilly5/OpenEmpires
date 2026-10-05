# Phase 4G capability audit

Source inventory date: 2026-10-04. The live Unity working tree is authoritative.

| Family | Native game path | Commander state | Evidence / boundary |
|---|---|---|---|
| Worker reassignment / gathering | `GameSimulation.ProcessGatherCommand`, `ResourceGatheringSystem`, `GatherCommand` | Fully supported for resource allocation; selector expansion needed | Worker selection remains deterministic and human-protected. |
| Construction / placement | `ProcessPlaceBuildingCommand`, `PlaceBuildingCommand`, `ConstructBuildingCommand` | Fully supported for existing structure goals; contextual resource anchors missing | Exact tile, footprint, age, resources and reachability stay game-side. |
| Production | `ProcessTrainUnitCommand`, `TrainUnitCommand` | Fully supported for current four Commander units | Producer, civilization replacement, cost and age are canonical simulation checks. |
| Research / age | `ProcessResearchCommand`, `ResearchSystem`, landmark completion | Partially supported; age goal exists, generic research action missing | Use `GetTechnologySpec` and normal research command. |
| Movement | `ProcessMoveCommand`, `MoveCommand` | Partially supported through strategic/AI intents | Generic semantic unit selectors and destinations are required. |
| Attack / defend / retreat | `AttackUnitCommand`, `AttackBuildingCommand`, AI attack/defend intents | Partially supported | Only visible, owned/known targets may be resolved. |
| Patrol | `ProcessPatrolCommand`, `PatrolCommand` | Native mechanic exists; Commander semantic path absent | Phase 4G adds a bounded patrol action. |
| Rally | `SetRallyPointCommand` | Partially supported (army-wide AI intent) | Phase 4G adds production-building + resource selector resolution. |
| Repair | `ProcessRepairBuildingCommand`, `RepairBuildingCommand` | Partially supported through AI repair intent | Phase 4G adds nearest damaged-owned-building selector. |
| Garrison | `GarrisonCommand` / `UngarrisonCommand` | Partial semantic protection only | No arbitrary garrison selector is added until a safe generic contract exists. |
| Scouting / visibility | `FogOfWarData`, `UnitMovementSystem` | Partial | Provider receives bounded visible/explored state; hidden objects never enter selectors. |
| Resource-node relations | `UnitData.TargetResourceNodeId`, `MapData.GetAllResourceNodes` | Read-only relation is available | Worked-resource selectors filter actual owned villagers, not proximity guesses. |
| Information queries | `GameKnowledgeCatalog`, bounded context | Partial | Static catalog facts are available; broad natural-language Q&A remains an AntiGravity scenario. |

Phase 4G implementation uses generic semantic action nodes and a game-side resolver. The provider never supplies entity IDs, workers, coordinates, or commands. Unsupported native mechanics remain honest `Unsupported` responses.
