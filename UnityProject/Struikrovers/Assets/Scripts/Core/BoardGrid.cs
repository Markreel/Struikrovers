using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Mutable terrain of the playable area. The visual frame is not part of this.
    public sealed class BoardGrid
    {
        public static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        private readonly TerrainType[,] terrain;

        public int Columns { get; }
        public int Rows { get; }

        /// Raised after SetTerrain, e.g. tree -> stump. Views listen to this later.
        public event Action<Vector2Int> TerrainChanged;

        public BoardGrid(MapData map)
        {
            Columns = map.Width;
            Rows = map.Height;
            terrain = new TerrainType[Columns, Rows];

            for (int x = 0; x < Columns; x++)
                for (int y = 0; y < Rows; y++)
                    terrain[x, y] = map[x, y];
        }

        public bool InBounds(Vector2Int p) =>
            p.x >= 0 && p.x < Columns && p.y >= 0 && p.y < Rows;

        /// Out-of-bounds positions are treated as an impassable tower.
        public TerrainType GetTerrain(Vector2Int p) =>
            InBounds(p) ? terrain[p.x, p.y] : TerrainType.Tower;

        public void SetTerrain(Vector2Int p, TerrainType value)
        {
            if (!InBounds(p) || terrain[p.x, p.y] == value)
                return;

            terrain[p.x, p.y] = value;
            TerrainChanged?.Invoke(p);
        }

        public static int Manhattan(Vector2Int a, Vector2Int b) =>
            Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        public static bool IsAdjacent(Vector2Int a, Vector2Int b) => Manhattan(a, b) == 1;

        /// The board-edge tile where a team's castle entry sits.
        /// Team 0 = top, 1 = right, 2 = bottom, 3 = left.
        /// This is the only place that knows the castle layout, so it is also
        /// the place to change when the player count becomes dynamic.
        public Vector2Int GetCastleEntry(int team)
        {
            switch (team)
            {
                case 0: return new Vector2Int(Columns / 2, 0);
                case 1: return new Vector2Int(Columns - 1, Rows / 2);
                case 2: return new Vector2Int(Columns / 2, Rows - 1);
                default: return new Vector2Int(0, Rows / 2);
            }
        }

        /// The castle itself: one step outside the map, behind the entry tile.
        /// Team 0 = top, 1 = right, 2 = bottom, 3 = left.
        public Vector2Int GetCastlePosition(int team)
        {
            Vector2Int e = GetCastleEntry(team);
            switch (team)
            {
                case 0: return new Vector2Int(e.x, e.y - 1);
                case 1: return new Vector2Int(e.x + 1, e.y);
                case 2: return new Vector2Int(e.x, e.y + 1);
                default: return new Vector2Int(e.x - 1, e.y);
            }
        }

        public bool IsCastle(Vector2Int p)
        {
            for (int team = 0; team < GameConfig.TeamCount; team++)
                if (GetCastlePosition(team) == p) return true;
            return false;
        }
    }
}