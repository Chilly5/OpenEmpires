using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class UnitHealingSystem
    {
        public void Tick(UnitRegistry registry, SimulationConfig config, SpatialGrid spatialGrid, int[] playerTeamIds, int currentTick, MapData mapData = null, BuildingRegistry buildingRegistry = null)
        {
            var allUnits = registry.GetAllUnits();
            int count = allUnits.Count;
            Fixed32 healRange = Fixed32.FromFloat(config.MonkHealRange);
            Fixed32 healRangeSq = healRange * healRange;
            Fixed32 detectionRange = Fixed32.FromFloat(config.MonkDetectionRange);
            Fixed32 detectionRangeSq = detectionRange * detectionRange;
            int healAmount = config.MonkHealAmount;

            for (int i = 0; i < count; i++)
            {
                var unit = allUnits[i];
                if (unit.State == UnitState.Dead) continue;
                if (!unit.IsHealer) continue;

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
        }
    }
}
