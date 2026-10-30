using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// One pawn token: a team-coloured badge with an icon (or an initial as fallback).
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

            Sprite icon = theme.GetPawnSprite(marker.Type);
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);

            fallbackLabel.gameObject.SetActive(icon == null);
            if (icon == null)
                fallbackLabel.text = theme.GetPawnInitial(marker.Type) + (marker.Team + 1);

            captiveLabel.gameObject.SetActive(marker.IsCaptive);
            gameObject.name = "PawnBadge_T" + (marker.Team + 1);
        }
    }
}