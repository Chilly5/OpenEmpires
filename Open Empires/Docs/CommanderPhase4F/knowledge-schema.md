# Knowledge schema

Stable IDs are `unit:<integer>`, `building:<enum>`, `technology:<enum>`, `civilization:<enum>`, `age:<integer>`, and `resource:<enum>`. Display names and aliases are non-authoritative presentation/lookup helpers.

The provider slice (`KnowledgeSlice`) deterministically serializes structured canonical facts:
- **Units**: `id` (`unit:<int>`), `name`, `age` (required age), `producer` (`building:<enum>`), and `cost` (`food`, `wood`, `gold`, `stone`).
- **Buildings**: `id` (`building:<enum>`), `name`, `age` (required age), and `cost` (`food`, `wood`, `gold`, `stone`).
- **Technologies**: `id` (`technology:<enum>`), `name`, `age` (required age), `researchBuilding` (`building:<enum>`), and `cost` (`food`, `wood`, `gold`, `stone`).

All numeric facts are derived from canonical simulation and configuration sources (`SimulationConfig`, `GameSimulation`, `LandmarkDefinitions`, `ResearchSystem`), remaining detached, read-only, and bounded under the 8,192-character provider context budget. Lookup ambiguity and duplicate stable IDs fail closed.
