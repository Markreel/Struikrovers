using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    public enum ActionOutcome
    {
        /// Not valid for this tile. The click is ignored.
        NotPerformed,
        /// Done. The activation ends (unless the action already moved the turn flow on).
        Performed,
        /// Something happened (like a refusal message) but the pawn keeps its action.
        StayInActionPhase
    }

    /// Something a pawn can do in its action phase. Equipment and cards add and remove these.
    public interface IPawnAction
    {
        string Id { get; }

        /// Short name, also used on the HUD button of its group.
        string Label { get; }

        /// One HUD button per group. Actions in one group never share a target tile.
        /// ActionGroups.Quick means: no button, always active.
        string Group { get; }

        /// Tiles this action would work on right now (used for highlighting).
        IReadOnlyCollection<Vector2Int> GetTargets(ActionContext context);

        /// Must validate everything itself and return NotPerformed for an invalid tile.
        ActionOutcome Execute(ActionContext context, Vector2Int target);
    }
}