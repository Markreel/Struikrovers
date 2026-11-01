using UnityEngine;
using Struikrovers.Core;
using Struikrovers.Presentation;

namespace Struikrovers.Game
{
    /// Wires the model, the turn flow and the views together.
    public sealed class GameController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private HudView hud;
        [SerializeField] private PromptView promptView;
        [SerializeField] private InputHandler input;

        [Header("Cards and players (optional)")]
        [Tooltip("One panel per team, in team order (Player 1 first).")]
        [SerializeField] private PlayerPanelView[] playerPanels;
        [SerializeField] private PilesView piles;
        [SerializeField] private DiscardBrowserView discardBrowser;
        [SerializeField] private CardDetailView cardDetail;
        [SerializeField] private EventLogView eventLog;
        [SerializeField] private PrivacyOverlayView privacyOverlay;

        [Header("Map")]
        [Tooltip("Select a JSON map from Assets/StreamingAssets/Maps using the Inspector dropdown.")]
        [SerializeField] private string mapFileName = "default-map.json";

        [Header("Cards")]
        [SerializeField] private DeckSettings deckSettings = new DeckSettings();

        [Header("Hidden information")]
        [Tooltip("Hide other teams' Bandits and the captives they carry. Also available as a toggle in the HUD.")]
        [SerializeField] private bool hideEnemyBandits = false;
        [Tooltip("Show a 'pass the device' screen at the start of every team's turn. Turn it off for solo testing.")]
        [SerializeField] private bool usePrivacyOverlay = true;

        [Header("Debug")]
        [Tooltip("1-6 makes every die roll (movement and fights) this number. 0 = normal random die. " +
                 "Note: with equal modifiers a forced die ties, so fights end by the reroll cap.")]
        [SerializeField] private int forcedDieResult = 0;

        private GameState state;
        private TurnManager turn;
        private string clue = "";
        private int revealedTeam = -1;
        private int lastLoggedOwner = -1;

        private bool OverlayActive => usePrivacyOverlay && privacyOverlay != null;
        private bool InputBlocked => OverlayActive && privacyOverlay.IsVisible;

        private void OnEnable()
        {
            if (boardView != null) boardView.PositionClicked += OnPositionClicked;
            if (hud != null)
            {
                hud.PrimaryClicked += OnPrimary;
                hud.CancelClicked += OnCancel;
                hud.ActionGroupSelected += OnActionGroupSelected;
                hud.HideBanditsChanged += OnHideBanditsChanged;
            }
            if (promptView != null) promptView.OptionChosen += OnPromptChosen;
            if (piles != null) piles.DiscardClicked += OnDiscardClicked;
            if (privacyOverlay != null) privacyOverlay.RevealClicked += OnRevealClicked;
            if (input != null) input.PrimaryPressed += OnPrimary;
        }

        private void OnDisable()
        {
            if (boardView != null) boardView.PositionClicked -= OnPositionClicked;
            if (hud != null)
            {
                hud.PrimaryClicked -= OnPrimary;
                hud.CancelClicked -= OnCancel;
                hud.ActionGroupSelected -= OnActionGroupSelected;
                hud.HideBanditsChanged -= OnHideBanditsChanged;
            }
            if (promptView != null) promptView.OptionChosen -= OnPromptChosen;
            if (piles != null) piles.DiscardClicked -= OnDiscardClicked;
            if (privacyOverlay != null) privacyOverlay.RevealClicked -= OnRevealClicked;
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
            revealedTeam = -1;
            lastLoggedOwner = -1;

            boardView.Build(grid);
            hud.SetHideBandits(hideEnemyBandits);
            if (promptView != null) promptView.Hide();

            VisualTheme theme = boardView.Theme;

            if (playerPanels != null)
            {
                for (int i = 0; i < playerPanels.Length && i < state.TeamCount; i++)
                    if (playerPanels[i] != null)
                        playerPanels[i].Initialize(i, theme, cardDetail);
            }

            if (piles != null) piles.Initialize(theme, cardDetail);
            if (discardBrowser != null) { discardBrowser.Initialize(theme, cardDetail); discardBrowser.Hide(); }
            if (cardDetail != null) cardDetail.Hide();
            if (eventLog != null) eventLog.Clear();

            turn = new TurnManager(state, deckSettings);

            if (forcedDieResult > 0)
            {
                int forced = forcedDieResult;
                turn.DieRoller = () => forced;
            }

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

        private void OnPositionClicked(Vector2Int position)
        {
            if (!InputBlocked) turn?.OnTileClicked(position);
        }

        // Space does nothing while the overlay is up, so a double press can't reveal the next hand.
        private void OnPrimary()
        {
            if (!InputBlocked) turn?.OnPrimaryButton();
        }

        private void OnCancel()
        {
            if (!InputBlocked) turn?.OnCancel();
        }

        private void OnActionGroupSelected(string group)
        {
            if (!InputBlocked) turn?.OnActionGroupSelected(group);
        }

        private void OnPromptChosen(int index)
        {
            if (!InputBlocked) turn?.OnPromptChosen(index);
        }

        private void OnHideBanditsChanged(bool value)
        {
            hideEnemyBandits = value;
            Refresh();
        }

        private void OnDiscardClicked()
        {
            if (turn != null && discardBrowser != null && !InputBlocked)
                discardBrowser.Toggle(turn.Cards.DiscardPile);
        }

        private void OnRevealClicked()
        {
            if (turn == null)
                return;

            revealedTeam = turn.TurnOwner;
            Refresh();
        }

        // ---- Output to the views ----

        private void OnTurnEvent(TurnEvent e)
        {
            TurnMessage message = TurnMessages.Describe(e, state);

            if (message.Clue != null)
                clue = message.Clue;

            // The log gets what the console gets, or the status clue when there is no log line.
            // "Choose a highlighted tile" style hints are not history.
            string line = message.Log ?? message.Clue;
            if (e.Kind == TurnEventKind.AttackInvalidTarget)
                line = null;

            if (line != null && eventLog != null)
                eventLog.Add(line);

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

            VisualTheme theme = boardView.Theme;
            int owner = turn.TurnOwner;

            if (owner != lastLoggedOwner)
            {
                lastLoggedOwner = owner;
                if (eventLog != null)
                    eventLog.AddDivider($"Player {owner + 1}'s turn", theme.GetTeamColor(owner));
            }

            // The overlay covers everything until the team whose turn it is has clicked Reveal.
            bool needsReveal = OverlayActive && revealedTeam != owner;

            // Bandit hiding follows the turn owner, also during a rescue run.
            boardView.Render(state, step, owner, hideEnemyBandits);

            DecisionPrompt prompt = step.Prompt;
            if (promptView != null)
            {
                if (prompt != null) promptView.Show(prompt);
                else promptView.Hide();
            }

            if (playerPanels != null)
            {
                for (int i = 0; i < playerPanels.Length && i < state.TeamCount; i++)
                {
                    if (playerPanels[i] == null)
                        continue;

                    bool handFaceUp = i == owner && !needsReveal;
                    playerPanels[i].Refresh(state, turn.Cards.MaxHandSize, handFaceUp, i == turn.CurrentTeam);
                }
            }

            if (piles != null) piles.Refresh(turn.Cards);

            if (discardBrowser != null)
            {
                if (needsReveal) discardBrowser.Hide();
                else discardBrowser.Refresh(turn.Cards.DiscardPile);
            }

            if (needsReveal && cardDetail != null)
                cardDetail.Hide();

            if (privacyOverlay != null)
            {
                if (needsReveal) privacyOverlay.Show(owner, theme.GetStatusBackground(owner));
                else privacyOverlay.Hide();
            }

            hud.Show(
                TurnMessages.BuildStatus(turn, clue),
                theme.GetStatusBackground(turn.CurrentTeam),
                prompt != null ? "Choose an option in the panel" : TurnMessages.GetPrimaryLabel(step.Primary),
                step.Primary != PrimaryAction.None,
                step.CanCancel,
                step.AvailableActionGroups,
                step.SelectedActionGroup);
        }
    }
}