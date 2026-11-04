using System;
using System.Collections.Generic;
using static Struikrovers.Core.DeckSettings;

namespace Struikrovers.Core
{
    /// The draw pile, the discard pile, and what happens to a card once it is drawn or taken.
    /// Drawn cards, looted cards and theft requests wait in one queue until they are resolved one
    /// at a time (see TurnManager.RunPending). Resolving can ask the player something (a DecisionPrompt).
    public sealed class CardSystem
    {
        public const string MartyrShieldId = "martyr-shield";
        public const string ChainmailId = "chainmail";
        public const string RoyalInheritanceId = "royal-inheritance";

        private static readonly EquipmentSlot[] SlotsInOrder =
            { EquipmentSlot.Clothing, EquipmentSlot.Weapon };

        // A card to resolve (Card is set), an equipment theft (Thief and Victim are set), or a
        // looted card whose source is chosen when it is resolved (LootPawn is set).
        private sealed class PendingItem
        {
            public int Team;
            public CardDefinition Card;
            public Pawn Thief;
            public Pawn Victim;
            public bool TakeAll;
            public Pawn LootPawn;
            public bool DiscardReward;
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

        /// False when equipment (Warding Amulet) protects the team. Phase F's curse cards use this.
        public bool CanBeCursed(int team) => !EquipmentEffects.PreventsCurse(state, team, out _);

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

        /// Cards looted from a bush by this pawn. Normally these are plain draws. When the pawn
        /// wears Gravedigger's Spade, each card gets its own choice when it is resolved: the top
        /// of the draw pile, or one of the top cards of the discard pile.
        public void QueueLoot(Pawn looter, int count)
        {
            if (looter == null || count <= 0)
                return;

            if (!EquipmentEffects.OffersDiscardPick(looter))
            {
                QueueDraw(looter.Team, count);
                return;
            }

            for (int i = 0; i < count; i++)
                pending.Enqueue(new PendingItem { Team = looter.Team, LootPawn = looter });
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

        /// Delivery reward: the team picks 1 card from the discard pile. It waits until everything
        /// else in the queue is resolved, so the stolen equipment can be picked too.
        public void QueueDiscardPick(int team)
        {
            pending.Enqueue(new PendingItem { Team = team, DiscardReward = true });
        }

        // ---- Resolving ----

        /// Resolves the next waiting item. Returns a prompt when the player has to decide
        /// something, or null when it was handled by itself.
        public DecisionPrompt ProcessNext()
        {
            if (pending.Count == 0)
                return null;

            PendingItem item = pending.Dequeue();

            if (item.DiscardReward)
                return ResolveDiscardReward(item);

            if (item.Thief != null)
                return ResolveTheft(item);

            if (item.LootPawn != null)
                return ResolveLootChoice(item);

            return ResolveCard(item.Team, item.Card);
        }

        /// What happens to a card that has just arrived: dragon, equipment or hand.
        /// Returns a prompt when the player has to decide something.
        internal DecisionPrompt ResolveCard(int team, CardDefinition card)
        {
            switch (card.Kind)
            {
                case CardKind.Dragon:
                    // The dragon phase isn't built yet: announce it and carry on.
                    DragonDrawn = true;
                    raise(TurnEvent.DragonAwakes(team));
                    return null;

                case CardKind.Equipment:
                    return EquipPawnPrompt.Create(this, state, team, card);

                default:
                    if (state.Teams[team].Hand.Count < settings.maxHandSize)
                    {
                        AddToHand(team, card);
                        return null;
                    }

                    return new HandFullPrompt(this, state, team, card);
            }
        }

        // A card looted with Gravedigger's Spade: ask where it comes from, when there is a choice.
        private DecisionPrompt ResolveLootChoice(PendingItem item)
        {
            bool canDraw = drawPile.Count > 0;
            bool canPick = discardPile.Count > 0;

            if (!canPick)
            {
                // Nothing to pick from: an ordinary draw (this also reports an empty draw pile).
                CardDefinition drawn = DrawFromPile(item.Team);
                return drawn != null ? ResolveCard(item.Team, drawn) : null;
            }

            if (!canDraw)
                return new DiscardPickPrompt(this, item.Team, false);

            return new LootSourcePrompt(this, item.Team);
        }

        // Waits behind every other waiting item (stolen equipment gets equipped or discarded first).
        // Null when the pile has nothing to pick.
        private DecisionPrompt ResolveDiscardReward(PendingItem item)
        {
            foreach (PendingItem other in pending)
            {
                if (!other.DiscardReward)
                {
                    pending.Enqueue(item);
                    return null;
                }
            }

            return OnesMansTrashPrompt.CreateDeliveryReward(this, item.Team);
        }

        /// Takes the top card of the draw pile right now. Null (and a message) when it is empty.
        internal CardDefinition DrawFromPile(int team)
        {
            if (!drawPile.TryDraw(out CardDefinition card))
            {
                raise(TurnEvent.DeckEmpty(team));
                return null;
            }

            raise(TurnEvent.CardDrawn(team));
            return card;
        }

        /// The top cards of the discard pile, top first.
        public List<CardDefinition> PeekDiscard(int count)
        {
            var result = new List<CardDefinition>();

            for (int i = discardPile.Count - 1; i >= 0 && result.Count < count; i--)
                result.Add(discardPile[i]);

            return result;
        }

        /// Takes a card from the discard pile. fromTop = 0 is the top card.
        internal CardDefinition TakeFromDiscard(int team, int fromTop)
        {
            int index = discardPile.Count - 1 - fromTop;
            if (fromTop < 0 || index < 0)
                return null;

            CardDefinition card = discardPile[index];
            discardPile.RemoveAt(index);
            raise(TurnEvent.CardTakenFromDiscard(team, card));
            return card;
        }

        /// One Man's Trash can pick anything from the discard pile except the Dragon Awakes card.
        public static bool CanPickWithTrash(CardDefinition card) =>
            card != null && card.Kind != CardKind.Dragon;

        /// How many cards One Man's Trash could pick right now.
        public int CountPickableDiscard()
        {
            int count = 0;

            foreach (CardDefinition card in discardPile)
                if (CanPickWithTrash(card))
                    count++;

            return count;
        }

        /// Takes the card at this position of the discard pile (0 = bottom). The other cards keep their
        /// order. 'expected' guards against a stale index.
        internal CardDefinition TakeFromDiscardAt(int team, int pileIndex, CardDefinition expected)
        {
            if (pileIndex < 0 || pileIndex >= discardPile.Count || discardPile[pileIndex] != expected)
                return null;

            discardPile.RemoveAt(pileIndex);
            raise(TurnEvent.CardTakenFromDiscard(team, expected));
            return expected;
        }

        private DecisionPrompt ResolveTheft(PendingItem item)
        {
            Pawn thief = item.Thief;
            Pawn victim = item.Victim;

            var slots = new List<EquipmentSlot>();
            foreach (EquipmentSlot slot in SlotsInOrder)
                if (victim.Equipment.Get(slot) != null)
                    slots.Add(slot);

            // Nothing to take, so nothing happens (and no protector is triggered).
            if (slots.Count == 0)
                return null;

            // Every slot is taken once, and each protector only covers its own slot, so
            // Martyr Shield and Chainmail can each trigger at most once per theft.
            if (item.TakeAll)
            {
                foreach (EquipmentSlot slot in slots)
                    TakeEquipment(thief.Team, thief, victim, slot);

                return null;
            }

            // With a single item there is no real choice.
            if (slots.Count == 1)
            {
                TakeEquipment(thief.Team, thief, victim, slots[0]);
                return null;
            }

            return StealEquipmentPrompt.Create(this, thief, victim);
        }

        /// Takes one item off the victim for the thief. A Martyr Shield (weapons) or Chainmail
        /// (clothing) on any pawn of the victim's team is taken instead of the chosen item.
        /// The item then waits in the queue to be equipped or discarded by the thief's team.
        internal void TakeForTheft(Pawn thief, Pawn victim, EquipmentSlot slot) =>
            TakeEquipment(thief.Team, thief, victim, slot);

        /// The thief chose this item in a StealEquipmentPrompt.
        internal void CompleteChosenTheft(Pawn thief, Pawn victim, EquipmentSlot slot) =>
            TakeEquipment(thief.Team, thief, victim, slot);

        /// Nab card: the team takes 1 equipped item of its choice from an opponent's pawn.
        /// A Martyr Shield or Chainmail protects against it like against any other theft.
        internal void Nab(int thiefTeam, Pawn victim, EquipmentSlot slot)
        {
            if (victim == null || victim.Team == thiefTeam || victim.Equipment.Get(slot) == null)
                return;

            TakeEquipment(thiefTeam, null, victim, slot);
        }

        // 'thief' is null when no pawn is doing the stealing (the Nab card).
        private void TakeEquipment(int thiefTeam, Pawn thief, Pawn victim, EquipmentSlot slot)
        {
            CardDefinition card;
            bool viaProtector = false;

            if (TryFindProtector(victim.Team, slot, out Pawn holder))
            {
                card = Unequip(holder, slot);

                // Choosing the protector itself is not a substitution.
                viaProtector = holder != victim;
            }
            else
            {
                card = Unequip(victim, slot);
            }

            if (card == null)
                return;

            raise(thief != null
                ? TurnEvent.EquipmentTaken(thief, victim, card, viaProtector)
                : TurnEvent.EquipmentNabbed(thiefTeam, victim, card, viaProtector));

            pending.Enqueue(new PendingItem { Team = thiefTeam, Card = card });
        }

        private static string ProtectorId(EquipmentSlot slot) =>
            slot == EquipmentSlot.Weapon ? MartyrShieldId : ChainmailId;

        // The Martyr Shield (weapon slot) or Chainmail (clothing slot) of this team, on any pawn.
        private bool TryFindProtector(int team, EquipmentSlot slot, out Pawn holder)
        {
            string id = ProtectorId(slot);

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Team != team)
                    continue;

                CardDefinition item = pawn.Equipment.Get(slot);
                if (item != null && item.Id == id)
                {
                    holder = pawn;
                    return true;
                }
            }

            holder = null;
            return false;
        }

        /// The item that would be taken instead of something in this slot, or null. For prompt text.
        public CardDefinition GetProtector(int team, EquipmentSlot slot) =>
            TryFindProtector(team, slot, out Pawn holder) ? holder.Equipment.Get(slot) : null;

        // ---- Stealing from a hand ----

        /// Yoink card: takes the card at this position from the victim's hand. It goes to the
        /// thief's team like a drawn card (hand full? the thief decides what to discard).
        internal CardDefinition Yoink(int thiefTeam, int victimTeam, int handIndex)
        {
            if (thiefTeam == victimTeam || victimTeam < 0 || victimTeam >= state.TeamCount)
                return null;

            List<CardDefinition> hand = state.Teams[victimTeam].hand;
            if (handIndex < 0 || handIndex >= hand.Count)
                return null;

            CardDefinition card = RemoveFromHand(victimTeam, handIndex);
            raise(TurnEvent.CardYoinked(thiefTeam, victimTeam));
            QueueGain(thiefTeam, card);
            return card;
        }

        // ---- Swapping ----

        /// Hand Over: the two teams swap their whole hands.
        internal void SwapHands(int teamA, int teamB)
        {
            if (teamA == teamB || teamA < 0 || teamB < 0 ||
                teamA >= state.TeamCount || teamB >= state.TeamCount)
                return;

            List<CardDefinition> a = state.Teams[teamA].hand;
            List<CardDefinition> b = state.Teams[teamB].hand;

            var copy = new List<CardDefinition>(a);
            a.Clear();
            a.AddRange(b);
            b.Clear();
            b.AddRange(copy);

            raise(TurnEvent.HandsSwapped(teamA, teamB));
        }

        /// Switcheroo: two pawns of the same type swap all their equipment. Every item fits both
        /// pawns, because pawn-specific items are tied to the type. It is a swap, not a theft,
        /// so Martyr Shield and Chainmail don't trigger.
        internal bool SwapEquipment(Pawn mine, Pawn theirs)
        {
            if (mine == null || theirs == null || mine.Team == theirs.Team ||
                mine.BaseType != theirs.BaseType)
                return false;

            var fromMine = new CardDefinition[SlotsInOrder.Length];
            var fromTheirs = new CardDefinition[SlotsInOrder.Length];

            // Take everything off first, so no OnEquipped runs while an item is still on the other pawn.
            for (int i = 0; i < SlotsInOrder.Length; i++)
            {
                fromMine[i] = Unequip(mine, SlotsInOrder[i]);
                fromTheirs[i] = Unequip(theirs, SlotsInOrder[i]);
            }

            for (int i = 0; i < SlotsInOrder.Length; i++)
            {
                if (fromTheirs[i] != null) Place(mine, fromTheirs[i]);
                if (fromMine[i] != null) Place(theirs, fromMine[i]);
            }

            raise(TurnEvent.EquipmentSwapped(mine, theirs));
            return true;
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

        // ---- Managing your own equipment and hand ----
        // All of this is voluntary, so it never triggers the Martyr Shield or Chainmail.

        /// What the owner can do with the item in this slot: move it to a pawn that can wear it,
        /// swap with that pawn's item when both fit, or discard it.
        public List<ManageOption> GetEquipmentOptions(Pawn pawn, EquipmentSlot slot)
        {
            var options = new List<ManageOption>();

            CardDefinition item = pawn?.Equipment.Get(slot);
            if (item == null)
                return options;

            foreach (PawnType role in GameConfig.TurnOrder)
            {
                Pawn other = state.GetPawnByRole(pawn.Team, role);
                if (other == null || other == pawn || !CanEquip(other, item))
                    continue;

                CardDefinition occupant = other.Equipment.Get(slot);

                if (occupant == null) options.Add(ManageOption.Move(other));
                else if (CanEquip(pawn, occupant)) options.Add(ManageOption.Swap(other));
            }

            options.Add(ManageOption.DiscardEquipment());
            return options;
        }

        /// Moves the item to another pawn of the same team, swapping when the slot there is taken.
        internal bool MoveEquipment(Pawn from, EquipmentSlot slot, Pawn to)
        {
            if (from == null || to == null || from == to || from.Team != to.Team)
                return false;

            CardDefinition item = from.Equipment.Get(slot);
            if (item == null || !CanEquip(to, item))
                return false;

            CardDefinition other = to.Equipment.Get(slot);
            if (other != null && !CanEquip(from, other))
                return false;

            Unequip(from, slot);
            if (other != null)
                Unequip(to, slot);

            Place(to, item);
            raise(TurnEvent.EquipmentMoved(from, to, item));

            if (other != null)
            {
                Place(from, other);
                raise(TurnEvent.EquipmentMoved(to, from, other));
            }

            return true;
        }

        internal bool DiscardEquipment(Pawn pawn, EquipmentSlot slot)
        {
            CardDefinition card = Unequip(pawn, slot);
            if (card == null)
                return false;

            Discard(pawn.Team, card);
            return true;
        }

        /// 'expected' guards against a stale index: the card at that position must still be this one.
        internal bool DiscardHandCard(int team, int index, CardDefinition expected)
        {
            List<CardDefinition> hand = state.Teams[team].hand;
            if (index < 0 || index >= hand.Count || hand[index] != expected)
                return false;

            Discard(team, RemoveFromHand(team, index));
            return true;
        }

        // Puts the item in its slot and starts its effect, without discarding or announcing anything.
        private static void Place(Pawn pawn, CardDefinition card)
        {
            pawn.Equipment.Set(card.Slot, card);
            EquipmentEffects.Get(card)?.OnEquipped(pawn, card);
        }

        /// Plays a hand card: it goes face up onto the discard pile. 'expected' guards against a stale index.
        internal bool PlayCard(int team, int index, CardDefinition expected)
        {
            List<CardDefinition> hand = state.Teams[team].hand;
            if (index < 0 || index >= hand.Count || hand[index] != expected)
                return false;

            discardPile.Add(RemoveFromHand(team, index));
            raise(TurnEvent.CardPlayed(team, expected));
            return true;
        }

        /// Takes back a card that was just played, because its question was cancelled. Only works
        /// while the card is still on top of the discard pile. It returns to its old place in the hand.
        internal bool UndoPlay(int team, int index, CardDefinition card)
        {
            if (discardPile.Count == 0 || discardPile[discardPile.Count - 1] != card)
                return false;

            discardPile.RemoveAt(discardPile.Count - 1);

            List<CardDefinition> hand = state.Teams[team].hand;
            hand.Insert(Math.Min(Math.Max(0, index), hand.Count), card);

            raise(TurnEvent.CardPlayCancelled(team, card));
            return true;
        }

        /// Debug: puts the curses from the deck settings on their teams before the game starts.
        internal void ApplyDebugCurses()
        {
            if (settings.debugCurses == null)
                return;

            foreach (DebugCurse entry in settings.debugCurses)
            {
                if (entry == null)
                    continue;

                CardDefinition card = CardCatalog.Find(entry.cardId);
                if (card == null || card.Kind != CardKind.Curse ||
                    entry.team < 0 || entry.team >= state.TeamCount)
                {
                    UnityEngine.Debug.LogWarning($"DeckSettings: invalid debug curse '{entry.cardId}' for team {entry.team}.");
                    continue;
                }

                List<CardDefinition> curses = state.Teams[entry.team].curses;
                if (curses.Count < GameConfig.MaxCurses && !curses.Contains(card))
                    curses.Add(card);
            }
        }

        /// Pawn-specific items only fit their own pawn. General items fit every pawn.
        public static bool CanEquip(Pawn pawn, CardDefinition card) =>
            pawn != null && card != null && card.IsEquipment &&
            (!card.UsableBy.HasValue || card.UsableBy.Value == pawn.BaseType);
    }
}