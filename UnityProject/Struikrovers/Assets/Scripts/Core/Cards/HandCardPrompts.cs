using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// Who a hand card can be played on.
    internal static class HandCardTargets
    {
        public static readonly EquipmentSlot[] Slots = { EquipmentSlot.Clothing, EquipmentSlot.Weapon };

        public static int EquipmentCount(GameState state, int team)
        {
            int count = 0;

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Team != team)
                    continue;

                foreach (EquipmentSlot slot in Slots)
                    if (pawn.Equipment.Get(slot) != null)
                        count++;
            }

            return count;
        }

        public static bool AnyOpponentHasEquipment(GameState state, int team)
        {
            for (int t = 0; t < state.TeamCount; t++)
                if (t != team && EquipmentCount(state, t) > 0)
                    return true;

            return false;
        }

        public static bool AnyOpponentHasHandCards(GameState state, int team)
        {
            for (int t = 0; t < state.TeamCount; t++)
                if (t != team && state.Teams[t].Cards > 0)
                    return true;

            return false;
        }
    }

    /// Nab: pick any equipped card of any opponent, straight on the board.
    public sealed class NabPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly List<Pawn> pawns;
        private readonly List<EquipmentSlot> slots;

        private NabPrompt(
            CardSystem cards, int team,
            List<Pawn> pawns, List<EquipmentSlot> slots, List<PromptOption> options)
            : base(team,
                   "Nab: take an equipped card",
                   "Click one of the highlighted cards. You can then equip it or discard it.",
                   options)
        {
            this.cards = cards;
            this.pawns = pawns;
            this.slots = slots;
        }

        /// Null when no opponent has equipment.
        public static NabPrompt Create(CardSystem cards, GameState state, int team)
        {
            var pawns = new List<Pawn>();
            var slots = new List<EquipmentSlot>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                foreach (PawnType role in GameConfig.TurnOrder)
                {
                    Pawn pawn = state.GetPawnByRole(t, role);
                    if (pawn == null)
                        continue;

                    foreach (EquipmentSlot slot in HandCardTargets.Slots)
                    {
                        CardDefinition item = pawn.Equipment.Get(slot);
                        if (item == null)
                            continue;

                        string label = $"Take {item.Name} from Player {t + 1}'s {role} ({slot}): {item.Text}";

                        CardDefinition guard = cards.GetProtector(t, slot);
                        if (guard != null && guard != item)
                            label += $"  [{guard.Name} is taken instead]";

                        pawns.Add(pawn);
                        slots.Add(slot);
                        options.Add(new PromptOption(label, item, PromptTarget.Equipment(t, role, slot)));
                    }
                }
            }

            if (options.Count == 0)
                return null;

            return new NabPrompt(cards, team, pawns, slots, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.Nab(Team, pawns[index], slots[index]);
            return null;
        }
    }

    /// Yoink: pick any hand card of any opponent. Face down, unless the team has a Measuring Monocle.
    public sealed class YoinkPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly List<int> victims;
        private readonly List<int> handIndexes;

        private YoinkPrompt(
            CardSystem cards, int team, bool look,
            List<int> victims, List<int> handIndexes, List<PromptOption> options)
            : base(team,
                   "Yoink: take a hand card",
                   look
                       ? "Your Measuring Monocle lets you look at the cards. Click one to take it."
                       : "The cards are face down: click one to take it blind.",
                   options)
        {
            this.cards = cards;
            this.victims = victims;
            this.handIndexes = handIndexes;
        }

        /// Null when no opponent has hand cards.
        public static YoinkPrompt Create(CardSystem cards, GameState state, int team)
        {
            bool look = EquipmentEffects.RevealsStolenHand(state, team);
            var victims = new List<int>();
            var handIndexes = new List<int>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                IReadOnlyList<CardDefinition> hand = state.Teams[t].Hand;

                for (int i = 0; i < hand.Count; i++)
                {
                    victims.Add(t);
                    handIndexes.Add(i);

                    options.Add(look
                        ? new PromptOption(
                            $"Take {hand[i].Name} from Player {t + 1}: {hand[i].Text}",
                            hand[i],
                            PromptTarget.HandCard(t, i, true))
                        : new PromptOption(
                            $"Card {i + 1} of Player {t + 1} (face down)",
                            null,
                            PromptTarget.HandCard(t, i, false)));
                }
            }

            if (options.Count == 0)
                return null;

            return new YoinkPrompt(cards, team, look, victims, handIndexes, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.Yoink(Team, victims[index], handIndexes[index]);
            return null;
        }
    }
}