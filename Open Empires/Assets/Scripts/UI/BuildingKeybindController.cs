using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace OpenEmpires
{
    // Polls user-defined building keybinds each frame and dispatches to UnitSelectionManager.
    // Bindings live in KeybindManager (PlayerPrefs); call RefreshBindings after the user edits any.
    public class BuildingKeybindController : MonoBehaviour
    {
        private struct Entry
        {
            public BuildingType type;
            public BuildingKeybindKind kind;
            public ButtonControl control;
        }

        private static BuildingKeybindController instance;
        public static BuildingKeybindController Instance => instance;

        private readonly List<Entry> entries = new List<Entry>();
        private UnitSelectionManager cachedSelection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("BuildingKeybindController");
            instance = go.AddComponent<BuildingKeybindController>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            RefreshBindings();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Re-reads every building binding from KeybindManager and resolves it to an InputSystem control.
        public void RefreshBindings()
        {
            entries.Clear();
            foreach (BuildingType type in System.Enum.GetValues(typeof(BuildingType)))
            {
                TryAdd(type, BuildingKeybindKind.Cycle);
                TryAdd(type, BuildingKeybindKind.SelectAll);
            }
        }

        private void TryAdd(BuildingType type, BuildingKeybindKind kind)
        {
            string path = KeybindManager.GetBuildingBinding(type, kind);
            if (string.IsNullOrEmpty(path)) return;
            var control = InputSystem.FindControl(path) as ButtonControl;
            if (control == null) return;
            entries.Add(new Entry { type = type, kind = kind, control = control });
        }

        private void Update()
        {
            if (entries.Count == 0) return;
            // Don't fire while menus / chat / minimap drag suppress gameplay input.
            if (UnitSelectionManager.UIInputSuppressed) return;

            if (cachedSelection == null)
            {
                cachedSelection = UnitSelectionManager.Instance;
                if (cachedSelection == null) return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.control == null) continue;
                if (!e.control.wasPressedThisFrame) continue;

                if (e.kind == BuildingKeybindKind.Cycle)
                    cachedSelection.CycleSelectBuilding(e.type);
                else
                    cachedSelection.SelectAllBuildings(e.type);
            }
        }
    }
}
