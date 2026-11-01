using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// A click on a face-up card in a player panel: a card in hand or an equipped item.
    public readonly struct CardClick
    {
        public readonly int Team;
        public readonly CardDefinition Card;
        public readonly bool IsHand;
        public readonly int HandIndex;
        public readonly PawnType Role;
        public readonly EquipmentSlot Slot;

        private CardClick(int team, CardDefinition card, bool isHand, int handIndex, PawnType role, EquipmentSlot slot)
        {
            Team = team;
            Card = card;
            IsHand = isHand;
            HandIndex = handIndex;
            Role = role;
            Slot = slot;
        }

        public static CardClick Hand(int team, int index, CardDefinition card) =>
            new CardClick(team, card, true, index, PawnType.Royalty, EquipmentSlot.Clothing);

        public static CardClick Equipment(int team, PawnType role, EquipmentSlot slot, CardDefinition card) =>
            new CardClick(team, card, false, -1, role, slot);
    }
}