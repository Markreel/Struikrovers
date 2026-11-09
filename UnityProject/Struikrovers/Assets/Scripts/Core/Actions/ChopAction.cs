using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Struikrovers.Core
{
    /// Chop an adjacent tree for wood. A hidden enemy Bandit ambushes the chopper instead.
    /// If your own Bandit was hiding in it, that Bandit is left exposed on the stump.
    public sealed class ChopAction : IPawnAction
    {
        public string Id => "chop";
        public string Label => "Chop";
        public string Group => ActionGroups.Gather;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            var targets = new HashSet<Vector2Int>();

            foreach (Vector2Int p in ctx.AdjacentTiles())
                if (ctx.Grid.GetTerrain(p).IsTree())
                    targets.Add(p);

            return targets;
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!ctx.IsAdjacent(target))
                return ActionOutcome.NotPerformed;

            TerrainType terrain = ctx.Grid.GetTerrain(target);
            if (!terrain.IsTree())
                return ActionOutcome.NotPerformed;

            var rescued = new List<Pawn>();

            if (EquipmentEffects.KillsBanditOnChop(ctx.Actor))
            {
                Pawn hidden = ctx.State.GetBanditAt(target);
                if (hidden != null && hidden.Team != ctx.Actor.Team)
                    StabResolver.StabTile(ctx, target, rescued);
            }

            if (ctx.ResolveAmbush(target))   // no Bandit left after a Battleaxe kill, so no ambush
                return ActionOutcome.Performed;

            int wood = terrain.IsPlentiful() ? GameConfig.YieldPlentiful : GameConfig.YieldNormal;
            ctx.State.Teams[ctx.Actor.Team].Wood += wood;
            ctx.Grid.SetTerrain(target, TerrainType.Stump);
            ctx.Raise(TurnEvent.Chopped(ctx.Actor, wood));

            // Only the actor's own Bandit can still be here (an enemy would have ambushed).
            Pawn own = ctx.State.GetBanditAt(target);
            if (own != null && own.Team == ctx.Actor.Team)
            {
                own.IsExposed = true;
                ctx.Raise(TurnEvent.BanditExposed(own));
            }

            StabResolver.StartRescues(ctx, rescued);
            return ActionOutcome.Performed;
        }
    }
}