using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Base class with do-nothing defaults, so each state only overrides what it uses.
    public abstract class TurnState : ITurnState
    {
        private static readonly Vector2Int[] NoTiles = new Vector2Int[0];
        private static readonly ActionGroupInfo[] NoGroups = new ActionGroupInfo[0];

        protected readonly TurnManager Turn;

        protected TurnState(TurnManager turn) { Turn = turn; }

        protected Pawn Pawn => Turn.ActivePawn;

        public virtual PrimaryAction Primary => PrimaryAction.None;
        public virtual bool CanCancel => false;
        public virtual IReadOnlyCollection<Vector2Int> Destinations => NoTiles;
        public virtual IReadOnlyCollection<Vector2Int> AbductionTargets => NoTiles;
        public virtual IReadOnlyCollection<Vector2Int> AttackTargets => NoTiles;
        public virtual IReadOnlyList<ActionGroupInfo> AvailableActionGroups => NoGroups;
        public virtual string SelectedActionGroup => ActionGroups.Quick;
        public virtual IReadOnlyCollection<Vector2Int> ActionTargets => NoTiles;
        public virtual IReadOnlyCollection<Vector2Int> QuickActionTargets => NoTiles;

        public virtual void Enter() { }
        public virtual void OnTileClicked(Vector2Int position) { }
        public virtual void OnPrimaryButton() { }
        public virtual void OnCancel() { }
        public virtual void OnActionGroupSelected(string group) { }
    }
}