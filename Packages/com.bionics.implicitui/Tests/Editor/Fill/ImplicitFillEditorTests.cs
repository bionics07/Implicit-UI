using ImplicitUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    // The inspector edits the Image through serialized field names that Unity does not promise to keep; these
    // fail on the Unity line where a name changes instead of leaving a blank inspector.
    public class ImplicitFillEditorTests
    {
        private GameObject m_GameObject;

        [SetUp]
        public void SetUp()
        {
            m_GameObject = new GameObject("Implicit Fill", typeof(RectTransform));
            m_GameObject.AddComponent<ImplicitFill>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_GameObject);
        }

        [TestCase(ImplicitFillEditor.FillMethodProperty)]
        [TestCase(ImplicitFillEditor.FillOriginProperty)]
        [TestCase(ImplicitFillEditor.FillAmountProperty)]
        [TestCase(ImplicitFillEditor.FillClockwiseProperty)]
        public void ImageHasTheSerializedFieldTheInspectorEdits(string propertyName)
        {
            var image = new SerializedObject(m_GameObject.GetComponent<Image>());

            Assert.That(image.FindProperty(propertyName), Is.Not.Null);
        }

        [Test]
        public void EditingTheSerializedFillFieldsReachesTheImage()
        {
            var image = m_GameObject.GetComponent<Image>();
            var serialized = new SerializedObject(image);

            serialized.FindProperty(ImplicitFillEditor.FillMethodProperty).intValue = (int)Image.FillMethod.Vertical;
            serialized.FindProperty(ImplicitFillEditor.FillOriginProperty).intValue = (int)Image.OriginVertical.Top;
            serialized.FindProperty(ImplicitFillEditor.FillAmountProperty).floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(image.fillMethod, Is.EqualTo(Image.FillMethod.Vertical));
            Assert.That(image.fillOrigin, Is.EqualTo((int)Image.OriginVertical.Top));
            Assert.That(image.fillAmount, Is.EqualTo(0.25f));
        }

        [Test]
        public void ImplicitFillUsesItsOwnInspector()
        {
            var editor = UnityEditor.Editor.CreateEditor(m_GameObject.GetComponent<ImplicitFill>());
            try
            {
                Assert.That(editor, Is.InstanceOf<ImplicitFillEditor>());
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void AddingImplicitFillBringsAnImage()
        {
            Assert.That(m_GameObject.GetComponent<Image>(), Is.Not.Null);
        }
    }
}
