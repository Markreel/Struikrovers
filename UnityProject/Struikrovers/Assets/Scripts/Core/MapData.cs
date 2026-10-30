using UnityEngine;

namespace Struikrovers.Core
{
    /// Immutable result of loading a map file. Indexed [x, y].
    public sealed class MapData
    {
        private readonly TerrainType[,] tiles;

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }

        public MapData(string name, TerrainType[,] tiles)
        {
            Name = name;
            this.tiles = tiles;
            Width = tiles.GetLength(0);
            Height = tiles.GetLength(1);
        }

        public TerrainType this[int x, int y] => tiles[x, y];
        public TerrainType this[Vector2Int p] => tiles[p.x, p.y];
    }
}