using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The single search used by every movement-related rule.
    public sealed class Pathfinder
    {
        private readonly BoardGrid grid;

        public Pathfinder(BoardGrid grid) { this.grid = grid; }

        /// Moves from a tile with these movement items applied. Used by the search below and by
        /// MovementRules' fight-through search.
        public MoveGenerator CreateMoves(MovementPerks perks, Func<Vector2Int, bool> canEnter) =>
            new MoveGenerator(grid, perks, canEnter);

        // Where the search is: a tile, plus (for Skipping Shoes only) the direction of the last
        // step and whether the run of steps in that direction has an odd length so far.
        private readonly struct SearchState : IEquatable<SearchState>
        {
            public readonly Vector2Int Position;
            public readonly int Direction;
            public readonly bool Odd;

            public SearchState(Vector2Int position, int direction, bool odd)
            {
                Position = position;
                Direction = direction;
                Odd = odd;
            }

            public bool Equals(SearchState other) =>
                Position == other.Position && Direction == other.Direction && Odd == other.Odd;

            public override bool Equals(object obj) => obj is SearchState other && Equals(other);

            public override int GetHashCode() =>
                (Position.GetHashCode() * 397 ^ Direction) * 397 ^ (Odd ? 1 : 0);
        }

        /// Step cost from start to every tile that can be reached within maxSteps.
        /// canEnter decides which tiles may be entered (it must check bounds itself, because
        /// castles lie outside the map). A tile for which blocksFurther is true can be
        /// reached, but the search does not continue through it.
        /// perks adds the movement items: diagonals, jumps, cluster moves and free steps.
        public Dictionary<Vector2Int, int> GetDistances(
            Vector2Int start, int maxSteps,
            Func<Vector2Int, bool> canEnter, int startCost = 0,
            Func<Vector2Int, bool> blocksFurther = null,
            MovementPerks perks = MovementPerks.None)
        {
            bool skipping = (perks & MovementPerks.SkippingShoes) != 0;
            MoveGenerator generator = CreateMoves(perks, canEnter);

            var distance = new Dictionary<Vector2Int, int> { [start] = startCost };
            var best = new Dictionary<SearchState, int>();

            // One bucket per cost. A free step stays in the bucket that is being worked on.
            int levels = Math.Max(0, maxSteps - startCost) + 1;
            var buckets = new List<Queue<SearchState>>(levels);
            for (int i = 0; i < levels; i++)
                buckets.Add(new Queue<SearchState>());

            var first = new SearchState(start, -1, false);
            best[first] = startCost;
            buckets[0].Enqueue(first);

            var moves = new List<MoveStep>();

            for (int level = 0; level < levels; level++)
            {
                Queue<SearchState> queue = buckets[level];
                int cost = startCost + level;

                while (queue.Count > 0)
                {
                    SearchState state = queue.Dequeue();

                    // Skip entries that were improved after they were queued.
                    if (best[state] != cost)
                        continue;

                    if (blocksFurther != null && state.Position != start && blocksFurther(state.Position))
                        continue;

                    moves.Clear();
                    generator.Collect(state.Position, moves);

                    foreach (MoveStep move in moves)
                    {
                        int stepCost = 1;
                        int direction = -1;
                        bool odd = false;

                        if (skipping && move.Direction >= 0)
                        {
                            direction = move.Direction;

                            if (direction == state.Direction && state.Odd)
                            {
                                stepCost = 0; // the 2nd step of the pair is free
                            }
                            else
                            {
                                odd = true;   // first step of a new pair
                            }
                        }

                        int newCost = cost + stepCost;
                        if (newCost > maxSteps)
                            continue;

                        var next = new SearchState(move.To, direction, odd);
                        if (best.TryGetValue(next, out int existing) && existing <= newCost)
                            continue;

                        best[next] = newCost;
                        buckets[newCost - startCost].Enqueue(next);

                        if (!distance.TryGetValue(move.To, out int known) || newCost < known)
                            distance[move.To] = newCost;
                    }
                }
            }

            return distance;
        }

        /// Nearest tile (any terrain is crossed) that satisfies isMatch.
        public bool TryFindNearest(
            Vector2Int origin, Func<Vector2Int, bool> isMatch, out Vector2Int result)
        {
            var visited = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                if (isMatch(current))
                {
                    result = current;
                    return true;
                }

                foreach (Vector2Int direction in BoardGrid.Directions)
                {
                    Vector2Int next = current + direction;
                    if (grid.InBounds(next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }

            result = origin;
            return false;
        }
    }
}