using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// Status text, the main button, Cancel and the hide-Bandits toggle.
    /// It only displays what it is told and raises events for input.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private Image statusBackground;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Toggle hideBanditsToggle;

        public event Action PrimaryClicked;
        public event Action CancelClicked;
        public event Action<bool> HideBanditsChanged;

        private void Awake()
        {
            primaryButton.onClick.AddListener(() => PrimaryClicked?.Invoke());
            cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());

            if (hideBanditsToggle != null)
                hideBanditsToggle.onValueChanged.AddListener(value => HideBanditsChanged?.Invoke(value));
        }

        public void SetHideBandits(bool value)
        {
            if (hideBanditsToggle != null)
                hideBanditsToggle.SetIsOnWithoutNotify(value);
        }

        public void Show(
            string message, Color backgroundColor,
            string primaryText, bool primaryInteractable, bool cancelVisible)
        {
            statusText.text = message;
            statusBackground.color = backgroundColor;
            primaryLabel.text = primaryText;
            primaryButton.interactable = primaryInteractable;
            cancelButton.gameObject.SetActive(cancelVisible);
        }
    }
}