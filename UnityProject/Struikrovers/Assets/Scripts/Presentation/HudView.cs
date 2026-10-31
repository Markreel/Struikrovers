using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// Status text, the main button, Cancel, one button per action group and the
    /// hide-Bandits toggle. It only displays what it is told and raises events for input.
    public sealed class HudView : MonoBehaviour
    {
        private static readonly Color GroupSelectedColor = new Color(0.30f, 0.60f, 0.85f);
        private static readonly Color GroupUnselectedColor = new Color(0.30f, 0.35f, 0.42f);

        [SerializeField] private Image statusBackground;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Toggle hideBanditsToggle;

        [Header("Action group buttons (generated)")]
        [SerializeField] private Button actionButtonPrefab;
        [SerializeField] private RectTransform actionButtonContainer;

        private readonly List<Button> groupButtons = new List<Button>();
        private readonly List<TMP_Text> groupLabels = new List<TMP_Text>();
        private readonly List<string> groupIds = new List<string>();

        public event Action PrimaryClicked;
        public event Action CancelClicked;
        public event Action<string> ActionGroupSelected;
        public event Action<bool> HideBanditsChanged;

        private void Awake()
        {
            primaryButton.onClick.AddListener(() => PrimaryClicked?.Invoke());
            cancelButton.onClick.AddListener(() => CancelClicked?.Invoke());

            if (hideBanditsToggle != null)
                hideBanditsToggle.onValueChanged.AddListener(value => HideBanditsChanged?.Invoke(value));
        }

        public void SetHideBandits(bool value)
        {
            if (hideBanditsToggle != null)
                hideBanditsToggle.SetIsOnWithoutNotify(value);
        }

        public void Show(
            string message, Color backgroundColor,
            string primaryText, bool primaryInteractable, bool cancelVisible,
            IReadOnlyList<ActionGroupInfo> groups, string selectedGroup)
        {
            statusText.text = message;
            statusBackground.color = backgroundColor;
            primaryLabel.text = primaryText;
            primaryButton.interactable = primaryInteractable;
            cancelButton.gameObject.SetActive(cancelVisible);

            ShowGroups(groups, selectedGroup);
        }

        private void ShowGroups(IReadOnlyList<ActionGroupInfo> groups, string selectedGroup)
        {
            bool canShow = actionButtonPrefab != null && actionButtonContainer != null;
            int count = canShow && groups != null ? groups.Count : 0;

            while (groupButtons.Count < count)
                CreateGroupButton();

            for (int i = 0; i < groupButtons.Count; i++)
            {
                if (i >= count)
                {
                    groupButtons[i].gameObject.SetActive(false);
                    continue;
                }

                groupIds[i] = groups[i].Id;
                groupLabels[i].text = groups[i].Label;
                groupButtons[i].gameObject.SetActive(true);

                if (groupButtons[i].targetGraphic != null)
                {
                    groupButtons[i].targetGraphic.color =
                        groups[i].Id == selectedGroup ? GroupSelectedColor : GroupUnselectedColor;
                }
            }
        }

        private void CreateGroupButton()
        {
            Button button = Instantiate(actionButtonPrefab, actionButtonContainer);
            int index = groupButtons.Count;

            groupButtons.Add(button);
            groupLabels.Add(button.GetComponentInChildren<TMP_Text>(true));
            groupIds.Add("");

            button.onClick.AddListener(() => ActionGroupSelected?.Invoke(groupIds[index]));
        }
    }
}