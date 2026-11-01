using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// Every card in the game, from the GDD. Effects are added in later phases;
    /// this holds names, texts and the numbers (slot, modifier, who can use it).
    public static class CardCatalog
    {
        public static readonly CardDefinition Dragon = CardDefinition.CreateDragon();

        private static readonly List<CardDefinition> all = Build();
        private static readonly Dictionary<string, CardDefinition> byId = Index(all);

        public static IReadOnlyList<CardDefinition> All => all;

        public static CardDefinition Find(string id) =>
            id != null && byId.TryGetValue(id, out CardDefinition card) ? card : null;

        private static Dictionary<string, CardDefinition> Index(List<CardDefinition> cards)
        {
            var map = new Dictionary<string, CardDefinition>();
            foreach (CardDefinition card in cards)
                map[card.Id] = card;
            return map;
        }

        private static List<CardDefinition> Build()
        {
            const EquipmentSlot C = EquipmentSlot.Clothing;
            const EquipmentSlot W = EquipmentSlot.Weapon;
            PawnType? royalty = PawnType.Royalty;
            PawnType? squire = PawnType.Squire;
            PawnType? bandit = PawnType.Bandit;
            PawnType? any = null;

            var l = new List<CardDefinition>();

            // ---- Hand cards ----
            l.Add(CardDefinition.Hand("curse-reverse", "Curse Reverse", "Move a curse from one player to another. Cost: discard 1 other card."));
            l.Add(CardDefinition.Hand("peek", "Peek", "Choose a player to watch them move their Bandit on their next turn."));
            l.Add(CardDefinition.Hand("hand-over", "Hand Over", "Swap hand cards with an opponent. Condition: hold at least 1 other hand card."));
            l.Add(CardDefinition.Hand("switcheroo", "Switcheroo", "Swap an opponent pawn's equipment with your pawn of the same type. Both need at least 1 item."));
            l.Add(CardDefinition.Hand("nab", "Nab", "Steal an opponent's equipped card of choice."));
            l.Add(CardDefinition.Hand("yoink", "Yoink", "Blindly steal a hand card from an opponent of your choice."));
            l.Add(CardDefinition.Hand("double-roll", "Double Roll", "After rolling a die in the roving phase, roll a second die and add both."));
            l.Add(CardDefinition.Hand("reroll", "Reroll", "Reroll any die after having rolled it."));
            l.Add(CardDefinition.Hand("ones-mans-trash", "One Man's Trash", "Pick any card from the draw pile."));

            // ---- Curses ----
            l.Add(CardDefinition.Curse("curse-possessed-royalty", "Curse of the Possessed Royalty", "At the end of their turn, the caster may move the target's Royalty up to 2 steps. Remove: discard 2 hand cards or 1 equipment."));
            l.Add(CardDefinition.Curse("curse-possessed-squire", "Curse of the Possessed Squire", "At the end of their turn, the caster may move the target's Squire up to 2 steps. Remove: discard 2 hand cards or 1 equipment."));
            l.Add(CardDefinition.Curse("curse-drunk-royalty", "Curse of the Drunk Royalty", "Royalty rolls movement and combat with disadvantage (ignores equipment advantage). Remove: discard 2 hand cards or 1 equipment."));
            l.Add(CardDefinition.Curse("curse-drunk-squire", "Curse of the Drunk Squire", "Squire rolls movement and combat with disadvantage (ignores equipment advantage). Remove: discard 2 hand cards or 1 equipment."));
            l.Add(CardDefinition.Curse("curse-germophobic-squire", "Curse of the Germophobic Squire", "The Squire can no longer loot bushes. Remove: discard 2 hand cards or 1 equipment."));
            l.Add(CardDefinition.Curse("curse-naked-royalty", "Curse of the Naked Royalty", "The Royalty cannot equip clothing; worn clothing is discarded. Remove: discard 4 hand cards or 1 weapon."));
            l.Add(CardDefinition.Curse("curse-naked-squire", "Curse of the Naked Squire", "The Squire cannot equip clothing; worn clothing is discarded. Remove: discard 4 hand cards or 1 weapon."));
            l.Add(CardDefinition.Curse("curse-naked-bandit", "Curse of the Naked Bandit", "The Bandit cannot equip clothing; worn clothing is discarded. Remove: discard 4 hand cards or 1 weapon."));
            l.Add(CardDefinition.Curse("curse-frog", "Curse of the Frog", "The Royalty can only move, no actions. Remove: stand next to any Squire, discard 4 hand cards, or discard 2 equipment."));

            // ---- Royalty equipment ----
            l.Add(CardDefinition.Equipment("charming-chestplate", "Charming Chestplate", C, royalty, 0, "Safe from being abducted by a Bandit when looting a bush."));
            l.Add(CardDefinition.Equipment("royal-blade", "Royal Blade", W, royalty, 0, "Gain the ability to stab instead of loot."));
            l.Add(CardDefinition.Equipment("keys-to-the-castle", "Keys to the Castle", W, royalty, 0, "May end the turn safely inside the castle if in reach. Bandits cannot abduct you there."));
            l.Add(CardDefinition.Equipment("skipping-shoes", "Skipping Shoes", C, royalty, 0, "Every 2nd straight step doesn't cost a step."));
            l.Add(CardDefinition.Equipment("shrieking-chalice", "Shrieking Chalice", W, royalty, 0, "When abducted, reveal your previous position on your turn."));
            l.Add(CardDefinition.Equipment("nimble-cloak", "Nimble Cloak", C, royalty, 0, "After your action, you may take 1 extra step."));

            // ---- Squire equipment ----
            l.Add(CardDefinition.Equipment("tomahawk", "Tomahawk", W, squire, 1, "Throw it to stab a tree or bush within 3 steps in a straight, unobstructed line."));
            l.Add(CardDefinition.Equipment("halberd", "Halberd", W, squire, 2, "When stabbing, stab all bushes and trees adjacent to you."));
            l.Add(CardDefinition.Equipment("bolting-boots", "Bolting Boots", C, squire, 1, "Take 1 step back to shoot forward in the opposite direction until you hit terrain or an enemy Squire."));
            l.Add(CardDefinition.Equipment("battleaxe", "Battleaxe", W, squire, 2, "If a tree you chop has a Bandit in it, you immediately kill them."));
            l.Add(CardDefinition.Equipment("short-swords", "Short Swords", W, squire, 1, "In combat, roll with advantage."));
            l.Add(CardDefinition.Equipment("lucky-loafers", "Lucky Loafers", C, squire, 1, "Roll with advantage for movement."));

            // ---- Bandit equipment ----
            l.Add(CardDefinition.Equipment("thorny-trunk", "Thorny Trunk", W, bandit, 2, "May end the turn on top of a tree trunk (not hidden, can still be stabbed)."));
            l.Add(CardDefinition.Equipment("tumbling-tunic", "Tumbling Tunic", C, bandit, 2, "Moving between bushes and trees connected by other bushes, trees or stumps costs 1 step."));
            l.Add(CardDefinition.Equipment("bush-dashers", "Bush Dashers", C, bandit, 1, "Jump in a straight line from bush to bush over an empty space or stump for 1 step."));
            l.Add(CardDefinition.Equipment("tree-swingers", "Tree Swingers", C, bandit, 1, "When moving out of an unchopped tree, you may take 1 extra free step."));
            l.Add(CardDefinition.Equipment("prowling-pincers", "Prowling Pincers", W, bandit, 2, "As an action, abduct a Royalty from 1 tile extra distance."));
            l.Add(CardDefinition.Equipment("bush-blade", "Bush Blade", W, bandit, 1, "In combat, roll with advantage."));

            // ---- General equipment ----
            l.Add(CardDefinition.Equipment("martyr-shield", "Martyr Shield", W, any, 3, "When you must discard or give away equipment from any of your pawns, this is taken instead. (Not against Curse of the Naked.)"));
            l.Add(CardDefinition.Equipment("slanted-slippers", "Slanted Slippers", C, any, 1, "May move diagonally, costing 1 step per diagonal move."));
            l.Add(CardDefinition.Equipment("flaying-fishpole", "Flaying Fishpole", W, any, 1, "May loot a bush 2 tiles away, through terrain and pawns. A hidden Bandit still gets you. Bandits can't use it."));
            l.Add(CardDefinition.Equipment("measuring-monocle", "Measuring Monocle", C, any, 1, "When stealing from someone's hand, look at the cards before choosing."));
            l.Add(CardDefinition.Equipment("stump-stompers", "Stump Stompers", C, any, 2, "Next to a tree stump, move over it to the other side in 1 step, if you may stand there."));
            l.Add(CardDefinition.Equipment("gravediggers-spade", "Gravedigger's Spade", W, any, 1, "When drawing, you may draw from the top of the discard pile instead."));
            l.Add(CardDefinition.Equipment("royal-inheritance", "Royal Inheritance", W, any, 1, "When killing a pawn, take all their equipment."));
            l.Add(CardDefinition.Equipment("greedy-gloves", "Greedy Gloves", C, any, 1, "Take 1 extra card when looting bushes."));
            l.Add(CardDefinition.Equipment("greatsword", "Greatsword", W, any, 3, "No effect."));
            l.Add(CardDefinition.Equipment("full-plate-armor", "Full Plate Armor", C, any, 3, "No effect."));
            l.Add(CardDefinition.Equipment("warding-amulet", "Warding Amulet", C, any, -1, "Prevents the player from being cursed."));

            l.Add(Dragon);
            return l;
        }
    }
}