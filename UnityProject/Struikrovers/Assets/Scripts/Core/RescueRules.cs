using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Frees the Royalty a Bandit carries. Used by the Bloodhound attack, by stabbing
    /// and by lost fights. A freed Royalty starts on the tile it was freed on (the hiding
    /// place) and runs to safety from there (see AwaitRollState).
    public sealed class RescueRules
    {
        private readonly GameState state;
        private readonly MovementRules movement;

        public RescueRules(GameState state, MovementRules movement)
        {
            this.state = state;
            this.movement = movement;
        }

        /// Frees every captive onto 'origin' (the tile of its captor). Returns how many could
        /// not be freed, which is always 0 now. 'reserved' is no longer used and only kept so
        /// existing callers keep working. A Bandit left empty-handed goes back to its castle.
        public int ReleaseCaptives(
            Pawn bandit, Vector2Int origin, HashSet<Vector2Int> reserved, List<Pawn> rescued)
        {
            // Copy: freeing a captive changes bandit.Captives.
            foreach (Pawn royalty in new List<Pawn>(bandit.Captives))
            {
                state.FreeCaptive(bandit, royalty, origin);
                rescued.Add(royalty);
            }

            if (!bandit.IsCarrying)
                state.SendToCastle(bandit);

            return 0;
        }

        /// Fallback for a Royalty that cannot run: moves it to the nearest free grass tile.
        public bool PlaceNearest(Pawn royalty)
        {
            bool found = movement.Pathfinder.TryFindNearest(
                royalty.Position,
                p => state.Grid.GetTerrain(p) == TerrainType.Grass &&
                     !state.HasEnemyPawnAt(p, royalty.Team),
                out Vector2Int tile);

            if (found)
                royalty.Position = tile;

            return found;
        }
    }
}