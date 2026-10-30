namespace Struikrovers.Core
{
    public sealed class TeamState
    {
        public int Index { get; }
        public int Wood { get; set; }
        public int Cards { get; set; }

        public TeamState(int index) { Index = index; }
    }
}