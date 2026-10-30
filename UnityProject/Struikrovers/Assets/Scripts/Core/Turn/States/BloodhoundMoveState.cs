using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// One of the Bloodhound's two moves. It sniffs right after each move.
    public sealed class BloodhoundMoveState : TurnState
    {
        private HashSet<Vector2Int> destinations = new HashSet<Vector2Int>();

        public BloodhoundMoveState(TurnManager turn) : base(turn) { }

        public override IReadOnlyCollection<Vector2Int> Destinations => destinations;

        public override void Enter()
        {
            destinations = Turn.Movement.GetDestinations(Pawn, Turn.RolledMovement);
        }

        public override void OnTileClicked(Vector2Int position)
        {
            if (!destinations.Contains(position))
                return;

            Pawn.Position = position;
            Turn.BloodhoundMovesRemaining--;

            Turn.Raise(TurnEvent.Sniffed(Pawn, Turn.BloodhoundRules.Sniff(Pawn)));

            if (Turn.BloodhoundMovesRemaining > 0)
                Turn.ChangeState(new BloodhoundMoveState(Turn));
            else
                Turn.ChangeState(new BloodhoundAttackState(Turn));
        }
    }
}