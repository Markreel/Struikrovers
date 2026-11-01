namespace Struikrovers.Core
{
    public enum TurnEventKind
    {
        Abducted,
        Delivered,
        Sniffed,
        Chopped,
        Looted,
        CannotLootPlentiful,
        AttackInvalidTarget,
        AttackBlocked,
        AttackFoundNothing,
        AttackNoCaptives,
        AttackRescued,
        RescueRunStarted,
        RescueRunBlocked,
        SquireKilled,
        StabFoundNothing,
        StabKilled,
        StabKilledOwnBandit,
        StabFreedCaptives,
        BanditExposed,
        BanditFight,
        SquireFight,
        CardDrawn,
        CardDiscarded,
        CardEquipped,
        DragonAwakes,
        DeckEmpty
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
        /// The team this is about (card events). -1 when not used.
        public int Team { get; }
        public CardDefinition Card { get; }

        private TurnEvent(
            TurnEventKind kind,
            Pawn actor = null,
            Pawn target = null,
            int amount = 0,
            int secondAmount = 0,
            SniffClue clue = SniffClue.NoCaptive,
            FightResult fight = default,
            int team = -1,
            CardDefinition card = null)
        {
            Kind = kind;
            Actor = actor;
            Target = target;
            Amount = amount;
            SecondAmount = secondAmount;
            Clue = clue;
            Fight = fight;
            Team = team;
            Card = card;
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

        /// Amount = cards looted.
        public static TurnEvent Looted(Pawn looter, int cards) =>
            new TurnEvent(TurnEventKind.Looted, looter, amount: cards);

        public static TurnEvent CannotLootPlentiful(Pawn looter) =>
            new TurnEvent(TurnEventKind.CannotLootPlentiful, looter);

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

        /// Actor = the freed Royalty that could not run and was placed on the nearest free tile.
        public static TurnEvent RescueRunBlocked(Pawn royalty) =>
            new TurnEvent(TurnEventKind.RescueRunBlocked, royalty);

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

        // ---- Cards ----

        /// The card itself stays private, so only the team is given.
        public static TurnEvent CardDrawn(int team) =>
            new TurnEvent(TurnEventKind.CardDrawn, team: team);

        /// Discarded cards are face up, so everyone sees which card it was.
        public static TurnEvent CardDiscarded(int team, CardDefinition card) =>
            new TurnEvent(TurnEventKind.CardDiscarded, team: team, card: card);

        /// Actor = the pawn that got the item.
        public static TurnEvent CardEquipped(Pawn pawn, CardDefinition card) =>
            new TurnEvent(TurnEventKind.CardEquipped, pawn, team: pawn.Team, card: card);

        public static TurnEvent DragonAwakes(int team) =>
            new TurnEvent(TurnEventKind.DragonAwakes, team: team);

        public static TurnEvent DeckEmpty(int team) =>
            new TurnEvent(TurnEventKind.DeckEmpty, team: team);
    }
}