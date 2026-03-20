using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public class UnitView : MonoBehaviour
    {
        public int UnitId { get; private set; }
        public int UnitType { get; private set; }
        public bool IsDead { get; private set; }

        [SerializeField] private GameObject selectionRing;
        [SerializeField] private float turnSmoothing = 12f;

        private UnitData unitData;
        private MapData cachedMapData;
        private float cachedHeightScale;
        private bool isSelected;
        private bool isPreselected;
        private bool isHovered;

        // Stencil + silhouette materials for resource stack rendering
        private Material cachedStencilMat;
        private Material cachedSilhouetteMat;

        // Control group badge
        private int controlGroupLabel = -1;
        public void SetControlGroup(int index) { controlGroupLabel = index; }

        // Cached Camera.main (avoids FindGameObjectWithTag per entity per frame)
        private static Camera s_cachedCamera;
        private static int s_cachedCameraFrame = -1;
        public static Camera CachedMainCamera
        {
            get
            {
                int frame = Time.frameCount;
                if (s_cachedCameraFrame != frame)
                {
                    s_cachedCamera = Camera.main;
                    s_cachedCameraFrame = frame;
                }
                return s_cachedCamera;
            }
        }

        // Screen-space health bar (Canvas)
        private static readonly Color HealthColorFull = new Color(0.2f, 0.8f, 0.2f);
        private static readonly Color HealthColorEmpty = new Color(0.8f, 0.1f, 0.1f);
        private const float HealthBarWidth = 40f;
        private const float HealthBarHeight = 4f;
        private const float HealthBarYOffset = 1.3f;
        private float healthBarYOffset;
        private RectTransform healthBarRoot;
        private Image healthBarFill;
        private RectTransform healthBarFillRT;
        private TextMeshProUGUI groupLabelTMP;

        // Attack dash
        private int lastSeenAttackTick;
        private float attackDashTimer;
        private Vector3 attackDashDir;
        private const float AttackDashDuration = 0.18f;
        private const float AttackDashDistance = 0.05f;

        // Damage flinch
        private int lastSeenDamageTick;
        private float damageFlinchTimer;
        private Vector3 damageFlinchDir;
        private const float DamageFlinchDuration = 0.22f;
        private const float DamageFlinchDistance = 0.04f;

        // Knockback arc (shared by meteor, charge, etc.)
        private bool meteorKnockbackActive;
        private Vector3 meteorKnockbackStart;
        private Vector3 meteorKnockbackEnd;
        private float meteorKnockbackTimer;
        private float knockbackDuration;
        private float knockbackArcHeight;
        private const float MeteorKnockbackDuration = 0.5f;
        private const float MeteorKnockbackArcHeight = 3f;
        private const float ChargeKnockupDuration = 0.6f;
        private const float ChargeKnockupArcHeight = 1.5f;

        // Charge knockup detection
        private int lastSeenChargeHitTick;

        // Damage flash
        private float damageFlashTimer;
        private const float DamageFlashDuration = 0.18f;
        private Renderer[] bodyRenderers;
        private Color[] originalColors;
        private bool flashActive;

        // Resource stack on villager's back
        private Transform resourceStackContainer;
        private GameObject[] resourceStackItems;
        private const int ResourceStackMaxItems = 10;
        private int currentVisibleStackItems;
        private ResourceType currentStackResourceType = (ResourceType)(-1);
        private static Material[] s_resourceMaterials;

        private static readonly Vector3[] StackOffsets = new Vector3[]
        {
            new Vector3(0f,     0f,      0f),
            new Vector3(0.008f, 0.09f,   0.005f),
            new Vector3(-0.006f,0.18f,  -0.005f),
            new Vector3(0.01f,  0.27f,   0.003f),
            new Vector3(-0.004f,0.36f,  -0.008f),
            new Vector3(0.006f, 0.45f,   0.005f),
            new Vector3(-0.01f, 0.54f,  -0.003f),
            new Vector3(0.004f, 0.63f,   0.008f),
            new Vector3(-0.008f,0.72f,  -0.005f),
            new Vector3(0.01f,  0.81f,   0.003f),
        };

        // Command confirmation flash (attack)
        private float commandFlashTimer;
        private const float CommandFlashDuration = 0.18f;
        private static readonly Color AttackFlashColor = new Color(1f, 0.2f, 0.2f);

        // Attack target ring pulsation
        private float attackRingTimer;
        private const float AttackRingDuration = 0.5f;
        private static readonly Color AttackRingColor = new Color(1f, 0.15f, 0.15f);
        private Renderer selectionRingRenderer;
        private MaterialPropertyBlock ringPropBlock;
        private Color originalRingColor = Color.white;

        // Shader-safe color access (RGBRecolor shader uses _BaseColor instead of _Color)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static Color GetMaterialColor(Material mat)
        {
            if (mat == null) return Color.white;
            if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId)) return mat.color;
            return Color.white;
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.color = color;
        }

        // Smoothed base position (excludes combat offsets to prevent feedback loop)
        private Vector3 smoothedBasePos;

        // Waypoint queue line visualization
        private LineRenderer waypointLine;
        private List<LineRenderer> extraWaypointLines;
        private const float WaypointLineWidth = 0.08f;
        private const float WaypointLineYOffset = 0.1f;
        private static readonly Color WaypointLineColor = new Color(1f, 1f, 0.4f, 0.5f);
        private static readonly Color AttackMoveLineColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        // Resource deposit floating indicator (screen-space UI)
        private int lastSeenDepositTick;
        private const int IndicatorPoolSize = 3;
        private const float IndicatorDuration = 1.5f;
        private const float IndicatorRisePixels = 60f;
        private const float IndicatorYOffsetPixels = 80f;
        private static Canvas sharedIndicatorCanvas;
        private RectTransform[] indicatorRects;
        private Text[] indicatorTexts;
        private UnityEngine.UI.Image[] indicatorIconImages;
        private float[] indicatorTimers;
        private int nextIndicatorIndex;

        // Monk arm animation
        private Transform leftArm;
        private Transform rightArm;
        private Quaternion leftArmRestRotation;
        private Quaternion rightArmRestRotation;

        // Heal indicator (green + signs)
        private int lastSeenHealTick;
        private const int HealIndicatorPoolSize = 3;
        private const float HealIndicatorDuration = 1.0f;
        private const float HealIndicatorRisePixels = 50f;
        private const float HealIndicatorYOffsetPixels = 80f;
        private RectTransform[] healIndicatorRects;
        private Text[] healIndicatorTexts;
        private float[] healIndicatorTimers;
        private int nextHealIndicatorIndex;

        // Idle "zzz" visual effect
        private GameObject idleZzzContainer;
        private GameObject[] zzzTexts;
        private float idleTimer;
        private bool showingIdleEffect;
        private const float IdleDelayTime = 3f; // Show zzz after 3 seconds of being idle
        private const float ZzzAnimationSpeed = 1f;
        private const float ZzzYOffset = 1.2f;
        private const int ZzzCount = 3;

        public void Initialize(int unitId, Vector3 startPos, UnitData data, int unitType = 0, MapData mapData = null, float heightScale = 0f,
            Material stencilMat = null, Material silhouetteMat = null)
        {
            UnitId = unitId;
            UnitType = unitType;
            unitData = data;
            cachedMapData = mapData;
            cachedHeightScale = heightScale;
            cachedStencilMat = stencilMat;
            cachedSilhouetteMat = silhouetteMat;
            transform.position = startPos;
            smoothedBasePos = startPos;

            // Apply initial facing
            Vector3 facing = data.SimFacing.ToVector3();
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facing);

            CreateWaypointLine();
            CreateIdleZzzEffect();
            CreateDepositIndicatorPool();
            CreateHealIndicatorPool();
            CreateHealthBarWidget();
            var col = GetComponent<Collider>();
            healthBarYOffset = col != null
                ? col.bounds.max.y - transform.position.y + 0.1f
                : HealthBarYOffset;
            if (UnitType == 5) // Sheep — raise health bar to match small model
                healthBarYOffset = 0.8f;
            if (UnitType == 9) // Monk — match standard unit height
            {
                healthBarYOffset = HealthBarYOffset;
                leftArm = transform.Find("LeftArm");
                rightArm = transform.Find("RightArm");
                if (leftArm != null) leftArmRestRotation = leftArm.localRotation;
                if (rightArm != null) rightArmRestRotation = rightArm.localRotation;
            }
            if (UnitType == 0) CreateResourceStack();
            SetSelected(false);
            CacheRenderers();
        }

        private void CacheRenderers()
        {
            // Cache body renderers (exclude selection ring)
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (selectionRing != null && allRenderers[i].transform.IsChildOf(selectionRing.transform))
                    continue;
                if (waypointLine != null && allRenderers[i] == waypointLine)
                    continue;
                if (idleZzzContainer != null && allRenderers[i].transform.IsChildOf(idleZzzContainer.transform))
                    continue;
                if (resourceStackContainer != null && allRenderers[i].transform.IsChildOf(resourceStackContainer))
                    continue;
                count++;
            }
            bodyRenderers = new Renderer[count];
            originalColors = new Color[count];
            int idx = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (selectionRing != null && allRenderers[i].transform.IsChildOf(selectionRing.transform))
                    continue;
                if (waypointLine != null && allRenderers[i] == waypointLine)
                    continue;
                if (idleZzzContainer != null && allRenderers[i].transform.IsChildOf(idleZzzContainer.transform))
                    continue;
                if (resourceStackContainer != null && allRenderers[i].transform.IsChildOf(resourceStackContainer))
                    continue;
                bodyRenderers[idx] = allRenderers[i];
                originalColors[idx] = GetMaterialColor(allRenderers[i].sharedMaterial);
                idx++;
            }
        }

        private static void EnsureResourceMaterials()
        {
            if (s_resourceMaterials != null) return;
            s_resourceMaterials = new Material[4];
            Color[] colors = new Color[]
            {
                new Color(0.8f, 0.15f, 0.1f),  // Food — red
                new Color(0.55f, 0.35f, 0.15f), // Wood — brown
                new Color(0.9f, 0.75f, 0.1f),   // Gold — yellow
                new Color(0.6f, 0.6f, 0.6f),    // Stone — gray
            };
            for (int i = 0; i < 4; i++)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.color = colors[i];
                if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, colors[i]);
                s_resourceMaterials[i] = mat;
            }
        }

        private static Vector3 GetScaleForResourceType(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood:  return new Vector3(0.35f, 0.08f, 0.08f);
                case ResourceType.Food:  return new Vector3(0.18f, 0.15f, 0.18f);
                case ResourceType.Gold:  return new Vector3(0.15f, 0.15f, 0.15f);
                case ResourceType.Stone: return new Vector3(0.20f, 0.12f, 0.18f);
                default:                 return new Vector3(0.15f, 0.15f, 0.15f);
            }
        }

        private void CreateResourceStack()
        {
            var containerObj = new GameObject("ResourceStack");
            resourceStackContainer = containerObj.transform;
            resourceStackContainer.SetParent(transform, false);
            resourceStackContainer.localPosition = new Vector3(0f, 0.45f, -0.25f);

            resourceStackItems = new GameObject[ResourceStackMaxItems];
            for (int i = 0; i < ResourceStackMaxItems; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(cube.GetComponent<Collider>());
                cube.transform.SetParent(resourceStackContainer, false);
                cube.transform.localPosition = StackOffsets[i];
                cube.SetActive(false);
                resourceStackItems[i] = cube;
            }
            containerObj.SetActive(false);
        }

        private void UpdateResourceStack()
        {
            if (resourceStackContainer == null) return;

            int amount = unitData != null ? unitData.CarriedResourceAmount : 0;
            if (amount <= 0)
            {
                if (resourceStackContainer.gameObject.activeSelf)
                    resourceStackContainer.gameObject.SetActive(false);
                currentVisibleStackItems = 0;
                currentStackResourceType = (ResourceType)(-1);
                return;
            }

            ResourceType resType = unitData.CarriedResourceType;
            int itemsToShow = amount;
            if (itemsToShow > ResourceStackMaxItems) itemsToShow = ResourceStackMaxItems;

            if (itemsToShow != currentVisibleStackItems || resType != currentStackResourceType)
            {
                EnsureResourceMaterials();
                Vector3 scale = GetScaleForResourceType(resType);
                Material mat = s_resourceMaterials[(int)resType];

                for (int i = 0; i < ResourceStackMaxItems; i++)
                {
                    bool show = i < itemsToShow;
                    resourceStackItems[i].SetActive(show);
                    if (show)
                    {
                        resourceStackItems[i].transform.localScale = scale;
                        var r = resourceStackItems[i].GetComponent<Renderer>();
                        if (cachedStencilMat != null && cachedSilhouetteMat != null)
                            r.sharedMaterials = new Material[] { mat, cachedStencilMat, cachedSilhouetteMat };
                        else
                            r.sharedMaterial = mat;
                    }
                }

                currentVisibleStackItems = itemsToShow;
                currentStackResourceType = resType;
            }

            if (!resourceStackContainer.gameObject.activeSelf)
                resourceStackContainer.gameObject.SetActive(true);
        }

        private void CreateWaypointLine()
        {
            waypointLine = CreateWaypointLineRenderer("WaypointLine", WaypointLineColor);
            waypointLine.gameObject.SetActive(false);
            extraWaypointLines = new List<LineRenderer>();
        }

        private LineRenderer CreateWaypointLineRenderer(string name, Color color)
        {
            var lineObj = new GameObject(name);
            lineObj.transform.SetParent(transform, false);
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = WaypointLineWidth;
            lr.endWidth = WaypointLineWidth;
            lr.positionCount = 0;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            SetMaterialColor(mat, color);
            lr.material = mat;
            lr.startColor = color;
            lr.endColor = color;

            return lr;
        }

        private LineRenderer GetOrCreateExtraLine(int index)
        {
            while (extraWaypointLines.Count <= index)
            {
                var lr = CreateWaypointLineRenderer("WaypointLineExtra", WaypointLineColor);
                lr.gameObject.SetActive(false);
                extraWaypointLines.Add(lr);
            }
            return extraWaypointLines[index];
        }

        private void SetLineColor(LineRenderer lr, Color color)
        {
            lr.startColor = color;
            lr.endColor = color;
            SetMaterialColor(lr.material, color);
        }

        private void CreateIdleZzzEffect()
        {
            idleZzzContainer = new GameObject("IdleZzzContainer");
            idleZzzContainer.transform.SetParent(transform, false);
            idleZzzContainer.transform.localPosition = new Vector3(0f, ZzzYOffset, 0f);

            zzzTexts = new GameObject[ZzzCount];
            for (int i = 0; i < ZzzCount; i++)
            {
                var zzzObj = new GameObject($"Zzz_{i}");
                zzzObj.transform.SetParent(idleZzzContainer.transform, false);
                
                // Create a 3D text mesh
                var textMesh = zzzObj.AddComponent<TextMesh>();
                textMesh.text = "z";
                textMesh.fontSize = 20; // Much smaller font size
                textMesh.color = new Color(1f, 1f, 1f, 0.7f); // Semi-transparent white
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;

                // All z's start at the same bottom position
                zzzObj.transform.localPosition = Vector3.zero;

                // Much smaller overall scale
                float scale = 0.3f - i * 0.05f; // Start smaller and get even smaller
                zzzObj.transform.localScale = Vector3.one * scale;

                zzzTexts[i] = zzzObj;
            }

            idleZzzContainer.SetActive(false);
        }

        private static void EnsureIndicatorCanvas()
        {
            if (sharedIndicatorCanvas != null) return;
            var canvasObj = new GameObject("DepositIndicatorCanvas");
            Object.DontDestroyOnLoad(canvasObj);
            sharedIndicatorCanvas = canvasObj.AddComponent<Canvas>();
            sharedIndicatorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            sharedIndicatorCanvas.sortingOrder = 4;
        }

        private void CreateDepositIndicatorPool()
        {
            if (unitData == null || !unitData.IsVillager) return;

            ResourceIcons.EnsureLoaded();
            EnsureIndicatorCanvas();

            indicatorRects = new RectTransform[IndicatorPoolSize];
            indicatorTexts = new Text[IndicatorPoolSize];
            indicatorIconImages = new UnityEngine.UI.Image[IndicatorPoolSize];
            indicatorTimers = new float[IndicatorPoolSize];

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < IndicatorPoolSize; i++)
            {
                var obj = new GameObject($"DepositIndicator_{UnitId}_{i}");
                obj.transform.SetParent(sharedIndicatorCanvas.transform, false);
                var rect = obj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(120, 32);

                // Text child (left side)
                var textObj = new GameObject("Text");
                textObj.transform.SetParent(obj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchoredPosition = new Vector2(-20f, 0f);
                textRect.sizeDelta = new Vector2(60, 32);
                var text = textObj.AddComponent<Text>();
                text.font = font;
                text.fontSize = 18;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleRight;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                var outline = textObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);

                // Icon child (right side, with gap)
                var iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(obj.transform, false);
                var iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchoredPosition = new Vector2(24f, 0f);
                iconRect.sizeDelta = new Vector2(28, 28);
                var img = iconObj.AddComponent<UnityEngine.UI.Image>();
                img.preserveAspect = true;

                indicatorRects[i] = rect;
                indicatorTexts[i] = text;
                indicatorIconImages[i] = img;
                indicatorTimers[i] = -1f;
                obj.SetActive(false);
            }
        }

        private void CreateHealIndicatorPool()
        {
            EnsureIndicatorCanvas();

            healIndicatorRects = new RectTransform[HealIndicatorPoolSize];
            healIndicatorTexts = new Text[HealIndicatorPoolSize];
            healIndicatorTimers = new float[HealIndicatorPoolSize];

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < HealIndicatorPoolSize; i++)
            {
                var obj = new GameObject($"HealIndicator_{UnitId}_{i}");
                obj.transform.SetParent(sharedIndicatorCanvas.transform, false);
                var rect = obj.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(60, 32);

                var textObj = new GameObject("Text");
                textObj.transform.SetParent(obj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchoredPosition = Vector2.zero;
                textRect.sizeDelta = new Vector2(60, 32);
                var text = textObj.AddComponent<Text>();
                text.font = font;
                text.fontSize = 22;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.color = new Color(0.2f, 1f, 0.2f);
                var outline = textObj.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0.3f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);

                healIndicatorRects[i] = rect;
                healIndicatorTexts[i] = text;
                healIndicatorTimers[i] = -1f;
                obj.SetActive(false);
            }
        }

        private void SpawnHealIndicator(int amount)
        {
            if (healIndicatorRects == null) return;

            int idx = nextHealIndicatorIndex;
            nextHealIndicatorIndex = (nextHealIndicatorIndex + 1) % HealIndicatorPoolSize;

            healIndicatorTexts[idx].text = $"+{amount}";
            healIndicatorTexts[idx].color = new Color(0.2f, 1f, 0.2f);
            healIndicatorTimers[idx] = 0f;
            healIndicatorRects[idx].gameObject.SetActive(true);
        }

        private void UpdateHealIndicators()
        {
            if (healIndicatorRects == null) return;

            Camera mainCam = UnitView.CachedMainCamera;
            if (mainCam == null) return;

            Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position);
            bool behindCamera = screenPos.z < 0f;

            for (int i = 0; i < HealIndicatorPoolSize; i++)
            {
                if (healIndicatorTimers[i] < 0f) continue;

                healIndicatorTimers[i] += Time.deltaTime;

                if (healIndicatorTimers[i] >= HealIndicatorDuration)
                {
                    healIndicatorRects[i].gameObject.SetActive(false);
                    healIndicatorTimers[i] = -1f;
                    continue;
                }

                if (behindCamera)
                {
                    healIndicatorRects[i].gameObject.SetActive(false);
                    continue;
                }
                healIndicatorRects[i].gameObject.SetActive(true);

                float progress = healIndicatorTimers[i] / HealIndicatorDuration;
                float yOffset = HealIndicatorYOffsetPixels + HealIndicatorRisePixels * progress;
                healIndicatorRects[i].position = new Vector3(screenPos.x, screenPos.y + yOffset, 0f);

                float alpha = progress < 0.6f ? 1f : 1f - (progress - 0.6f) / 0.4f;
                healIndicatorTexts[i].color = new Color(0.2f, 1f, 0.2f, alpha);
            }
        }

        private void SpawnDepositIndicator(int amount, ResourceType type)
        {
            if (indicatorRects == null) return;

            int idx = nextIndicatorIndex;
            nextIndicatorIndex = (nextIndicatorIndex + 1) % IndicatorPoolSize;

            indicatorTexts[idx].text = $"+{amount}";
            indicatorTexts[idx].color = Color.white;
            indicatorIconImages[idx].sprite = ResourceIcons.Get(type);
            indicatorIconImages[idx].color = Color.white;
            indicatorTimers[idx] = 0f;
            indicatorRects[idx].gameObject.SetActive(true);
        }

        private void UpdateDepositIndicators()
        {
            if (indicatorRects == null) return;

            Camera mainCam = UnitView.CachedMainCamera;
            if (mainCam == null) return;

            Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position);
            bool behindCamera = screenPos.z < 0f;

            for (int i = 0; i < IndicatorPoolSize; i++)
            {
                if (indicatorTimers[i] < 0f) continue;

                indicatorTimers[i] += Time.deltaTime;

                if (indicatorTimers[i] >= IndicatorDuration)
                {
                    indicatorRects[i].gameObject.SetActive(false);
                    indicatorTimers[i] = -1f;
                    continue;
                }

                if (behindCamera)
                {
                    indicatorRects[i].gameObject.SetActive(false);
                    continue;
                }
                indicatorRects[i].gameObject.SetActive(true);

                float progress = indicatorTimers[i] / IndicatorDuration;

                // Position at unit's screen pos, rising in pixels
                float yOffset = IndicatorYOffsetPixels + IndicatorRisePixels * progress;
                indicatorRects[i].position = new Vector3(screenPos.x, screenPos.y + yOffset, 0f);

                // Fade: fully opaque first 60%, linear fade over last 40%
                float alpha;
                if (progress < 0.6f)
                    alpha = 1f;
                else
                    alpha = 1f - (progress - 0.6f) / 0.4f;

                indicatorTexts[i].color = new Color(1f, 1f, 1f, alpha);

                Color ic = indicatorIconImages[i].color;
                ic.a = alpha;
                indicatorIconImages[i].color = ic;
            }
        }

        private void DestroyIndicatorPool()
        {
            if (indicatorRects == null) return;
            for (int i = 0; i < IndicatorPoolSize; i++)
            {
                if (indicatorRects[i] != null)
                    Destroy(indicatorRects[i].gameObject);
            }
            indicatorRects = null;
        }

        private void CreateHealthBarWidget()
        {
            WorldOverlayCanvas.EnsureCreated();

            var rootGO = new GameObject($"HealthBar_{UnitId}");
            rootGO.transform.SetParent(WorldOverlayCanvas.Instance.transform, false);
            healthBarRoot = rootGO.AddComponent<RectTransform>();
            healthBarRoot.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(rootGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f);
            var bgOutline = bgGO.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            bgOutline.effectDistance = new Vector2(1, -1);

            // Fill
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(rootGO.transform, false);
            healthBarFillRT = fillGO.AddComponent<RectTransform>();
            healthBarFillRT.anchorMin = Vector2.zero;
            healthBarFillRT.anchorMax = Vector2.one;
            healthBarFillRT.offsetMin = Vector2.zero;
            healthBarFillRT.offsetMax = Vector2.zero;
            healthBarFill = fillGO.AddComponent<Image>();
            healthBarFill.color = HealthColorFull;

            // Group label
            var labelGO = new GameObject("GroupLabel");
            labelGO.transform.SetParent(rootGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(18f, 13f);
            labelRT.anchoredPosition = new Vector2(0f, HealthBarHeight * 0.5f + 3f + 13f * 0.5f);
            groupLabelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            groupLabelTMP.fontSize = 11f;
            groupLabelTMP.fontStyle = FontStyles.Bold;
            groupLabelTMP.color = Color.white;
            groupLabelTMP.alignment = TextAlignmentOptions.Center;
            groupLabelTMP.raycastTarget = false;
            labelGO.SetActive(false);

            rootGO.SetActive(false);
        }

        private void UpdateHealthBarUI()
        {
            if (IsDead || unitData == null || unitData.MaxHealth <= 0 || unitData.CurrentHealth <= 0)
            {
                if (healthBarRoot != null && healthBarRoot.gameObject.activeSelf)
                    healthBarRoot.gameObject.SetActive(false);
                return;
            }

            bool damaged = unitData.CurrentHealth < unitData.MaxHealth;
            if (!isSelected && !isHovered && !damaged)
            {
                if (healthBarRoot != null && healthBarRoot.gameObject.activeSelf)
                    healthBarRoot.gameObject.SetActive(false);
                return;
            }

            Camera cam = UnitView.CachedMainCamera;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * healthBarYOffset;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0f)
            {
                if (healthBarRoot.gameObject.activeSelf)
                    healthBarRoot.gameObject.SetActive(false);
                return;
            }

            if (!healthBarRoot.gameObject.activeSelf)
                healthBarRoot.gameObject.SetActive(true);

            healthBarRoot.position = new Vector3(screenPos.x, screenPos.y, 0f);

            float fraction = Mathf.Clamp01((float)unitData.CurrentHealth / unitData.MaxHealth);
            healthBarFillRT.anchorMax = new Vector2(fraction, 1f);
            healthBarFill.color = Color.Lerp(HealthColorEmpty, HealthColorFull, fraction);

            bool showLabel = controlGroupLabel >= 0 && isSelected;
            if (groupLabelTMP.gameObject.activeSelf != showLabel)
                groupLabelTMP.gameObject.SetActive(showLabel);
            if (showLabel)
                groupLabelTMP.text = controlGroupLabel.ToString();
        }

        private void LateUpdate()
        {
            UpdateHealthBarUI();
        }

        private Vector3 WaypointYAdjust(Vector3 pos)
        {
            if (cachedMapData != null)
                pos.y = cachedMapData.SampleHeight(pos.x, pos.z) * cachedHeightScale + WaypointLineYOffset;
            else
                pos.y = WaypointLineYOffset;
            return pos;
        }

        private void UpdateWaypointLine()
        {
            if (waypointLine == null || unitData == null) return;

            bool hasQueue = unitData.HasQueuedCommands;
            bool hasPath = unitData.HasPath;

            if (!isSelected || (!hasQueue && !hasPath))
            {
                if (waypointLine.gameObject.activeSelf)
                    waypointLine.gameObject.SetActive(false);
                for (int i = 0; i < extraWaypointLines.Count; i++)
                    if (extraWaypointLines[i].gameObject.activeSelf)
                        extraWaypointLines[i].gameObject.SetActive(false);
                return;
            }

            var queue = unitData.CommandQueue;
            int queueCount = hasQueue ? queue.Count : 0;

            // Compute all waypoint positions
            Vector3 unitPos = WaypointYAdjust(unitData.SimPosition.ToVector3());

            Vector3 currentDest = default;
            if (hasPath)
                currentDest = WaypointYAdjust(unitData.FinalDestination.ToVector3());

            // Current segment: unit pos → current destination (uses primary LineRenderer)
            bool hasCombatTarget = unitData.CombatTargetId >= 0;
            bool currentIsAttack = unitData.IsAttackMoving || hasCombatTarget;
            Color currentColor = currentIsAttack ? AttackMoveLineColor : WaypointLineColor;

            if (hasPath)
            {
                waypointLine.gameObject.SetActive(true);
                waypointLine.positionCount = 2;
                waypointLine.SetPosition(0, unitPos);
                waypointLine.SetPosition(1, currentDest);
                SetLineColor(waypointLine, currentColor);
            }
            else if (queueCount > 0)
            {
                // No active path but have queue — first queue segment starts from unit pos
                waypointLine.gameObject.SetActive(false);
            }
            else
            {
                waypointLine.gameObject.SetActive(false);
            }

            // Queue segments: one LineRenderer per segment
            Vector3 prevPos = hasPath ? currentDest : unitPos;
            int extraIdx = 0;
            for (int i = 0; i < queueCount; i++)
            {
                Vector3 pos;
                if (queue[i].Type == QueuedCommandType.Construct)
                {
                    var sim = GameBootstrapper.Instance?.Simulation;
                    var building = sim?.BuildingRegistry.GetBuilding(queue[i].BuildingId);
                    pos = building != null ? building.SimPosition.ToVector3() : unitPos;
                }
                else
                {
                    pos = queue[i].TargetPosition.ToVector3();
                }
                pos = WaypointYAdjust(pos);

                bool segIsAttack = queue[i].Type == QueuedCommandType.AttackMove;
                Color segColor = segIsAttack ? AttackMoveLineColor : WaypointLineColor;

                var lr = GetOrCreateExtraLine(extraIdx);
                lr.gameObject.SetActive(true);
                lr.positionCount = 2;
                lr.SetPosition(0, prevPos);
                lr.SetPosition(1, pos);
                SetLineColor(lr, segColor);

                prevPos = pos;
                extraIdx++;
            }

            // Hide unused extra lines
            for (int i = extraIdx; i < extraWaypointLines.Count; i++)
                if (extraWaypointLines[i].gameObject.activeSelf)
                    extraWaypointLines[i].gameObject.SetActive(false);
        }

        private void Update()
        {
            if (IsDead || unitData == null) return;
            if (GameBootstrapper.Instance == null) return;

            // Convert fixed-point sim positions to float at the render boundary
            float alpha = GameBootstrapper.Instance.InterpolationAlpha;
            Vector3 prev = unitData.PreviousSimPosition.ToVector3();
            Vector3 curr = unitData.SimPosition.ToVector3();

            // Tick-aligned interpolation as target position
            Vector3 tickPos = Vector3.Lerp(prev, curr, alpha);

            // Smooth toward tick position to absorb network tick bunching in multiplayer
            // Uses exponential smoothing (Lerp) instead of linear MoveTowards:
            // - Catches up faster when far behind (after tick bursts)
            // - Slows down smoothly when close to target
            // - Result: smoother visual movement that absorbs lockstep jitter
            const float smoothSpeed = 15f; // Higher = faster catch-up, lower = smoother

            // Snap threshold - when too far behind, teleport (e.g., initial spawn, large desync)
            if ((tickPos - smoothedBasePos).sqrMagnitude > 25f) // snap if > 5 units away
                smoothedBasePos = tickPos;
            else
                smoothedBasePos = Vector3.Lerp(smoothedBasePos, tickPos, smoothSpeed * Time.deltaTime);

            // Apply terrain height
            if (cachedMapData != null)
                smoothedBasePos.y = cachedMapData.SampleHeight(smoothedBasePos.x, smoothedBasePos.z) * cachedHeightScale;

            // Detect new attack — apply dash
            if (unitData.LastAttackTick > lastSeenAttackTick && unitData.LastAttackTick > 0)
            {
                lastSeenAttackTick = unitData.LastAttackTick;
                attackDashTimer = AttackDashDuration;
                Vector3 toTarget = unitData.LastAttackTargetPos.ToVector3() - smoothedBasePos;
                toTarget.y = 0f;
                attackDashDir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;

                if (unitData.State == UnitState.Gathering)
                    SFXManager.Instance?.Play(SFXType.GatherStrike, smoothedBasePos, 0.4f);
                else if (!unitData.IsRanged && unitData.State != UnitState.Constructing)
                    SFXManager.Instance?.Play(SFXType.MeleeAttack, smoothedBasePos, 0.6f);
            }

            // Detect new damage — apply flinch + flash
            if (unitData.LastDamageTick > lastSeenDamageTick && unitData.LastDamageTick > 0)
            {
                lastSeenDamageTick = unitData.LastDamageTick;
                damageFlinchTimer = DamageFlinchDuration;
                damageFlashTimer = DamageFlashDuration;
                Vector3 fromAttacker = unitData.LastDamageFromPos.ToVector3() - smoothedBasePos;
                fromAttacker.y = 0f;
                damageFlinchDir = fromAttacker.sqrMagnitude > 0.001f ? -fromAttacker.normalized : -transform.forward;

                SFXManager.Instance?.Play(SFXType.UnitHurt, smoothedBasePos, 0.5f);
            }

            // Detect charge hit — trigger knockback arc to sim-displaced position
            if (unitData.LastChargeHitTick > lastSeenChargeHitTick && unitData.LastChargeHitTick > 0)
            {
                lastSeenChargeHitTick = unitData.LastChargeHitTick;
                Vector3 endPos = unitData.SimPosition.ToVector3();
                if (cachedMapData != null)
                    endPos.y = cachedMapData.SampleHeight(endPos.x, endPos.z) * cachedHeightScale;
                meteorKnockbackActive = true;
                meteorKnockbackStart = smoothedBasePos;
                meteorKnockbackEnd = endPos;
                meteorKnockbackTimer = 0f;
                knockbackDuration = ChargeKnockupDuration;
                knockbackArcHeight = ChargeKnockupArcHeight;
            }

            // Detect heal — spawn green + indicator
            if (unitData.LastHealTick > lastSeenHealTick && unitData.LastHealTick > 0)
            {
                lastSeenHealTick = unitData.LastHealTick;
                SpawnHealIndicator(unitData.LastHealAmount);
            }

            // Detect new resource deposit — spawn floating indicator
            if (unitData.LastDepositTick > lastSeenDepositTick && unitData.LastDepositTick > 0)
            {
                lastSeenDepositTick = unitData.LastDepositTick;
                var net = GameBootstrapper.Instance?.Network;
                int localPid = (net != null && net.IsMultiplayer) ? net.LocalPlayerId : 0;
                if (unitData.PlayerId == localPid)
                    SpawnDepositIndicator(unitData.LastDepositAmount, unitData.LastDepositResourceType);
            }

            // Apply combat visual offsets
            Vector3 combatOffset = Vector3.zero;

            if (attackDashTimer > 0f)
            {
                float t = attackDashTimer / AttackDashDuration;
                // Quick out-and-back: peak at t=0.5 of the remaining duration
                float curve = Mathf.Sin(t * Mathf.PI);
                combatOffset += attackDashDir * (curve * AttackDashDistance);
                attackDashTimer -= Time.deltaTime;
            }

            if (damageFlinchTimer > 0f)
            {
                float t = damageFlinchTimer / DamageFlinchDuration;
                float curve = Mathf.Sin(t * Mathf.PI);
                combatOffset += damageFlinchDir * (curve * DamageFlinchDistance);
                damageFlinchTimer -= Time.deltaTime;
            }

            // Knockback arc override (meteor, charge, etc.)
            if (meteorKnockbackActive)
            {
                meteorKnockbackTimer += Time.deltaTime;
                float t = Mathf.Clamp01(meteorKnockbackTimer / knockbackDuration);
                Vector3 pos = Vector3.Lerp(meteorKnockbackStart, meteorKnockbackEnd, t);
                pos.y += 4f * knockbackArcHeight * t * (1f - t);
                transform.position = pos;

                if (t >= 1f)
                {
                    meteorKnockbackActive = false;
                    smoothedBasePos = unitData.SimPosition.ToVector3();
                    if (cachedMapData != null)
                        smoothedBasePos.y = cachedMapData.SampleHeight(smoothedBasePos.x, smoothedBasePos.z) * cachedHeightScale;
                }
            }
            else
            {
                transform.position = smoothedBasePos + combatOffset;
            }

            // White flash
            UpdateFlash();

            // Attack target ring pulsation
            UpdateAttackRing();

            // Facing: rotate toward movement direction or target facing
            // In combat, use target facing directly (position delta is noisy from chase/separation)
            if ((unitData.State == UnitState.InCombat || unitData.State == UnitState.Gathering || unitData.State == UnitState.Constructing) && unitData.HasTargetFacing)
            {
                Vector3 facingDir = unitData.TargetFacing.ToVector3();
                facingDir.y = 0f;
                if (facingDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(facingDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSmoothing);
                }
            }
            else
            {
                Vector3 delta = curr - prev;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(delta);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSmoothing);
                }
                else if (unitData.HasTargetFacing)
                {
                    Vector3 facingDir = unitData.TargetFacing.ToVector3();
                    facingDir.y = 0f;
                    if (facingDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(facingDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSmoothing);
                    }
                }
                else
                {
                    Vector3 facingDir = unitData.SimFacing.ToVector3();
                    facingDir.y = 0f;
                    if (facingDir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(facingDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSmoothing);
                    }
                }
            }

            UpdateWaypointLine();
            UpdateIdleEffect();
            UpdateDepositIndicators();
            UpdateHealIndicators();
            UpdateMonkArms();
            UpdateResourceStack();
        }

        private void UpdateMonkArms()
        {
            if (leftArm == null || rightArm == null) return;

            bool isHealing = unitData != null && unitData.IsHealer && unitData.HealTargetId >= 0
                && unitData.State == UnitState.InCombat;

            if (isHealing)
            {
                // Arms raised up and wiggling
                float t = Time.time;
                float wiggleSpeed = 8f;
                float wiggleAmount = 15f;
                float raiseAngle = -70f; // raise arms up

                float leftWiggle = Mathf.Sin(t * wiggleSpeed) * wiggleAmount;
                float rightWiggle = Mathf.Sin(t * wiggleSpeed + 1.5f) * wiggleAmount; // offset phase

                leftArm.localRotation = leftArmRestRotation
                    * Quaternion.Euler(raiseAngle, 0f, leftWiggle);
                rightArm.localRotation = rightArmRestRotation
                    * Quaternion.Euler(raiseAngle, 0f, rightWiggle);
            }
            else
            {
                // Return to rest position
                leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmRestRotation, Time.deltaTime * 5f);
                rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmRestRotation, Time.deltaTime * 5f);
            }
        }

        private void UpdateIdleEffect()
        {
            if (unitData == null || idleZzzContainer == null) return;

            bool isIdle = unitData.IsVillager && unitData.State == UnitState.Idle && unitData.CurrentHealth > 0 && unitData.IdleTimer >= Fixed32.One;
            
            if (isIdle)
            {
                idleTimer += Time.deltaTime;
                
                if (idleTimer >= IdleDelayTime && !showingIdleEffect)
                {
                    showingIdleEffect = true;
                    idleZzzContainer.SetActive(true);
                }
                
                if (showingIdleEffect)
                {
                    float animTime = (idleTimer - IdleDelayTime) * ZzzAnimationSpeed;
                    
                    // Each z rises up from bottom to top, then disappears - cycle through available z objects
                    for (int i = 0; i < ZzzCount; i++)
                    {
                        if (zzzTexts[i] != null)
                        {
                            // Calculate which "z cycle" this object should represent
                            // Each z spawns every 1.5 seconds, so we can have overlapping z's
                            float cycleTime = 2f; // Time between spawns
                            float zLifetime = 4f; // How long each z lasts
                            
                            // Find which cycle this z object should be in
                            int currentCycle = Mathf.FloorToInt(animTime / cycleTime);
                            int zCycle = currentCycle - i; // This z's cycle number
                            
                            if (zCycle >= 0)
                            {
                                float zStartTime = zCycle * cycleTime;
                                float zTime = animTime - zStartTime;
                                
                                if (zTime >= 0f && zTime <= zLifetime)
                                {
                                    // Rise from bottom (y=0) to top (y=0.6) over 4 seconds
                                    float progress = zTime / zLifetime;
                                    float yPos = progress * 0.6f;
                                    
                                    // Add slight horizontal drift
                                    float xOffset = Mathf.Sin(zTime * 2f) * 0.05f;
                                    
                                    zzzTexts[i].transform.localPosition = new Vector3(xOffset, yPos, 0f);
                                    
                                    // Fade in for first 25%, stay visible for 50%, fade out for last 25%
                                    float alpha;
                                    if (progress < 0.25f)
                                        alpha = progress / 0.25f; // Fade in
                                    else if (progress > 0.75f)
                                        alpha = (1f - progress) / 0.25f; // Fade out
                                    else
                                        alpha = 1f; // Fully visible
                                    
                                    var textMesh = zzzTexts[i].GetComponent<TextMesh>();
                                    if (textMesh != null)
                                    {
                                        Color color = new Color(1f, 1f, 1f, alpha * 0.7f);
                                        textMesh.color = color;
                                    }
                                    
                                    zzzTexts[i].SetActive(true);
                                }
                                else
                                {
                                    zzzTexts[i].SetActive(false);
                                }
                            }
                            else
                            {
                                zzzTexts[i].SetActive(false);
                            }
                        }
                    }
                    
                    // Make the container face the camera
                    Camera mainCam = UnitView.CachedMainCamera;
                    if (mainCam != null)
                    {
                        idleZzzContainer.transform.LookAt(mainCam.transform.position);
                        idleZzzContainer.transform.Rotate(0, 180, 0); // Face towards camera
                    }
                }
            }
            else
            {
                idleTimer = 0f;
                if (showingIdleEffect)
                {
                    showingIdleEffect = false;
                    idleZzzContainer.SetActive(false);
                }
            }
        }

        public void FlashAttackConfirm()
        {
            commandFlashTimer = CommandFlashDuration;
        }

        public void ShowAttackTargetRing()
        {
            if (selectionRing == null) return;

            // Cache renderer on first use
            if (selectionRingRenderer == null)
            {
                selectionRingRenderer = selectionRing.GetComponentInChildren<Renderer>();
                if (selectionRingRenderer == null) return;
                ringPropBlock = new MaterialPropertyBlock();
                originalRingColor = GetMaterialColor(selectionRingRenderer.sharedMaterial);
            }

            selectionRing.SetActive(true);
            attackRingTimer = AttackRingDuration;

            // Set initial red color via property block
            ringPropBlock.SetColor(BaseColorId, AttackRingColor);
            if (selectionRingRenderer.sharedMaterial.HasProperty(ColorId))
                ringPropBlock.SetColor(ColorId, AttackRingColor);
            selectionRingRenderer.SetPropertyBlock(ringPropBlock);
        }

        private void UpdateAttackRing()
        {
            if (attackRingTimer <= 0f) return;

            attackRingTimer -= Time.deltaTime;
            if (attackRingTimer <= 0f)
            {
                // Pulsation finished — restore ring
                attackRingTimer = 0f;
                if (selectionRingRenderer != null)
                {
                    ringPropBlock.SetColor(BaseColorId, originalRingColor);
                    if (selectionRingRenderer.sharedMaterial.HasProperty(ColorId))
                        ringPropBlock.SetColor(ColorId, originalRingColor);
                    selectionRingRenderer.SetPropertyBlock(ringPropBlock);
                }
                if (!isSelected && selectionRing != null)
                    selectionRing.SetActive(false);
                return;
            }

            // Pulsate: oscillate alpha between 0.3 and 1.0
            float t = attackRingTimer / AttackRingDuration;
            float pulse = 0.3f + 0.7f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 4f));
            Color c = AttackRingColor;
            c.a = pulse;
            if (selectionRingRenderer != null)
            {
                ringPropBlock.SetColor(BaseColorId, c);
                if (selectionRingRenderer.sharedMaterial.HasProperty(ColorId))
                    ringPropBlock.SetColor(ColorId, c);
                selectionRingRenderer.SetPropertyBlock(ringPropBlock);
            }
        }

        private void UpdateFlash()
        {
            // Command flash (red) takes priority over damage flash (white)
            if (commandFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, AttackFlashColor);
                    }
                }
                else
                {
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, AttackFlashColor);
                    }
                }
                commandFlashTimer -= Time.deltaTime;
            }
            else if (damageFlashTimer > 0f)
            {
                if (!flashActive)
                {
                    flashActive = true;
                    for (int i = 0; i < bodyRenderers.Length; i++)
                    {
                        if (bodyRenderers[i] != null)
                            SetMaterialColor(bodyRenderers[i].material, Color.white);
                    }
                }
                damageFlashTimer -= Time.deltaTime;
            }
            else if (flashActive)
            {
                flashActive = false;
                for (int i = 0; i < bodyRenderers.Length && i < originalColors.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, originalColors[i]);
                }
            }
        }

        private void OnDisable()
        {
            if (healthBarRoot != null && healthBarRoot.gameObject.activeSelf)
                healthBarRoot.gameObject.SetActive(false);
        }

        private void DestroyHealIndicatorPool()
        {
            if (healIndicatorRects == null) return;
            for (int i = 0; i < HealIndicatorPoolSize; i++)
            {
                if (healIndicatorRects[i] != null)
                    Destroy(healIndicatorRects[i].gameObject);
            }
            healIndicatorRects = null;
        }

        private void OnDestroy()
        {
            DestroyIndicatorPool();
            DestroyHealIndicatorPool();
            if (healthBarRoot != null)
                Destroy(healthBarRoot.gameObject);
        }

        public void HideHealthBar()
        {
            if (healthBarRoot != null && healthBarRoot.gameObject.activeSelf)
                healthBarRoot.gameObject.SetActive(false);
        }

        public void OnDeath()
        {
            if (IsDead) return;
            IsDead = true;

            if (healthBarRoot != null)
            {
                Destroy(healthBarRoot.gameObject);
                healthBarRoot = null;
            }

            SetSelected(false);

            // Restore colors in case flash was active
            if (flashActive)
            {
                flashActive = false;
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] != null)
                        SetMaterialColor(bodyRenderers[i].material, originalColors[i]);
                }
            }

            // Strip silhouette material so it doesn't persist as a solid shape during fade
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                var mats = bodyRenderers[i].sharedMaterials;
                if (mats.Length > 1)
                    bodyRenderers[i].sharedMaterials = new Material[] { mats[0] };
            }

            // Disable collider so corpse can't be selected
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Ensure active so coroutine can start (may have been deactivated by fog of war)
            gameObject.SetActive(true);
            StartCoroutine(CorpseFadeCoroutine());
        }

        private IEnumerator CorpseFadeCoroutine()
        {
            // Fall over animation — tip forward 90 degrees
            float fallDuration = 0.5f;
            float fallElapsed = 0f;
            Quaternion startRot = transform.rotation;
            Quaternion endRot = startRot * Quaternion.Euler(90f, 0f, 0f);
            while (fallElapsed < fallDuration)
            {
                fallElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fallElapsed / fallDuration);
                // Ease-in curve (accelerating fall like gravity)
                float eased = t * t;
                transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
                yield return null;
            }
            transform.rotation = endRot;

            yield return new WaitForSeconds(5f);

            // Collect all renderers for fading
            var renderers = GetComponentsInChildren<Renderer>();
            var materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                // Switch to transparent rendering
                materials[i] = renderers[i].material;
                materials[i].SetFloat("_Surface", 1); // Transparent (URP)
                materials[i].SetFloat("_Blend", 0);
                materials[i].SetOverrideTag("RenderType", "Transparent");
                materials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                materials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                materials[i].SetInt("_ZWrite", 0);
                materials[i].DisableKeyword("_ALPHATEST_ON");
                materials[i].EnableKeyword("_ALPHABLEND_ON");
                materials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                materials[i].renderQueue = 3000;
            }

            float fadeDuration = 2f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color c = GetMaterialColor(materials[i]);
                    c.a = a;
                    SetMaterialColor(materials[i], c);
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        public void TriggerMeteorKnockback(Vector3 startPos, Vector3 endPos)
        {
            meteorKnockbackActive = true;
            meteorKnockbackStart = startPos;
            meteorKnockbackEnd = endPos;
            meteorKnockbackTimer = 0f;
            knockbackDuration = MeteorKnockbackDuration;
            knockbackArcHeight = MeteorKnockbackArcHeight;
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            isPreselected = false;
            if (selectionRing != null)
            {
                // Keep ring active while attack pulsation is playing, even if deselected
                bool keepForPulse = !selected && attackRingTimer > 0f;
                selectionRing.SetActive(selected || keepForPulse);
            }
            if (!selected && waypointLine != null)
            {
                waypointLine.gameObject.SetActive(false);
                if (extraWaypointLines != null)
                    for (int i = 0; i < extraWaypointLines.Count; i++)
                        extraWaypointLines[i].gameObject.SetActive(false);
            }
        }

        public void SetPreselected(bool preselected)
        {
            isPreselected = preselected;
            if (selectionRing != null && !isSelected)
                selectionRing.SetActive(preselected);
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
        }

        public void SetSelectionRing(GameObject ring) { selectionRing = ring; }

        public Rect GetScreenBounds(Camera cam)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                return default;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool anyValid = false;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null) continue;
                Bounds b = bodyRenderers[i].bounds;
                Vector3 center = b.center;
                Vector3 ext = b.extents;

                // Project all 8 corners of the world-space AABB
                bool behindCamera = false;
                for (int cx = -1; cx <= 1 && !behindCamera; cx += 2)
                for (int cy = -1; cy <= 1 && !behindCamera; cy += 2)
                for (int cz = -1; cz <= 1 && !behindCamera; cz += 2)
                {
                    Vector3 corner = new Vector3(
                        center.x + ext.x * cx,
                        center.y + ext.y * cy,
                        center.z + ext.z * cz);
                    Vector3 sp = cam.WorldToScreenPoint(corner);
                    if (sp.z < 0) { behindCamera = true; break; }
                    if (sp.x < minX) minX = sp.x;
                    if (sp.x > maxX) maxX = sp.x;
                    if (sp.y < minY) minY = sp.y;
                    if (sp.y > maxY) maxY = sp.y;
                    anyValid = true;
                }
            }

            if (!anyValid) return default;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public bool IsSelected => isSelected;
        public bool IsHovered => isHovered;
        public bool IsElite => UnitType == 1 || UnitType == 3 || UnitType == 6 || UnitType == 7 || UnitType == 8 || UnitType == 12;
        public bool InFormation => unitData != null && unitData.InFormation;
        public FixedVector3 FormationOffset => unitData != null ? unitData.FormationOffset : default;
        public int FormationGroupId => unitData != null ? unitData.FormationGroupId : 0;
        public int FormationGroupSize => unitData != null ? unitData.FormationGroupSize : 0;
        public int PlayerId => unitData != null ? unitData.PlayerId : 0;
    }
}
