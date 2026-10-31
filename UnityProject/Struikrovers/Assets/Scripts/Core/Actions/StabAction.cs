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

            Pawn bandit = ctx.State.GetBanditAt(target);

            if (bandit == null)
            {
                ctx.Raise(TurnEvent.StabFoundNothing(ctx.Actor));
                return ActionOutcome.Performed;
            }

            bool ownBandit = bandit.Team == ctx.Actor.Team;

            var rescued = new List<Pawn>();
            int blocked = ctx.Turn.RescueRules.ReleaseCaptives(
                bandit, target, new HashSet<Vector2Int>(), rescued);

            ctx.State.SendToCastle(bandit);

            // Hook for later: the Squire takes 1 piece of an enemy Bandit's equipment (no wood).
            // Your own Bandit dies with no reward for anyone.
            ctx.Raise(ownBandit
                ? TurnEvent.StabKilledOwnBandit(ctx.Actor)
                : TurnEvent.StabKilled(ctx.Actor, bandit));

            if (blocked > 0)
                ctx.Raise(TurnEvent.AttackBlocked(ctx.Actor, blocked));

            if (rescued.Count > 0)
            {
                ctx.Raise(TurnEvent.StabFreedCaptives(ctx.Actor, rescued.Count));
                ctx.BeginRescueRuns(rescued);
            }

            return ActionOutcome.Performed;
        }
    }
}