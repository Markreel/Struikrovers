using System.Collections.Generic;

namespace Struikrovers.Core
{
    /// The actions each pawn type starts with.
    public static class ActionCatalog
    {
        /// Source tag of the built-in actions, so equipment can't revoke them by accident.
        public static readonly object BaseSource = new object();

        public static IEnumerable<IPawnAction> GetDefaults(PawnType type)
        {
            switch (type)
            {
                case PawnType.Royalty:
                    yield return new LootAction(fullPlentifulYield: true);
                    break;

                case PawnType.Squire:
                    yield return new ChopAction();
                    yield return new LootAction(fullPlentifulYield: false);
                    yield return new StabAction();
                    yield return new FightSquireAction();
                    break;

                case PawnType.Bandit:
                    yield return new AbductAction();
                    yield return new FightSquireAction();
                    break;
            }
        }
    }
}