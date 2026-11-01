using System.Collections.Generic;

namespace Struikrovers.Core
{
    public sealed class PromptOption
    {
        public string Label { get; }
        /// The card this option is about, for the UI to show. Can be null.
        public CardDefinition Card { get; }

        public PromptOption(string label, CardDefinition card = null)
        {
            Label = label;
            Card = card;
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

        protected DecisionPrompt(int team, string title, string detail, List<PromptOption> options)
        {
            Team = team;
            Title = title;
            Detail = detail;
            Options = options;
        }

        /// Applies the choice. Returns a follow-up prompt, or null when the decision is complete.
        public abstract DecisionPrompt Choose(int index);
    }
}