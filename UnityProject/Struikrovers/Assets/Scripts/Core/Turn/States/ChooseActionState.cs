using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// The single action of a Royalty, Squire or Bandit. It can always be skipped.
    /// What the pawn can do comes from its action list. When its actions fall into two or more
    /// groups, the player picks a group first (one HUD button each). Actions without a
    /// group (fight a Squire, abduct a Royalty) are always active.
    public sealed class ChooseActionState : TurnState
    {
        private static readonly ActionGroupInfo[] NoGroups = new ActionGroupInfo[0];

        private readonly List<ActionGroupInfo> groups = new List<ActionGroupInfo>();
        private string selectedGroup;
        private HashSet<Vector2Int> actionTargets = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> quickTargets = new HashSet<Vector2Int>();

        public ChooseActionState(TurnManager turn) : base(turn) { }

        public override PrimaryAction Primary => PrimaryAction.Finish;

        /// Only offered when there is a real choice.
        public override IReadOnlyList<ActionGroupInfo> AvailableActionGroups =>
            groups.Count >= 2 ? (IReadOnlyList<ActionGroupInfo>)groups : NoGroups;

        public override string SelectedActionGroup => selectedGroup ?? ActionGroups.Quick;
        public override IReadOnlyCollection<Vector2Int> ActionTargets => actionTargets;
        public override IReadOnlyCollection<Vector2Int> QuickActionTargets => quickTargets;

        public override void Enter()
        {
            BuildGroups();
            selectedGroup = groups.Count > 0 ? groups[0].Id : null;
            RefreshTargets();
        }

        public override void OnPrimaryButton() => Turn.FinishActivation();

        public override void OnActionGroupSelected(string group)
        {
            if (group == selectedGroup || !groups.Exists(g => g.Id == group))
                return;

            selectedGroup = group;
            RefreshTargets();
        }

        public override void OnTileClicked(Vector2Int position)
        {
            var context = new ActionContext(Turn, Pawn);

            if (selectedGroup != null && TryExecute(context, selectedGroup, position))
                return;

            TryExecute(context, ActionGroups.Quick, position);
        }

        // Runs the first action of the group that accepts the tile.
        private bool TryExecute(ActionContext context, string group, Vector2Int position)
        {
            foreach (IPawnAction action in Pawn.Actions)
            {
                if (action.Group != group)
                    continue;

                ActionOutcome outcome = action.Execute(context, position);
                if (outcome == ActionOutcome.NotPerformed)
                    continue;

                if (outcome == ActionOutcome.Performed && !context.FlowHandled)
                    Turn.FinishActivation();

                return true;
            }

            return false;
        }

        private void BuildGroups()
        {
            groups.Clear();

            var order = new List<string>();
            var labels = new Dictionary<string, List<string>>();

            foreach (IPawnAction action in Pawn.Actions)
            {
                if (ActionGroups.IsQuick(action.Group))
                    continue;

                if (!labels.TryGetValue(action.Group, out List<string> list))
                {
                    list = new List<string>();
                    labels[action.Group] = list;
                    order.Add(action.Group);
                }

                if (!list.Contains(action.Label))
                    list.Add(action.Label);
            }

            foreach (string id in order)
                groups.Add(new ActionGroupInfo(id, string.Join(" / ", labels[id])));
        }

        private void RefreshTargets()
        {
            actionTargets = new HashSet<Vector2Int>();
            quickTargets = new HashSet<Vector2Int>();

            var context = new ActionContext(Turn, Pawn);

            foreach (IPawnAction action in Pawn.Actions)
            {
                bool quick = ActionGroups.IsQuick(action.Group);
                if (!quick && action.Group != selectedGroup)
                    continue;

                HashSet<Vector2Int> set = quick ? quickTargets : actionTargets;
                foreach (Vector2Int tile in action.GetTargets(context))
                    set.Add(tile);
            }
        }
    }
}