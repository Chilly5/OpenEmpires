using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OpenEmpires
{
    internal static class CommanderUIInputGuard
    {
        internal static bool IsPointerOverCommander
        {
            get
            {
                var commander = CommanderChatUI.Instance;
                if (commander == null) return false;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                // Input callbacks precede VirtualCursor.Update. Read this event's
                // live device position, preserving the virtual position while locked.
                Vector2 point = mouse != null && Cursor.lockState != CursorLockMode.Locked
                    ? mouse.position.ReadValue() : VirtualCursor.Position;
                return commander.ContainsScreenPoint(point);
            }
        }

        // Observe actual selected editable UI, not one shared flag that another chat
        // can clear. This also covers non-Commander text fields without disabling play
        // merely because a conversation panel is visible.
        internal static bool IsEditingText
        {
            get
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy) return false;
                var tmp = selected.GetComponentInParent<TMP_InputField>();
                if (tmp != null && tmp.isActiveAndEnabled && tmp.interactable) return true;
                var legacy = selected.GetComponentInParent<InputField>();
                return legacy != null && legacy.isActiveAndEnabled && legacy.interactable;
            }
        }
    }
}
