using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// A scrollable list of the discard pile, newest card first. The discard pile is public.
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

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
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
            if (IsOpen)
            {
                Hide();
                return;
            }

            panel.SetActive(true);
            Refresh(discard);
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        public void Refresh(IReadOnlyList<CardDefinition> discard)
        {
            if (!IsOpen)
                return;

            titleLabel.text = $"Discard pile ({discard.Count})";

            while (pool.Count < discard.Count)
            {
                CardView card = Instantiate(cardPrefab, content);
                card.Setup(theme, detail);
                pool.Add(card);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                bool used = i < discard.Count;
                pool[i].gameObject.SetActive(used);

                if (used)
                    pool[i].ShowCard(discard[discard.Count - 1 - i], true);
            }
        }
    }
}