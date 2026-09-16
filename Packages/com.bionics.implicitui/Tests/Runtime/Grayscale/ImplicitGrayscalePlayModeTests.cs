using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // The Edit Mode tests call RefreshIntensity directly. This one leaves it to the player loop: nothing notifies the
    // component when a Selectable changes, so it has to notice on its own within a frame.
    public class ImplicitGrayscalePlayModeTests
    {
        private Canvas m_Canvas;

        [TearDown]
        public void TearDown()
        {
            if (m_Canvas != null)
                Object.Destroy(m_Canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator DisablingAButtonTurnsItGrayOnItsOwn()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(100f, 40f));
            image.gameObject.AddComponent<ImplicitGrayscale>();
            var capture = image.gameObject.AddComponent<MeshCapture>();
            var button = image.gameObject.AddComponent<Button>();

            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertGray(capture, 0f);

            button.interactable = false;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertGray(capture, 1f);

            button.interactable = true;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertGray(capture, 0f);
        }

        private static void AssertGray(MeshCapture capture, float expected)
        {
            Assert.That(capture.Triangles, Is.Not.Empty);
            foreach (var vertex in capture.Triangles)
                Assert.That(vertex.uv0.z, Is.EqualTo(expected).Within(1e-4f));
        }
    }
}
