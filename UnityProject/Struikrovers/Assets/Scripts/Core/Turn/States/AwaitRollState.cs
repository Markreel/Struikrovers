namespace Struikrovers.Core
{
    /// Start of an activation. The die is rolled here for Royalty and Squire; the
    /// Bandit and the Bloodhound start with a fixed movement.
    public sealed class AwaitRollState : TurnState
    {
        public AwaitRollState(TurnManager turn) : base(turn) { }

        public override PrimaryAction Primary =>
            Pawn.Type == PawnType.Bandit ? PrimaryAction.MoveBandit : PrimaryAction.Roll;

        // A new activation starts without an open roll.
        public override void Enter() => Turn.ClearMovementRoll();

        public override void OnPrimaryButton()
        {
            switch (Pawn.Type)
            {
                case PawnType.Bloodhound:
                    // Records the scent distance BEFORE the first move, so the first
                    // sniff can compare the new position with the starting position.
                    Turn.BloodhoundRules.BeginActivation(Pawn);
                    Turn.BloodhoundMovesRemaining = GameConfig.BloodhoundMovesPerActivation;
                    Turn.RolledMovement = GameConfig.BloodhoundMoveSteps;
                    Turn.ChangeState(new BloodhoundMoveState(Turn));
                    break;

                case PawnType.Bandit:
                    Turn.RolledMovement = GameConfig.BanditMoveSteps;
                    Turn.ChangeState(new ChooseDestinationState(Turn));
                    break;

                default:
                    // The roll stays open until the player picks a move, so cards can change it.
                    Turn.BeginMovementRoll(Turn.Dice.Roll(Pawn, RollKind.Movement));

                    // A freed Royalty that is boxed in on its hiding place cannot run.
                    // It is placed on the nearest free grass tile instead.
                    if (Turn.IsRescueRun && Pawn.Type == PawnType.Royalty &&
                        Turn.Movement.GetDestinations(Pawn, Turn.RolledMovement).Count == 0)
                    {
                        Turn.CloseMovementRoll();
                        Turn.RescueRules.PlaceNearest(Pawn);
                        Turn.Raise(TurnEvent.RescueRunBlocked(Pawn));
                        Turn.EndMovement(); // a rescue run has no action, so this ends the run
                        break;
                    }

                    Turn.ChangeState(new ChooseDestinationState(Turn));
                    break;
            }
        }
    }
}