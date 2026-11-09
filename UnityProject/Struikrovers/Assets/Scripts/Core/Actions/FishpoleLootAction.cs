using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Flaying Fishpole: loot a bush at Manhattan distance 2, through terrain and pawns.
    /// Same rules as Loot: a hidden Bandit still ambushes, Royalty get the full plentiful yield,
    /// Squires don't. A Bandit can wear it but never loots with it.
    public sealed class FishpoleLootAction : IPawnAction
    {
        private static readonly Vector2Int[] Offsets =
        {
            new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2), new Vector2Int(0, -2),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        public string Id => "loot-fishpole";
        public string Label => "Loot";
        public string Group => ActionGroups.Gather;

        // Not a Bandit, not a Bloodhound, not inside the (off-board) castle.
        private static bool CanUse(Pawn actor) =>
            actor.Type != PawnType.Bandit && actor.Type == actor.BaseType && !actor.AtCastle;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            var targets = new List<Vector2Int>();
            if (!CanUse(ctx.Actor))
                return targets;

            foreach (Vector2Int offset in Offsets)
            {
                Vector2Int p = ctx.Actor.Position + offset;
                if (ctx.Grid.InBounds(p) && ctx.Grid.GetTerrain(p).IsBush())
                    targets.Add(p);
            }

            return targets;
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!CanUse(ctx.Actor) || BoardGrid.Manhattan(ctx.Actor.Position, target) != 2)
                return ActionOutcome.NotPerformed;

            if (!ctx.Grid.InBounds(target))
                return ActionOutcome.NotPerformed;

            TerrainType terrain = ctx.Grid.GetTerrain(target);
            if (!terrain.IsBush())
                return ActionOutcome.NotPerformed;

            if (ctx.ResolveAmbush(target))
                return ActionOutcome.Performed;

            bool takesFullYield = terrain.IsPlentiful() && ctx.Actor.BaseType == PawnType.Royalty;
            int cards = takesFullYield ? GameConfig.YieldPlentiful : GameConfig.YieldNormal;
            cards = EquipmentEffects.ModifyLootCount(ctx.Actor, cards);

            if (takesFullYield)
                ctx.Grid.SetTerrain(target, TerrainType.Bush);

            ctx.Raise(TurnEvent.Looted(ctx.Actor, cards));
            ctx.Turn.Cards.QueueLoot(ctx.Actor, cards);
            return ActionOutcome.Performed;
        }
    }
}