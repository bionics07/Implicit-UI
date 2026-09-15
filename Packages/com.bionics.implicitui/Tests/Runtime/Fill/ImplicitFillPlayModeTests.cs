using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // Geometry is covered in Edit Mode; this only proves the component does the same in a running player loop.
    public class ImplicitFillPlayModeTests
    {
        private Canvas m_Canvas;
        private Sprite m_Sprite;

        [TearDown]
        public void TearDown()
        {
            if (m_Canvas != null)
                Object.Destroy(m_Canvas.gameObject);
            UiTestUtility.DestroySprite(m_Sprite);
        }

        [UnityTest]
        public IEnumerator FillsASlicedImageInPlayMode()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            m_Sprite = UiTestUtility.CreateSprite(64, new Rect(0f, 0f, 64f, 64f), new Vector4(16f, 16f, 16f, 16f));
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 40f));
            image.sprite = m_Sprite;
            image.type = Image.Type.Sliced;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.gameObject.AddComponent<ImplicitFill>();
            var capture = image.gameObject.AddComponent<MeshCapture>();

            yield return null;
            Canvas.ForceUpdateCanvases();
            var fullArea = UiTestUtility.Area(capture.Triangles);
            Assert.That(fullArea, Is.GreaterThan(0f));

            image.fillAmount = 0.5f;
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(UiTestUtility.Area(capture.Triangles), Is.EqualTo(fullArea * 0.5f).Within(fullArea * 1e-3f));
        }
    }
}
