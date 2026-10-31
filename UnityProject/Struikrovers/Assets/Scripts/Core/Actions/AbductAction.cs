using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Abduct an enemy Royalty standing next to the Bandit. (Abducting by moving over a
    /// Royalty is part of movement, see AbductionPreviewState.)
    public sealed class AbductAction : IPawnAction
    {
        public string Id => "abduct";
        public string Label => "Abduct";
        public string Group => ActionGroups.Quick;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx) =>
            ctx.Turn.AbductionRules.GetAdjacentTargets(ctx.Actor);

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!ctx.Turn.AbductionRules.TryAbductAdjacent(ctx.Actor, target, out Pawn royalty))
                return ActionOutcome.NotPerformed;

            ctx.Raise(TurnEvent.Abducted(royalty, ctx.Actor));
            return ActionOutcome.Performed;
        }
    }
}