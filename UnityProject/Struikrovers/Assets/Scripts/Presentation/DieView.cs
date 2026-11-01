using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Struikrovers.Core;

namespace Struikrovers.Presentation
{
    /// One die. It shows a face sprite from the theme (a number on a coloured square when
    /// there is no sprite) and can tumble through the faces before landing on the result.
    public sealed class DieView : MonoBehaviour
    {
        [SerializeField] private Image face;
        [SerializeField] private TMP_Text fallbackLabel;

        private VisualTheme theme;
        private Coroutine rolling;
        private float rollEndTime;

        /// Based on a deadline, so a stopped coroutine can never leave this stuck on true.
        public bool IsRolling => Time.unscaledTime < rollEndTime;

        public void Setup(VisualTheme theme) => this.theme = theme;

        /// Shows the result at once. A discarded die (the lower one of advantage) is dimmed and smaller.
        public void Show(int value, Color tint, bool discarded)
        {
            StopRolling();
            Apply(value, tint, discarded);
        }

        /// Tumbles through the faces and lands on 'value'.
        public void Roll(int value, Color tint, bool discarded)
        {
            StopRolling();

            if (!isActiveAndEnabled || theme == null || theme.DiceRollDuration <= 0f)
            {
                Apply(value, tint, discarded);
                return;
            }

            rollEndTime = Time.unscaledTime + theme.DiceRollDuration;
            rolling = StartCoroutine(Tumble(value, tint, discarded));
        }

        private IEnumerator Tumble(int value, Color tint, bool discarded)
        {
            var wait = new WaitForSecondsRealtime(Mathf.Max(0.01f, theme.DiceFaceInterval));
            int[] faces = GameConfig.MovementDie;

            while (Time.unscaledTime < rollEndTime)
            {
                Apply(faces[Random.Range(0, faces.Length)], tint, discarded);
                yield return wait;
            }

            Apply(value, tint, discarded);
            rolling = null;
        }

        private void StopRolling()
        {
            if (rolling != null)
                StopCoroutine(rolling);

            rolling = null;
            rollEndTime = 0f;
        }

        private void OnDisable()
        {
            rolling = null;
            rollEndTime = 0f;
        }

        private void Apply(int value, Color tint, bool discarded)
        {
            Sprite sprite = theme != null ? theme.GetDieFace(value) : null;

            face.sprite = sprite;
            fallbackLabel.gameObject.SetActive(sprite == null);
            fallbackLabel.text = value.ToString();

            Color color = tint;
            if (discarded && theme != null)
                color.a *= theme.DiscardedDieAlpha;

            face.color = color;
            transform.localScale = discarded ? new Vector3(0.8f, 0.8f, 1f) : Vector3.one;
        }
    }
}