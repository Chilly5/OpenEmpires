# Knowledge architecture

Canonical OpenEmpires data remains authoritative. `GameKnowledgeCatalog` snapshots facts into detached records. `EffectivePlayerKnowledge` is a read-only player-state observation. `KnowledgeSlice` selects a deterministic bounded subset for provider context. `CommanderCapabilityCatalog` is an explicit execution policy and never follows from discovery automatically.
