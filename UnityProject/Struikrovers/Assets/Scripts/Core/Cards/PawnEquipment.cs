using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The two equipment slots of a pawn: one clothing, one weapon.
    public sealed class PawnEquipment
    {
        private CardDefinition clothing;
        private CardDefinition weapon;

        public CardDefinition Clothing => clothing;
        public CardDefinition Weapon => weapon;

        public CardDefinition Get(EquipmentSlot slot) =>
            slot == EquipmentSlot.Clothing ? clothing : weapon;

        internal void Set(EquipmentSlot slot, CardDefinition card)
        {
            if (slot == EquipmentSlot.Clothing) clothing = card;
            else weapon = card;
        }

        public IEnumerable<CardDefinition> Items
        {
            get
            {
                if (clothing != null) yield return clothing;
                if (weapon != null) yield return weapon;
            }
        }

        /// Sum of the +N modifiers. Combat starts using this in Phase B.
        public int CombatModifier
        {
            get
            {
                int total = 0;
                foreach (CardDefinition item in Items)
                    total += item.Modifier;
                return total;
            }
        }
    }
}