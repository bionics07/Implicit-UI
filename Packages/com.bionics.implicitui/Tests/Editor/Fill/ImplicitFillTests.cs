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

        // Normalized distance from a triangle edge under which a sample point counts as lying on the cut.
        private const float BoundaryTolerance = 1e-4f;

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

        [TestCase(Image.FillMethod.Radial90)]
        [TestCase(Image.FillMethod.Radial180)]
        [TestCase(Image.FillMethod.Radial360)]
        public void RadialMatchesANativeFilledImageOnASquare(Image.FillMethod method)
        {
            AssertRadialParity(method, new Vector2(100f, 100f), Image.Type.Simple, true);
        }

        [TestCase(Image.FillMethod.Radial90)]
        [TestCase(Image.FillMethod.Radial180)]
        [TestCase(Image.FillMethod.Radial360)]
        public void RadialMatchesANativeFilledImageOnAWideRect(Image.FillMethod method)
        {
            AssertRadialParity(method, new Vector2(200f, 60f), Image.Type.Simple, true);
        }

        // A sliced or tiled mesh has different UVs than a Filled one, so only the region it covers is compared.
        [TestCase(Image.FillMethod.Radial90)]
        [TestCase(Image.FillMethod.Radial180)]
        [TestCase(Image.FillMethod.Radial360)]
        public void RadialOnASlicedImageCoversTheRegionANativeFilledImageCovers(Image.FillMethod method)
        {
            AssertRadialParity(method, new Vector2(200f, 60f), Image.Type.Sliced, false);
        }

        [Test]
        public void RadialOnATiledImageCoversTheRegionANativeFilledImageCovers()
        {
            AssertRadialParity(Image.FillMethod.Radial360, new Vector2(200f, 60f), Image.Type.Tiled, false);
        }

        [Test]
        public void RadialWithPreserveAspectMatchesANativeFilledImage()
        {
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>(CircleSpritePath);
            AssertRadialParity(Image.FillMethod.Radial360, new Vector2(200f, 60f), Image.Type.Simple, true, image =>
            {
                image.sprite = circle;
                image.preserveAspect = true;
            });
        }

        [Test]
        public void RadialParityCheckCatchesADifferentFill()
        {
            // Guards the parity helper itself: a native image at another amount must not count as a match.
            m_Image.type = Image.Type.Simple;
            var native = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 40f));
            native.sprite = m_Image.sprite;
            native.type = Image.Type.Filled;
            var nativeCapture = native.gameObject.AddComponent<MeshCapture>();
            foreach (var image in new[] { m_Image, native })
                image.fillMethod = Image.FillMethod.Radial360;
            m_Image.fillAmount = 0.4f;
            native.fillAmount = 0.45f;

            Canvas.ForceUpdateCanvases();

            Assert.That(CountCoverageMismatches(m_Capture.Triangles, nativeCapture.Triangles, new Vector2(200f, 40f), false),
                Is.GreaterThan(0));
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

        private static readonly float[] s_RadialAmounts = { 0.1f, 0.3f, 0.5f, 0.62f, 0.9f };

        // Runs every origin, direction and several amounts against a native Filled image of the same size and
        // sprite, and reports every combination that differs in covered area or in which sample points it covers.
        private void AssertRadialParity(Image.FillMethod method, Vector2 size, Image.Type type, bool checkUvs,
            System.Action<Image> configure = null)
        {
            ((RectTransform)m_Image.transform).sizeDelta = size;
            m_Image.type = type;

            var native = UiTestUtility.CreateImage(m_Canvas.transform, size);
            native.sprite = m_Image.sprite;
            native.type = Image.Type.Filled;
            configure?.Invoke(m_Image);
            configure?.Invoke(native);
            var nativeCapture = native.gameObject.AddComponent<MeshCapture>();

            // Each mesh is compared in its own normalized space. A Filled image draws inside the sprite's drawing
            // dimensions while a sliced or tiled mesh covers the rect, so their full extents already differ a little
            // before any fill - and the promise is the same fill relative to what each Image draws.
            foreach (var image in new[] { m_Image, native })
                image.fillAmount = 1f;
            Canvas.ForceUpdateCanvases();
            var ourFullArea = UiTestUtility.Area(m_Capture.Triangles);
            var nativeFullArea = UiTestUtility.Area(nativeCapture.Triangles);
            GetBounds(m_Capture.Triangles, out var ourMin, out var ourMax);
            GetBounds(nativeCapture.Triangles, out var nativeMin, out var nativeMax);
            Assert.That(ourFullArea, Is.GreaterThan(0f));
            Assert.That(nativeFullArea, Is.GreaterThan(0f));

            var failures = new List<string>();
            foreach (var clockwise in new[] { true, false })
            {
                for (var origin = 0; origin < 4; origin++)
                {
                    foreach (var amount in s_RadialAmounts)
                    {
                        foreach (var image in new[] { m_Image, native })
                        {
                            image.fillMethod = method;
                            image.fillOrigin = origin;
                            image.fillClockwise = clockwise;
                            image.fillAmount = amount;
                        }

                        Canvas.ForceUpdateCanvases();

                        var label = method + " origin " + origin + (clockwise ? " clockwise" : " counterclockwise") + " amount " + amount;
                        var ourFraction = UiTestUtility.Area(m_Capture.Triangles) / ourFullArea;
                        var nativeFraction = UiTestUtility.Area(nativeCapture.Triangles) / nativeFullArea;
                        if (Mathf.Abs(ourFraction - nativeFraction) > Tolerance)
                            failures.Add(label + ": filled fraction " + ourFraction + ", native " + nativeFraction);

                        var details = new System.Text.StringBuilder();
                        var mismatches = CountCoverageMismatches(m_Capture.Triangles, ourMin, ourMax,
                            nativeCapture.Triangles, nativeMin, nativeMax, checkUvs, details);
                        if (mismatches > 0)
                            failures.Add(label + ": " + mismatches + " sample points differ" + details);
                    }
                }
            }

            Assert.That(nativeCapture.Rebuilds, Is.GreaterThan(0), "the native image was rebuilt");
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        // Both meshes sampled at the same points of a shared rect.
        private static int CountCoverageMismatches(List<UIVertex> ours, List<UIVertex> native, Vector2 size, bool checkUvs)
        {
            return CountCoverageMismatches(ours, -size * 0.5f, size * 0.5f, native, -size * 0.5f, size * 0.5f, checkUvs);
        }

        // Sample points on an irregular grid, so none sits exactly on a center line or a diagonal. Each point is
        // the same normalized position inside each mesh's own bounds.
        private static int CountCoverageMismatches(List<UIVertex> ours, Vector2 ourMin, Vector2 ourMax,
            List<UIVertex> native, Vector2 nativeMin, Vector2 nativeMax, bool checkUvs,
            System.Text.StringBuilder details = null)
        {
            const int columns = 41;
            const int rows = 29;
            var mismatches = 0;
            for (var column = 0; column < columns; column++)
            {
                for (var row = 0; row < rows; row++)
                {
                    var normalized = new Vector2((column + 0.37f) / columns, (row + 0.61f) / rows);
                    var ourPoint = ourMin + Vector2.Scale(normalized, ourMax - ourMin);
                    var nativePoint = nativeMin + Vector2.Scale(normalized, nativeMax - nativeMin);
                    var inOurs = TryGetUv(ours, ourPoint, out var ourUv);
                    var inNative = TryGetUv(native, nativePoint, out var nativeUv);
                    if (inOurs == inNative && !(checkUvs && inOurs && (ourUv - nativeUv).sqrMagnitude > 1e-6f))
                        continue;

                    // A point this close to an edge sits on the cut itself, where inside or outside depends on float
                    // rounding (the native cut and ours reach the same line through different arithmetic). Measured
                    // ties were 3e-6 to 2e-5; a wrong cut moves whole regions and fails the area check anyway. Only
                    // measured for points that disagree, which keeps meshes with many triangles fast.
                    var ourEdge = NearestEdgeDistance(ours, ourPoint, ourMin, ourMax);
                    var nativeEdge = NearestEdgeDistance(native, nativePoint, nativeMin, nativeMax);
                    if (ourEdge < BoundaryTolerance || nativeEdge < BoundaryTolerance)
                        continue;

                    mismatches++;
                    details?.Append(" [point " + normalized.x.ToString("F4") + ", " + normalized.y.ToString("F4")
                        + " ours " + inOurs + " native " + inNative
                        + " edge distance ours " + ourEdge.ToString("G3") + " native " + nativeEdge.ToString("G3") + "]");
                }
            }

            return mismatches;
        }

        // Shortest distance from a point to any triangle edge, in the mesh's normalized space (bounds = 0..1).
        private static float NearestEdgeDistance(List<UIVertex> triangles, Vector2 point, Vector2 min, Vector2 max)
        {
            var size = max - min;
            var p = new Vector2((point.x - min.x) / size.x, (point.y - min.y) / size.y);
            var nearest = float.MaxValue;
            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                for (var k = 0; k < 3; k++)
                {
                    Vector2 a = triangles[i + k].position;
                    Vector2 b = triangles[i + (k + 1) % 3].position;
                    a = new Vector2((a.x - min.x) / size.x, (a.y - min.y) / size.y);
                    b = new Vector2((b.x - min.x) / size.x, (b.y - min.y) / size.y);
                    var ab = b - a;
                    var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                    nearest = Mathf.Min(nearest, Vector2.Distance(p, a + ab * t));
                }
            }

            return nearest;
        }

        private static void GetBounds(List<UIVertex> mesh, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (var vertex in mesh)
            {
                min = Vector2.Min(min, vertex.position);
                max = Vector2.Max(max, vertex.position);
            }
        }

        private static bool TryGetUv(List<UIVertex> triangles, Vector2 point, out Vector2 uv)
        {
            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                Vector2 a = triangles[i].position;
                Vector2 b = triangles[i + 1].position;
                Vector2 c = triangles[i + 2].position;
                var denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(denominator) < 1e-6f)
                    continue;

                var wa = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
                var wb = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
                var wc = 1f - wa - wb;
                if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f)
                    continue;

                uv = wa * (Vector2)triangles[i].uv0 + wb * (Vector2)triangles[i + 1].uv0 + wc * (Vector2)triangles[i + 2].uv0;
                return true;
            }

            uv = default;
            return false;
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
