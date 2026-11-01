using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// One team's panel: equipment per pawn, hand and curses.
    /// A hand that is not face up is shown as card backs.
    public sealed class PlayerPanelView : MonoBehaviour
    {
        /// Wired per pawn in the prefab. The order is GameConfig.TurnOrder: Royalty, Squire, Bandit.
        [Serializable]
        public sealed class PawnRow
        {
            public TMP_Text label;
            public CardView clothing;
            public CardView weapon;
            public TMP_Text modifier;
        }

        [SerializeField] private Image background;
        [SerializeField] private GameObject activeMarker;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private PawnRow[] pawnRows;
        [SerializeField] private CardView[] curseSlots;
        [SerializeField] private RectTransform handContainer;
        [SerializeField] private CardView cardPrefab;

        private readonly List<CardView> handCards = new List<CardView>();
        private VisualTheme theme;
        private CardDetailView detail;
        private int team;

        public void Initialize(int team, VisualTheme theme, CardDetailView detail)
        {
            this.team = team;
            this.theme = theme;
            this.detail = detail;

            background.color = theme.GetFrameColor(team);
            titleLabel.text = "PLAYER " + (team + 1);

            foreach (PawnRow row in pawnRows)
            {
                row.clothing.Setup(theme, detail);
                row.weapon.Setup(theme, detail);
            }

            foreach (CardView slot in curseSlots)
                slot.Setup(theme, detail);

            foreach (CardView card in handCards)
                card.Setup(theme, detail);
        }

        public void Refresh(GameState state, int maxHandSize, bool handFaceUp, bool isActive)
        {
            TeamState stats = state.Teams[team];

            if (activeMarker != null)
                activeMarker.SetActive(isActive);

            summaryLabel.text = $"Wood {stats.Wood}  ·  Hand {stats.Cards}/{maxHandSize}";

            RefreshEquipment(state);
            RefreshCurses(stats);
            RefreshHand(stats, handFaceUp);
        }

        private void RefreshEquipment(GameState state)
        {
            for (int i = 0; i < pawnRows.Length && i < GameConfig.TurnOrder.Length; i++)
            {
                PawnRow row = pawnRows[i];
                PawnType role = GameConfig.TurnOrder[i];
                Pawn pawn = state.GetPawnByRole(team, role);

                // A Royalty keeps its equipment while it is a Bloodhound.
                bool asBloodhound = pawn != null && pawn.Type != pawn.BaseType;
                row.label.text = asBloodhound ? role + " (Bloodhound)" : role.ToString();

                row.clothing.ShowCard(pawn?.Equipment.Clothing, true);
                if (pawn == null || pawn.Equipment.Clothing == null)
                    row.clothing.ShowEmpty("Clothing");

                row.weapon.ShowCard(pawn?.Equipment.Weapon, true);
                if (pawn == null || pawn.Equipment.Weapon == null)
                    row.weapon.ShowEmpty("Weapon");

                int modifier = pawn != null ? pawn.Equipment.CombatModifier : 0;
                row.modifier.text = "Combat " + modifier.ToString("+0;-0;0");
            }
        }

        private void RefreshCurses(TeamState stats)
        {
            for (int i = 0; i < curseSlots.Length; i++)
            {
                if (i < stats.Curses.Count) curseSlots[i].ShowCard(stats.Curses[i], true);
                else curseSlots[i].ShowEmpty("Curse");
            }
        }

        private void RefreshHand(TeamState stats, bool faceUp)
        {
            IReadOnlyList<CardDefinition> hand = stats.Hand;

            while (handCards.Count < hand.Count)
            {
                CardView card = Instantiate(cardPrefab, handContainer);
                card.Setup(theme, detail);
                handCards.Add(card);
            }

            for (int i = 0; i < handCards.Count; i++)
            {
                bool used = i < hand.Count;
                handCards[i].gameObject.SetActive(used);

                if (used)
                    handCards[i].ShowCard(hand[i], faceUp);
            }
        }
    }
}