using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Fight the enemy Squires on an adjacent tile. Squires and Bandits both have this action.
    /// Fighting is the action, so it ends the activation. Moving a Squire onto an enemy Squire
    /// starts the same fight without using the action (see ChooseDestinationState).
    public sealed class FightSquireAction : IPawnAction
    {
        public string Id => "fight-squire";
        public string Label => "Fight";
        public string Group => ActionGroups.Quick;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx) =>
            ctx.Turn.CombatRules.GetSquireTargets(ctx.Actor);

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (ctx.Actor.AtCastle || !ctx.IsAdjacent(target))
                return ActionOutcome.NotPerformed;

            TurnManager turn = ctx.Turn;
            Pawn actor = ctx.Actor;

            if (turn.CombatRules.GetSquireOpponents(actor, target).Count == 0)
                return ActionOutcome.NotPerformed;

            // The fights may wait for the dice, so this action ends the activation itself.
            ctx.TakeOverFlow();

            turn.FightSquiresAt(actor, target, results =>
            {
                var rescued = new List<Pawn>();
                int blocked = 0;

                foreach (FightResult fight in results)
                {
                    blocked += fight.Blocked;
                    if (fight.Rescued != null)
                        rescued.AddRange(fight.Rescued);
                }

                if (blocked > 0)
                    turn.Raise(TurnEvent.AttackBlocked(actor, blocked));

                // A Bandit that lost freed its captives. They run to safety, then the turn passes on.
                if (rescued.Count > 0)
                    turn.BeginRescueRuns(rescued, false);
                else
                    turn.FinishActivation();
            });

            return ActionOutcome.Performed;
        }
    }
}