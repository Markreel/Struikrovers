using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// What the main button does in a state. None means the button is disabled
    /// and the player has to click a highlighted tile.
    public enum PrimaryAction { None, Roll, MoveBandit, Finish, KeepResult, RerollTie }

    public interface ITurnState
    {
        PrimaryAction Primary { get; }
        bool CanCancel { get; }

        /// Tiles the active pawn may move to.
        IReadOnlyCollection<Vector2Int> Destinations { get; }

        /// Tiles with an enemy Royalty the Bandit can abduct by moving over it.
        IReadOnlyCollection<Vector2Int> AbductionTargets { get; }

        /// Hiding places (and exposed Bandits) the Bloodhound can attack.
        IReadOnlyCollection<Vector2Int> AttackTargets { get; }

        /// Action groups the player picks between before clicking a tile. Empty = no choice.
        IReadOnlyList<ActionGroupInfo> AvailableActionGroups { get; }
        string SelectedActionGroup { get; }

        /// Adjacent tiles the selected action group works on.
        IReadOnlyCollection<Vector2Int> ActionTargets { get; }

        /// Adjacent tiles for actions without a button (fight a Squire, abduct a Royalty).
        IReadOnlyCollection<Vector2Int> QuickActionTargets { get; }

        /// Enemy Squires that block a Squire's path and can be fought on the way (without using the action).
        IReadOnlyCollection<Vector2Int> MoveFightTargets { get; }

        /// Tiles a Squire could reach if enemy Squires were not in the way. Clicking one fights its way there.
        IReadOnlyCollection<Vector2Int> BlockedDestinations { get; }

        void Enter();
        void OnTileClicked(Vector2Int position);
        void OnPrimaryButton();
        void OnCancel();
        void OnActionGroupSelected(string group);

        /// A decision the player has to make before the game continues (hand full, equip a card...).
        DecisionPrompt Prompt { get; }
        void OnPromptChosen(int index);
    }
}