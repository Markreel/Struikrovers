namespace Struikrovers.Core
{
    /// Every tweakable rule number in one place.
    public static class GameConfig
    {
        public const int TeamCount = 4;

        /// The custom "D6" from the GDD: 3-4-4-5-5-6. Also used for the rescue run.
        public static readonly int[] MovementDie = { 3, 4, 4, 5, 5, 6 };

        /// Order in which a team activates its pawns.
        /// The Royalty slot is taken by the Bloodhound while the Royalty is abducted.
        public static readonly PawnType[] TurnOrder =
            { PawnType.Royalty, PawnType.Squire, PawnType.Bandit };

        public const int BanditMoveSteps = 5;
        public const int BloodhoundMoveSteps = 5;
        public const int BloodhoundMovesPerActivation = 2;
        public const int BloodhoundAttackRange = 5;

        /// The data model supports several captives. The rule is limited to this number.
        public const int MaxCaptivesPerBandit = 1;

        public const int WoodStolenOnDelivery = 2;

        /// Wood from a tree / cards from a bush. Plentiful = large tree / loot bush.
        public const int YieldNormal = 1;
        public const int YieldPlentiful = 2;
    }
}