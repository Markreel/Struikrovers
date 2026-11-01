using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Abduct an enemy Royalty standing next to the Bandit. If enemy Squires stand on the
    /// same tile, the Bandit has to beat them first. (Abducting by moving over a Royalty
    /// is part of movement, see AbductionPreviewState.)
    public sealed class AbductAction : IPawnAction
    {
        public string Id => "abduct";
        public string Label => "Abduct";
        public string Group => ActionGroups.Quick;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx) =>
            ctx.Turn.AbductionRules.GetAdjacentTargets(ctx.Actor);

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            TurnManager turn = ctx.Turn;
            AbductionRules abduction = turn.AbductionRules;
            Pawn bandit = ctx.Actor;

            if (!abduction.CanAbductAdjacent(bandit, target))
                return ActionOutcome.NotPerformed;

            // A guard fight may wait for the dice, so this action ends the activation itself.
            ctx.TakeOverFlow();

            // The guard fight comes first. Losing ends the activation without an abduction.
            turn.FightGuards(bandit, target, (survived, rescued) =>
            {
                if (!survived)
                {
                    if (rescued.Count > 0)
                        turn.BeginRescueRuns(rescued, false);
                    else
                        turn.FinishActivation();

                    return;
                }

                if (abduction.TryAbductAdjacent(bandit, target, out Pawn royalty))
                    turn.Raise(TurnEvent.Abducted(royalty, bandit));

                turn.FinishActivation();
            });

            return ActionOutcome.Performed;
        }
    }
}