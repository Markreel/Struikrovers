using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// What the main button does in a state. None means the button is disabled
    /// and the player has to click a highlighted tile.
    public enum PrimaryAction { None, Roll, MoveBandit, Finish }

    public interface ITurnState
    {
        PrimaryAction Primary { get; }
        bool CanCancel { get; }

        /// Tiles the active pawn may move to (yellow).
        IReadOnlyCollection<Vector2Int> Destinations { get; }

        /// Tiles with an enemy Royalty the Bandit can abduct (orange).
        IReadOnlyCollection<Vector2Int> AbductionTargets { get; }

        /// Hiding places the Bloodhound can attack (blue).
        IReadOnlyCollection<Vector2Int> AttackTargets { get; }

        void Enter();
        void OnTileClicked(Vector2Int position);
        void OnPrimaryButton();
        void OnCancel();
    }
}