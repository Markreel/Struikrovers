using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// What to draw for one token on a tile: a pawn, a carried captive (Royalty token with a
    /// CAPTIVE label) or a Shrieking Chalice marker.
    public readonly struct PawnMarker
    {
        public readonly int Team;
        public readonly PawnType Type;
        public readonly bool IsCaptive;
        public readonly bool IsChalice;

        public PawnMarker(int team, PawnType type, bool isCaptive, bool isChalice = false)
        {
            Team = team;
            Type = type;
            IsCaptive = isCaptive;
            IsChalice = isChalice;
        }

        public static PawnMarker Chalice(int team) =>
            new PawnMarker(team, PawnType.Royalty, false, true);
    }
}