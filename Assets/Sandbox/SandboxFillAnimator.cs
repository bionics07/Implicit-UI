using UnityEngine;
using UnityEngine.UI;

// Sandbox only: ping-pongs Image.fillAmount in Play Mode, so Implicit Fill can be watched while it animates.
[RequireComponent(typeof(Image))]
public sealed class SandboxFillAnimator : MonoBehaviour
{
    public float Speed = 0.3f;
    public float Offset;

    private Image m_Image;

    private void Awake()
    {
        m_Image = GetComponent<Image>();
    }

    private void Update()
    {
        m_Image.fillAmount = Mathf.PingPong(Time.time * Speed + Offset, 1f);
    }
}
