using System;
using TMPro;
using UnityEngine;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// The draw pile (a card back and a count) and the discard pile (top card face up).
    public sealed class PilesView : MonoBehaviour
    {
        [SerializeField] private CardView drawPile;
        [SerializeField] private TMP_Text drawCount;
        [SerializeField] private CardView discardPile;
        [SerializeField] private TMP_Text discardCount;

        public event Action DiscardClicked;

        private void Awake()
        {
            discardPile.Clicked += _ => DiscardClicked?.Invoke();
        }

        public void Initialize(VisualTheme theme, CardDetailView detail)
        {
            drawPile.Setup(theme, null); // the top of the draw pile is hidden
            discardPile.Setup(theme, detail);
        }

        public void Refresh(CardSystem cards)
        {
            if (cards.DrawPileCount > 0) drawPile.ShowBack("DRAW");
            else drawPile.ShowEmpty("EMPTY");

            drawCount.text = "Draw pile: " + cards.DrawPileCount;

            CardDefinition top = cards.TopOfDiscard;
            if (top != null) discardPile.ShowCard(top, true);
            else discardPile.ShowEmpty("Discard");

            discardCount.text = "Discard pile: " + cards.DiscardPile.Count;
        }
    }
}