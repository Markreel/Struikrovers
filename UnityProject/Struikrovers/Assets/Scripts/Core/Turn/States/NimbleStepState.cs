using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Nimble Cloak: after the action, one optional extra step (movement gear applies, budget 1).
    public sealed class NimbleStepState : TurnState
    {
        private HashSet<Vector2Int> steps = new HashSet<Vector2Int>();

        public NimbleStepState(TurnManager turn) : base(turn) { }

        internal static HashSet<Vector2Int> FindSteps(TurnManager turn, Pawn pawn)
        {
            HashSet<Vector2Int> result = turn.Movement.GetDestinations(pawn, 1);
            result.Remove(pawn.Position);
            return result;
        }

        public override PrimaryAction Primary => PrimaryAction.Finish;
        public override IReadOnlyCollection<Vector2Int> Destinations => steps;

        public override void Enter() => steps = FindSteps(Turn, Pawn);

        public override void OnTileClicked(Vector2Int position)
        {
            if (!steps.Contains(position))
                return;

            Pawn.Position = position;
            Turn.FinishActivation();
        }

        public override void OnPrimaryButton() => Turn.FinishActivation();
    }
}