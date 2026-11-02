using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// One card, an empty slot, or a face-down card. A face-down card never keeps a
    /// reference to its definition, so hidden information cannot leak through the view.
    public sealed class CardView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text modifierLabel;
        [SerializeField] private TMP_Text placeholderLabel;
        [SerializeField] private Outline playableGlow;
        private bool playable;
        private bool selectable;
        private bool glowing;

        private VisualTheme theme;
        private CardDetailView detail;
        private bool hovering;

        /// The card shown face up, or null for a back or an empty slot.
        public CardDefinition Card { get; private set; }

        public event Action<CardView> Clicked;

        /// detail can be null, then hovering does nothing.
        public void Setup(VisualTheme theme, CardDetailView detail)
        {
            this.theme = theme;
            this.detail = detail;
        }

        public void ShowCard(CardDefinition card, bool faceUp)
        {
            if (card == null) { ShowEmpty(""); return; }
            if (!faceUp) { ShowBack(); return; }

            Card = card;
            background.color = theme.GetCardColor(card);

            nameLabel.gameObject.SetActive(true);
            nameLabel.text = card.Name;

            bool showModifier = card.IsEquipment && card.Modifier != 0;
            modifierLabel.gameObject.SetActive(showModifier);
            if (showModifier)
                modifierLabel.text = card.Modifier.ToString("+0;-0");

            placeholderLabel.gameObject.SetActive(false);
            UpdateHover();
        }

        public void ShowBack(string text = "?") => ShowPlaceholder(theme.CardBackColor, text);

        public void ShowEmpty(string text) => ShowPlaceholder(theme.EmptySlotColor, text);

        private void ShowPlaceholder(Color color, string text)
        {
            SetPlayable(false);

            Card = null;
            background.color = color;

            nameLabel.gameObject.SetActive(false);
            modifierLabel.gameObject.SetActive(false);

            placeholderLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            placeholderLabel.text = text ?? "";
            UpdateHover();
        }

        // ---- Pointer ----

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            UpdateHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (detail != null) detail.Hide();
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

        private void OnDisable()
        {
            // Pooled cards get deactivated under the cursor; no exit event follows.
            if (!hovering)
                return;

            hovering = false;
            if (detail != null) detail.Hide();
        }

        private void UpdateHover()
        {
            if (!hovering || detail == null)
                return;

            if (Card != null) detail.Show(Card, theme);
            else detail.Hide();
        }

        public void SetPlayable(bool value)
        {
            playable = value;
            ApplyGlow();
        }

        /// Highlighted while the player has to pick this card (SelectionLayer).
        public void SetSelectable(bool value)
        {
            selectable = value;
            ApplyGlow();
        }

        private void ApplyGlow()
        {
            glowing = playable || selectable;

            if (playableGlow != null)
                playableGlow.enabled = glowing;
        }

        private void Update()
        {
            if (!glowing || playableGlow == null || theme == null)
                return;

            float pulse = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) / 2f;
            Color color = theme.PlayableGlowColor;
            color.a = Mathf.Lerp(0.35f, 1f, pulse);
            playableGlow.effectColor = color;
        }
    }
}