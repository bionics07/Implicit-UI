using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Samples
{
    /// <summary>
    /// Fills in the sample's instructions at runtime, listing the fonts the scene and the prefabs use now,
    /// so the Font Changer can be tried on them and the result seen right away.
    /// </summary>
    public sealed class FontChangerSampleNote : MonoBehaviour
    {
        [SerializeField]
        private Text m_Readout;

        [SerializeField, Tooltip("Texts to report the font of.")]
        private Text[] m_Texts;

        private void Update()
        {
            if (m_Readout == null)
                return;

            var report = "";
            foreach (var text in m_Texts)
            {
                if (text != null)
                    report += "\"" + text.text + "\" uses " + (text.font != null ? text.font.name : "no font") + "\n";
            }

            m_Readout.text = report;
        }
    }
}
