using System.Collections.Generic;

namespace OpenEmpires
{
    public class BuildingRegistry
    {
        private Dictionary<int, BuildingData> buildings = new Dictionary<int, BuildingData>();
        private List<BuildingData> orderedBuildings = new List<BuildingData>();
        private int nextBuildingId = 0;

        public BuildingData CreateBuilding(int playerId, BuildingType type, FixedVector3 position,
            int originTileX, int originTileZ, int footprintWidth, int footprintHeight)
        {
            var building = new BuildingData(nextBuildingId++, playerId, type, position,
                originTileX, originTileZ, footprintWidth, footprintHeight);
            buildings[building.Id] = building;
            orderedBuildings.Add(building);
            return building;
        }

        public BuildingData GetBuilding(int id)
        {
            buildings.TryGetValue(id, out var building);
            return building;
        }

        public void RemoveBuilding(int id)
        {
            if (buildings.TryGetValue(id, out var building))
            {
                // Swap-with-last removal: O(1) instead of O(n)
                int idx = orderedBuildings.IndexOf(building);
                if (idx >= 0)
                {
                    int last = orderedBuildings.Count - 1;
                    if (idx != last)
                        orderedBuildings[idx] = orderedBuildings[last];
                    orderedBuildings.RemoveAt(last);
                }
                buildings.Remove(id);
            }
        }

        public List<BuildingData> GetAllBuildings()
        {
            return orderedBuildings;
        }

        public int Count => buildings.Count;
    }
}
