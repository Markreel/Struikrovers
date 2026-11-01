using System;
using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The draw pile, the discard pile, and what happens to a card once it is drawn or taken.
    /// Drawn cards and theft requests wait in one queue until they are resolved one at a time
    /// (see TurnManager.RunPending). Resolving can ask the player something (a DecisionPrompt).
    public sealed class CardSystem
    {
        public const string MartyrShieldId = "martyr-shield";
        public const string RoyalInheritanceId = "royal-inheritance";

        private static readonly EquipmentSlot[] SlotsInOrder =
            { EquipmentSlot.Clothing, EquipmentSlot.Weapon };

        // Either a card to resolve (Card is set) or an equipment theft (Thief and Victim are set).
        private sealed class PendingItem
        {
            public int Team;
            public CardDefinition Card;
            public Pawn Thief;
            public Pawn Victim;
            public bool TakeAll;
        }

        private readonly GameState state;
        private readonly DeckSettings settings;
        private readonly Action<TurnEvent> raise;
        private readonly Deck drawPile;
        private readonly List<CardDefinition> discardPile = new List<CardDefinition>();
        private readonly Queue<PendingItem> pending = new Queue<PendingItem>();

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

                pending.Enqueue(new PendingItem { Team = team, Card = card });
                raise(TurnEvent.CardDrawn(team));
                drawn++;
            }

            return drawn;
        }

        /// A card that came from somewhere else (stolen, picked): handled like a drawn one.
        public void QueueGain(int team, CardDefinition card)
        {
            pending.Enqueue(new PendingItem { Team = team, Card = card });
        }

        // ---- Stealing equipment ----

        /// The thief has killed or beaten the victim: it takes 1 piece of the victim's equipment
        /// of its choice, or all of it when the thief has Royal Inheritance.
        public void QueueSteal(Pawn thief, Pawn victim)
        {
            if (thief == null || victim == null || thief.Team == victim.Team)
                return;

            bool all = thief.Equipment.Has(RoyalInheritanceId);
            pending.Enqueue(new PendingItem { Team = thief.Team, Thief = thief, Victim = victim, TakeAll = all });
        }

        /// The thief takes every piece of the victim's equipment (delivering a Royalty).
        public void QueueTakeAll(Pawn thief, Pawn victim)
        {
            if (thief == null || victim == null || thief.Team == victim.Team)
                return;

            pending.Enqueue(new PendingItem { Team = thief.Team, Thief = thief, Victim = victim, TakeAll = true });
        }

        // ---- Resolving ----

        /// Resolves the next waiting item. Returns a prompt when the player has to decide
        /// something, or null when it was handled by itself.
        public DecisionPrompt ProcessNext()
        {
            if (pending.Count == 0)
                return null;

            PendingItem item = pending.Dequeue();

            if (item.Thief != null)
                return ResolveTheft(item);

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

        private DecisionPrompt ResolveTheft(PendingItem item)
        {
            Pawn thief = item.Thief;
            Pawn victim = item.Victim;

            var slots = new List<EquipmentSlot>();
            foreach (EquipmentSlot slot in SlotsInOrder)
                if (victim.Equipment.Get(slot) != null)
                    slots.Add(slot);

            // Nothing to take, so nothing happens (and a Martyr Shield is not triggered).
            if (slots.Count == 0)
                return null;

            bool shieldSpent = false;

            if (item.TakeAll)
            {
                foreach (EquipmentSlot slot in slots)
                    TakeForTheft(thief, victim, slot, ref shieldSpent);

                return null;
            }

            // With a single item, or a Martyr Shield that takes its place, there is no real choice.
            if (slots.Count == 1 || TryFindShield(victim.Team, out _, out _))
            {
                TakeForTheft(thief, victim, slots[0], ref shieldSpent);
                return null;
            }

            return StealEquipmentPrompt.Create(this, thief, victim);
        }

        /// Takes one item off the victim for the thief. A Martyr Shield on any pawn of the
        /// victim's team is taken instead of the chosen item (once per theft). The item then
        /// waits in the queue to be equipped or discarded by the thief's team.
        internal void TakeForTheft(Pawn thief, Pawn victim, EquipmentSlot slot, ref bool shieldSpent)
        {
            CardDefinition card;
            bool viaShield = false;

            if (!shieldSpent && TryFindShield(victim.Team, out Pawn holder, out EquipmentSlot shieldSlot))
            {
                shieldSpent = true;
                card = Unequip(holder, shieldSlot);

                // Choosing the shield itself is not a substitution.
                viaShield = !(holder == victim && shieldSlot == slot);
            }
            else
            {
                card = Unequip(victim, slot);
            }

            if (card == null)
                return;

            raise(TurnEvent.EquipmentTaken(thief, victim, card, viaShield));
            pending.Enqueue(new PendingItem { Team = thief.Team, Card = card });
        }

        /// The thief chose this item in a StealEquipmentPrompt.
        internal void CompleteChosenTheft(Pawn thief, Pawn victim, EquipmentSlot slot)
        {
            bool shieldSpent = false;
            TakeForTheft(thief, victim, slot, ref shieldSpent);
        }

        private bool TryFindShield(int team, out Pawn holder, out EquipmentSlot slot)
        {
            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Team != team)
                    continue;

                foreach (EquipmentSlot s in SlotsInOrder)
                {
                    CardDefinition item = pawn.Equipment.Get(s);
                    if (item != null && item.Id == MartyrShieldId)
                    {
                        holder = pawn;
                        slot = s;
                        return true;
                    }
                }
            }

            holder = null;
            slot = EquipmentSlot.Weapon;
            return false;
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

        /// Puts the card in its slot. An item that is already there is taken off and discarded first.
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