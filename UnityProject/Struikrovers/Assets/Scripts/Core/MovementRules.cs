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

        /// Tiles a Squire could reach within 'movement' steps if it fought its way through the enemy
        /// Squires in the way, with the cheapest route to each (fewest fights, then fewest steps).
        /// Tiles that are reachable without a fight are not included.
        public Dictionary<Vector2Int, BlockedPath> GetBlockedSquireDestinations(Pawn squire, int movement)
        {
            var result = new Dictionary<Vector2Int, BlockedPath>();

            if (squire == null || squire.Type != PawnType.Squire || movement <= 0)
                return result;

            int team = squire.Team;

            // Layer s holds the fewest fights needed to stand on a tile after exactly s steps.
            var fightsByLayer = new List<Dictionary<Vector2Int, int>>
    {
        new Dictionary<Vector2Int, int> { [squire.Position] = 0 }
    };
            var parentsByLayer = new List<Dictionary<Vector2Int, Vector2Int>>
    {
        new Dictionary<Vector2Int, Vector2Int>()
    };

            var bestFights = new Dictionary<Vector2Int, int>();
            var bestLayer = new Dictionary<Vector2Int, int>();

            for (int s = 1; s <= movement; s++)
            {
                var layer = new Dictionary<Vector2Int, int>();
                var parents = new Dictionary<Vector2Int, Vector2Int>();

                foreach (var entry in fightsByLayer[s - 1])
                {
                    foreach (Vector2Int direction in BoardGrid.Directions)
                    {
                        Vector2Int next = entry.Key + direction;
                        if (!CanWalkerEnter(next))
                            continue;

                        int fights = entry.Value + (state.HasEnemySquireAt(next, team) ? 1 : 0);

                        if (layer.TryGetValue(next, out int existing) && existing <= fights)
                            continue;

                        layer[next] = fights;
                        parents[next] = entry.Key;
                    }
                }

                fightsByLayer.Add(layer);
                parentsByLayer.Add(parents);

                foreach (var entry in layer)
                {
                    if (!bestFights.TryGetValue(entry.Key, out int best) || entry.Value < best)
                    {
                        bestFights[entry.Key] = entry.Value;
                        bestLayer[entry.Key] = s;
                    }
                }
            }

            foreach (var entry in bestFights)
            {
                Vector2Int tile = entry.Key;

                // No fight needed means it is a normal destination.
                if (entry.Value == 0 || tile == squire.Position)
                    continue;

                // The winner has to be able to stand there: no other enemy pawn (such as a Royalty).
                bool otherEnemy = state.Exists(p =>
                    !p.AtCastle && p.Team != team && p.Position == tile && p.Type != PawnType.Squire);
                if (otherEnemy)
                    continue;

                // Walk the route back from the destination to the start.
                int steps = bestLayer[tile];
                var route = new List<Vector2Int>();
                Vector2Int current = tile;

                for (int s = steps; s > 0; s--)
                {
                    route.Add(current);
                    current = parentsByLayer[s][current];
                }

                route.Reverse();

                var blockers = new List<Vector2Int>();
                foreach (Vector2Int p in route)
                    if (state.HasEnemySquireAt(p, team) && !blockers.Contains(p))
                        blockers.Add(p);

                result[tile] = new BlockedPath(tile, steps, blockers);
            }

            return result;
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