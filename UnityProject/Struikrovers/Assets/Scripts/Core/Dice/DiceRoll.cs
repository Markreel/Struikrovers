using System.Collections.Generic;

namespace Struikrovers.Core
{
    public enum RollKind { Movement, Combat }
    public enum RollMode { Normal, Advantage, Disadvantage }

    /// One die as rolled. With advantage or disadvantage two dice are rolled and one is kept.
    public readonly struct DieResult
    {
        public readonly int Kept;
        /// The die that was not kept (only when HasOther).
        public readonly int Other;
        public readonly bool HasOther;

        public DieResult(int kept) { Kept = kept; Other = 0; HasOther = false; }
        public DieResult(int kept, int other) { Kept = kept; Other = other; HasOther = true; }
    }

    /// The result of one roll: one die, or two after a Double Roll. Modifiers are not included.
    public sealed class DiceRoll
    {
        private readonly List<DieResult> dice;

        public Pawn Pawn { get; }
        public RollKind Kind { get; }
        public RollMode Mode { get; }
        public bool Doubled { get; }
        public bool Rerolled { get; }
        public IReadOnlyList<DieResult> Dice => dice;
        public int Total { get; }

        public DiceRoll(Pawn pawn, RollKind kind, RollMode mode, bool doubled, bool rerolled, List<DieResult> dice)
        {
            Pawn = pawn;
            Kind = kind;
            Mode = mode;
            Doubled = doubled;
            Rerolled = rerolled;
            this.dice = dice;

            int total = 0;
            foreach (DieResult die in dice)
                total += die.Kept;
            Total = total;
        }

        internal DiceRoll WithExtraDie(DieResult die)
        {
            var list = new List<DieResult>(dice) { die };
            return new DiceRoll(Pawn, Kind, Mode, true, Rerolled, list);
        }
    }
}