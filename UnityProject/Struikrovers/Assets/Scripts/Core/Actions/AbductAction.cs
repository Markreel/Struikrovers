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
            AbductionRules abduction = ctx.Turn.AbductionRules;

            if (!abduction.CanAbductAdjacent(ctx.Actor, target))
                return ActionOutcome.NotPerformed;

            // The guard fight comes first. Losing ends the activation without an abduction.
            if (!ctx.Turn.FightGuards(ctx.Actor, target, out List<Pawn> rescued))
            {
                if (rescued.Count > 0)
                    ctx.BeginRescueRuns(rescued);

                return ActionOutcome.Performed;
            }

            if (abduction.TryAbductAdjacent(ctx.Actor, target, out Pawn royalty))
                ctx.Raise(TurnEvent.Abducted(royalty, ctx.Actor));

            return ActionOutcome.Performed;
        }
    }
}