using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public enum SniffClue
    {
        NoCaptive,
        Nearby,           // captive is inside the 3x3 area
        Stronger,         // closer than at the previous sniff
        Fainter,          // further than at the previous sniff
        Unchanged,
        DirectionUnclear  // no previous sniff to compare with
    }

    public readonly struct AttackResult
    {
        /// Every Bandit found on the attacked tile, including the attacker's own team.
        public readonly int BanditsFound;
        /// Royalty pawns that were freed and now stand on the board again.
        public readonly IReadOnlyList<Pawn> Rescued;
        /// Captives that stayed captive because no free grass tile was found.
        public readonly int Blocked;

        public AttackResult(int banditsFound, IReadOnlyList<Pawn> rescued, int blocked)
        {
            BanditsFound = banditsFound;
            Rescued = rescued;
            Blocked = blocked;
        }

        public int ReleasedCount => Rescued?.Count ?? 0;
    }

    public sealed class BloodhoundRules
    {
        private readonly GameState state;
        private readonly MovementRules movement;

        // Distance at the previous sniff. Only meaningful inside one activation,
        // because BeginActivation overwrites it before the first move.
        private readonly Dictionary<Pawn, int> previousDistances = new Dictionary<Pawn, int>();

        public BloodhoundRules(GameState state, MovementRules movement)
        {
            this.state = state;
            this.movement = movement;
        }

        /// Call before the Bloodhound's first move, so the first sniff can compare
        /// its new position with the starting position.
        public void BeginActivation(Pawn bloodhound)
        {
            Pawn captor = state.GetCaptor(bloodhound);

            if (captor != null)
                previousDistances[bloodhound] = BoardGrid.Manhattan(captor.Position, bloodhound.Position);
            else
                previousDistances.Remove(bloodhound);
        }

        public SniffClue Sniff(Pawn bloodhound)
        {
            if (bloodhound == null || bloodhound.Type != PawnType.Bloodhound)
                return SniffClue.NoCaptive;

            // The captor carries this Bloodhound's Royalty.
            Pawn captor = state.GetCaptor(bloodhound);
            if (captor == null)
                return SniffClue.NoCaptive;

            Vector2Int difference = captor.Position - bloodhound.Position;
            int distance = BoardGrid.Manhattan(captor.Position, bloodhound.Position);

            SniffClue clue;

            // The 3x3 area is the surrounding eight tiles plus the Bloodhound's own tile.
            if (Mathf.Abs(difference.x) <= 1 && Mathf.Abs(difference.y) <= 1)
                clue = SniffClue.Nearby;
            else if (previousDistances.TryGetValue(bloodhound, out int previous))
                clue = distance < previous ? SniffClue.Stronger
                     : distance > previous ? SniffClue.Fainter
                     : SniffClue.Unchanged;
            else
                clue = SniffClue.DirectionUnclear;

            previousDistances[bloodhound] = distance;
            return clue;
        }

        /// Bushes and trees within attack range (straight-line tile distance).
        public HashSet<Vector2Int> GetAttackTargets(Pawn bloodhound)
        {
            var targets = new HashSet<Vector2Int>();

            if (bloodhound == null || bloodhound.Type != PawnType.Bloodhound)
                return targets;

            BoardGrid grid = state.Grid;

            for (int x = 0; x < grid.Columns; x++)
            {
                for (int y = 0; y < grid.Rows; y++)
                {
                    var position = new Vector2Int(x, y);

                    if (BoardGrid.Manhattan(position, bloodhound.Position) > GameConfig.BloodhoundAttackRange)
                        continue;

                    if (movement.CanBanditEnd(position))
                        targets.Add(position);
                }
            }

            return targets;
        }

        /// Attacks a hiding place. Every enemy Bandit there releases its captives and
        /// goes back to its castle. The attacker's own team's Bandits are unaffected.
        public AttackResult ResolveAttack(Pawn bloodhound, Vector2Int target)
        {
            List<Pawn> banditsFound = state.FindAll(p =>
                p.Type == PawnType.Bandit && !p.AtCastle && p.Position == target);

            var rescued = new List<Pawn>();
            var reserved = new HashSet<Vector2Int>();
            int blocked = 0;

            foreach (Pawn bandit in banditsFound)
            {
                if (bandit.Team == bloodhound.Team || !bandit.IsCarrying)
                    continue;

                // Copy: freeing a captive changes bandit.Captives.
                foreach (Pawn royalty in new List<Pawn>(bandit.Captives))
                {
                    if (!TryFindFreeTile(target, reserved, out Vector2Int tile))
                    {
                        // Leave the captive intact if there is nowhere legal to place the Royalty.
                        blocked++;
                        continue;
                    }

                    state.FreeCaptive(bandit, royalty, tile);
                    reserved.Add(tile);
                    rescued.Add(royalty);
                }

                // A Bandit that lost all its captives returns to its own castle.
                if (!bandit.IsCarrying)
                    state.SendToCastle(bandit);
            }

            return new AttackResult(banditsFound.Count, rescued, blocked);
        }

        private bool TryFindFreeTile(Vector2Int origin, HashSet<Vector2Int> reserved, out Vector2Int tile)
        {
            return movement.Pathfinder.TryFindNearest(
                origin,
                p => state.Grid.GetTerrain(p) == TerrainType.Grass &&
                     !reserved.Contains(p) &&
                     !state.IsOccupied(p),
                out tile);
        }
    }
}