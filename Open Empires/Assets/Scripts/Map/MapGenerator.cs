using Unity.Mathematics;
using UnityEngine;

namespace OpenEmpires
{
    public static class MapGenerator
    {
        // Gentle rolling hills
        private const float HillFrequency = 0.008f;
        private const float HillAmplitude = 0.06f;

        // Cliff parameters
        private const float CliffHeight = 0.75f;
        private const float CliffSlopeThreshold = 0.055f;
        private const int CliffsPerSide = 3;
        private const int CliffMinLength = 25;
        private const int CliffMaxLength = 55;

        // Pond parameters
        private const int PondCount = 3;
        private const int PondMinRadius = 8;
        private const int PondMaxRadius = 16;

        // Forest parameters
        private const float ForestFrequency = 0.018f;
        private const float ForestSecondaryFreq = 0.04f;
        private const float ForestPlayerRadius = 10f;
        private const float ForestPlayerStrength = 0.35f;
        private const float ForestThreshold = 0.68f;

        // Base positioning
        private const int BaseClearRadius = 15;

        public static (TileType[,] tiles, float[,] heights, Vector2Int[] basePositions, float[,] forestDensity) Generate(
            int width, int height, int seed, float waterThreshold, int playerCount, int[] teamAssignments = null)
        {
            var tiles = new TileType[width, height];
            var heights = new float[width, height];
            var forestDensity = new float[width, height];
            var rng = new System.Random(seed);
            float offsetX = (float)(rng.NextDouble() * 10000);
            float offsetZ = (float)(rng.NextDouble() * 10000);

            // Step 1: Flat base with gentle rolling hills
            float baseHeight = 0.45f;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float2 c1 = new float2((x + offsetX) * HillFrequency, (z + offsetZ) * HillFrequency);
                    float2 c2 = new float2((x + offsetX) * HillFrequency * 2.1f + 500f, (z + offsetZ) * HillFrequency * 2.1f + 500f);
                    float hills = noise.snoise(c1) * HillAmplitude + noise.snoise(c2) * HillAmplitude * 0.5f;
                    heights[x, z] = baseHeight + hills;
                }
            }

            // Step 2: Small cliff patches on left and right sides
            for (int i = 0; i < CliffsPerSide; i++)
            {
                AddCliffPatch(heights, width, height, rng, leftSide: true);
                AddCliffPatch(heights, width, height, rng, leftSide: false);
            }

            // Step 3: Small ponds scattered in the middle area
            for (int i = 0; i < PondCount; i++)
            {
                int px = rng.Next(width / 4, 3 * width / 4);
                int pz = rng.Next(height / 4, 3 * height / 4);
                int radius = rng.Next(PondMinRadius, PondMaxRadius + 1);
                CarvePond(heights, width, height, px, pz, radius, waterThreshold);
            }

            // Step 4: Assign tile types
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float h = heights[x, z];
                    if (h < waterThreshold)
                    {
                        tiles[x, z] = TileType.Water;
                        float depth = waterThreshold - h;
                        heights[x, z] = waterThreshold - 0.02f - depth * 0.5f;
                    }
                    else if (h < waterThreshold + 0.05f)
                    {
                        tiles[x, z] = TileType.Sand;
                    }
                    else if (h < 0.75f)
                    {
                        tiles[x, z] = TileType.Grass;
                    }
                    else
                    {
                        tiles[x, z] = TileType.Rock;
                    }
                }
            }

            // Step 5: Cliff detection on steep slopes
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var t = tiles[x, z];
                    if (t != TileType.Grass && t != TileType.Sand) continue;

                    float h = heights[x, z];
                    float maxSlope = 0f;
                    if (x > 0) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(h - heights[x - 1, z]));
                    if (x < width - 1) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(h - heights[x + 1, z]));
                    if (z > 0) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(h - heights[x, z - 1]));
                    if (z < height - 1) maxSlope = Mathf.Max(maxSlope, Mathf.Abs(h - heights[x, z + 1]));

                    if (maxSlope > CliffSlopeThreshold)
                        tiles[x, z] = TileType.Cliff;
                }
            }

            // Step 5.5: Generate forest density noise over all Grass tiles
            float forestOffX = (float)(rng.NextDouble() * 10000);
            float forestOffZ = (float)(rng.NextDouble() * 10000);
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (tiles[x, z] != TileType.Grass)
                    {
                        forestDensity[x, z] = 0f;
                        continue;
                    }
                    float2 c1 = new float2((x + forestOffX) * ForestFrequency, (z + forestOffZ) * ForestFrequency);
                    float2 c2 = new float2((x + forestOffX) * ForestSecondaryFreq + 300f, (z + forestOffZ) * ForestSecondaryFreq + 300f);
                    float n = noise.snoise(c1) * 0.65f + noise.snoise(c2) * 0.35f;
                    forestDensity[x, z] = (n + 1f) * 0.5f; // remap [-1,1] to [0,1]
                }
            }

            // Step 6: Player base positioning
            var basePositions = FindBasePositions(heights, tiles, width, height, playerCount, teamAssignments);
            for (int p = 0; p < basePositions.Length; p++)
                ClearAreaForBase(tiles, heights, basePositions[p].x + 2, basePositions[p].y + 2, BaseClearRadius, width, height);

            // Step 7: Inject per-player forest seeds at cluster offsets
            Vector2Int[] clusterOffsets = new Vector2Int[]
            {
                new Vector2Int(12, 5),
                new Vector2Int(-5, 14),
                new Vector2Int(8, -10)
            };
            for (int p = 0; p < basePositions.Length; p++)
            {
                for (int c = 0; c < clusterOffsets.Length; c++)
                {
                    int seedX = basePositions[p].x + clusterOffsets[c].x;
                    int seedZ = basePositions[p].y + clusterOffsets[c].y;
                    int r = Mathf.CeilToInt(ForestPlayerRadius) + 2;
                    for (int dz = -r; dz <= r; dz++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int tx = seedX + dx;
                            int tz = seedZ + dz;
                            if (tx < 0 || tx >= width || tz < 0 || tz >= height) continue;
                            if (tiles[tx, tz] != TileType.Grass) continue;
                            float dist = Mathf.Sqrt(dx * dx + dz * dz);
                            float t = Mathf.Clamp01(dist / ForestPlayerRadius);
                            float falloff = 1f - t * t * (3f - 2f * t); // smoothstep
                            float boost = falloff * ForestPlayerStrength;
                            forestDensity[tx, tz] = Mathf.Max(forestDensity[tx, tz], forestDensity[tx, tz] + boost);
                            forestDensity[tx, tz] = Mathf.Min(forestDensity[tx, tz], 1f);
                        }
                    }
                }
            }

            // Step 8: Zero out forest density within base clear radius
            for (int p = 0; p < basePositions.Length; p++)
            {
                int bcx = basePositions[p].x + 2;
                int bcz = basePositions[p].y + 2;
                for (int dz = -BaseClearRadius; dz <= BaseClearRadius; dz++)
                {
                    for (int dx = -BaseClearRadius; dx <= BaseClearRadius; dx++)
                    {
                        int tx = bcx + dx;
                        int tz = bcz + dz;
                        if (tx < 0 || tx >= width || tz < 0 || tz >= height) continue;
                        if (dx * dx + dz * dz > BaseClearRadius * BaseClearRadius) continue;
                        forestDensity[tx, tz] = 0f;
                    }
                }
            }

            return (tiles, heights, basePositions, forestDensity);
        }

        private static void AddCliffPatch(float[,] heights, int width, int height, System.Random rng, bool leftSide)
        {
            // Random center X near left or right side
            int cx = leftSide
                ? rng.Next((int)(width * 0.08f), (int)(width * 0.25f))
                : rng.Next((int)(width * 0.75f), (int)(width * 0.92f));
            int cz = rng.Next(height / 6, 5 * height / 6);
            int patchLength = rng.Next(CliffMinLength, CliffMaxLength + 1);
            int patchWidth = rng.Next(8, 16);
            float heightVar = 0.85f + (float)rng.NextDouble() * 0.3f;

            int zStart = cz - patchLength / 2;
            int zEnd = cz + patchLength / 2;
            int xStart = cx - patchWidth / 2;
            int xEnd = cx + patchWidth / 2;

            for (int z = zStart - 4; z <= zEnd + 4; z++)
            {
                for (int x = xStart - 4; x <= xEnd + 4; x++)
                {
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;

                    // Distance to patch rectangle (0 inside, >0 outside)
                    float dx = Mathf.Max(0, Mathf.Max(xStart - x, x - xEnd));
                    float dz = Mathf.Max(0, Mathf.Max(zStart - z, z - zEnd));
                    float edgeDist = Mathf.Sqrt(dx * dx + dz * dz);

                    // Fade to 0 over 4 tiles outside the rectangle
                    float fade = 1f - Mathf.Clamp01(edgeDist / 4f);
                    fade = fade * fade * (3f - 2f * fade); // smoothstep

                    // Also fade at the Z ends of the patch for rounded tips
                    float zFade = 1f;
                    if (z >= zStart && z <= zEnd)
                    {
                        float distFromZCenter = Mathf.Abs(z - cz) / (float)(patchLength / 2);
                        zFade = 1f - Mathf.Clamp01((distFromZCenter - 0.7f) / 0.3f);
                        zFade = zFade * zFade * (3f - 2f * zFade);
                    }

                    float strength = fade * zFade;
                    if (strength < 0.01f) continue;

                    float boost = CliffHeight * heightVar * strength;
                    heights[x, z] = Mathf.Max(heights[x, z], heights[x, z] + boost);
                }
            }
        }

        private static void CarvePond(float[,] heights, int width, int height, int cx, int cz, int radius, float waterThreshold)
        {
            float radiusSq = radius * radius;
            float targetDepth = waterThreshold - 0.04f;

            for (int x = cx - radius - 2; x <= cx + radius + 2; x++)
            {
                for (int z = cz - radius - 2; z <= cz + radius + 2; z++)
                {
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;
                    int dx = x - cx;
                    int dz = z - cz;
                    float distSq = dx * dx + dz * dz;

                    if (distSq <= radiusSq)
                    {
                        // Interior: push below water threshold
                        float t = distSq / radiusSq;
                        t = t * t; // ease-in
                        heights[x, z] = Mathf.Lerp(targetDepth, heights[x, z], t);
                    }
                    else if (distSq <= (radius + 2f) * (radius + 2f))
                    {
                        // Sandy beach fringe: blend toward sand-level height
                        float outerT = (Mathf.Sqrt(distSq) - radius) / 2f;
                        float sandHeight = waterThreshold + 0.02f;
                        heights[x, z] = Mathf.Lerp(sandHeight, heights[x, z], outerT);
                    }
                }
            }
        }

        private static Vector2Int[] FindBasePositions(float[,] heights, TileType[,] tiles, int width, int height, int playerCount, int[] teamAssignments = null)
        {
            // Scale search parameters to map size
            int searchRadius = Mathf.Max(20, width / 12);
            int edgeMargin = Mathf.Max(15, width / 16);

            // Determine zone assignment based on teams
            float circleRadius = Mathf.Min(width, height) / 2f - 10f;
            bool hasTeams = teamAssignments != null && HasRealTeams(teamAssignments);
            float spawnRadius = hasTeams
                ? Mathf.Min(circleRadius * 0.6f, 100f)
                : circleRadius * 0.6f;
            float cx = width / 2f;
            float cz = height / 2f;
            Vector2Int[] zones;
            float minDistance = Mathf.Max(40f, width * 0.15f);

            if (hasTeams)
            {
                // Team mode: team 0 on angles 0-180, team 1 on angles 180-360
                int team0Count = 0;
                int team1Count = 0;
                for (int p = 0; p < playerCount; p++)
                {
                    if (teamAssignments[p] == 0) team0Count++;
                    else team1Count++;
                }

                zones = new Vector2Int[playerCount];
                int t0Index = 0;
                int t1Index = 0;
                int maxTeamSize = Mathf.Max(team0Count, team1Count);
                float minAngularSep = 2f * Mathf.Asin(minDistance / (2f * spawnRadius));
                float maxTeamArc = Mathf.Clamp(minAngularSep * maxTeamSize, Mathf.PI / 4f, Mathf.PI * 0.75f);
                for (int p = 0; p < playerCount; p++)
                {
                    float angle;
                    if (teamAssignments[p] == 0)
                    {
                        float startAngle = -maxTeamArc / 2f;
                        angle = startAngle + (t0Index + 0.5f) * maxTeamArc / team0Count;
                        t0Index++;
                    }
                    else
                    {
                        float startAngle = Mathf.PI - maxTeamArc / 2f;
                        angle = startAngle + (t1Index + 0.5f) * maxTeamArc / team1Count;
                        t1Index++;
                    }
                    zones[p] = new Vector2Int(
                        (int)(cx + spawnRadius * Mathf.Cos(angle)),
                        (int)(cz + spawnRadius * Mathf.Sin(angle)));
                }
            }
            else
            {
                // FFA / 1v1: evenly distribute around full circle
                zones = new Vector2Int[playerCount];
                for (int p = 0; p < playerCount; p++)
                {
                    float angle = p * 2f * Mathf.PI / playerCount;
                    zones[p] = new Vector2Int(
                        (int)(cx + spawnRadius * Mathf.Cos(angle)),
                        (int)(cz + spawnRadius * Mathf.Sin(angle)));
                }
            }

            var result = new Vector2Int[playerCount];

            for (int p = 0; p < playerCount; p++)
            {
                Vector2Int zone = zones[p];
                Vector2Int best = zone;
                float bestScore = float.MinValue;

                float maxSpawnDistSq = (circleRadius - edgeMargin) * (circleRadius - edgeMargin);

                for (int x = zone.x - searchRadius; x <= zone.x + searchRadius; x += 4)
                {
                    for (int z = zone.y - searchRadius; z <= zone.y + searchRadius; z += 4)
                    {
                        float ddx = x - cx;
                        float ddz = z - cz;
                        if (ddx * ddx + ddz * ddz > maxSpawnDistSq) continue;
                        float score = ScoreBaseCandidate(heights, tiles, width, height, x, z);
                        score += ProximityPenalty(x, z, result, p, minDistance);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = new Vector2Int(x, z);
                        }
                    }
                }

                Vector2Int refined = best;
                float refinedScore = bestScore;
                for (int x = best.x - 5; x <= best.x + 5; x++)
                {
                    for (int z = best.y - 5; z <= best.y + 5; z++)
                    {
                        float ddx2 = x - cx;
                        float ddz2 = z - cz;
                        if (ddx2 * ddx2 + ddz2 * ddz2 > maxSpawnDistSq) continue;
                        float score = ScoreBaseCandidate(heights, tiles, width, height, x, z);
                        score += ProximityPenalty(x, z, result, p, minDistance);
                        if (score > refinedScore)
                        {
                            refinedScore = score;
                            refined = new Vector2Int(x, z);
                        }
                    }
                }

                result[p] = refined;
            }

            return result;
        }

        /// <summary>Returns true if teamAssignments contains real teams (not all-different FFA).</summary>
        private static bool HasRealTeams(int[] teamAssignments)
        {
            for (int i = 1; i < teamAssignments.Length; i++)
            {
                if (teamAssignments[i] == teamAssignments[0])
                    return true;
            }
            return false;
        }

        /// <summary>Heavy penalty for candidates too close to already-placed bases (prevents TC overlap).</summary>
        private static float ProximityPenalty(int x, int z, Vector2Int[] placed, int placedCount, float minDistance)
        {
            const float penalty = -10000f;
            float total = 0f;
            for (int i = 0; i < placedCount; i++)
            {
                float dx = x - placed[i].x;
                float dz = z - placed[i].y;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < minDistance)
                    total += penalty;
            }
            return total;
        }

        private static float ScoreBaseCandidate(float[,] heights, TileType[,] tiles, int width, int height, int cx, int cz)
        {
            float score = 0f;
            int checkRadius = 8;
            float heightSum = 0f;
            float heightSumSq = 0f;
            int count = 0;

            for (int dx = -checkRadius; dx <= checkRadius; dx++)
            {
                for (int dz = -checkRadius; dz <= checkRadius; dz++)
                {
                    int x = cx + dx;
                    int z = cz + dz;
                    if (x < 0 || x >= width || z < 0 || z >= height) continue;

                    var t = tiles[x, z];
                    float h = heights[x, z];

                    if (t == TileType.Grass) score += 2f;
                    else if (t == TileType.Sand) score += 0.5f;
                    else if (t == TileType.Water) score -= 5f;
                    else if (t == TileType.Rock || t == TileType.Cliff) score -= 3f;

                    if (h >= 0.38f && h <= 0.55f) score += 1f;

                    heightSum += h;
                    heightSumSq += h * h;
                    count++;
                }
            }

            if (count > 0)
            {
                float mean = heightSum / count;
                float variance = (heightSumSq / count) - (mean * mean);
                score -= variance * 200f;
            }

            float edgeMargin = Mathf.Max(15, width / 16);
            float circR = Mathf.Min(width, height) / 2f - 10f;
            float dxC = cx - width / 2f;
            float dzC = cz - height / 2f;
            float distFromEdge = circR - Mathf.Sqrt(dxC * dxC + dzC * dzC);
            if (distFromEdge > edgeMargin) score += 5f;

            return score;
        }

        public static void ClearAreaForBase(TileType[,] tiles, float[,] heights, int centerX, int centerZ, int radius, int mapWidth, int mapHeight)
        {
            float sumHeight = 0f;
            int count = 0;
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int z = centerZ - radius; z <= centerZ + radius; z++)
                {
                    if (x < 0 || x >= mapWidth || z < 0 || z >= mapHeight) continue;
                    int dx = x - centerX;
                    int dz = z - centerZ;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    sumHeight += heights[x, z];
                    count++;
                }
            }
            float flatHeight = count > 0 ? sumHeight / count : 0.4f;
            flatHeight = Mathf.Clamp(flatHeight, 0.36f, 0.70f);

            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int z = centerZ - radius; z <= centerZ + radius; z++)
                {
                    if (x < 0 || x >= mapWidth || z < 0 || z >= mapHeight) continue;
                    int dx = x - centerX;
                    int dz = z - centerZ;
                    float distSq = dx * dx + dz * dz;
                    float radiusSq = radius * radius;
                    if (distSq > radiusSq) continue;

                    float t = distSq / radiusSq;
                    t = t * t;

                    tiles[x, z] = TileType.Grass;
                    heights[x, z] = Mathf.Lerp(flatHeight, heights[x, z], t);
                }
            }
        }
    }
}
