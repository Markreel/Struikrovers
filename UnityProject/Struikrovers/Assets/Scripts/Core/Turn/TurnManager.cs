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

            public ResumePoint(int team, int slotIndex, Pawn pawn)
            {
                Team = team;
                SlotIndex = slotIndex;
                Pawn = pawn;
            }
        }

        // Rescued Royalty run to safety one by one, then the interrupted
        // Bloodhound activation resumes from the resume point.
        private readonly Queue<Pawn> pendingRescueRuns = new Queue<Pawn>();
        private ResumePoint? resumePoint;

        // Several state changes in one input produce a single Changed event.
        private int batchDepth;
        private bool dirty;

        public GameState State { get; }
        public MovementRules Movement { get; }
        public AbductionRules AbductionRules { get; }
        public BloodhoundRules BloodhoundRules { get; }
        public ForagingRules ForagingRules { get; }

        public int CurrentTeam { get; private set; }
        public int CurrentSlotIndex { get; private set; }
        public Pawn ActivePawn { get; private set; }
        public ITurnState CurrentState { get; private set; }

        /// Movement allowance of the current move (die roll, or a fixed 5).
        public int RolledMovement { get; internal set; }
        public int BloodhoundMovesRemaining { get; internal set; }

        /// True while a freed Royalty runs to safety: movement only, no action.
        public bool IsRescueRun { get; private set; }

        /// Replace this to force die results while testing.
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
            BloodhoundRules = new BloodhoundRules(state, Movement);
            ForagingRules = new ForagingRules(state, AbductionRules);

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

        // ---- Used by the states ----

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

        /// Called after a successful Bloodhound attack. Each freed Royalty gets a
        /// movement-only run, then the interrupted activation is finished.
        internal void BeginRescueRuns(IEnumerable<Pawn> rescued)
        {
            resumePoint = new ResumePoint(CurrentTeam, CurrentSlotIndex, ActivePawn);

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

            // All freed Royalty have run. Resume the interrupted Bloodhound
            // activation and finish it, rather than advancing from the last rescuer.
            IsRescueRun = false;

            if (resumePoint.HasValue)
            {
                CurrentTeam = resumePoint.Value.Team;
                CurrentSlotIndex = resumePoint.Value.SlotIndex;
                ActivePawn = resumePoint.Value.Pawn;
                resumePoint = null;
            }

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