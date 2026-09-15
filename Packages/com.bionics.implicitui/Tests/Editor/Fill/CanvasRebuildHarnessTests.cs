using NUnit.Framework;
using UnityEngine;

namespace ImplicitUI.Tests.EditorTests
{
    // The fill tests read meshes and count layout passes through Canvas.ForceUpdateCanvases in Edit Mode. If
    // Unity stops rebuilding graphics that way, those tests would pass without checking anything - these
    // fail first.
    public class CanvasRebuildHarnessTests
    {
        private Canvas m_Canvas;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Canvas.gameObject);
        }

        [Test]
        public void ForceUpdateCanvasesRebuildsGraphicMeshes()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 40f));
            var capture = image.gameObject.AddComponent<MeshCapture>();

            Canvas.ForceUpdateCanvases();
            Assert.That(capture.Rebuilds, Is.GreaterThan(0));
            Assert.That(capture.Triangles, Is.Not.Empty);

            var rebuilds = capture.Rebuilds;
            image.color = Color.red;
            Canvas.ForceUpdateCanvases();
            Assert.That(capture.Rebuilds, Is.GreaterThan(rebuilds));
        }

        [Test]
        public void ForceUpdateCanvasesRunsLayoutRebuilds()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 40f));
            var counter = image.gameObject.AddComponent<LayoutRebuildCounter>();
            Canvas.ForceUpdateCanvases();
            counter.ResetCount();

            image.SetLayoutDirty();
            Canvas.ForceUpdateCanvases();

            Assert.That(counter.Rebuilds, Is.GreaterThan(0));
        }
    }
}
