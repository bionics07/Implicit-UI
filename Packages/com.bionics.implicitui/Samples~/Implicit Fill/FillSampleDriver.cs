using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Samples
{
    /// <summary>
    /// Drives the demo: the bars drain and refill while the panel is resized, which is when a Sliced
    /// fill and the usual workarounds stop looking the same.
    /// </summary>
    public sealed class FillSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Bars to fill. The framed one has Implicit Fill; the other is a plain Filled image.")]
        private Image[] m_Bars;

        [SerializeField, Tooltip("Resized to show that a Sliced image keeps its frame at any width.")]
        private RectTransform m_ResizedPanel;

        [SerializeField]
        private Text m_Readout;

        [SerializeField, Min(0.1f)]
        private float m_Seconds = 3f;

        private void Update()
        {
            var amount = Mathf.PingPong(Time.time / m_Seconds, 1f);
            foreach (var bar in m_Bars)
            {
                if (bar != null)
                    bar.fillAmount = amount;
            }

            if (m_ResizedPanel != null)
            {
                var size = m_ResizedPanel.sizeDelta;
                size.x = Mathf.Lerp(260f, 560f, Mathf.PingPong(Time.time / 5f, 1f));
                m_ResizedPanel.sizeDelta = size;
            }

            if (m_Readout != null)
                m_Readout.text = "fillAmount " + amount.ToString("0.00");
        }
    }
}
