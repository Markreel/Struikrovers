using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// A tile a Squire could reach if enemy Squires did not block the way.
    public readonly struct BlockedPath
    {
        public readonly Vector2Int Destination;
        /// Steps of the route (what the move would cost).
        public readonly int Steps;
        /// Tiles with enemy Squires on the route, nearest first.
        public readonly IReadOnlyList<Vector2Int> Blockers;

        public BlockedPath(Vector2Int destination, int steps, IReadOnlyList<Vector2Int> blockers)
        {
            Destination = destination;
            Steps = steps;
            Blockers = blockers;
        }
    }
}