using UnityEngine;
using UnityEngine.UI;

// Sandbox only: flips the Selectable's interactable state on an interval in Play Mode, so Implicit Grayscale can be
// watched reacting without clicking anything.
[RequireComponent(typeof(Selectable))]
public sealed class SandboxInteractableToggler : MonoBehaviour
{
    public float Interval = 1f;

    private Selectable m_Selectable;
    private float m_Next;

    private void Awake()
    {
        m_Selectable = GetComponent<Selectable>();
    }

    private void Update()
    {
        if (Time.time < m_Next)
            return;

        m_Next = Time.time + Interval;
        m_Selectable.interactable = !m_Selectable.interactable;
    }
}
