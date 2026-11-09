using System;

namespace Struikrovers.Core
{
    /// Grants an action while worn, revokes it when taken off.
    /// canGrant (optional) decides per wearer, e.g. the Fishpole is never granted to a Bandit.
    public sealed class GrantActionEffect : EquipmentEffect
    {
        private readonly Func<IPawnAction> factory;
        private readonly Func<Pawn, bool> canGrant;

        public GrantActionEffect(Func<IPawnAction> factory, Func<Pawn, bool> canGrant = null)
        {
            this.factory = factory;
            this.canGrant = canGrant;
        }

        public override void OnEquipped(Pawn pawn, CardDefinition card)
        {
            if (canGrant == null || canGrant(pawn))
                pawn.Grant(factory(), card);
        }

        public override void OnUnequipped(Pawn pawn, CardDefinition card) => pawn.RevokeAll(card);
    }

    public sealed class HalberdEffect : EquipmentEffect
    {
        public override bool StabsAllAdjacent(Pawn wearer, CardDefinition card) => true;
    }

    public sealed class BattleaxeEffect : EquipmentEffect
    {
        public override bool KillsBanditOnChop(Pawn wearer, CardDefinition card) => true;
    }

    public sealed class NimbleCloakEffect : EquipmentEffect
    {
        public override bool GrantsExtraStep(Pawn wearer, CardDefinition card) => true;
    }

    public sealed class KeysToTheCastleEffect : EquipmentEffect
    {
        public override bool AllowsCastleStay(Pawn wearer, CardDefinition card) => true;
    }

    public sealed class ShriekingChaliceEffect : EquipmentEffect
    {
        public override bool MarksAbductionSpot(Pawn wearer, CardDefinition card) => true;
    }
}