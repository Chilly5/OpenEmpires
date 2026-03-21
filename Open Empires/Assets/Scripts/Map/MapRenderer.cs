using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public class MapRenderer : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private float heightScale = 8f;

        [Header("Resource Prefabs")]
        [SerializeField] private GameObject goldMinePrefab;
        [SerializeField] private GameObject stoneMinePrefab;
        [SerializeField] private GameObject berryBushPrefab;

        private Dictionary<int, ResourceNode> resourceNodeViews = new Dictionary<int, ResourceNode>();
        private System.Random rng;
        private MapData cachedMapData;

        // Tree billboard data (loaded at runtime from Resources)
        private static readonly string[] TreeSpriteNames = { "TreeSprites/Beech1", "TreeSprites/Beech2", "TreeSprites/Bush1", "TreeSprites/Bush2" };
        private Material[] treeMaterials;
        public Material[] TreeMaterials => treeMaterials;
        private Transform billboardContainer;

        private static readonly Vector2Int[] BerryRingOffsets = new Vector2Int[]
        {
            new Vector2Int(0, -2),  // bottom-left
            new Vector2Int(2, -2),  // bottom-right
            new Vector2Int(-2, 1),  // left
            new Vector2Int(4,  1),  // right
            new Vector2Int(0,  4),  // top-left
            new Vector2Int(2,  4),  // top-right
        };

        public float HeightScale => heightScale;
        public Dictionary<int, ResourceNode> ResourceNodeViews => resourceNodeViews;

        public void Initialize(MapData mapData, Vector2Int[] playerBases, int seed = 42)
        {
            rng = new System.Random(seed);
            cachedMapData = mapData;
            // Billboard sprites go in a separate container to avoid static batching
            var containerGo = new GameObject("BillboardSprites");
            billboardContainer = containerGo.transform;

            LoadTreeMaterials();
            CreateTerrainMesh(mapData);
            CreateWaterPlane(mapData);
            SpawnResourceNodes(mapData, playerBases);

            Debug.Log($"[MapRenderer] Spawned {resourceNodeViews.Count} resource nodes. Running static batching...");
            StaticBatchingUtility.Combine(gameObject);

            // Parent billboard container after batching so sprites aren't included
            billboardContainer.SetParent(transform);
            billboardContainer.localPosition = Vector3.zero;
        }

        public float GetWorldHeight(float x, float z)
        {
            if (cachedMapData == null) return 0f;
            return cachedMapData.SampleHeight(x, z) * heightScale;
        }

        private void CreateTerrainMesh(MapData mapData)
        {
            var terrainGO = new GameObject("Terrain");
            terrainGO.transform.SetParent(transform);
            terrainGO.layer = LayerMask.NameToLayer("Ground");

            var meshFilter = terrainGO.AddComponent<MeshFilter>();
            var meshRenderer = terrainGO.AddComponent<MeshRenderer>();
            var meshCollider = terrainGO.AddComponent<MeshCollider>();

            int subdivisions = 256;
            int vertsPerSide = subdivisions + 1;
            float w = mapData.Width;
            float h = mapData.Height;
            float cellW = w / subdivisions;
            float cellH = h / subdivisions;

            // Use 32-bit indices for large meshes
            int vertexCount = vertsPerSide * vertsPerSide;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int z = 0; z < vertsPerSide; z++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    int i = z * vertsPerSide + x;
                    float worldX = x * cellW;
                    float worldZ = z * cellH;
                    float y = mapData.SampleHeight(worldX, worldZ) * heightScale;
                    vertices[i] = new Vector3(worldX, y, worldZ);
                    uvs[i] = new Vector2((float)x / subdivisions, (float)z / subdivisions);
                }
            }

            var triangles = new int[subdivisions * subdivisions * 6];
            int tri = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int bl = z * vertsPerSide + x;
                    int br = bl + 1;
                    int tl = bl + vertsPerSide;
                    int tr = tl + 1;
                    triangles[tri++] = bl;
                    triangles[tri++] = tl;
                    triangles[tri++] = br;
                    triangles[tri++] = br;
                    triangles[tri++] = tl;
                    triangles[tri++] = tr;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "TerrainMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();

            meshFilter.mesh = mesh;
            meshCollider.sharedMesh = mesh;

            // Create splatmap material
            var splatShader = Shader.Find("OpenEmpires/Terrain_Splatmap");
            if (splatShader != null)
            {
                var mat = new Material(splatShader);
                mat.SetTexture("_SplatMap", GenerateSplatmap(mapData));

                var (dirtTex, sandTex, rockTex, snowTex, forestFloorTex) = GenerateTerrainTextures();
                var grassArray = CreateGrassTextureArray();
                var grassIndexMap = GenerateGrassIndexMap(mapData, 9);
                mat.SetTexture("_GrassArray", grassArray);
                mat.SetTexture("_GrassIndexMap", grassIndexMap);
                mat.SetFloat("_GrassArrayCount", 9f);
                mat.SetTexture("_TexDirt", dirtTex);
                mat.SetTexture("_TexSand", sandTex);
                mat.SetTexture("_TexRock", rockTex);
                mat.SetTexture("_TexSnow", snowTex);
                mat.SetTexture("_ForestMask", GenerateForestMask(mapData));
                mat.SetTexture("_TexForestFloor", forestFloorTex);
                mat.SetFloat("_SnowStartHeight", 1.85f * heightScale);
                mat.SetFloat("_SnowFullHeight", 1.95f * heightScale);

                meshRenderer.material = mat;
            }
            else if (terrainMaterial != null)
            {
                meshRenderer.material = terrainMaterial;
            }

            terrainGO.tag = "Ground";
        }

        private Texture2D GenerateSplatmap(MapData mapData)
        {
            // 4x map resolution for smoother blending between tile types
            int texW = mapData.Width * 4;
            int texH = mapData.Height * 4;
            var splatmap = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            splatmap.filterMode = FilterMode.Bilinear;
            splatmap.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[texW * texH];

            // Generate raw splatmap with noise-perturbed boundaries for organic edges
            for (int pz = 0; pz < texH; pz++)
            {
                for (int px = 0; px < texW; px++)
                {
                    // Map pixel to world space (center of sub-pixel)
                    float worldX = (px + 0.5f) / 4f;
                    float worldZ = (pz + 0.5f) / 4f;

                    // Perlin noise perturbation to break up grid-aligned boundaries
                    float noiseScale = 0.15f;
                    float perturbStrength = 1.2f;
                    float nx = Mathf.PerlinNoise(worldX * noiseScale + 500f, worldZ * noiseScale + 500f) * 2f - 1f;
                    float nz = Mathf.PerlinNoise(worldX * noiseScale + 800f, worldZ * noiseScale + 800f) * 2f - 1f;
                    float sampleX = worldX + nx * perturbStrength;
                    float sampleZ = worldZ + nz * perturbStrength;

                    int tileX = Mathf.FloorToInt(sampleX);
                    int tileZ = Mathf.FloorToInt(sampleZ);
                    tileX = Mathf.Clamp(tileX, 0, mapData.Width - 1);
                    tileZ = Mathf.Clamp(tileZ, 0, mapData.Height - 1);

                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    var tile = mapData.Tiles[tileX, tileZ];

                    switch (tile)
                    {
                        case TileType.Grass:
                        case TileType.Building:
                        case TileType.Foundation:
                            // Blend dirt on slopes using height gradient
                            float hLeft = mapData.GetHeightClamped(tileX - 1, tileZ);
                            float hRight = mapData.GetHeightClamped(tileX + 1, tileZ);
                            float hDown = mapData.GetHeightClamped(tileX, tileZ - 1);
                            float hUp = mapData.GetHeightClamped(tileX, tileZ + 1);
                            float slope = Mathf.Abs(hRight - hLeft) + Mathf.Abs(hUp - hDown);
                            float dirtBlend = Mathf.Clamp01(slope * 8f);
                            // Noise-based dirt patches for natural variation
                            float dirtNoise = Mathf.PerlinNoise(worldX * 0.08f + 300f, worldZ * 0.08f + 300f);
                            dirtBlend = Mathf.Max(dirtBlend, Mathf.Clamp01((dirtNoise - 0.6f) * 3f) * 0.4f);
                            r = 1f - dirtBlend;
                            g = dirtBlend;
                            break;
                        case TileType.Sand:
                            b = 1f;
                            break;
                        case TileType.Rock:
                            // Blend dirt on steep rock slopes for visual variety
                            float rhLeft = mapData.GetHeightClamped(tileX - 1, tileZ);
                            float rhRight = mapData.GetHeightClamped(tileX + 1, tileZ);
                            float rhDown = mapData.GetHeightClamped(tileX, tileZ - 1);
                            float rhUp = mapData.GetHeightClamped(tileX, tileZ + 1);
                            float rockSlope = Mathf.Abs(rhRight - rhLeft) + Mathf.Abs(rhUp - rhDown);
                            float rockDirtBlend = Mathf.Clamp01(rockSlope * 5f) * 0.3f;
                            a = 1f - rockDirtBlend;
                            g = rockDirtBlend;
                            break;
                        case TileType.Cliff:
                            a = 1f;
                            break;
                        case TileType.River:
                            // Sandy riverbed under water plane
                            b = 0.6f;
                            g = 0.4f;
                            break;
                        case TileType.Water:
                            b = 1f;
                            break;
                    }

                    pixels[pz * texW + px] = new Color(r, g, b, a);
                }
            }

            // Multi-pass separable box blur to smooth terrain transitions
            SplatmapBlur(pixels, texW, texH, 3, 3);

            // Renormalize so RGBA channels always sum to 1.0
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                float sum = c.r + c.g + c.b + c.a;
                if (sum > 0.001f)
                    pixels[i] = new Color(c.r / sum, c.g / sum, c.b / sum, c.a / sum);
                else
                    pixels[i] = new Color(1f, 0f, 0f, 0f); // default to grass
            }

            splatmap.SetPixels(pixels);
            splatmap.Apply();
            return splatmap;
        }

        private void SplatmapBlur(Color[] pixels, int width, int height, int radius, int passes)
        {
            var temp = new Color[width * height];
            for (int pass = 0; pass < passes; pass++)
            {
                // Horizontal blur
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                        int count = 0;
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int sx = Mathf.Clamp(x + dx, 0, width - 1);
                            var c = pixels[y * width + sx];
                            sr += c.r; sg += c.g; sb += c.b; sa += c.a;
                            count++;
                        }
                        temp[y * width + x] = new Color(sr / count, sg / count, sb / count, sa / count);
                    }
                }
                // Vertical blur
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float sr = 0f, sg = 0f, sb = 0f, sa = 0f;
                        int count = 0;
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            int sy = Mathf.Clamp(y + dy, 0, height - 1);
                            var c = temp[sy * width + x];
                            sr += c.r; sg += c.g; sb += c.b; sa += c.a;
                            count++;
                        }
                        pixels[y * width + x] = new Color(sr / count, sg / count, sb / count, sa / count);
                    }
                }
            }
        }

        private Texture2D GenerateForestMask(MapData mapData)
        {
            int texW = mapData.Width * 4;
            int texH = mapData.Height * 4;
            var mask = new Texture2D(texW, texH, TextureFormat.R8, false);
            mask.filterMode = FilterMode.Bilinear;
            mask.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[texW * texH];

            for (int pz = 0; pz < texH; pz++)
            {
                for (int px = 0; px < texW; px++)
                {
                    int tileX = Mathf.Clamp(px / 4, 0, mapData.Width - 1);
                    int tileZ = Mathf.Clamp(pz / 4, 0, mapData.Height - 1);

                    // Align to 2x2 block corner to match tree spawn grid
                    int blockX = Mathf.Clamp(tileX & ~1, 0, mapData.Width - 1);
                    int blockZ = Mathf.Clamp(tileZ & ~1, 0, mapData.Height - 1);
                    float v = mapData.ForestDensity[blockX, blockZ] >= MapData.ForestWalkableThreshold ? 1f : 0f;

                    pixels[pz * texW + px] = new Color(v, 0f, 0f, 1f);
                }
            }

            SplatmapBlur(pixels, texW, texH, 3, 2);
            mask.SetPixels(pixels);
            mask.Apply();
            return mask;
        }

        private static readonly string[] GrassTextureNames = new string[]
        {
            "GroundTiles/Tile_Grass_1",
            "GroundTiles/Tile_Grass_2",
            "GroundTiles/Tile_Grass_3",
            "GroundTiles/Tile_Grass_4",
            "GroundTiles/Tile_RockyGrass_1",
            "GroundTiles/Tile_RockyGrass_2",
            "GroundTiles/Tile_RockyGrass_3",
            "GroundTiles/Tile_RockyDirt_1",
            "GroundTiles/Tile_Rocks_on_grass1",
            "GroundTiles/Tile_DirtyGrass",
        };

        private Texture2DArray CreateGrassTextureArray()
        {
            int size = 512;
            var texArray = new Texture2DArray(size, size, GrassTextureNames.Length, TextureFormat.RGBA32, true);
            texArray.filterMode = FilterMode.Trilinear;
            texArray.wrapMode = TextureWrapMode.Repeat;
            texArray.anisoLevel = 8;

            for (int i = 0; i < GrassTextureNames.Length; i++)
            {
                var src = Resources.Load<Texture2D>(GrassTextureNames[i]);
                if (src == null)
                {
                    Debug.LogError($"[MapRenderer] Failed to load grass texture: {GrassTextureNames[i]}");
                    continue;
                }

                // If source is compressed or wrong size, blit to a readable RGBA32 texture
                var readable = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, readable);
                var tmp = new Texture2D(size, size, TextureFormat.RGBA32, false);
                RenderTexture.active = readable;
                tmp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tmp.Apply();
                RenderTexture.active = null;

                Graphics.CopyTexture(tmp, 0, 0, texArray, i, 0);

                // Generate mipmaps by copying mip 0 and letting Unity generate the rest
                Object.Destroy(tmp);
                Object.Destroy(readable);
            }

            texArray.Apply(true); // generate mipmaps
            return texArray;
        }

        private Texture2D GenerateGrassIndexMap(MapData mapData, int variantCount)
        {
            int texW = mapData.Width * 4;
            int texH = mapData.Height * 4;
            var indexMap = new Texture2D(texW, texH, TextureFormat.R8, false);
            indexMap.filterMode = FilterMode.Bilinear;
            indexMap.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color[texW * texH];

            for (int pz = 0; pz < texH; pz++)
            {
                for (int px = 0; px < texW; px++)
                {
                    float worldX = (px + 0.5f) / 4f;
                    float worldZ = (pz + 0.5f) / 4f;

                    // Large-scale region noise for coherent patches
                    float regionNoise = Mathf.PerlinNoise(worldX * 0.03f + 1000f, worldZ * 0.03f + 1000f);
                    // Finer noise for local variation within regions
                    float localNoise = Mathf.PerlinNoise(worldX * 0.12f + 2000f, worldZ * 0.12f + 2000f);
                    float combined = regionNoise * 0.7f + localNoise * 0.3f;

                    int idx = Mathf.FloorToInt(combined * variantCount);
                    idx = Mathf.Clamp(idx, 0, variantCount - 1);

                    float normalizedIdx = (float)idx / (variantCount - 1);
                    pixels[pz * texW + px] = new Color(normalizedIdx, 0f, 0f, 1f);
                }
            }

            indexMap.SetPixels(pixels);
            indexMap.Apply();
            return indexMap;
        }

        private (Texture2D dirt, Texture2D sand, Texture2D rock, Texture2D snow, Texture2D forestFloor) GenerateTerrainTextures()
        {
            int size = 1024;
            var dirt = GenerateNoiseTexture(size, 73, new Color(0.9f, 0.85f, 0.75f), new Color(0.65f, 0.55f, 0.4f));
            var sand = GenerateNoiseTexture(size, 104, new Color(0.95f, 0.92f, 0.82f), new Color(0.8f, 0.75f, 0.6f));
            var rock = GenerateNoiseTexture(size, 135, new Color(0.85f, 0.83f, 0.8f), new Color(0.55f, 0.52f, 0.48f));
            var snow = GenerateNoiseTexture(size, 166, new Color(0.95f, 0.96f, 0.98f), new Color(0.85f, 0.88f, 0.95f));
            var forestFloor = Resources.Load<Texture2D>("GroundTiles/Tile_ForestFloor_4");
            if (forestFloor == null)
                forestFloor = GenerateNoiseTexture(size, 197, new Color(0.65f, 0.62f, 0.42f), new Color(0.45f, 0.50f, 0.30f));
            return (dirt, sand, rock, snow, forestFloor);
        }

        private Texture2D GenerateNoiseTexture(int size, int seed, Color colorA, Color colorB)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true); // mipmaps enabled
            tex.filterMode = FilterMode.Trilinear;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 8;

            float offsetX = seed * 7.3f;
            float offsetY = seed * 13.1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 5-octave noise for richer micro-detail
                    float n = Mathf.PerlinNoise(x * 0.06f + offsetX, y * 0.06f + offsetY) * 0.35f
                            + Mathf.PerlinNoise(x * 0.15f + offsetX + 100f, y * 0.15f + offsetY + 100f) * 0.25f
                            + Mathf.PerlinNoise(x * 0.4f + offsetX + 200f, y * 0.4f + offsetY + 200f) * 0.2f
                            + Mathf.PerlinNoise(x * 1.0f + offsetX + 300f, y * 1.0f + offsetY + 300f) * 0.12f
                            + Mathf.PerlinNoise(x * 2.5f + offsetX + 400f, y * 2.5f + offsetY + 400f) * 0.08f;

                    // Subtle per-pixel color variation so textures look less uniform
                    float colorShift = Mathf.PerlinNoise(x * 0.3f + offsetX + 500f, y * 0.3f + offsetY + 500f) * 0.1f - 0.05f;
                    Color col = Color.Lerp(colorA, colorB, n);
                    col.r = Mathf.Clamp01(col.r + colorShift);
                    col.g = Mathf.Clamp01(col.g + colorShift * 0.7f);
                    col.b = Mathf.Clamp01(col.b + colorShift * 0.5f);

                    tex.SetPixel(x, y, col);
                }
            }
            tex.Apply(true); // generate mipmaps
            return tex;
        }

        private void CreateWaterPlane(MapData mapData)
        {
            // Read water threshold from config if available, else estimate
            float waterY = 0.30f; // default waterThreshold
            var config = GameBootstrapper.Instance?.Simulation?.Config;
            if (config != null)
                waterY = config.WaterThreshold;
            waterY *= heightScale;

            var waterGO = new GameObject("Water");
            waterGO.transform.SetParent(transform);

            var mf = waterGO.AddComponent<MeshFilter>();
            var mr = waterGO.AddComponent<MeshRenderer>();

            // Simple quad mesh covering the map
            var mesh = new Mesh();
            mesh.name = "WaterMesh";
            float w = mapData.Width;
            float h = mapData.Height;
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(w, 0, 0),
                new Vector3(w, 0, h),
                new Vector3(0, 0, h)
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mf.mesh = mesh;

            // Custom water shader with fog of war support
            var mat = new Material(Shader.Find("OpenEmpires/Water"));
            mat.SetColor("_Color", new Color(0.1f, 0.25f, 0.5f, 0.6f));
            mat.SetFloat("_Smoothness", 0.9f);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            waterGO.transform.position = new Vector3(0f, waterY, 0f);
        }

        private Vector3 ClampToMap(float x, float z)
        {
            return new Vector3(
                Mathf.Clamp(x, 5f, cachedMapData.Width - 5f),
                0f,
                Mathf.Clamp(z, 5f, cachedMapData.Height - 5f));
        }

        private bool IsFootprintValid(MapData mapData, int originX, int originZ, int w, int h, ResourceType type)
        {
            for (int x = originX; x < originX + w; x++)
            {
                for (int z = originZ; z < originZ + h; z++)
                {
                    if (!mapData.IsInBounds(x, z)) return false;
                    var tt = mapData.Tiles[x, z];
                    if (tt != TileType.Grass && tt != TileType.Sand) return false;
                    if (type != ResourceType.Wood && mapData.ForestDensity[x, z] >= MapData.ForestWalkableThreshold) return false;
                }
            }
            return true;
        }

        private bool FindValidSpawnPosition(MapData mapData, ResourceType type, Vector3 target, int maxRadius, out Vector3 result)
        {
            int footprintW = (type == ResourceType.Wood || type == ResourceType.Food) ? 2 : 3;
            int footprintH = footprintW;

            int startOriginX, startOriginZ;
            if (footprintW == 2)
            {
                startOriginX = Mathf.FloorToInt(target.x) & ~1;
                startOriginZ = Mathf.FloorToInt(target.z) & ~1;
            }
            else
            {
                startOriginX = Mathf.FloorToInt(target.x) - 1;
                startOriginZ = Mathf.FloorToInt(target.z) - 1;
            }

            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;

                        int originX = startOriginX + dx;
                        int originZ = startOriginZ + dz;

                        if (IsFootprintValid(mapData, originX, originZ, footprintW, footprintH, type))
                        {
                            float cx = (footprintW == 2) ? originX + 1.0f : originX + 1.5f;
                            float cz = (footprintH == 2) ? originZ + 1.0f : originZ + 1.5f;
                            result = ClampToMap(cx, cz);
                            return true;
                        }
                    }
                }
            }

            result = target;
            return false;
        }

        private bool FindValidBerryRingCenter(MapData mapData, Vector3 target, int maxRadius, out int centerX, out int centerZ)
        {
            int startX = Mathf.FloorToInt(target.x) & ~1;
            int startZ = Mathf.FloorToInt(target.z) & ~1;

            for (int r = 0; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (r > 0 && Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;

                        int cx = (startX + dx) & ~1;
                        int cz = (startZ + dz) & ~1;

                        int validCount = 0;
                        for (int b = 0; b < BerryRingOffsets.Length; b++)
                        {
                            int bx = cx + BerryRingOffsets[b].x;
                            int bz = cz + BerryRingOffsets[b].y;
                            if (IsFootprintValid(mapData, bx, bz, 2, 2, ResourceType.Food))
                                validCount++;
                        }

                        if (validCount >= 4)
                        {
                            centerX = cx;
                            centerZ = cz;
                            return true;
                        }
                    }
                }
            }

            centerX = startX;
            centerZ = startZ;
            return false;
        }

        private bool IsTooCloseToBase(float x, float z, Vector2Int[] playerBases, float minDistance)
        {
            float minDistSq = minDistance * minDistance;
            for (int i = 0; i < playerBases.Length; i++)
            {
                float dx = x - playerBases[i].x;
                float dz = z - playerBases[i].y;
                if (dx * dx + dz * dz < minDistSq) return true;
            }
            return false;
        }

        private void LoadTreeMaterials()
        {
            var shader = Shader.Find("OpenEmpires/Billboard");
            if (shader == null)
            {
                Debug.LogError("[MapRenderer] OpenEmpires/Billboard shader not found!");
                return;
            }

            treeMaterials = new Material[TreeSpriteNames.Length];
            for (int i = 0; i < TreeSpriteNames.Length; i++)
            {
                var tex = Resources.Load<Texture2D>(TreeSpriteNames[i]);
                if (tex == null)
                {
                    Debug.LogError($"[MapRenderer] Failed to load tree sprite: {TreeSpriteNames[i]}");
                    continue;
                }
                var mat = new Material(shader);
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_Color", Color.white);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry + 1;
                mat.enableInstancing = true;
                treeMaterials[i] = mat;
            }
            Debug.Log($"[MapRenderer] Loaded {treeMaterials.Length} tree materials with Billboard shader");
        }

        private GameObject CreateTreeBillboard(Vector3 position, int variantIndex)
        {
            if (treeMaterials == null || variantIndex >= treeMaterials.Length || treeMaterials[variantIndex] == null)
                return null;

            var root = new GameObject("Tree");
            root.transform.SetParent(transform);
            root.transform.position = position;
            root.layer = 10;
            root.tag = "Resource";

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.5f;
            capsule.height = 2.5f;
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 1f, 0f);

            // Create quad child for the billboard sprite
            var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteGo.name = "Sprite";
            spriteGo.layer = 10;
            // Remove the auto-added MeshCollider
            var mc = spriteGo.GetComponent<MeshCollider>();
            if (mc != null) Destroy(mc);

            spriteGo.transform.SetParent(billboardContainer);
            spriteGo.transform.position = new Vector3(position.x, 5f, position.z);
            spriteGo.transform.localScale = new Vector3(5f, 5f, 1f);

            var renderer = spriteGo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = treeMaterials[variantIndex];

            return root;
        }

        private void SpawnTreesFromForestDensity(MapData mapData)
        {
            float threshold = 0.68f;
            for (int z = 0; z < mapData.Height; z += 2)
            {
                for (int x = 0; x < mapData.Width; x += 2)
                {
                    float density = mapData.ForestDensity[x, z];
                    if (density < threshold) continue;
                    SpawnResourceNode(mapData, ResourceType.Wood, ClampToMap(x, z), 100, null);
                }
            }
        }

        private void SpawnResourceNodes(MapData mapData, Vector2Int[] playerBases)
        {
            SpawnTreesFromForestDensity(mapData);
            SpawnPerPlayerResources(mapData, playerBases);
            SpawnNeutralResources(mapData, playerBases);
        }

        private void SpawnPerPlayerResources(MapData mapData, Vector2Int[] playerBases)
        {
            for (int p = 0; p < playerBases.Length; p++)
            {
                float bx = playerBases[p].x;
                float bz = playerBases[p].y;

                // Berry bushes: 6 in a ring, target offset (+14, +10), spiral retry on ring center
                Vector3 berryTarget = ClampToMap(bx + 14, bz + 10);
                if (FindValidBerryRingCenter(mapData, berryTarget, 15, out int berryCX, out int berryCZ))
                {
                    for (int b = 0; b < BerryRingOffsets.Length; b++)
                    {
                        int bx2 = berryCX + BerryRingOffsets[b].x;
                        int bz2 = berryCZ + BerryRingOffsets[b].y;
                        SpawnResourceNode(mapData, ResourceType.Food, ClampToMap(bx2 + 1f, bz2 + 1f), 500, berryBushPrefab);
                    }
                }
                else
                {
                    Debug.LogWarning($"[MapRenderer] Could not place berry patch for player {p} near ({bx + 14}, {bz + 10})");
                }

                // Gold mine: target offset (-14, +10), spiral retry
                Vector3 goldTarget = ClampToMap(bx - 14, bz + 10);
                if (FindValidSpawnPosition(mapData, ResourceType.Gold, goldTarget, 15, out Vector3 goldPos))
                {
                    SpawnResourceNode(mapData, ResourceType.Gold, goldPos, 3000, goldMinePrefab);
                }
                else
                {
                    Debug.LogWarning($"[MapRenderer] Could not place gold mine for player {p} near ({bx - 14}, {bz + 10})");
                }

                // Stone mine: target offset (+10, -14), spiral retry
                Vector3 stoneTarget = ClampToMap(bx + 10, bz - 14);
                if (FindValidSpawnPosition(mapData, ResourceType.Stone, stoneTarget, 15, out Vector3 stonePos))
                {
                    SpawnResourceNode(mapData, ResourceType.Stone, stonePos, 3000, stoneMinePrefab);
                }
                else
                {
                    Debug.LogWarning($"[MapRenderer] Could not place stone mine for player {p} near ({bx + 10}, {bz - 14})");
                }
            }
        }

        private void SpawnNeutralResources(MapData mapData, Vector2Int[] playerBases)
        {
            float baseMinDistance = 30f;
            int playerCount = playerBases.Length;

            // 3 gold mines per player
            SpawnNeutralScattered(mapData, playerBases, ResourceType.Gold,
                3 * playerCount, goldMinePrefab, 3000, baseMinDistance);

            // 2 stone mines per player
            SpawnNeutralScattered(mapData, playerBases, ResourceType.Stone,
                2 * playerCount, stoneMinePrefab, 3000, baseMinDistance);

            // 2 berry patches per player (6 bushes each)
            SpawnNeutralBerryScattered(mapData, playerBases, 2 * playerCount, baseMinDistance);

            // 4 forest clusters per player
            SpawnNeutralForestClusters(mapData, playerBases, 4 * playerCount, baseMinDistance);
        }

        private void SpawnNeutralScattered(MapData mapData, Vector2Int[] playerBases,
            ResourceType type, int count, GameObject prefab, int amount, float baseMinDistance)
        {
            int margin = 15;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float x = (float)(rng.NextDouble() * (mapData.Width - margin * 2) + margin);
                    float z = (float)(rng.NextDouble() * (mapData.Height - margin * 2) + margin);

                    if (IsTooCloseToBase(x, z, playerBases, baseMinDistance)) continue;

                    Vector3 target = ClampToMap(x, z);
                    if (FindValidSpawnPosition(mapData, type, target, 10, out Vector3 pos))
                    {
                        SpawnResourceNode(mapData, type, pos, amount, prefab);
                        break;
                    }
                }
            }
        }

        private void SpawnNeutralBerryScattered(MapData mapData, Vector2Int[] playerBases,
            int count, float baseMinDistance)
        {
            int margin = 15;
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float x = (float)(rng.NextDouble() * (mapData.Width - margin * 2) + margin);
                    float z = (float)(rng.NextDouble() * (mapData.Height - margin * 2) + margin);

                    if (IsTooCloseToBase(x, z, playerBases, baseMinDistance)) continue;

                    Vector3 target = ClampToMap(x, z);
                    if (FindValidBerryRingCenter(mapData, target, 10, out int bcx, out int bcz))
                    {
                        for (int b = 0; b < BerryRingOffsets.Length; b++)
                        {
                            int bx2 = bcx + BerryRingOffsets[b].x;
                            int bz2 = bcz + BerryRingOffsets[b].y;
                            SpawnResourceNode(mapData, ResourceType.Food, ClampToMap(bx2 + 1f, bz2 + 1f), 500, berryBushPrefab);
                        }
                        break;
                    }
                }
            }
        }

        private void SpawnNeutralForestClusters(MapData mapData, Vector2Int[] playerBases,
            int count, float baseMinDistance)
        {
            int margin = 15;
            int treesPerCluster = 8;
            int clusterRadius = 4;

            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    float cx = (float)(rng.NextDouble() * (mapData.Width - margin * 2) + margin);
                    float cz = (float)(rng.NextDouble() * (mapData.Height - margin * 2) + margin);

                    if (IsTooCloseToBase(cx, cz, playerBases, baseMinDistance)) continue;

                    // Check center tile is valid terrain before committing the cluster
                    int checkX = Mathf.FloorToInt(cx);
                    int checkZ = Mathf.FloorToInt(cz);
                    if (!mapData.IsInBounds(checkX, checkZ)) continue;
                    var tt = mapData.Tiles[checkX, checkZ];
                    if (tt != TileType.Grass && tt != TileType.Sand) continue;

                    for (int t = 0; t < treesPerCluster; t++)
                    {
                        float tx = cx + (float)(rng.NextDouble() * clusterRadius * 2 - clusterRadius);
                        float tz = cz + (float)(rng.NextDouble() * clusterRadius * 2 - clusterRadius);
                        SpawnResourceNode(mapData, ResourceType.Wood, ClampToMap(tx, tz), 100, null);
                    }
                    break;
                }
            }
        }

        private void SpawnResourceNode(MapData mapData, ResourceType type, Vector3 position, int amount, GameObject prefab)
        {
            int footprintW = 1;
            int footprintH = 1;

            if (type == ResourceType.Wood || type == ResourceType.Food)
            {
                // 2x2 footprint: snap position to center of 2x2 block
                footprintW = 2;
                footprintH = 2;
                int originX = Mathf.FloorToInt(position.x);
                originX = originX & ~1; // round down to nearest even
                int originZ = Mathf.FloorToInt(position.z);
                originZ = originZ & ~1; // round down to nearest even
                position = new Vector3(originX + 1.0f, position.y, originZ + 1.0f);
            }
            else if (type == ResourceType.Gold || type == ResourceType.Stone)
            {
                // 3x3 footprint: snap position to center of 3x3 block
                footprintW = 3;
                footprintH = 3;
                int originX = Mathf.FloorToInt(position.x) - 1;
                int originZ = Mathf.FloorToInt(position.z) - 1;
                position = new Vector3(originX + 1.5f, position.y, originZ + 1.5f);
            }

            // Skip spawning if any footprint tile is water or already occupied
            int tileX = Mathf.FloorToInt(position.x - footprintW * 0.5f);
            int tileZ = Mathf.FloorToInt(position.z - footprintH * 0.5f);
            for (int x = tileX; x < tileX + footprintW; x++)
            {
                for (int z = tileZ; z < tileZ + footprintH; z++)
                {
                    if (!mapData.IsInBounds(x, z)) return;
                    var tt = mapData.Tiles[x, z];
                    if (tt != TileType.Grass && tt != TileType.Sand) return;
                    if (type != ResourceType.Wood && mapData.ForestDensity[x, z] >= MapData.ForestWalkableThreshold) return;
                }
            }

            // Set Y from terrain height
            position.y = mapData.SampleHeight(position.x, position.z) * heightScale;

            var nodeData = mapData.AddResourceNode(type, position, amount, footprintW, footprintH);

            GameObject go = null;

            if (type == ResourceType.Wood && treeMaterials != null && treeMaterials.Length > 0)
            {
                int variant = rng.Next(treeMaterials.Length);
                go = CreateTreeBillboard(position, variant);
                if (go == null) Debug.LogError($"[MapRenderer] CreateTreeBillboard returned null for variant {variant} at {position}");
            }
            else if (prefab != null)
            {
                go = Instantiate(prefab, position, Quaternion.identity, transform);
                go.transform.localScale *= 1.5f;
                if (type == ResourceType.Gold || type == ResourceType.Stone)
                    go.transform.localScale *= 2f; // 1.5 * 2 = 3.0 total, fills 3x3 tiles
                else if (type == ResourceType.Food)
                {
                    go.transform.localScale *= 1.35f; // 1.5 * 1.35 ≈ 2.0 total, fills 2x2 tiles
                    foreach (Transform child in go.transform)
                        child.localPosition = Vector3.zero;
                }
            }

            if (go != null)
            {
                // Mark non-Wood nodes as static for batching (billboard sprites can't be static)
                if (type != ResourceType.Wood)
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        t.gameObject.isStatic = true;
                }

                // Enable GPU instancing on shared materials
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r.sharedMaterial != null)
                        r.sharedMaterial.enableInstancing = true;
                }

                var nodeView = go.GetComponent<ResourceNode>();
                if (nodeView == null)
                    nodeView = go.AddComponent<ResourceNode>();
                nodeView.Initialize(nodeData.Id, nodeData);
                resourceNodeViews[nodeData.Id] = nodeView;
            }
        }

        public void SyncFromSim(MapData mapData)
        {
            foreach (var node in mapData.GetAllResourceNodes())
            {
                if (resourceNodeViews.TryGetValue(node.Id, out var view))
                {
                    view.SyncFromSim(node);
                }
            }
        }

        public MapData CachedMapData => cachedMapData;

        public void RegisterResourceNodeView(int nodeId, ResourceNode view) { resourceNodeViews[nodeId] = view; }

        public ResourceNode GetResourceNodeView(int nodeId)
        {
            resourceNodeViews.TryGetValue(nodeId, out var view);
            return view;
        }

    }
}
