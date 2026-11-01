using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    [Serializable]
    public sealed class CardCountOverride
    {
        [Tooltip("Card id, for example \"nab\" or \"greatsword\" (see CardCatalog).")]
        public string cardId;
        [Min(0)] public int copies = 1;
    }

    /// Everything about the deck you may want to change while playtesting.
    [Serializable]
    public sealed class DeckSettings
    {
        [Header("Copies of each card in the deck")]
        [Min(0)] public int handCardCopies = 3;
        [Min(0)] public int equipmentCopies = 1;
        [Min(0)] public int curseCopies = 1;
        [Tooltip("Overrides the copies of single cards.")]
        public List<CardCountOverride> overrides = new List<CardCountOverride>();

        [Header("Rules")]
        [Min(1)] public int maxHandSize = 5;
        [Tooltip("The Dragon card is shuffled in with the last N cards of the deck (the Dragon + N-1 others).")]
        [Min(1)] public int dragonBottomCount = 10;
        [Min(0)] public int endOfTurnDraws = 1;

        [Header("Debug")]
        [Tooltip("0 = random shuffle. Any other number gives the same shuffle every time.")]
        public int seed = 0;
        [Tooltip("Card ids placed on top of the deck, first = drawn first.")]
        public List<string> topCards = new List<string>();
        [Serializable]
        public sealed class DebugCurse
        {
            [Min(0)] public int team;
            [Tooltip("For example \"curse-drunk-squire\" (see CardCatalog).")]
            public string cardId;
        }

        // in DeckSettings, under [Header("Debug")]
        [Tooltip("Curses placed on teams when the game starts. For testing, until the curse cards work.")]
        public List<DebugCurse> debugCurses = new List<DebugCurse>();

        public int GetCopies(CardDefinition card)
        {
            if (overrides != null)
            {
                foreach (CardCountOverride entry in overrides)
                    if (entry != null && entry.cardId == card.Id)
                        return Mathf.Max(0, entry.copies);
            }

            switch (card.Kind)
            {
                case CardKind.Hand: return handCardCopies;
                case CardKind.Curse: return curseCopies;
                case CardKind.Equipment: return equipmentCopies;
                default: return 1;
            }
        }
    }
}