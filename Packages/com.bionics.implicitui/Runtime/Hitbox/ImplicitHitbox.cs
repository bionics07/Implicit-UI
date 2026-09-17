using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI
{
    /// <summary>
    /// Keeps a graphic easy to hit on touch screens: a minimum touch target in density-independent pixels, and an
    /// alpha hit test that works with raycast padding and sprite atlases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum size grows the graphic's <see cref="Graphic.raycastPadding"/> while the game runs: any axis smaller
    /// than <see cref="MinimumSize"/> grows by the same amount on both sides, and an axis already large enough keeps the
    /// padding set in the inspector. Nothing is written in Edit Mode, so scenes and prefabs never store a value that
    /// depends on the screen of whoever opened them. Disabling the component restores the original padding.
    /// </para>
    /// <para>
    /// Density comes from <see cref="Screen.dpi"/>, with 160 dpi (one dp per pixel) when the platform reports 0. In the
    /// Editor that is the monitor's density, not a phone's: use the Device Simulator to see the size a device gets.
    /// World Space canvases have no fixed screen size, so the minimum size does not apply to them.
    /// </para>
    /// <para>
    /// The alpha hit test ignores touches on pixels more transparent than <see cref="AlphaThreshold"/>. Unlike the
    /// Image's own threshold, it accepts the padding area around the image, maps sprites packed in an atlas correctly,
    /// and logs once instead of on every touch when the texture is not readable. Sprites packed tightly or rotated in
    /// an atlas fall back to the rectangle.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/Implicit UI/Implicit Hitbox")]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    public sealed class ImplicitHitbox : MonoBehaviour, ICanvasRaycastFilter
    {
        internal const float BaseDpi = 160f;

        // Tests set this to get a fixed density; below zero the real Screen.dpi is used.
        internal static float s_DpiOverride = -1f;

        [SerializeField]
        [Tooltip("Grow the touch area of small graphics to at least Minimum Size while the game runs.")]
        private bool m_UseMinimumSize = true;

        [SerializeField, Min(0f)]
        [Tooltip("Smallest touch target, in density-independent pixels (dp). 48 dp is about 7.6 mm.")]
        private float m_MinimumSize = 48f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Touches on pixels with less alpha than this are ignored. 0 turns the alpha hit test off.")]
        private float m_AlphaThreshold;

        private Graphic m_Graphic;
        private Vector4 m_BasePadding;
        private Vector4 m_WrittenPadding;
        private bool m_HasWritten;
        private bool m_WarnedUnreadable;

        /// <summary>
        /// Whether small graphics grow their touch area to at least <see cref="MinimumSize"/> while the game runs.
        /// </summary>
        public bool UseMinimumSize
        {
            get => m_UseMinimumSize;
            set => m_UseMinimumSize = value;
        }

        /// <summary>
        /// Smallest touch target, in density-independent pixels. 48 by default, the size Android recommends.
        /// </summary>
        public float MinimumSize
        {
            get => m_MinimumSize;
            set => m_MinimumSize = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Touches on pixels with less alpha than this are ignored, from 0 (off) to 1. Needs an <see cref="Image"/>
        /// whose sprite texture has Read/Write enabled.
        /// </summary>
        public float AlphaThreshold
        {
            get => m_AlphaThreshold;
            set => m_AlphaThreshold = Mathf.Clamp01(value);
        }

        // Null once this component is destroyed: GetComponent on a destroyed component throws, and editor code can still
        // hold a reference for one more repaint.
        internal Graphic Graphic
        {
            get
            {
                if (m_Graphic == null && this != null)
                    m_Graphic = GetComponent<Graphic>();

                return m_Graphic;
            }
        }

        internal static float ScreenDpi
        {
            get
            {
                if (s_DpiOverride >= 0f)
                    return s_DpiOverride;

                var dpi = Screen.dpi;
                return dpi > 0f ? dpi : BaseDpi;
            }
        }

        /// <summary>
        /// Whether a touch at <paramref name="screenPoint"/> hits this graphic, according to the alpha hit test.
        /// </summary>
        /// <param name="screenPoint">The touch position, in screen pixels.</param>
        /// <param name="eventCamera">The canvas camera, or null for Screen Space - Overlay.</param>
        /// <returns>False only when the touch lands on a pixel more transparent than <see cref="AlphaThreshold"/>.</returns>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (m_AlphaThreshold <= 0f || !(Graphic is Image image))
                return true;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform, screenPoint, eventCamera,
                    out var local))
                return false;

            switch (SampleAlpha(image, local, m_AlphaThreshold))
            {
                case AlphaSample.Unreadable:
                    WarnUnreadable();
                    return true;
                case AlphaSample.Transparent:
                    return false;
                default:
                    return true;
            }
        }

        // The padding that gives the graphic at least minimumSize (in its own rect units) on each axis, starting from
        // basePadding. Raycast padding is inset per side: x left, y bottom, z right, w top; negative values grow the area.
        internal static Vector4 ComputePadding(Vector4 basePadding, Vector2 rectSize, Vector2 minimumSize)
        {
            var width = rectSize.x - basePadding.x - basePadding.z;
            if (width < minimumSize.x)
            {
                var grow = (minimumSize.x - width) * 0.5f;
                basePadding.x -= grow;
                basePadding.z -= grow;
            }

            var height = rectSize.y - basePadding.y - basePadding.w;
            if (height < minimumSize.y)
            {
                var grow = (minimumSize.y - height) * 0.5f;
                basePadding.y -= grow;
                basePadding.w -= grow;
            }

            return basePadding;
        }

        // The minimum size converted to the graphic's rect units, or zero where it does not apply (World Space).
        internal static Vector2 MinimumSizeInRectUnits(RectTransform rectTransform, Canvas canvas, float minimumDp,
            float dpi)
        {
            if (canvas == null || minimumDp <= 0f)
                return Vector2.zero;

            var root = canvas.rootCanvas;
            if (root.renderMode == RenderMode.WorldSpace)
                return Vector2.zero;

            var rootScale = root.transform.lossyScale;
            var scale = rectTransform.lossyScale;
            var pixelsPerUnitX = root.scaleFactor * Mathf.Abs(scale.x / rootScale.x);
            var pixelsPerUnitY = root.scaleFactor * Mathf.Abs(scale.y / rootScale.y);
            if (pixelsPerUnitX <= 0f || pixelsPerUnitY <= 0f || float.IsNaN(pixelsPerUnitX) || float.IsNaN(pixelsPerUnitY))
                return Vector2.zero;

            var pixels = minimumDp * dpi / BaseDpi;
            return new Vector2(pixels / pixelsPerUnitX, pixels / pixelsPerUnitY);
        }

        // The padding set by the user or other scripts: what the graphic has in Edit Mode, what was there before this
        // component grew it while playing.
        internal Vector4 BasePadding
        {
            get
            {
                var graphic = Graphic;
                if (graphic == null)
                    return Vector4.zero;

                return m_HasWritten && graphic.raycastPadding == m_WrittenPadding ? m_BasePadding : graphic.raycastPadding;
            }
        }

        // The minimum size in the graphic's rect units for a density, or zero where it does not apply.
        internal Vector2 MinimumSizeInRectUnits(float dpi)
        {
            var graphic = Graphic;
            return m_UseMinimumSize && graphic != null
                ? MinimumSizeInRectUnits(graphic.rectTransform, graphic.canvas, m_MinimumSize, dpi)
                : Vector2.zero;
        }

        // The padding this component wants on its graphic right now, including in Edit Mode, where the inspector draws it.
        internal Vector4 DesiredPadding(Vector4 basePadding, float dpi)
        {
            var graphic = Graphic;
            if (!m_UseMinimumSize || graphic == null)
                return basePadding;

            return ComputePadding(basePadding, graphic.rectTransform.rect.size, MinimumSizeInRectUnits(dpi));
        }

        // Writes the padding for the current screen. Anything else that changed the padding since the last write is taken
        // as the new base, so scripts and the inspector keep working while the component is on.
        internal void ApplyPadding()
        {
            var graphic = Graphic;
            if (graphic == null)
                return;

            var current = graphic.raycastPadding;
            if (!m_HasWritten || current != m_WrittenPadding)
                m_BasePadding = current;

            var desired = DesiredPadding(m_BasePadding, ScreenDpi);
            if (desired != current)
                graphic.raycastPadding = desired;

            m_WrittenPadding = desired;
            m_HasWritten = true;
        }

        internal void RestorePadding()
        {
            var graphic = Graphic;
            if (graphic != null && m_HasWritten && graphic.raycastPadding == m_WrittenPadding)
                graphic.raycastPadding = m_BasePadding;

            m_HasWritten = false;
        }

        private void OnEnable()
        {
            ApplyPadding();
        }

        private void OnDisable()
        {
            RestorePadding();
        }

        // Screen density, canvas scale and rect size all change without a callback that covers every case, so the padding
        // is checked once a frame; the write only happens when the value moved.
        private void LateUpdate()
        {
            ApplyPadding();
        }

        private void WarnUnreadable()
        {
            if (m_WarnedUnreadable)
                return;

            m_WarnedUnreadable = true;
            Debug.LogWarning("[Implicit UI] Implicit Hitbox on '" + name + "' cannot read its sprite's alpha, so " +
                             "touches are accepted on the whole image. Enable Read/Write on the texture (or on its " +
                             "Sprite Atlas); the Implicit Hitbox inspector does it with one click.", this);
        }

        internal enum AlphaSample
        {
            Opaque,
            Transparent,
            Unreadable,
        }

        // Samples the sprite under a point in the image's local space. Points outside the rect are in the padding area and
        // always hit; points inside the rect but outside what the image draws (preserve aspect) never do.
        internal static AlphaSample SampleAlpha(Image image, Vector2 local, float threshold)
        {
            var sprite = image.overrideSprite;
            var rect = image.rectTransform.rect;
            if (sprite == null || !rect.Contains(local))
                return AlphaSample.Opaque;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight ||
                                  sprite.packingRotation != SpritePackingRotation.None))
                return AlphaSample.Opaque;

            var texture = sprite.texture;
            if (texture == null)
                return AlphaSample.Opaque;

            if (!texture.isReadable)
                return AlphaSample.Unreadable;

            if (!TryMapToSprite(image, sprite, rect, local, out var spritePixel))
                return AlphaSample.Transparent;

            // From the sprite's rect to where its pixels sit in the texture; an atlas may trim transparent edges.
            var textureRect = sprite.textureRect;
            var texturePixel = textureRect.position + spritePixel - sprite.textureRectOffset;
            if (texturePixel.x < textureRect.xMin || texturePixel.x > textureRect.xMax ||
                texturePixel.y < textureRect.yMin || texturePixel.y > textureRect.yMax)
                return AlphaSample.Transparent;

            var alpha = texture.GetPixelBilinear(texturePixel.x / texture.width, texturePixel.y / texture.height).a;
            return alpha >= threshold ? AlphaSample.Opaque : AlphaSample.Transparent;
        }

        // Maps a local point to a pixel inside the sprite's rect, following how each image type stretches the sprite.
        private static bool TryMapToSprite(Image image, Sprite sprite, Rect rect, Vector2 local, out Vector2 spritePixel)
        {
            var spriteSize = sprite.rect.size;
            spritePixel = Vector2.zero;

            switch (image.type)
            {
                case Image.Type.Sliced when sprite.border != Vector4.zero:
                case Image.Type.Tiled:
                {
                    var unitsPerPixel = 1f / (image.pixelsPerUnit * image.pixelsPerUnitMultiplier);
                    var border = sprite.border;
                    var tiled = image.type == Image.Type.Tiled;
                    spritePixel.x = MapSlicedAxis(local.x - rect.xMin, rect.width, spriteSize.x, border.x, border.z,
                        unitsPerPixel, tiled);
                    spritePixel.y = MapSlicedAxis(local.y - rect.yMin, rect.height, spriteSize.y, border.y, border.w,
                        unitsPerPixel, tiled);
                    return true;
                }
                default:
                {
                    if (image.preserveAspect && image.type != Image.Type.Sliced)
                        rect = PreserveAspect(rect, spriteSize, image.rectTransform.pivot);

                    if (!rect.Contains(local))
                        return false;

                    spritePixel.x = (local.x - rect.xMin) / rect.width * spriteSize.x;
                    spritePixel.y = (local.y - rect.yMin) / rect.height * spriteSize.y;
                    return true;
                }
            }
        }

        // One axis of a sliced or tiled image: the borders keep their size (shrunk together when the rect is too small for
        // them), and the center either stretches or repeats.
        private static float MapSlicedAxis(float position, float size, float spriteSize, float borderMin,
            float borderMax, float unitsPerPixel, bool tiled)
        {
            var minUnits = borderMin * unitsPerPixel;
            var maxUnits = borderMax * unitsPerPixel;
            var bordersUnits = minUnits + maxUnits;
            if (bordersUnits > size && bordersUnits > 0f)
            {
                var shrink = size / bordersUnits;
                minUnits *= shrink;
                maxUnits *= shrink;
            }

            if (position < minUnits)
                return minUnits > 0f ? position / minUnits * borderMin : 0f;

            if (position > size - maxUnits)
                return maxUnits > 0f ? spriteSize - (size - position) / maxUnits * borderMax : spriteSize;

            var centerPixels = spriteSize - borderMin - borderMax;
            var offset = position - minUnits;
            if (tiled)
            {
                var tileUnits = centerPixels * unitsPerPixel;
                return tileUnits > 0f ? borderMin + Mathf.Repeat(offset, tileUnits) / unitsPerPixel : borderMin;
            }

            var centerUnits = size - minUnits - maxUnits;
            return centerUnits > 0f ? borderMin + offset / centerUnits * centerPixels : borderMin;
        }

        // The part of the rect a preserve-aspect image draws: the sprite's aspect, shrunk towards the pivot.
        private static Rect PreserveAspect(Rect rect, Vector2 spriteSize, Vector2 pivot)
        {
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                return rect;

            var spriteRatio = spriteSize.x / spriteSize.y;
            var rectRatio = rect.width / rect.height;
            if (spriteRatio > rectRatio)
            {
                var height = rect.width / spriteRatio;
                rect.y += (rect.height - height) * pivot.y;
                rect.height = height;
            }
            else
            {
                var width = rect.height * spriteRatio;
                rect.x += (rect.width - width) * pivot.x;
                rect.width = width;
            }

            return rect;
        }
    }
}
