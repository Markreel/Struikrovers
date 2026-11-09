using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Stab a target 2 or 3 tiles away in a straight line. Every tile between must be
    /// open grass. (Distance 1 is the normal Stab.)
    public sealed class TomahawkAction : IPawnAction
    {
        private const int MaxRange = 3;

        public string Id => "tomahawk";
        public string Label => "Throw";
        public string Group => ActionGroups.Stab;

        public IReadOnlyCollection<Vector2Int> GetTargets(ActionContext ctx)
        {
            var targets = new HashSet<Vector2Int>();

            foreach (Vector2Int dir in BoardGrid.Directions)
            {
                for (int d = 1; d <= MaxRange; d++)
                {
                    Vector2Int p = ctx.Actor.Position + dir * d;
                    if (!ctx.Grid.InBounds(p))
                        break;

                    if (d >= 2 && ctx.State.IsAttackable(p))
                        targets.Add(p);

                    // Only grass lets the line continue.
                    if (ctx.Grid.GetTerrain(p) != TerrainType.Grass)
                        break;
                }
            }

            return targets;
        }

        public ActionOutcome Execute(ActionContext ctx, Vector2Int target)
        {
            if (!GetTargets(ctx).Contains(target))
                return ActionOutcome.NotPerformed;

            var rescued = new List<Pawn>();
            if (!StabResolver.StabTile(ctx, target, rescued))
                ctx.Raise(TurnEvent.StabFoundNothing(ctx.Actor));

            StabResolver.StartRescues(ctx, rescued);
            return ActionOutcome.Performed;
        }
    }
}