using UnityEngine;

namespace Struikrovers.Core
{
    public enum LootOutcome { NotPossible, Looted, Ambushed }

    public sealed class ForagingRules
    {
        private readonly GameState state;
        private readonly AbductionRules abduction;

        public ForagingRules(GameState state, AbductionRules abduction)
        {
            this.state = state;
            this.abduction = abduction;
        }

        /// Squire chops an adjacent tree: wood for the team, the tree becomes a stump.
        /// Hook for later: the GDD says a hidden Bandit kills the Squire instead.
        public bool TryChop(Pawn squire, Vector2Int target, out int wood)
        {
            wood = 0;

            if (squire == null || squire.Type != PawnType.Squire)
                return false;

            if (!BoardGrid.IsAdjacent(squire.Position, target))
                return false;

            TerrainType terrain = state.Grid.GetTerrain(target);
            if (!terrain.IsTree())
                return false;

            wood = terrain.IsPlentiful() ? GameConfig.YieldPlentiful : GameConfig.YieldNormal;
            state.Teams[squire.Team].Wood += wood;
            state.Grid.SetTerrain(target, TerrainType.Stump);
            return true;
        }

        /// Royalty loots an adjacent bush. A hidden enemy Bandit abducts the Royalty
        /// instead (ambusher is set and no cards are drawn).
        public LootOutcome TryLoot(Pawn looter, Vector2Int target, out int cards, out Pawn ambusher)
        {
            cards = 0;
            ambusher = null;

            if (looter == null || looter.Type != PawnType.Royalty)
                return LootOutcome.NotPossible;

            if (!BoardGrid.IsAdjacent(looter.Position, target))
                return LootOutcome.NotPossible;

            TerrainType terrain = state.Grid.GetTerrain(target);
            if (!terrain.IsBush())
                return LootOutcome.NotPossible;

            if (abduction.TryAmbush(looter, target, out ambusher))
                return LootOutcome.Ambushed;

            cards = terrain.IsPlentiful() ? GameConfig.YieldPlentiful : GameConfig.YieldNormal;
            state.Teams[looter.Team].Cards += cards;

            // A plentiful bush becomes a normal bush.
            if (terrain == TerrainType.LootBush)
                state.Grid.SetTerrain(target, TerrainType.Bush);

            return LootOutcome.Looted;
        }
    }
}