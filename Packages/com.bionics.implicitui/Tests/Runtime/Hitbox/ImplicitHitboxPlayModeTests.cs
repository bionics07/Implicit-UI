using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // The Edit Mode tests call ApplyPadding directly. These go through the player loop and a real GraphicRaycaster: the
    // component has to write the padding on its own, and the raycaster has to accept touches in it.
    public class ImplicitHitboxPlayModeTests
    {
        private readonly List<RaycastResult> m_Results = new List<RaycastResult>();
        private Canvas m_Canvas;
        private GameObject m_EventSystem;

        [SetUp]
        public void SetUp()
        {
            ImplicitHitbox.s_DpiOverride = ImplicitHitbox.BaseDpi;
            m_EventSystem = new GameObject("Test EventSystem", typeof(EventSystem));
            m_Canvas = UiTestUtility.CreateCanvas();
            m_Canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        [TearDown]
        public void TearDown()
        {
            ImplicitHitbox.s_DpiOverride = -1f;
            Object.Destroy(m_Canvas.gameObject);
            Object.Destroy(m_EventSystem);
        }

        [UnityTest]
        public IEnumerator SmallButtonIsHitOutsideItsVisualAndStopsWhenDisabled()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 20f));
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();
            yield return null;

            // 48 dp at 160 dpi and scale 1 is 48 units: 20 units from the center is outside the visual, inside the target.
            var outsideVisual = ScreenPoint(image, new Vector2(20f, 0f));
            Assert.That(HitsImage(image, outsideVisual), Is.True);
            Assert.That(HitsImage(image, ScreenPoint(image, new Vector2(30f, 0f))), Is.False);

            hitbox.enabled = false;
            yield return null;

            Assert.That(image.raycastPadding, Is.EqualTo(Vector4.zero));
            Assert.That(HitsImage(image, outsideVisual), Is.False);
        }

        [UnityTest]
        public IEnumerator DensityChangeIsPickedUpWithinAFrame()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 20f));
            image.gameObject.AddComponent<ImplicitHitbox>();
            yield return null;

            var point = ScreenPoint(image, new Vector2(40f, 0f));
            Assert.That(HitsImage(image, point), Is.False);

            ImplicitHitbox.s_DpiOverride = 320f;
            yield return null;

            Assert.That(HitsImage(image, point), Is.True);
        }

        private bool HitsImage(Image image, Vector2 screenPoint)
        {
            m_Results.Clear();
            var data = new PointerEventData(EventSystem.current) { position = screenPoint };
            m_Canvas.GetComponent<GraphicRaycaster>().Raycast(data, m_Results);
            return m_Results.Exists(result => result.gameObject == image.gameObject);
        }

        private static Vector2 ScreenPoint(Image image, Vector2 local)
        {
            return RectTransformUtility.WorldToScreenPoint(null, image.rectTransform.TransformPoint(local));
        }
    }
}
