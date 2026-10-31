using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The Bandit has picked an enemy Royalty and now chooses where to end up.
    /// Clicking another enemy Royalty switches the preview; Cancel goes back.
    public sealed class AbductionPreviewState : TurnState
    {
        private readonly HashSet<Vector2Int> abductionTargets;

        public AbductionPlan Plan { get; }

        public AbductionPreviewState(
            TurnManager turn, AbductionPlan plan, HashSet<Vector2Int> abductionTargets)
            : base(turn)
        {
            Plan = plan;
            this.abductionTargets = abductionTargets;
        }

        public override bool CanCancel => true;
        public override IReadOnlyCollection<Vector2Int> Destinations => Plan.Destinations;
        public override IReadOnlyCollection<Vector2Int> AbductionTargets => abductionTargets;

        public override void OnTileClicked(Vector2Int position)
        {
            // Pick a different Royalty.
            if (abductionTargets.Contains(position))
            {
                if (Turn.AbductionRules.TryPlan(Pawn, position, Turn.RolledMovement, out AbductionPlan other))
                    Turn.ChangeState(new AbductionPreviewState(Turn, other, abductionTargets));

                return;
            }

            if (!Plan.Destinations.Contains(position))
                return;

            Pawn royalty = Plan.Royalty;

            // Enemy Squires on the Royalty's tile guard it. The Bandit has to beat them first;
            // losing sends it to its castle and ends the activation.
            if (!Turn.FightGuards(Pawn, royalty.Position, out List<Pawn> rescued))
            {
                if (rescued.Count > 0)
                    Turn.BeginRescueRuns(rescued, false);
                else
                    Turn.FinishActivation();

                return;
            }

            if (!Turn.AbductionRules.CommitPlan(Pawn, Plan, position))
                return;

            Turn.Raise(TurnEvent.Abducted(royalty, Pawn));

            // The Bandit may end on an occupied hiding place (a fight, with the abducting
            // penalty) or run straight home (delivery). Then the action phase follows.
            Turn.CompleteMovement();
        }

        public override void OnCancel()
        {
            // Restores the original Bandit movement options.
            Turn.ChangeState(new ChooseDestinationState(Turn));
        }
    }
}