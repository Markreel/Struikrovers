using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Loot an adjacent bush for cards. A hidden enemy Bandit ambushes the looter instead
    /// (a Royalty is abducted, a Squire is killed).
    /// With fullPlentifulYield (Royalty) a plentiful bush gives 2 cards and becomes a normal
    /// bush. Without it (Squire) a plentiful bush gives 1 card and stays plentiful.
    public sealed class LootAction : IPawnAction
    {
        private readonly bool fullPlentifulYield;

        public LootAction(bool fullPlentifulYield) { this.fullPlentifulYield = fullPlentifulYield; }

        public string Id => "loot";
        public string Label => "Loot";
        public string Group => ActionGroups.Gather;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            var targets = new HashSet<Vector2Int>();

            foreach (Vector2Int p in ctx.AdjacentTiles())
                if (ctx.Grid.GetTerrain(p).IsBush())
                    targets.Add(p);

            return targets;
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!ctx.IsAdjacent(target))
                return ActionOutcome.NotPerformed;

            TerrainType terrain = ctx.Grid.GetTerrain(target);
            if (!terrain.IsBush())
                return ActionOutcome.NotPerformed;

            if (ctx.ResolveAmbush(target))
                return ActionOutcome.Performed;

            bool takesFullYield = terrain.IsPlentiful() && fullPlentifulYield;

            int cards = takesFullYield ? GameConfig.YieldPlentiful : GameConfig.YieldNormal;
            ctx.State.Teams[ctx.Actor.Team].Cards += cards;

            // Only a full-yield loot uses up the plentiful bush.
            if (takesFullYield)
                ctx.Grid.SetTerrain(target, TerrainType.Bush);

            ctx.Raise(TurnEvent.Looted(ctx.Actor, cards));
            return ActionOutcome.Performed;
        }
    }
}