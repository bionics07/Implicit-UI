using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sandbox only: shows a graphic's real touch area on screen, including on a device where there is no Scene view, and
// counts the taps it receives, with the area size in canvas units next to the count. The area is a translucent child
// resized to the graphic's Raycast Padding every frame.
[RequireComponent(typeof(Graphic))]
public sealed class SandboxHitArea : MonoBehaviour, IPointerClickHandler
{
    public Text Counter;

    private Graphic m_Graphic;
    private RectTransform m_Area;
    private int m_Taps;

    private void Awake()
    {
        m_Graphic = GetComponent<Graphic>();

        var area = new GameObject("Touch area", typeof(RectTransform), typeof(Image));
        m_Area = (RectTransform)area.transform;
        m_Area.SetParent(transform, false);
        m_Area.SetAsFirstSibling();
        m_Area.anchorMin = Vector2.zero;
        m_Area.anchorMax = Vector2.one;

        var image = area.GetComponent<Image>();
        image.color = new Color(0.25f, 0.9f, 0.45f, 0.25f);
        image.raycastTarget = false;
    }

    private void LateUpdate()
    {
        var padding = m_Graphic.raycastPadding;
        m_Area.offsetMin = new Vector2(padding.x, padding.y);
        m_Area.offsetMax = new Vector2(-padding.z, -padding.w);

        if (Counter != null)
        {
            var size = m_Area.rect.size;
            Counter.text = m_Taps + (m_Taps == 1 ? " tap" : " taps") + " - area " + size.x.ToString("0") + " x " +
                           size.y.ToString("0");
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        m_Taps++;
    }
}
