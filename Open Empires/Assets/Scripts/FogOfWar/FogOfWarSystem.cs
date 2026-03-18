using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class FogOfWarSystem
    {
        private readonly Dictionary<int, List<(int dx, int dz)>> radiusOffsets = new Dictionary<int, List<(int dx, int dz)>>();

        // Cached ally lists — only rebuilt when team config changes
        private int[][] cachedAllyLists;
        private int[] cachedTeamIds;
        private int cachedPlayerCount;


        private void EnsureAllyLists(int playerCount, int[] playerTeamIds)
        {
            if (playerTeamIds == null)
            {
                cachedAllyLists = null;
                cachedTeamIds = null;
                return;
            }

            // Check if teams changed
            if (cachedAllyLists != null && cachedPlayerCount == playerCount && cachedTeamIds != null)
            {
                bool same = true;
                for (int i = 0; i < playerCount && i < cachedTeamIds.Length && i < playerTeamIds.Length; i++)
                {
                    if (cachedTeamIds[i] != playerTeamIds[i]) { same = false; break; }
                }
                if (same) return;
            }

            cachedPlayerCount = playerCount;
            cachedTeamIds = new int[playerCount];
            System.Array.Copy(playerTeamIds, cachedTeamIds, playerCount);
            cachedAllyLists = new int[playerCount][];
            var allies = new List<int>();
            for (int p = 0; p < playerCount; p++)
            {
                allies.Clear();
                for (int q = 0; q < playerCount; q++)
                    if (p != q && playerTeamIds[p] == playerTeamIds[q])
                        allies.Add(q);
                cachedAllyLists[p] = allies.Count > 0 ? allies.ToArray() : null;
            }
        }

        public void Tick(FogOfWarData fogData, UnitRegistry unitRegistry, BuildingRegistry buildingRegistry, MapData mapData, int playerCount, int[] playerTeamIds = null, SimulationConfig config = null)
        {
            for (int p = 0; p < playerCount; p++)
                fogData.DemoteAllVisible(p);

            // Build per-player ally lists for shared vision (cached, only rebuilt on change)
            EnsureAllyLists(playerCount, playerTeamIds);
            int[][] allyLists = cachedAllyLists;

            var units = unitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State == UnitState.Dead) continue;
                if (unit.IsDummy) continue;
                if (unit.PlayerId < 0) continue;

                float det = unit.DetectionRange.ToFloat();
                int radius = det > 15f ? 24 : det > 7f ? 16 : 6;
                var offsets = GetOffsets(radius);

                Vector2Int tile = mapData.WorldToTile(unit.SimPosition);

                for (int j = 0; j < offsets.Count; j++)
                {
                    int tx = tile.x + offsets[j].dx;
                    int tz = tile.y + offsets[j].dz;
                    fogData.SetVisible(unit.PlayerId, tx, tz);

                    if (allyLists != null && unit.PlayerId < allyLists.Length && allyLists[unit.PlayerId] != null)
                        for (int a = 0; a < allyLists[unit.PlayerId].Length; a++)
                            fogData.SetVisible(allyLists[unit.PlayerId][a], tx, tz);
                }
            }

            // Buildings reveal fog around their position (only completed buildings)
            var buildings = buildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building.IsUnderConstruction) continue;
                
                // Towers have much larger vision radius
                int radius;
                if (building.Type == BuildingType.Tower && config != null)
                {
                    float visionRadius = config.TowerVisionRadius;
                    if (building.HasVisionUpgrade)
                        visionRadius += config.VisionUpgradeRangeBonus;
                    radius = Mathf.RoundToInt(visionRadius);
                }
                else
                    radius = 20;
                    
                var offsets = GetOffsets(radius);

                Vector2Int tile = mapData.WorldToTile(building.SimPosition);

                for (int j = 0; j < offsets.Count; j++)
                {
                    int tx = tile.x + offsets[j].dx;
                    int tz = tile.y + offsets[j].dz;
                    fogData.SetVisible(building.PlayerId, tx, tz);

                    if (allyLists != null && building.PlayerId < allyLists.Length && allyLists[building.PlayerId] != null)
                        for (int a = 0; a < allyLists[building.PlayerId].Length; a++)
                            fogData.SetVisible(allyLists[building.PlayerId][a], tx, tz);
                }
            }
        }

        private List<(int dx, int dz)> GetOffsets(int radius)
        {
            if (radiusOffsets.TryGetValue(radius, out var cached))
                return cached;

            var offsets = new List<(int dx, int dz)>();
            int r2 = radius * radius;
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dz * dz <= r2)
                        offsets.Add((dx, dz));
                }
            }

            radiusOffsets[radius] = offsets;
            return offsets;
        }
    }
}
