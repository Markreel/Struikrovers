using UnityEngine;
using Struikrovers.Core;
using Struikrovers.Presentation;
using System.Collections.Generic;

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
        [SerializeField] private CardMenuView cardMenu;
        [SerializeField] private PrivacyOverlayView privacyOverlay;
        [SerializeField] private SelectionLayer selectionLayer;

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

        [Tooltip("Pressing space on the 'pass the device' screen clicks Reveal. Handy for testing. " +
         "Turn it off if a double press might reveal the next hand by accident.")]
        [SerializeField] private bool spaceRevealsOverlay = true;

        [Header("Debug")]
        [Tooltip("1-6 makes every die roll (movement and fights) this number. 0 = normal random die. " +
                 "Note: with equal modifiers a forced die ties, so fights end by the reroll cap.")]
        [SerializeField] private int forcedDieResult = 0;
        [Tooltip("Fights stop after the dice so you can play Reroll or Double Roll, and ties must be rolled again by hand. " +
         "Turn it off for solo testing: fights then resolve at once and ties reroll automatically.")]
        [SerializeField] private bool confirmFightRolls = true;

        private GameState state;
        private TurnManager turn;
        private string clue = "";
        private int revealedTeam = -1;
        private int lastLoggedOwner = -1;
        private bool wasAnimating;
        private DecisionPrompt selectionPrompt;

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
            if (playerPanels != null)
                foreach (PlayerPanelView panel in playerPanels)
                    if (panel != null) panel.CardClicked += OnCardClicked;
            if (input != null)
            {
                input.PrimaryPressed += OnPrimary;
                input.CancelPressed += OnEscape;
            }
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
            if (input != null)
            {
                input.PrimaryPressed -= OnPrimary;
                input.CancelPressed -= OnEscape;
            }
        }

        private void Start() => Reload();

        private void OnDestroy() => DetachTurn();

        private void Update()
        {
            bool animating = DiceAnimating;
            if (wasAnimating && !animating)
                Refresh();

            wasAnimating = animating;
        }

        private bool DiceAnimating
        {
            get
            {
                if (playerPanels == null)
                    return false;

                foreach (PlayerPanelView panel in playerPanels)
                    if (panel != null && panel.Dice != null && panel.Dice.IsAnimating)
                        return true;

                return false;
            }
        }

        private void ClearDice()
        {
            if (playerPanels == null)
                return;

            foreach (PlayerPanelView panel in playerPanels)
                if (panel != null && panel.Dice != null)
                    panel.Dice.Clear();
        }

        private void ShowRoll(int team, DiceRoll roll, string caption, int modifier)
        {
            if (roll == null || playerPanels == null || team < 0 || team >= playerPanels.Length)
                return;

            PlayerPanelView panel = playerPanels[team];
            if (panel != null && panel.Dice != null)
                panel.Dice.ShowRoll(roll, caption, modifier);
        }

        // Dice come from the roll events. A fight that resolves without a pause (ConfirmFightRolls off)
        // shows its dice from the result events. Showing the same roll twice doesn't re-animate it.
        private void ShowDice(TurnEvent e)
        {
            switch (e.Kind)
            {
                case TurnEventKind.DiceRolled:
                    ShowRoll(e.Team, e.Dice, "Movement", 0);
                    break;

                case TurnEventKind.FightRolled:
                    ShowRoll(e.Actor.Team, e.Dice,
                        TurnMessages.GetFighterCaption(e.Actor, true), e.Amount - e.Dice.Total);
                    ShowRoll(e.Target.Team, e.OtherDice,
                        TurnMessages.GetFighterCaption(e.Target, false), e.SecondAmount - e.OtherDice.Total);
                    break;

                case TurnEventKind.BanditFight:
                case TurnEventKind.SquireFight:
                    {
                        FightRoll roll = e.Fight.Roll;
                        ShowRoll(e.Fight.Attacker.Team, roll.AttackerDice,
                            TurnMessages.GetFighterCaption(e.Fight.Attacker, true), roll.AttackerModifier);
                        ShowRoll(e.Fight.Defender.Team, roll.DefenderDice,
                            TurnMessages.GetFighterCaption(e.Fight.Defender, false), roll.DefenderModifier);
                        break;
                    }
            }
        }

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

            selectionPrompt = null;
            if (selectionLayer != null)
            {
                selectionLayer.End();
                selectionLayer.SetOverlays(cardDetail != null ? cardDetail.gameObject : null);
            }

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
            if (cardMenu != null) cardMenu.Hide();
            if (eventLog != null) eventLog.Clear();

            turn = new TurnManager(state, deckSettings);
            turn.ConfirmFightRolls = confirmFightRolls;
            turn.HideEnemyBandits = hideEnemyBandits;

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
            if (InputBlocked)
            {
                // The overlay covers the buttons, so this is the space bar.
                if (spaceRevealsOverlay)
                    OnRevealClicked();

                return;
            }

            if (turn != null && turn.CurrentState is FightPauseState && DiceAnimating)
                return;

            turn?.OnPrimaryButton();
        }

        private void OnCancel()
        {
            if (!InputBlocked) turn?.OnCancel();
        }

        // Escape closes an open card menu first, otherwise it is the same as the Cancel button.
        private void OnEscape()
        {
            if (cardMenu != null && cardMenu.IsOpen)
            {
                cardMenu.Hide();
                return;
            }

            OnCancel();
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
            if (turn != null) turn.HideEnemyBandits = value;
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

        private void OnCardClicked(CardClick click)
        {
            if (turn == null || cardMenu == null || InputBlocked)
                return;

            if (selectionLayer != null && selectionLayer.IsActive)
                return;

            if (DiceAnimating)
                return;

            // Only the player whose turn it is can use cards.
            if (click.Team != turn.TurnOwner)
                return;

            var entries = new List<CardMenuEntry>();

            if (click.IsHand)
            {
                int index = click.HandIndex;
                CardDefinition card = click.Card;

                // Playing a card is always listed, with the reason when it can't be played now.
                if (HandCardEffects.Get(card) != null)
                {
                    bool canPlay = turn.CanPlayHandCard(click.Team, card, out string reason);
                    string label = canPlay ? $"Play {card.Name}" : $"Can't play {card.Name}: {reason}";

                    entries.Add(new CardMenuEntry(
                        label,
                        () => turn.PlayHandCard(click.Team, index, card),
                        canPlay));
                }

                if (turn.CanManage)
                {
                    entries.Add(new CardMenuEntry(
                        $"Discard {card.Name}",
                        () => turn.DiscardHandCard(click.Team, index, card)));
                }
            }
            else if (turn.CanManageEquipment)
            {
                Pawn pawn = state.GetPawnByRole(click.Team, click.Role);
                if (pawn == null)
                    return;

                EquipmentSlot slot = click.Slot;

                foreach (ManageOption option in turn.Cards.GetEquipmentOptions(pawn, slot))
                {
                    ManageOption chosen = option;

                    entries.Add(new CardMenuEntry(
                        TurnMessages.GetManageLabel(chosen, pawn, slot),
                        () =>
                        {
                            if (chosen.Kind == ManageKind.DiscardEquipment)
                                turn.DiscardEquipment(pawn, slot);
                            else
                                turn.MoveEquipment(pawn, slot, chosen.Target);
                        }));
                }
            }

            if (entries.Count == 0)
                return;

            cardMenu.Show(click.Card.Name, click.Card.Text, entries);
        }

        // ---- Output to the views ----

        private void OnTurnEvent(TurnEvent e)
        {
            ShowDice(e);
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

            // A new activation starts without dice from the last one.
            if (step is AwaitRollState)
                ClearDice();

            if (cardMenu != null) cardMenu.Hide();

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
            boardView.Render(state, step, owner, hideEnemyBandits,
    step.Prompt != null ? step.Prompt.RevealedBanditTeam : -1);

            DecisionPrompt prompt = step.Prompt;

            if (playerPanels != null)
            {
                for (int i = 0; i < playerPanels.Length && i < state.TeamCount; i++)
                {
                    if (playerPanels[i] == null)
                        continue;

                    bool ownHand = i == owner && !needsReveal;
                    // A hand is also shown face up while the owner picks from it with a Measuring Monocle.
                    bool handFaceUp = ownHand || (!needsReveal && RevealsHandOf(prompt, i));
                    int panelTeam = i;
                    System.Predicate<CardDefinition> playable = ownHand && !DiceAnimating
                        ? card => turn.CanPlayHandCard(panelTeam, card, out _)
                        : (System.Predicate<CardDefinition>)null;

                    playerPanels[i].Refresh(state, turn.Cards.MaxHandSize, handFaceUp, i == turn.CurrentTeam, playable);
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

            // Panels are up to date, so the selection can find the cards it has to lift.
            bool picking = RefreshSelection(prompt, step.CanCancel, needsReveal);

            if (promptView != null)
            {
                if (prompt != null && !picking) promptView.Show(prompt);
                else promptView.Hide();
            }

            string primaryLabel = prompt == null
                ? TurnMessages.GetPrimaryLabel(turn, step)
                : picking
                    ? (prompt.RevealedBanditTeam >= 0 ? "Press Continue" : "Choose a highlighted card")
                    : "Choose an option in the panel";

            hud.Show(
                TurnMessages.BuildStatus(turn, clue),
                theme.GetStatusBackground(turn.CurrentTeam),
                primaryLabel,
                step.Primary != PrimaryAction.None,
                step.CanCancel,
                step.AvailableActionGroups,
                step.SelectedActionGroup);
        }

        // True when the prompt is answered on the board, so the text panel stays hidden.
        private bool RefreshSelection(DecisionPrompt prompt, bool canCancel, bool needsReveal)
        {
            if (selectionLayer != null && prompt != null && prompt.RevealedBanditTeam >= 0)
                return RefreshReveal(prompt, needsReveal);

            if (selectionLayer == null || prompt == null || !prompt.IsBoardSelection)
            {
                EndSelection();
                return false;
            }

            // Wait behind the "pass the device" screen, and keep the text panel hidden meanwhile.
            if (needsReveal)
            {
                EndSelection();
                return true;
            }

            if (prompt == selectionPrompt)
                return true;

            var targets = new List<SelectionEntry>();
            int found = 0;

            foreach (PromptOption option in prompt.Options)
            {
                PromptTarget target = option.Target;
                GameObject go = null;

                if (playerPanels != null && target.Team >= 0 && target.Team < playerPanels.Length
                    && playerPanels[target.Team] != null)
                    go = playerPanels[target.Team].GetTargetObject(target);

                // A pawn row pulses its cards. A whole panel is only lifted above the dim.
                targets.Add(new SelectionEntry(go, target.Kind == PromptTargetKind.PawnRow));
                if (go != null) found++;
            }

            // Nothing to point at: fall back to the text panel.
            if (found == 0)
            {
                EndSelection();
                return false;
            }

            selectionPrompt = prompt;
            string hint = (prompt.Detail ?? "") + (canCancel ? "  (Esc to cancel)" : "");

            selectionLayer.BeginPick(prompt.Title, hint, targets, canCancel,
                index => turn?.OnPromptChosen(index),
                () => turn?.OnCancel());

            return true;
        }

        // Peek: dim the screen, keep the peeked Bandit's tile lit, wait for Continue.
        private bool RefreshReveal(DecisionPrompt prompt, bool needsReveal)
        {
            // Wait behind the "pass the device" screen, like the other selections.
            if (needsReveal)
            {
                EndSelection();
                return true;
            }

            if (prompt == selectionPrompt)
                return true;

            GameObject go = boardView.GetBanditObject(state, prompt.RevealedBanditTeam);

            // Nothing to light up: fall back to the text panel.
            if (go == null)
            {
                EndSelection();
                return false;
            }

            selectionPrompt = prompt;

            var targets = new List<SelectionEntry> { new SelectionEntry(go, false) };

            selectionLayer.BeginReveal(
                prompt.Title,
                prompt.Detail ?? "",
                targets,
                () => turn?.OnPromptChosen(0));

            return true;
        }

        private void EndSelection()
        {
            if (selectionPrompt == null)
                return;

            selectionPrompt = null;
            if (selectionLayer != null) selectionLayer.End();
        }

        private static bool RevealsHandOf(DecisionPrompt prompt, int team)
        {
            if (prompt == null || !prompt.IsBoardSelection)
                return false;

            foreach (PromptOption option in prompt.Options)
                if (option.Target.Kind == PromptTargetKind.HandCard
                    && option.Target.Team == team
                    && option.Target.RevealFace)
                    return true;

            return false;
        }
    }
}