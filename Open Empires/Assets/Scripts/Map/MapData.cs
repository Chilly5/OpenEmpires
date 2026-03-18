using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public enum TileType
    {
        Grass,
        Water,
        Building,
        Sand,
        Rock,
        Cliff,
        River,
        Foundation,
        BuildingTemplate, // placed but not yet under active construction; walkable by units
        Farm // completed farm; walkable so villagers can stand on it
    }

    public class MapData
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
        public int Width { get; private set; }
        public int Height { get; private set; }
        public TileType[,] Tiles { get; private set; }
        public float[,] Heights { get; private set; }
        public const float ForestWalkableThreshold = 0.68f;
        public float[,] ForestDensity { get; private set; }
        public int[,] FoundationCount { get; private set; }
        private bool[,] holeMap;
        public Vector2Int[] BasePositions { get; set; }

        // Circular map boundary — tiles outside are impassable and permanently fogged
        private float circleCenterX;
        private float circleCenterZ;
        private float circleRadiusSq;

        private Dictionary<int, ResourceNodeData> resourceNodes = new Dictionary<int, ResourceNodeData>();
        private List<ResourceNodeData> orderedResourceNodes = new List<ResourceNodeData>();
        private int nextResourceId = 0;

        public MapData(int width, int height)
        {
            Width = width;
            Height = height;
            Tiles = new TileType[width, height];
            Heights = new float[width, height];
            ForestDensity = new float[width, height];
            FoundationCount = new int[width, height];

            // Circular boundary: inscribe circle in the map with padding
            circleCenterX = width / 2f;
            circleCenterZ = height / 2f;
            float r = Mathf.Min(width, height) / 2f - 10f;
            circleRadiusSq = r * r;

            // Default all tiles to grass, heights to zero
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    Tiles[x, z] = TileType.Grass;
        }

        /// <summary>
        /// Returns the fixed-point world position for the center of a tile.
        /// </summary>
        public FixedVector3 TileToWorldFixed(int x, int z)
        {
            return new FixedVector3(
                Fixed32.FromInt(x) + Fixed32.Half,
                Fixed32.Zero,
                Fixed32.FromInt(z) + Fixed32.Half);
        }

        /// <summary>
        /// Float version for render-side callers (MapRenderer, etc).
        /// </summary>
        public Vector3 TileToWorld(int x, int z)
        {
            return new Vector3(x + 0.5f, 0f, z + 0.5f);
        }

        /// <summary>
        /// Converts a fixed-point world position to tile coordinates (floor).
        /// </summary>
        public Vector2Int WorldToTile(FixedVector3 worldPos)
        {
            return new Vector2Int(FloorFixed(worldPos.x), FloorFixed(worldPos.z));
        }

        /// <summary>
        /// Float version for render-side callers.
        /// </summary>
        public Vector2Int WorldToTile(Vector3 worldPos)
        {
            return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z));
        }

        /// <summary>
        /// Floor a Fixed32 value to int. Correct for negative values.
        /// </summary>
        private static int FloorFixed(Fixed32 value)
        {
            int raw = value.Raw;
            if (raw >= 0)
                return raw >> Fixed32.FractionalBits;
            // For negatives: shift right rounds toward zero, we need floor (toward -inf)
            // Check if there are any fractional bits set
            return (raw >> Fixed32.FractionalBits) - (((raw & (Fixed32.Scale - 1)) != 0) ? 1 : 0);
        }

        public bool IsInBounds(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        /// <summary>
        /// Returns true if the tile is outside the circular playable area.
        /// Tiles outside are impassable and permanently hidden by fog of war.
        /// </summary>
        public bool IsOutsideCircle(int x, int z)
        {
            float dx = x + 0.5f - circleCenterX;
            float dz = z + 0.5f - circleCenterZ;
            return dx * dx + dz * dz > circleRadiusSq;
        }

        public bool IsWalkable(int x, int z)
        {
            if (!IsInBounds(x, z)) return false;
            if (IsOutsideCircle(x, z)) return false;
            var t = Tiles[x, z];
            if (!(t == TileType.Grass || t == TileType.Sand || t == TileType.Foundation
                  || t == TileType.BuildingTemplate || t == TileType.Farm))
                return false;
            if (ForestDensity[x, z] >= ForestWalkableThreshold) return false;
            if (holeMap != null && holeMap[x, z]) return false;
            return true;
        }

        public bool IsBuildable(int x, int z)
        {
            if (!IsInBounds(x, z)) return false;
            if (IsOutsideCircle(x, z)) return false;
            var t = Tiles[x, z];
            return t == TileType.Grass || t == TileType.Sand;
        }

        public bool IsWalkable(int x, int z, int playerId, BuildingRegistry buildingRegistry)
        {
            if (!IsInBounds(x, z)) return false;
            if (IsOutsideCircle(x, z)) return false;
            var t = Tiles[x, z];

            if (t == TileType.Grass || t == TileType.Sand || t == TileType.Foundation
                || t == TileType.BuildingTemplate || t == TileType.Farm)
            {
                if (ForestDensity[x, z] >= ForestWalkableThreshold) return false;
                if (holeMap != null && holeMap[x, z]) return false;
                return true;
            }

            if (t == TileType.Building)
            {
                var building = GetBuildingAt(x, z, buildingRegistry);
                if (building != null && building.IsGate)
                    return building.PlayerId == playerId;
            }

            return false;
        }

        public BuildingData GetBuildingAt(int x, int z, BuildingRegistry buildingRegistry)
        {
            if (!IsInBounds(x, z)) return null;

            foreach (var building in buildingRegistry.GetAllBuildings())
            {
                if (building.IsDestroyed) continue;

                int minX = building.OriginTileX;
                int minZ = building.OriginTileZ;
                int maxX = minX + building.TileFootprintWidth;
                int maxZ = minZ + building.TileFootprintHeight;

                if (x >= minX && x < maxX && z >= minZ && z < maxZ)
                    return building;
            }

            return null;
        }

        public void ApplyGenerationResult(TileType[,] tiles, float[,] heights, float[,] forestDensity)
        {
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                {
                    Tiles[x, z] = tiles[x, z];
                    Heights[x, z] = heights[x, z];
                    ForestDensity[x, z] = forestDensity[x, z];
                }
        }

        public float SampleForestDensity(float worldX, float worldZ)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(worldX), 0, Width - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(worldZ), 0, Height - 1);
            return ForestDensity[x, z];
        }

        /// <summary>
        /// Bilinear interpolation of height at a world position.
        /// </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            // World position maps directly to tile coords (tile 0 spans 0..1)
            float fx = worldX - 0.5f;
            float fz = worldZ - 0.5f;

            int x0 = Mathf.FloorToInt(fx);
            int z0 = Mathf.FloorToInt(fz);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = fx - x0;
            float tz = fz - z0;

            float h00 = GetHeightClamped(x0, z0);
            float h10 = GetHeightClamped(x1, z0);
            float h01 = GetHeightClamped(x0, z1);
            float h11 = GetHeightClamped(x1, z1);

            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);

            return Mathf.Lerp(h0, h1, tz);
        }

        public float GetHeightClamped(int x, int z)
        {
            x = Mathf.Clamp(x, 0, Width - 1);
            z = Mathf.Clamp(z, 0, Height - 1);
            return Heights[x, z];
        }

        public void ClearAreaForBase(int centerX, int centerZ, int radius)
        {
            MapGenerator.ClearAreaForBase(Tiles, Heights, centerX, centerZ, radius, Width, Height);
        }

        public void MarkBuildingTiles(int originX, int originZ, int width, int height)
        {
            for (int x = originX; x < originX + width; x++)
                for (int z = originZ; z < originZ + height; z++)
                    if (IsInBounds(x, z))
                        Tiles[x, z] = TileType.Building;
        }

        public void MarkTemplateTiles(int originX, int originZ, int width, int height)
        {
            for (int x = originX; x < originX + width; x++)
                for (int z = originZ; z < originZ + height; z++)
                    if (IsInBounds(x, z))
                        Tiles[x, z] = TileType.BuildingTemplate;
        }

        public void MarkFarmTiles(int originX, int originZ, int width, int height)
        {
            for (int x = originX; x < originX + width; x++)
                for (int z = originZ; z < originZ + height; z++)
                    if (IsInBounds(x, z))
                        Tiles[x, z] = TileType.Farm;
        }

        public void ClearBuildingTiles(int originX, int originZ, int width, int height)
        {
            for (int x = originX; x < originX + width; x++)
                for (int z = originZ; z < originZ + height; z++)
                    if (IsInBounds(x, z))
                        Tiles[x, z] = FoundationCount[x, z] > 0 ? TileType.Foundation : TileType.Grass;
        }

        public void MarkFoundationBorder(int originX, int originZ, int width, int height, int border)
        {
            if (border <= 0) return;
            for (int x = originX - border; x < originX + width + border; x++)
            {
                for (int z = originZ - border; z < originZ + height + border; z++)
                {
                    // Skip tiles inside the footprint itself
                    if (x >= originX && x < originX + width && z >= originZ && z < originZ + height)
                        continue;
                    if (!IsInBounds(x, z)) continue;
                    var t = Tiles[x, z];
                    if (t != TileType.Grass && t != TileType.Sand && t != TileType.Foundation)
                        continue;
                    FoundationCount[x, z]++;
                    Tiles[x, z] = TileType.Foundation;
                }
            }
        }

        public void ClearFoundationBorder(int originX, int originZ, int width, int height, int border)
        {
            if (border <= 0) return;
            for (int x = originX - border; x < originX + width + border; x++)
            {
                for (int z = originZ - border; z < originZ + height + border; z++)
                {
                    if (x >= originX && x < originX + width && z >= originZ && z < originZ + height)
                        continue;
                    if (!IsInBounds(x, z)) continue;
                    FoundationCount[x, z]--;
                    if (FoundationCount[x, z] <= 0 && Tiles[x, z] == TileType.Foundation)
                    {
                        FoundationCount[x, z] = 0;
                        Tiles[x, z] = TileType.Grass;
                    }
                }
            }
        }

        public ResourceNodeData AddResourceNode(ResourceType type, FixedVector3 position, int amount, int footprintWidth = 1, int footprintHeight = 1)
        {
            var node = new ResourceNodeData(nextResourceId++, type, position, amount, footprintWidth, footprintHeight);
            resourceNodes[node.Id] = node;
            orderedResourceNodes.Add(node);

            // Mark all footprint tiles as unwalkable (like a building)
            MarkBuildingTiles(node.TileX, node.TileZ, footprintWidth, footprintHeight);

            return node;
        }

        /// <summary>
        /// Float overload for render-side callers (MapRenderer).
        /// Converts to FixedVector3 internally.
        /// </summary>
        public ResourceNodeData AddResourceNode(ResourceType type, Vector3 position, int amount, int footprintWidth = 1, int footprintHeight = 1)
        {
            return AddResourceNode(type, FixedVector3.FromVector3(position), amount, footprintWidth, footprintHeight);
        }

        public ResourceNodeData AddCarcassResourceNode(ResourceType type, FixedVector3 position, int amount)
        {
            var node = new ResourceNodeData(nextResourceId++, type, position, amount);
            node.IsCarcass = true;
            resourceNodes[node.Id] = node;
            orderedResourceNodes.Add(node);
            return node; // Does NOT mark tiles unwalkable — carcass doesn't block pathing
        }

        public ResourceNodeData AddFarmResourceNode(ResourceType type, FixedVector3 position, int amount)
        {
            var node = new ResourceNodeData(nextResourceId++, type, position, amount);
            node.IsFarmNode = true;
            resourceNodes[node.Id] = node;
            orderedResourceNodes.Add(node);
            return node;
        }

        public ResourceNodeData GetResourceNode(int id)
        {
            resourceNodes.TryGetValue(id, out var node);
            return node;
        }

        public void RemoveResourceNode(int id)
        {
            resourceNodes.Remove(id);
            for (int i = orderedResourceNodes.Count - 1; i >= 0; i--)
            {
                if (orderedResourceNodes[i].Id == id)
                {
                    orderedResourceNodes.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Clears the tiles occupied by a depleted resource node, restoring walkability.
        /// Only clears each tile if no other non-depleted resource's footprint overlaps it.
        /// </summary>
        public void ClearResourceTile(int nodeId)
        {
            if (!resourceNodes.TryGetValue(nodeId, out var node)) return;

            for (int x = node.TileX; x < node.TileX + node.FootprintWidth; x++)
            {
                for (int z = node.TileZ; z < node.TileZ + node.FootprintHeight; z++)
                {
                    if (!IsInBounds(x, z)) continue;

                    // Check if another non-depleted resource's footprint overlaps this tile
                    bool occupied = false;
                    foreach (var other in orderedResourceNodes)
                    {
                        if (other.Id == nodeId) continue;
                        if (other.IsDepleted) continue;
                        if (x >= other.TileX && x < other.TileX + other.FootprintWidth &&
                            z >= other.TileZ && z < other.TileZ + other.FootprintHeight)
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (!occupied)
                    {
                        Tiles[x, z] = TileType.Grass;
                        // Clear forest density so the tile (and gaps between trees) become walkable
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                int ax = x + dx, az = z + dz;
                                if (IsInBounds(ax, az))
                                    ForestDensity[ax, az] = 0f;
                            }
                    }
                }
            }

            // Recompute hole map — chopping trees may open paths to previously-disconnected areas
            ComputeHoleMap();
        }

        public void ComputeHoleMap()
        {
            holeMap = null; // clear so IsWalkable uses raw checks during computation

            int[,] componentId = new int[Width, Height];
            int currentId = 0;
            int largestId = 0;
            int largestSize = 0;
            var queue = new Queue<Vector2Int>();

            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Height; z++)
                {
                    if (componentId[x, z] != 0 || !IsWalkable(x, z)) continue;
                    currentId++;
                    int size = 0;
                    queue.Enqueue(new Vector2Int(x, z));
                    componentId[x, z] = currentId;

                    while (queue.Count > 0)
                    {
                        var tile = queue.Dequeue();
                        size++;
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = tile.x + Directions[d].x;
                            int nz = tile.y + Directions[d].y;
                            if (!IsInBounds(nx, nz) || componentId[nx, nz] != 0 || !IsWalkable(nx, nz))
                                continue;

                            // Diagonal corner-cutting check (same as GridPathfinder)
                            if (d >= 4)
                            {
                                if (!IsWalkable(tile.x + Directions[d].x, tile.y) || !IsWalkable(tile.x, tile.y + Directions[d].y))
                                    continue;
                            }

                            componentId[nx, nz] = currentId;
                            queue.Enqueue(new Vector2Int(nx, nz));
                        }
                    }

                    if (size > largestSize)
                    {
                        largestSize = size;
                        largestId = currentId;
                    }
                }
            }

            var newHoleMap = new bool[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                    if (componentId[x, z] != 0 && componentId[x, z] != largestId)
                        newHoleMap[x, z] = true;

            holeMap = newHoleMap;
        }

        public IReadOnlyList<ResourceNodeData> GetAllResourceNodes()
        {
            return orderedResourceNodes;
        }

        public List<ResourceNodeData> GetAllResourceNodesSorted()
        {
            var list = new List<ResourceNodeData>(orderedResourceNodes);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }
    }
}
