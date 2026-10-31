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
        /// Bandits found on the attacked tile (0 or 1), including one of the attacker's own team.
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
        private readonly RescueRules rescue;

        // Distance at the previous sniff. Only meaningful inside one activation,
        // because BeginActivation overwrites it before the first move.
        private readonly Dictionary<Pawn, int> previousDistances = new Dictionary<Pawn, int>();

        public BloodhoundRules(GameState state, RescueRules rescue)
        {
            this.state = state;
            this.rescue = rescue;
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

        /// Bushes and trees within attack range (straight-line tile distance), plus
        /// stumps where a Bandit stands exposed.
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

                    if (state.IsAttackable(position))
                        targets.Add(position);
                }
            }

            return targets;
        }

        /// Attacks a hiding place. An enemy Bandit there releases its captives and
        /// goes back to its castle. The attacker's own team's Bandit is unaffected.
        public AttackResult ResolveAttack(Pawn bloodhound, Vector2Int target)
        {
            Pawn bandit = state.GetBanditAt(target);

            var rescued = new List<Pawn>();
            int blocked = 0;

            if (bandit != null && bandit.Team != bloodhound.Team && bandit.IsCarrying)
                blocked = rescue.ReleaseCaptives(bandit, target, new HashSet<Vector2Int>(), rescued);

            return new AttackResult(bandit != null ? 1 : 0, rescued, blocked);
        }
    }
}