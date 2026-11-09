using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Stab an adjacent bush, tree, or a stump with an exposed Bandit on it. The Bandit
    /// there dies and returns to its castle, freeing the Royalty it carried.
    /// Stabbing your own Bandit kills it too, as a punishment for forgetting where it hides.
    public sealed class StabAction : IPawnAction
    {
        public string Id => "stab";
        public string Label => "Stab";
        public string Group => ActionGroups.Stab;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            var targets = new HashSet<Vector2Int>();

            foreach (Vector2Int p in ctx.AdjacentTiles())
                if (ctx.State.IsAttackable(p))
                    targets.Add(p);

            return targets;
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!ctx.IsAdjacent(target) || !ctx.State.IsAttackable(target))
                return ActionOutcome.NotPerformed;

            var tiles = new List<Vector2Int>();
            if (EquipmentEffects.StabsAllAdjacent(ctx.Actor))
                tiles.AddRange(GetTargets(ctx));
            else
                tiles.Add(target);

            var rescued = new List<Pawn>();
            bool found = false;
            foreach (Vector2Int tile in tiles)
                found |= StabResolver.StabTile(ctx, tile, rescued);

            if (!found)
                ctx.Raise(TurnEvent.StabFoundNothing(ctx.Actor));

            StabResolver.StartRescues(ctx, rescued);
            return ActionOutcome.Performed;
        }
    }
}