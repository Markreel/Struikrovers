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

        public static bool HasEquipment(Pawn pawn) =>
    pawn != null && (pawn.Equipment.Clothing != null || pawn.Equipment.Weapon != null);

        /// Your pawn and the opponent's pawn of the same type, when both wear something.
        public static bool TryGetSwitchPair(
            GameState state, int team, int opponent, PawnType role, out Pawn mine, out Pawn theirs)
        {
            mine = state.GetPawnByRole(team, role);
            theirs = state.GetPawnByRole(opponent, role);
            return HasEquipment(mine) && HasEquipment(theirs);
        }

        public static bool AnySwitcheroo(GameState state, int team)
        {
            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                foreach (PawnType role in GameConfig.TurnOrder)
                    if (TryGetSwitchPair(state, team, t, role, out _, out _))
                        return true;
            }

            return false;
        }

        public static bool AnyOpponent(GameState state, int team) => state.TeamCount > 1;

        public static string DescribeEquipment(Pawn pawn)
        {
            string text = "";

            foreach (CardDefinition item in pawn.Equipment.Items)
                text += (text.Length > 0 ? " + " : "") + item.Name;

            return text;
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

    /// Hand Over: click an opponent's panel to swap whole hands with them.
    public sealed class HandOverPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly List<int> opponents;

        private HandOverPrompt(CardSystem cards, int team, List<int> opponents, List<PromptOption> options)
            : base(team,
                   "Hand Over: swap hands",
                   "Click the player you want to swap hands with. Everyone keeps the other's whole hand.",
                   options)
        {
            this.cards = cards;
            this.opponents = opponents;
        }

        /// Null when there is no opponent.
        public static HandOverPrompt Create(CardSystem cards, GameState state, int team)
        {
            var opponents = new List<int>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team || state.Teams[t].Cards == 0)
                    continue;

                opponents.Add(t);
                options.Add(new PromptOption(
                    $"Swap hands with Player {t + 1} ({state.Teams[t].Cards} card(s))",
                    null,
                    PromptTarget.Panel(t)));
            }

            if (options.Count == 0)
                return null;

            return new HandOverPrompt(cards, team, opponents, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.SwapHands(Team, opponents[index]);
            return null;
        }
    }

    /// Switcheroo: click an opponent's pawn. It swaps all equipment with your pawn of the same type.
    /// Only pawns where both sides wear something can be picked.
    public sealed class SwitcherooPrompt : DecisionPrompt
    {
        private readonly CardSystem cards;
        private readonly List<Pawn> mine;
        private readonly List<Pawn> theirs;

        private SwitcherooPrompt(
            CardSystem cards, int team, List<Pawn> mine, List<Pawn> theirs, List<PromptOption> options)
            : base(team,
                   "Switcheroo: swap equipment",
                   "Click an opponent's pawn to swap all its equipment with your pawn of the same type.",
                   options)
        {
            this.cards = cards;
            this.mine = mine;
            this.theirs = theirs;
        }

        /// Null when no pawn pair qualifies.
        public static SwitcherooPrompt Create(CardSystem cards, GameState state, int team)
        {
            var mine = new List<Pawn>();
            var theirs = new List<Pawn>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                foreach (PawnType role in GameConfig.TurnOrder)
                {
                    if (!HandCardTargets.TryGetSwitchPair(state, team, t, role, out Pawn own, out Pawn other))
                        continue;

                    mine.Add(own);
                    theirs.Add(other);
                    options.Add(new PromptOption(
                        $"Swap your {role} ({HandCardTargets.DescribeEquipment(own)}) with " +
                        $"Player {t + 1}'s {role} ({HandCardTargets.DescribeEquipment(other)})",
                        null,
                        PromptTarget.PawnRow(t, role)));
                }
            }

            if (options.Count == 0)
                return null;

            return new SwitcherooPrompt(cards, team, mine, theirs, options);
        }

        public override DecisionPrompt Choose(int index)
        {
            cards.SwapEquipment(mine[index], theirs[index]);
            return null;
        }
    }

    /// Peek, step 1: pick the player whose Bandit you want to see.
    public sealed class PeekPrompt : DecisionPrompt
    {
        private readonly GameState state;
        private readonly List<int> targets;

        private PeekPrompt(GameState state, int team, List<int> targets, List<PromptOption> options)
            : base(team,
                   "Peek: choose a player",
                   "First ask the other players to close their eyes. Then click the player whose Bandit you want to see.",
                   options)
        {
            this.state = state;
            this.targets = targets;
        }

        /// Null when there is no other player.
        public static PeekPrompt Create(GameState state, int team)
        {
            var targets = new List<int>();
            var options = new List<PromptOption>();

            for (int t = 0; t < state.TeamCount; t++)
            {
                if (t == team)
                    continue;

                targets.Add(t);
                options.Add(new PromptOption(
                    $"Peek at Player {t + 1}'s Bandit",
                    null,
                    PromptTarget.Panel(t)));
            }

            if (options.Count == 0)
                return null;

            return new PeekPrompt(state, team, targets, options);
        }

        public override DecisionPrompt Choose(int index) =>
            PeekRevealPrompt.Create(state, Team, targets[index]);
    }

    /// Peek, step 2: the Bandit is shown on the board until the player presses Continue.
    public sealed class PeekRevealPrompt : DecisionPrompt
    {
        private readonly int target;

        private PeekRevealPrompt(int team, int target, string detail, List<PromptOption> options)
            : base(team, $"Peek: Player {target + 1}'s Bandit", detail, options)
        {
            this.target = target;
        }

        public static PeekRevealPrompt Create(GameState state, int team, int target)
        {
            Pawn bandit = state.GetPawnByRole(target, PawnType.Bandit);

            string detail = bandit != null && bandit.AtCastle
                ? $"Player {target + 1}'s Bandit is in their castle. Press Continue when you are done."
                : $"Player {target + 1}'s Bandit is shown on the board. Press Continue when you have seen it. It is hidden again after that.";

            var options = new List<PromptOption> { new PromptOption("Continue") };

            return new PeekRevealPrompt(team, target, detail, options);
        }

        public override int RevealedBanditTeam => target;

        public override bool AllowsCancel => false;

        public override DecisionPrompt Choose(int index) => null;
    }
}