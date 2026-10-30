using UnityEngine;
using Struikrovers.Core;
using Struikrovers.Presentation;

namespace Struikrovers.Game
{
    /// Wires the model, the turn flow and the views together. It replaces
    /// the old StruikroversBoard component.
    public sealed class GameController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private HudView hud;
        [SerializeField] private InputHandler input;

        [Header("Map")]
        [Tooltip("Select a JSON map from Assets/StreamingAssets/Maps using the Inspector dropdown.")]
        [SerializeField] private string mapFileName = "default-map.json";

        [Header("Hidden information")]
        [Tooltip("Hide other teams' Bandits and the captives they carry. Also available as a toggle in the HUD.")]
        [SerializeField] private bool hideEnemyBandits = false;

        private GameState state;
        private TurnManager turn;
        private string clue = "";

        private void OnEnable()
        {
            if (boardView != null) boardView.PositionClicked += OnPositionClicked;
            if (hud != null)
            {
                hud.PrimaryClicked += OnPrimary;
                hud.CancelClicked += OnCancel;
                hud.HideBanditsChanged += OnHideBanditsChanged;
            }
            if (input != null) input.PrimaryPressed += OnPrimary;
        }

        private void OnDisable()
        {
            if (boardView != null) boardView.PositionClicked -= OnPositionClicked;
            if (hud != null)
            {
                hud.PrimaryClicked -= OnPrimary;
                hud.CancelClicked -= OnCancel;
                hud.HideBanditsChanged -= OnHideBanditsChanged;
            }
            if (input != null) input.PrimaryPressed -= OnPrimary;
        }

        private void Start() => Reload();

        private void OnDestroy() => DetachTurn();

        [ContextMenu("Reload Board")]
        public void Reload()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Reload Board only works in Play Mode.");
                return;
            }

            if (boardView == null || hud == null)
            {
                Debug.LogError("GameController: assign Board View and HUD in the Inspector.");
                return;
            }

            if (!MapLoader.TryLoadFromStreamingAssets(mapFileName, out MapData map, out string error))
            {
                Debug.LogError(error);
                return;
            }

            DetachTurn();

            var grid = new BoardGrid(map);
            state = new GameState(grid, GameConfig.TeamCount);
            clue = "";

            boardView.Build(grid);
            hud.SetHideBandits(hideEnemyBandits);

            turn = new TurnManager(state);
            turn.Changed += Refresh;
            turn.EventRaised += OnTurnEvent;
            turn.Start(); // Raises Changed once, which draws the first frame.

            Debug.Log("Loaded board map: " + map.Name);
        }

        private void DetachTurn()
        {
            if (turn == null)
                return;

            turn.Changed -= Refresh;
            turn.EventRaised -= OnTurnEvent;
            turn = null;
        }

        // ---- Input from the views ----

        private void OnPositionClicked(Vector2Int position) => turn?.OnTileClicked(position);
        private void OnPrimary() => turn?.OnPrimaryButton();
        private void OnCancel() => turn?.OnCancel();

        private void OnHideBanditsChanged(bool value)
        {
            hideEnemyBandits = value;
            Refresh();
        }

        // ---- Output to the views ----

        private void OnTurnEvent(TurnEvent e)
        {
            TurnMessage message = TurnMessages.Describe(e, state);

            if (message.Clue != null)
                clue = message.Clue;

            if (message.Log == null)
                return;

            if (message.IsWarning) Debug.LogWarning(message.Log);
            else Debug.Log(message.Log);
        }

        private void Refresh()
        {
            ITurnState step = turn?.CurrentState;
            if (step == null)
                return;

            boardView.Render(state, step, turn.CurrentTeam, hideEnemyBandits);

            hud.Show(
                TurnMessages.BuildStatus(turn, clue),
                boardView.Theme.GetStatusBackground(turn.CurrentTeam),
                TurnMessages.GetPrimaryLabel(step.Primary),
                step.Primary != PrimaryAction.None,
                step.CanCancel);
        }
    }
}