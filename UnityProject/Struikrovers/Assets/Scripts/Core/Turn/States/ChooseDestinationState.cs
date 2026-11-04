using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Royalty, Squire and Bandit pick where to stop. A Bandit may instead click an enemy
    /// Royalty to preview an abduction.
    /// A Squire may fight enemy Squires on the way, and that fight is not an action:
    /// - an enemy Squire it can reach right now: fight it and keep the steps that are left;
    /// - a tile behind one or more enemy Squires: fight them all, then end on that tile.
    /// The first click that commits to a move closes the window for Reroll and Double Roll.
    public sealed class ChooseDestinationState : TurnState
    {
        private HashSet<Vector2Int> destinations = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> abductionTargets = new HashSet<Vector2Int>();
        private Dictionary<Vector2Int, int> fightSteps = new Dictionary<Vector2Int, int>();
        private HashSet<Vector2Int> fightTargets = new HashSet<Vector2Int>();
        private Dictionary<Vector2Int, BlockedPath> blockedPaths = new Dictionary<Vector2Int, BlockedPath>();
        private HashSet<Vector2Int> blockedTargets = new HashSet<Vector2Int>();

        public ChooseDestinationState(TurnManager turn) : base(turn) { }

        public override IReadOnlyCollection<Vector2Int> Destinations => destinations;
        public override IReadOnlyCollection<Vector2Int> AbductionTargets => abductionTargets;
        public override IReadOnlyCollection<Vector2Int> MoveFightTargets => fightTargets;
        public override IReadOnlyCollection<Vector2Int> BlockedDestinations => blockedTargets;

        public override void Enter()
        {
            destinations = Turn.Movement.GetDestinations(Pawn, Turn.RolledMovement);

            abductionTargets = Pawn.Type == PawnType.Bandit
                ? Turn.AbductionRules.GetTargets(Pawn, Turn.RolledMovement)
                : new HashSet<Vector2Int>();

            fightSteps = Turn.Movement.GetSquireFightTargets(Pawn, Turn.RolledMovement);
            fightTargets = new HashSet<Vector2Int>(fightSteps.Keys);

            // Tiles behind enemy Squires. The Squires you can fight right now keep their own highlight.
            blockedPaths = Turn.Movement.GetBlockedSquireDestinations(Pawn, Turn.RolledMovement);
            foreach (Vector2Int tile in fightSteps.Keys)
                blockedPaths.Remove(tile);

            blockedTargets = new HashSet<Vector2Int>(blockedPaths.Keys);
        }

        public override void OnTileClicked(Vector2Int position)
        {
            // Fight an enemy Squire that blocks the way, then keep moving.
            if (fightSteps.TryGetValue(position, out int steps))
            {
                Turn.CloseMovementRoll();
                FightWhileMoving(position, steps);
                return;
            }

            // Fight through every Squire on the way to a tile behind them.
            if (blockedPaths.TryGetValue(position, out BlockedPath path))
            {
                Turn.CloseMovementRoll();
                FightThroughPath(path);
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

            Turn.CloseMovementRoll();
            Pawn.Position = position;

            // Bandit fights on an occupied hiding place, delivery at the castle, and the
            // action phase are handled by the turn manager.
            Turn.CompleteMovement();
        }

        private void FightWhileMoving(Vector2Int tile, int steps)
        {
            Pawn pawn = Pawn;

            Turn.FightSquiresAt(pawn, tile, results =>
            {
                if (results.Count == 0)
                    return;

                // The Squire lost and is back in its castle: the activation is over.
                if (!results.TrueForAll(f => f.AttackerWon))
                {
                    Turn.FinishActivation();
                    return;
                }

                // The winner takes the tile (leaving the castle if it started there) and keeps
                // the steps it has left. The fight did not use up its action.
                pawn.Position = tile;
                Turn.RolledMovement = Mathf.Max(0, Turn.RolledMovement - steps);

                // Stolen equipment is resolved first (prompts), then the move continues.
                Turn.RunPending(() =>
                {
                    if (Turn.RolledMovement == 0)
                        Turn.CompleteMovement();
                    else
                        Turn.ChangeState(new ChooseDestinationState(Turn));
                });
            });
        }

        private void FightThroughPath(BlockedPath path)
        {
            Pawn pawn = Pawn;
            int index = 0;

            // Fight the Squires in the order they stand on the route. A loss ends the activation.
            void FightNextBlocker()
            {
                if (index >= path.Blockers.Count)
                {
                    // Every blocker is beaten: the Squire ends on the tile it clicked.
                    pawn.Position = path.Destination;
                    Turn.RunPending(Turn.CompleteMovement);
                    return;
                }

                Vector2Int tile = path.Blockers[index++];

                Turn.FightSquiresAt(pawn, tile, results =>
                {
                    if (!results.TrueForAll(f => f.AttackerWon))
                    {
                        Turn.FinishActivation();
                        return;
                    }

                    FightNextBlocker();
                });
            }

            FightNextBlocker();
        }

        /// Works the reachable tiles out again, after equipment changed (movement items change them).
        public void Refresh() => Enter();
    }
}