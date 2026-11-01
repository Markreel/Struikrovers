using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// A panel that asks the player a DecisionPrompt, with one button per option.
    /// It only shows what it is given and raises an event for the chosen option.
    public sealed class PromptView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button optionButtonPrefab;
        [SerializeField] private RectTransform optionContainer;

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private DecisionPrompt shown;

        public event Action<int> OptionChosen;

        private void Awake()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        public void Show(DecisionPrompt prompt)
        {
            // Refreshes happen on every change; only rebuild when the question is new.
            if (prompt == shown)
                return;

            shown = prompt;
            panel.SetActive(true);

            titleText.text = prompt.Title;
            detailText.text = prompt.Detail ?? "";

            int count = prompt.Options.Count;
            while (buttons.Count < count)
                CreateButton();

            for (int i = 0; i < buttons.Count; i++)
            {
                bool used = i < count;
                buttons[i].gameObject.SetActive(used);

                if (used)
                    labels[i].text = prompt.Options[i].Label;
            }
        }

        public void Hide()
        {
            shown = null;

            if (panel != null)
                panel.SetActive(false);
        }

        private void CreateButton()
        {
            Button button = Instantiate(optionButtonPrefab, optionContainer);
            int index = buttons.Count;

            buttons.Add(button);
            labels.Add(button.GetComponentInChildren<TMP_Text>(true));
            button.onClick.AddListener(() => OptionChosen?.Invoke(index));
        }
    }
}