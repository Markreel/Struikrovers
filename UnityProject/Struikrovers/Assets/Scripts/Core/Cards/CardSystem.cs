using System;
using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The draw pile, the discard pile, and what happens to a card once it is drawn.
    /// Drawn cards wait in a queue until they are resolved one at a time (see
    /// TurnManager.RunPending). Resolving can ask the player something (a DecisionPrompt).
    public sealed class CardSystem
    {
        private readonly struct PendingCard
        {
            public readonly int Team;
            public readonly CardDefinition Card;

            public PendingCard(int team, CardDefinition card)
            {
                Team = team;
                Card = card;
            }
        }

        private readonly GameState state;
        private readonly DeckSettings settings;
        private readonly Action<TurnEvent> raise;
        private readonly Deck drawPile;
        private readonly List<CardDefinition> discardPile = new List<CardDefinition>();
        private readonly Queue<PendingCard> pending = new Queue<PendingCard>();

        public CardSystem(GameState state, DeckSettings settings, Action<TurnEvent> raise, Random random = null)
        {
            this.state = state;
            this.settings = settings ?? new DeckSettings();
            this.raise = raise;

            if (random == null)
                random = this.settings.seed != 0 ? new Random(this.settings.seed) : new Random();

            drawPile = Deck.Build(this.settings, random);
        }

        public int DrawPileCount => drawPile.Count;

        /// Bottom to top. The last card is on top and face up.
        public IReadOnlyList<CardDefinition> DiscardPile => discardPile;

        public CardDefinition TopOfDiscard =>
            discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : null;

        public bool DragonDrawn { get; private set; }
        public int MaxHandSize => settings.maxHandSize;
        public int EndOfTurnDraws => settings.endOfTurnDraws;
        public bool HasPending => pending.Count > 0;

        // ---- Drawing ----

        /// Takes cards from the draw pile. They are resolved later, one by one.
        /// Returns how many could be drawn.
        public int QueueDraw(int team, int count)
        {
            int drawn = 0;

            for (int i = 0; i < count; i++)
            {
                if (!drawPile.TryDraw(out CardDefinition card))
                {
                    raise(TurnEvent.DeckEmpty(team));
                    break;
                }

                pending.Enqueue(new PendingCard(team, card));
                raise(TurnEvent.CardDrawn(team));
                drawn++;
            }

            return drawn;
        }

        /// A card that came from somewhere else (stolen, picked): handled like a drawn one.
        public void QueueGain(int team, CardDefinition card)
        {
            pending.Enqueue(new PendingCard(team, card));
        }

        /// Resolves the next waiting card. Returns a prompt when the player has to decide
        /// something, or null when the card was handled by itself.
        public DecisionPrompt ProcessNext()
        {
            if (pending.Count == 0)
                return null;

            PendingCard item = pending.Dequeue();
            CardDefinition card = item.Card;

            switch (card.Kind)
            {
                case CardKind.Dragon:
                    // The dragon phase isn't built yet: announce it and carry on.
                    DragonDrawn = true;
                    raise(TurnEvent.DragonAwakes(item.Team));
                    return null;

                case CardKind.Equipment:
                    return EquipPawnPrompt.Create(this, state, item.Team, card);

                default:
                    if (state.Teams[item.Team].Hand.Count < settings.maxHandSize)
                    {
                        AddToHand(item.Team, card);
                        return null;
                    }

                    return new HandFullPrompt(this, state, item.Team, card);
            }
        }

        // ---- Changes (used by the prompts and later by card effects) ----

        internal void AddToHand(int team, CardDefinition card) => state.Teams[team].hand.Add(card);

        internal CardDefinition RemoveFromHand(int team, int index)
        {
            List<CardDefinition> hand = state.Teams[team].hand;
            CardDefinition card = hand[index];
            hand.RemoveAt(index);
            return card;
        }

        /// Puts the card face up on the discard pile.
        internal void Discard(int team, CardDefinition card)
        {
            discardPile.Add(card);
            raise(TurnEvent.CardDiscarded(team, card));
        }

        internal void Equip(Pawn pawn, CardDefinition card)
        {
            CardDefinition old = Unequip(pawn, card.Slot);
            if (old != null)
                Discard(pawn.Team, old);

            pawn.Equipment.Set(card.Slot, card);
            EquipmentEffects.Get(card)?.OnEquipped(pawn, card);
            raise(TurnEvent.CardEquipped(pawn, card));
        }

        /// Takes the item out of the slot, runs its cleanup and returns it. It does not discard it:
        /// the caller decides where it goes (discard pile, another player's pawn...).
        internal CardDefinition Unequip(Pawn pawn, EquipmentSlot slot)
        {
            CardDefinition card = pawn.Equipment.Get(slot);
            if (card == null)
                return null;

            EquipmentEffects.Get(card)?.OnUnequipped(pawn, card);
            pawn.Equipment.Set(slot, null);
            return card;
        }

        /// Pawn-specific items only fit their own pawn. General items fit every pawn.
        public static bool CanEquip(Pawn pawn, CardDefinition card) =>
            pawn != null && card != null && card.IsEquipment &&
            (!card.UsableBy.HasValue || card.UsableBy.Value == pawn.BaseType);
    }
}