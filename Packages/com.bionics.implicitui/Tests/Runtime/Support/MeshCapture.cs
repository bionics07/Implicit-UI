using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // Records the final mesh of a Graphic. Mesh modifiers run in component order, so adding this after the
    // component under test captures that component's output.
    [ExecuteAlways]
    public sealed class MeshCapture : BaseMeshEffect
    {
        public List<UIVertex> Triangles { get; } = new List<UIVertex>();

        public int Rebuilds { get; private set; }

        public override void ModifyMesh(VertexHelper vh)
        {
            Triangles.Clear();
            vh.GetUIVertexStream(Triangles);
            Rebuilds++;
        }
    }
}
