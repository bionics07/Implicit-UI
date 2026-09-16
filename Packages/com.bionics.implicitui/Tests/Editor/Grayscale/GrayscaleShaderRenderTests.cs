using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    // Renders a solid red image through the grayscale shader into a render texture and reads the pixel back. This is
    // the proof that UV0.z and UV0.w survive the Canvas all the way to the shader: if the Canvas dropped them, every
    // case would come back red.
    public class GrayscaleShaderRenderTests
    {
        private const string ShaderName = "Bionics/ImplicitUI/Grayscale";
        private const int Size = 32;

        private RenderTexture m_Target;
        private Camera m_Camera;
        private Canvas m_Canvas;
        private Image m_Image;
        private Material m_Material;
        private Uv0ChannelWriter m_Writer;

        [SetUp]
        public void SetUp()
        {
            m_Target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            m_Camera = new GameObject("Test Camera").AddComponent<Camera>();
            m_Camera.orthographic = true;
            m_Camera.clearFlags = CameraClearFlags.SolidColor;
            m_Camera.backgroundColor = Color.black;
            m_Camera.targetTexture = m_Target;
            m_Camera.transform.position = new Vector3(0f, 0f, -10f);

            m_Canvas = new GameObject("Test Canvas", typeof(RectTransform)).AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            m_Canvas.worldCamera = m_Camera;
            m_Canvas.planeDistance = 5f;

            m_Image = UiTestUtility.CreateImage(m_Canvas.transform, Vector2.zero);
            var rect = (RectTransform)m_Image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            m_Image.color = Color.red;

            var shader = Shader.Find(ShaderName);
            Assert.That(shader, Is.Not.Null, "shader " + ShaderName + " was not found");
            Assert.That(shader.isSupported, Is.True, "shader " + ShaderName + " is not supported here");
            m_Material = new Material(shader);
            m_Image.material = m_Material;

            m_Writer = m_Image.gameObject.AddComponent<Uv0ChannelWriter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Canvas.gameObject);
            Object.DestroyImmediate(m_Camera.gameObject);
            Object.DestroyImmediate(m_Material);
            m_Target.Release();
            Object.DestroyImmediate(m_Target);
        }

        [Test]
        public void ZeroAmountKeepsTheOriginalColor()
        {
            var color = Render(0f, 0f, Color.white);

            Assert.That(color.r, Is.GreaterThan(0.9f), "red channel");
            Assert.That(color.g, Is.LessThan(0.05f), "green channel");
            Assert.That(color.b, Is.LessThan(0.05f), "blue channel");
        }

        [Test]
        public void FullAmountTurnsTheColorGray()
        {
            var color = Render(1f, 0f, Color.white);

            // Red has a luminance of 0.299: every channel lands on that same value.
            Assert.That(color.r, Is.EqualTo(0.299f).Within(0.03f), "red channel");
            Assert.That(color.g, Is.EqualTo(color.r).Within(0.02f), "green matches red");
            Assert.That(color.b, Is.EqualTo(color.r).Within(0.02f), "blue matches red");
        }

        [Test]
        public void HalfAmountBlendsTowardGray()
        {
            var color = Render(0.5f, 0f, Color.white);

            Assert.That(color.r, Is.EqualTo((1f + 0.299f) * 0.5f).Within(0.03f), "red channel");
            Assert.That(color.g, Is.EqualTo(0.299f * 0.5f).Within(0.03f), "green channel");
        }

        [Test]
        public void TintAmountColorsTheGray()
        {
            var color = Render(1f, 1f, new Color(0f, 0f, 1f, 1f));

            Assert.That(color.b, Is.EqualTo(0.299f).Within(0.03f), "blue carries the luminance");
            Assert.That(color.r, Is.LessThan(0.05f), "red channel");
            Assert.That(color.g, Is.LessThan(0.05f), "green channel");
        }

        [Test]
        public void ImageOnADisabledButtonRendersGrayThroughTheComponent()
        {
            // End to end with the real component: default material, no writer, gray coming from the button state.
            Object.DestroyImmediate(m_Writer);
            m_Image.material = null;
            var grayscale = m_Image.gameObject.AddComponent<ImplicitGrayscale>();
            var button = m_Image.gameObject.AddComponent<Button>();
            // A Button's default color transition darkens and fades a disabled image; only the gray is under test.
            button.transition = Selectable.Transition.None;
            button.interactable = false;
            grayscale.RefreshIntensity();

            var color = RenderFrame();

            Assert.That(color.r, Is.EqualTo(0.299f).Within(0.03f), "red channel");
            Assert.That(color.g, Is.EqualTo(color.r).Within(0.02f), "green matches red");
            Assert.That(color.b, Is.EqualTo(color.r).Within(0.02f), "blue matches red");
        }

        private Color Render(float amount, float tintAmount, Color grayTint)
        {
            m_Material.SetColor("_GrayTint", grayTint);
            m_Writer.Z = amount;
            m_Writer.W = tintAmount;
            m_Image.SetVerticesDirty();
            m_Image.SetMaterialDirty();
            return RenderFrame();
        }

        private Color RenderFrame()
        {
            Canvas.ForceUpdateCanvases();
            m_Camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = m_Target;
            var readback = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
            try
            {
                readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                readback.Apply();
                return readback.GetPixel(Size / 2, Size / 2);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }
    }
}
