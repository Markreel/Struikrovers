namespace Struikrovers.Presentation
{
    public enum FrameStat { None, TeamName, Wood, Cards }

    /// Which frame cell shows what. The frame is the ring of cells around the map.
    /// Top and bottom edges use wide labels, left and right edges use compact ones.
    public static class FrameLayout
    {
        /// The team an edge cell belongs to: 0 top, 1 right, 2 bottom, 3 left.
        public static int GetEdgeTeam(int x, int y, int visualColumns, int visualRows)
        {
            if (y == 0) return 0;
            if (x == visualColumns - 1) return 1;
            if (y == visualRows - 1) return 2;
            if (x == 0) return 3;
            return -1;
        }

        public static FrameStat GetStat(
            int x, int y, int visualColumns, int visualRows, out bool compact)
        {
            compact = false;

            if (GetEdgeTeam(x, y, visualColumns, visualRows) < 0)
                return FrameStat.None;

            if (y == 0 || y == visualRows - 1)
            {
                if (x == 0) return FrameStat.TeamName;
                if (x == 1) return FrameStat.Wood;
                if (x == visualColumns - 2) return FrameStat.Cards;
                return FrameStat.None;
            }

            compact = true;
            if (y == 1) return FrameStat.TeamName;
            if (y == 2) return FrameStat.Wood;
            if (y == visualRows - 2) return FrameStat.Cards;
            return FrameStat.None;
        }
    }
}