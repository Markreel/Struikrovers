using System;

namespace Struikrovers.Core
{
    /// Grants an action while worn, revokes it when taken off.
    public sealed class GrantActionEffect : EquipmentEffect
    {
        private readonly Func<IPawnAction> factory;
        public GrantActionEffect(Func<IPawnAction> factory) { this.factory = factory; }

        public override void OnEquipped(Pawn pawn, CardDefinition card) => pawn.Grant(factory(), card);
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
}