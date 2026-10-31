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

        public int CurrentTeam { get; private set; }
        public int CurrentSlotIndex { get; private set; }
        public Pawn ActivePawn { get; private set; }
        public ITurnState CurrentState { get; private set; }

        /// Movement allowance of the current move (die roll, or a fixed 5).
        public int RolledMovement { get; internal set; }
        public int BloodhoundMovesRemaining { get; internal set; }

        /// True while a freed Royalty runs to safety: movement only, no action.
        public bool IsRescueRun { get; private set; }

        /// Replace this to force die results while testing. Used for movement and fights.
        public Func<int> DieRoller { get; set; }

        /// Raised once after each input has been handled, and once from Start().
        public event Action Changed;

        /// Raised immediately, for messages and logging.
        public event Action<TurnEvent> EventRaised;

        public TurnManager(GameState state)
        {
            State = state;
            Movement = new MovementRules(state.Grid);
            AbductionRules = new AbductionRules(state, Movement);
            RescueRules = new RescueRules(state, Movement);
            BloodhoundRules = new BloodhoundRules(state, RescueRules);
            AmbushRules = new AmbushRules(state);
            CombatRules = new CombatRules(state, RescueRules, () => DieRoller());

            DieRoller = () => GameConfig.MovementDie[
                UnityEngine.Random.Range(0, GameConfig.MovementDie.Length)];
        }

        // ---- Input (called by the view) ----

        public void Start()
        {
            Dispatch(() =>
            {
                CurrentTeam = 0;
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

        // ---- Used by the states and actions ----

        internal void ChangeState(ITurnState next)
        {
            CurrentState = next;
            next.Enter();
            MarkChanged();
        }

        internal int RollMovementDie() => DieRoller();

        internal void Raise(TurnEvent turnEvent)
        {
            EventRaised?.Invoke(turnEvent);
            MarkChanged();
        }

        /// Delivers the captives if the Bandit stands on its own castle, and reports it.
        internal void ResolveDelivery(Pawn bandit)
        {
            DeliveryResult result = AbductionRules.DeliverIfHome(bandit);
            if (result.Happened)
                Raise(TurnEvent.Delivered(bandit, result.Delivered, result.WoodStolen));
        }

        /// The active pawn has chosen where to stop. Fights and delivery are resolved
        /// here, then the pawn gets its action (or the activation ends).
        internal void CompleteMovement()
        {
            Pawn pawn = ActivePawn;

            if (pawn.Type == PawnType.Squire)
            {
                // Moving onto an enemy Squire starts a fight. That fight is the Squire's action.
                List<FightResult> fights = CombatRules.FightSquiresAt(pawn, pawn.Position);
                if (fights.Count > 0)
                {
                    foreach (FightResult fight in fights)
                        Raise(TurnEvent.SquireFight(fight));

                    FinishActivation();
                    return;
                }
            }
            else if (pawn.Type == PawnType.Bandit &&
                     CombatRules.TryFightOccupant(pawn, out FightResult fight))
            {
                // A hiding place holds one Bandit, so the arriving Bandit fights the occupant.
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
            }

            ResolveDelivery(pawn);
            EndMovement();
        }

        /// Movement is done: a rescue run ends here, every other pawn gets its action.
        internal void EndMovement()
        {
            if (IsRescueRun)
                FinishActivation();
            else
                ChangeState(new ChooseActionState(this));
        }

        internal void FinishActivation()
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
            StartNextRescueRun();
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
            CurrentSlotIndex++;

            if (CurrentSlotIndex >= GameConfig.TurnOrder.Length)
            {
                CurrentSlotIndex = 0;
                CurrentTeam = (CurrentTeam + 1) % State.TeamCount;
            }

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
    }
}