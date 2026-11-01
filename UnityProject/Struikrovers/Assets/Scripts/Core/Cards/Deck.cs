using System;
using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The draw pile. Index 0 is the bottom of the pile and the last element is the top.
    public sealed class Deck
    {
        private readonly List<CardDefinition> cards = new List<CardDefinition>();

        public int Count => cards.Count;

        /// Bottom to top. For debugging and the debug panel.
        public IReadOnlyList<CardDefinition> Cards => cards;

        public static Deck Build(DeckSettings settings, Random random)
        {
            var main = new List<CardDefinition>();

            foreach (CardDefinition card in CardCatalog.All)
            {
                if (card.Kind == CardKind.Dragon)
                    continue;

                int copies = settings.GetCopies(card);
                for (int i = 0; i < copies; i++)
                    main.Add(card);
            }

            Shuffle(main, random);

            // The Dragon is shuffled in with the cards at the very bottom.
            int others = Math.Min(Math.Max(0, settings.dragonBottomCount - 1), main.Count);
            List<CardDefinition> bottom = main.GetRange(main.Count - others, others);
            main.RemoveRange(main.Count - others, others);
            bottom.Add(CardCatalog.Dragon);
            Shuffle(bottom, random);

            var deck = new Deck();
            deck.cards.AddRange(bottom);
            deck.cards.AddRange(main);

            // Debug: force the first draws.
            if (settings.topCards != null)
            {
                for (int i = settings.topCards.Count - 1; i >= 0; i--)
                {
                    CardDefinition forced = CardCatalog.Find(settings.topCards[i]);
                    if (forced != null)
                        deck.PutOnTop(forced);
                    else
                        UnityEngine.Debug.LogWarning($"DeckSettings: unknown card id '{settings.topCards[i]}'.");
                }
            }

            return deck;
        }

        public bool TryDraw(out CardDefinition card)
        {
            if (cards.Count == 0)
            {
                card = null;
                return false;
            }

            card = cards[cards.Count - 1];
            cards.RemoveAt(cards.Count - 1);
            return true;
        }

        /// Moves a card of this kind to the top. If the deck holds none, a new copy is added.
        public void PutOnTop(CardDefinition card)
        {
            int index = cards.LastIndexOf(card);
            if (index >= 0)
                cards.RemoveAt(index);

            cards.Add(card);
        }

        /// For cards like One Man's Trash. Returns false when the card is not in the deck.
        public bool Remove(CardDefinition card)
        {
            int index = cards.LastIndexOf(card);
            if (index < 0)
                return false;

            cards.RemoveAt(index);
            return true;
        }

        private static void Shuffle(List<CardDefinition> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}