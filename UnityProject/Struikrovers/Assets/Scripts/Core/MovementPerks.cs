using System;

namespace Struikrovers.Core
{
    /// Movement rules an equipment card can switch on for its wearer.
    [Flags]
    public enum MovementPerks
    {
        None = 0,
        SkippingShoes = 1,    // every 2nd step in a row in the same direction is free
        TumblingTunic = 2,    // moving inside a cluster of bushes/trees/stumps costs 1 in total
        BushDashers = 4,      // jump bush to bush over 1 grass tile or stump
        StumpStompers = 8,    // jump over an adjacent stump
        SlantedSlippers = 16  // diagonal steps
    }
}