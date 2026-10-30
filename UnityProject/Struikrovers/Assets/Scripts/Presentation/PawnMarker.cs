using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// What to draw for one pawn on a tile. A carried captive is drawn as a
    /// Royalty token with a CAPTIVE label.
    public readonly struct PawnMarker
    {
        public readonly int Team;
        public readonly PawnType Type;
        public readonly bool IsCaptive;

        public PawnMarker(int team, PawnType type, bool isCaptive)
        {
            Team = team;
            Type = type;
            IsCaptive = isCaptive;
        }
    }
}