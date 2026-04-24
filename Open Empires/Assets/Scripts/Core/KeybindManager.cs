using UnityEngine;

namespace OpenEmpires
{
    public enum BuildingKeybindKind
    {
        Cycle,
        SelectAll,
    }

    public enum UnitKeybindKind
    {
        Cycle,
        SelectAll,
    }

    public enum SpecialKeybind
    {
        CycleIdleVillager,
        SelectAllIdleVillagers,
        CycleIdleMilitary,
        SelectAllIdleMilitary,
    }

    public static class KeybindManager
    {
        private struct ActionDef
        {
            public string actionName;
            public string displayName;
            public string defaultPath;
            public ActionDef(string n, string d, string p) { actionName = n; displayName = d; defaultPath = p; }
        }

        private static readonly ActionDef[] RemappableActionDefs =
        {
            new ActionDef("AttackMove", "Attack Move", "<Keyboard>/a"),
        };

        public static string[] ActionNames
        {
            get
            {
                var names = new string[RemappableActionDefs.Length];
                for (int i = 0; i < RemappableActionDefs.Length; i++)
                    names[i] = RemappableActionDefs[i].actionName;
                return names;
            }
        }

        public static string GetDisplayName(string actionName)
        {
            foreach (var def in RemappableActionDefs)
                if (def.actionName == actionName) return def.displayName;
            return actionName;
        }

        private static string DefaultPath(string actionName)
        {
            foreach (var def in RemappableActionDefs)
                if (def.actionName == actionName) return def.defaultPath;
            return string.Empty;
        }

        public static string GetBinding(string actionName)
        {
            string key = "kb_" + actionName;
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetString(key);
            return DefaultPath(actionName);
        }

        public static void SetBinding(string actionName, string path)
        {
            PlayerPrefs.SetString("kb_" + actionName, path);
            PlayerPrefs.Save();
        }

        public static void ResetToDefault(string actionName)
        {
            PlayerPrefs.DeleteKey("kb_" + actionName);
            PlayerPrefs.Save();
        }

        public static void ResetAll()
        {
            foreach (var def in RemappableActionDefs)
                PlayerPrefs.DeleteKey("kb_" + def.actionName);

            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                PlayerPrefs.DeleteKey(BuildingKeybindKey(type, BuildingKeybindKind.Cycle));
                PlayerPrefs.DeleteKey(BuildingKeybindKey(type, BuildingKeybindKind.SelectAll));
            }

            for (int i = 0; i < BindableUnitTypes.Length; i++)
            {
                int t = BindableUnitTypes[i];
                PlayerPrefs.DeleteKey(UnitKeybindKey(t, UnitKeybindKind.Cycle));
                PlayerPrefs.DeleteKey(UnitKeybindKey(t, UnitKeybindKind.SelectAll));
            }

            foreach (SpecialKeybind k in System.Enum.GetValues(typeof(SpecialKeybind)))
                PlayerPrefs.DeleteKey(SpecialKeybindKey(k));

            PlayerPrefs.Save();
        }

        // Building keybinds — no defaults (empty == unbound).
        private static string BuildingKeybindKey(BuildingType type, BuildingKeybindKind kind)
            => "kb_building_" + (int)type + "_" + (int)kind;

        public static string GetBuildingBinding(BuildingType type, BuildingKeybindKind kind)
        {
            string key = BuildingKeybindKey(type, kind);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : string.Empty;
        }

        public static void SetBuildingBinding(BuildingType type, BuildingKeybindKind kind, string path)
        {
            PlayerPrefs.SetString(BuildingKeybindKey(type, kind), path);
            PlayerPrefs.Save();
        }

        public static void ClearBuildingBinding(BuildingType type, BuildingKeybindKind kind)
        {
            PlayerPrefs.DeleteKey(BuildingKeybindKey(type, kind));
            PlayerPrefs.Save();
        }

        // Unit type ids that can have keybinds. Sheep (5) is excluded — it isn't user-controlled.
        // Mirrors the integer encoding used throughout the codebase (see UnitData.cs:79).
        public static readonly int[] BindableUnitTypes = { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        // Indexed by unit type id. Mirrors UnitInfoUI.UnitTypeNames so the keybind UI can label rows
        // without taking a dependency on the info-panel script.
        private static readonly string[] UnitTypeDisplayNames =
        {
            "Villager", "Spearman", "Archer", "Horseman", "Scout", "Sheep",
            "Man-at-Arms", "Knight", "Crossbowman", "Monk",
            "Longbowman", "Gendarme", "Landsknecht",
            "Battering Ram", "Mangonel", "Trebuchet",
        };

        public static string GetUnitTypeDisplayName(int unitType)
        {
            if (unitType < 0 || unitType >= UnitTypeDisplayNames.Length) return "Unit " + unitType;
            return UnitTypeDisplayNames[unitType];
        }

        private static string UnitKeybindKey(int unitType, UnitKeybindKind kind)
            => "kb_unit_" + unitType + "_" + (int)kind;

        public static string GetUnitBinding(int unitType, UnitKeybindKind kind)
        {
            string key = UnitKeybindKey(unitType, kind);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : string.Empty;
        }

        public static void SetUnitBinding(int unitType, UnitKeybindKind kind, string path)
        {
            PlayerPrefs.SetString(UnitKeybindKey(unitType, kind), path);
            PlayerPrefs.Save();
        }

        public static void ClearUnitBinding(int unitType, UnitKeybindKind kind)
        {
            PlayerPrefs.DeleteKey(UnitKeybindKey(unitType, kind));
            PlayerPrefs.Save();
        }

        // Special keybinds — hand-picked actions that don't fit the per-type buckets.
        private static string SpecialKeybindKey(SpecialKeybind k) => "kb_special_" + (int)k;

        public static string GetSpecialDisplayName(SpecialKeybind k)
        {
            switch (k)
            {
                case SpecialKeybind.CycleIdleVillager: return "Cycle Idle Villager";
                case SpecialKeybind.SelectAllIdleVillagers: return "Select All Idle Villagers";
                case SpecialKeybind.CycleIdleMilitary: return "Cycle Idle Military";
                case SpecialKeybind.SelectAllIdleMilitary: return "Select All Idle Military";
                default: return k.ToString();
            }
        }

        public static string GetSpecialBinding(SpecialKeybind k)
        {
            string key = SpecialKeybindKey(k);
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : string.Empty;
        }

        public static void SetSpecialBinding(SpecialKeybind k, string path)
        {
            PlayerPrefs.SetString(SpecialKeybindKey(k), path);
            PlayerPrefs.Save();
        }

        public static void ClearSpecialBinding(SpecialKeybind k)
        {
            PlayerPrefs.DeleteKey(SpecialKeybindKey(k));
            PlayerPrefs.Save();
        }

        /// <summary>Extracts a short display name from an input path, e.g. "&lt;Keyboard&gt;/h" → "H".</summary>
        public static string GetKeyDisplayName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "?";
            int slash = path.LastIndexOf('/');
            string key = slash >= 0 ? path.Substring(slash + 1) : path;
            return key.ToUpper();
        }
    }
}
