using System.Collections.Generic;

namespace OpenEmpires
{
    public class UnitRegistry
    {
        private Dictionary<int, UnitData> units = new Dictionary<int, UnitData>();
        private List<UnitData> orderedUnits = new List<UnitData>();
        private Dictionary<int, UnitData> garrisonedUnits = new Dictionary<int, UnitData>();
        private int nextUnitId = 0;

        public UnitData CreateUnit(int playerId, FixedVector3 position, Fixed32 moveSpeed, Fixed32 radius, Fixed32 mass)
        {
            var unit = new UnitData(nextUnitId++, playerId, position, moveSpeed, radius, mass);
            units[unit.Id] = unit;
            orderedUnits.Add(unit);
            return unit;
        }

        public UnitData GetUnit(int id)
        {
            units.TryGetValue(id, out var unit);
            return unit;
        }

        public void RemoveUnit(int id)
        {
            if (units.TryGetValue(id, out var unit))
            {
                orderedUnits.Remove(unit);
                units.Remove(id);
            }
        }

        /// <summary>Move a unit from active to garrisoned storage.</summary>
        public void GarrisonUnit(int id)
        {
            if (units.TryGetValue(id, out var unit))
            {
                orderedUnits.Remove(unit);
                units.Remove(id);
                garrisonedUnits[id] = unit;
            }
        }

        /// <summary>Restore a garrisoned unit back to active.</summary>
        public UnitData RestoreUnit(int id)
        {
            if (garrisonedUnits.TryGetValue(id, out var unit))
            {
                garrisonedUnits.Remove(id);
                units[id] = unit;
                orderedUnits.Add(unit);
                return unit;
            }
            return null;
        }

        /// <summary>Get a garrisoned unit (not in active list).</summary>
        public UnitData GetGarrisonedUnit(int id)
        {
            garrisonedUnits.TryGetValue(id, out var unit);
            return unit;
        }

        public List<UnitData> GetAllUnits()
        {
            return orderedUnits;
        }

        public int Count => units.Count;

        public int GetPopulation(int playerId)
        {
            int count = 0;
            for (int i = 0; i < orderedUnits.Count; i++)
                if (orderedUnits[i].PlayerId == playerId && orderedUnits[i].CurrentHealth > 0 && !orderedUnits[i].IsSheep)
                    count++;
            foreach (var kv in garrisonedUnits)
                if (kv.Value.PlayerId == playerId && !kv.Value.IsSheep)
                    count++;
            return count;
        }
    }
}
