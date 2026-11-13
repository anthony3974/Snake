using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeonSnake
{
    /// <summary>
    /// Swipe detection for touch (and mouse drag / keyboard in the editor).
    /// Swipes fire mid-drag as soon as the finger has moved far enough, and the anchor resets,
    /// so you can chain turns without lifting your finger.
    /// </summary>
    public class SwipeInput
    {
        public event Action<Vector2Int> Swiped;
        public event Action Tapped;
        public event Action BackPressed;

        /// <summary>Presses that start above this screen y (the HUD) are ignored.</summary>
        public float IgnoreAboveY = float.MaxValue;

        bool tracking, swiped, waitForRelease;
        Vector2 anchor;
        float downTime;

        static float Threshold
        {
            get
            {
                float t = Screen.dpi > 0f ? Screen.dpi * 0.11f : Screen.width * 0.045f;
                return Mathf.Clamp(t, 20f, Mathf.Max(20f, Screen.width * 0.08f));
            }
        }

        /// <summary>Ignore the current touch until the finger lifts (used on menu/state changes).</summary>
        public void Reset()
        {
            tracking = false;
            waitForRelease = true;
        }

        public void Update()
        {
            ReadKeyboard();

            bool pressed = ReadPointer(out Vector2 pos);
            if (waitForRelease)
            {
                if (!pressed) waitForRelease = false;
                return;
            }

            if (pressed && !tracking)
            {
                if (pos.y > IgnoreAboveY)
                {
                    waitForRelease = true;
                    return;
                }
                tracking = true;
                swiped = false;
                anchor = pos;
                downTime = Time.unscaledTime;
                return;
            }

            if (!tracking) return;

            if (pressed)
            {
                Vector2 d = pos - anchor;
                float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
                float major = Mathf.Max(ax, ay), minor = Mathf.Min(ax, ay);
                float threshold = Threshold;
                bool clear = major >= threshold && major >= minor * 1.25f;
                if (clear || major >= threshold * 2.5f)
                {
                    Vector2Int dir = ax > ay
                        ? (d.x > 0f ? Vector2Int.right : Vector2Int.left)
                        : (d.y > 0f ? Vector2Int.up : Vector2Int.down);
                    swiped = true;
                    anchor = pos;
                    Swiped?.Invoke(dir);
                }
            }
            else
            {
                tracking = false;
                if (!swiped && Time.unscaledTime - downTime < 0.35f) Tapped?.Invoke();
            }
        }

        static bool ReadPointer(out Vector2 pos)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                pos = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                pos = mouse.position.ReadValue();
                return true;
            }
            pos = default;
            return false;
        }

        void ReadKeyboard()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Swiped?.Invoke(Vector2Int.up);
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Swiped?.Invoke(Vector2Int.down);
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Swiped?.Invoke(Vector2Int.left);
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Swiped?.Invoke(Vector2Int.right);
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) Tapped?.Invoke();
            // Escape is also the Android back button.
            if (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame) BackPressed?.Invoke();
        }
    }
}
