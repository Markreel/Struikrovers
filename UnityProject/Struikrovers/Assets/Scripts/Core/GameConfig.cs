namespace Struikrovers.Core
{
    /// Every tweakable rule number in one place.
    public static class GameConfig
    {
        public const int TeamCount = 4;

        /// The custom "D6" from the GDD: 3-4-4-5-5-6. Used for movement and fights.
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

        // ---- Killing and fighting ----

        /// A Bandit that kills a Squire (chop or loot on its hiding place) takes this much wood.
        public const int WoodStolenOnSquireKill = 1;

        /// A Squire that wins a fight against another Squire takes this much wood.
        public const int WoodStolenOnSquireFight = 1;

        /// Added to the roll of the Squire that starts a Squire fight.
        public const int SquireAttackerBonus = 1;

        /// Added to the roll of a Bandit that starts a fight against another Bandit. 0 = no bonus.
        public const int BanditAttackerBonus = 0;

        /// Added to the roll of a Bandit that starts a fight against a Squire.
        public const int BanditAttackerBonusVsSquire = 1;

        /// Added (so use a negative number) to the roll of a Bandit that carries a captive.
        public const int BanditAbductingModifier = -1;

        /// Safety net so a forced die can never loop forever. After this many ties the defender wins.
        public const int MaxFightRerolls = 100;
    }
}