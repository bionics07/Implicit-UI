using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    public class ImplicitGrayscaleTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<Object> m_Created = new List<Object>();
        private Canvas m_Canvas;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Canvas.gameObject);
            foreach (var created in m_Created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            m_Created.Clear();
        }

        [Test]
        public void NewComponentLeavesTheImageInItsColors()
        {
            var (_, grayscale, capture) = CreateGrayImage(m_Canvas.transform);

            Assert.That(grayscale.Intensity, Is.Zero);
            Assert.That(grayscale.GrayWhenDisabled, Is.True);
            AssertUv0(capture, 0f, 0f);
        }

        [Test]
        public void IntensityAndTintIntensityAreWrittenIntoUv0()
        {
            var (_, grayscale, capture) = CreateGrayImage(m_Canvas.transform);

            grayscale.Intensity = 0.4f;
            grayscale.TintIntensity = 0.3f;

            AssertUv0(capture, 0.4f, 0.3f);
        }

        [Test]
        public void DisabledButtonTurnsItFullyGray()
        {
            var (image, grayscale, capture) = CreateGrayImage(m_Canvas.transform);
            var button = image.gameObject.AddComponent<Button>();
            Canvas.ForceUpdateCanvases();

            button.interactable = false;
            grayscale.RefreshIntensity();

            Assert.That(grayscale.EffectiveIntensity, Is.EqualTo(1f));
            AssertUv0(capture, 1f, 0f);
        }

        [Test]
        public void EnablingTheButtonAgainRestoresTheColors()
        {
            var (image, grayscale, capture) = CreateGrayImage(m_Canvas.transform);
            var button = image.gameObject.AddComponent<Button>();
            button.interactable = false;
            grayscale.RefreshIntensity();
            AssertUv0(capture, 1f, 0f);

            button.interactable = true;
            grayscale.RefreshIntensity();

            AssertUv0(capture, 0f, 0f);
        }

        [Test]
        public void DisabledButtonAboveTurnsItGray()
        {
            var parent = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 80f));
            var button = parent.gameObject.AddComponent<Button>();
            var (_, grayscale, capture) = CreateGrayImage(parent.transform);

            button.interactable = false;
            grayscale.RefreshIntensity();

            AssertUv0(capture, 1f, 0f);
        }

        [Test]
        public void NonInteractableCanvasGroupTurnsItGray()
        {
            var group = new GameObject("Group", typeof(RectTransform)).AddComponent<CanvasGroup>();
            group.transform.SetParent(m_Canvas.transform, false);
            var (image, grayscale, capture) = CreateGrayImage(group.transform);
            image.gameObject.AddComponent<Button>();
            Canvas.ForceUpdateCanvases();

            group.interactable = false;
            grayscale.RefreshIntensity();

            Assert.That(grayscale.EffectiveIntensity, Is.EqualTo(1f));
            AssertUv0(capture, 1f, 0f);
        }

        [Test]
        public void GrayWhenDisabledOffIgnoresTheButton()
        {
            var (image, grayscale, capture) = CreateGrayImage(m_Canvas.transform);
            image.gameObject.AddComponent<Button>().interactable = false;

            grayscale.GrayWhenDisabled = false;
            grayscale.Intensity = 0.2f;
            grayscale.RefreshIntensity();

            AssertUv0(capture, 0.2f, 0f);
        }

        [Test]
        public void ImageIsAtLeastAsGrayAsTheComponentAboveIt()
        {
            var (parent, parentGrayscale, _) = CreateGrayImage(m_Canvas.transform);
            var (_, grayscale, capture) = CreateGrayImage(parent.transform);
            parentGrayscale.Intensity = 0.7f;

            grayscale.Intensity = 0.2f;
            grayscale.RefreshIntensity();
            AssertUv0(capture, 0.7f, 0f);

            grayscale.Intensity = 0.9f;
            AssertUv0(capture, 0.9f, 0f);
        }

        [Test]
        public void ComponentAboveDoesNotGrayImagesWithoutTheirOwn()
        {
            var (parent, parentGrayscale, _) = CreateGrayImage(m_Canvas.transform);
            parentGrayscale.Intensity = 1f;
            var child = UiTestUtility.CreateImage(parent.transform, new Vector2(50f, 20f));
            var capture = child.gameObject.AddComponent<MeshCapture>();
            Canvas.ForceUpdateCanvases();

            Assert.That(child.materialForRendering.shader.name, Is.Not.EqualTo(ImplicitGrayscale.ShaderName));
            AssertUv0(capture, 0f, 0f);
        }

        [Test]
        public void SupportedImageRendersWithTheGrayscaleShader()
        {
            var (image, _, _) = CreateGrayImage(m_Canvas.transform);

            Assert.That(image.materialForRendering.shader.name, Is.EqualTo(ImplicitGrayscale.ShaderName));
        }

        [Test]
        public void ImagesWithTheSameTintShareOneMaterial()
        {
            var (first, firstGrayscale, _) = CreateGrayImage(m_Canvas.transform);
            var (second, secondGrayscale, _) = CreateGrayImage(m_Canvas.transform);
            firstGrayscale.Intensity = 0.2f;
            secondGrayscale.Intensity = 0.8f;

            Assert.That(second.materialForRendering, Is.SameAs(first.materialForRendering));
        }

        [Test]
        public void DifferentTintColorsUseDifferentMaterials()
        {
            var (first, _, _) = CreateGrayImage(m_Canvas.transform);
            var (second, secondGrayscale, _) = CreateGrayImage(m_Canvas.transform);
            var tint = new Color(0.9f, 0.7f, 0.4f);

            secondGrayscale.TintColor = tint;

            Assert.That(second.materialForRendering, Is.Not.SameAs(first.materialForRendering));
            var stored = second.materialForRendering.GetColor("_GrayTint");
            Assert.That(stored.r, Is.EqualTo(tint.r).Within(1e-3f), "red");
            Assert.That(stored.g, Is.EqualTo(tint.g).Within(1e-3f), "green");
            Assert.That(stored.b, Is.EqualTo(tint.b).Within(1e-3f), "blue");
        }

        [Test]
        public void CustomMaterialIsLeftAlone()
        {
            var (image, grayscale, capture) = CreateGrayImage(m_Canvas.transform);
            var custom = Track(new Material(Shader.Find("UI/Default")));
            grayscale.Intensity = 1f;

            image.material = custom;
            Canvas.ForceUpdateCanvases();

            Assert.That(grayscale.HasCustomMaterial, Is.True);
            Assert.That(image.materialForRendering, Is.SameAs(custom));
            AssertUv0(capture, 0f, 0f);
        }

        [Test]
        public void RawImageIsSupported()
        {
            var rawImage = new GameObject("Raw Image", typeof(RectTransform)).AddComponent<RawImage>();
            rawImage.transform.SetParent(m_Canvas.transform, false);
            rawImage.gameObject.AddComponent<ImplicitGrayscale>();

            Assert.That(rawImage.materialForRendering.shader.name, Is.EqualTo(ImplicitGrayscale.ShaderName));
        }

        [Test]
        public void OtherGraphicsAreLeftAlone()
        {
            var graphic = new GameObject("Plain Graphic", typeof(RectTransform)).AddComponent<PlainGraphic>();
            graphic.transform.SetParent(m_Canvas.transform, false);
            graphic.gameObject.AddComponent<ImplicitGrayscale>();

            Assert.That(ImplicitGrayscale.IsSupported(graphic), Is.False);
            Assert.That(graphic.materialForRendering.shader.name, Is.Not.EqualTo(ImplicitGrayscale.ShaderName));
        }

        [Test]
        public void MaskedImageKeepsTheStencilOfTheMask()
        {
            var mask = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(300f, 300f));
            mask.gameObject.AddComponent<Mask>();
            var plain = UiTestUtility.CreateImage(mask.transform, new Vector2(50f, 20f));
            var (gray, _, _) = CreateGrayImage(mask.transform);
            Canvas.ForceUpdateCanvases();

            var plainMaterial = plain.materialForRendering;
            var grayMaterial = gray.materialForRendering;

            Assert.That(plainMaterial.GetFloat("_Stencil"), Is.GreaterThan(0f), "the mask stencils its children");
            Assert.That(grayMaterial.shader.name, Is.EqualTo(ImplicitGrayscale.ShaderName));
            Assert.That(grayMaterial.GetFloat("_Stencil"), Is.EqualTo(plainMaterial.GetFloat("_Stencil")));
            Assert.That(grayMaterial.GetFloat("_StencilComp"), Is.EqualTo(plainMaterial.GetFloat("_StencilComp")));
            Assert.That(grayMaterial.GetFloat("_StencilReadMask"), Is.EqualTo(plainMaterial.GetFloat("_StencilReadMask")));
        }

        [Test]
        public void DisablingTheComponentRestoresTheDefaultMaterial()
        {
            var (image, grayscale, _) = CreateGrayImage(m_Canvas.transform);
            Assert.That(image.materialForRendering.shader.name, Is.EqualTo(ImplicitGrayscale.ShaderName));

            grayscale.enabled = false;

            Assert.That(image.materialForRendering, Is.SameAs(image.defaultMaterial));
        }

        [Test]
        public void ChangingTheIntensityDoesNotRebuildLayout()
        {
            var (image, grayscale, capture) = CreateGrayImage(m_Canvas.transform);
            var counter = image.gameObject.AddComponent<LayoutRebuildCounter>();
            Canvas.ForceUpdateCanvases();
            counter.ResetCount();
            var meshRebuilds = capture.Rebuilds;

            grayscale.Intensity = 0.6f;
            Canvas.ForceUpdateCanvases();

            Assert.That(capture.Rebuilds, Is.GreaterThan(meshRebuilds), "the change rebuilt the mesh");
            Assert.That(counter.Rebuilds, Is.Zero);
        }

        [Test]
        public void AddingTheComponentInTheEditorAssignsTheShader()
        {
            var gameObject = new GameObject("Editor Added", typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(m_Canvas.transform, false);

            // ObjectFactory is the path Add Component takes, and it applies the script's default references.
            var grayscale = ObjectFactory.AddComponent<ImplicitGrayscale>(gameObject);

            Assert.That(grayscale.HasShaderReference, Is.True);
            Assert.That(new SerializedObject(grayscale).FindProperty("m_Shader").objectReferenceValue.name,
                Is.EqualTo(ImplicitGrayscale.ShaderName));
        }

        [Test]
        public void ComponentWithoutAShaderReferenceFindsTheShaderByName()
        {
            var (image, grayscale, _) = CreateGrayImage(m_Canvas.transform);

            // In the Editor even AddComponent applies the default reference, so the case of a component added from
            // code in a player - no reference - is built by clearing the field.
            var serialized = new SerializedObject(grayscale);
            serialized.FindProperty("m_Shader").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            image.SetMaterialDirty();

            Assert.That(grayscale.HasShaderReference, Is.False);
            Assert.That(image.materialForRendering.shader.name, Is.EqualTo(ImplicitGrayscale.ShaderName));
        }

        [Test]
        public void ResettingStaticsKeepsMaterialsThatAreInUse()
        {
            var (image, grayscale, _) = CreateGrayImage(m_Canvas.transform);
            var material = image.materialForRendering;

            ImplicitGrayscale.ResetStatics();
            grayscale.TintColor = Color.white;
            image.SetMaterialDirty();

            Assert.That(material != null, Is.True, "the material survived");
            Assert.That(image.materialForRendering, Is.SameAs(material));
        }

        [Test]
        public void MaterialCacheDropsEntriesWhoseSourceIsGone()
        {
            // The cache is shared by every test; clear what earlier ones left behind before counting.
            GrayscaleMaterials.RemoveDestroyed();
            var shader = Shader.Find(ImplicitGrayscale.ShaderName);
            var source = new Material(Shader.Find("UI/Default"));
            GrayscaleMaterials.Get(source, shader, new Color(0.1f, 0.2f, 0.3f));
            var count = GrayscaleMaterials.Count;

            Object.DestroyImmediate(source);
            GrayscaleMaterials.RemoveDestroyed();

            Assert.That(GrayscaleMaterials.Count, Is.EqualTo(count - 1));
        }

        private (Image image, ImplicitGrayscale grayscale, MeshCapture capture) CreateGrayImage(Transform parent)
        {
            var image = UiTestUtility.CreateImage(parent, new Vector2(100f, 40f));
            var grayscale = image.gameObject.AddComponent<ImplicitGrayscale>();
            var capture = image.gameObject.AddComponent<MeshCapture>();
            return (image, grayscale, capture);
        }

        private T Track<T>(T created) where T : Object
        {
            m_Created.Add(created);
            return created;
        }

        private static void AssertUv0(MeshCapture capture, float expectedZ, float expectedW)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(capture.Triangles, Is.Not.Empty);
            foreach (var vertex in capture.Triangles)
            {
                Assert.That(vertex.uv0.z, Is.EqualTo(expectedZ).Within(Tolerance), "UV0.z");
                Assert.That(vertex.uv0.w, Is.EqualTo(expectedW).Within(Tolerance), "UV0.w");
            }
        }
    }
}
