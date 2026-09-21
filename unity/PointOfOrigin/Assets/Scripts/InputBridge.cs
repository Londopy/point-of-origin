using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PointOfOrigin
{
    public enum GameAction { Left, Right, Jump, Plant, Grow, Rewind, Skip, Reveal, Mute, Menu, Confirm }

    /// <summary>
    /// Keyboard and mouse through the Input System, by action. Each action has
    /// a rebindable primary key (saved in PlayerPrefs) and fixed fallbacks
    /// (the arrows, W for jump, Enter for grow) so a bad rebind never locks
    /// the player out.
    /// </summary>
    public static class InputBridge
    {
        const string PrefPrefix = "po.key.";

        static readonly Dictionary<GameAction, Key[]> Defaults = new Dictionary<GameAction, Key[]>
        {
            { GameAction.Left, new[] { Key.A, Key.LeftArrow } },
            { GameAction.Right, new[] { Key.D, Key.RightArrow } },
            { GameAction.Jump, new[] { Key.Space, Key.W, Key.UpArrow } },
            { GameAction.Plant, new[] { Key.E } },
            { GameAction.Grow, new[] { Key.Enter, Key.G } },
            { GameAction.Rewind, new[] { Key.R } },
            { GameAction.Skip, new[] { Key.N } },
            { GameAction.Reveal, new[] { Key.V } },
            { GameAction.Mute, new[] { Key.M } },
            { GameAction.Menu, new[] { Key.Escape } },
            { GameAction.Confirm, new[] { Key.Enter, Key.Space } },
        };

        /// <summary>Actions the player may rebind, in the order the Controls page lists them. Menu stays on Esc so nobody locks themselves out.</summary>
        public static readonly GameAction[] Rebindable =
        {
            GameAction.Left, GameAction.Right, GameAction.Jump, GameAction.Plant, GameAction.Grow,
            GameAction.Rewind, GameAction.Skip, GameAction.Reveal, GameAction.Mute,
        };

        static readonly Dictionary<GameAction, Key> primary = new Dictionary<GameAction, Key>();

        public static void Load()
        {
            foreach (var kv in Defaults)
            {
                var saved = PlayerPrefs.GetString(PrefPrefix + kv.Key, "");
                primary[kv.Key] = saved.Length > 0 && Enum.TryParse(saved, out Key k) && k != Key.None ? k : kv.Value[0];
            }
        }

        public static void Bind(GameAction action, Key key)
        {
            primary[action] = key;
            PlayerPrefs.SetString(PrefPrefix + action, key.ToString());
            PlayerPrefs.Save();
        }

        public static void ResetBindings()
        {
            foreach (var kv in Defaults)
            {
                primary[kv.Key] = kv.Value[0];
                PlayerPrefs.DeleteKey(PrefPrefix + kv.Key);
            }
            PlayerPrefs.Save();
        }

        public static Key Primary(GameAction action) => primary.TryGetValue(action, out var k) ? k : Defaults[action][0];

        /// <summary>Every key that triggers the action: the rebindable primary plus the fixed fallbacks.</summary>
        static IEnumerable<Key> Keys(GameAction action)
        {
            yield return Primary(action);
            var d = Defaults[action];
            for (int i = 1; i < d.Length; i++) yield return d[i];
        }

        public static string Describe(GameAction action)
        {
            switch (action)
            {
                case GameAction.Left: return "Run left";
                case GameAction.Right: return "Run right";
                case GameAction.Jump: return "Jump";
                case GameAction.Plant: return "Plant or take back a seed";
                case GameAction.Grow: return "Grow";
                case GameAction.Rewind: return "Rewind the growth";
                case GameAction.Skip: return "Skip the chapter";
                case GameAction.Reveal: return "Reveal the origins";
                case GameAction.Mute: return "Sound on or off";
                case GameAction.Menu: return "Menu";
                default: return action.ToString();
            }
        }

        /// <summary>A short human name for a key, for the HUD and the Controls page.</summary>
        public static string Label(Key key)
        {
            switch (key)
            {
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Escape: return "Esc";
                case Key.LeftArrow: return "Left";
                case Key.RightArrow: return "Right";
                case Key.UpArrow: return "Up";
                case Key.DownArrow: return "Down";
                case Key.LeftShift: return "L Shift";
                case Key.RightShift: return "R Shift";
                case Key.LeftCtrl: return "L Ctrl";
                case Key.RightCtrl: return "R Ctrl";
                case Key.LeftAlt: return "L Alt";
                case Key.RightAlt: return "R Alt";
                case Key.Tab: return "Tab";
                case Key.Backspace: return "Backspace";
            }
            var name = key.ToString();
            if (name.StartsWith("Digit")) return name.Substring(5);
            if (name.StartsWith("Numpad")) return "Num " + name.Substring(6);
            return name;
        }

        public static string Label(GameAction action) => Label(Primary(action));

        static bool KeyDown(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            try { return kb[key].wasPressedThisFrame; } catch (ArgumentException) { return false; }
        }

        static bool KeyHeld(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null || key == Key.None) return false;
            try { return kb[key].isPressed; } catch (ArgumentException) { return false; }
        }

        // ------------------------------------------------------------------ gamepad: a fixed layout

        /// <summary>The pad's buttons for an action; the sticks and d-pad handle Left and Right.</summary>
        static IEnumerable<ButtonControl> PadButtons(Gamepad pad, GameAction action)
        {
            switch (action)
            {
                case GameAction.Jump: yield return pad.buttonSouth; break;
                case GameAction.Plant: yield return pad.buttonWest; break;
                case GameAction.Grow: yield return pad.buttonEast; break;
                case GameAction.Confirm: yield return pad.buttonSouth; yield return pad.buttonEast; yield return pad.startButton; break;
                case GameAction.Rewind: yield return pad.buttonNorth; break;
                case GameAction.Reveal: yield return pad.leftShoulder; break;
                case GameAction.Skip: yield return pad.rightShoulder; break;
                case GameAction.Menu: yield return pad.startButton; break;
                case GameAction.Left: yield return pad.dpad.left; break;
                case GameAction.Right: yield return pad.dpad.right; break;
            }
        }

        public static string PadLabel(GameAction action)
        {
            switch (action)
            {
                case GameAction.Left: case GameAction.Right: return "stick or d-pad";
                case GameAction.Jump: return "A";
                case GameAction.Plant: return "X";
                case GameAction.Grow: return "B";
                case GameAction.Rewind: return "Y";
                case GameAction.Reveal: return "LB";
                case GameAction.Skip: return "RB";
                case GameAction.Menu: return "Start";
                case GameAction.Mute: return "(settings)";
                default: return "";
            }
        }

        public static bool PadPresent => Gamepad.current != null;

        static bool PadPressed(GameAction action)
        {
            var pad = Gamepad.current;
            if (pad == null) return false;
            foreach (var b in PadButtons(pad, action)) if (b.wasPressedThisFrame) return true;
            if (action == GameAction.Left) return pad.leftStick.left.wasPressedThisFrame;
            if (action == GameAction.Right) return pad.leftStick.right.wasPressedThisFrame;
            return false;
        }

        static bool PadHeld(GameAction action)
        {
            var pad = Gamepad.current;
            if (pad == null) return false;
            foreach (var b in PadButtons(pad, action)) if (b.isPressed) return true;
            if (action == GameAction.Left) return pad.leftStick.ReadValue().x < -0.45f;
            if (action == GameAction.Right) return pad.leftStick.ReadValue().x > 0.45f;
            return false;
        }

        /// <summary>Menu navigation on a pad: -1 up, +1 down, 0 nothing, this frame.</summary>
        public static int PadMenuStep()
        {
            var pad = Gamepad.current;
            if (pad == null) return 0;
            if (pad.dpad.up.wasPressedThisFrame || pad.leftStick.up.wasPressedThisFrame) return -1;
            if (pad.dpad.down.wasPressedThisFrame || pad.leftStick.down.wasPressedThisFrame) return 1;
            return 0;
        }

        public static int PadMenuSide()
        {
            var pad = Gamepad.current;
            if (pad == null) return 0;
            if (pad.dpad.left.wasPressedThisFrame || pad.leftStick.left.wasPressedThisFrame) return -1;
            if (pad.dpad.right.wasPressedThisFrame || pad.leftStick.right.wasPressedThisFrame) return 1;
            return 0;
        }

        public static bool PadActivate()
        {
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
        }

        public static bool PadBack()
        {
            var pad = Gamepad.current;
            return pad != null && (pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame);
        }

        public static bool Pressed(GameAction action)
        {
            foreach (var k in Keys(action)) if (KeyDown(k)) return true;
            return PadPressed(action);
        }

        public static bool Held(GameAction action)
        {
            foreach (var k in Keys(action)) if (KeyHeld(k)) return true;
            return PadHeld(action);
        }

        /// <summary>The first key pressed this frame, for rebinding: any key counts except None.</summary>
        public static bool AnyKeyPressed(out Key key)
        {
            key = Key.None;
            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var control in kb.allKeys)
            {
                if (control.wasPressedThisFrame && control.keyCode != Key.None)
                {
                    key = control.keyCode;
                    return true;
                }
            }
            return false;
        }

        public static bool ShiftHeld => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool MouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool RightClicked => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }
}
