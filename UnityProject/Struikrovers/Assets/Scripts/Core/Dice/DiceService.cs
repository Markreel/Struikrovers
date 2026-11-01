using System;
using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// Every die in the game is rolled here, so advantage, disadvantage, Double Roll and
    /// Reroll follow one set of rules.
    public sealed class DiceService
    {
        private readonly GameState state;
        private readonly Func<int> rollDie;

        public DiceService(GameState state, Func<int> rollDie)
        {
            this.state = state;
            this.rollDie = rollDie;
        }

        /// A Drunk curse gives disadvantage and overrides every advantage. Otherwise
        /// equipment can give advantage. Advantage is yes or no: two sources are still one.
        public RollMode GetMode(Pawn pawn, RollKind kind)
        {
            if (pawn == null)
                return RollMode.Normal;

            if (CurseEffects.RollsWithDisadvantage(state, pawn))
                return RollMode.Disadvantage;

            if (EquipmentEffects.GrantsAdvantage(pawn, kind))
                return RollMode.Advantage;

            return RollMode.Normal;
        }

        public DiceRoll Roll(Pawn pawn, RollKind kind) => Build(pawn, kind, false, false);

        /// Reroll card: a fresh roll that replaces the old one (a doubled roll stays doubled).
        /// With 'reject', the roll is repeated until it is accepted (card rolls can't tie).
        public DiceRoll Reroll(DiceRoll previous, Func<DiceRoll, bool> reject = null)
        {
            DiceRoll roll = null;

            for (int i = 0; i < GameConfig.MaxCardRollAttempts; i++)
            {
                roll = Build(previous.Pawn, previous.Kind, previous.Doubled, true);
                if (reject == null || !reject(roll))
                    break;
            }

            return roll;
        }

        /// Double Roll card: a second die is rolled (with the same advantage or disadvantage)
        /// and added to the first.
        public DiceRoll DoubleUp(DiceRoll previous, Func<DiceRoll, bool> reject = null)
        {
            DiceRoll roll = null;

            for (int i = 0; i < GameConfig.MaxCardRollAttempts; i++)
            {
                roll = previous.WithExtraDie(RollOne(previous.Mode));
                if (reject == null || !reject(roll))
                    break;
            }

            return roll;
        }

        private DiceRoll Build(Pawn pawn, RollKind kind, bool doubled, bool rerolled)
        {
            RollMode mode = GetMode(pawn, kind);
            var dice = new List<DieResult> { RollOne(mode) };

            if (doubled)
                dice.Add(RollOne(mode));

            return new DiceRoll(pawn, kind, mode, doubled, rerolled, dice);
        }

        private DieResult RollOne(RollMode mode)
        {
            if (mode == RollMode.Normal)
                return new DieResult(rollDie());

            int a = rollDie();
            int b = rollDie();

            return mode == RollMode.Advantage
                ? new DieResult(Math.Max(a, b), Math.Min(a, b))
                : new DieResult(Math.Min(a, b), Math.Max(a, b));
        }
    }
}