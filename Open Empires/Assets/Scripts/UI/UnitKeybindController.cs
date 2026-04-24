using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace OpenEmpires
{
    // Polls user-defined unit keybinds each frame and dispatches to UnitSelectionManager.
    // Mirrors BuildingKeybindController; bindings live in KeybindManager (PlayerPrefs).
    public class UnitKeybindController : MonoBehaviour
    {
        private struct Entry
        {
            public int unitType;
            public UnitKeybindKind kind;
            public ButtonControl control;
        }

        private struct SpecialEntry
        {
            public SpecialKeybind action;
            public ButtonControl control;
        }

        private static UnitKeybindController instance;
        public static UnitKeybindController Instance => instance;

        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<SpecialEntry> specialEntries = new List<SpecialEntry>();
        private UnitSelectionManager cachedSelection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (instance != null) return;
            var go = new GameObject("UnitKeybindController");
            instance = go.AddComponent<UnitKeybindController>();
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

        public void RefreshBindings()
        {
            entries.Clear();
            for (int i = 0; i < KeybindManager.BindableUnitTypes.Length; i++)
            {
                int t = KeybindManager.BindableUnitTypes[i];
                TryAdd(t, UnitKeybindKind.Cycle);
                TryAdd(t, UnitKeybindKind.SelectAll);
            }

            specialEntries.Clear();
            foreach (SpecialKeybind k in System.Enum.GetValues(typeof(SpecialKeybind)))
                TryAddSpecial(k);
        }

        private void TryAdd(int unitType, UnitKeybindKind kind)
        {
            string path = KeybindManager.GetUnitBinding(unitType, kind);
            if (string.IsNullOrEmpty(path)) return;
            var control = InputSystem.FindControl(path) as ButtonControl;
            if (control == null) return;
            entries.Add(new Entry { unitType = unitType, kind = kind, control = control });
        }

        private void TryAddSpecial(SpecialKeybind action)
        {
            string path = KeybindManager.GetSpecialBinding(action);
            if (string.IsNullOrEmpty(path)) return;
            var control = InputSystem.FindControl(path) as ButtonControl;
            if (control == null) return;
            specialEntries.Add(new SpecialEntry { action = action, control = control });
        }

        private void Update()
        {
            if (entries.Count == 0 && specialEntries.Count == 0) return;
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

                if (e.kind == UnitKeybindKind.Cycle)
                    cachedSelection.CycleSelectUnit(e.unitType);
                else
                    cachedSelection.SelectAllUnits(e.unitType);
            }

            for (int i = 0; i < specialEntries.Count; i++)
            {
                var e = specialEntries[i];
                if (e.control == null) continue;
                if (!e.control.wasPressedThisFrame) continue;

                switch (e.action)
                {
                    case SpecialKeybind.CycleIdleVillager: cachedSelection.CycleSelectIdleVillager(); break;
                    case SpecialKeybind.SelectAllIdleVillagers: cachedSelection.SelectAllIdleVillagers(); break;
                    case SpecialKeybind.CycleIdleMilitary: cachedSelection.CycleSelectIdleMilitary(); break;
                    case SpecialKeybind.SelectAllIdleMilitary: cachedSelection.SelectAllIdleMilitary(); break;
                }
            }
        }
    }
}
