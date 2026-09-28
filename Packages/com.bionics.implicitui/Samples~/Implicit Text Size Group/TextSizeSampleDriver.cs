using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Samples
{
    /// <summary>
    /// Cycles the labels of both menus through short and long words. The menu with an Implicit Text Size
    /// Group keeps every label at one size; the menu without it lets each label pick its own.
    /// </summary>
    public sealed class TextSizeSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Labels of both menus, in the same order: the same text goes into each pair.")]
        private Text[] m_GroupedLabels;

        [SerializeField]
        private Text[] m_LooseLabels;

        [SerializeField]
        private string[] m_Labels = { "OK", "Settings", "Back to the main menu" };

        [SerializeField, Min(0.1f)]
        private float m_Seconds = 2f;

        private float m_Next;
        private int m_Index;

        private void Update()
        {
            if (Time.time < m_Next)
                return;

            m_Next = Time.time + m_Seconds;
            for (var i = 0; i < m_GroupedLabels.Length; i++)
            {
                var text = m_Labels[(m_Index + i) % m_Labels.Length];
                if (m_GroupedLabels[i] != null)
                    m_GroupedLabels[i].text = text;
                if (i < m_LooseLabels.Length && m_LooseLabels[i] != null)
                    m_LooseLabels[i].text = text;
            }

            m_Index++;
        }
    }
}
