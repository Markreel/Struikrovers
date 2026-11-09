using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    internal static class StabResolver
    {
        /// Stabs one tile. True when a Bandit was there. Freed captives are added to rescued;
        /// the caller starts the rescue runs, last.
        public static bool StabTile(ActionContext ctx, Vector2Int tile, List<Pawn> rescued)
        {
            Pawn bandit = ctx.State.GetBanditAt(tile);
            if (bandit == null)
                return false;

            bool ownBandit = bandit.Team == ctx.Actor.Team;

            var freed = new List<Pawn>();
            int blocked = ctx.Turn.RescueRules.ReleaseCaptives(
                bandit, tile, new HashSet<Vector2Int>(), freed);

            ctx.State.SendToCastle(bandit);

            if (!ownBandit)
                ctx.Turn.Cards.QueueSteal(ctx.Actor, bandit);

            ctx.Raise(ownBandit
                ? TurnEvent.StabKilledOwnBandit(ctx.Actor)
                : TurnEvent.StabKilled(ctx.Actor, bandit));

            if (blocked > 0)
                ctx.Raise(TurnEvent.AttackBlocked(ctx.Actor, blocked));

            rescued.AddRange(freed);
            return true;
        }

        /// Call after all tiles are done. Always call it last in the action.
        public static void StartRescues(ActionContext ctx, List<Pawn> rescued)
        {
            if (rescued.Count == 0)
                return;

            ctx.Raise(TurnEvent.StabFreedCaptives(ctx.Actor, rescued.Count));
            ctx.BeginRescueRuns(rescued);
        }
    }
}