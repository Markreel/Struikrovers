using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public sealed class MovementRules
    {
        private readonly GameState state;
        private readonly BoardGrid grid;

        public Pathfinder Pathfinder { get; }

        public MovementRules(GameState state)
        {
            this.state = state;
            grid = state.Grid;
            Pathfinder = new Pathfinder(grid);
        }

        public bool CanWalkerEnter(Vector2Int p) =>
            grid.InBounds(p) && grid.GetTerrain(p).IsWalkable();

        public bool CanBanditPass(Vector2Int p) =>
            grid.InBounds(p) && !grid.GetTerrain(p).BlocksBandit();

        public bool CanBanditEnd(Vector2Int p) =>
            grid.InBounds(p) && grid.GetTerrain(p).IsHidingPlace();

        private bool IsOwnCastle(Vector2Int p, int team) =>
            team >= 0 && p == grid.GetCastlePosition(team);

        /// A carrying Bandit may also step onto (and end in) its own castle.
        public bool CanBanditEnd(Vector2Int p, int team, bool carrying) =>
            CanBanditEnd(p) || (carrying && IsOwnCastle(p, team));

        private Func<Vector2Int, bool> BanditPassable(int team, bool carrying) =>
            p => CanBanditPass(p) || (carrying && IsOwnCastle(p, team));

        public Dictionary<Vector2Int, int> GetBanditDistances(
            Vector2Int start, int maxSteps, int team = -1, bool carrying = false) =>
            Pathfinder.GetDistances(start, maxSteps, BanditPassable(team, carrying));

        public HashSet<Vector2Int> GetBanditDestinations(
            Vector2Int start, int maxSteps, int team = -1, bool carrying = false)
        {
            var destinations = new HashSet<Vector2Int>();
            if (maxSteps < 0)
                return destinations;

            foreach (var entry in GetBanditDistances(start, maxSteps, team, carrying))
                if (CanBanditEnd(entry.Key, team, carrying))
                    destinations.Add(entry.Key);

            return destinations;
        }

        /// Every tile this pawn may stop on. A pawn in its castle simply starts from the
        /// castle tile, so leaving costs the normal first step.
        /// Royalty, Bloodhound and Squire may not stop on a tile with an enemy pawn.
        public HashSet<Vector2Int> GetDestinations(Pawn pawn, int movement)
        {
            var result = new HashSet<Vector2Int>();

            if (pawn.Type == PawnType.Bandit)
            {
                var distances = Pathfinder.GetDistances(
                    pawn.Position, movement, BanditPassable(pawn.Team, pawn.IsCarrying));

                foreach (Vector2Int p in distances.Keys)
                    if (CanBanditEnd(p, pawn.Team, pawn.IsCarrying))
                        result.Add(p);

                return result;
            }

            foreach (Vector2Int p in GetWalkerDistances(pawn, movement).Keys)
                if (CanWalkerEnter(p) && !state.HasEnemyPawnAt(p, pawn.Team))
                    result.Add(p);

            return result;
        }

        /// Enemy Squires a Squire can reach and fight on its way, with the steps it takes to
        /// get there. A tile is only offered when the winner could legally stand on it, so
        /// tiles that also hold another enemy pawn (such as a Royalty) are left out.
        public Dictionary<Vector2Int, int> GetSquireFightTargets(Pawn squire, int movement)
        {
            var targets = new Dictionary<Vector2Int, int>();

            if (squire == null || squire.Type != PawnType.Squire)
                return targets;

            foreach (var entry in GetWalkerDistances(squire, movement))
            {
                Vector2Int p = entry.Key;

                if (entry.Value == 0 || !state.HasEnemySquireAt(p, squire.Team))
                    continue;

                bool otherEnemy = state.Exists(x =>
                    !x.AtCastle && x.Team != squire.Team &&
                    x.Position == p && x.Type != PawnType.Squire);

                if (!otherEnemy)
                    targets[p] = entry.Value;
            }

            return targets;
        }

        // Walkers (Royalty, Bloodhound, Squire) only cross grass. An enemy Squire blocks the
        // path of a Squire: its tile can be reached (to fight) but not passed.
        private Dictionary<Vector2Int, int> GetWalkerDistances(Pawn pawn, int movement)
        {
            int team = pawn.Team;

            Func<Vector2Int, bool> blocksFurther = pawn.Type == PawnType.Squire
                ? (Func<Vector2Int, bool>)(p => state.HasEnemySquireAt(p, team))
                : null;

            return Pathfinder.GetDistances(pawn.Position, movement, CanWalkerEnter, 0, blocksFurther);
        }
    }
}