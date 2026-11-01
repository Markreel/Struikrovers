using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// Full text of the card under the cursor.
    public sealed class CardDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private TMP_Text modifierLabel;
        [SerializeField] private TMP_Text bodyLabel;

        private void Awake() => Hide();

        public void Show(CardDefinition card, VisualTheme theme)
        {
            if (card == null || panel == null)
                return;

            panel.SetActive(true);
            background.color = Color.Lerp(theme.GetCardColor(card.Kind), Color.black, 0.55f);

            titleLabel.text = card.Name;
            subtitleLabel.text = DescribeKind(card);

            bool equipment = card.IsEquipment;
            modifierLabel.gameObject.SetActive(equipment);
            if (equipment)
                modifierLabel.text = "Combat modifier: " + card.Modifier.ToString("+0;-0;0");

            bodyLabel.text = card.Text;
        }

        public void Hide()
        {
            if (panel != null)
                panel.SetActive(false);
        }

        private static string DescribeKind(CardDefinition card)
        {
            switch (card.Kind)
            {
                case CardKind.Hand: return "Hand card";
                case CardKind.Curse: return "Curse";
                case CardKind.Dragon: return "Dragon";
                default:
                    string who = card.UsableBy.HasValue ? card.UsableBy.Value + " only" : "any pawn";
                    return $"Equipment · {card.Slot} · {who}";
            }
        }
    }
}