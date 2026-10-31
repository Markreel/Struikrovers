namespace Struikrovers.Core
{
    public enum TurnEventKind
    {
        Abducted,
        Delivered,
        Sniffed,
        Chopped,
        Looted,
        AttackInvalidTarget,
        AttackBlocked,
        AttackFoundNothing,
        AttackNoCaptives,
        AttackRescued,
        RescueRunStarted,
        SquireKilled,
        StabFoundNothing,
        StabKilled,
        StabKilledOwnBandit,
        StabFreedCaptives,
        BanditExposed,
        BanditFight,
        SquireFight,
        RescueRunBlocked
    }

    /// Something worth telling the players. Core never builds message text;
    /// the view decides how to word each event.
    public readonly struct TurnEvent
    {
        public TurnEventKind Kind { get; }
        public Pawn Actor { get; }
        public Pawn Target { get; }
        public int Amount { get; }
        public int SecondAmount { get; }
        public SniffClue Clue { get; }
        public FightResult Fight { get; }

        private TurnEvent(
            TurnEventKind kind,
            Pawn actor = null,
            Pawn target = null,
            int amount = 0,
            int secondAmount = 0,
            SniffClue clue = SniffClue.NoCaptive,
            FightResult fight = default)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Amount = amount;
            SecondAmount = secondAmount;
            Clue = clue;
            Fight = fight;
        }

        /// Actor = the Bandit, Target = the abducted Royalty.
        public static TurnEvent Abducted(Pawn royalty, Pawn bandit) =>
            new TurnEvent(TurnEventKind.Abducted, bandit, royalty);

        /// Amount = Royalty delivered, SecondAmount = wood stolen.
        public static TurnEvent Delivered(Pawn bandit, int delivered, int woodStolen) =>
            new TurnEvent(TurnEventKind.Delivered, bandit, null, delivered, woodStolen);

        public static TurnEvent Sniffed(Pawn bloodhound, SniffClue clue) =>
            new TurnEvent(TurnEventKind.Sniffed, bloodhound, clue: clue);

        public static TurnEvent Chopped(Pawn squire, int wood) =>
            new TurnEvent(TurnEventKind.Chopped, squire, amount: wood);

        public static TurnEvent Looted(Pawn looter, int cards) =>
            new TurnEvent(TurnEventKind.Looted, looter, amount: cards);

        public static TurnEvent AttackInvalidTarget(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackInvalidTarget, bloodhound);

        public static TurnEvent AttackBlocked(Pawn attacker, int blocked) =>
            new TurnEvent(TurnEventKind.AttackBlocked, attacker, amount: blocked);

        public static TurnEvent AttackFoundNothing(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackFoundNothing, bloodhound);

        public static TurnEvent AttackNoCaptives(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackNoCaptives, bloodhound);

        public static TurnEvent AttackRescued(Pawn bloodhound, int released) =>
            new TurnEvent(TurnEventKind.AttackRescued, bloodhound, amount: released);

        public static TurnEvent RescueRunStarted(Pawn royalty) =>
            new TurnEvent(TurnEventKind.RescueRunStarted, royalty);

        /// Actor = the Squire, Target = the Bandit, Amount = wood stolen.
        public static TurnEvent SquireKilled(Pawn squire, Pawn bandit, int woodStolen) =>
            new TurnEvent(TurnEventKind.SquireKilled, squire, bandit, woodStolen);

        public static TurnEvent StabFoundNothing(Pawn squire) =>
            new TurnEvent(TurnEventKind.StabFoundNothing, squire);

        /// Actor = the Squire, Target = the Bandit that died.
        public static TurnEvent StabKilled(Pawn squire, Pawn bandit) =>
            new TurnEvent(TurnEventKind.StabKilled, squire, bandit);

        public static TurnEvent StabKilledOwnBandit(Pawn squire) =>
            new TurnEvent(TurnEventKind.StabKilledOwnBandit, squire);

        public static TurnEvent StabFreedCaptives(Pawn squire, int released) =>
            new TurnEvent(TurnEventKind.StabFreedCaptives, squire, amount: released);

        /// Actor = the exposed Bandit.
        public static TurnEvent BanditExposed(Pawn bandit) =>
            new TurnEvent(TurnEventKind.BanditExposed, bandit);

        public static TurnEvent BanditFight(FightResult fight) =>
            new TurnEvent(TurnEventKind.BanditFight, fight.Attacker, fight.Defender, fight: fight);

        public static TurnEvent SquireFight(FightResult fight) =>
            new TurnEvent(TurnEventKind.SquireFight, fight.Attacker, fight.Defender, fight: fight);

        /// Actor = the freed Royalty that could not run and was placed on the nearest free tile.
        public static TurnEvent RescueRunBlocked(Pawn royalty) =>
            new TurnEvent(TurnEventKind.RescueRunBlocked, royalty);
    }
}