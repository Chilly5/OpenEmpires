using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private RTSCameraController cameraController;
        [SerializeField] private UnitSelectionManager selectionManager;

        private const int TexSize = 256;
        private const int DotSize = 3;
        private const float MinimapSize = 200f;
        private const float Margin = 10f;
        private const float BorderWidth = 2f;

        private int mapWidth;
        private int mapHeight;

        // Composite texture rendered each frame
        private Texture2D compositeTexture;
        private Color32[] compositePixels;
        private Color32[] mapPixelsCache;

        // Circular map mask — true for pixels inside the playable circle
        private bool[] circleMask;

        // Rotated cropped view: texture shows only the circular region, rotated so camera forward = up
        private float mapCenterX;
        private float mapCenterZ;
        private float mapCircleRadius;
        private float rotCos; // cos(initialYaw)
        private float rotSin; // sin(initialYaw)

        // Cached composite (after fog/dots/mask, before frustum)
        private Color32[] cachedCompositePixels;

        // Fog source
        private Color32[] fogPixelBuffer;

        // Canvas elements
        private RectTransform minimapRT;
        private Canvas minimapCanvas;

        private Camera mainCamera;
        private int minimapFrameCounter;

        // Under-attack alert system
        private float lastAttackAlertTime = -100f;
        private const float AttackAlertCooldown = 10f;
        private const float PingDuration = 3f;
        private const float PingFlashRate = 4f;
        private int lastAlertCheckTick;

        private struct MinimapPing
        {
            public float worldX, worldZ, timeRemaining;
        }
        private List<MinimapPing> activePings = new List<MinimapPing>();

        // Cached viewport quad corners (world-space ground intersections)
        private Vector3[] viewportGroundCorners = new Vector3[4];
        private bool viewportQuadValid;

        private void Start()
        {
            mainCamera = Camera.main;
            StartCoroutine(WaitAndInitialize());
        }

        private System.Collections.IEnumerator WaitAndInitialize()
        {
            while (GameBootstrapper.Instance?.Simulation == null)
                yield return null;

            var sim = GameBootstrapper.Instance.Simulation;
            mapWidth = sim.MapData.Width;
            mapHeight = sim.MapData.Height;

            // Generate base map pixels
            GenerateMapTexture(sim.MapData);

            // Composite texture that gets updated each frame
            compositeTexture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            compositeTexture.filterMode = FilterMode.Point;
            compositeTexture.wrapMode = TextureWrapMode.Clamp;
            compositePixels = new Color32[TexSize * TexSize];
            cachedCompositePixels = new Color32[TexSize * TexSize];
            System.Array.Copy(mapPixelsCache, cachedCompositePixels, mapPixelsCache.Length);

            fogPixelBuffer = new Color32[mapWidth * mapHeight];

            BuildCanvas();
        }

        private void GenerateMapTexture(MapData mapData)
        {
            Color32 grass = new Color32(46, 107, 36, 255);
            Color32 water = new Color32(38, 77, 153, 255);
            Color32 sand = new Color32(194, 179, 128, 255);
            Color32 rock = new Color32(115, 110, 102, 255);
            Color32 forest = new Color32(54, 40, 16, 255);
            Color32 black = new Color32(0, 0, 0, 255);

            // Rotated cropped view: initial camera yaw (45 deg) treated as "north" (up on minimap)
            mapCenterX = mapWidth / 2f;
            mapCenterZ = mapHeight / 2f;
            mapCircleRadius = Mathf.Min(mapWidth, mapHeight) / 2f - 10f;
            float initialYawRad = 45f * Mathf.Deg2Rad;
            rotCos = Mathf.Cos(initialYawRad);
            rotSin = Mathf.Sin(initialYawRad);

            // Circle mask fills the entire texture (circle edge = texture edge)
            float texCenter = TexSize / 2f;
            float texRadiusSq = texCenter * texCenter;
            circleMask = new bool[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float dx = x + 0.5f - texCenter;
                    float dy = y + 0.5f - texCenter;
                    circleMask[y * TexSize + x] = dx * dx + dy * dy <= texRadiusSq;
                }
            }

            var pixels = new Color32[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    int idx = y * TexSize + x;
                    if (!circleMask[idx])
                    {
                        pixels[idx] = black;
                        continue;
                    }

                    PixelToWorld(x, y, out float worldX, out float worldZ);
                    int tileX = Mathf.Clamp((int)worldX, 0, mapWidth - 1);
                    int tileZ = Mathf.Clamp((int)worldZ, 0, mapHeight - 1);

                    TileType tile = mapData.Tiles[tileX, tileZ];
                    Color32 c;
                    switch (tile)
                    {
                        case TileType.Water: c = water; break;
                        case TileType.Sand: c = sand; break;
                        case TileType.Rock: c = rock; break;
                        case TileType.Cliff: c = rock; break;
                        case TileType.River: c = water; break;
                        default: c = grass; break;
                    }
                    // Align to 2x2 block corner to match tree spawn grid
                    int blockX = Mathf.Clamp(tileX & ~1, 0, mapWidth - 1);
                    int blockZ = Mathf.Clamp(tileZ & ~1, 0, mapHeight - 1);
                    if (tile == TileType.Grass && mapData.ForestDensity[blockX, blockZ] >= MapData.ForestWalkableThreshold)
                        c = forest;
                    pixels[idx] = c;
                }
            }

            // Cache base map pixels for fast reset each frame
            mapPixelsCache = new Color32[TexSize * TexSize];
            System.Array.Copy(pixels, mapPixelsCache, pixels.Length);
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("MinimapCanvas");
            canvasGO.transform.SetParent(transform, false);
            minimapCanvas = canvasGO.AddComponent<Canvas>();
            minimapCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            minimapCanvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // Generate a circular sprite for border and mask (high-res for smooth edges)
            var circleSprite = CreateCircleSprite(512);

            // Border (dark gray circular background, slightly larger than minimap)
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(canvasGO.transform, false);
            var borderRT = borderGO.AddComponent<RectTransform>();
            borderRT.anchorMin = new Vector2(1f, 0f);
            borderRT.anchorMax = new Vector2(1f, 0f);
            borderRT.pivot = new Vector2(1f, 0f);
            borderRT.anchoredPosition = new Vector2(-Margin, Margin);
            borderRT.sizeDelta = new Vector2(MinimapSize + BorderWidth * 2f, MinimapSize + BorderWidth * 2f);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.sprite = circleSprite;
            borderImg.type = Image.Type.Simple;
            borderImg.color = new Color(0.2f, 0.2f, 0.2f);
            borderImg.raycastTarget = false;

            // Mask container (clips children to circle)
            var maskGO = new GameObject("Mask");
            maskGO.transform.SetParent(borderGO.transform, false);
            var maskRT = maskGO.AddComponent<RectTransform>();
            maskRT.anchorMin = new Vector2(0.5f, 0.5f);
            maskRT.anchorMax = new Vector2(0.5f, 0.5f);
            maskRT.pivot = new Vector2(0.5f, 0.5f);
            maskRT.anchoredPosition = Vector2.zero;
            maskRT.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            var maskImg = maskGO.AddComponent<Image>();
            maskImg.sprite = circleSprite;
            maskImg.type = Image.Type.Simple;
            maskImg.raycastTarget = false;
            var mask = maskGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Map RawImage (child of mask, so it's clipped to circle)
            var mapGO = new GameObject("Map");
            mapGO.transform.SetParent(maskGO.transform, false);
            minimapRT = mapGO.AddComponent<RectTransform>();
            minimapRT.anchorMin = new Vector2(0.5f, 0.5f);
            minimapRT.anchorMax = new Vector2(0.5f, 0.5f);
            minimapRT.pivot = new Vector2(0.5f, 0.5f);
            minimapRT.anchoredPosition = Vector2.zero;
            minimapRT.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            var rawImg = mapGO.AddComponent<RawImage>();
            rawImg.texture = compositeTexture;
            rawImg.raycastTarget = false;
        }

        private static Sprite CreateCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float center = size / 2f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Antialias: 1-pixel soft edge
                    float alpha = Mathf.Clamp01(radius - dist);
                    byte a = (byte)(alpha * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (compositeTexture == null) return;

            // Compute screen circle for hover suppression
            float uiScale = ComputeUIScale();
            float scaledSize = MinimapSize * uiScale;
            float scaledMargin = Margin * uiScale;
            float screenMinimapX = Screen.width - scaledMargin - scaledSize;
            float screenMinimapY = scaledMargin;
            var screenRect = new Rect(screenMinimapX, screenMinimapY, scaledSize, scaledSize);

            // Circular hit test
            Vector2 mousePos = VirtualCursor.Position;
            float circCenterX = screenRect.x + screenRect.width * 0.5f;
            float circCenterY = screenRect.y + screenRect.height * 0.5f;
            float circRadius = scaledSize * 0.5f;
            float mdx = mousePos.x - circCenterX;
            float mdy = mousePos.y - circCenterY;
            bool insideCircle = mdx * mdx + mdy * mdy <= circRadius * circRadius;

            UnitSelectionManager.SetMinimapSuppressed(insideCircle);

            // Handle input
            HandleMinimapInput(screenRect, insideCircle);

            // Render composite at reduced rate (fog, dots, mask)
            if (++minimapFrameCounter >= 5)
            {
                minimapFrameCounter = 0;
                RenderComposite();
            }

            // Check for off-screen attacks on local player's entities
            CheckForAttacks();

            // Draw frustum every frame on top of cached composite
            DrawViewportOverlay();
        }

        private float ComputeUIScale()
        {
            const float refW = 1280f;
            const float refH = 720f;
            float logWidth = Mathf.Log(Screen.width / refW, 2);
            float logHeight = Mathf.Log(Screen.height / refH, 2);
            return Mathf.Pow(2, Mathf.Lerp(logWidth, logHeight, 0.5f));
        }

        private void RenderComposite()
        {
            // 1. Reset to base map
            System.Array.Copy(mapPixelsCache, compositePixels, mapPixelsCache.Length);

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) goto apply;

            int localPlayerId = selectionManager != null ? selectionManager.LocalPlayerId : 0;

            // 2. Resource dots (below fog)
            DrawResourceDots(sim);

            // 3. Building dots (below fog)
            DrawBuildingDots(sim, localPlayerId);

            // 4. Fog overlay (alpha blend black)
            BlendFog(sim, localPlayerId);

            // 5. Unit dots (above fog)
            DrawUnitDots(sim, localPlayerId);

            // 6. Waypoint lines (above fog)
            DrawWaypointLines(sim);

            // 6b. Rally point lines (above fog)
            DrawRallyLines(sim);

            // Black out pixels outside the circular map boundary
            if (circleMask != null)
            {
                Color32 black = new Color32(0, 0, 0, 255);
                for (int i = 0; i < compositePixels.Length; i++)
                    if (!circleMask[i])
                        compositePixels[i] = black;
            }

            apply:
            // Cache the composite (before frustum) so frustum can be redrawn every frame
            System.Array.Copy(compositePixels, cachedCompositePixels, compositePixels.Length);
        }

        private void DrawViewportOverlay()
        {
            // Copy cached composite, draw frustum on top, then apply
            System.Array.Copy(cachedCompositePixels, compositePixels, cachedCompositePixels.Length);
            DrawViewportRect();
            UpdateAndDrawPings();
            compositeTexture.SetPixels32(compositePixels);
            compositeTexture.Apply();
        }

        // ---- Coordinate Transforms (rotated so initial camera yaw = up) ----

        private void WorldToPixel(float worldX, float worldZ, out int px, out int py)
        {
            float dx = worldX - mapCenterX;
            float dz = worldZ - mapCenterZ;
            float invScale = (TexSize * 0.5f) / mapCircleRadius;
            px = Mathf.Clamp((int)(TexSize * 0.5f + (dx * rotCos - dz * rotSin) * invScale), 0, TexSize - 1);
            py = Mathf.Clamp((int)(TexSize * 0.5f + (dx * rotSin + dz * rotCos) * invScale), 0, TexSize - 1);
        }

        private void WorldToPixelUnclamped(float worldX, float worldZ, out int px, out int py)
        {
            float dx = worldX - mapCenterX;
            float dz = worldZ - mapCenterZ;
            float invScale = (TexSize * 0.5f) / mapCircleRadius;
            px = (int)(TexSize * 0.5f + (dx * rotCos - dz * rotSin) * invScale);
            py = (int)(TexSize * 0.5f + (dx * rotSin + dz * rotCos) * invScale);
        }

        private void PixelToWorld(int px, int py, out float worldX, out float worldZ)
        {
            float dpx = px + 0.5f - TexSize * 0.5f;
            float dpy = py + 0.5f - TexSize * 0.5f;
            float scale = mapCircleRadius / (TexSize * 0.5f);
            worldX = mapCenterX + (dpx * rotCos + dpy * rotSin) * scale;
            worldZ = mapCenterZ + (-dpx * rotSin + dpy * rotCos) * scale;
        }

        private void DrawDot(int cx, int cy, int size, Color32 color)
        {
            int half = size / 2;
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px >= 0 && px < TexSize && py >= 0 && py < TexSize)
                        compositePixels[py * TexSize + px] = color;
                }
            }
        }

        private void DrawResourceDots(GameSimulation sim)
        {
            foreach (var node in sim.MapData.GetAllResourceNodes())
            {
                if (node.IsDepleted) continue;

                Color32 color = node.Type switch
                {
                    ResourceType.Food => new Color32(179, 38, 38, 255),
                    ResourceType.Wood => new Color32(26, 128, 26, 255),
                    ResourceType.Gold => new Color32(230, 204, 26, 255),
                    ResourceType.Stone => new Color32(153, 153, 153, 255),
                    _ => new Color32(255, 255, 255, 255)
                };

                WorldToPixel(node.Position.x.ToFloat(), node.Position.z.ToFloat(), out int px, out int py);
                DrawDot(px, py, 2, color);
            }
        }

        private void DrawBuildingDots(GameSimulation sim, int localPlayerId)
        {
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building.IsDestroyed) continue;

                int size = building.Type == BuildingType.TownCenter ? 4 : 3;
                WorldToPixel(building.SimPosition.x.ToFloat(), building.SimPosition.z.ToFloat(), out int px, out int py);

                Color playerColor = GameSetup.PlayerColors[building.PlayerId];
                Color32 c32 = playerColor;
                DrawDot(px, py, size, c32);
            }
        }

        private void BlendFog(GameSimulation sim, int localPlayerId)
        {
            var fogData = sim.FogOfWar;

            // Update fog buffer
            for (int z = 0; z < mapHeight; z++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    var vis = fogData.GetVisibility(localPlayerId, x, z);
                    fogPixelBuffer[z * mapWidth + x] = vis switch
                    {
                        TileVisibility.Visible => new Color32(0, 0, 0, 0),
                        TileVisibility.Explored => new Color32(0, 0, 0, 160),
                        _ => new Color32(0, 0, 0, 255)
                    };
                }
            }

            // Blend fog onto composite (fog is mapWidth x mapHeight, composite is TexSize x TexSize)
            for (int cy = 0; cy < TexSize; cy++)
            {
                for (int cx = 0; cx < TexSize; cx++)
                {
                    PixelToWorld(cx, cy, out float fogWX, out float fogWZ);
                    int fogX = Mathf.Clamp((int)fogWX, 0, mapWidth - 1);
                    int fogY = Mathf.Clamp((int)fogWZ, 0, mapHeight - 1);

                    Color32 fog = fogPixelBuffer[fogY * mapWidth + fogX];
                    if (fog.a == 0) continue;

                    int idx = cy * TexSize + cx;
                    Color32 src = compositePixels[idx];

                    if (fog.a == 255)
                    {
                        compositePixels[idx] = new Color32(0, 0, 0, 255);
                    }
                    else
                    {
                        // Alpha blend: dst = src * (1-a) + fog * a
                        int a = fog.a;
                        int ia = 255 - a;
                        compositePixels[idx] = new Color32(
                            (byte)((src.r * ia) / 255),
                            (byte)((src.g * ia) / 255),
                            (byte)((src.b * ia) / 255),
                            255);
                    }
                }
            }
        }

        private void DrawUnitDots(GameSimulation sim, int localPlayerId)
        {
            var units = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State == UnitState.Dead) continue;
                if (unit.PlayerId < 0 || unit.PlayerId >= GameSetup.PlayerColors.Length) continue;

                if (unit.PlayerId != localPlayerId)
                {
                    Vector2Int tile = sim.MapData.WorldToTile(unit.SimPosition);
                    if (sim.FogOfWar.GetVisibility(localPlayerId, tile.x, tile.y) != TileVisibility.Visible)
                        continue;
                }

                WorldToPixel(unit.SimPosition.x.ToFloat(), unit.SimPosition.z.ToFloat(), out int px, out int py);
                Color playerColor = GameSetup.PlayerColors[unit.PlayerId];
                Color32 c32 = playerColor;
                DrawDot(px, py, DotSize, c32);
            }
        }

        private void DrawWaypointLines(GameSimulation sim)
        {
            if (selectionManager == null) return;

            int[] unitIds = selectionManager.GetSelectedUnitIds();
            if (unitIds.Length == 0) return;

            for (int u = 0; u < unitIds.Length; u++)
            {
                var unit = sim.UnitRegistry.GetUnit(unitIds[u]);
                if (unit == null || unit.State == UnitState.Dead) continue;

                bool hasQueue = unit.HasQueuedCommands;
                bool hasPath = unit.HasPath;
                if (!hasQueue && !hasPath) continue;

                var queue = unit.CommandQueue;
                int queueCount = hasQueue ? queue.Count : 0;

                Color32 attackColor = new Color32(255, 77, 77, 255);
                Color32 moveColor = new Color32(255, 255, 102, 255);

                // Current segment color based on IsAttackMoving
                bool currentIsAttack = unit.IsAttackMoving || unit.CombatTargetId >= 0;
                Color32 currentColor = currentIsAttack ? attackColor : moveColor;

                WorldToPixel(unit.SimPosition.x.ToFloat(), unit.SimPosition.z.ToFloat(), out int prevX, out int prevY);

                if (hasPath)
                {
                    WorldToPixel(unit.FinalDestination.x.ToFloat(), unit.FinalDestination.z.ToFloat(), out int destX, out int destY);
                    DrawLine(prevX, prevY, destX, destY, currentColor);
                    prevX = destX;
                    prevY = destY;
                }

                for (int i = 0; i < queueCount; i++)
                {
                    int nextX, nextY;
                    if (queue[i].Type == QueuedCommandType.Construct)
                    {
                        var building = sim.BuildingRegistry.GetBuilding(queue[i].BuildingId);
                        if (building != null)
                        {
                            WorldToPixel(building.SimPosition.x.ToFloat(), building.SimPosition.z.ToFloat(), out nextX, out nextY);
                        }
                        else
                        {
                            nextX = prevX;
                            nextY = prevY;
                        }
                    }
                    else
                    {
                        WorldToPixel(queue[i].TargetPosition.x.ToFloat(), queue[i].TargetPosition.z.ToFloat(), out nextX, out nextY);
                    }

                    // Per-segment color based on queued command type
                    Color32 segColor = queue[i].Type == QueuedCommandType.AttackMove ? attackColor : moveColor;
                    DrawLine(prevX, prevY, nextX, nextY, segColor);
                    prevX = nextX;
                    prevY = nextY;
                }
            }
        }

        private void DrawRallyLines(GameSimulation sim)
        {
            if (selectionManager == null) return;

            var selectedBuildings = selectionManager.SelectedBuildings;
            if (selectedBuildings.Count == 0) return;

            for (int b = 0; b < selectedBuildings.Count; b++)
            {
                var buildingView = selectedBuildings[b];
                var buildingData = sim.BuildingRegistry.GetBuilding(buildingView.BuildingId);
                if (buildingData == null || !buildingData.HasRallyPoint) continue;

                // Resolve rally target position
                float targetX = buildingData.RallyPoint.x.ToFloat();
                float targetZ = buildingData.RallyPoint.z.ToFloat();
                bool isGreen = buildingData.RallyPointOnResource;

                if (buildingData.RallyPointUnitId >= 0)
                {
                    var targetUnit = sim.UnitRegistry.GetUnit(buildingData.RallyPointUnitId);
                    if (targetUnit != null && targetUnit.State != UnitState.Dead)
                    {
                        targetX = targetUnit.SimPosition.x.ToFloat();
                        targetZ = targetUnit.SimPosition.z.ToFloat();
                        if (targetUnit.IsSheep) isGreen = true;
                    }
                }

                Color32 lineColor = isGreen
                    ? new Color32(0, 255, 0, 255)
                    : new Color32(255, 255, 255, 255);

                WorldToPixel(buildingData.SimPosition.x.ToFloat(), buildingData.SimPosition.z.ToFloat(), out int fromX, out int fromY);
                WorldToPixel(targetX, targetZ, out int toX, out int toY);

                DrawLine(fromX, fromY, toX, toY, lineColor);
            }
        }

        private void DrawViewportRect()
        {
            if (mainCamera == null) return;

            Vector3[] screenCorners = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(Screen.width, 0, 0),
                new Vector3(Screen.width, Screen.height, 0),
                new Vector3(0, Screen.height, 0)
            };

            int[] px = new int[4];
            int[] py = new int[4];

            for (int i = 0; i < 4; i++)
            {
                Ray ray = mainCamera.ScreenPointToRay(screenCorners[i]);
                if (ray.direction.y >= 0) return;
                float t = -ray.origin.y / ray.direction.y;
                Vector3 wp = ray.origin + ray.direction * t;
                WorldToPixelUnclamped(wp.x, wp.z, out px[i], out py[i]);
            }

            Color32 white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                DrawLineClippedThick(px[i], py[i], px[j], py[j], white);
            }
        }

        // Bresenham line drawing (clipped to texture bounds)
        private void DrawLine(int x0, int y0, int x1, int y1, Color32 color)
        {
            // Cohen-Sutherland clip to 0..TexSize-1
            if (!ClipLine(ref x0, ref y0, ref x1, ref y1)) return;
            BresenhamLine(x0, y0, x1, y1, color);
        }

        private void DrawLineClipped(int x0, int y0, int x1, int y1, Color32 color)
        {
            if (!ClipLine(ref x0, ref y0, ref x1, ref y1)) return;
            BresenhamLine(x0, y0, x1, y1, color);
        }

        private void DrawLineClippedThick(int x0, int y0, int x1, int y1, Color32 color)
        {
            if (!ClipLine(ref x0, ref y0, ref x1, ref y1)) return;
            BresenhamLineThick(x0, y0, x1, y1, color);
        }

        private void BresenhamLine(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                if (x0 >= 0 && x0 < TexSize && y0 >= 0 && y0 < TexSize)
                    compositePixels[y0 * TexSize + x0] = color;

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void BresenhamLineThick(int x0, int y0, int x1, int y1, Color32 color)
        {
            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                // Set pixel + neighbors for 2px thickness
                SetPixelSafe(x0, y0, color);
                SetPixelSafe(x0 + 1, y0, color);
                SetPixelSafe(x0, y0 + 1, color);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void SetPixelSafe(int x, int y, Color32 color)
        {
            if (x >= 0 && x < TexSize && y >= 0 && y < TexSize)
                compositePixels[y * TexSize + x] = color;
        }

        // Cohen-Sutherland line clipping for pixel coordinates
        private static bool ClipLine(ref int x0, ref int y0, ref int x1, ref int y1)
        {
            const int xmin = 0, ymin = 0;
            const int xmax = TexSize - 1, ymax = TexSize - 1;

            int code0 = OutCode(x0, y0);
            int code1 = OutCode(x1, y1);

            while (true)
            {
                if ((code0 | code1) == 0) return true;
                if ((code0 & code1) != 0) return false;

                int codeOut = code0 != 0 ? code0 : code1;
                int x, y;
                int dx = x1 - x0;
                int dy = y1 - y0;

                if ((codeOut & 8) != 0) { y = ymax; x = dx != 0 ? x0 + dx * (ymax - y0) / dy : x0; }
                else if ((codeOut & 4) != 0) { y = ymin; x = dx != 0 ? x0 + dx * (ymin - y0) / dy : x0; }
                else if ((codeOut & 2) != 0) { x = xmax; y = dy != 0 ? y0 + dy * (xmax - x0) / dx : y0; }
                else { x = xmin; y = dy != 0 ? y0 + dy * (xmin - x0) / dx : y0; }

                if (codeOut == code0) { x0 = x; y0 = y; code0 = OutCode(x0, y0); }
                else { x1 = x; y1 = y; code1 = OutCode(x1, y1); }
            }
        }

        private static int OutCode(int x, int y)
        {
            int code = 0;
            if (x < 0) code |= 1;
            if (x > TexSize - 1) code |= 2;
            if (y < 0) code |= 4;
            if (y > TexSize - 1) code |= 8;
            return code;
        }

        // ---- Input ----

        private void HandleMinimapInput(Rect screenRect, bool insideCircle)
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (!insideCircle) return;
            Vector2 mousePos = VirtualCursor.Position;

            bool leftPressed = mouse.leftButton.wasPressedThisFrame;
            bool leftHeld = mouse.leftButton.isPressed;
            bool rightPressed = mouse.rightButton.wasPressedThisFrame;

            bool inAttackMoveMode = selectionManager != null && selectionManager.IsAttackMoveMode;

            if (inAttackMoveMode && (leftPressed || rightPressed))
            {
                Vector3 worldPos = ScreenToMinimapWorld(mousePos, screenRect);
                IssueMoveCommand(worldPos, true);
                selectionManager.ClearAttackMoveMode();
            }
            else if ((leftPressed || leftHeld) && !IsBoxDragging())
            {
                Vector3 worldPos = ScreenToMinimapWorld(mousePos, screenRect);
                cameraController.PivotPosition = worldPos;
            }
            else if (rightPressed)
            {
                Vector3 worldPos = ScreenToMinimapWorld(mousePos, screenRect);
                IssueMoveCommand(worldPos, false);
            }
        }

        private bool IsBoxDragging()
        {
            return selectionManager != null && selectionManager.IsDragging;
        }

        private Vector3 ScreenToMinimapWorld(Vector2 screenPos, Rect screenRect)
        {
            float u = (screenPos.x - screenRect.x) / screenRect.width;
            float v = (screenPos.y - screenRect.y) / screenRect.height;

            int px = Mathf.RoundToInt(u * (TexSize - 1));
            int py = Mathf.RoundToInt(v * (TexSize - 1));
            PixelToWorld(px, py, out float worldX, out float worldZ);
            return new Vector3(worldX, 0f, worldZ);
        }

        private void IssueMoveCommand(Vector3 worldPos, bool isAttackMove = false)
        {
            if (selectionManager == null) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int[] unitIds = selectionManager.GetSelectedUnitIds();
            if (unitIds.Length > 0)
            {
                FixedVector3 fixedTarget = FixedVector3.FromVector3(worldPos);

                var positions = GameSetup.ComputeGridFormation(worldPos, unitIds.Length);
                var fixedPositions = new FixedVector3[positions.Count];
                for (int i = 0; i < positions.Count; i++)
                    fixedPositions[i] = FixedVector3.FromVector3(positions[i]);

                int playerId = selectionManager.LocalPlayerId;
                var moveCmd = new MoveCommand(playerId, unitIds, fixedTarget, fixedPositions);
                var keyboard = Keyboard.current;
                moveCmd.IsQueued = keyboard != null && keyboard.shiftKey.isPressed;
                moveCmd.IsAttackMove = isAttackMove;
                sim.CommandBuffer.EnqueueCommand(moveCmd);
                return;
            }

            // Rally point: right-click minimap with own buildings selected
            var selectedBuildings = selectionManager.SelectedBuildings;
            if (selectedBuildings.Count > 0 && selectedBuildings[0].PlayerId == selectionManager.LocalPlayerId)
            {
                FixedVector3 fixedPos = FixedVector3.FromVector3(worldPos);
                for (int i = 0; i < selectedBuildings.Count; i++)
                {
                    sim.CommandBuffer.EnqueueCommand(new SetRallyPointCommand(
                        selectionManager.LocalPlayerId, selectedBuildings[i].BuildingId, fixedPos));
                }
            }
        }

        private void ComputeViewportQuad()
        {
            viewportQuadValid = false;
            if (mainCamera == null) return;

            Vector3[] screenCorners = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(Screen.width, 0, 0),
                new Vector3(Screen.width, Screen.height, 0),
                new Vector3(0, Screen.height, 0)
            };

            for (int i = 0; i < 4; i++)
            {
                Ray ray = mainCamera.ScreenPointToRay(screenCorners[i]);
                if (ray.direction.y >= 0) return;
                float t = -ray.origin.y / ray.direction.y;
                viewportGroundCorners[i] = ray.origin + ray.direction * t;
            }
            viewportQuadValid = true;
        }

        private bool IsInsideViewport(float worldX, float worldZ)
        {
            if (!viewportQuadValid) return false;

            // Point-in-convex-quad via cross products (all same sign = inside)
            float Sign(Vector3 a, Vector3 b, float px, float pz)
            {
                return (b.x - a.x) * (pz - a.z) - (b.z - a.z) * (px - a.x);
            }

            bool allPos = true, allNeg = true;
            for (int i = 0; i < 4; i++)
            {
                float s = Sign(viewportGroundCorners[i], viewportGroundCorners[(i + 1) % 4], worldX, worldZ);
                if (s < 0) allPos = false;
                if (s > 0) allNeg = false;
            }
            return allPos || allNeg;
        }

        private void CheckForAttacks()
        {
            if (Time.time - lastAttackAlertTime < AttackAlertCooldown) return;

            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;

            int localPlayerId = selectionManager != null ? selectionManager.LocalPlayerId : 0;
            int currentTick = sim.CurrentTick;
            if (currentTick <= lastAlertCheckTick) return;

            ComputeViewportQuad();

            float alertX = 0f, alertZ = 0f;
            bool found = false;

            // Check units
            var units = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.PlayerId != localPlayerId) continue;
                if (unit.State == UnitState.Dead) continue;
                if (unit.LastDamageTick <= lastAlertCheckTick || unit.LastDamageTick <= 0) continue;

                float wx = unit.SimPosition.x.ToFloat();
                float wz = unit.SimPosition.z.ToFloat();
                if (!IsInsideViewport(wx, wz))
                {
                    alertX = wx;
                    alertZ = wz;
                    found = true;
                    break;
                }
            }

            // Check buildings
            if (!found)
            {
                var buildings = sim.BuildingRegistry.GetAllBuildings();
                for (int i = 0; i < buildings.Count; i++)
                {
                    var building = buildings[i];
                    if (building.PlayerId != localPlayerId) continue;
                    if (building.IsDestroyed) continue;
                    if (building.LastDamageTick <= lastAlertCheckTick || building.LastDamageTick <= 0) continue;

                    float wx = building.SimPosition.x.ToFloat();
                    float wz = building.SimPosition.z.ToFloat();
                    if (!IsInsideViewport(wx, wz))
                    {
                        alertX = wx;
                        alertZ = wz;
                        found = true;
                        break;
                    }
                }
            }

            lastAlertCheckTick = currentTick;

            if (found)
            {
                lastAttackAlertTime = Time.time;
                activePings.Add(new MinimapPing { worldX = alertX, worldZ = alertZ, timeRemaining = PingDuration });
                SFXManager.Instance?.PlayUI(SFXType.UnderAttack);
            }
        }

        private void UpdateAndDrawPings()
        {
            // Update lifetimes and remove expired
            for (int i = activePings.Count - 1; i >= 0; i--)
            {
                var ping = activePings[i];
                ping.timeRemaining -= Time.deltaTime;
                if (ping.timeRemaining <= 0f)
                {
                    activePings.RemoveAt(i);
                    continue;
                }
                activePings[i] = ping;
            }

            // Draw active pings
            for (int i = 0; i < activePings.Count; i++)
            {
                var ping = activePings[i];

                // Flash on/off at PingFlashRate Hz
                float phase = Mathf.Sin(2f * Mathf.PI * PingFlashRate * (PingDuration - ping.timeRemaining));
                if (phase <= 0f) continue;

                WorldToPixel(ping.worldX, ping.worldZ, out int cx, out int cy);

                // Pulsing radius 4..8 px
                float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f * (PingDuration - ping.timeRemaining));
                int radius = (int)Mathf.Lerp(4f, 8f, pulse);

                // Draw circle as line segments
                Color32 pingColor = new Color32(255, 60, 30, 255);
                const int segments = 16;
                for (int s = 0; s < segments; s++)
                {
                    float a0 = 2f * Mathf.PI * s / segments;
                    float a1 = 2f * Mathf.PI * (s + 1) / segments;
                    int x0 = cx + (int)(Mathf.Cos(a0) * radius);
                    int y0 = cy + (int)(Mathf.Sin(a0) * radius);
                    int x1 = cx + (int)(Mathf.Cos(a1) * radius);
                    int y1 = cy + (int)(Mathf.Sin(a1) * radius);
                    DrawLineClipped(x0, y0, x1, y1, pingColor);
                }
            }
        }

        private void OnDestroy()
        {
            if (compositeTexture != null) Destroy(compositeTexture);
        }
    }
}
