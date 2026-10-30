using UnityEngine;
using UnityEngine.UI;

namespace Struikrovers.Presentation
{
    /// Pulses the alpha of the four glow edge images. Only runs while active,
    /// so tiles without a glow cost nothing.
    public sealed class GlowPulse : MonoBehaviour
    {
        [SerializeField] private Image[] edges;

        private Color baseColor = Color.white;

        public void SetColor(Color color)
        {
            baseColor = color;
            Apply(1f);
        }

        private void OnEnable() => Apply(1f);

        private void Update()
        {
            float pulse = (Mathf.Sin(Time.time * 3f) + 1f) / 2f;
            Apply(Mathf.Lerp(0.35f, 1f, pulse));
        }

        private void Apply(float alpha)
        {
            Color color = baseColor;
            color.a = alpha;

            foreach (Image edge in edges)
                if (edge != null)
                    edge.color = color;
        }
    }
}