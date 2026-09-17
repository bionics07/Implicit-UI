using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    // The component does not run in Edit Mode, so these tests call ApplyPadding, RestorePadding and the raycast filter
    // directly. The player loop driving them is covered by ImplicitHitboxPlayModeTests.
    public class ImplicitHitboxTests
    {
        private Canvas m_Canvas;
        private Texture2D m_Texture;
        private Sprite m_Sprite;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            m_Canvas.scaleFactor = 1f;
            ImplicitHitbox.s_DpiOverride = ImplicitHitbox.BaseDpi;
        }

        [TearDown]
        public void TearDown()
        {
            ImplicitHitbox.s_DpiOverride = -1f;
            Object.DestroyImmediate(m_Canvas.gameObject);
            if (m_Sprite != null)
                Object.DestroyImmediate(m_Sprite);
            if (m_Texture != null)
                Object.DestroyImmediate(m_Texture);
        }

        // --- Minimum size -------------------------------------------------------------------------------------------

        [Test]
        public void SmallAxisGrowsEquallyOnBothSides()
        {
            var padding = ImplicitHitbox.ComputePadding(Vector4.zero, new Vector2(20f, 100f), new Vector2(48f, 48f));

            Assert.That(padding, Is.EqualTo(new Vector4(-14f, 0f, -14f, 0f)));
        }

        [Test]
        public void LargeEnoughAxesKeepTheirPadding()
        {
            var basePadding = new Vector4(2f, 3f, 4f, 5f);

            var padding = ImplicitHitbox.ComputePadding(basePadding, new Vector2(100f, 100f), new Vector2(48f, 48f));

            Assert.That(padding, Is.EqualTo(basePadding));
        }

        [Test]
        public void PaddingThatAlreadyReachesTheMinimumWins()
        {
            // 20 wide with 20 of padding on each side is 60, more than the minimum: the larger area is kept.
            var basePadding = new Vector4(-20f, 0f, -20f, 0f);

            var padding = ImplicitHitbox.ComputePadding(basePadding, new Vector2(20f, 60f), new Vector2(48f, 48f));

            Assert.That(padding, Is.EqualTo(basePadding));
        }

        [Test]
        public void MinimumGrowsFromAsymmetricPadding()
        {
            // Left grows the area by 10, right shrinks it by 2: 20 + 10 - 2 = 28, so 20 more is split evenly.
            var basePadding = new Vector4(-10f, 0f, 2f, 0f);

            var padding = ImplicitHitbox.ComputePadding(basePadding, new Vector2(20f, 60f), new Vector2(48f, 48f));

            Assert.That(padding, Is.EqualTo(new Vector4(-20f, 0f, -8f, 0f)));
        }

        [Test]
        public void DpBecomeRectUnitsThroughDensityAndCanvasScale()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 20f));
            m_Canvas.scaleFactor = 2f;
            image.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            Canvas.ForceUpdateCanvases();

            // 48 dp at 320 dpi is 96 pixels; each rect unit covers 2 * 1.5 = 3 pixels.
            var units = ImplicitHitbox.MinimumSizeInRectUnits(image.rectTransform, m_Canvas, 48f, 320f);

            Assert.That(units.x, Is.EqualTo(32f).Within(1e-3f));
            Assert.That(units.y, Is.EqualTo(32f).Within(1e-3f));
        }

        [Test]
        public void WorldSpaceCanvasHasNoMinimum()
        {
            m_Canvas.renderMode = RenderMode.WorldSpace;
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 20f));

            var units = ImplicitHitbox.MinimumSizeInRectUnits(image.rectTransform, m_Canvas, 48f, 160f);

            Assert.That(units, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ApplyWritesTheGrownPaddingAndRestoreBringsBackTheOriginal()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 60f));
            image.raycastPadding = new Vector4(0f, 1f, 0f, 1f);
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();

            hitbox.ApplyPadding();
            Assert.That(image.raycastPadding, Is.EqualTo(new Vector4(-14f, 1f, -14f, 1f)));

            hitbox.RestorePadding();
            Assert.That(image.raycastPadding, Is.EqualTo(new Vector4(0f, 1f, 0f, 1f)));
        }

        [Test]
        public void PaddingChangedByAnotherScriptBecomesTheNewBase()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 60f));
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();
            hitbox.ApplyPadding();

            image.raycastPadding = new Vector4(0f, -5f, 0f, -5f);
            hitbox.ApplyPadding();
            Assert.That(image.raycastPadding, Is.EqualTo(new Vector4(-14f, -5f, -14f, -5f)));

            hitbox.RestorePadding();
            Assert.That(image.raycastPadding, Is.EqualTo(new Vector4(0f, -5f, 0f, -5f)));
        }

        [Test]
        public void DensityChangeUpdatesThePadding()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 60f));
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();
            hitbox.ApplyPadding();

            ImplicitHitbox.s_DpiOverride = 320f;
            hitbox.ApplyPadding();

            // 48 dp at 320 dpi is 96 units at scale 1: the width grows 76 (38 per side), and the height of 60, fine at
            // 160 dpi, now grows 36 (18 per side).
            Assert.That(image.raycastPadding, Is.EqualTo(new Vector4(-38f, -18f, -38f, -18f)));
        }

        [Test]
        public void TurningOffMinimumSizeWritesTheBasePadding()
        {
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 60f));
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();
            hitbox.ApplyPadding();

            hitbox.UseMinimumSize = false;
            hitbox.ApplyPadding();

            Assert.That(image.raycastPadding, Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void DestroyedHitboxHasNoGraphicInsteadOfThrowing()
        {
            // Leaving Play Mode destroys the component while the inspector still repaints it once.
            var image = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(20f, 20f));
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();

            Object.DestroyImmediate(hitbox);

            Assert.That(() => hitbox.Graphic, Throws.Nothing);
            Assert.That(hitbox.Graphic == null, Is.True);
        }

        // --- Alpha hit test -----------------------------------------------------------------------------------------

        [Test]
        public void TransparentPixelIsNotHitAndOpaquePixelIs()
        {
            // Left half transparent, right half opaque.
            var hitbox = AlphaImage(CreateTexture(8, 8, x => x >= 4), new Rect(0, 0, 8, 8), new Vector2(80f, 80f));

            Assert.That(Hits(hitbox, new Vector2(-20f, 0f)), Is.False);
            Assert.That(Hits(hitbox, new Vector2(20f, 0f)), Is.True);
        }

        [Test]
        public void PaddingAreaAroundTheImageIsAlwaysHit()
        {
            var hitbox = AlphaImage(CreateTexture(8, 8, x => false), new Rect(0, 0, 8, 8), new Vector2(80f, 80f));

            Assert.That(Hits(hitbox, new Vector2(-50f, 0f)), Is.True);
            Assert.That(Hits(hitbox, new Vector2(0f, 0f)), Is.False);
        }

        [Test]
        public void ThresholdZeroTurnsTheTestOff()
        {
            var hitbox = AlphaImage(CreateTexture(8, 8, x => false), new Rect(0, 0, 8, 8), new Vector2(80f, 80f));

            hitbox.AlphaThreshold = 0f;

            Assert.That(Hits(hitbox, Vector2.zero), Is.True);
        }

        [Test]
        public void SpriteOverPartOfATextureReadsItsOwnPixels()
        {
            // Columns 4-5 transparent, the rest opaque; the sprite covers columns 4-7, like a sprite inside an atlas.
            var hitbox = AlphaImage(CreateTexture(8, 8, x => x < 4 || x >= 6), new Rect(4, 0, 4, 8),
                new Vector2(80f, 80f));

            Assert.That(Hits(hitbox, new Vector2(-20f, 0f)), Is.False);
            Assert.That(Hits(hitbox, new Vector2(20f, 0f)), Is.True);
        }

        [Test]
        public void SlicedImageKeepsItsBordersAtSpriteSize()
        {
            // 12 pixels, borders of 4 transparent, center opaque. At 100 pixels per unit on a 100 reference canvas, a
            // border is 4 units wide whatever the image size, so 40 units in is well into the center. Stretched like a
            // Simple image, 40 of 300 units would still be border.
            var hitbox = AlphaImage(CreateTexture(12, 12, x => x >= 4 && x < 8), new Rect(0, 0, 12, 12),
                new Vector2(300f, 100f), new Vector4(4, 0, 4, 0));
            var image = (Image)hitbox.Graphic;
            image.type = Image.Type.Sliced;

            Assert.That(Hits(hitbox, new Vector2(-150f + 2f, 0f)), Is.False);
            Assert.That(Hits(hitbox, new Vector2(-150f + 40f, 0f)), Is.True);
        }

        [Test]
        public void TiledImageRepeatsTheSprite()
        {
            // 12 pixels without borders: each tile is 12 units, its left half transparent.
            var hitbox = AlphaImage(CreateTexture(12, 12, x => x >= 6), new Rect(0, 0, 12, 12), new Vector2(120f, 120f));
            var image = (Image)hitbox.Graphic;
            image.type = Image.Type.Tiled;

            Assert.That(Hits(hitbox, new Vector2(-60f + 36f + 3f, 0f)), Is.False);
            Assert.That(Hits(hitbox, new Vector2(-60f + 36f + 9f, 0f)), Is.True);
        }

        [Test]
        public void PreserveAspectLeavesTheUndrawnSidesUnhit()
        {
            var hitbox = AlphaImage(CreateTexture(12, 12, x => true), new Rect(0, 0, 12, 12), new Vector2(300f, 100f));
            ((Image)hitbox.Graphic).preserveAspect = true;

            Assert.That(Hits(hitbox, new Vector2(-140f, 0f)), Is.False);
            Assert.That(Hits(hitbox, Vector2.zero), Is.True);
        }

        [Test]
        public void UnreadableTextureIsHitAndWarnsOnce()
        {
            var texture = CreateTexture(8, 8, x => false);
            texture.Apply(false, true);
            var hitbox = AlphaImage(texture, new Rect(0, 0, 8, 8), new Vector2(80f, 80f));

            LogAssert.Expect(LogType.Warning, new Regex("cannot read its sprite's alpha"));
            Assert.That(Hits(hitbox, Vector2.zero), Is.True);
            Assert.That(Hits(hitbox, Vector2.zero), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GraphicThatIsNotAnImageIsAlwaysHit()
        {
            var gameObject = new GameObject("Plain", typeof(RectTransform));
            gameObject.transform.SetParent(m_Canvas.transform, false);
            gameObject.AddComponent<PlainGraphic>();
            var hitbox = gameObject.AddComponent<ImplicitHitbox>();
            hitbox.AlphaThreshold = 0.5f;

            Assert.That(Hits(hitbox, Vector2.zero), Is.True);
        }

        private ImplicitHitbox AlphaImage(Texture2D texture, Rect spriteRect, Vector2 size, Vector4 border = default)
        {
            m_Texture = texture;
            m_Sprite = Sprite.Create(texture, spriteRect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                border);
            var image = UiTestUtility.CreateImage(m_Canvas.transform, size);
            image.sprite = m_Sprite;
            var hitbox = image.gameObject.AddComponent<ImplicitHitbox>();
            hitbox.AlphaThreshold = 0.5f;
            return hitbox;
        }

        // Opaque where the column passes, transparent elsewhere.
        private static Texture2D CreateTexture(int width, int height, System.Func<int, bool> opaqueColumn)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (var x = 0; x < width; x++)
            {
                var color = opaqueColumn(x) ? Color.white : new Color(1f, 1f, 1f, 0f);
                for (var y = 0; y < height; y++)
                    texture.SetPixel(x, y, color);
            }

            texture.Apply();
            return texture;
        }

        // Point is in the graphic's local space; the canvas is Screen Space - Overlay, so no camera.
        private static bool Hits(ImplicitHitbox hitbox, Vector2 local)
        {
            var world = hitbox.Graphic.rectTransform.TransformPoint(local);
            var screen = RectTransformUtility.WorldToScreenPoint(null, world);
            return hitbox.IsRaycastLocationValid(screen, null);
        }
    }
}
