using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// A scrollable public log of what happened. It keeps the last 'maxLines' lines.
    public sealed class EventLogView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private int maxLines = 200;

        private readonly List<string> lines = new List<string>();
        private bool scrollPending;

        public void Clear()
        {
            lines.Clear();
            Rebuild();
        }

        public void Add(string line)
        {
            if (string.IsNullOrEmpty(line))
                return;

            lines.Add(line);
            Trim();
            Rebuild();
        }

        /// A bold line in the team's colour that marks the start of a turn.
        public void AddDivider(string text, Color color)
        {
            Add($"<b><color=#{ColorUtility.ToHtmlStringRGB(color)}>— {text} —</color></b>");
        }

        private void Trim()
        {
            int extra = lines.Count - Mathf.Max(1, maxLines);
            if (extra > 0)
                lines.RemoveRange(0, extra);
        }

        private void Rebuild()
        {
            logText.text = string.Join("\n", lines);

            if (!scrollPending && isActiveAndEnabled)
            {
                scrollPending = true;
                StartCoroutine(ScrollToBottom());
            }
        }

        // The text height is only updated after a frame.
        private IEnumerator ScrollToBottom()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
            scrollPending = false;
        }
    }
}