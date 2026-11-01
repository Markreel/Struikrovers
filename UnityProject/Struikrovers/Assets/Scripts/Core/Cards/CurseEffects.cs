namespace Struikrovers.Core
{
    /// Questions about curses that other systems ask. Phase F adds the rest.
    public static class CurseEffects
    {
        /// Curse of the Drunk Royalty / Squire: the pawn rolls movement and combat with disadvantage.
        public static bool RollsWithDisadvantage(GameState state, Pawn pawn)
        {
            // A Bloodhound ignores effects. A freed Royalty is a Royalty again.
            if (pawn == null || pawn.Type != pawn.BaseType)
                return false;

            foreach (CardDefinition curse in state.Teams[pawn.Team].Curses)
            {
                if (curse.Id == "curse-drunk-royalty" && pawn.BaseType == PawnType.Royalty) return true;
                if (curse.Id == "curse-drunk-squire" && pawn.BaseType == PawnType.Squire) return true;
            }

            return false;
        }
    }
}