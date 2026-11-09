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

        /// Thorny Trunk: a stump is a legal ending tile too.
        public bool CanBanditEnd(Vector2Int p, int team, bool carrying, MovementPerks perks) =>
            CanBanditEnd(p, team, carrying) ||
            ((perks & MovementPerks.ThornyTrunk) != 0 &&
             grid.InBounds(p) && grid.GetTerrain(p) == TerrainType.Stump);

        private bool IsOwnCastle(Vector2Int p, int team) =>
            team >= 0 && p == grid.GetCastlePosition(team);

        /// A carrying Bandit may also step onto (and end in) its own castle.
        public bool CanBanditEnd(Vector2Int p, int team, bool carrying) =>
            CanBanditEnd(p) || (carrying && IsOwnCastle(p, team));

        private Func<Vector2Int, bool> BanditPassable(int team, bool carrying) =>
            p => CanBanditPass(p) || (carrying && IsOwnCastle(p, team));

        public Dictionary<Vector2Int, int> GetBanditDistances(
            Vector2Int start, int maxSteps, int team = -1, bool carrying = false,
            MovementPerks perks = MovementPerks.None) =>
            Pathfinder.GetDistances(start, maxSteps, BanditPassable(team, carrying), 0, null, perks);

        public HashSet<Vector2Int> GetBanditDestinations(
            Vector2Int start, int maxSteps, int team = -1, bool carrying = false,
            MovementPerks perks = MovementPerks.None)
        {
            var destinations = new HashSet<Vector2Int>();
            if (maxSteps < 0)
                return destinations;

            foreach (var entry in GetBanditDistances(start, maxSteps, team, carrying, perks))
                if (CanBanditEnd(entry.Key, team, carrying, perks))
                    destinations.Add(entry.Key);

            return destinations;
        }

        /// Every tile this pawn may stop on. A pawn in its castle simply starts from the
        /// castle tile, so leaving costs the normal first step.
        /// Royalty, Bloodhound and Squire may not stop on a tile with an enemy pawn.
        /// Movement items (diagonals, jumps, free steps...) are taken from the pawn's equipment.
        public HashSet<Vector2Int> GetDestinations(Pawn pawn, int movement)
        {
            var result = new HashSet<Vector2Int>();

            if (pawn.Type == PawnType.Bandit)
            {
                MovementPerks perks = EquipmentEffects.GetMovementPerks(pawn);

                var distances = Pathfinder.GetDistances(
                    pawn.Position, movement, BanditPassable(pawn.Team, pawn.IsCarrying), 0, null, perks);

                foreach (Vector2Int p in distances.Keys)
                    if (CanBanditEnd(p, pawn.Team, pawn.IsCarrying, perks))
                        result.Add(p);

                return result;
            }

            Func<Vector2Int, bool> canEnter = GetWalkerEnter(pawn);

            foreach (Vector2Int p in GetWalkerDistances(pawn, movement).Keys)
                if (canEnter(p) && !state.HasEnemyPawnAt(p, pawn.Team))
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

            // The Squire's own movement items (diagonals, jumps, Bolting Boots shots) apply here too.
            MovementPerks perks = EquipmentEffects.GetMovementPerks(squire);
            bool bolting = (perks & MovementPerks.BoltingBoots) != 0;
            MoveGenerator moves = Pathfinder.CreateMoves(perks, CanWalkerEnter, GetBoltOpen(squire));
            var stepBuffer = new List<MoveStep>();

            // Layer s holds the fewest fights needed to stand on a node after exactly s steps.
            var start = new PathNode(squire.Position, -1);
            var fightsByLayer = new List<Dictionary<PathNode, int>>
    {
        new Dictionary<PathNode, int> { [start] = 0 }
    };
            var parentsByLayer = new List<Dictionary<PathNode, PathNode>>
    {
        new Dictionary<PathNode, PathNode>()
    };

            var bestFights = new Dictionary<Vector2Int, int>();
            var bestLayer = new Dictionary<Vector2Int, int>();
            var bestNode = new Dictionary<Vector2Int, PathNode>();

            for (int s = 1; s <= movement; s++)
            {
                var layer = new Dictionary<PathNode, int>();
                var parents = new Dictionary<PathNode, PathNode>();

                foreach (var entry in fightsByLayer[s - 1])
                {
                    stepBuffer.Clear();
                    moves.Collect(entry.Key.Position, stepBuffer, entry.Key.Direction);

                    foreach (MoveStep move in stepBuffer)
                    {
                        Vector2Int next = move.To;
                        int fights = entry.Value + (state.HasEnemySquireAt(next, team) ? 1 : 0);

                        var node = new PathNode(next, bolting ? move.Direction : -1);

                        if (layer.TryGetValue(node, out int existing) && existing <= fights)
                            continue;

                        layer[node] = fights;
                        parents[node] = entry.Key;
                    }
                }

                fightsByLayer.Add(layer);
                parentsByLayer.Add(parents);

                foreach (var entry in layer)
                {
                    Vector2Int tile = entry.Key.Position;

                    if (!bestFights.TryGetValue(tile, out int best) || entry.Value < best)
                    {
                        bestFights[tile] = entry.Value;
                        bestLayer[tile] = s;
                        bestNode[tile] = entry.Key;
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
                PathNode current = bestNode[tile];

                for (int s = steps; s > 0; s--)
                {
                    route.Add(current.Position);
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

        // Walkers (Royalty, Bloodhound, Squire) only cross grass, unless their items say otherwise.
        // An enemy Squire blocks the path of a Squire: its tile can be reached (to fight) but not passed.
        private Dictionary<Vector2Int, int> GetWalkerDistances(Pawn pawn, int movement)
        {
            int team = pawn.Team;

            Func<Vector2Int, bool> blocksFurther = pawn.Type == PawnType.Squire
                ? (Func<Vector2Int, bool>)(p => state.HasEnemySquireAt(p, team))
                : null;

            return Pathfinder.GetDistances(
                pawn.Position, movement, GetWalkerEnter(pawn), 0, blocksFurther,
                EquipmentEffects.GetMovementPerks(pawn), GetBoltOpen(pawn));
        }

        /// Tiles a Bolting Boots shot may cross and land on: open grass without an enemy pawn.
        public Func<Vector2Int, bool> GetBoltOpen(Pawn pawn)
        {
            int team = pawn.Team;
            return p => CanWalkerEnter(p) && !state.HasEnemyPawnAt(p, team);
        }

        // A tile plus the direction of the last ordinary step (-1 when Bolting Boots aren't worn).
        private readonly struct PathNode : IEquatable<PathNode>
        {
            public readonly Vector2Int Position;
            public readonly int Direction;

            public PathNode(Vector2Int position, int direction)
            {
                Position = position;
                Direction = direction;
            }

            public bool Equals(PathNode other) => Position == other.Position && Direction == other.Direction;
            public override bool Equals(object obj) => obj is PathNode other && Equals(other);
            public override int GetHashCode() => Position.GetHashCode() * 397 ^ Direction;
        }

        // Keys to the Castle: besides grass, the wearer may also enter (and stay in) its own castle.
        private Func<Vector2Int, bool> GetWalkerEnter(Pawn pawn)
        {
            if (pawn.Type != PawnType.Royalty || !EquipmentEffects.AllowsCastleStay(pawn))
                return CanWalkerEnter;

            Vector2Int castle = grid.GetCastlePosition(pawn.Team);
            return p => CanWalkerEnter(p) || p == castle;
        }
    }
}