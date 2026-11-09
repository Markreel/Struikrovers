using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// What an equipment card does besides its +N combat modifier. Most cards need no entry.
    public interface IEquipmentEffect
    {
        /// The item was put on a pawn. Typical use: pawn.Grant(action, card).
        void OnEquipped(Pawn pawn, CardDefinition card);

        /// The item is taken off (replaced, stolen, discarded). Typical use: pawn.RevokeAll(card).
        void OnUnequipped(Pawn pawn, CardDefinition card);

        /// The wearer loots a bush: returns the number of cards, changed by this item.
        int ModifyLootCount(Pawn looter, CardDefinition card, int count);

        /// A hidden enemy Bandit would ambush the victim: true when this item stops it.
        bool PreventsAmbush(Pawn victim, Pawn bandit, CardDefinition card);

        /// True when this item stops the wearer's team from being cursed.
        bool PreventsCurse(Pawn wearer, CardDefinition card);

        /// True when the wearer may take looted cards from the top of the discard pile.
        bool OffersDiscardPick(Pawn looter, CardDefinition card);

        /// True when this item gives the wearer advantage on this kind of roll.
        bool GrantsAdvantage(Pawn wearer, CardDefinition card, RollKind kind);

        /// True when this item lets the wearer's team look at a hand before stealing a card from it.
        bool RevealsStolenHand(Pawn wearer, CardDefinition card);

        /// The movement rules this item gives the wearer.
        MovementPerks GrantsMovement(Pawn wearer, CardDefinition card);

        bool StabsAllAdjacent(Pawn wearer, CardDefinition card);
        bool KillsBanditOnChop(Pawn wearer, CardDefinition card);
        bool GrantsExtraStep(Pawn wearer, CardDefinition card);
        bool AllowsCastleStay(Pawn wearer, CardDefinition card);
        bool MarksAbductionSpot(Pawn wearer, CardDefinition card);
    }

    /// Base class with do-nothing defaults, so an effect only overrides what it needs.
    public abstract class EquipmentEffect : IEquipmentEffect
    {
        public virtual void OnEquipped(Pawn pawn, CardDefinition card) { }
        public virtual void OnUnequipped(Pawn pawn, CardDefinition card) { }
        public virtual int ModifyLootCount(Pawn looter, CardDefinition card, int count) => count;
        public virtual bool PreventsAmbush(Pawn victim, Pawn bandit, CardDefinition card) => false;
        public virtual bool PreventsCurse(Pawn wearer, CardDefinition card) => false;
        public virtual bool OffersDiscardPick(Pawn looter, CardDefinition card) => false;
        public virtual bool GrantsAdvantage(Pawn wearer, CardDefinition card, RollKind kind) => false;
        public virtual bool RevealsStolenHand(Pawn wearer, CardDefinition card) => false;
        public virtual MovementPerks GrantsMovement(Pawn wearer, CardDefinition card) => MovementPerks.None;
        public virtual bool StabsAllAdjacent(Pawn wearer, CardDefinition card) => false;
        public virtual bool KillsBanditOnChop(Pawn wearer, CardDefinition card) => false;
        public virtual bool GrantsExtraStep(Pawn wearer, CardDefinition card) => false;
        public virtual bool AllowsCastleStay(Pawn wearer, CardDefinition card) => false;
        public virtual bool MarksAbductionSpot(Pawn wearer, CardDefinition card) => false;
    }

    /// The effect of each equipment card, by card id. Cards without an entry only have their
    /// +N modifier (Greatsword and Full Plate Armor are exactly that). Martyr Shield and
    /// Chainmail are handled by CardSystem, where theft happens.
    public static class EquipmentEffects
    {
        private static readonly Dictionary<string, IEquipmentEffect> effects =
            new Dictionary<string, IEquipmentEffect>();

        static EquipmentEffects()
        {
            Register("greedy-gloves", new GreedyGlovesEffect());
            Register("charming-chestplate", new CharmingChestplateEffect());
            Register("warding-amulet", new WardingAmuletEffect());
            Register("gravediggers-spade", new GravediggersSpadeEffect());
            Register("measuring-monocle", new MeasuringMonocleEffect());
            Register("lucky-loafers", new RollAdvantageEffect(RollKind.Movement));
            Register("short-swords", new RollAdvantageEffect(RollKind.Combat));
            Register("bush-blade", new RollAdvantageEffect(RollKind.Combat));

            //Movement
            Register("skipping-shoes", new MovementPerkEffect(MovementPerks.SkippingShoes));
            Register("tumbling-tunic", new MovementPerkEffect(MovementPerks.TumblingTunic));
            Register("bush-dashers", new MovementPerkEffect(MovementPerks.BushDashers));
            Register("stump-stompers", new MovementPerkEffect(MovementPerks.StumpStompers));
            Register("slanted-slippers", new MovementPerkEffect(MovementPerks.SlantedSlippers));
            Register("thorny-trunk", new MovementPerkEffect(MovementPerks.ThornyTrunk));
            Register("tree-swingers", new MovementPerkEffect(MovementPerks.TreeSwingers));
            Register("bolting-boots", new MovementPerkEffect(MovementPerks.BoltingBoots));

            //Actions
            Register("royal-blade", new GrantActionEffect(() => new StabAction()));
            Register("tomahawk", new GrantActionEffect(() => new TomahawkAction()));
            Register("halberd", new HalberdEffect());
            Register("battleaxe", new BattleaxeEffect());
            Register("prowling-pincers", new GrantActionEffect(() => new PincersAbductAction()));
            Register("flaying-fishpole", new GrantActionEffect(
                () => new FishpoleLootAction(), pawn => pawn.BaseType != PawnType.Bandit));
            Register("nimble-cloak", new NimbleCloakEffect());
            Register("keys-to-the-castle", new KeysToTheCastleEffect());
            Register("shrieking-chalice", new ShriekingChaliceEffect());
        }

        public static void Register(string cardId, IEquipmentEffect effect)
        {
            effects[cardId] = effect;
        }

        public static IEquipmentEffect Get(CardDefinition card) =>
            card != null && effects.TryGetValue(card.Id, out IEquipmentEffect effect) ? effect : null;

        // ---- Queries over everything a pawn or team wears ----

        public static int ModifyLootCount(Pawn looter, int count)
        {
            if (looter == null)
                return count;

            foreach (CardDefinition item in looter.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null)
                    count = effect.ModifyLootCount(looter, item, count);
            }

            return count;
        }

        public static bool PreventsAmbush(Pawn victim, Pawn bandit, out CardDefinition source)
        {
            source = null;
            if (victim == null)
                return false;

            foreach (CardDefinition item in victim.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null && effect.PreventsAmbush(victim, bandit, item))
                {
                    source = item;
                    return true;
                }
            }

            return false;
        }

        /// Looks at every pawn of the team: the amulet protects the player, not one pawn.
        public static bool PreventsCurse(GameState state, int team, out CardDefinition source)
        {
            source = null;

            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Team != team)
                    continue;

                foreach (CardDefinition item in pawn.Equipment.Items)
                {
                    IEquipmentEffect effect = Get(item);
                    if (effect != null && effect.PreventsCurse(pawn, item))
                    {
                        source = item;
                        return true;
                    }
                }
            }

            return false;
        }

        /// Looks at every pawn of the team: the Monocle belongs to the player, not one pawn.
        public static bool RevealsStolenHand(GameState state, int team)
        {
            foreach (Pawn pawn in state.Pawns)
            {
                if (pawn.Team != team)
                    continue;

                foreach (CardDefinition item in pawn.Equipment.Items)
                {
                    IEquipmentEffect effect = Get(item);
                    if (effect != null && effect.RevealsStolenHand(pawn, item))
                        return true;
                }
            }

            return false;
        }

        public static bool OffersDiscardPick(Pawn looter)
        {
            if (looter == null)
                return false;

            foreach (CardDefinition item in looter.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null && effect.OffersDiscardPick(looter, item))
                    return true;
            }

            return false;
        }

        public static bool GrantsAdvantage(Pawn pawn, RollKind kind)
        {
            // A Bloodhound ignores equipment effects.
            if (pawn == null || pawn.Type != pawn.BaseType)
                return false;

            foreach (CardDefinition item in pawn.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null && effect.GrantsAdvantage(pawn, item, kind))
                    return true;
            }

            return false;
        }

        /// Everything a pawn's items change about its movement. A Bloodhound ignores equipment.
        public static MovementPerks GetMovementPerks(Pawn pawn)
        {
            if (pawn == null || pawn.Type != pawn.BaseType)
                return MovementPerks.None;

            // A Bandit carrying a captive can't use movement equipment.
            if (pawn.Type == PawnType.Bandit && pawn.IsCarrying)
                return MovementPerks.None;

            MovementPerks perks = MovementPerks.None;

            foreach (CardDefinition item in pawn.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null)
                    perks |= effect.GrantsMovement(pawn, item);
            }

            return perks;
        }

        public static bool StabsAllAdjacent(Pawn pawn) => Any(pawn, (e, i) => e.StabsAllAdjacent(pawn, i));
        public static bool GrantsExtraStep(Pawn pawn) => Any(pawn, (e, c) => e.GrantsExtraStep(pawn, c));
        public static bool AllowsCastleStay(Pawn pawn) => Any(pawn, (e, c) => e.AllowsCastleStay(pawn, c));
        public static bool MarksAbductionSpot(Pawn pawn) => Any(pawn, (e, c) => e.MarksAbductionSpot(pawn, c));
        public static bool KillsBanditOnChop(Pawn pawn) => Any(pawn, (e, i) => e.KillsBanditOnChop(pawn, i));

        private static bool Any(Pawn pawn, System.Func<IEquipmentEffect, CardDefinition, bool> test)
        {
            if (pawn == null || pawn.Type != pawn.BaseType) return false; // Bloodhound ignores equipment
            foreach (CardDefinition item in pawn.Equipment.Items)
            {
                IEquipmentEffect effect = Get(item);
                if (effect != null && test(effect, item)) return true;
            }
            return false;
        }
    }
}