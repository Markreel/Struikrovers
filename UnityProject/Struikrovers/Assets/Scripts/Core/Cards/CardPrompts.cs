using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The hand is full: discard the new card, or a card from the hand to make room.
    public sealed class HandFullPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly CardDefinition newCard;

        public HandFullPrompt(CardSystem cards, GameState state, int team, CardDefinition newCard)
            : base(team,
                   $"Your hand is full ({cards.MaxHandSize} cards)",
                   $"You got {newCard.Name}: {newCard.Text}",
                   BuildOptions(state, team, newCard))
        {
            this.cards = cards;
            this.newCard = newCard;
        }

        private static List<PromptOption> BuildOptions(GameState state, int team, CardDefinition newCard)
        {
            var options = new List<PromptOption>
            {
                new PromptOption($"Discard the new card ({newCard.Name})", newCard)
            };

            foreach (CardDefinition inHand in state.Teams[team].Hand)
                options.Add(new PromptOption($"Discard {inHand.Name} and keep {newCard.Name}", inHand));

            return options;
        }

        public override DecisionPrompt Choose(int index)
        {
            if (index == 0)
            {
                cards.Discard(Team, newCard);
                return null;
            }

            CardDefinition removed = cards.RemoveFromHand(Team, index - 1);
            cards.Discard(Team, removed);
            cards.AddToHand(Team, newCard);
            return null;
        }
    }

    /// A new piece of equipment: put it on a pawn it fits, or discard it.
    public sealed class EquipPawnPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly CardDefinition card;
        private readonly List<Pawn> targets;

        private EquipPawnPrompt(
            CardSystem cards, int team, CardDefinition card,
            List<Pawn> targets, List<PromptOption> options)
            : base(team, $"New equipment: {card.Name}", card.Text, options)
        {
            this.cards = cards;
            this.card = card;
            this.targets = targets;
        }

        public static EquipPawnPrompt Create(CardSystem cards, GameState state, int team, CardDefinition card)
        {
            var targets = new List<Pawn>();
            var options = new List<PromptOption>();

            foreach (PawnType role in GameConfig.TurnOrder)
            {
                Pawn pawn = state.GetPawnByRole(team, role);
                if (pawn == null || !CardSystem.CanEquip(pawn, card))
                    continue;

                CardDefinition current = pawn.Equipment.Get(card.Slot);
                string label = current == null
                    ? $"Equip on the {role}"
                    : $"Equip on the {role} (currently: {current.Name})";

                targets.Add(pawn);
                options.Add(new PromptOption(label, current));
            }

            options.Add(new PromptOption($"Discard {card.Name}", card));
            return new EquipPawnPrompt(cards, team, card, targets, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            // The last option is always "discard".
            if (index >= targets.Count)
            {
                cards.Discard(Team, card);
                return null;
            }

            Pawn pawn = targets[index];
            CardDefinition current = pawn.Equipment.Get(card.Slot);

            if (current == null)
            {
                cards.Equip(pawn, card);
                return null;
            }

            return new ReplaceEquipmentPrompt(cards, Team, pawn, card, current);
        }
    }

    /// The slot is taken: replace the old item (it is discarded) or discard the new one.
    public sealed class ReplaceEquipmentPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly Pawn pawn;
        private readonly CardDefinition newCard;

        public ReplaceEquipmentPrompt(
            CardSystem cards, int team, Pawn pawn, CardDefinition newCard, CardDefinition oldCard)
            : base(team,
                   $"The {pawn.BaseType}'s {newCard.Slot.ToString().ToLowerInvariant()} slot is taken",
                   $"{oldCard.Name}: {oldCard.Text}",
                   new List<PromptOption>
                   {
                       new PromptOption($"Replace {oldCard.Name} with {newCard.Name} (the old one is discarded)", newCard),
                       new PromptOption($"Keep {oldCard.Name} and discard {newCard.Name}", oldCard)
                   })
        {
            this.cards = cards;
            this.pawn = pawn;
            this.newCard = newCard;
        }

        public override DecisionPrompt Choose(int index)
        {
            if (index == 0)
            {
                // Equip takes the old item off and puts it on the discard pile.
                cards.Equip(pawn, newCard);
            }
            else
            {
                cards.Discard(Team, newCard);
            }

            return null;
        }
    }

    /// The thief picks which of the victim's items it takes.
    public sealed class StealEquipmentPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly Pawn thief;
        private readonly Pawn victim;
        private readonly List<EquipmentSlot> slots;

        private StealEquipmentPrompt(
            CardSystem cards, Pawn thief, Pawn victim,
            List<EquipmentSlot> slots, List<PromptOption> options)
            : base(thief.Team,
                   $"Take equipment from Player {victim.Team + 1}'s {victim.BaseType}",
                   "Choose 1 item. You can then equip it or discard it.",
                   options)
        {
            this.cards = cards;
            this.thief = thief;
            this.victim = victim;
            this.slots = slots;
        }

        public static StealEquipmentPrompt Create(CardSystem cards, Pawn thief, Pawn victim)
        {
            var slots = new List<EquipmentSlot>();
            var options = new List<PromptOption>();

            foreach (EquipmentSlot slot in new[] { EquipmentSlot.Clothing, EquipmentSlot.Weapon })
            {
                CardDefinition item = victim.Equipment.Get(slot);
                if (item == null)
                    continue;

                slots.Add(slot);
                options.Add(new PromptOption($"Take {item.Name} ({slot}): {item.Text}", item));
            }

            return new StealEquipmentPrompt(cards, thief, victim, slots, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.CompleteChosenTheft(thief, victim, slots[index]);
            return null;
        }
    }
}