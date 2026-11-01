using System;

namespace Struikrovers.Core
{
    /// Waits for the player to answer a DecisionPrompt. Nothing else can be done meanwhile.
    /// A choice can lead to another question. When the last one is answered, 'onDone' runs.
    public sealed class PromptState : TurnState
    {
        private readonly DecisionPrompt prompt;
        private readonly Action onDone;

        public PromptState(TurnManager turn, DecisionPrompt prompt, Action onDone) : base(turn)
        {
            this.prompt = prompt;
            this.onDone = onDone;
        }

        public override DecisionPrompt Prompt => prompt;

        public override void OnPromptChosen(int index)
        {
            if (index < 0 || index >= prompt.Options.Count)
                return;

            DecisionPrompt next = prompt.Choose(index);

            if (next != null)
                Turn.ChangeState(new PromptState(Turn, next, onDone));
            else
                onDone();
        }
    }
}