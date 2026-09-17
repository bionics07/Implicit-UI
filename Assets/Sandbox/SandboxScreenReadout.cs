using UnityEngine;
using UnityEngine.UI;

// Sandbox only: prints the numbers the minimum touch size is computed from, so a device test can be checked by hand.
[RequireComponent(typeof(Text))]
public sealed class SandboxScreenReadout : MonoBehaviour
{
    private Text m_Text;
    private Canvas m_Canvas;

    private void Awake()
    {
        m_Text = GetComponent<Text>();
        m_Canvas = GetComponentInParent<Canvas>().rootCanvas;
    }

    private void Update()
    {
        var dpi = Screen.dpi;
        var usedDpi = dpi > 0f ? dpi : 160f;
        var pixels48 = 48f * usedDpi / 160f;
        m_Text.text = "Screen " + Screen.width + "x" + Screen.height + ", Screen.dpi " + dpi.ToString("0") +
                      (dpi > 0f ? "" : " (fallback 160)") + ", canvas scale " + m_Canvas.scaleFactor.ToString("0.00") +
                      " - 48 dp = " + pixels48.ToString("0") + " px = " + (pixels48 / m_Canvas.scaleFactor).ToString("0") +
                      " canvas units";
    }
}
