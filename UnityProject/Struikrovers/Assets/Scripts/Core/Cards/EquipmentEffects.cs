using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// What an equipment card does besides its +N combat modifier. Later phases add more hooks
    /// here (dice advantage, movement, interceptors). Most cards need no entry.
    public interface IEquipmentEffect
    {
        /// The item was put on a pawn. Typical use: pawn.Grant(action, card).
        void OnEquipped(Pawn pawn, CardDefinition card);

        /// The item is taken off (replaced, stolen, discarded). Typical use: pawn.RevokeAll(card).
        void OnUnequipped(Pawn pawn, CardDefinition card);
    }

    /// Base class with do-nothing defaults, so an effect only overrides what it needs.
    public abstract class EquipmentEffect : IEquipmentEffect
    {
        public virtual void OnEquipped(Pawn pawn, CardDefinition card) { }
        public virtual void OnUnequipped(Pawn pawn, CardDefinition card) { }
    }

    /// The effect of each equipment card, by card id. Cards without an entry only have their
    /// +N modifier (Greatsword and Full Plate Armor are exactly that).
    public static class EquipmentEffects
    {
        private static readonly Dictionary<string, IEquipmentEffect> effects =
            new Dictionary<string, IEquipmentEffect>();

        public static void Register(string cardId, IEquipmentEffect effect)
        {
            effects[cardId] = effect;
        }

        public static IEquipmentEffect Get(CardDefinition card) =>
            card != null && effects.TryGetValue(card.Id, out IEquipmentEffect effect) ? effect : null;
    }
}