namespace Struikrovers.Core
{
    /// +1 card whenever the wearer loots a bush.
    internal sealed class GreedyGlovesEffect : EquipmentEffect
    {
        public override int ModifyLootCount(Pawn looter, CardDefinition card, int count) =>
            count + GameConfig.GreedyGlovesBonusCards;
    }

    /// A Royalty that loots a bush can't be abducted by a Bandit hiding in it.
    internal sealed class CharmingChestplateEffect : EquipmentEffect
    {
        public override bool PreventsAmbush(Pawn victim, Pawn bandit, CardDefinition card) =>
            victim.Type == PawnType.Royalty;
    }

    /// The wearer's team can't be cursed. A Bloodhound ignores equipment effects.
    internal sealed class WardingAmuletEffect : EquipmentEffect
    {
        public override bool PreventsCurse(Pawn wearer, CardDefinition card) =>
            wearer.Type == wearer.BaseType;
    }

    /// Cards the wearer loots can come from the top of the discard pile (see LootSourcePrompt).
    internal sealed class GravediggersSpadeEffect : EquipmentEffect
    {
        public override bool OffersDiscardPick(Pawn looter, CardDefinition card) => true;
    }

    /// Advantage on one kind of roll: Lucky Loafers (movement), Short Swords and Bush Blade (combat).
    internal sealed class RollAdvantageEffect : EquipmentEffect
    {
        private readonly RollKind kind;

        public RollAdvantageEffect(RollKind kind) { this.kind = kind; }

        public override bool GrantsAdvantage(Pawn wearer, CardDefinition card, RollKind rollKind) =>
            rollKind == kind;
    }
}