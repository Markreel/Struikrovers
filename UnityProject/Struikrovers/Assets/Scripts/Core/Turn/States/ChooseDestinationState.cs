using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Royalty, Squire and Bandit pick where to stop. A Bandit may instead click an
    /// enemy Royalty to preview an abduction.
    public sealed class ChooseDestinationState : TurnState
    {
        private HashSet<Vector2Int> destinations = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> abductionTargets = new HashSet<Vector2Int>();

        public ChooseDestinationState(TurnManager turn) : base(turn) { }

        public override IReadOnlyCollection<Vector2Int> Destinations => destinations;
        public override IReadOnlyCollection<Vector2Int> AbductionTargets => abductionTargets;

        public override void Enter()
        {
            destinations = Turn.Movement.GetDestinations(Pawn, Turn.RolledMovement);

            abductionTargets = Pawn.Type == PawnType.Bandit
                ? Turn.AbductionRules.GetTargets(Pawn, Turn.RolledMovement)
                : new HashSet<Vector2Int>();
        }

        public override void OnTileClicked(Vector2Int position)
        {
            // Selecting an enemy Royalty previews an abduction without committing it.
            if (abductionTargets.Contains(position))
            {
                if (Turn.AbductionRules.TryPlan(Pawn, position, Turn.RolledMovement, out AbductionPlan plan))
                    Turn.ChangeState(new AbductionPreviewState(Turn, plan, abductionTargets));

                return;
            }

            if (!destinations.Contains(position))
                return;

            Pawn.Position = position;

            // Fights (Squire on a Squire, Bandit on an occupied hiding place), delivery at
            // the castle, and the action phase are handled by the turn manager.
            Turn.CompleteMovement();
        }
    }
}