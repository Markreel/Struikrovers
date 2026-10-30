namespace Struikrovers.Core
{
    /// Start of an activation. The die is rolled here for Royalty and Squire; the
    /// Bandit and the Bloodhound start with a fixed movement.
    public sealed class AwaitRollState : TurnState
    {
        public AwaitRollState(TurnManager turn) : base(turn) { }

        public override PrimaryAction Primary =>
            Pawn.Type == PawnType.Bandit ? PrimaryAction.MoveBandit : PrimaryAction.Roll;

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
                    Turn.RolledMovement = Turn.RollMovementDie();
                    Turn.ChangeState(new ChooseDestinationState(Turn));
                    break;
            }
        }
    }
}