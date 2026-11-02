using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Struikrovers.Game
{
    /// Keyboard input. Clicks are handled by the UI buttons themselves.
    public sealed class InputHandler : MonoBehaviour
    {
        /// Spacebar: same as pressing the main button. TurnManager ignores it
        /// when the main button is disabled.
        public event Action PrimaryPressed;

        /// Escape: closes an open menu, or cancels (same as the Cancel button).
        public event Action CancelPressed;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame)
                PrimaryPressed?.Invoke();

            if (keyboard.escapeKey.wasPressedThisFrame)
                CancelPressed?.Invoke();
        }
    }
}