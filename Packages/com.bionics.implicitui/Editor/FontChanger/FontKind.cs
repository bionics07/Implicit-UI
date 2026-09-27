using System;
using UnityEngine;
using UnityEngine.UI;
#if IMPLICITUI_TMP
using TMPro;
#endif

namespace ImplicitUI.Editor
{
    // The two kinds of text the Font Changer handles. Their fonts are different types (Font and TMP_FontAsset) with no
    // conversion between them, so one run changes one kind.
    internal enum FontKind
    {
        TextMeshPro,
        LegacyText,
    }

    // Everything the Font Changer needs to know about a kind: which component, which serialized properties hold the font
    // and material, and the font types. Reading and writing through serialized properties lets Unity record prefab
    // overrides and Undo on its own, without the side effects of the components' setters.
    internal static class FontKinds
    {
        internal const string LegacyFontProperty = "m_FontData.m_Font";
        internal const string TmpFontProperty = "m_fontAsset";
        internal const string TmpMaterialProperty = "m_sharedMaterial";

        internal static bool IsAvailable(FontKind kind)
        {
#if IMPLICITUI_TMP
            return true;
#else
            return kind == FontKind.LegacyText;
#endif
        }

        internal static Type ComponentType(FontKind kind)
        {
#if IMPLICITUI_TMP
            if (kind == FontKind.TextMeshPro)
                return typeof(TMP_Text);
#endif
            return typeof(Text);
        }

        internal static Type FontType(FontKind kind)
        {
#if IMPLICITUI_TMP
            if (kind == FontKind.TextMeshPro)
                return typeof(TMP_FontAsset);
#endif
            return typeof(Font);
        }

        internal static string FontProperty(FontKind kind)
        {
            return kind == FontKind.TextMeshPro ? TmpFontProperty : LegacyFontProperty;
        }

        // Only TextMeshPro texts carry a material tied to their font.
        internal static bool HasMaterial(FontKind kind)
        {
            return kind == FontKind.TextMeshPro;
        }

        // The material a font uses when a text does not pick one.
        internal static Material DefaultMaterial(UnityEngine.Object font)
        {
#if IMPLICITUI_TMP
            if (font is TMP_FontAsset tmpFont)
                return tmpFont.material;
#endif
            return null;
        }

        // What a text says, whatever its kind.
        internal static string Content(Component component)
        {
            if (component is Text text)
                return text.text;
#if IMPLICITUI_TMP
            if (component is TMP_Text tmp)
                return tmp.text;
#endif
            return null;
        }

        // A TextMeshPro material samples its font's atlas; one made for another font renders garbage.
        internal static bool MaterialBelongsTo(Material material, UnityEngine.Object font)
        {
#if IMPLICITUI_TMP
            if (material == null || !(font is TMP_FontAsset tmpFont) || !material.HasProperty(ShaderUtilities.ID_MainTex))
                return false;

            var texture = material.GetTexture(ShaderUtilities.ID_MainTex);
            if (texture == null)
                return false;

            if (tmpFont.atlasTextures != null)
            {
                foreach (var atlas in tmpFont.atlasTextures)
                {
                    if (atlas == texture)
                        return true;
                }
            }

            return tmpFont.atlasTexture == texture;
#else
            return false;
#endif
        }

        // Line height relative to the font size: two fonts that differ here lay text out differently.
        internal static float RelativeLineHeight(UnityEngine.Object font)
        {
#if IMPLICITUI_TMP
            if (font is TMP_FontAsset tmpFont && tmpFont.faceInfo.pointSize > 0)
                return tmpFont.faceInfo.lineHeight / tmpFont.faceInfo.pointSize;
#endif
            return 0f;
        }

        internal static bool HasFallbacks(UnityEngine.Object font)
        {
#if IMPLICITUI_TMP
            if (font is TMP_FontAsset tmpFont)
                return tmpFont.fallbackFontAssetTable != null && tmpFont.fallbackFontAssetTable.Count > 0;
#endif
            return false;
        }
    }
}
