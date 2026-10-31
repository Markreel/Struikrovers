using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Fight the enemy Squires on an adjacent tile. Squires and Bandits both have this action.
    /// Moving a Squire onto an enemy Squire starts the same fight (see TurnManager.CompleteMovement).
    public sealed class FightSquireAction : IPawnAction
    {
        public string Id => "fight-squire";
        public string Label => "Fight";
        public string Group => ActionGroups.Quick;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx) =>
            ctx.Turn.CombatRules.GetSquireTargets(ctx.Actor);

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!ctx.IsAdjacent(target))
                return ActionOutcome.NotPerformed;

            List<FightResult> fights = ctx.Turn.CombatRules.FightSquiresAt(ctx.Actor, target);
            if (fights.Count == 0)
                return ActionOutcome.NotPerformed;

            var rescued = new List<Pawn>();
            int blocked = 0;

            foreach (FightResult fight in fights)
            {
                ctx.Raise(TurnEvent.SquireFight(fight));
                blocked += fight.Blocked;

                if (fight.Rescued != null)
                    rescued.AddRange(fight.Rescued);
            }

            if (blocked > 0)
                ctx.Raise(TurnEvent.AttackBlocked(ctx.Actor, blocked));

            // A Bandit that lost freed its captives. They run to safety, then the turn passes on.
            if (rescued.Count > 0)
                ctx.BeginRescueRuns(rescued);

            return ActionOutcome.Performed;
        }
    }
}