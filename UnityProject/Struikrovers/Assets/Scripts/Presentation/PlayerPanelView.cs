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
        [SerializeField] private DiceStripView diceStrip;
        public DiceStripView Dice => diceStrip;
        private readonly List<CardView> handCards = new List<CardView>();
        private VisualTheme theme;
        private CardDetailView detail;
        private int team;

        /// A face-up card of this team was clicked (equipment or a card in hand).
        public event Action<CardClick> CardClicked;

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
                Listen(row.clothing);
                Listen(row.weapon);
            }

            foreach (CardView slot in curseSlots)
                slot.Setup(theme, detail);

            foreach (CardView card in handCards)
            {
                card.Setup(theme, detail);
                Listen(card);
            }

            if (diceStrip != null)
                diceStrip.Initialize(theme);
        }

        public void Refresh(GameState state, int maxHandSize, bool handFaceUp, bool isActive, Predicate<CardDefinition> isPlayable = null)
        {
            TeamState stats = state.Teams[team];

            if (activeMarker != null)
                activeMarker.SetActive(isActive);

            summaryLabel.text = $"Wood {stats.Wood}  ·  Hand {stats.Cards}/{maxHandSize}";

            RefreshEquipment(state);
            RefreshCurses(stats);
            RefreshHand(stats, handFaceUp, isPlayable);
        }

        // Unsubscribing first means Initialize can run again (reload) without double clicks.
        private void Listen(CardView view)
        {
            view.Clicked -= OnCardViewClicked;
            view.Clicked += OnCardViewClicked;
        }

        private void OnCardViewClicked(CardView view)
        {
            // Empty slots and face-down cards have no card to act on.
            if (view.Card == null)
                return;

            int handIndex = handCards.IndexOf(view);
            if (handIndex >= 0)
            {
                CardClicked?.Invoke(CardClick.Hand(team, handIndex, view.Card));
                return;
            }

            for (int i = 0; i < pawnRows.Length && i < GameConfig.TurnOrder.Length; i++)
            {
                PawnType role = GameConfig.TurnOrder[i];

                if (view == pawnRows[i].clothing)
                {
                    CardClicked?.Invoke(CardClick.Equipment(team, role, EquipmentSlot.Clothing, view.Card));
                    return;
                }

                if (view == pawnRows[i].weapon)
                {
                    CardClicked?.Invoke(CardClick.Equipment(team, role, EquipmentSlot.Weapon, view.Card));
                    return;
                }
            }
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
                row.label.color = theme.GetRoleColor(role);

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

        private void RefreshHand(TeamState stats, bool faceUp, Predicate<CardDefinition> isPlayable)
        {
            IReadOnlyList<CardDefinition> hand = stats.Hand;

            while (handCards.Count < hand.Count)
            {
                CardView card = Instantiate(cardPrefab, handContainer);
                card.Setup(theme, detail);
                Listen(card);
                handCards.Add(card);
            }

            for (int i = 0; i < handCards.Count; i++)
            {
                bool used = i < hand.Count;
                handCards[i].gameObject.SetActive(used);

                if (used)
                {
                    handCards[i].ShowCard(hand[i], faceUp);
                    handCards[i].SetPlayable(faceUp && isPlayable != null && isPlayable(hand[i]));
                }
            }
        }
    }
}