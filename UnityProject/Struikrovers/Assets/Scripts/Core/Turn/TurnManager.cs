using System;
using System.Collections.Generic;
using UnityEngine;

namespace Struikrovers.Core
{
    /// Owns whose turn it is and which state handles input. No UI in here.
    public sealed class TurnManager
    {
        private readonly struct ResumePoint
        {
            public readonly int Team;
            public readonly int SlotIndex;
            public readonly Pawn Pawn;
            /// True when the interrupted pawn still has its action to take.
            public readonly bool ResumeWithAction;

            public ResumePoint(int team, int slotIndex, Pawn pawn, bool resumeWithAction)
            {
                Team = team;
                SlotIndex = slotIndex;
                Pawn = pawn;
                ResumeWithAction = resumeWithAction;
            }
        }

        // Rescued Royalty run to safety one by one, then the interrupted
        // activation resumes from the resume point.
        private readonly Queue<Pawn> pendingRescueRuns = new Queue<Pawn>();
        private ResumePoint? resumePoint;

        // Several state changes in one input produce a single Changed event.
        private int batchDepth;
        private bool dirty;

        public GameState State { get; }
        public MovementRules Movement { get; }
        public AbductionRules AbductionRules { get; }
        public RescueRules RescueRules { get; }
        public BloodhoundRules BloodhoundRules { get; }
        public AmbushRules AmbushRules { get; }
        public CombatRules CombatRules { get; }
        public CardSystem Cards { get; }
        public DiceService Dice { get; }

        public int CurrentTeam { get; private set; }
        /// The team whose turn it is. Unlike CurrentTeam it stays put during a rescue run.
        public int TurnOwner { get; private set; }
        public int CurrentSlotIndex { get; private set; }
        public Pawn ActivePawn { get; private set; }
        public ITurnState CurrentState { get; private set; }

        /// Movement allowance of the current move (die roll, or a fixed 5).
        public int RolledMovement { get; internal set; }
        public int BloodhoundMovesRemaining { get; internal set; }

        /// The movement roll of the active pawn. Null for pawns that don't roll (Bandit, Bloodhound).
        public DiceRoll MovementRoll { get; private set; }

        /// True from the roll until the player commits to a move. Roll cards work in this window.
        public bool MovementRollOpen { get; private set; }

        /// Hand cards the turn owner has played this turn (see GameConfig.MaxHandCardsPerTurn).
        public int HandCardsPlayedThisTurn { get; private set; }

        /// True while a freed Royalty runs to safety: movement only, no action.
        public bool IsRescueRun { get; private set; }

        /// Fights stop after the dice so cards can be played. Off = fights resolve at once,
        /// with ties rolled again automatically. Handy for solo testing.
        public bool ConfirmFightRolls { get; set; } = true;

        /// Mirrors the "Hide enemy Bandits" setting. Peek needs it on. Set by the GameController.
        public bool HideEnemyBandits { get; set; }

        /// Replace this to force die results while testing. Used for every die.
        public Func<int> DieRoller { get; set; }

        /// Raised once after each input has been handled, and once from Start().
        public event Action Changed;

        /// Raised immediately, for messages and logging.
        public event Action<TurnEvent> EventRaised;

        public TurnManager(GameState state) : this(state, new DeckSettings()) { }

        public TurnManager(GameState state, DeckSettings deckSettings)
        {
            State = state;
            Movement = new MovementRules(state);
            Cards = new CardSystem(state, deckSettings, e => Raise(e));
            Cards.ApplyDebugCurses();
            Dice = new DiceService(state, () => DieRoller());
            AbductionRules = new AbductionRules(state, Movement, Cards);
            RescueRules = new RescueRules(state, Movement);
            BloodhoundRules = new BloodhoundRules(state, RescueRules);
            AmbushRules = new AmbushRules(state, Cards);
            CombatRules = new CombatRules(state, RescueRules, Cards, Dice);

            DieRoller = () => GameConfig.MovementDie[
                UnityEngine.Random.Range(0, GameConfig.MovementDie.Length)];
        }

        // ---- Input (called by the view) ----

        public void Start()
        {
            Dispatch(() =>
            {
                CurrentTeam = 0;
                TurnOwner = 0;
                HandCardsPlayedThisTurn = 0;
                CurrentSlotIndex = 0;
                ActivePawn = State.GetPawnForSlot(CurrentTeam, GameConfig.TurnOrder[CurrentSlotIndex]);
                ChangeState(new AwaitRollState(this));
            });
        }

        public void OnTileClicked(Vector2Int position)
        {
            Dispatch(() => CurrentState?.OnTileClicked(position));
        }

        public void OnPrimaryButton()
        {
            Dispatch(() =>
            {
                if (CurrentState != null && CurrentState.Primary != PrimaryAction.None)
                    CurrentState.OnPrimaryButton();
            });
        }

        public void OnCancel()
        {
            Dispatch(() =>
            {
                if (CurrentState != null && CurrentState.CanCancel)
                    CurrentState.OnCancel();
            });
        }

        public void OnActionGroupSelected(string group)
        {
            Dispatch(() =>
            {
                CurrentState?.OnActionGroupSelected(group);
                MarkChanged();
            });
        }

        public void OnPromptChosen(int index)
        {
            Dispatch(() => CurrentState?.OnPromptChosen(index));
        }

        // ---- Managing your own cards (any time during your turn, except while a question is open) ----

        /// True when the turn owner may discard cards or play hand cards right now.
        public bool CanManage =>
            CurrentState != null && CurrentState.Prompt == null && !Cards.HasPending;

        /// Equipment can't be changed while a fight waits: its modifiers are already counted.
        public bool CanManageEquipment => CanManage && !(CurrentState is FightPauseState);

        public void MoveEquipment(Pawn pawn, EquipmentSlot slot, Pawn target)
        {
            Dispatch(() =>
            {
                if (CanManageEquipment && pawn != null && pawn.Team == TurnOwner)
                {
                    Cards.MoveEquipment(pawn, slot, target);
                    RefreshMoveChoices();
                }
            });
        }

        public void DiscardEquipment(Pawn pawn, EquipmentSlot slot)
        {
            Dispatch(() =>
            {
                if (CanManageEquipment && pawn != null && pawn.Team == TurnOwner)
                { 
                    Cards.DiscardEquipment(pawn, slot);
                    RefreshMoveChoices();
                }
            });
        }

        public void DiscardHandCard(int team, int index, CardDefinition expected)
        {
            Dispatch(() =>
            {
                if (CanManage && team == TurnOwner)
                    Cards.DiscardHandCard(team, index, expected);
            });
        }

        // ---- Playing hand cards ----

        public bool CanPlayHandCard(int team, CardDefinition card, out string reason)
        {
            reason = null;

            IHandCardEffect effect = HandCardEffects.Get(card);
            if (effect == null)
            {
                reason = "this card can't be played yet";
                return false;
            }

            if (team != TurnOwner)
            {
                reason = "it isn't your turn";
                return false;
            }

            if (!CanManage)
            {
                reason = "answer the question first";
                return false;
            }

            if (HandCardsPlayedThisTurn >= GameConfig.MaxHandCardsPerTurn)
            {
                reason = "you already played a hand card this turn";
                return false;
            }

            return effect.CanPlay(this, out reason);
        }

        // The hand card being played right now, so a question it asks can be cancelled.
        private bool playing;
        private int playingTeam;
        private int playingIndex;
        private CardDefinition playingCard;

        public void PlayHandCard(int team, int index, CardDefinition expected)
        {
            Dispatch(() =>
            {
                if (!CanPlayHandCard(team, expected, out _))
                    return;

                if (!Cards.PlayCard(team, index, expected))
                    return;

                HandCardsPlayedThisTurn++;

                playing = true;
                playingTeam = team;
                playingIndex = index;
                playingCard = expected;

                try { HandCardEffects.Get(expected).Play(this); }
                finally { playing = false; }
            });
        }

        /// A hand card that needs a decision: asks the turn owner, then resolves whatever was
        /// taken (equip it? hand full?) and goes back to the state the turn was in.
        /// Cancelling gives the card back and goes straight back to that state.
        internal void AskDuringTurn(DecisionPrompt prompt)
        {
            if (prompt == null)
                return;

            ITurnState previous = CurrentState;
            Action cancel = null;

            if (playing)
            {
                int team = playingTeam;
                int index = playingIndex;
                CardDefinition card = playingCard;

                cancel = () =>
                {
                    if (Cards.UndoPlay(team, index, card))
                        HandCardsPlayedThisTurn = Math.Max(0, HandCardsPlayedThisTurn - 1);

                    ResumeState(previous);
                };
            }

            ChangeState(new PromptState(this, prompt, () => RunPending(() => ResumeState(previous)), cancel));
        }

        // Goes back to a state without entering it again, so its choices (action group...) stay as they were.
        private void ResumeState(ITurnState previous)
        {
            CurrentState = previous;
            RefreshMoveChoices();
            MarkChanged();
        }

        /// True when the turn owner holds a roll card they could play right now (for hints).
        public bool RollCardAvailable
        {
            get
            {
                foreach (CardDefinition card in State.Teams[TurnOwner].Hand)
                    if (HandCardEffects.ChangesRoll(card) && CanPlayHandCard(TurnOwner, card, out _))
                        return true;

                return false;
            }
        }

        // ---- Rolls that cards can change ----

        /// True when the turn owner has a roll of their own that is still open: their movement
        /// roll before they commit, or the attacker's die in a fight they started.
        internal bool CanChangeRoll(out string reason)
        {
            reason = null;

            if (CurrentState is FightPauseState pause)
            {
                if (pause.Fight.Attacker.Team == TurnOwner)
                    return true;

                reason = "you can only change your own rolls";
                return false;
            }

            if (CurrentState is ChooseDestinationState && MovementRollOpen && MovementRoll != null)
            {
                if (MovementRoll.Pawn.Team == TurnOwner)
                    return true;

                reason = "you can only change your own rolls";
                return false;
            }

            reason = "there is no roll of yours to change right now";
            return false;
        }

        internal void RerollCurrentRoll()
        {
            if (CurrentState is FightPauseState pause)
            {
                CombatRules.RerollAttacker(pause.Fight);
                Raise(TurnEvent.FightRolled(pause.Fight));
                return;
            }

            MovementRoll = Dice.Reroll(MovementRoll);
            ApplyMovementRoll();
        }

        internal void DoubleCurrentRoll()
        {
            if (CurrentState is FightPauseState pause)
            {
                CombatRules.DoubleAttacker(pause.Fight);
                Raise(TurnEvent.FightRolled(pause.Fight));
                return;
            }

            MovementRoll = Dice.DoubleUp(MovementRoll);
            ApplyMovementRoll();
        }

        // The new roll changes how far the pawn can go, so the destinations are worked out again.
        private void ApplyMovementRoll()
        {
            RolledMovement = MovementRoll.Total;
            Raise(TurnEvent.DiceRolled(MovementRoll));
            ChangeState(new ChooseDestinationState(this));
        }

        internal void BeginMovementRoll(DiceRoll roll)
        {
            MovementRoll = roll;
            MovementRollOpen = true;
            RolledMovement = roll.Total;
            Raise(TurnEvent.DiceRolled(roll));
        }

        /// The player has committed to a move: the roll can no longer be changed.
        internal void CloseMovementRoll() => MovementRollOpen = false;

        internal void ClearMovementRoll()
        {
            MovementRoll = null;
            MovementRollOpen = false;
        }

        // ---- Used by the states and actions ----

        internal void ChangeState(ITurnState next)
        {
            CurrentState = next;
            next.Enter();
            MarkChanged();
        }

        internal void Raise(TurnEvent turnEvent)
        {
            EventRaised?.Invoke(turnEvent);
            MarkChanged();
        }

        /// Resolves every drawn card that is waiting (hand full? equip it?), asking the player
        /// where needed, and then runs 'continuation'. Without waiting cards it runs at once.
        internal void RunPending(Action continuation)
        {
            while (Cards.HasPending)
            {
                DecisionPrompt prompt = Cards.ProcessNext();
                if (prompt == null)
                    continue;

                ChangeState(new PromptState(this, prompt, () => RunPending(continuation)));
                return;
            }

            continuation();
        }

        /// Delivers the captives if the Bandit stands on its own castle, and reports it.
        internal void ResolveDelivery(Pawn bandit)
        {
            DeliveryResult result = AbductionRules.DeliverIfHome(bandit);
            if (result.Happened)
                Raise(TurnEvent.Delivered(bandit, result.Delivered, result.WoodStolen));
        }

        // ---- Fights ----

        /// Rolls one fight. With ConfirmFightRolls the game waits on the dice (cards, tie
        /// rerolls, Keep result). 'onResolved' runs once the fight has been applied.
        internal void StartFight(Pawn attacker, Pawn defender, FightKind kind, Action<FightResult> onResolved)
        {
            PendingFight fight = CombatRules.Prepare(attacker, defender, kind);

            if (!ConfirmFightRolls)
            {
                CombatRules.RollOutTies(fight);
                onResolved(CombatRules.Resolve(fight));
                return;
            }

            Raise(TurnEvent.FightRolled(fight));
            ChangeState(new FightPauseState(this, fight, onResolved));
        }

        /// Fights every enemy Squire on the tile one after another, until the attacker dies or
        /// all are beaten. Each fight is announced. 'onDone' gets every result.
        internal void FightSquiresAt(Pawn attacker, Vector2Int tile, Action<List<FightResult>> onDone)
        {
            List<Pawn> opponents = CombatRules.GetSquireOpponents(attacker, tile);
            var results = new List<FightResult>();
            int next = 0;

            void FightNext()
            {
                if (next >= opponents.Count)
                {
                    onDone(results);
                    return;
                }

                Pawn opponent = opponents[next++];

                StartFight(attacker, opponent, FightKind.VsSquire, result =>
                {
                    results.Add(result);
                    Raise(TurnEvent.SquireFight(result));

                    // The attacker lost and is back in its castle.
                    if (!result.AttackerWon)
                        onDone(results);
                    else
                        FightNext();
                });
            }

            FightNext();
        }

        /// A Royalty guarded by enemy Squires can only be abducted after the Bandit beats them.
        /// 'onDone' gets true when the Bandit survived (or nothing guarded the tile). When it
        /// lost, 'rescued' holds Royalty it was carrying that were freed and need a rescue run.
        internal void FightGuards(Pawn bandit, Vector2Int tile, Action<bool, List<Pawn>> onDone)
        {
            FightSquiresAt(bandit, tile, results =>
            {
                var rescued = new List<Pawn>();
                int blocked = 0;

                foreach (FightResult fight in results)
                {
                    blocked += fight.Blocked;
                    if (fight.Rescued != null)
                        rescued.AddRange(fight.Rescued);
                }

                if (blocked > 0)
                    Raise(TurnEvent.AttackBlocked(bandit, blocked));

                onDone(results.TrueForAll(f => f.AttackerWon), rescued);
            });
        }

        /// The active pawn has chosen where to stop. A Bandit fights the occupant of its hiding
        /// place, delivery is resolved, then the pawn gets its action (or the activation ends).
        /// Squires fight enemy Squires on the way, see ChooseDestinationState.
        internal void CompleteMovement()
        {
            Pawn pawn = ActivePawn;

            if (pawn.Type == PawnType.Bandit && CombatRules.TryGetOccupant(pawn, out Pawn occupant))
            {
                // A hiding place holds one Bandit, so the arriving Bandit fights the occupant.
                StartFight(pawn, occupant, FightKind.VsBandit, fight =>
                {
                    Raise(TurnEvent.BanditFight(fight));

                    if (fight.Blocked > 0)
                        Raise(TurnEvent.AttackBlocked(pawn, fight.Blocked));

                    bool survived = !pawn.AtCastle;

                    if (fight.ReleasedCount > 0)
                    {
                        // Freed Royalty run to safety first. A surviving Bandit then still gets its action.
                        BeginRescueRuns(fight.Rescued, survived);
                        return;
                    }

                    if (!survived)
                    {
                        FinishActivation();
                        return;
                    }

                    FinishMovement(pawn);
                });

                return;
            }

            FinishMovement(pawn);
        }

        // Delivery, then stolen equipment is resolved (prompts), then the action phase.
        private void FinishMovement(Pawn pawn)
        {
            ResolveDelivery(pawn);
            RunPending(EndMovement);
        }

        /// Movement is done: a rescue run ends here, every other pawn gets its action.
        internal void EndMovement()
        {
            if (IsRescueRun)
                FinishActivation();
            else
                ChangeState(new ChooseActionState(this));
        }

        /// Ends the active pawn's activation. Cards drawn during it are resolved first.
        internal void FinishActivation()
        {
            RunPending(FinishActivationNow);
        }

        private void FinishActivationNow()
        {
            if (IsRescueRun)
            {
                StartNextRescueRun();
                return;
            }

            AdvanceToNextPawn();
        }

        /// Each freed Royalty gets a movement-only run. Afterwards the interrupted pawn either
        /// takes its action (resumeWithAction) or its activation ends.
        internal void BeginRescueRuns(IEnumerable<Pawn> rescued, bool resumeWithAction = false)
        {
            resumePoint = new ResumePoint(CurrentTeam, CurrentSlotIndex, ActivePawn, resumeWithAction);

            foreach (Pawn royalty in rescued)
                pendingRescueRuns.Enqueue(royalty);

            IsRescueRun = true;
            RunPending(StartNextRescueRun);
        }

        // ---- Turn order ----

        private void StartNextRescueRun()
        {
            if (pendingRescueRuns.Count > 0)
            {
                Pawn royalty = pendingRescueRuns.Dequeue();

                CurrentTeam = royalty.Team;
                CurrentSlotIndex = 0; // Royalty is the first slot in GameConfig.TurnOrder.
                ActivePawn = royalty;

                Raise(TurnEvent.RescueRunStarted(royalty));
                ChangeState(new AwaitRollState(this));
                return;
            }

            // All freed Royalty have run. Resume the interrupted activation,
            // rather than advancing from the last rescuer.
            IsRescueRun = false;
            bool resumeWithAction = false;

            if (resumePoint.HasValue)
            {
                CurrentTeam = resumePoint.Value.Team;
                CurrentSlotIndex = resumePoint.Value.SlotIndex;
                ActivePawn = resumePoint.Value.Pawn;
                resumeWithAction = resumePoint.Value.ResumeWithAction;
                resumePoint = null;
            }

            if (resumeWithAction)
                ChangeState(new ChooseActionState(this));
            else
                AdvanceToNextPawn();
        }

        private void AdvanceToNextPawn()
        {
            int next = CurrentSlotIndex + 1;

            if (next >= GameConfig.TurnOrder.Length)
            {
                // All three pawns have acted: the team draws a card to signal the end of its turn.
                // The drawn cards are resolved (hand full? equip?) before the next team starts.
                Cards.QueueDraw(CurrentTeam, Cards.EndOfTurnDraws);
                RunPending(StartNextTeam);
                return;
            }

            CurrentSlotIndex = next;
            ActivateCurrentSlot();
        }

        private void StartNextTeam()
        {
            CurrentSlotIndex = 0;
            CurrentTeam = (CurrentTeam + 1) % State.TeamCount;
            TurnOwner = CurrentTeam;
            HandCardsPlayedThisTurn = 0;
            ActivateCurrentSlot();
        }

        private void ActivateCurrentSlot()
        {
            ActivePawn = State.GetPawnForSlot(CurrentTeam, GameConfig.TurnOrder[CurrentSlotIndex]);
            ChangeState(new AwaitRollState(this));
        }

        // ---- Change batching ----

        private void Dispatch(Action action)
        {
            batchDepth++;
            try { action(); }
            finally { batchDepth--; }

            if (batchDepth == 0)
                Flush();
        }

        private void MarkChanged()
        {
            dirty = true;
            if (batchDepth == 0)
                Flush();
        }

        private void Flush()
        {
            if (!dirty)
                return;

            dirty = false;
            Changed?.Invoke();
        }

        // Movement items can change while destinations are shown: recalculate what can be reached.
        private void RefreshMoveChoices()
        {
            if (CurrentState is ChooseDestinationState choose)
                choose.Refresh();
        }
    }
}