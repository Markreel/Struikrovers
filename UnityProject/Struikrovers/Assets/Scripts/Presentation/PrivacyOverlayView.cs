using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// Full-screen "pass the device" screen. It hides the board and every hand
    /// until the next player clicks Reveal.
    public sealed class PrivacyOverlayView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private Button revealButton;

        public event Action RevealClicked;

        public bool IsVisible => panel != null && panel.activeSelf;

        private void Awake()
        {
            revealButton.onClick.AddListener(() => RevealClicked?.Invoke());
            panel.SetActive(false);
        }

        public void Show(int team, Color teamColor)
        {
            panel.SetActive(true);
            background.color = new Color(teamColor.r, teamColor.g, teamColor.b, 1f);
            titleLabel.text = $"Pass the device to Player {team + 1}";
            subtitleLabel.text = $"Only Player {team + 1} should look at the screen. Click Reveal when ready.";
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }
}