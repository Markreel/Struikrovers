using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// One playable tile. Draws its terrain, highlight, glow and pawn badges.
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private GlowPulse glow;
        [SerializeField] private RectTransform markerContainer;

        private readonly List<PawnBadgeView> badgePool = new List<PawnBadgeView>();
        private VisualTheme theme;
        private PawnBadgeView badgePrefab;

        public Vector2Int Position { get; private set; }

        public event Action<Vector2Int> Clicked;

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(Position));
        }

        public void Initialize(Vector2Int position, VisualTheme theme, PawnBadgeView badgePrefab)
        {
            Position = position;
            this.theme = theme;
            this.badgePrefab = badgePrefab;
            gameObject.name = $"Tile_{position.x}_{position.y}";
        }

        /// actionColor is only used for TileHighlight.ActionTarget (it depends on the selected group).
        public void Show(
            TerrainType terrain, TileHighlight highlight, Color actionColor,
            IReadOnlyList<PawnMarker> markers)
        {
            background.color = GetBackgroundColor(terrain, highlight, actionColor);

            if (glow != null)
            {
                bool glowing = theme.TryGetGlowColor(terrain, out Color glowColor);
                glow.gameObject.SetActive(glowing);
                if (glowing)
                    glow.SetColor(glowColor);
            }

            int count = markers != null ? markers.Count : 0;
            LayoutBadges(markers, count);

            if (count > 0)
            {
                label.text = "";
            }
            else
            {
                label.color = theme.TerrainLabelColor;
                label.text = theme.GetTerrainSymbol(terrain);
            }
        }

        private Color GetBackgroundColor(TerrainType terrain, TileHighlight highlight, Color actionColor)
        {
            switch (highlight)
            {
                case TileHighlight.Destination: return theme.DestinationColor;
                case TileHighlight.AbductionTarget: return theme.AbductionTargetColor;
                case TileHighlight.AttackTarget: return theme.AttackTargetColor;
                case TileHighlight.ActionTarget: return actionColor;
                case TileHighlight.QuickActionTarget: return theme.QuickActionColor;
                default: return theme.GetTerrainColor(terrain);
            }
        }

        // Badges fill a roughly square grid of anchored cells. The badge prefab
        // keeps itself square, so no pixel maths is needed.
        private void LayoutBadges(IReadOnlyList<PawnMarker> markers, int count)
        {
            while (badgePool.Count < count)
                badgePool.Add(Instantiate(badgePrefab, markerContainer));

            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));

            for (int i = 0; i < badgePool.Count; i++)
            {
                PawnBadgeView badge = badgePool[i];

                if (i >= count)
                {
                    badge.gameObject.SetActive(false);
                    continue;
                }

                badge.gameObject.SetActive(true);
                badge.Bind(markers[i], theme);

                int column = i % columns;
                int row = i / columns;

                var rect = (RectTransform)badge.transform;
                rect.anchorMin = new Vector2(column / (float)columns, 1f - (row + 1) / (float)rows);
                rect.anchorMax = new Vector2((column + 1) / (float)columns, 1f - row / (float)rows);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }
    }
}