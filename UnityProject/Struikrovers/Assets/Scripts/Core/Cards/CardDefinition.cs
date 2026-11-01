namespace Struikrovers.Core
{
    public enum CardKind { Hand, Curse, Equipment, Dragon }

    public enum EquipmentSlot { Clothing, Weapon }

    /// One kind of card. Copies of a card in the deck share one definition.
    public sealed class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CardKind Kind { get; }
        public string Text { get; }

        // Equipment only.
        public EquipmentSlot Slot { get; }
        /// The pawn this item is made for. Null = general equipment, usable by every pawn.
        public PawnType? UsableBy { get; }
        public int Modifier { get; }

        public bool IsEquipment => Kind == CardKind.Equipment;

        private CardDefinition(
            string id, string name, CardKind kind, string text,
            EquipmentSlot slot = EquipmentSlot.Clothing, PawnType? usableBy = null, int modifier = 0)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Text = text;
            Slot = slot;
            UsableBy = usableBy;
            Modifier = modifier;
        }

        public static CardDefinition Hand(string id, string name, string text) =>
            new CardDefinition(id, name, CardKind.Hand, text);

        public static CardDefinition Curse(string id, string name, string text) =>
            new CardDefinition(id, name, CardKind.Curse, text);

        public static CardDefinition Equipment(
            string id, string name, EquipmentSlot slot, PawnType? usableBy, int modifier, string text) =>
            new CardDefinition(id, name, CardKind.Equipment, text, slot, usableBy, modifier);

        public static CardDefinition CreateDragon() =>
            new CardDefinition("dragon-awakes", "The Dragon Awakes", CardKind.Dragon,
                "The game moves into the dragon phase.");

        public override string ToString() => Name;
    }
}