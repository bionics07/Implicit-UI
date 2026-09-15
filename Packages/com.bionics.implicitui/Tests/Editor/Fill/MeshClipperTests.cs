using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ImplicitUI.Tests.EditorTests
{
    public class MeshClipperTests
    {
        private const float Tolerance = 1e-4f;

        // Keeps x <= 1 in every test unless stated otherwise.
        private static readonly Vector2 s_KeepLeft = Vector2.right;

        private readonly List<UIVertex> m_Result = new List<UIVertex>();

        [Test]
        public void TriangleFullyInsideIsKeptAsIs()
        {
            var triangle = Triangle(new Vector2(-2f, 0f), new Vector2(0f, 2f), new Vector2(0f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result.Count, Is.EqualTo(3));
            for (var i = 0; i < 3; i++)
                Assert.That(m_Result[i].position, Is.EqualTo(triangle[i].position));
        }

        [Test]
        public void TriangleFullyOutsideIsDropped()
        {
            var triangle = Triangle(new Vector2(2f, 0f), new Vector2(3f, 2f), new Vector2(3f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result, Is.Empty);
        }

        [Test]
        public void OneVertexInsideLeavesOneTriangle()
        {
            // Only (0, 0) is left of x = 1. The part kept is the same triangle scaled by 1/4: 1/16 of its area of 8.
            var triangle = Triangle(new Vector2(0f, 0f), new Vector2(4f, 4f), new Vector2(4f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result.Count, Is.EqualTo(3));
            Assert.That(UiTestUtility.Area(m_Result), Is.EqualTo(0.5f).Within(Tolerance));
            AssertAllLeftOf(1f);
        }

        [Test]
        public void TwoVerticesInsideLeaveTwoTriangles()
        {
            var triangle = Triangle(new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(2f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result.Count, Is.EqualTo(6));
            Assert.That(UiTestUtility.Area(m_Result), Is.EqualTo(2f - 0.5f).Within(Tolerance));
            AssertAllLeftOf(1f);
        }

        [Test]
        public void VertexTouchingTheCutFromOutsideLeavesNothing()
        {
            var triangle = Triangle(new Vector2(1f, 0f), new Vector2(3f, 2f), new Vector2(3f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result, Is.Empty, "a triangle reduced to a point is degenerate");
        }

        [Test]
        public void EdgeLyingOnTheCutKeepsTheTriangleWithoutNewVertices()
        {
            var triangle = Triangle(new Vector2(1f, 0f), new Vector2(1f, 2f), new Vector2(-1f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result.Count, Is.EqualTo(3));
            Assert.That(UiTestUtility.Area(m_Result), Is.EqualTo(2f).Within(Tolerance));
        }

        [Test]
        public void NegativeAxisKeepsTheOtherSide()
        {
            var triangle = Triangle(new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(2f, 0f));

            // Keeps x >= 1, written as dot(position, -right) <= -1.
            MeshClipper.ClipTriangles(triangle, -Vector2.right, -1f, m_Result);

            Assert.That(UiTestUtility.Area(m_Result), Is.EqualTo(0.5f).Within(Tolerance));
            foreach (var vertex in m_Result)
                Assert.That(vertex.position.x, Is.GreaterThanOrEqualTo(1f - Tolerance));
        }

        [Test]
        public void VerticesOnTheCutInterpolateEveryChannel()
        {
            var from = Vertex(new Vector2(0f, 0f), 0f);
            var to = Vertex(new Vector2(2f, 0f), 1f);
            var apex = Vertex(new Vector2(0f, 2f), 0f);
            var triangle = new List<UIVertex> { from, apex, to };

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            // The edge from -> to is cut at its midpoint, so every channel there sits halfway.
            var cut = m_Result.Find(v => Mathf.Approximately(v.position.x, 1f) && Mathf.Approximately(v.position.y, 0f));
            var expected = Vertex(new Vector2(1f, 0f), 0.5f);
            Assert.That(cut.normal, Is.EqualTo(expected.normal));
            Assert.That(cut.tangent, Is.EqualTo(expected.tangent));
            Assert.That(cut.uv0, Is.EqualTo(expected.uv0));
            Assert.That(cut.uv1, Is.EqualTo(expected.uv1));
            Assert.That(cut.uv2, Is.EqualTo(expected.uv2));
            Assert.That(cut.uv3, Is.EqualTo(expected.uv3));
            Assert.That(cut.color.r, Is.InRange(expected.color.r - 1, expected.color.r + 1));
            Assert.That(cut.color.a, Is.InRange(expected.color.a - 1, expected.color.a + 1));
        }

        [Test]
        public void ClippedTrianglesKeepTheSourceWinding()
        {
            var triangle = Triangle(new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(2f, 0f));
            var sourceSign = Mathf.Sign(Cross(triangle, 0));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            for (var i = 0; i < m_Result.Count; i += 3)
                Assert.That(Mathf.Sign(Cross(m_Result, i)), Is.EqualTo(sourceSign));
        }

        [Test]
        public void ResultIsClearedBeforeWriting()
        {
            m_Result.Add(UIVertex.simpleVert);
            var triangle = Triangle(new Vector2(2f, 0f), new Vector2(3f, 2f), new Vector2(3f, 0f));

            MeshClipper.ClipTriangles(triangle, s_KeepLeft, 1f, m_Result);

            Assert.That(m_Result, Is.Empty);
        }

        private void AssertAllLeftOf(float limit)
        {
            foreach (var vertex in m_Result)
                Assert.That(vertex.position.x, Is.LessThanOrEqualTo(limit + Tolerance));
        }

        private static List<UIVertex> Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            return new List<UIVertex> { Vertex(a, 0f), Vertex(b, 0f), Vertex(c, 0f) };
        }

        // Every channel carries a value derived from t, so interpolation mistakes show up per channel.
        private static UIVertex Vertex(Vector2 position, float t)
        {
            return new UIVertex
            {
                position = position,
                normal = new Vector3(t, 1f, 0f),
                tangent = new Vector4(t, 0f, 2f * t, 1f),
                color = Color32.Lerp(new Color32(0, 0, 0, 255), new Color32(200, 100, 50, 55), t),
                uv0 = new Vector4(t, 0f, 0f, 0f),
                uv1 = new Vector4(0f, t, 0f, 0f),
                uv2 = new Vector4(0f, 0f, t, 0f),
                uv3 = new Vector4(0f, 0f, 0f, t),
            };
        }

        private static float Cross(List<UIVertex> triangles, int index)
        {
            Vector2 ab = triangles[index + 1].position - triangles[index].position;
            Vector2 ac = triangles[index + 2].position - triangles[index].position;
            return ab.x * ac.y - ab.y * ac.x;
        }
    }
}
