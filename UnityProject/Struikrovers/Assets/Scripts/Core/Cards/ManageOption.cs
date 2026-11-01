namespace Struikrovers.Core
{
    public enum ManageKind { MoveEquipment, SwapEquipment, DiscardEquipment }

    /// One thing a player can do with an item they have equipped.
    public readonly struct ManageOption
    {
        public readonly ManageKind Kind;
        /// The pawn the item goes to (Move and Swap only).
        public readonly Pawn Target;

        private ManageOption(ManageKind kind, Pawn target)
        {
            Kind = kind;
            Target = target;
        }

        public static ManageOption Move(Pawn target) => new ManageOption(ManageKind.MoveEquipment, target);
        public static ManageOption Swap(Pawn target) => new ManageOption(ManageKind.SwapEquipment, target);
        public static ManageOption DiscardEquipment() => new ManageOption(ManageKind.DiscardEquipment, null);
    }
}