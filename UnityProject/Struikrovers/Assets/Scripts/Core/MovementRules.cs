using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public sealed class MovementRules
    {
        private readonly BoardGrid grid;

        public Pathfinder Pathfinder { get; }

        public MovementRules(BoardGrid grid)
        {
            this.grid = grid;
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

        /// Every tile this pawn may stop on. A pawn in its castle simply starts
        /// from the castle tile, so leaving costs the normal first step.
        public HashSet<Vector2Int> GetDestinations(Pawn pawn, int movement)
        {
            var result = new HashSet<Vector2Int>();
            bool isBandit = pawn.Type == PawnType.Bandit;

            Func<Vector2Int, bool> canEnter = isBandit
                ? BanditPassable(pawn.Team, pawn.IsCarrying)
                : (Func<Vector2Int, bool>)CanWalkerEnter;

            var distances = Pathfinder.GetDistances(pawn.Position, movement, canEnter);

            foreach (Vector2Int p in distances.Keys)
            {
                bool canStop = isBandit
                    ? CanBanditEnd(p, pawn.Team, pawn.IsCarrying)
                    : CanWalkerEnter(p);

                if (canStop)
                    result.Add(p);
            }

            return result;
        }
    }
}