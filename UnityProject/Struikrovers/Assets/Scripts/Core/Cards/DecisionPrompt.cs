using System.Collections.Generic;

namespace Struikrovers.Core
{
    public enum PromptTargetKind { None, HandCard, Equipment }

    /// Where an option lives on screen, so the UI can highlight it instead of listing a button.
    public readonly struct PromptTarget
    {
        public readonly PromptTargetKind Kind;
        public readonly int Team;
        public readonly int HandIndex;
        public readonly PawnType Role;
        public readonly EquipmentSlot Slot;
        /// A hand card that is normally face down is shown face up while choosing (Measuring Monocle).
        public readonly bool RevealFace;

        private PromptTarget(PromptTargetKind kind, int team, int handIndex,
                             PawnType role, EquipmentSlot slot, bool revealFace)
        {
            Kind = kind;
            Team = team;
            HandIndex = handIndex;
            Role = role;
            Slot = slot;
            RevealFace = revealFace;
        }

        public static PromptTarget HandCard(int team, int index, bool revealFace) =>
            new PromptTarget(PromptTargetKind.HandCard, team, index,
                             PawnType.Royalty, EquipmentSlot.Clothing, revealFace);

        public static PromptTarget Equipment(int team, PawnType role, EquipmentSlot slot) =>
            new PromptTarget(PromptTargetKind.Equipment, team, -1, role, slot, false);
    }

    public sealed class PromptOption
    {
        public string Label { get; }
        /// The card this option is about, for the UI to show. Can be null.
        public CardDefinition Card { get; }
        /// Where this option is on screen. Kind None = a plain text option.
        public PromptTarget Target { get; }

        public PromptOption(string label, CardDefinition card = null)
            : this(label, card, default(PromptTarget)) { }

        public PromptOption(string label, CardDefinition card, PromptTarget target)
        {
            Label = label;
            Card = card;
            Target = target;
        }
    }

    /// A decision a player has to make before the game continues, such as discarding to make
    /// room or choosing a pawn to equip. A choice can lead to a follow-up question.
    public abstract class DecisionPrompt
    {
        public int Team { get; }
        public string Title { get; }
        public string Detail { get; }
        public IReadOnlyList<PromptOption> Options { get; }

        /// True when every option points at something on screen. The UI then highlights
        /// those things instead of showing a button per option.
        public bool IsBoardSelection { get; }

        protected DecisionPrompt(int team, string title, string detail, List<PromptOption> options)
        {
            Team = team;
            Title = title;
            Detail = detail;
            Options = options;

            bool all = options.Count > 0;
            foreach (PromptOption option in options)
            {
                if (option.Target.Kind == PromptTargetKind.None)
                {
                    all = false;
                    break;
                }
            }

            IsBoardSelection = all;
        }

        /// Applies the choice. Returns a follow-up prompt, or null when the decision is complete.
        public abstract DecisionPrompt Choose(int index);
    }
}