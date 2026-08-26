using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires.Village
{
    /// <summary>
    /// Minimal observer HUD: day/clock at the top, an inspector card for the selected
    /// villager, and 1/2/3 keys for simulation speed.
    /// </summary>
    public class VillageHUD : MonoBehaviour
    {
        [Tooltip("Overall size of the village HUD panels.")]
        [SerializeField] private float uiScale = 1.4f;
        private float vw, vh; // virtual screen size after scaling

        private UnitSelectionManager selection;
        private VillageCameraFollow follow;
        private readonly List<(VillagerProfile who, int score)> friendsScratch = new List<(VillagerProfile, int)>();
        private readonly List<(VillagerProfile who, int score)> rivalsScratch = new List<(VillagerProfile, int)>();
        private GUIStyle boxStyle, titleStyle, bodyStyle, smallStyle, smallStyleLeft, buttonStyle, logStyle, logTimeStyle, logNotableStyle;
        private float speed = 1f;

        private void Start()
        {
            selection = FindFirstObjectByType<UnitSelectionManager>();
            follow = FindFirstObjectByType<VillageCameraFollow>();

            // The RTS info panel used to create the EventSystem; the minimap still needs one.
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private bool playerListHidden;

        private void Update()
        {
            // The match-style team panel / "destroy all enemy HQs" hint is meaningless here.
            if (!playerListHidden)
            {
                var list = FindFirstObjectByType<PlayerListUI>();
                if (list != null) { list.gameObject.SetActive(false); playerListHidden = true; }
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) SetSpeed(speed == 0f ? 1f : 0f);
            if (kb.digit1Key.wasPressedThisFrame) SetSpeed(1f);
            if (kb.digit2Key.wasPressedThisFrame) SetSpeed(2f);
            if (kb.digit3Key.wasPressedThisFrame) SetSpeed(3f);
            if (kb.digit4Key.wasPressedThisFrame) SetSpeed(5f);
        }

        private static readonly float[] Speeds = { 0f, 1f, 2f, 3f, 5f };
        private static readonly string[] SpeedLabels = { "❚❚", "▶ 1×", "2×", "3×", "5×" };

        /// <summary>Scales only the simulation clock (camera/UI keep running), so pause is a true freeze.</summary>
        private void SetSpeed(float s)
        {
            speed = s;
            var gb = GameBootstrapper.Instance;
            if (gb != null) gb.SimulationSpeed = s;
        }

        private void DrawSpeedButtons(Rect r)
        {
            float bw = r.width / Speeds.Length;
            for (int i = 0; i < Speeds.Length; i++)
            {
                bool active = Mathf.Approximately(speed, Speeds[i]);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = active ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.8f, 0.8f);
                if (GUI.Button(new Rect(r.x + i * bw, r.y, bw - 3f, r.height), SpeedLabels[i], buttonStyle))
                    SetSpeed(Speeds[i]);
                GUI.backgroundColor = prev;
            }
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            // Opaque panels: a solid dark background with a thin light edge (default skin boxes are translucent).
            var panelTex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
            var fill = new Color(0.13f, 0.14f, 0.17f, 1f);
            var edge = new Color(0.55f, 0.50f, 0.40f, 1f);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    panelTex.SetPixel(x, y, (x == 0 || y == 0 || x == 7 || y == 7) ? edge : fill);
            panelTex.Apply();
            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 8, 8), border = new RectOffset(2, 2, 2, 2) };
            boxStyle.normal.background = panelTex;
            boxStyle.normal.scaledBackgrounds = null;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            bodyStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            smallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            logStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false };
            logStyle.normal.textColor = Color.white;
            logTimeStyle = new GUIStyle(logStyle) { fontStyle = FontStyle.Bold };
            logTimeStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
            smallStyleLeft = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
            logNotableStyle = new GUIStyle(logStyle) { fontStyle = FontStyle.Bold };
            logNotableStyle.normal.textColor = new Color(1f, 0.82f, 0.35f);
            logWrapStyle = new GUIStyle(logStyle) { wordWrap = true, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Overflow };
            logNotableWrapStyle = new GUIStyle(logNotableStyle) { wordWrap = true, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Overflow };
            if (whiteTex == null) BuildTimelineTexture();
        }

        private void OnGUI()
        {
            var sim = GameBootstrapper.Instance?.Simulation;
            var village = VillageBootstrapper.Instance;
            if (sim == null || village == null || village.Routine == null) return;
            EnsureStyles();

            // Scale the whole HUD; lay everything out in virtual (pre-scale) coordinates.
            float s = Mathf.Max(0.5f, uiScale);
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            vw = Screen.width / s;
            vh = Screen.height / s;

            int tick = sim.CurrentTick;
            int minute = VillageClock.MinuteOfDay(tick);
            string icon = VillageClock.IsNight(minute) ? "☾" : "☀";

            // --- Clock + day/night timeline ---
            float w = 340f;
            var rect = new Rect(12f, 12f, w, 124f); // top-left
            GUI.Box(rect, GUIContent.none, boxStyle);
            var season = VillageClock.SeasonOf(tick);
            GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width, 26f), $"{VillageClock.SeasonIcon(season)} {season}, Year {VillageClock.Year(tick)} · Day {VillageClock.Day(tick)}   {VillageClock.Format(minute)} {icon}", titleStyle);
            DrawTimeline(new Rect(rect.x + 16f, rect.y + 34f, rect.width - 32f, 14f),
                VillageClock.DayFraction(tick, GameBootstrapper.Instance.InterpolationAlpha));
            GUI.Label(new Rect(rect.x, rect.y + 66f, rect.width, 20f), CountPhases(village.Routine), smallStyle);
            DrawSpeedButtons(new Rect(rect.x + 16f, rect.y + 90f, rect.width - 32f, 24f));

            DrawActivityLog(village.Routine);

            // --- Selected / followed villager (the follow keeps the card up while they're indoors) ---
            int inspectId = selection != null && selection.SelectedUnits.Count == 1 ? selection.SelectedUnits[0].UnitId : -1;
            if (inspectId < 0 && follow != null) inspectId = follow.FollowedUnitId;
            if (inspectId < 0) return;
            var profile = village.Routine.GetProfile(inspectId);
            if (profile == null) return;

            var home = sim.BuildingRegistry.GetBuilding(profile.HomeBuildingId);
            var work = sim.BuildingRegistry.GetBuilding(profile.WorkplaceBuildingId);
            string workName = work != null ? work.Type.ToString() : "—";
            string lunch = profile.TakesLunch ? $"{VillageClock.Format(profile.LunchStartMinute)}–{VillageClock.Format(profile.LunchEndMinute)}" : "none";

            DrawVillagerCard(sim, village.Routine, profile, home, workName, lunch);
        }

        // ------------------------------------------------------------------ villager card

        private void DrawVillagerCard(GameSimulation sim, VillageRoutineSystem routine, VillagerProfile profile, BuildingData home, string workName, string lunch)
        {
            const float W = 360f;
            float H = 436f + Mathf.Max(0, (profile.Traits.Count - 1) / 3) * 18f + (profile.Memories.Count > 0 ? 16f + 16f * Mathf.Min(3, profile.Memories.Count) : 0f);
            var card = new Rect(12f, vh - 12f - H, W, H); // bottom-left
            GUI.Box(card, GUIContent.none, boxStyle);
            float x = card.x + 12f, w = card.width - 24f, y = card.y + 8f;

            // Identity
            float age = profile.AgeDays(sim.CurrentTick);
            string stage = profile.Stage == LifeStage.Child ? (profile.Gender == Gender.Female ? "girl" : "boy")
                         : profile.Stage == LifeStage.Elder ? (profile.Gender == Gender.Female ? "old woman" : "old man")
                         : (profile.Gender == Gender.Female ? "woman" : "man");
            var prevColor = GUI.color;
            GUI.color = profile.Gender == Gender.Female ? new Color(1f, 0.6f, 0.8f) : new Color(0.55f, 0.7f, 1f);
            GUI.Label(new Rect(x, y, w, 24f), (profile.Gender == Gender.Female ? "♀ " : "♂ ") + profile.FullName, titleStyle);
            GUI.color = prevColor;
            y += 26f;
            GUI.Label(new Rect(x, y, w, 18f), $"{Cap(stage)}, {age:0.0} days old · {VillageJobInfo.DisplayName(profile.Job)} at the {workName}" + (profile.Quirky ? " · ✶ eccentric" : "") + (profile.Mounted ? " · 🐎 knight" : profile.Military == MilitaryKind.Soldier ? " · ⚔ soldier" : profile.Military == MilitaryKind.Archer ? " · ➶ archer" : profile.Armed ? " · ⚔ militia" : ""), bodyStyle); y += 20f;
            var partner = profile.PartnerId >= 0 ? routine.GetProfile(profile.PartnerId) : null;
            string family = partner != null ? $"♥ {partner.FullName}" : (profile.Stage == LifeStage.Adult ? "single" : "");
            if (profile.Children > 0) family += (family.Length > 0 ? " · " : "") + $"{profile.Children} child{(profile.Children > 1 ? "ren" : "")}";
            GUI.Label(new Rect(x, y, w, 18f), $"⌂ {profile.FamilyName} house #{profile.HouseholdIndex + 1}" + (home == null ? " (gone)" : "") + (family.Length > 0 ? "   " + family : ""), bodyStyle); y += 22f;

            // Traits (three per line)
            if (profile.Traits.Count > 0)
            {
                var line = new System.Text.StringBuilder("Traits: ");
                for (int i = 0; i < profile.Traits.Count; i++)
                {
                    if (i > 0 && i % 3 == 0)
                    {
                        GUI.Label(new Rect(x, y, w, 18f), line.ToString(), smallStyleLeft); y += 18f;
                        line.Clear().Append("        ");
                    }
                    else if (i > 0) line.Append("  ·  ");
                    var t = profile.Traits[i];
                    line.Append(VillageTraits.Icon(t)).Append(' ').Append(VillageTraits.Name(t));
                    if (profile.TraitExpiry.TryGetValue(t, out int until))
                        line.Append(" (").Append(Mathf.Max(0, (until - sim.CurrentTick) * 24 / Mathf.Max(1, VillageClock.DayLengthTicks))).Append("h)");
                }
                GUI.Label(new Rect(x, y, w, 18f), line.ToString(), smallStyleLeft); y += 20f;
            }

            // Relationships
            routine.TopRelations(profile, 3, friendsScratch, rivalsScratch);
            if (friendsScratch.Count > 0 || rivalsScratch.Count > 0)
            {
                var rel = new System.Text.StringBuilder();
                if (friendsScratch.Count > 0)
                {
                    rel.Append("♥ ");
                    for (int i = 0; i < friendsScratch.Count; i++) rel.Append(i > 0 ? ", " : "").Append(friendsScratch[i].who.FirstName).Append(' ').Append(friendsScratch[i].score);
                }
                if (rivalsScratch.Count > 0)
                {
                    if (rel.Length > 0) rel.Append("   ");
                    rel.Append("✕ ");
                    for (int i = 0; i < rivalsScratch.Count; i++) rel.Append(i > 0 ? ", " : "").Append(rivalsScratch[i].who.FirstName).Append(' ').Append(rivalsScratch[i].score);
                }
                GUI.Label(new Rect(x, y, w, 18f), rel.ToString(), smallStyleLeft); y += 20f;
            }

            // Now doing
            string doing = profile.IsDead ? "☠ " + profile.Activity
                         : (string.IsNullOrEmpty(profile.Thought) ? "→ " : profile.Thought + "  ·  ") + profile.Activity;
            GUI.Label(new Rect(x, y, w, 18f), "Now: " + doing, bodyStyle); y += 22f;

            // Mood + needs
            int mood = routine.MoodPercent(profile);
            string moodWord = mood >= 70 ? "☺ content" : mood >= 40 ? "· so-so" : mood >= 20 ? "☹ miserable" : "☹ at the end of their rope";
            DrawNeed(x, ref y, w, "Mood", routine.Mood(profile), mood >= 40 ? new Color(0.75f, 0.85f, 0.55f) : new Color(0.9f, 0.4f, 0.35f), "  " + moodWord);
            string hungerNote = profile.IsStarving ? "  STARVING" : profile.MissedMeals > 0 ? $"  ({profile.MissedMeals} meals missed)" : "";
            DrawNeed(x, ref y, w, "♨ Hunger", profile.Hunger, new Color(0.95f, 0.55f, 0.25f), hungerNote);
            DrawNeed(x, ref y, w, "☾ Sleep", profile.Energy, new Color(0.45f, 0.55f, 1f), "");
            DrawNeed(x, ref y, w, "☺ Social", profile.Social, new Color(0.45f, 0.85f, 0.45f), "");
            DrawNeed(x, ref y, w, "♪ Fun", profile.Fun, new Color(0.95f, 0.75f, 0.25f), "");
            y += 4f;

            // Money & meals
            string meals = (profile.HasEaten(Meal.Breakfast) ? "B✓" : profile.HasHandled(Meal.Breakfast) ? "B✗" : "B·") + " " +
                           (profile.HasEaten(Meal.Lunch) ? "L✓" : profile.HasHandled(Meal.Lunch) ? "L✗" : "L·") + " " +
                           (profile.HasEaten(Meal.Dinner) ? "D✓" : profile.HasHandled(Meal.Dinner) ? "D✗" : "D·");
            GUI.Label(new Rect(x, y, w, 18f), $"● {profile.Money} coins   Meals today: {meals}", bodyStyle); y += 22f;

            // Schedule
            GUI.Label(new Rect(x, y, w, 18f), $"☀ Wakes {VillageClock.Format(profile.WakeMinute)} · works {VillageClock.Format(profile.WorkStartMinute)}–{VillageClock.Format(profile.WorkEndMinute)} · lunch {lunch} · bed {VillageClock.Format(profile.SleepMinute)}", smallStyleLeft); y += 20f;

            // Memories (strongest first)
            if (profile.Memories.Count > 0)
            {
                GUI.Label(new Rect(x, y, w, 16f), "Remembers:", smallStyleLeft); y += 16f;
                int shownMem = 0;
                for (int i = profile.Memories.Count - 1; i >= 0 && shownMem < 3; i--, shownMem++)
                {
                    var m = profile.Memories[i];
                    GUI.Label(new Rect(x + 8f, y, w - 8f, 16f), (m.Delta < 0 ? "☹ " : "☺ ") + m.Text, m.Delta < 0 ? logStyle : logStyle); y += 16f;
                }
            }

            // Recent actions
            GUI.Label(new Rect(x, y, w, 18f), "Recent:", smallStyleLeft); y += 16f;
            int shown = 0;
            for (int i = routine.Activity.Count - 1; i >= 0 && shown < 4; i--)
            {
                var e = routine.Activity[i];
                if (e.UnitId != profile.UnitId) continue;
                string text = e.Text.StartsWith(profile.FullName) ? e.Text.Substring(profile.FullName.Length).TrimStart() : e.Text;
                GUI.Label(new Rect(x, y, 44f, 16f), VillageClock.Format(VillageClock.MinuteOfDay(e.Tick)), logTimeStyle);
                GUI.Label(new Rect(x + 46f, y, w - 46f, 16f), (e.Notable ? "★ " : "") + text, e.Notable ? logNotableStyle : logStyle);
                y += 16f; shown++;
            }
        }

        private void DrawNeed(float x, ref float y, float w, string label, int value, Color color, string note)
        {
            float pct = Mathf.Clamp01(value / (float)VillageRoutineSystem.NeedMax);
            GUI.Label(new Rect(x, y, 78f, 18f), label, bodyStyle);
            var bar = new Rect(x + 82f, y + 4f, w - 82f - 40f, 10f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, whiteTex ?? Texture2D.whiteTexture);
            GUI.color = pct < 0.25f ? Color.Lerp(new Color(0.9f, 0.2f, 0.2f), color, pct * 4f) : color;
            GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * pct, bar.height - 2f), whiteTex ?? Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 4f, y, 40f, 18f), $"{Mathf.RoundToInt(pct * 100f)}%" + note, smallStyleLeft);
            y += 20f;
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // ------------------------------------------------------------------ activity log

        // Log filter toggles (only Events on by default) and scroll state.
        private readonly bool[] logCategoryOn = { true, false, false, false }; // Events, Social, Economy, Routine
        private static readonly string[] LogCategoryNames = { "★ Events", "☺ Social", "● Economy", "· Routine" };
        private Vector2 logScroll;
        private int logLastCount = -1;
        private bool logFollowNewest = true;
        private readonly List<int> logVisible = new List<int>();
        private readonly List<float> logRowY = new List<float>();
        private readonly Dictionary<string, float> rowHeightCache = new Dictionary<string, float>();
        private float rowHeightCacheWidth = -1f;
        private GUIStyle logWrapStyle, logNotableWrapStyle;

        /// <summary>Height of a wrapped log row (cached per text; cache reset when the width changes).</summary>
        private float RowHeight(VillageRoutineSystem.ActivityEntry e, float width)
        {
            if (rowHeightCacheWidth != width) { rowHeightCache.Clear(); rowHeightCacheWidth = width; }
            string text = e.Notable ? "★ " + e.Text : e.Text;
            if (!rowHeightCache.TryGetValue(text, out float h))
            {
                h = Mathf.Max(18f, (e.Notable ? logNotableWrapStyle : logWrapStyle).CalcHeight(new GUIContent(text), width) + 2f);
                if (rowHeightCache.Count > 2000) rowHeightCache.Clear();
                rowHeightCache[text] = h;
            }
            return h;
        }

        /// <summary>Top-right scrollable feed with per-category toggles; follows the newest line unless scrolled up.</summary>
        private void DrawActivityLog(VillageRoutineSystem routine)
        {
            float w = 380f, lineH = 18f, listH = 300f;
            var rect = new Rect(vw - w - 12f, 12f, w, listH + 66f);
            GUI.Box(rect, GUIContent.none, boxStyle);
            GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width, 22f), "Village activity", titleStyle);

            // Category toggles
            float bw = (rect.width - 24f) / LogCategoryNames.Length;
            for (int c = 0; c < LogCategoryNames.Length; c++)
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = logCategoryOn[c] ? new Color(1f, 0.85f, 0.4f) : new Color(0.6f, 0.6f, 0.6f);
                if (GUI.Button(new Rect(rect.x + 12f + c * bw, rect.y + 28f, bw - 3f, 20f), LogCategoryNames[c], buttonStyle))
                {
                    logCategoryOn[c] = !logCategoryOn[c];
                    logFollowNewest = true;
                }
                GUI.backgroundColor = prevBg;
            }

            // Collect visible entries and measure their wrapped heights.
            var log = routine.Activity;
            var listRect = new Rect(rect.x + 8f, rect.y + 54f, rect.width - 16f, listH);
            float textW = listRect.width - 18f - 50f;
            logVisible.Clear();
            logRowY.Clear();
            float contentH = 0f;
            for (int i = 0; i < log.Count; i++)
            {
                if (!logCategoryOn[(int)log[i].Category]) continue;
                logVisible.Add(i);
                logRowY.Add(contentH);
                contentH += RowHeight(log[i], textW);
            }
            float scrollH = Mathf.Max(listH, contentH);
            var contentRect = new Rect(0f, 0f, listRect.width - 18f, scrollH);

            // Auto-follow the newest entry unless the player scrolled up.
            if (logVisible.Count != logLastCount)
            {
                logLastCount = logVisible.Count;
                if (logFollowNewest) logScroll.y = Mathf.Max(0f, scrollH - listH);
            }
            var before = logScroll;
            logScroll = GUI.BeginScrollView(listRect, logScroll, contentRect, false, true);
            if (Mathf.Abs(logScroll.y - before.y) > 0.5f) logFollowNewest = logScroll.y >= scrollH - listH - 2f;

            // Only draw the rows in view (binary search for the first visible row).
            int first = 0, hi = logVisible.Count - 1;
            while (first < hi) { int mid = (first + hi) / 2; if (logRowY[mid] + RowHeight(log[logVisible[mid]], textW) < logScroll.y) first = mid + 1; else hi = mid; }
            for (int v = first; v < logVisible.Count; v++)
            {
                float y = logRowY[v];
                if (y > logScroll.y + listH) break;
                var e = log[logVisible[v]];
                float h = RowHeight(e, textW);
                GUI.Label(new Rect(2f, y, 44f, lineH), VillageClock.Format(VillageClock.MinuteOfDay(e.Tick)), logTimeStyle);
                GUI.Label(new Rect(48f, y, textW, h), e.Notable ? "★ " + e.Text : e.Text, e.Notable ? logNotableWrapStyle : logWrapStyle);
            }
            GUI.EndScrollView();

            if (logVisible.Count == 0)
                GUI.Label(new Rect(listRect.x, listRect.y + listH * 0.45f, listRect.width, 20f), "No entries in the selected categories yet", smallStyle);
        }

        // ------------------------------------------------------------------ timeline

        private Texture2D timelineTex, whiteTex;

        /// <summary>Horizontal 24h strip: night → dawn → day → dusk → night, with a marker at the current time.</summary>
        private void DrawTimeline(Rect r, float dayFraction)
        {
            if (timelineTex == null) BuildTimelineTexture();

            GUI.DrawTexture(r, timelineTex, ScaleMode.StretchToFill);

            // Hour ticks every 6h with labels
            for (int h = 0; h <= 24; h += 6)
            {
                float x = r.x + r.width * h / 24f;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(new Rect(x - 0.5f, r.y, 1f, r.height), whiteTex);
                GUI.color = Color.white;
                GUI.Label(new Rect(x - 16f, r.yMax + 1f, 32f, 14f), h == 24 ? "0" : h.ToString(), smallStyle);
            }

            // Current-time marker
            float mx = r.x + r.width * dayFraction;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(mx - 2f, r.y - 3f, 4f, r.height + 6f), whiteTex);
            GUI.color = new Color(1f, 0.95f, 0.6f);
            GUI.DrawTexture(new Rect(mx - 1f, r.y - 2f, 2f, r.height + 4f), whiteTex);
            GUI.color = Color.white;
        }

        private void BuildTimelineTexture()
        {
            const int W = 240;
            timelineTex = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color night = new Color(0.10f, 0.13f, 0.30f);
            Color dawn = new Color(0.95f, 0.55f, 0.35f);
            Color day = new Color(0.55f, 0.80f, 1.00f);
            Color dusk = new Color(0.85f, 0.45f, 0.40f);
            for (int i = 0; i < W; i++)
            {
                float t = (float)i / (W - 1) * 24f; // hour
                Color c;
                if (t < 5f) c = night;
                else if (t < 6.5f) c = Color.Lerp(night, dawn, (t - 5f) / 1.5f);
                else if (t < 8f) c = Color.Lerp(dawn, day, (t - 6.5f) / 1.5f);
                else if (t < 17.5f) c = day;
                else if (t < 19f) c = Color.Lerp(day, dusk, (t - 17.5f) / 1.5f);
                else if (t < 20.5f) c = Color.Lerp(dusk, night, (t - 19f) / 1.5f);
                else c = night;
                timelineTex.SetPixel(i, 0, c);
            }
            timelineTex.Apply();

            whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            whiteTex.SetPixel(0, 0, Color.white);
            whiteTex.Apply();
        }

        private static string CountPhases(VillageRoutineSystem routine)
        {
            int sleeping = 0, working = 0, other = 0, eating = 0, starving = 0, dead = 0;
            foreach (var p in routine.Profiles)
            {
                if (p.IsDead) { dead++; continue; }
                if (p.IsStarving) starving++;
                if (p.PendingMeal != Meal.None) { eating++; continue; }
                switch (p.Phase)
                {
                    case RoutinePhase.Sleeping: sleeping++; break;
                    case RoutinePhase.Working: working++; break;
                    default: other++; break;
                }
            }
            string s = $"{working} working · {eating} eating · {other} at leisure · {sleeping} asleep";
            if (routine.Fights.Count > 0) s += $" · ⚔ {routine.Fights.Count} fight{(routine.Fights.Count > 1 ? "s" : "")}";
            if (routine.WolfIds.Count > 0) s += $" · 🐺 {routine.WolfIds.Count} wolves!";
            if (routine.SoldierIds.Count > 0) s += $" · ⚔ {routine.SoldierIds.Count} raiders!";
            if (routine.StablesBuildingId >= 0) s += $" · 🐎 {routine.StablesHorses}/{VillageRoutineSystem.StableCapacity} stabled, {routine.HorseIds.Count} wild";
            if (routine.ActiveProject != null) s += $" · ⚒ building {routine.ActiveProject.Label}" + (routine.ActiveProject.Placed ? "" : $" ({routine.ActiveProject.LoadsDelivered}/{routine.ActiveProject.LoadsNeeded} loads)");
            if (routine.Corpses.Count > 0) s += $" · ☠ {routine.Corpses.Count} unburied";
            var sim = GameBootstrapper.Instance?.Simulation;
            if (sim != null) { var res = sim.ResourceManager.GetPlayerResources(0); s += $" · food {res.Food} (meal {routine.CurrentMealPrice(sim)}c, harvest {VillageRoutineSystem.HarvestPercent(VillageClock.SeasonOf(sim.CurrentTick))}%) · wood {res.Wood}"; }
            if (starving > 0) s += $" · {starving} starving";
            if (dead > 0) s += $" · {dead} dead";
            return s;
        }
    }
}
