using System.Collections.Generic;

namespace OpenEmpires
{
    public class SpatialGrid
    {
        private readonly Fixed32 cellSize;
        private readonly Fixed32 inverseCellSize;
        private readonly Dictionary<long, List<UnitData>> cells = new Dictionary<long, List<UnitData>>();
        private readonly List<List<UnitData>> activeLists = new List<List<UnitData>>();
        private readonly Stack<List<UnitData>> pool = new Stack<List<UnitData>>();
        private readonly List<UnitData> queryResult = new List<UnitData>();

        public SpatialGrid(Fixed32 cellSize)
        {
            this.cellSize = cellSize;
            this.inverseCellSize = Fixed32.One / cellSize;
        }

        public void Clear()
        {
            for (int i = 0; i < activeLists.Count; i++)
            {
                activeLists[i].Clear();
                pool.Push(activeLists[i]);
            }
            activeLists.Clear();
            cells.Clear();
        }

        public void Insert(UnitData unit)
        {
            long key = CellKey(unit.SimPosition.x, unit.SimPosition.z);
            if (!cells.TryGetValue(key, out var list))
            {
                list = pool.Count > 0 ? pool.Pop() : new List<UnitData>();
                cells[key] = list;
                activeLists.Add(list);
            }
            list.Add(unit);
        }

        public void Build(List<UnitData> units)
        {
            Clear();
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].State != UnitState.Dead)
                    Insert(units[i]);
            }
        }

        public List<UnitData> GetNearby(FixedVector3 pos, Fixed32 radius)
        {
            queryResult.Clear();

            int minCX = CellCoord(pos.x - radius);
            int maxCX = CellCoord(pos.x + radius);
            int minCZ = CellCoord(pos.z - radius);
            int maxCZ = CellCoord(pos.z + radius);

            for (int cx = minCX; cx <= maxCX; cx++)
            {
                for (int cz = minCZ; cz <= maxCZ; cz++)
                {
                    long key = PackKey(cx, cz);
                    if (cells.TryGetValue(key, out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                            queryResult.Add(list[i]);
                    }
                }
            }

            return queryResult;
        }

        private int CellCoord(Fixed32 value)
        {
            // Floor division: shift right to get integer part after multiply
            int raw = (value * inverseCellSize).Raw;
            // Fixed32 has 16 fractional bits, so arithmetic right-shift by 16 gives floor
            return raw >> 16;
        }

        private long CellKey(Fixed32 x, Fixed32 z)
        {
            return PackKey(CellCoord(x), CellCoord(z));
        }

        private static long PackKey(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }
    }
}
