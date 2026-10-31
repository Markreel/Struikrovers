using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The preview shown after a Bandit picks an enemy Royalty to run over.
    public readonly struct AbductionPlan
    {
        public readonly Pawn Royalty;
        public readonly int StepsToRoyalty;
        public readonly int RemainingMovement;
        public readonly HashSet<Vector2Int> Destinations;

        public AbductionPlan(Pawn royalty, int stepsToRoyalty, int remainingMovement,
                             HashSet<Vector2Int> destinations)
        {
            Royalty = royalty;
            StepsToRoyalty = stepsToRoyalty;
            RemainingMovement = remainingMovement;
            Destinations = destinations;
        }
    }

    public readonly struct DeliveryResult
    {
        public readonly int Delivered;
        public readonly int WoodStolen;

        public DeliveryResult(int delivered, int woodStolen)
        {
            Delivered = delivered;
            WoodStolen = woodStolen;
        }

        public bool Happened => Delivered > 0;
    }

    public sealed class AbductionRules
    {
        private readonly GameState state;
        private readonly MovementRules movement;

        public AbductionRules(GameState state, MovementRules movement)
        {
            this.state = state;
            this.movement = movement;
        }

        /// Action: abduct an enemy Royalty standing next to the Bandit.
        public bool TryAbductAdjacent(Pawn bandit, Vector2Int target, out Pawn royalty)
        {
            royalty = null;

            if (bandit == null || bandit.Type != PawnType.Bandit || bandit.AtCastle)
                return false;

            if (!BoardGrid.IsAdjacent(bandit.Position, target))
                return false;

            Pawn candidate = state.GetRoyaltyAt(target);
            if (candidate == null || !state.TryCapture(candidate, bandit))
                return false;

            royalty = candidate;
            return true;
        }

        /// True when the Bandit could abduct the Royalty on this adjacent tile right now.
        public bool CanAbductAdjacent(Pawn bandit, Vector2Int target)
        {
            if (bandit == null || bandit.Type != PawnType.Bandit || bandit.AtCastle)
                return false;

            if (!BoardGrid.IsAdjacent(bandit.Position, target))
                return false;

            Pawn candidate = state.GetRoyaltyAt(target);
            return candidate != null && candidate.Team != bandit.Team && state.CanCarryMore(bandit);
        }

        /// Tiles next to the Bandit that hold an enemy Royalty it could still abduct.
        public HashSet<Vector2Int> GetAdjacentTargets(Pawn bandit)
        {
            var targets = new HashSet<Vector2Int>();

            if (bandit == null || bandit.Type != PawnType.Bandit ||
                bandit.AtCastle || !state.CanCarryMore(bandit))
            {
                return targets;
            }

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Type == PawnType.Royalty && !pawn.AtCastle &&
                    pawn.Team != bandit.Team &&
                    BoardGrid.IsAdjacent(bandit.Position, pawn.Position))
                {
                    targets.Add(pawn.Position);
                }
            }

            return targets;
        }

        /// Where the Bandit could end up after abducting the Royalty on this tile, given the steps
        /// that are left once it has reached the Royalty. Empty when there is no legal ending tile
        /// (no bush, tree or own castle within the remaining steps).
        private HashSet<Vector2Int> GetPostAbductionDestinations(
            Pawn bandit, Vector2Int royaltyPosition, int stepsToRoyalty, int movementSteps)
        {
            int remaining = movementSteps - stepsToRoyalty;

            // The Bandit will be carrying a captive, so its own castle is a destination.
            return movement.GetBanditDestinations(
                royaltyPosition, remaining, bandit.Team, carrying: true);
        }

        /// Tiles holding an enemy Royalty that the Bandit can reach with this movement AND from
        /// which it can still reach a bush, tree or its own castle with the steps that are left.
        public HashSet<Vector2Int> GetTargets(Pawn bandit, int movementSteps)
        {
            var targets = new HashSet<Vector2Int>();

            if (bandit == null || bandit.Type != PawnType.Bandit || !state.CanCarryMore(bandit))
                return targets;

            Dictionary<Vector2Int, int> distances =
                movement.GetBanditDistances(bandit.Position, movementSteps);

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Type != PawnType.Royalty || pawn.AtCastle || pawn.Team == bandit.Team)
                    continue;

                if (!distances.TryGetValue(pawn.Position, out int steps) ||
                    steps <= 0 || steps > movementSteps)
                {
                    continue;
                }

                if (GetPostAbductionDestinations(bandit, pawn.Position, steps, movementSteps).Count == 0)
                    continue;

                targets.Add(pawn.Position);
            }

            return targets;
        }

        /// Previews an abduction by moving over a Royalty. Leaving the castle is already part of
        /// the step count because the castle is a normal tile. Fails when there is no legal
        /// ending tile afterwards.
        public bool TryPlan(Pawn bandit, Vector2Int royaltyPosition, int movementSteps,
                            out AbductionPlan plan)
        {
            plan = default;

            Pawn royalty = state.GetRoyaltyAt(royaltyPosition);
            if (bandit == null || royalty == null || royalty.Team == bandit.Team)
                return false;

            Dictionary<Vector2Int, int> distances =
                movement.GetBanditDistances(bandit.Position, movementSteps);

            if (!distances.TryGetValue(royalty.Position, out int steps))
                return false;

            HashSet<Vector2Int> destinations =
                GetPostAbductionDestinations(bandit, royalty.Position, steps, movementSteps);

            if (destinations.Count == 0)
                return false;

            plan = new AbductionPlan(royalty, steps, movementSteps - steps, destinations);
            return true;
        }

        /// Captures the previewed Royalty and moves the Bandit to the chosen destination.
        public bool CommitPlan(Pawn bandit, AbductionPlan plan, Vector2Int destination)
        {
            if (plan.Destinations == null || !plan.Destinations.Contains(destination))
                return false;

            if (!state.TryCapture(plan.Royalty, bandit))
                return false;

            bandit.Position = destination;
            return true;
        }

        /// If the Bandit stands on its own castle while carrying captives, deliver them:
        /// each Royalty returns to its castle and 2 wood moves from its team to the Bandit's.
        /// Returns a result with Delivered == 0 when nothing happened.
        public DeliveryResult DeliverIfHome(Pawn bandit)
        {
            if (bandit == null || bandit.Type != PawnType.Bandit || !bandit.IsCarrying)
                return default;

            if (bandit.Position != state.Grid.GetCastlePosition(bandit.Team))
                return default;

            // Copy: delivering a captive changes bandit.Captives.
            var delivered = new List<Pawn>(bandit.Captives);
            int totalWoodStolen = 0;

            foreach (Pawn royalty in delivered)
            {
                state.ReturnCaptiveHome(bandit, royalty);

                TeamState victim = state.Teams[royalty.Team];
                int stolen = Mathf.Min(GameConfig.WoodStolenOnDelivery, victim.Wood);
                victim.Wood -= stolen;
                totalWoodStolen += stolen;
            }

            state.Teams[bandit.Team].Wood += totalWoodStolen;
            return new DeliveryResult(delivered.Count, totalWoodStolen);
        }
    }
}