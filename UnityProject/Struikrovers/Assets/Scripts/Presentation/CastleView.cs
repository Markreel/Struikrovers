using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// A castle cell in the frame. It is a real board position, so it is clickable.
    public sealed class CastleView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        private VisualTheme theme;

        public int Team { get; private set; }
        public Vector2Int Position { get; private set; }

        public event Action<Vector2Int> Clicked;

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(Position));
        }

        public void Initialize(int team, Vector2Int position, VisualTheme theme)
        {
            Team = team;
            Position = position;
            this.theme = theme;

            gameObject.name = $"Castle_Team_{team + 1}";
            label.text = "CASTLE\nP" + (team + 1);
            SetHighlighted(false);
        }

        public void SetHighlighted(bool highlighted)
        {
            background.color = highlighted
                ? theme.DestinationColor
                : theme.GetCastleColor(Team);
        }
    }
}