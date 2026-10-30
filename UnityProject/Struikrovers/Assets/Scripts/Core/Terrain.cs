namespace Struikrovers.Core
{
    public enum TerrainType { Grass, Bush, LootBush, Tree, LargeTree, Stump, Tower }

    public static class TerrainTypeExtensions
    {
        /// Royalty, Squire and Bloodhound may only walk on grass.
        public static bool IsWalkable(this TerrainType t) => t == TerrainType.Grass;

        /// Bandits may pass over everything except the tower.
        public static bool BlocksBandit(this TerrainType t) => t == TerrainType.Tower;

        public static bool IsBush(this TerrainType t) =>
            t == TerrainType.Bush || t == TerrainType.LootBush;

        public static bool IsTree(this TerrainType t) =>
            t == TerrainType.Tree || t == TerrainType.LargeTree;

        /// Bandits must end their move here. Bloodhounds may attack these.
        public static bool IsHidingPlace(this TerrainType t) => t.IsBush() || t.IsTree();

        /// Plentiful bush / plentiful tree.
        public static bool IsPlentiful(this TerrainType t) =>
            t == TerrainType.LootBush || t == TerrainType.LargeTree;
    }
}