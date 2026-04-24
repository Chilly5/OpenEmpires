using UnityEngine;

namespace OpenEmpires
{
    public enum BuildingKeybindKind
    {
        Cycle,
        SelectAll,
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
