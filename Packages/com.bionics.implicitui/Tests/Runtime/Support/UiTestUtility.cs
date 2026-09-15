using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    public static class UiTestUtility
    {
        public static Canvas CreateCanvas()
        {
            var canvas = new GameObject("Test Canvas", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return canvas;
        }

        public static Image CreateImage(Transform parent, Vector2 size)
        {
            var gameObject = new GameObject("Test Image", typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            ((RectTransform)gameObject.transform).sizeDelta = size;
            return gameObject.AddComponent<Image>();
        }

        // A sprite over part of a texture, so its UVs are not 0..1 - what an atlas does to a sprite.
        public static Sprite CreateSprite(int textureSize, Rect rect, Vector4 border)
        {
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            Object.DestroyImmediate(sprite.texture);
            Object.DestroyImmediate(sprite);
        }

        public static float Area(List<UIVertex> triangles)
        {
            var area = 0f;
            for (var i = 0; i + 2 < triangles.Count; i += 3)
            {
                Vector2 ab = triangles[i + 1].position - triangles[i].position;
                Vector2 ac = triangles[i + 2].position - triangles[i].position;
                area += Mathf.Abs(ab.x * ac.y - ab.y * ac.x) * 0.5f;
            }

            return area;
        }
    }
}
