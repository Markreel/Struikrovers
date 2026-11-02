using System;

namespace Struikrovers.Core
{
    /// Waits for the player to answer a DecisionPrompt. Nothing else can be done meanwhile.
    /// A choice can lead to another question. When the last one is answered, 'onDone' runs.
    /// With 'onCancel' the player can back out (Cancel button or Escape) before answering the last question.
    public sealed class PromptState : TurnState
    {
        private readonly DecisionPrompt prompt;
        private readonly Action onDone;
        private readonly Action onCancel;

        public PromptState(TurnManager turn, DecisionPrompt prompt, Action onDone, Action onCancel = null)
            : base(turn)
        {
            this.prompt = prompt;
            this.onDone = onDone;
            this.onCancel = onCancel;
        }

        public override DecisionPrompt Prompt => prompt;

        public override bool CanCancel => onCancel != null && prompt.AllowsCancel;

        public override void OnCancel()
        {
            if (CanCancel)
                onCancel();
        }

        public override void OnPromptChosen(int index)
        {
            if (index < 0 || index >= prompt.Options.Count)
                return;

            DecisionPrompt next = prompt.Choose(index);

            if (next != null)
                Turn.ChangeState(new PromptState(Turn, next, onDone, onCancel));
            else
                onDone();
        }
    }
}