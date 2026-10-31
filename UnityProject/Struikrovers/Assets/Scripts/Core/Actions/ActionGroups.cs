namespace Struikrovers.Core
{
    public static class ActionGroups
    {
        /// Chop a tree or loot a bush.
        public const string Gather = "Gather";
        public const string Stab = "Stab";

        /// No HUD button. Always active, and its tiles are highlighted.
        public const string Quick = "";

        public static bool IsQuick(string group) => string.IsNullOrEmpty(group);
    }

    public readonly struct ActionGroupInfo
    {
        public readonly string Id;
        public readonly string Label;

        public ActionGroupInfo(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }
}