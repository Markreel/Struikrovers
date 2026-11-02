using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// What a hand card does when played. Phase D adds the rest of the hand cards here.
    public interface IHandCardEffect
    {
        /// True when the card can be played right now. 'reason' explains why not.
        bool CanPlay(TurnManager turn, out string reason);

        /// The card has already been moved to the discard pile when this runs.
        void Play(TurnManager turn);
    }

    public static class HandCardEffects
    {
        public const string RerollId = "reroll";
        public const string DoubleRollId = "double-roll";
        public const string NabId = "nab";
        public const string YoinkId = "yoink";
        public const string HandOverId = "hand-over";
        public const string SwitcherooId = "switcheroo";
        public const string PeekId = "peek";

        private static readonly Dictionary<string, IHandCardEffect> effects =
            new Dictionary<string, IHandCardEffect>
            {
                { RerollId, new RerollEffect() },
                { DoubleRollId, new DoubleRollEffect() },
                { NabId, new NabEffect() },
                { YoinkId, new YoinkEffect() },
                { HandOverId, new HandOverEffect() },
                { SwitcherooId, new SwitcherooEffect() },
                { PeekId, new PeekEffect() }

            };

        /// Null for cards that can't be played yet.
        public static IHandCardEffect Get(CardDefinition card) =>
            card != null && effects.TryGetValue(card.Id, out IHandCardEffect effect) ? effect : null;

        public static bool ChangesRoll(CardDefinition card) =>
            card != null && (card.Id == RerollId || card.Id == DoubleRollId);
    }

    /// Replace one of your own rolls with a fresh one. You keep the new result.
    internal sealed class RerollEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason) => turn.CanChangeRoll(out reason);
        public void Play(TurnManager turn) => turn.RerollCurrentRoll();
    }

    /// Roll a second die and add it to your own roll.
    internal sealed class DoubleRollEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason) => turn.CanChangeRoll(out reason);
        public void Play(TurnManager turn) => turn.DoubleCurrentRoll();
    }

    /// Steal an opponent's equipped card of your choice. A Martyr Shield or Chainmail still protects it.
    internal sealed class NabEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason)
        {
            reason = null;

            // Equipment can't change while a fight waits: its modifiers are already counted.
            if (!turn.CanManageEquipment)
            {
                reason = "equipment can't be taken while a fight is waiting";
                return false;
            }

            if (!HandCardTargets.AnyOpponentHasEquipment(turn.State, turn.TurnOwner))
            {
                reason = "no opponent has equipped cards";
                return false;
            }

            return true;
        }

        public void Play(TurnManager turn) =>
    turn.AskDuringTurn(NabPrompt.Create(turn.Cards, turn.State, turn.TurnOwner));
    }

    /// Steal a hand card from an opponent of your choice, blind (or with a look, with a Measuring Monocle).
    internal sealed class YoinkEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason)
        {
            reason = null;

            if (!HandCardTargets.AnyOpponentHasHandCards(turn.State, turn.TurnOwner))
            {
                reason = "no opponent has hand cards";
                return false;
            }

            return true;
        }

        public void Play(TurnManager turn) =>
    turn.AskDuringTurn(YoinkPrompt.Create(turn.Cards, turn.State, turn.TurnOwner));
    }

    /// Swap whole hands with an opponent. You must hold at least 1 other hand card.
    internal sealed class HandOverEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason)
        {
            reason = null;

            // The card being played is still in the hand at this point, so 2 means "1 other card".
            if (turn.State.Teams[turn.TurnOwner].Cards < 2)
            {
                reason = "you need at least 1 other hand card";
                return false;
            }

            if (!HandCardTargets.AnyOpponentHasHandCards(turn.State, turn.TurnOwner))
            {
                reason = "no opponent has hand cards";
                return false;
            }

            return true;
        }

        public void Play(TurnManager turn) =>
            turn.AskDuringTurn(HandOverPrompt.Create(turn.Cards, turn.State, turn.TurnOwner));
    }

    /// Swap an opponent pawn's equipment with your pawn of the same type. Both need at least 1 item.
    internal sealed class SwitcherooEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason)
        {
            reason = null;

            // Equipment can't change while a fight waits: its modifiers are already counted.
            if (!turn.CanManageEquipment)
            {
                reason = "equipment can't be swapped while a fight is waiting";
                return false;
            }

            if (!HandCardTargets.AnySwitcheroo(turn.State, turn.TurnOwner))
            {
                reason = "no pawn pair has equipment on both sides";
                return false;
            }

            return true;
        }

        public void Play(TurnManager turn) =>
            turn.AskDuringTurn(SwitcherooPrompt.Create(turn.Cards, turn.State, turn.TurnOwner));
    }

    /// Look at another player's Bandit until you press Continue.
    internal sealed class PeekEffect : IHandCardEffect
    {
        public bool CanPlay(TurnManager turn, out string reason)
        {
            reason = null;

            if (!turn.HideEnemyBandits)
            {
                reason = "enemy Bandits aren't hidden, so there is nothing to peek at";
                return false;
            }

            return true;
        }

        public void Play(TurnManager turn) =>
            turn.AskDuringTurn(PeekPrompt.Create(turn.State, turn.TurnOwner));
    }
}