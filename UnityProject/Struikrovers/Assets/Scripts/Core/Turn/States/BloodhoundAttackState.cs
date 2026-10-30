using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// After the second move and sniff: attack one bush or tree in range, or skip.
    public sealed class BloodhoundAttackState : TurnState
    {
        private HashSet<Vector2Int> targets = new HashSet<Vector2Int>();

        public BloodhoundAttackState(TurnManager turn) : base(turn) { }

        public override PrimaryAction Primary => PrimaryAction.Finish;
        public override IReadOnlyCollection<Vector2Int> AttackTargets => targets;

        public override void Enter()
        {
            targets = Turn.BloodhoundRules.GetAttackTargets(Pawn);
        }

        public override void OnPrimaryButton() => Turn.FinishActivation();

        public override void OnTileClicked(Vector2Int position)
        {
            if (!targets.Contains(position))
            {
                Turn.Raise(TurnEvent.AttackInvalidTarget(Pawn));
                return;
            }

            AttackResult result = Turn.BloodhoundRules.ResolveAttack(Pawn, position);

            if (result.Blocked > 0)
                Turn.Raise(TurnEvent.AttackBlocked(Pawn, result.Blocked));

            if (result.ReleasedCount == 0)
            {
                Turn.Raise(result.BanditsFound == 0
                    ? TurnEvent.AttackFoundNothing(Pawn)
                    : TurnEvent.AttackNoCaptives(Pawn));

                Turn.FinishActivation();
                return;
            }

            Turn.Raise(TurnEvent.AttackRescued(Pawn, result.ReleasedCount));
            Turn.BeginRescueRuns(result.Rescued);
        }
    }
}