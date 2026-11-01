using UnityEngine;

namespace Struikrovers.Core
{
    public enum FightKind
    {
        /// The defender is a Squire (the attacker is a Squire or a Bandit).
        VsSquire,
        /// Two Bandits fighting over a hiding place.
        VsBandit
    }

    /// A fight whose dice are rolled but not yet applied. The attacker can still change its die
    /// with a card, or reroll a tie. Nothing happens to the pawns until CombatRules.Resolve.
    public sealed class PendingFight
    {
        public Pawn Attacker { get; }
        public Pawn Defender { get; }
        public Vector2Int Position { get; }
        public FightKind Kind { get; }

        public DiceRoll AttackerDice { get; internal set; }
        public DiceRoll DefenderDice { get; internal set; }
        public int AttackerModifier { get; internal set; }
        public int DefenderModifier { get; internal set; }
        public int Rerolls { get; internal set; }

        public int AttackerTotal => AttackerDice.Total + AttackerModifier;
        public int DefenderTotal => DefenderDice.Total + DefenderModifier;
        public bool IsTie => AttackerTotal == DefenderTotal;

        /// A tie can be rolled again, until the safety cap (then the defender wins).
        public bool CanRerollTie => IsTie && Rerolls < GameConfig.MaxFightRerolls;

        public PendingFight(Pawn attacker, Pawn defender, Vector2Int position, FightKind kind)
        {
            Attacker = attacker;
            Defender = defender;
            Position = position;
            Kind = kind;
        }
    }
}