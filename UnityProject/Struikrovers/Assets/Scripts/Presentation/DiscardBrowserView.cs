using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// A scrollable list of the discard pile, newest card first. The discard pile is public.
    /// In pick mode (One Man's Trash) the cards that can be taken glow and can be clicked.
    public sealed class DiscardBrowserView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private CardView cardPrefab;

        private readonly List<CardView> pool = new List<CardView>();
        private VisualTheme theme;
        private CardDetailView detail;

        private int shownCount;
        private bool picking;
        private string pickTitle;
        private Func<int, bool> canPick;   // by position in the discard pile (0 = bottom)
        private Action<int> onPick;
        private Action onCancel;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsPicking => picking;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
            if (panel != null) panel.SetActive(false);
        }

        public void Initialize(VisualTheme theme, CardDetailView detail)
        {
            this.theme = theme;
            this.detail = detail;

            foreach (CardView card in pool)
                card.Setup(theme, detail);
        }

        public void Toggle(IReadOnlyList<CardDefinition> discard)
        {
            // While picking, the browser can only be closed by cancelling.
            if (picking)
                return;

            if (IsOpen)
            {
                Hide();
                return;
            }

            panel.SetActive(true);
            Refresh(discard);
        }

        /// Opens the browser so the player can click one card. canPick, onPick and the positions
        /// use the discard pile's own order (0 = bottom). Closing the browser counts as cancel.
        public void BeginPick(
            string title, IReadOnlyList<CardDefinition> discard,
            Func<int, bool> canPick, Action<int> onPick, Action onCancel)
        {
            picking = true;
            pickTitle = title;
            this.canPick = canPick;
            this.onPick = onPick;
            this.onCancel = onCancel;

            panel.SetActive(true);
            Refresh(discard);
        }

        /// Leaves pick mode and closes the browser. Does nothing when not picking.
        public void EndPick()
        {
            if (picking)
                Hide();
        }

        public void Hide()
        {
            picking = false;
            canPick = null;
            onPick = null;
            onCancel = null;

            if (panel != null)
                panel.SetActive(false);
        }

        private void OnCloseClicked()
        {
            if (picking) onCancel?.Invoke();
            else Hide();
        }

        public void Refresh(IReadOnlyList<CardDefinition> discard)
        {
            if (!IsOpen)
                return;

            shownCount = discard.Count;
            titleLabel.text = picking
                ? $"{pickTitle} ({discard.Count})"
                : $"Discard pile ({discard.Count})";

            while (pool.Count < discard.Count)
            {
                CardView card = Instantiate(cardPrefab, content);
                card.Setup(theme, detail);
                card.Clicked += OnCardClicked;
                pool.Add(card);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                bool used = i < discard.Count;
                pool[i].gameObject.SetActive(used);

                if (!used)
                    continue;

                int pileIndex = discard.Count - 1 - i;
                pool[i].ShowCard(discard[pileIndex], true);
                pool[i].SetSelectable(picking && canPick != null && canPick(pileIndex));
            }
        }

        private void OnCardClicked(CardView view)
        {
            if (!picking)
                return;

            int poolIndex = pool.IndexOf(view);
            if (poolIndex < 0 || poolIndex >= shownCount)
                return;

            int pileIndex = shownCount - 1 - poolIndex;
            if (canPick != null && canPick(pileIndex))
                onPick?.Invoke(pileIndex);
        }
    }
}