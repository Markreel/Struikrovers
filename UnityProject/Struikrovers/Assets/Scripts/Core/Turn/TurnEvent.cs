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
        RescueRunStarted
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

        private TurnEvent(
            TurnEventKind kind,
            Pawn actor = null,
            Pawn target = null,
            int amount = 0,
            int secondAmount = 0,
            SniffClue clue = SniffClue.NoCaptive)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Amount = amount;
            SecondAmount = secondAmount;
            Clue = clue;
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

        public static TurnEvent Looted(Pawn royalty, int cards) =>
            new TurnEvent(TurnEventKind.Looted, royalty, amount: cards);

        public static TurnEvent AttackInvalidTarget(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackInvalidTarget, bloodhound);

        public static TurnEvent AttackBlocked(Pawn bloodhound, int blocked) =>
            new TurnEvent(TurnEventKind.AttackBlocked, bloodhound, amount: blocked);

        public static TurnEvent AttackFoundNothing(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackFoundNothing, bloodhound);

        public static TurnEvent AttackNoCaptives(Pawn bloodhound) =>
            new TurnEvent(TurnEventKind.AttackNoCaptives, bloodhound);

        public static TurnEvent AttackRescued(Pawn bloodhound, int released) =>
            new TurnEvent(TurnEventKind.AttackRescued, bloodhound, amount: released);

        public static TurnEvent RescueRunStarted(Pawn royalty) =>
            new TurnEvent(TurnEventKind.RescueRunStarted, royalty);
    }
}