using System.Collections.Generic;

namespace Struikrovers.Core
{
    public sealed class TeamState
    {
        internal readonly List<CardDefinition> hand = new List<CardDefinition>();
        internal readonly List<CardDefinition> curses = new List<CardDefinition>();

        public int Index { get; }
        public int Wood { get; set; }

        /// Cards in hand (curse cards included).
        public int Cards => hand.Count;

        /// Private to the player. Cards are added and removed through CardSystem.
        public IReadOnlyList<CardDefinition> Hand => hand;

        /// Curses currently placed on this player (at most 2). Used from Phase F.
        public IReadOnlyList<CardDefinition> Curses => curses;

        public TeamState(int index) { Index = index; }
    }
}