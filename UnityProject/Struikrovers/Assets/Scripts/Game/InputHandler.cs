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

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                PrimaryPressed?.Invoke();
        }
    }
}