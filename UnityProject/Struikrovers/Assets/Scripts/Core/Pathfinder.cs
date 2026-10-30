using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The single breadth-first search used by every movement-related rule.
    public sealed class Pathfinder
    {
        private readonly BoardGrid grid;

        public Pathfinder(BoardGrid grid) { this.grid = grid; }

        /// Step distance from start to every tile that can be reached within
        /// maxSteps. startCost lets a pawn leaving its castle pay 1 step up front.
        public Dictionary<Vector2Int, int> GetDistances(
            Vector2Int start, int maxSteps,
            Func<Vector2Int, bool> canEnter, int startCost = 0)
        {
            var distance = new Dictionary<Vector2Int, int> { [start] = startCost };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                int currentDistance = distance[current];

                if (currentDistance >= maxSteps)
                    continue;

                foreach (Vector2Int direction in BoardGrid.Directions)
                {
                    Vector2Int next = current + direction;

                    if (distance.ContainsKey(next) || !canEnter(next))
                        continue;

                    distance[next] = currentDistance + 1;
                    queue.Enqueue(next);
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