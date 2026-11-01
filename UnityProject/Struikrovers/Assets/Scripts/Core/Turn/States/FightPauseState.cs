using System;

namespace Struikrovers.Core
{
    /// The dice of a fight are rolled and shown. The attacker may play Reroll or Double Roll
    /// (through the card menu). A tie must be rolled again. Then Keep result applies the fight.
    public sealed class FightPauseState : TurnState
    {
        private readonly Action<FightResult> onResolved;

        public PendingFight Fight { get; }

        public FightPauseState(TurnManager turn, PendingFight fight, Action<FightResult> onResolved)
            : base(turn)
        {
            Fight = fight;
            this.onResolved = onResolved;
        }

        public override PrimaryAction Primary =>
            Fight.CanRerollTie ? PrimaryAction.RerollTie : PrimaryAction.KeepResult;

        public override void OnPrimaryButton()
        {
            if (Fight.CanRerollTie)
            {
                Turn.CombatRules.RerollTie(Fight);
                Turn.Raise(TurnEvent.FightRolled(Fight));
                return;
            }

            // Whatever the callback does next changes the state.
            onResolved(Turn.CombatRules.Resolve(Fight));
        }
    }
}