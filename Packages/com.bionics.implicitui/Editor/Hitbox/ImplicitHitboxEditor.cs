using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace ImplicitUI.Editor
{
    // Default fields, plus what decides whether the hitbox works: the touch area it gives right now and where that comes
    // from, and everything the alpha hit test needs from the Image and its texture, with one-click fixes that state their
    // memory cost. The Scene view outline is orange, apart from the green one Unity draws for Raycast Padding.
    [CustomEditor(typeof(ImplicitHitbox))]
    [CanEditMultipleObjects]
    internal sealed class ImplicitHitboxEditor : UnityEditor.Editor
    {
        private static readonly Color s_AreaColor = new Color(1f, 0.55f, 0.1f, 1f);

        private Sprite m_CachedSprite;
        private SpriteAtlas m_CachedAtlas;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // Leaving Play Mode destroys the component before the inspector's last repaint; C# pattern matching does not
            // see Unity's destroyed state, so the null check has to be explicit.
            if (targets.Length != 1 || !(target is ImplicitHitbox hitbox) || hitbox == null)
                return;

            if (hitbox.UseMinimumSize)
                DrawMinimumSizeInfo(hitbox);

            if (hitbox.AlphaThreshold > 0f)
                DrawAlphaChecks(hitbox.Graphic);
        }

        // The numbers change with the Game view size and the monitor, so the inspector repaints while it is shown.
        public override bool RequiresConstantRepaint()
        {
            return targets.Length == 1 && target is ImplicitHitbox hitbox && hitbox != null && hitbox.UseMinimumSize;
        }

        private static void DrawMinimumSizeInfo(ImplicitHitbox hitbox)
        {
            var graphic = hitbox.Graphic;
            var canvas = graphic != null ? graphic.canvas : null;
            if (canvas == null)
                return;

            var root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace)
            {
                EditorGUILayout.HelpBox("World Space canvases have no fixed size on screen, so the minimum size does " +
                                        "not apply here.", MessageType.Info);
                return;
            }

            var dpi = ImplicitHitbox.ScreenDpi;
            var size = graphic.rectTransform.rect.size;
            var basePadding = hitbox.BasePadding;
            var paddingArea = new Vector2(size.x - basePadding.x - basePadding.z, size.y - basePadding.y - basePadding.w);
            var minimum = hitbox.MinimumSizeInRectUnits(dpi);
            var area = Vector2.Max(paddingArea, minimum);

            EditorGUILayout.HelpBox(
                "Touch area" + (Application.isPlaying ? " (playing)" : "") + ": " + Units(area.x) + " x " + Units(area.y) +
                " units - " + Winner(paddingArea, minimum) + ".\n" +
                hitbox.MinimumSize.ToString("0.#") + " dp = " + Units(minimum.x) + " x " + Units(minimum.y) + " units here (" +
                dpi.ToString("0") + " dpi, canvas scale " + root.scaleFactor.ToString("0.00") + "). The Editor uses this " +
                "monitor's density and the Game view size, so the area changes with them and on each device; the Device " +
                "Simulator shows a phone's. Orange outline in the Scene view.",
                MessageType.Info);
        }

        private static string Winner(Vector2 paddingArea, Vector2 minimum)
        {
            var widthFromMinimum = minimum.x > paddingArea.x;
            var heightFromMinimum = minimum.y > paddingArea.y;
            if (widthFromMinimum && heightFromMinimum)
                return "the minimum size wins";
            if (!widthFromMinimum && !heightFromMinimum)
                return "the Raycast Padding wins";
            return widthFromMinimum
                ? "the minimum size wins on width, the Raycast Padding on height"
                : "the Raycast Padding wins on width, the minimum size on height";
        }

        private static string Units(float value)
        {
            return value.ToString("0.#");
        }

        private void DrawAlphaChecks(Graphic graphic)
        {
            if (!(graphic is Image image))
            {
                EditorGUILayout.HelpBox("The alpha hit test needs an Image. On other graphics it does nothing.",
                    MessageType.Warning);
                return;
            }

            if (image.alphaHitTestMinimumThreshold > 0f)
            {
                EditorGUILayout.HelpBox("The Image has its own Alpha Hit Test Minimum Threshold. Both tests run, and the " +
                                        "Image's rejects the padding area and atlas sprites. Turn it off and keep this one.",
                    MessageType.Warning);
                if (GUILayout.Button("Turn Off the Image's Threshold"))
                {
                    Undo.RecordObject(image, "Turn Off Alpha Hit Test Threshold");
                    image.alphaHitTestMinimumThreshold = 0f;
                    EditorUtility.SetDirty(image);
                }
            }

            var sprite = image.sprite;
            if (sprite == null)
                return;

            var atlas = FindAtlas(sprite);
            if (atlas != null)
                DrawAtlasChecks(atlas);
            else
                DrawTextureChecks(sprite.texture);
        }

        private static void DrawTextureChecks(Texture2D texture)
        {
            if (texture == null || texture.isReadable)
                return;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            if (importer == null)
            {
                EditorGUILayout.HelpBox("The sprite's texture cannot be read, so the alpha hit test accepts the whole " +
                                        "image.", MessageType.Warning);
                return;
            }

            var cost = FormatBytes(TextureMemory(texture));
            EditorGUILayout.HelpBox(
                "The alpha hit test needs Read/Write on '" + texture.name + "'. Without it, touches are accepted on the " +
                "whole image. Read/Write keeps a copy of the texture in memory: about " + cost + " more for this one.",
                MessageType.Warning);

            if (GUILayout.Button("Enable Read/Write (+" + cost + ")"))
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        private static void DrawAtlasChecks(SpriteAtlas atlas)
        {
            var path = AssetDatabase.GetAssetPath(atlas);
            var packing = atlas.GetPackingSettings();
            var texture = atlas.GetTextureSettings();
            var maxSize = atlas.GetPlatformSettings("DefaultTexturePlatform").maxTextureSize;
#if UNITY_2022_1_OR_NEWER
            var importer = AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
            if (importer != null)
            {
                packing = importer.packingSettings;
                texture = importer.textureSettings;
                maxSize = importer.GetPlatformSettings("DefaultTexturePlatform").maxTextureSize;
            }
#endif

            if (packing.enableTightPacking || packing.enableRotation)
            {
                EditorGUILayout.HelpBox(
                    "This sprite is packed in '" + atlas.name + "' with Tight Packing or Allow Rotation, which the alpha " +
                    "hit test cannot map. Touches are tested against the image rectangle instead. Turn the alpha hit test " +
                    "off if the rectangle is not what you want.",
                    MessageType.Warning);
                return;
            }

            if (texture.readable)
                return;

            EditorGUILayout.HelpBox(
                "This sprite is packed in the Sprite Atlas '" + atlas.name + "'. The alpha hit test needs Read/Write on " +
                "the atlas, and that keeps a copy of the whole atlas in memory - up to " + maxSize + "x" + maxSize +
                " pixels, which can be several MB. Consider leaving this sprite out of the atlas instead.",
                MessageType.Warning);

            if (GUILayout.Button("Enable Read/Write on '" + atlas.name + "'"))
            {
                texture.readable = true;
#if UNITY_2022_1_OR_NEWER
                if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter atlasImporter)
                {
                    atlasImporter.textureSettings = texture;
                    atlasImporter.SaveAndReimport();
                    return;
                }
#endif
                atlas.SetTextureSettings(texture);
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
            }
        }

        // Scans the project's atlases once per sprite shown; an inspector repaint must not search the whole project.
        private SpriteAtlas FindAtlas(Sprite sprite)
        {
            if (sprite == m_CachedSprite)
                return m_CachedAtlas;

            m_CachedSprite = sprite;
            m_CachedAtlas = null;
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid));
                if (atlas != null && atlas.CanBindTo(sprite))
                {
                    m_CachedAtlas = atlas;
                    break;
                }
            }

            return m_CachedAtlas;
        }

        private static long TextureMemory(Texture2D texture)
        {
            long bytes = 0;
            for (var mip = 0; mip < texture.mipmapCount; mip++)
            {
                var width = Mathf.Max(1, texture.width >> mip);
                var height = Mathf.Max(1, texture.height >> mip);
                bytes += (long)GraphicsFormatUtility.ComputeMipmapSize(width, height, texture.graphicsFormat);
            }

            return bytes;
        }

        private static string FormatBytes(long bytes)
        {
            return bytes >= 1024 * 1024
                ? (bytes / (1024f * 1024f)).ToString("0.#") + " MB"
                : Mathf.Max(1f, bytes / 1024f).ToString("0") + " KB";
        }

        // The final touch area: in Play Mode what the component wrote, in Edit Mode what it would write on this monitor.
        private void OnSceneGUI()
        {
            if (!(target is ImplicitHitbox hitbox) || hitbox == null || !hitbox.UseMinimumSize)
                return;

            var graphic = hitbox.Graphic;
            if (graphic == null)
                return;

            var padding = Application.isPlaying && hitbox.isActiveAndEnabled
                ? graphic.raycastPadding
                : hitbox.DesiredPadding(graphic.raycastPadding, ImplicitHitbox.ScreenDpi);

            var rect = graphic.rectTransform.rect;
            var space = graphic.rectTransform;
            var corners = new[]
            {
                space.TransformPoint(new Vector3(rect.xMin + padding.x, rect.yMin + padding.y)),
                space.TransformPoint(new Vector3(rect.xMin + padding.x, rect.yMax - padding.w)),
                space.TransformPoint(new Vector3(rect.xMax - padding.z, rect.yMax - padding.w)),
                space.TransformPoint(new Vector3(rect.xMax - padding.z, rect.yMin + padding.y)),
                space.TransformPoint(new Vector3(rect.xMin + padding.x, rect.yMin + padding.y)),
            };

            var color = Handles.color;
            Handles.color = s_AreaColor;
            Handles.DrawPolyLine(corners);
            Handles.color = color;

            var style = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = s_AreaColor } };
            Handles.Label(corners[1], "Implicit Hitbox: " + hitbox.MinimumSize.ToString("0.#") + " dp", style);
        }
    }
}
