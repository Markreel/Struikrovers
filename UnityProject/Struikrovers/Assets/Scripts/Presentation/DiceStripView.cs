using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// The dice of one team: the last roll made for or against it, animated.
    /// The dice are wired in pairs: [kept, discarded twin, kept, discarded twin].
    public sealed class DiceStripView : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private DieView[] dice;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private TMP_Text totalLabel;

        private VisualTheme theme;
        private DiceRoll shown;

        public bool IsAnimating
        {
            get
            {
                if (dice == null)
                    return false;

                foreach (DieView die in dice)
                    if (die != null && die.IsRolling)
                        return true;

                return false;
            }
        }

        public void Initialize(VisualTheme theme)
        {
            this.theme = theme;

            if (dice != null)
                foreach (DieView die in dice)
                    if (die != null)
                        die.Setup(theme);

            Clear();
        }

        public void Clear()
        {
            shown = null;

            if (content != null)
                content.SetActive(false);
        }

        // The total is only shown once the dice have landed.
        private void Update()
        {
            if (shown != null && totalLabel != null)
                totalLabel.gameObject.SetActive(!IsAnimating);
        }

        /// Shows a roll. 'modifier' is what is added to the dice (equipment and so on).
        /// The same roll shown again only refreshes the text, so nothing re-animates by accident.
        public void ShowRoll(DiceRoll roll, string caption, int modifier)
        {
            if (roll == null || theme == null || dice == null)
            {
                Clear();
                return;
            }

            content.SetActive(true);

            infoLabel.text = BuildInfo(roll, caption);
            totalLabel.text = modifier == 0
                ? roll.Total.ToString()
                : $"{roll.Total} {modifier:+0;-0} = {roll.Total + modifier}";

            if (ReferenceEquals(roll, shown))
                return;

            int firstAnimated = FirstAnimatedDie(roll);
            shown = roll;

            for (int i = 0; i < dice.Length / 2; i++)
            {
                DieView kept = dice[2 * i];
                DieView twin = dice[2 * i + 1];

                if (i >= roll.Dice.Count)
                {
                    kept.gameObject.SetActive(false);
                    twin.gameObject.SetActive(false);
                    continue;
                }

                DieResult die = roll.Dice[i];
                Color tint = theme.GetDieColor(roll, i);
                bool animate = i >= firstAnimated;

                kept.gameObject.SetActive(true);
                Present(kept, die.Kept, tint, false, animate);

                twin.gameObject.SetActive(die.HasOther);
                if (die.HasOther)
                    Present(twin, die.Other, tint, true, animate);
            }

            totalLabel.gameObject.SetActive(!IsAnimating);
        }

        // Double Roll adds a die to the roll that is already there: only the new die rolls.
        // Everything else (a new roll, a Reroll, a tie reroll) rolls all dice.
        private int FirstAnimatedDie(DiceRoll roll)
        {
            DiceRoll old = shown;

            if (old == null || old.Pawn != roll.Pawn || old.Kind != roll.Kind)
                return 0;

            if (roll.Doubled && !old.Doubled && roll.Rerolled == old.Rerolled &&
                old.Dice.Count == 1 && roll.Dice.Count == 2)
            {
                return 1;
            }

            return 0;
        }

        private static void Present(DieView die, int value, Color tint, bool discarded, bool animate)
        {
            if (animate) die.Roll(value, tint, discarded);
            else die.Show(value, tint, discarded);
        }

        private static string BuildInfo(DiceRoll roll, string caption)
        {
            var tags = new List<string>();

            if (roll.Mode == RollMode.Advantage) tags.Add("Advantage");
            else if (roll.Mode == RollMode.Disadvantage) tags.Add("Disadvantage");

            if (roll.Doubled) tags.Add("Double Roll");
            if (roll.Rerolled) tags.Add("Rerolled");

            return tags.Count == 0 ? caption : caption + "\n" + string.Join(" · ", tags);
        }
    }
}