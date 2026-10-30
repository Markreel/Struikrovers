using UnityEngine;

namespace Struikrovers.Core
{
    /// The single action of a Royalty, Squire or Bandit. It can always be skipped.
    public sealed class ChooseActionState : TurnState
    {
        public ChooseActionState(TurnManager turn) : base(turn) { }

        public override PrimaryAction Primary => PrimaryAction.Finish;

        public override void OnPrimaryButton() => Turn.FinishActivation();

        public override void OnTileClicked(Vector2Int position)
        {
            switch (Pawn.Type)
            {
                case PawnType.Royalty:
                    {
                        // Loot an adjacent bush. A hidden enemy Bandit abducts the Royalty instead.
                        LootOutcome outcome = Turn.ForagingRules.TryLoot(
                            Pawn, position, out int cards, out Pawn ambusher);

                        if (outcome == LootOutcome.Ambushed)
                        {
                            Turn.Raise(TurnEvent.Abducted(Pawn, ambusher));
                            Turn.FinishActivation();
                        }
                        else if (outcome == LootOutcome.Looted)
                        {
                            Turn.Raise(TurnEvent.Looted(Pawn, cards));
                            Turn.FinishActivation();
                        }

                        break;
                    }

                case PawnType.Bandit:
                    {
                        // Abduct an adjacent enemy Royalty.
                        if (Turn.AbductionRules.TryAbductAdjacent(Pawn, position, out Pawn abducted))
                        {
                            Turn.Raise(TurnEvent.Abducted(abducted, Pawn));
                            Turn.FinishActivation();
                        }

                        break;
                    }

                case PawnType.Squire:
                    {
                        // Chop an adjacent tree.
                        if (Turn.ForagingRules.TryChop(Pawn, position, out int wood))
                        {
                            Turn.Raise(TurnEvent.Chopped(Pawn, wood));
                            Turn.FinishActivation();
                        }

                        break;
                    }
            }
        }
    }
}