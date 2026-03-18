using UnityEngine;
using UnityEngine.InputSystem;

namespace OpenEmpires
{
    public class RTSInputActions : System.IDisposable
    {
        public RTSActions RTS { get; private set; }

        public RTSInputActions()
        {
            RTS = new RTSActions();
        }

        public void Dispose()
        {
            RTS.Dispose();
        }

        public class RTSActions : System.IDisposable
        {
            public InputAction CameraPan { get; private set; }
            public InputAction CameraZoom { get; private set; }
            public InputAction CameraRotateEnable { get; private set; }
            public InputAction CameraRotateDelta { get; private set; }
            public InputAction MousePosition { get; private set; }
            public InputAction Select { get; private set; }
            public InputAction Command { get; private set; }
            public InputAction MultiSelect { get; private set; }
            public InputAction DeselectAll { get; private set; }
            public InputAction AttackMove { get; private set; }
            public InputAction DeleteEntity { get; private set; }

            // Control group keys (0-9)
            public InputAction[] ControlGroups { get; private set; }

            public RTSActions()
            {
                CameraPan = new InputAction("CameraPan", InputActionType.Value);
                CameraPan.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");

                CameraZoom = new InputAction("CameraZoom", InputActionType.Value, "<Mouse>/scroll/y");
                CameraRotateEnable = new InputAction("CameraRotateEnable", InputActionType.Button, "<Mouse>/middleButton");
                CameraRotateDelta = new InputAction("CameraRotateDelta", InputActionType.Value, "<Mouse>/delta");
                MousePosition = new InputAction("MousePosition", InputActionType.Value, "<Mouse>/position");
                Select = new InputAction("Select", InputActionType.Button, "<Mouse>/leftButton");
                Command = new InputAction("Command", InputActionType.Button, "<Mouse>/rightButton");
                MultiSelect = new InputAction("MultiSelect", InputActionType.Button, "<Keyboard>/leftShift");
                DeselectAll = new InputAction("DeselectAll", InputActionType.Button, "<Keyboard>/escape");
                AttackMove = new InputAction("AttackMove", InputActionType.Button, KeybindManager.GetBinding("AttackMove"));
                DeleteEntity = new InputAction("DeleteEntity", InputActionType.Button, "<Keyboard>/x");

                ControlGroups = new InputAction[10];
                string[] digitBindings = { "<Keyboard>/0", "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3",
                                           "<Keyboard>/4", "<Keyboard>/5", "<Keyboard>/6", "<Keyboard>/7",
                                           "<Keyboard>/8", "<Keyboard>/9" };
                for (int i = 0; i < 10; i++)
                    ControlGroups[i] = new InputAction("ControlGroup" + i, InputActionType.Button, digitBindings[i]);
            }

            public void Enable()
            {
                CameraPan.Enable();
                CameraZoom.Enable();
                CameraRotateEnable.Enable();
                CameraRotateDelta.Enable();
                MousePosition.Enable();
                Select.Enable();
                Command.Enable();
                MultiSelect.Enable();
                DeselectAll.Enable();
                AttackMove.Enable();
                DeleteEntity.Enable();

                foreach (var a in ControlGroups) a.Enable();
            }

            public void Disable()
            {
                CameraPan.Disable();
                CameraZoom.Disable();
                CameraRotateEnable.Disable();
                CameraRotateDelta.Disable();
                MousePosition.Disable();
                Select.Disable();
                Command.Disable();
                MultiSelect.Disable();
                DeselectAll.Disable();
                AttackMove.Disable();
                DeleteEntity.Disable();

                foreach (var a in ControlGroups) a.Disable();
            }

            public void Dispose()
            {
                CameraPan?.Dispose();
                CameraZoom?.Dispose();
                CameraRotateEnable?.Dispose();
                CameraRotateDelta?.Dispose();
                MousePosition?.Dispose();
                Select?.Dispose();
                Command?.Dispose();
                MultiSelect?.Dispose();
                DeselectAll?.Dispose();
                AttackMove?.Dispose();
                DeleteEntity?.Dispose();

                if (ControlGroups != null)
                    foreach (var a in ControlGroups) a?.Dispose();
            }
        }
    }
}
