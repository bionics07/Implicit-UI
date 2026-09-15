using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    // Every expectation is derived from the mesh Unity generates with the component disabled, so the tests do
    // not depend on how a given Unity version lays out a sliced or tiled mesh.
    public class ImplicitFillTests
    {
        private const float Tolerance = 1e-3f;
        private const string SquareSpritePath = "UI/Skin/UISprite.psd";
        private const string CircleSpritePath = "UI/Skin/Knob.psd";

        private Canvas m_Canvas;
        private Image m_Image;
        private ImplicitFill m_Fill;
        private MeshCapture m_Capture;
        private Sprite m_CreatedSprite;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            m_Image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 40f));
            m_Image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(SquareSpritePath);
            m_Image.type = Image.Type.Sliced;
            m_Fill = m_Image.gameObject.AddComponent<ImplicitFill>();
            m_Capture = m_Image.gameObject.AddComponent<MeshCapture>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Canvas.gameObject);
            UiTestUtility.DestroySprite(m_CreatedSprite);
        }

        [Test]
        public void BuiltInSquareSpriteIsNineSliced()
        {
            Assert.That(m_Image.sprite, Is.Not.Null);
            Assert.That(m_Image.hasBorder, Is.True);
        }

        [Test]
        public void FullAmountLeavesTheNativeMesh()
        {
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillAmount = 1f;

            AssertSameMesh(NativeMesh(), Rebuild());
        }

        [Test]
        public void ZeroAmountLeavesNoGeometry()
        {
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillAmount = 0f;

            Assert.That(Rebuild(), Is.Empty);
        }

        [Test]
        public void DisablingTheComponentRestoresTheFullMesh()
        {
            var native = NativeMesh();
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillAmount = 0.4f;
            Assert.That(Rebuild().Count, Is.Not.EqualTo(native.Count));

            m_Fill.enabled = false;

            AssertSameMesh(native, Rebuild());
        }

        [Test]
        public void CutInsideTheLeftBorder()
        {
            AssertCutBetweenColumnEdges(0, 0.5f);
        }

        [Test]
        public void CutExactlyOnTheLeftBorderEdge()
        {
            AssertCutBetweenColumnEdges(1, 0f);
        }

        [Test]
        public void CutInsideTheStretchedCenter()
        {
            AssertCutBetweenColumnEdges(1, 0.5f);
        }

        [Test]
        public void CutInsideTheRightBorder()
        {
            AssertCutBetweenColumnEdges(2, 0.5f);
        }

        [TestCase(Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left)]
        [TestCase(Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right)]
        [TestCase(Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom)]
        [TestCase(Image.FillMethod.Vertical, (int)Image.OriginVertical.Top)]
        public void EachOriginKeepsTheSideTheFillGrowsFrom(Image.FillMethod method, int origin)
        {
            AssertCut(NativeMesh(), method, origin, 0.37f, true);
        }

        [Test]
        public void RectSmallerThanTheBordersIsCutAlongTheShrunkBorders()
        {
            var border = m_Image.sprite.border;
            Assert.That(border.x + border.z, Is.GreaterThan(12f), "the rect must be narrower than both borders");
            ((RectTransform)m_Image.transform).sizeDelta = new Vector2(12f, 12f);

            AssertCut(NativeMesh(), Image.FillMethod.Horizontal, 0, 0.5f, true);
        }

        [Test]
        public void PreserveAspectIsCutAgainstTheDrawnMeshNotTheRect()
        {
            m_Image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(CircleSpritePath);
            m_Image.type = Image.Type.Simple;
            m_Image.preserveAspect = true;

            var native = NativeMesh();
            Assert.That(Extent(native, 0), Is.LessThan(199f), "preserveAspect narrows the mesh inside the rect");

            // A cut measured against the rect would fall left of this narrower mesh and leave nothing.
            AssertCut(native, Image.FillMethod.Horizontal, 0, 0.25f, true);
        }

        [Test]
        public void SpriteWithoutBordersFillsLikeASimpleImage()
        {
            m_Image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(CircleSpritePath);
            Assert.That(m_Image.hasBorder, Is.False);

            AssertCut(NativeMesh(), Image.FillMethod.Horizontal, 0, 0.6f, true);
        }

        [Test]
        public void TiledImageIsCut()
        {
            m_Image.type = Image.Type.Tiled;

            // Tiling repeats UVs along the bar, so only geometry is checked here.
            AssertCut(NativeMesh(), Image.FillMethod.Horizontal, 0, 0.45f, false);
        }

        [Test]
        public void SpriteFromPartOfATextureKeepsItsUvRange()
        {
            m_CreatedSprite = UiTestUtility.CreateSprite(64, new Rect(16f, 16f, 32f, 32f), new Vector4(8f, 8f, 8f, 8f));
            m_Image.sprite = m_CreatedSprite;

            var native = NativeMesh();
            Assert.That(native.Min(v => v.uv0.x), Is.EqualTo(0.25f).Within(Tolerance), "UVs start inside the texture");

            AssertCut(native, Image.FillMethod.Horizontal, 0, 0.6f, true);
        }

        [Test]
        public void FilledImageIsNotCutTwice()
        {
            m_Image.type = Image.Type.Filled;
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillAmount = 0.5f;

            var native = NativeMesh();
            Assert.That(native, Is.Not.Empty);

            AssertSameMesh(native, Rebuild());
        }

        [Test]
        public void RadialFillMethodLeavesTheMeshUntouched()
        {
            m_Image.fillMethod = Image.FillMethod.Radial360;
            m_Image.fillAmount = 0.5f;

            AssertSameMesh(NativeMesh(), Rebuild());
        }

        [Test]
        public void ChangingTheFillMethodResetsTheOriginAndTheCutFollows()
        {
            var native = NativeMesh();
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillOrigin = (int)Image.OriginHorizontal.Right;

            m_Image.fillMethod = Image.FillMethod.Vertical;

            Assert.That(m_Image.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Bottom));
            AssertCut(native, Image.FillMethod.Vertical, m_Image.fillOrigin, 0.5f, true);
        }

        [Test]
        public void ChangingTheFillAmountDoesNotRebuildLayout()
        {
            var counter = m_Image.gameObject.AddComponent<LayoutRebuildCounter>();
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            Rebuild();
            counter.ResetCount();
            var meshRebuilds = m_Capture.Rebuilds;

            m_Image.fillAmount = 0.3f;
            Rebuild();

            Assert.That(m_Capture.Rebuilds, Is.GreaterThan(meshRebuilds), "the fill change rebuilt the mesh");
            Assert.That(counter.Rebuilds, Is.Zero);
        }

        [TestCase(typeof(RectMask2D))]
        [TestCase(typeof(Mask))]
        public void MasksAboveDoNotChangeTheCut(System.Type maskType)
        {
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            m_Image.fillAmount = 0.4f;
            var unmasked = Rebuild();

            var mask = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(400f, 400f)).gameObject;
            if (maskType == typeof(RectMask2D))
                Object.DestroyImmediate(mask.GetComponent<Image>());
            mask.AddComponent(maskType);
            var rebuilds = m_Capture.Rebuilds;

            m_Image.transform.SetParent(mask.transform, false);
            var masked = Rebuild();

            Assert.That(m_Capture.Rebuilds, Is.GreaterThan(rebuilds), "moving under the mask rebuilt the mesh");
            AssertSameMesh(unmasked, masked);
        }

        private List<UIVertex> Rebuild()
        {
            Canvas.ForceUpdateCanvases();
            return new List<UIVertex>(m_Capture.Triangles);
        }

        private List<UIVertex> NativeMesh()
        {
            m_Fill.enabled = false;
            var mesh = Rebuild();
            m_Fill.enabled = true;
            return mesh;
        }

        // Places the cut between two column edges of the native sliced mesh, at t from the first.
        private void AssertCutBetweenColumnEdges(int column, float t)
        {
            m_Image.fillMethod = Image.FillMethod.Horizontal;
            var native = NativeMesh();
            var edges = DistinctSorted(native, 0);
            Assert.That(edges.Count, Is.EqualTo(4), "a sliced mesh has four column edges");

            var min = edges[0];
            var max = edges[edges.Count - 1];
            var cut = Mathf.Lerp(edges[column], edges[column + 1], t);

            AssertCut(native, Image.FillMethod.Horizontal, 0, (cut - min) / (max - min), true);
        }

        private void AssertCut(List<UIVertex> native, Image.FillMethod method, int origin, float amount, bool checkUvs)
        {
            m_Image.fillMethod = method;
            m_Image.fillOrigin = origin;
            m_Image.fillAmount = amount;
            var mesh = Rebuild();

            var axis = method == Image.FillMethod.Horizontal ? 0 : 1;
            var fromMax = origin == 1;
            var edges = DistinctSorted(native, axis);
            var min = edges[0];
            var max = edges[edges.Count - 1];
            var cut = fromMax ? max - (max - min) * amount : min + (max - min) * amount;

            Assert.That(mesh, Is.Not.Empty);
            foreach (var vertex in mesh)
            {
                if (fromMax)
                    Assert.That(vertex.position[axis], Is.GreaterThanOrEqualTo(cut - Tolerance), "vertex past the cut");
                else
                    Assert.That(vertex.position[axis], Is.LessThanOrEqualTo(cut + Tolerance), "vertex past the cut");
            }

            var reached = fromMax ? mesh.Min(v => v.position[axis]) : mesh.Max(v => v.position[axis]);
            Assert.That(reached, Is.EqualTo(cut).Within(Tolerance), "the fill reaches the cut");

            var nativeArea = UiTestUtility.Area(native);
            Assert.That(UiTestUtility.Area(mesh), Is.EqualTo(nativeArea * amount).Within(nativeArea * Tolerance), "filled area");

            for (var i = 0; i < mesh.Count; i += 3)
                Assert.That(UiTestUtility.Area(mesh.GetRange(i, 3)), Is.GreaterThan(0f), "degenerate triangle");

            if (!checkUvs)
                return;

            var expectedUv = UvAt(native, axis, cut);
            foreach (var vertex in mesh.Where(v => Mathf.Abs(v.position[axis] - cut) <= Tolerance))
                Assert.That(vertex.uv0[axis], Is.EqualTo(expectedUv).Within(Tolerance), "UV on the cut");
        }

        // The UV a native mesh has at a coordinate, interpolated between its nearest vertex columns.
        private static float UvAt(List<UIVertex> native, int axis, float coordinate)
        {
            var samples = native
                .Select(v => new Vector2(v.position[axis], v.uv0[axis]))
                .Distinct()
                .OrderBy(p => p.x)
                .ToList();

            for (var i = 0; i + 1 < samples.Count; i++)
            {
                var a = samples[i];
                var b = samples[i + 1];
                if (coordinate >= a.x - Tolerance && coordinate <= b.x + Tolerance && b.x - a.x > Tolerance)
                    return Mathf.Lerp(a.y, b.y, (coordinate - a.x) / (b.x - a.x));
            }

            Assert.Fail("no native vertex columns around " + coordinate);
            return 0f;
        }

        private static List<float> DistinctSorted(List<UIVertex> mesh, int axis)
        {
            return mesh.Select(v => v.position[axis]).Distinct().OrderBy(value => value).ToList();
        }

        private static float Extent(List<UIVertex> mesh, int axis)
        {
            return mesh.Max(v => v.position[axis]) - mesh.Min(v => v.position[axis]);
        }

        private static void AssertSameMesh(List<UIVertex> expected, List<UIVertex> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), "vertex count");
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].position, Is.EqualTo(expected[i].position), "position " + i);
                Assert.That(actual[i].uv0, Is.EqualTo(expected[i].uv0), "uv0 " + i);
                Assert.That(actual[i].color, Is.EqualTo(expected[i].color), "color " + i);
            }
        }
    }
}
