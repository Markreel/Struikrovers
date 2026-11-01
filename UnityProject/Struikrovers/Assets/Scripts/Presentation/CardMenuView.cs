using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    public readonly struct CardMenuEntry
    {
        public readonly string Label;
        public readonly Action OnChosen;
        public readonly bool Enabled;

        public CardMenuEntry(string label, Action onChosen, bool enabled = true)
        {
            Label = label;
            OnChosen = onChosen;
            Enabled = enabled;
        }
    }

    /// A small menu with the things you can do with one card. Clicking outside it closes it.
    public sealed class CardMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button backdrop;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button entryButtonPrefab;
        [SerializeField] private RectTransform entryContainer;

        private readonly List<Button> buttons = new List<Button>();
        private readonly List<TMP_Text> labels = new List<TMP_Text>();
        private readonly List<CardMenuEntry> current = new List<CardMenuEntry>();

        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (backdrop != null) backdrop.onClick.AddListener(Hide);
            if (panel != null) panel.SetActive(false);
        }

        public void Show(string title, string detail, IReadOnlyList<CardMenuEntry> entries)
        {
            current.Clear();
            current.AddRange(entries);

            panel.SetActive(true);
            titleLabel.text = title;
            detailLabel.text = detail ?? "";

            while (buttons.Count < current.Count)
                CreateButton();

            for (int i = 0; i < buttons.Count; i++)
            {
                bool used = i < current.Count;
                buttons[i].gameObject.SetActive(used);

                if (used)
                {
                    labels[i].text = current[i].Label;
                    buttons[i].interactable = current[i].Enabled;
                }
            }
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        private void CreateButton()
        {
            Button button = Instantiate(entryButtonPrefab, entryContainer);
            int index = buttons.Count;

            buttons.Add(button);
            labels.Add(button.GetComponentInChildren<TMP_Text>(true));
            button.onClick.AddListener(() => OnEntryClicked(index));
        }

        private void OnEntryClicked(int index)
        {
            if (index >= current.Count)
                return;

            Action action = current[index].OnChosen;
            Hide();
            action?.Invoke();
        }
    }
}