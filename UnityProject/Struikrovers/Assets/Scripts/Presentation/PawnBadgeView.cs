using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// One token: a team-coloured badge with an icon (or an initial as fallback).
    public sealed class PawnBadgeView : MonoBehaviour
    {
        [SerializeField] private Image badgeImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text fallbackLabel;
        [SerializeField] private TMP_Text captiveLabel;

        public void Bind(PawnMarker marker, VisualTheme theme)
        {
            badgeImage.sprite = theme.BadgeSprite;
            badgeImage.color = theme.GetTeamColor(marker.Team);

            // The Chalice has no icon yet: it shows a "C1" placeholder label.
            // Later, assign the card's own icon here.
            Sprite icon = marker.IsChalice ? null : theme.GetPawnSprite(marker.Type);
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);

            fallbackLabel.gameObject.SetActive(icon == null);
            if (icon == null)
            {
                fallbackLabel.text = marker.IsChalice
                    ? "C" + (marker.Team + 1)
                    : theme.GetPawnInitial(marker.Type) + (marker.Team + 1);
            }

            captiveLabel.gameObject.SetActive(marker.IsCaptive);
            gameObject.name = (marker.IsChalice ? "ChaliceMarker_T" : "PawnBadge_T") + (marker.Team + 1);
        }
    }
}