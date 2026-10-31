using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Frees the Royalty a Bandit carries. Used by the Bloodhound attack, by stabbing
    /// and by lost Bandit fights.
    public sealed class RescueRules
    {
        private readonly GameState state;
        private readonly MovementRules movement;

        public RescueRules(GameState state, MovementRules movement)
        {
            this.state = state;
            this.movement = movement;
        }

        /// Frees every captive onto the nearest free grass tile around origin and
        /// returns how many could not be placed. A Bandit left empty-handed goes
        /// back to its own castle.
        public int ReleaseCaptives(
            Pawn bandit, Vector2Int origin, HashSet<Vector2Int> reserved, List<Pawn> rescued)
        {
            int blocked = 0;

            // Copy: freeing a captive changes bandit.Captives.
            foreach (Pawn royalty in new List<Pawn>(bandit.Captives))
            {
                if (!TryFindFreeTile(origin, reserved, out Vector2Int tile))
                {
                    // Leave the captive intact if there is nowhere legal to place the Royalty.
                    blocked++;
                    continue;
                }

                state.FreeCaptive(bandit, royalty, tile);
                reserved.Add(tile);
                rescued.Add(royalty);
            }

            if (!bandit.IsCarrying)
                state.SendToCastle(bandit);

            return blocked;
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