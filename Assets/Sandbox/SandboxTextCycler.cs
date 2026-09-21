using TMPro;
using UnityEngine;

// Sandbox only: cycles a text through short and long labels in Play Mode, so a size group can be watched following it.
[RequireComponent(typeof(TMP_Text))]
public sealed class SandboxTextCycler : MonoBehaviour
{
    public string[] Labels = { "Play", "Continue your saved game", "Go" };
    public float Interval = 2f;

    private TMP_Text m_Text;
    private int m_Index;
    private float m_Next;

    private void Awake()
    {
        m_Text = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (Time.time < m_Next)
            return;

        m_Next = Time.time + Interval;
        m_Text.text = Labels[m_Index];
        m_Index = (m_Index + 1) % Labels.Length;
    }
}
