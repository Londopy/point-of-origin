using UnityEngine;
using UnityEngine.InputSystem;

namespace PointOfOrigin
{
    /// <summary>
    /// Mouse and keyboard through whichever input backend the project has active:
    /// the Input System package when its devices exist, the legacy manager otherwise.
    /// </summary>
    public static class InputBridge
    {
        static bool NewInput => Mouse.current != null;

        public static Vector2 MousePosition =>
            NewInput ? Mouse.current.position.ReadValue() : (Vector2)Input.mousePosition;

        public static bool Clicked =>
            NewInput ? Mouse.current.leftButton.wasPressedThisFrame : Input.GetMouseButtonDown(0);

        public static bool RightClicked =>
            NewInput ? Mouse.current.rightButton.wasPressedThisFrame : Input.GetMouseButtonDown(1);

        public static bool Pressed(Key key, KeyCode legacy)
        {
            if (NewInput)
                return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
            return Input.GetKeyDown(legacy);
        }

        public static bool Held(Key key, KeyCode legacy)
        {
            if (NewInput)
                return Keyboard.current != null && Keyboard.current[key].isPressed;
            return Input.GetKey(legacy);
        }
    }
}
