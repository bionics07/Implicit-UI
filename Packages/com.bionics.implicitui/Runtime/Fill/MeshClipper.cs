using System.Collections.Generic;
using UnityEngine;

namespace ImplicitUI
{
    // Clips a triangle stream against a half-plane, one triangle at a time (Sutherland-Hodgman). Vertices
    // created on the cut are interpolated across every UIVertex channel, so whatever the source mesh carried
    // - UVs read by masks or other effects, normals, tangents - survives the cut.
    internal static class MeshClipper
    {
        // Twice the area below which a clipped triangle is treated as degenerate and dropped.
        private const float DegenerateDoubleArea = 1e-5f;

        // A triangle cut by one straight line leaves at most four vertices.
        private static readonly UIVertex[] s_Polygon = new UIVertex[4];

        // Writes to result the part of each triangle where Vector2.Dot(position, axis) <= limit.
        internal static void ClipTriangles(List<UIVertex> triangles, Vector2 axis, float limit, List<UIVertex> result)
        {
            result.Clear();
            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                var count = ClipTriangle(triangles[i], triangles[i + 1], triangles[i + 2], axis, limit);

                // Fan triangulation keeps the source winding.
                for (var k = 1; k + 1 < count; k++)
                    AddTriangle(s_Polygon[0], s_Polygon[k], s_Polygon[k + 1], result);
            }
        }

        private static int ClipTriangle(UIVertex a, UIVertex b, UIVertex c, Vector2 axis, float limit)
        {
            var count = ClipEdge(a, b, axis, limit, 0);
            count = ClipEdge(b, c, axis, limit, count);
            return ClipEdge(c, a, axis, limit, count);
        }

        private static int ClipEdge(UIVertex from, UIVertex to, Vector2 axis, float limit, int count)
        {
            var fromDistance = Distance(from, axis, limit);
            var toDistance = Distance(to, axis, limit);
            var fromInside = fromDistance <= 0f;

            if (fromInside)
                s_Polygon[count++] = from;

            // The signs differ here, so the denominator cannot be zero.
            if (fromInside != toDistance <= 0f)
                s_Polygon[count++] = Lerp(from, to, fromDistance / (fromDistance - toDistance));

            return count;
        }

        private static float Distance(UIVertex vertex, Vector2 axis, float limit)
        {
            return Vector2.Dot(vertex.position, axis) - limit;
        }

        private static void AddTriangle(UIVertex a, UIVertex b, UIVertex c, List<UIVertex> result)
        {
            Vector2 ab = b.position - a.position;
            Vector2 ac = c.position - a.position;
            if (Mathf.Abs(ab.x * ac.y - ab.y * ac.x) <= DegenerateDoubleArea)
                return;

            result.Add(a);
            result.Add(b);
            result.Add(c);
        }

        private static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            return new UIVertex
            {
                position = Vector3.LerpUnclamped(a.position, b.position, t),
                normal = Vector3.LerpUnclamped(a.normal, b.normal, t),
                tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t),
                color = Color32.LerpUnclamped(a.color, b.color, t),
                uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t),
                uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t),
                uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t),
                uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t),
            };
        }
    }
}
