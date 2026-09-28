using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Samples
{
    /// <summary>
    /// Turns the demo buttons on and off, so Implicit Grayscale can be seen following them with no code
    /// of its own. Also creates the EventSystem the scene needs, with whichever input module the project has.
    /// </summary>
    public sealed class GrayscaleSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Buttons flipped between interactable and not.")]
        private Selectable[] m_Buttons;

        [SerializeField]
        private Text m_Readout;

        [SerializeField, Min(0.1f)]
        private float m_Seconds = 1.5f;

        private float m_Next;
        private bool m_Interactable = true;

        private void Update()
        {
            if (Time.time < m_Next)
                return;

            m_Next = Time.time + m_Seconds;
            m_Interactable = !m_Interactable;
            foreach (var button in m_Buttons)
            {
                if (button != null)
                    button.interactable = m_Interactable;
            }

            if (m_Readout != null)
                m_Readout.text = m_Interactable ? "interactable = true" : "interactable = false";
        }
    }
}
