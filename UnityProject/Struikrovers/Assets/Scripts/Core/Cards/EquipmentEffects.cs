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
    }

    /// The effect of each equipment card, by card id. Cards without an entry only have their
    /// +N modifier (Greatsword and Full Plate Armor are exactly that).
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
    }
}