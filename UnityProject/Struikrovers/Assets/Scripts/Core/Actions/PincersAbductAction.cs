using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Prowling Pincers: abduct an enemy Royalty exactly 2 tiles away (Manhattan) as the action.
    /// Guard Squires on the Royalty's tile are fought first, exactly like the adjacent abduction.
    public sealed class PincersAbductAction : IPawnAction
    {
        public const int Range = 2;

        public string Id => "abduct-pincers";
        public string Label => "Abduct";
        public string Group => ActionGroups.Quick;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            if (ctx.Actor.Type != PawnType.Bandit)
                return new Vector2Int[0];

            return ctx.Turn.AbductionRules.GetTargetsAtDistance(ctx.Actor, Range);
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            Pawn bandit = ctx.Actor;
            TurnManager turn = ctx.Turn;
            AbductionRules abduction = turn.AbductionRules;

            if (bandit.Type != PawnType.Bandit || !abduction.CanAbductAtDistance(bandit, target, Range))
                return ActionOutcome.NotPerformed;

            ctx.TakeOverFlow();

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

                if (abduction.TryAbductAtDistance(bandit, target, Range, out Pawn royalty))
                    turn.Raise(TurnEvent.Abducted(royalty, bandit));

                turn.FinishActivation();
            });

            return ActionOutcome.Performed;
        }
    }
}