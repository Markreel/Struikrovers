using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// One move from a tile. Everything costs 1 step, except that Skipping Shoes can make an
    /// ordinary step free (see Pathfinder.GetDistances).
    public readonly struct MoveStep
    {
        public readonly Vector2Int To;
        /// Index in BoardGrid.Directions for an ordinary orthogonal step. -1 for diagonals and jumps.
        public readonly int Direction;

        public MoveStep(Vector2Int to, int direction)
        {
            To = to;
            Direction = direction;
        }
    }

    /// Lists every tile a pawn can move to in one step from a tile, with its movement items applied.
    /// canEnter decides which tiles may be entered (it must check bounds itself).
    /// Create one per search: it caches the bush/tree/stump clusters for the current terrain.
    public sealed class MoveGenerator
    {
        private readonly BoardGrid grid;
        private readonly MovementPerks perks;
        private readonly Func<Vector2Int, bool> canEnter;

        private int[,] clusterOf;
        private List<List<Vector2Int>> clusters;

        public MoveGenerator(BoardGrid grid, MovementPerks perks, Func<Vector2Int, bool> canEnter)
        {
            this.grid = grid;
            this.perks = perks;
            this.canEnter = canEnter;
        }

        private bool Has(MovementPerks perk) => (perks & perk) != 0;

        public void Collect(Vector2Int from, List<MoveStep> moves)
        {
            Vector2Int[] directions = BoardGrid.Directions;

            // Ordinary steps.
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = from + directions[i];
                if (canEnter(next))
                    moves.Add(new MoveStep(next, i));
            }

            // Slanted Slippers: diagonals may cut corners, but a castle is only entered
            // and left through its entry tile.
            if (Has(MovementPerks.SlantedSlippers) && !grid.IsCastle(from))
            {
                foreach (Vector2Int diagonal in BoardGrid.DiagonalDirections)
                {
                    Vector2Int next = from + diagonal;
                    if (!grid.IsCastle(next) && canEnter(next))
                        moves.Add(new MoveStep(next, -1));
                }
            }

            // Stump Stompers: over an adjacent stump onto the tile behind it.
            if (Has(MovementPerks.StumpStompers))
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int over = from + directions[i];
                    if (grid.GetTerrain(over) != TerrainType.Stump)
                        continue;

                    Vector2Int land = over + directions[i];
                    if (canEnter(land))
                        moves.Add(new MoveStep(land, -1));
                }
            }

            // Bush Dashers: from a bush over exactly 1 grass tile or stump onto another bush.
            // Pawns on the tile in between are ignored.
            if (Has(MovementPerks.BushDashers) && grid.GetTerrain(from).IsBush())
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int over = from + directions[i];
                    TerrainType between = grid.GetTerrain(over);
                    if (between != TerrainType.Grass && between != TerrainType.Stump)
                        continue;

                    Vector2Int land = over + directions[i];
                    if (grid.GetTerrain(land).IsBush() && canEnter(land))
                        moves.Add(new MoveStep(land, -1));
                }
            }

            // Tumbling Tunic: from a bush or tree to every other bush or tree of the same
            // cluster (bushes, trees and stumps that touch, diagonals included) for 1 step.
            if (Has(MovementPerks.TumblingTunic) && grid.InBounds(from) &&
                grid.GetTerrain(from).IsHidingPlace())
            {
                EnsureClusters();

                foreach (Vector2Int tile in clusters[clusterOf[from.x, from.y]])
                {
                    if (tile != from && grid.GetTerrain(tile).IsHidingPlace() && canEnter(tile))
                        moves.Add(new MoveStep(tile, -1));
                }
            }
        }

        private static bool IsClusterTerrain(TerrainType t) =>
            t.IsHidingPlace() || t == TerrainType.Stump;

        private void EnsureClusters()
        {
            if (clusters != null)
                return;

            clusters = new List<List<Vector2Int>>();
            clusterOf = new int[grid.Columns, grid.Rows];

            for (int x = 0; x < grid.Columns; x++)
                for (int y = 0; y < grid.Rows; y++)
                    clusterOf[x, y] = -1;

            var queue = new Queue<Vector2Int>();

            for (int x = 0; x < grid.Columns; x++)
            {
                for (int y = 0; y < grid.Rows; y++)
                {
                    var start = new Vector2Int(x, y);
                    if (clusterOf[x, y] != -1 || !IsClusterTerrain(grid.GetTerrain(start)))
                        continue;

                    int id = clusters.Count;
                    var members = new List<Vector2Int>();
                    clusters.Add(members);

                    clusterOf[x, y] = id;
                    queue.Enqueue(start);

                    while (queue.Count > 0)
                    {
                        Vector2Int current = queue.Dequeue();
                        members.Add(current);

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                var next = new Vector2Int(current.x + dx, current.y + dy);
                                if (!grid.InBounds(next) || clusterOf[next.x, next.y] != -1 ||
                                    !IsClusterTerrain(grid.GetTerrain(next)))
                                    continue;

                                clusterOf[next.x, next.y] = id;
                                queue.Enqueue(next);
                            }
                        }
                    }
                }
            }
        }
    }
}