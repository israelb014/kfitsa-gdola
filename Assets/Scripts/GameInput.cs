using UnityEngine;
using UnityEngine.InputSystem;

namespace Kfitsa
{
    public struct Pad
    {
        public float x;
        public bool down, run, jumpHeld, jumpPressed;
    }

    /// Keyboard and gamepad, read straight from the devices. Stick input is digital, like a D-pad.
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
                p.jumpHeld |= g.buttonSouth.isPressed || g.buttonEast.isPressed;
                p.jumpPressed |= g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame;
            }
            p.x = Mathf.Clamp(p.x, -1, 1);
            return p;
        }

        public static bool RestartPressed => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame
            || Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
        public static bool QuitPressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
