using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class UnitHealingSystem
    {
        public void Tick(UnitRegistry registry, SimulationConfig config, SpatialGrid spatialGrid, int[] playerTeamIds, int currentTick,
            MapData mapData = null, BuildingRegistry buildingRegistry = null,
            Action<UnitData, float> onKingHealingAuraPulse = null,
            Action<BuildingData, float> onBuildingHealingAuraPulse = null)
        {
            var allUnits = registry.GetAllUnits();
            int count = allUnits.Count;

            for (int i = 0; i < count; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                bool isKing = unit.UnitType == UnitData.KingUnitType;
                if (isKing)
                {
                    TickKingHealingAura(unit, allUnits, count, config, playerTeamIds, currentTick, onKingHealingAuraPulse);
                    continue;
                }
                if (!unit.IsHealer) continue;

                Fixed32 healRange = Fixed32.FromFloat(config.MonkHealRange);
                Fixed32 healRangeSq = healRange * healRange;
                Fixed32 detectionRange = Fixed32.FromFloat(config.MonkDetectionRange);
                Fixed32 detectionRangeSq = detectionRange * detectionRange;
                int healAmount = config.MonkHealAmount;

                // Tick cooldown
                if (unit.AttackCooldownRemaining > 0)
                    unit.AttackCooldownRemaining--;

                // If player commanded to heal a specific target, track that target
                if (unit.HealTargetId >= 0 && unit.PlayerCommanded)
                {
                    var cmdTarget = registry.GetUnit(unit.HealTargetId);
                    if (cmdTarget == null || cmdTarget.State == UnitState.Dead ||
                        cmdTarget.CurrentHealth >= cmdTarget.MaxHealth)
                    {
                        // Target dead or fully healed — clear command
                        unit.HealTargetId = -1;
                        unit.PlayerCommanded = false;
                        if (unit.State == UnitState.InCombat)
                            unit.State = UnitState.Idle;
                    }
                    else
                    {
                        // Check if in heal range
                        Fixed32 dx = cmdTarget.SimPosition.x - unit.SimPosition.x;
                        Fixed32 dz = cmdTarget.SimPosition.z - unit.SimPosition.z;
                        Fixed32 distSq = dx * dx + dz * dz;

                        if (distSq <= healRangeSq)
                        {
                            unit.State = UnitState.InCombat;
                            if (unit.AttackCooldownRemaining <= 0)
                            {
                                cmdTarget.CurrentHealth += healAmount;
                                if (cmdTarget.CurrentHealth > cmdTarget.MaxHealth)
                                    cmdTarget.CurrentHealth = cmdTarget.MaxHealth;
                                unit.AttackCooldownRemaining = unit.AttackCooldownTicks;
                                cmdTarget.LastHealTick = currentTick;
                                cmdTarget.LastHealAmount = healAmount;
                            }
                        }
                        // If moving toward target, let movement system handle it
                    }
                    continue;
                }

                // Auto-heal: skip if busy with player movement commands
                if (unit.State != UnitState.Idle && unit.State != UnitState.InCombat)
                    continue;

                // Find nearest damaged friendly unit within detection range
                int bestId = -1;
                Fixed32 bestDistSq = detectionRangeSq;

                for (int j = 0; j < count; j++)
                {
                    var other = allUnits[j];
                    if (other.State == UnitState.Dead) continue;
                    if (other.Id == unit.Id) continue;
                    if (other.PlayerId != unit.PlayerId) continue;
                    if (other.CurrentHealth >= other.MaxHealth) continue;

                    Fixed32 dx = other.SimPosition.x - unit.SimPosition.x;
                    Fixed32 dz = other.SimPosition.z - unit.SimPosition.z;
                    Fixed32 distSq = dx * dx + dz * dz;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestId = other.Id;
                    }
                }

                if (bestId >= 0)
                {
                    unit.HealTargetId = bestId;
                    var target = registry.GetUnit(bestId);

                    if (bestDistSq <= healRangeSq)
                    {
                        // In heal range — heal
                        unit.State = UnitState.InCombat;
                        if (unit.AttackCooldownRemaining <= 0 && target != null && target.State != UnitState.Dead)
                        {
                            target.CurrentHealth += healAmount;
                            if (target.CurrentHealth > target.MaxHealth)
                                target.CurrentHealth = target.MaxHealth;
                            unit.AttackCooldownRemaining = unit.AttackCooldownTicks;
                            target.LastHealTick = currentTick;
                            target.LastHealAmount = healAmount;
                        }
                    }
                    else if (target != null && mapData != null)
                    {
                        // Out of heal range but within detection — move toward target
                        if (!unit.HasPath)
                        {
                            var path = GridPathfinder.FindPath(mapData,
                                mapData.WorldToTile(unit.SimPosition),
                                mapData.WorldToTile(target.SimPosition), unit.PlayerId, buildingRegistry);
                            if (path != null)
                            {
                                unit.SetPath(path);
                                unit.FinalDestination = target.SimPosition;
                                unit.State = UnitState.Moving;
                            }
                        }
                    }
                }
                else
                {
                    // No one to heal
                    unit.HealTargetId = -1;
                    if (unit.State == UnitState.InCombat)
                        unit.State = UnitState.Idle;
                }
            }

            TickBuildingHealingAuras(allUnits, count, config, playerTeamIds, currentTick, buildingRegistry, onBuildingHealingAuraPulse);
        }

        private void TickKingHealingAura(UnitData king, List<UnitData> allUnits, int unitCount,
            SimulationConfig config, int[] playerTeamIds, int currentTick,
            Action<UnitData, float> onHealingAuraPulse)
        {
            if (king.AttackCooldownRemaining > 0)
                return;

            Fixed32 healRange = Fixed32.FromFloat(config.KingHealRange);
            Fixed32 healRangeSq = healRange * healRange;
            bool healedAny = false;

            for (int i = 0; i < unitCount; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                // Heals through combat, and sustains the King himself.
                if (!TeamHelper.AreAllies(playerTeamIds, unit.PlayerId, king.PlayerId)) continue;
                if (unit.CurrentHealth >= unit.MaxHealth) continue;

                Fixed32 dx = unit.SimPosition.x - king.SimPosition.x;
                Fixed32 dz = unit.SimPosition.z - king.SimPosition.z;
                Fixed32 distSq = dx * dx + dz * dz;
                if (distSq > healRangeSq) continue;

                unit.CurrentHealth += config.KingHealAmount;
                if (unit.CurrentHealth > unit.MaxHealth)
                    unit.CurrentHealth = unit.MaxHealth;
                unit.LastHealTick = currentTick;
                unit.LastHealAmount = config.KingHealAmount;
                healedAny = true;
            }

            if (healedAny)
            {
                king.AttackCooldownRemaining = config.KingHealCooldownTicks;
                onHealingAuraPulse?.Invoke(king, config.KingHealRange);
            }
        }

        private void TickBuildingHealingAuras(List<UnitData> allUnits, int unitCount, SimulationConfig config,
            int[] playerTeamIds, int currentTick, BuildingRegistry buildingRegistry,
            Action<BuildingData, float> onHealingAuraPulse)
        {
            if (buildingRegistry == null) return;

            Fixed32 healRange = Fixed32.FromFloat(config.AbbeyOfKingsHealRange);
            Fixed32 healRangeSq = healRange * healRange;

            var buildings = buildingRegistry.GetAllBuildings();
            for (int b = 0; b < buildings.Count; b++)
            {
                var building = buildings[b];
                if (building.IsDestroyed || building.IsUnderConstruction) continue;
                if (building.Type != BuildingType.Landmark) continue;

                var def = LandmarkDefinitions.Get(building.LandmarkId);
                if (!def.HasHealingAura) continue;

                if (building.AttackCooldownRemaining > 0)
                {
                    building.AttackCooldownRemaining--;
                    continue;
                }

                bool healedAny = false;
                for (int i = 0; i < unitCount; i++)
                {
                    var unit = allUnits[i];
                    if (unit.State == UnitState.Dead) continue;
                    // Heals through combat — this is battlefield sustain, not out-of-combat regen.
                    if (!TeamHelper.AreAllies(playerTeamIds, unit.PlayerId, building.PlayerId)) continue;
                    if (unit.CurrentHealth >= unit.MaxHealth) continue;

                    Fixed32 dx = unit.SimPosition.x - building.SimPosition.x;
                    Fixed32 dz = unit.SimPosition.z - building.SimPosition.z;
                    Fixed32 distSq = dx * dx + dz * dz;
                    if (distSq > healRangeSq) continue;

                    unit.CurrentHealth += config.AbbeyOfKingsHealAmount;
                    if (unit.CurrentHealth > unit.MaxHealth)
                        unit.CurrentHealth = unit.MaxHealth;
                    unit.LastHealTick = currentTick;
                    unit.LastHealAmount = config.AbbeyOfKingsHealAmount;
                    healedAny = true;
                }

                if (healedAny)
                {
                    building.AttackCooldownRemaining = config.AbbeyOfKingsHealCooldownTicks;
                    onHealingAuraPulse?.Invoke(building, config.AbbeyOfKingsHealRange);
                }
            }
        }
    }
}
