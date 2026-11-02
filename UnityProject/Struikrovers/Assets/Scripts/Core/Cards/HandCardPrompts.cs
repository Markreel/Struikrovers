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

    /// Nab, step 1: which opponent. Skipped when only one opponent has equipment.
    public sealed class NabOpponentPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly GameState state;
        private readonly List<int> teams;

        private NabOpponentPrompt(
            CardSystem cards, GameState state, int team, List<int> teams, List<PromptOption> options)
            : base(team, "Nab: choose a player", "You then choose which equipped card to take.", options)
        {
            this.cards = cards;
            this.state = state;
            this.teams = teams;
        }

        /// Null when no opponent has equipment.
        public static DecisionPrompt Create(CardSystem cards, GameState state, int team)
        {
            var teams = new List<int>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                int count = HandCardTargets.EquipmentCount(state, t);
                if (count == 0)
                    continue;

                string plural = count == 1 ? "card" : "cards";
                teams.Add(t);
                options.Add(new PromptOption($"Player {t + 1} ({count} equipped {plural})"));
            }

            if (teams.Count == 0)
                return null;

            if (teams.Count == 1)
                return NabItemPrompt.Create(cards, state, team, teams[0]);

            return new NabOpponentPrompt(cards, state, team, teams, options);
        }

        public override DecisionPrompt Choose(int index) =>
            NabItemPrompt.Create(cards, state, Team, teams[index]);
    }

    /// Nab, step 2: which equipped card of that opponent.
    public sealed class NabItemPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly List<Pawn> pawns;
        private readonly List<EquipmentSlot> slots;

        private NabItemPrompt(
            CardSystem cards, int team, int victimTeam,
            List<Pawn> pawns, List<EquipmentSlot> slots, List<PromptOption> options)
            : base(team,
                   $"Nab: take equipment from Player {victimTeam + 1}",
                   "Choose 1 equipped card. You can then equip it or discard it.",
                   options)
        {
            this.cards = cards;
            this.pawns = pawns;
            this.slots = slots;
        }

        public static NabItemPrompt Create(CardSystem cards, GameState state, int team, int victimTeam)
        {
            var pawns = new List<Pawn>();
            var slots = new List<EquipmentSlot>();
            var options = new List<PromptOption>();

            foreach (PawnType role in GameConfig.TurnOrder)
            {
                Pawn pawn = state.GetPawnByRole(victimTeam, role);
                if (pawn == null)
                    continue;

                foreach (EquipmentSlot slot in HandCardTargets.Slots)
                {
                    CardDefinition item = pawn.Equipment.Get(slot);
                    if (item == null)
                        continue;

                    string label = $"Take {item.Name} from the {role} ({slot}): {item.Text}";

                    CardDefinition guard = cards.GetProtector(victimTeam, slot);
                    if (guard != null && guard != item)
                        label += $"  [{guard.Name} is taken instead]";

                    pawns.Add(pawn);
                    slots.Add(slot);
                    options.Add(new PromptOption(label, item));
                }
            }

            return new NabItemPrompt(cards, team, victimTeam, pawns, slots, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.Nab(Team, pawns[index], slots[index]);
            return null;
        }
    }

    /// Yoink, step 1: which opponent. Skipped when only one opponent has hand cards.
    public sealed class YoinkOpponentPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly GameState state;
        private readonly List<int> teams;

        private YoinkOpponentPrompt(
            CardSystem cards, GameState state, int team, List<int> teams, List<PromptOption> options)
            : base(team, "Yoink: choose a player", "You then take 1 card from their hand.", options)
        {
            this.cards = cards;
            this.state = state;
            this.teams = teams;
        }

        /// Null when no opponent has hand cards.
        public static DecisionPrompt Create(CardSystem cards, GameState state, int team)
        {
            var teams = new List<int>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                int count = state.Teams[t].Cards;
                if (count == 0)
                    continue;

                string plural = count == 1 ? "card" : "cards";
                teams.Add(t);
                options.Add(new PromptOption($"Player {t + 1} ({count} {plural} in hand)"));
            }

            if (teams.Count == 0)
                return null;

            if (teams.Count == 1)
                return YoinkCardPrompt.Create(cards, state, team, teams[0]);

            return new YoinkOpponentPrompt(cards, state, team, teams, options);
        }

        public override DecisionPrompt Choose(int index) =>
            YoinkCardPrompt.Create(cards, state, Team, teams[index]);
    }

    /// Yoink, step 2: which card. Face down, unless the team has a Measuring Monocle.
    public sealed class YoinkCardPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly int victimTeam;

        private YoinkCardPrompt(
            CardSystem cards, int team, int victimTeam, bool look, List<PromptOption> options)
            : base(team,
                   $"Yoink: take a card from Player {victimTeam + 1}'s hand",
                   look
                       ? "Your Measuring Monocle lets you look at the cards before you choose."
                       : "The cards are face down: pick one blind.",
                   options)
        {
            this.cards = cards;
            this.victimTeam = victimTeam;
        }

        public static YoinkCardPrompt Create(CardSystem cards, GameState state, int team, int victimTeam)
        {
            bool look = EquipmentEffects.RevealsStolenHand(state, team);
            IReadOnlyList<CardDefinition> hand = state.Teams[victimTeam].Hand;
            var options = new List<PromptOption>();

            for (int i = 0; i < hand.Count; i++)
            {
                options.Add(look
                    ? new PromptOption($"Take {hand[i].Name}: {hand[i].Text}", hand[i])
                    : new PromptOption($"Card {i + 1} (face down)"));
            }

            return new YoinkCardPrompt(cards, team, victimTeam, look, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.Yoink(Team, victimTeam, index);
            return null;
        }
    }
}