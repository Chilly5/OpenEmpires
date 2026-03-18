using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int( 1,  0), // E
            new Vector2Int(-1,  0), // W
            new Vector2Int( 0,  1), // N
            new Vector2Int( 0, -1), // S
            new Vector2Int( 1,  1), // NE
            new Vector2Int(-1,  1), // NW
            new Vector2Int( 1, -1), // SE
            new Vector2Int(-1, -1), // SW
        };

        private const int CardinalCost = 10;
        private const int DiagonalCost = 14;

        // Pooled working arrays (allocated once via Initialize)
        private static int[] gScore;
        private static int[] fScore;
        private static int[] cameFromIdx;
        private static bool[] closed;

        // Generation stamps to avoid clearing arrays between calls
        private static ushort[] gScoreGen;
        private static ushort[] fScoreGen;
        private static ushort[] cameFromGen;
        private static ushort[] closedGen;
        private static ushort currentGen;

        // Reusable min-heap
        private static int[] heapIndices;
        private static int[] heapPriorities;
        private static int heapCapacity;

        private static int pooledWidth;
        private static int pooledHeight;

        public static void Initialize(int width, int height)
        {
            int size = width * height;
            pooledWidth = width;
            pooledHeight = height;

            gScore = new int[size];
            fScore = new int[size];
            cameFromIdx = new int[size];
            closed = new bool[size];

            gScoreGen = new ushort[size];
            fScoreGen = new ushort[size];
            cameFromGen = new ushort[size];
            closedGen = new ushort[size];

            currentGen = 0;

            heapCapacity = size;
            heapIndices = new int[heapCapacity];
            heapPriorities = new int[heapCapacity];
        }

        private static void AdvanceGeneration()
        {
            currentGen++;
            if (currentGen == 0)
            {
                // Overflow: clear all generation arrays
                System.Array.Clear(gScoreGen, 0, gScoreGen.Length);
                System.Array.Clear(fScoreGen, 0, fScoreGen.Length);
                System.Array.Clear(cameFromGen, 0, cameFromGen.Length);
                System.Array.Clear(closedGen, 0, closedGen.Length);
                currentGen = 1;
            }
        }

        private static int GetGScore(int idx)
        {
            return gScoreGen[idx] == currentGen ? gScore[idx] : int.MaxValue;
        }

        private static void SetGScore(int idx, int val)
        {
            gScore[idx] = val;
            gScoreGen[idx] = currentGen;
        }

        private static int GetFScore(int idx)
        {
            return fScoreGen[idx] == currentGen ? fScore[idx] : int.MaxValue;
        }

        private static void SetFScore(int idx, int val)
        {
            fScore[idx] = val;
            fScoreGen[idx] = currentGen;
        }

        private static int GetCameFrom(int idx)
        {
            return cameFromGen[idx] == currentGen ? cameFromIdx[idx] : -1;
        }

        private static void SetCameFrom(int idx, int val)
        {
            cameFromIdx[idx] = val;
            cameFromGen[idx] = currentGen;
        }

        private static bool IsClosed(int idx)
        {
            return closedGen[idx] == currentGen && closed[idx];
        }

        private static void SetClosed(int idx)
        {
            closed[idx] = true;
            closedGen[idx] = currentGen;
        }

        public static List<Vector2Int> FindPath(MapData map, Vector2Int start, Vector2Int goal, int playerId = -1, BuildingRegistry buildingRegistry = null, int maxExpansions = 16384)
        {
            // Use player-aware walkability check if player ID and building registry are provided
            bool isGoalWalkable = (playerId >= 0 && buildingRegistry != null)
                ? map.IsWalkable(goal.x, goal.y, playerId, buildingRegistry)
                : map.IsWalkable(goal.x, goal.y);

            if (!isGoalWalkable)
            {
                goal = FindLastWalkableOnRay(map, start, goal, playerId, buildingRegistry);
                if (start == goal) return new List<Vector2Int> { goal };
            }

            if (start == goal)
                return new List<Vector2Int> { goal };

            int w = map.Width;
            int h = map.Height;

            // Use pooled arrays if available and matching size, otherwise fall back to allocation
            bool usePool = gScore != null && pooledWidth == w && pooledHeight == h;

            if (usePool)
            {
                AdvanceGeneration();
                return FindPathPooled(map, start, goal, w, h, playerId, buildingRegistry, maxExpansions);
            }
            else
            {
                return FindPathAllocating(map, start, goal, w, h, playerId, buildingRegistry, maxExpansions);
            }
        }

        private static List<Vector2Int> FindPathPooled(MapData map, Vector2Int start, Vector2Int goal, int w, int h, int playerId, BuildingRegistry buildingRegistry, int maxExpansions)
        {
            int startIdx = start.y * w + start.x;
            int goalIdx = goal.y * w + goal.x;

            SetGScore(startIdx, 0);
            SetFScore(startIdx, Heuristic(start, goal));

            // Track the closest-to-goal node we've visited (for partial path fallback)
            int bestIdx = startIdx;
            int bestH = Heuristic(start, goal);

            // Reset heap count
            int heapCount = 0;
            HeapPush(ref heapCount, startIdx, GetFScore(startIdx));

            int expansions = 0;
            while (heapCount > 0)
            {
                if (expansions++ >= maxExpansions)
                {
                    if (bestIdx != startIdx)
                    {
                        var rawPath = ReconstructRawPathPooled(bestIdx, w);
                        var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                        if (smoothed.Count > 1) smoothed.RemoveAt(0);
                        return smoothed;
                    }
                    return new List<Vector2Int>();
                }

                int currentIdx = HeapPop(ref heapCount);

                if (currentIdx == goalIdx)
                {
                    var rawPath = ReconstructRawPathPooled(currentIdx, w);
                    var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                    // Remove start tile — path should only contain waypoints to move toward
                    if (smoothed.Count > 1)
                        smoothed.RemoveAt(0);
                    return smoothed;
                }

                if (IsClosed(currentIdx))
                    continue;
                SetClosed(currentIdx);

                int cx = currentIdx % w;
                int cy = currentIdx / w;

                // Update best node if this one is closer to goal
                int hCurrent = Heuristic(new Vector2Int(cx, cy), goal);
                if (hCurrent < bestH) { bestH = hCurrent; bestIdx = currentIdx; }

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Directions[d].x;
                    int ny = cy + Directions[d].y;

                    // Use player-aware walkability check if available
                    bool isWalkable = (playerId >= 0 && buildingRegistry != null)
                        ? map.IsWalkable(nx, ny, playerId, buildingRegistry)
                        : map.IsWalkable(nx, ny);

                    if (!isWalkable)
                        continue;

                    bool isDiagonal = d >= 4;
                    if (isDiagonal)
                    {
                        bool adjacent1Walkable = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(cx + Directions[d].x, cy, playerId, buildingRegistry)
                            : map.IsWalkable(cx + Directions[d].x, cy);
                        bool adjacent2Walkable = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(cx, cy + Directions[d].y, playerId, buildingRegistry)
                            : map.IsWalkable(cx, cy + Directions[d].y);

                        if (!adjacent1Walkable || !adjacent2Walkable)
                            continue;
                    }

                    int nIdx = ny * w + nx;
                    if (IsClosed(nIdx))
                        continue;

                    int moveCost = isDiagonal ? DiagonalCost : CardinalCost;
                    int tentativeG = GetGScore(currentIdx) + moveCost;

                    if (tentativeG < GetGScore(nIdx))
                    {
                        SetCameFrom(nIdx, currentIdx);
                        SetGScore(nIdx, tentativeG);
                        SetFScore(nIdx, tentativeG + Heuristic(new Vector2Int(nx, ny), goal));
                        HeapPush(ref heapCount, nIdx, GetFScore(nIdx));
                    }
                }
            }

            // Open set exhausted without reaching goal — return partial path to closest node
            if (bestIdx != startIdx)
            {
                var rawPath = ReconstructRawPathPooled(bestIdx, w);
                var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                if (smoothed.Count > 1) smoothed.RemoveAt(0);
                return smoothed;
            }
            return new List<Vector2Int>();
        }

        private static List<Vector2Int> ReconstructRawPathPooled(int currentIdx, int width)
        {
            var path = new List<Vector2Int>();
            while (currentIdx != -1)
            {
                int x = currentIdx % width;
                int y = currentIdx / width;
                path.Add(new Vector2Int(x, y));
                currentIdx = GetCameFrom(currentIdx);
            }
            path.Reverse();
            return path;
        }

        // Reusable heap operations using static arrays
        private static void HeapPush(ref int count, int index, int priority)
        {
            if (count == heapCapacity)
            {
                int newCap = heapCapacity * 2;
                var newIdx = new int[newCap];
                var newPri = new int[newCap];
                System.Array.Copy(heapIndices, newIdx, count);
                System.Array.Copy(heapPriorities, newPri, count);
                heapIndices = newIdx;
                heapPriorities = newPri;
                heapCapacity = newCap;
            }

            heapIndices[count] = index;
            heapPriorities[count] = priority;
            // Bubble up
            int i = count;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapPriorities[i] < heapPriorities[parent])
                {
                    int tmpIdx = heapIndices[i]; heapIndices[i] = heapIndices[parent]; heapIndices[parent] = tmpIdx;
                    int tmpPri = heapPriorities[i]; heapPriorities[i] = heapPriorities[parent]; heapPriorities[parent] = tmpPri;
                    i = parent;
                }
                else break;
            }
            count++;
        }

        private static int HeapPop(ref int count)
        {
            int result = heapIndices[0];
            count--;
            heapIndices[0] = heapIndices[count];
            heapPriorities[0] = heapPriorities[count];
            // Bubble down
            if (count > 0)
            {
                int i = 0;
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;
                    int smallest = i;
                    if (left < count && heapPriorities[left] < heapPriorities[smallest])
                        smallest = left;
                    if (right < count && heapPriorities[right] < heapPriorities[smallest])
                        smallest = right;
                    if (smallest != i)
                    {
                        int tmpIdx = heapIndices[i]; heapIndices[i] = heapIndices[smallest]; heapIndices[smallest] = tmpIdx;
                        int tmpPri = heapPriorities[i]; heapPriorities[i] = heapPriorities[smallest]; heapPriorities[smallest] = tmpPri;
                        i = smallest;
                    }
                    else break;
                }
            }
            return result;
        }

        // Fallback for when pool isn't initialized or size doesn't match
        private static List<Vector2Int> FindPathAllocating(MapData map, Vector2Int start, Vector2Int goal, int w, int h, int playerId, BuildingRegistry buildingRegistry, int maxExpansions)
        {
            int size = w * h;

            int[] localGScore = new int[size];
            int[] localFScore = new int[size];
            int[] localCameFrom = new int[size];
            bool[] localClosed = new bool[size];

            for (int i = 0; i < size; i++)
            {
                localGScore[i] = int.MaxValue;
                localFScore[i] = int.MaxValue;
                localCameFrom[i] = -1;
            }

            int startIdx = start.y * w + start.x;
            int goalIdx = goal.y * w + goal.x;

            localGScore[startIdx] = 0;
            localFScore[startIdx] = Heuristic(start, goal);

            // Track the closest-to-goal node we've visited (for partial path fallback)
            int bestIdx = startIdx;
            int bestH = Heuristic(start, goal);

            var open = new MinHeap(size);
            open.Push(startIdx, localFScore[startIdx]);

            int expansions = 0;
            while (open.Count > 0)
            {
                if (expansions++ >= maxExpansions)
                {
                    if (bestIdx != startIdx)
                    {
                        var rawPath = ReconstructRawPath(localCameFrom, bestIdx, w);
                        var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                        if (smoothed.Count > 1) smoothed.RemoveAt(0);
                        return smoothed;
                    }
                    return new List<Vector2Int>();
                }

                int currentIdx = open.Pop();

                if (currentIdx == goalIdx)
                {
                    var rawPath = ReconstructRawPath(localCameFrom, currentIdx, w);
                    var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                    if (smoothed.Count > 1)
                        smoothed.RemoveAt(0);
                    return smoothed;
                }

                if (localClosed[currentIdx])
                    continue;
                localClosed[currentIdx] = true;

                int cx = currentIdx % w;
                int cy = currentIdx / w;

                // Update best node if this one is closer to goal
                int hCurrent = Heuristic(new Vector2Int(cx, cy), goal);
                if (hCurrent < bestH) { bestH = hCurrent; bestIdx = currentIdx; }

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Directions[d].x;
                    int ny = cy + Directions[d].y;

                    bool isWalkable = (playerId >= 0 && buildingRegistry != null)
                        ? map.IsWalkable(nx, ny, playerId, buildingRegistry)
                        : map.IsWalkable(nx, ny);

                    if (!isWalkable)
                        continue;

                    bool isDiagonal = d >= 4;
                    if (isDiagonal)
                    {
                        bool adjacent1Walkable = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(cx + Directions[d].x, cy, playerId, buildingRegistry)
                            : map.IsWalkable(cx + Directions[d].x, cy);
                        bool adjacent2Walkable = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(cx, cy + Directions[d].y, playerId, buildingRegistry)
                            : map.IsWalkable(cx, cy + Directions[d].y);

                        if (!adjacent1Walkable || !adjacent2Walkable)
                            continue;
                    }

                    int nIdx = ny * w + nx;
                    if (localClosed[nIdx])
                        continue;

                    int moveCost = isDiagonal ? DiagonalCost : CardinalCost;
                    int tentativeG = localGScore[currentIdx] + moveCost;

                    if (tentativeG < localGScore[nIdx])
                    {
                        localCameFrom[nIdx] = currentIdx;
                        localGScore[nIdx] = tentativeG;
                        localFScore[nIdx] = tentativeG + Heuristic(new Vector2Int(nx, ny), goal);
                        open.Push(nIdx, localFScore[nIdx]);
                    }
                }
            }

            // Open set exhausted without reaching goal — return partial path to closest node
            if (bestIdx != startIdx)
            {
                var rawPath = ReconstructRawPath(localCameFrom, bestIdx, w);
                var smoothed = SmoothPath(map, rawPath, playerId, buildingRegistry);
                if (smoothed.Count > 1) smoothed.RemoveAt(0);
                return smoothed;
            }
            return new List<Vector2Int>();
        }

        private static int Heuristic(Vector2Int a, Vector2Int b)
        {
            // Octile distance with integer costs
            int dx = Mathf.Abs(a.x - b.x);
            int dy = Mathf.Abs(a.y - b.y);
            return CardinalCost * (dx + dy) + (DiagonalCost - 2 * CardinalCost) * Mathf.Min(dx, dy);
        }

        private static List<Vector2Int> ReconstructRawPath(int[] cameFrom, int currentIdx, int width)
        {
            var path = new List<Vector2Int>();
            while (currentIdx != -1)
            {
                int x = currentIdx % width;
                int y = currentIdx / width;
                path.Add(new Vector2Int(x, y));
                currentIdx = cameFrom[currentIdx];
            }
            path.Reverse();
            return path;
        }

        private static List<Vector2Int> SmoothPath(MapData map, List<Vector2Int> path, int playerId = -1, BuildingRegistry buildingRegistry = null)
        {
            if (path.Count <= 2) return path;

            var smoothed = new List<Vector2Int>();
            smoothed.Add(path[0]);

            int anchor = 0;
            while (anchor < path.Count - 1)
            {
                int farthest = anchor + 1;
                for (int i = anchor + 2; i < path.Count; i++)
                {
                    if (HasLineOfSight(map, path[anchor], path[i], playerId, buildingRegistry))
                        farthest = i;
                }
                smoothed.Add(path[farthest]);
                anchor = farthest;
            }

            return smoothed;
        }

        public static Vector2Int FindLastWalkableOnRay(MapData map, Vector2Int from, Vector2Int to)
        {
            return FindLastWalkableOnRay(map, from, to, -1, null);
        }

        public static Vector2Int FindLastWalkableOnRay(MapData map, Vector2Int from, Vector2Int to,
            int playerId, BuildingRegistry buildingRegistry)
        {
            int x0 = from.x, y0 = from.y;
            int x1 = to.x, y1 = to.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            Vector2Int lastWalkable = from;

            while (!(x0 == x1 && y0 == y1))
            {
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }

                bool walkable = (playerId >= 0 && buildingRegistry != null)
                    ? map.IsWalkable(x0, y0, playerId, buildingRegistry)
                    : map.IsWalkable(x0, y0);

                if (!walkable) break;
                lastWalkable = new Vector2Int(x0, y0);
            }

            return lastWalkable;
        }

        private static bool HasLineOfSight(MapData map, Vector2Int from, Vector2Int to, int playerId = -1, BuildingRegistry buildingRegistry = null)
        {
            int x0 = from.x, y0 = from.y;
            int x1 = to.x, y1 = to.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                bool isWalkable = (playerId >= 0 && buildingRegistry != null)
                    ? map.IsWalkable(x0, y0, playerId, buildingRegistry)
                    : map.IsWalkable(x0, y0);

                if (!isWalkable)
                    return false;

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;
                bool stepX = e2 > -dy;
                bool stepY = e2 < dx;

                // Diagonal step — enforce no corner cutting (consistent with A*)
                if (stepX && stepY)
                {
                    bool adjacent1Walkable = (playerId >= 0 && buildingRegistry != null)
                        ? map.IsWalkable(x0 + sx, y0, playerId, buildingRegistry)
                        : map.IsWalkable(x0 + sx, y0);
                    bool adjacent2Walkable = (playerId >= 0 && buildingRegistry != null)
                        ? map.IsWalkable(x0, y0 + sy, playerId, buildingRegistry)
                        : map.IsWalkable(x0, y0 + sy);

                    if (!adjacent1Walkable || !adjacent2Walkable)
                        return false;
                }

                if (stepX) { err -= dy; x0 += sx; }
                if (stepY) { err += dx; y0 += sy; }
            }

            return true;
        }

        /// <summary>
        /// BFS flood fill from anchor tile, 8-directional with diagonal corner-cutting, bounded by Chebyshev distance maxRadius.
        /// Returns HashSet of reachable tile indices (packed as z * width + x).
        /// </summary>
        public static HashSet<int> FloodFillWalkable(MapData map, Vector2Int anchor, int maxRadius,
            int playerId = -1, BuildingRegistry buildingRegistry = null)
        {
            var reachable = new HashSet<int>();
            int w = map.Width;

            bool anchorWalkable = (playerId >= 0 && buildingRegistry != null)
                ? map.IsWalkable(anchor.x, anchor.y, playerId, buildingRegistry)
                : map.IsWalkable(anchor.x, anchor.y);

            if (!anchorWalkable) return reachable;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(anchor);
            reachable.Add(anchor.y * w + anchor.x);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                for (int d = 0; d < 8; d++)
                {
                    int nx = current.x + Directions[d].x;
                    int ny = current.y + Directions[d].y;

                    // Chebyshev distance bound
                    if (Mathf.Abs(nx - anchor.x) > maxRadius || Mathf.Abs(ny - anchor.y) > maxRadius)
                        continue;

                    int key = ny * w + nx;
                    if (reachable.Contains(key))
                        continue;

                    bool walkable = (playerId >= 0 && buildingRegistry != null)
                        ? map.IsWalkable(nx, ny, playerId, buildingRegistry)
                        : map.IsWalkable(nx, ny);

                    if (!walkable) continue;

                    // Diagonal corner-cutting check (same as FindPathPooled)
                    if (d >= 4)
                    {
                        bool adj1 = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(current.x + Directions[d].x, current.y, playerId, buildingRegistry)
                            : map.IsWalkable(current.x + Directions[d].x, current.y);
                        bool adj2 = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(current.x, current.y + Directions[d].y, playerId, buildingRegistry)
                            : map.IsWalkable(current.x, current.y + Directions[d].y);
                        if (!adj1 || !adj2) continue;
                    }

                    reachable.Add(key);
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return reachable;
        }

        /// <summary>
        /// Finds the nearest tile in the given set, spiraling outward from the starting tile.
        /// </summary>
        public static Vector2Int FindNearestTileInSet(Vector2Int tile, HashSet<int> tileSet, int mapWidth, int maxRadius)
        {
            int key = tile.y * mapWidth + tile.x;
            if (tileSet.Contains(key)) return tile;

            for (int r = 1; r <= maxRadius; r++)
            {
                int bestDist2 = int.MaxValue;
                Vector2Int best = new Vector2Int(-1, -1);

                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                            continue;

                        int tx = tile.x + dx;
                        int tz = tile.y + dz;
                        int candidateKey = tz * mapWidth + tx;

                        if (tileSet.Contains(candidateKey))
                        {
                            int dist2 = dx * dx + dz * dz;
                            if (dist2 < bestDist2)
                            {
                                bestDist2 = dist2;
                                best = new Vector2Int(tx, tz);
                            }
                        }
                    }
                }

                if (best.x >= 0)
                    return best;
            }

            return new Vector2Int(-1, -1);
        }

        /// <summary>
        /// Walks from 'from' toward 'toward' along a Bresenham line, returning the first tile
        /// in the reachable set with clearance past the obstacle boundary.
        /// Falls back to FindNearestTileInSet if the walk doesn't intersect the set.
        /// </summary>
        public static Vector2Int FindConnectedTileToward(Vector2Int from, Vector2Int toward,
            HashSet<int> reachableSet, int mapWidth, int maxRadius)
        {
            int key = from.y * mapWidth + from.x;
            if (reachableSet.Contains(key)) return from;

            int x0 = from.x, y0 = from.y;
            int x1 = toward.x, y1 = toward.y;
            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            Vector2Int best = new Vector2Int(-1, -1);
            int clearanceSteps = 0;
            const int clearance = 0;
            int maxSteps = dx + dy;

            for (int step = 0; step < maxSteps; step++)
            {
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }

                key = y0 * mapWidth + x0;
                if (reachableSet.Contains(key))
                {
                    best = new Vector2Int(x0, y0);
                    clearanceSteps++;
                    if (clearanceSteps > clearance) return best;
                }
                else if (best.x >= 0)
                {
                    return best; // Left connected region, return last good tile
                }

                if (x0 == x1 && y0 == y1) break;
            }

            if (best.x >= 0) return best;

            // Walk didn't intersect connected region — fall back to nearest tile in set
            return FindNearestTileInSet(from, reachableSet, mapWidth, maxRadius);
        }

        /// <summary>
        /// Finds the nearest walkable tile geometrically (no reachability check).
        /// Used to re-snap formation positions that land on impassable tiles.
        /// </summary>
        public static Vector2Int FindNearestWalkableTile(MapData map, Vector2Int tile,
            int maxRadius = 10, int playerId = -1, BuildingRegistry buildingRegistry = null)
        {
            bool walkable = (playerId >= 0 && buildingRegistry != null)
                ? map.IsWalkable(tile.x, tile.y, playerId, buildingRegistry)
                : map.IsWalkable(tile.x, tile.y);

            if (walkable) return tile;

            for (int r = 1; r <= maxRadius; r++)
            {
                int bestDist2 = int.MaxValue;
                Vector2Int best = new Vector2Int(-1, -1);

                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                            continue;

                        int tx = tile.x + dx;
                        int tz = tile.y + dz;

                        bool w = (playerId >= 0 && buildingRegistry != null)
                            ? map.IsWalkable(tx, tz, playerId, buildingRegistry)
                            : map.IsWalkable(tx, tz);

                        if (w)
                        {
                            int dist2 = dx * dx + dz * dz;
                            if (dist2 < bestDist2)
                            {
                                bestDist2 = dist2;
                                best = new Vector2Int(tx, tz);
                            }
                        }
                    }
                }

                if (best.x >= 0)
                    return best;
            }

            return new Vector2Int(-1, -1); // failure sentinel
        }

        private struct MinHeap
        {
            private int[] indices;
            private int[] priorities;
            private int count;

            public int Count => count;

            public MinHeap(int capacity)
            {
                indices = new int[capacity];
                priorities = new int[capacity];
                count = 0;
            }

            public void Push(int index, int priority)
            {
                if (count == indices.Length)
                {
                    // Grow arrays
                    int newCap = indices.Length * 2;
                    var newIdx = new int[newCap];
                    var newPri = new int[newCap];
                    System.Array.Copy(indices, newIdx, count);
                    System.Array.Copy(priorities, newPri, count);
                    indices = newIdx;
                    priorities = newPri;
                }

                indices[count] = index;
                priorities[count] = priority;
                BubbleUp(count);
                count++;
            }

            public int Pop()
            {
                int result = indices[0];
                count--;
                indices[0] = indices[count];
                priorities[0] = priorities[count];
                if (count > 0)
                    BubbleDown(0);
                return result;
            }

            private void BubbleUp(int i)
            {
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (priorities[i] < priorities[parent])
                    {
                        Swap(i, parent);
                        i = parent;
                    }
                    else break;
                }
            }

            private void BubbleDown(int i)
            {
                while (true)
                {
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;
                    int smallest = i;

                    if (left < count && priorities[left] < priorities[smallest])
                        smallest = left;
                    if (right < count && priorities[right] < priorities[smallest])
                        smallest = right;

                    if (smallest != i)
                    {
                        Swap(i, smallest);
                        i = smallest;
                    }
                    else break;
                }
            }

            private void Swap(int a, int b)
            {
                int tmpIdx = indices[a];
                indices[a] = indices[b];
                indices[b] = tmpIdx;

                int tmpPri = priorities[a];
                priorities[a] = priorities[b];
                priorities[b] = tmpPri;
            }
        }
    }
}
