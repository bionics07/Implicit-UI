using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // Writes fixed values into UV0.z and UV0.w of every vertex, to check what reaches the shader independently of
    // the component that will normally write them.
    [ExecuteAlways]
    public sealed class Uv0ChannelWriter : BaseMeshEffect
    {
        public float Z;
        public float W;

        public override void ModifyMesh(VertexHelper vh)
        {
            var vertex = new UIVertex();
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv0 = new Vector4(vertex.uv0.x, vertex.uv0.y, Z, W);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
