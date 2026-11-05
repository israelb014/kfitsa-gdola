using UnityEngine;
using UnityEngine.InputSystem;

namespace Kfitsa
{
    public struct Pad
    {
        public float x;
        public bool down, run, runPressed, jumpHeld, jumpPressed;
    }

    /// Keyboard, gamepad and touch, read straight from the devices. Stick input is digital, like a D-pad.
    public static class GameInput
    {
        public static Pad Read()
        {
            var p = new Pad();
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) p.x -= 1;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) p.x += 1;
                p.down |= k.downArrowKey.isPressed || k.sKey.isPressed;
                p.run |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                p.runPressed |= k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame;
                p.jumpHeld |= k.spaceKey.isPressed;
                p.jumpPressed |= k.spaceKey.wasPressedThisFrame;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue() + g.dpad.ReadValue();
                if (Mathf.Abs(s.x) > 0.35f) p.x += Mathf.Sign(s.x);
                p.down |= s.y < -0.6f;
                p.run |= g.buttonWest.isPressed || g.buttonNorth.isPressed || g.rightTrigger.isPressed || g.rightShoulder.isPressed;
                p.runPressed |= g.buttonWest.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame;
                p.jumpHeld |= g.buttonSouth.isPressed || g.buttonEast.isPressed;
                p.jumpPressed |= g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame;
            }
            TouchUi.Current?.Merge(ref p);
            p.x = Mathf.Clamp(p.x, -1, 1);
            return p;
        }

        static bool systemBack;

        /// The Android Back button, sent by the GameMain activity. Whichever screen reads it first uses it.
        public static void SystemBack() => systemBack = true;
        static bool TakeSystemBack() { bool b = systemBack; systemBack = false; return b; }

        public static bool PausePressed => TakeSystemBack() || Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            || Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame
            || TouchUi.Current != null && TouchUi.Current.PausePressed;

        static float nextRepeat;
        static int heldX, heldY;

        /// Menu navigation: one step per press, with auto-repeat while held.
        public static (int x, int y, bool ok, bool back) Menu()
        {
            var k = Keyboard.current; var g = Gamepad.current;
            int x = 0, y = 0; bool ok = false, back = false;
            if (k != null)
            {
                if (k.leftArrowKey.isPressed || k.aKey.isPressed) x -= 1;
                if (k.rightArrowKey.isPressed || k.dKey.isPressed) x += 1;
                if (k.upArrowKey.isPressed || k.wKey.isPressed) y += 1;
                if (k.downArrowKey.isPressed || k.sKey.isPressed) y -= 1;
                ok |= k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame;
                back |= k.escapeKey.wasPressedThisFrame;
            }
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue() + g.dpad.ReadValue();
                if (Mathf.Abs(s.x) > .5f) x += (int)Mathf.Sign(s.x);
                if (Mathf.Abs(s.y) > .5f) y += (int)Mathf.Sign(s.y);
                ok |= g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame;
                back |= g.buttonEast.wasPressedThisFrame;
            }
            back |= TakeSystemBack();
            x = Mathf.Clamp(x, -1, 1); y = Mathf.Clamp(y, -1, 1);
            float now = Time.unscaledTime;
            bool changed = x != heldX || y != heldY;
            heldX = x; heldY = y;
            if (changed) nextRepeat = now + .35f;
            else if ((x != 0 || y != 0) && now >= nextRepeat) nextRepeat = now + .12f;
            else { x = 0; y = 0; }
            return (x, y, ok, back);
        }
    }
}
