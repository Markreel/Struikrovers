using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// A non-castle frame cell. It shows one team stat, or nothing (filler).
    public sealed class FrameCellView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;

        private int team = -1;
        private FrameStat stat;
        private bool compact;

        public void Configure(int team, FrameStat stat, bool compact, VisualTheme theme)
        {
            this.team = team;
            this.stat = stat;
            this.compact = compact;

            gameObject.name = "BoardFrame";
            background.color = team >= 0 ? theme.GetFrameColor(team) : theme.FrameFillerColor;
            label.text = "";
        }

        public void Refresh(GameState state)
        {
            if (stat == FrameStat.None || team < 0 || team >= state.TeamCount)
            {
                label.text = "";
                return;
            }

            TeamState teamState = state.Teams[team];
            int number = team + 1;

            switch (stat)
            {
                case FrameStat.TeamName:
                    label.text = compact ? "P" + number : "TEAM " + number;
                    break;
                case FrameStat.Wood:
                    label.text = compact ? "W " + teamState.Wood : "WOOD\n" + teamState.Wood;
                    break;
                case FrameStat.Cards:
                    label.text = compact ? "C " + teamState.Cards : "CARDS\n" + teamState.Cards;
                    break;
            }
        }
    }
}