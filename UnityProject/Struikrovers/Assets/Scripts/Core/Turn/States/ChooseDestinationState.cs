using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Royalty, Squire and Bandit pick where to stop. A Bandit may instead click an enemy
    /// Royalty to preview an abduction. A Squire may click an enemy Squire to fight it on
    /// the way: that fight is not an action, and a win lets it continue with the steps it
    /// has left.
    public sealed class ChooseDestinationState : TurnState
    {
        private HashSet<Vector2Int> destinations = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> abductionTargets = new HashSet<Vector2Int>();
        private Dictionary<Vector2Int, int> fightSteps = new Dictionary<Vector2Int, int>();
        private HashSet<Vector2Int> fightTargets = new HashSet<Vector2Int>();

        public ChooseDestinationState(TurnManager turn) : base(turn) { }

        public override IReadOnlyCollection<Vector2Int> Destinations => destinations;
        public override IReadOnlyCollection<Vector2Int> AbductionTargets => abductionTargets;

        // Enemy Squires that can be fought on the way are highlighted as quick targets (pink).
        public override IReadOnlyCollection<Vector2Int> QuickActionTargets => fightTargets;

        public override void Enter()
        {
            destinations = Turn.Movement.GetDestinations(Pawn, Turn.RolledMovement);

            abductionTargets = Pawn.Type == PawnType.Bandit
                ? Turn.AbductionRules.GetTargets(Pawn, Turn.RolledMovement)
                : new HashSet<Vector2Int>();

            fightSteps = Turn.Movement.GetSquireFightTargets(Pawn, Turn.RolledMovement);
            fightTargets = new HashSet<Vector2Int>(fightSteps.Keys);
        }

        public override void OnTileClicked(Vector2Int position)
        {
            // Fight an enemy Squire that blocks the way.
            if (fightSteps.TryGetValue(position, out int steps))
            {
                FightWhileMoving(position, steps);
                return;
            }

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

            // Bandit fights on an occupied hiding place, delivery at the castle, and the
            // action phase are handled by the turn manager.
            Turn.CompleteMovement();
        }

        private void FightWhileMoving(Vector2Int tile, int steps)
        {
            List<FightResult> fights = Turn.CombatRules.FightSquiresAt(Pawn, tile);
            if (fights.Count == 0)
                return;

            foreach (FightResult fight in fights)
                Turn.Raise(TurnEvent.SquireFight(fight));

            // The Squire lost and is back in its castle: the activation is over.
            if (!fights.TrueForAll(f => f.AttackerWon))
            {
                Turn.FinishActivation();
                return;
            }

            // The winner takes the tile (leaving the castle if it started there) and keeps
            // the steps it has left. The fight did not use up its action.
            Pawn.Position = tile;
            Turn.RolledMovement = Mathf.Max(0, Turn.RolledMovement - steps);

            if (Turn.RolledMovement == 0)
                Turn.CompleteMovement();
            else
                Turn.ChangeState(new ChooseDestinationState(Turn));
        }
    }
}