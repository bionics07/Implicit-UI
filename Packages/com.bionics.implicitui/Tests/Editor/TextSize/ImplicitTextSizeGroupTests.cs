using ImplicitUI.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
#if IMPLICITUI_TMP
using TMPro;
#endif

namespace ImplicitUI.Tests.EditorTests
{
    // The group runs in Edit Mode, so these tests drive it directly with RefreshIfChanged. In a 200x50 box with best fit
    // from 10 to 72, "OK" picks 44 on its own and "Return to main menu" picks 22.
    public class ImplicitTextSizeGroupTests
    {
        private const string Short = "OK";
        private const string Long = "Return to main menu";

        private Canvas m_Canvas;
        private Font m_Font;

        [SetUp]
        public void SetUp()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            m_Canvas.scaleFactor = 1f;
            m_Font = UiTestUtility.BuiltinFont();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Canvas.gameObject);
        }

        [Test]
        public void TextsShareTheSmallestSizeTheyPickAlone()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var back = Legacy(root, Long);
            Assert.That(Resolved(ok), Is.GreaterThan(Resolved(back)), "premise: alone, OK is larger");

            var group = Group(root);

            Assert.That(group.GroupSize, Is.EqualTo(Resolved(back)));
            Assert.That(Resolved(ok), Is.EqualTo(Resolved(back)));
            Assert.That(ok.resizeTextForBestFit, Is.True, "best fit stays on");
        }

        [Test]
        public void TextsDeeperInTheHierarchyTakePart()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(Node(root, "Button A"), Short);
            var back = Legacy(Node(Node(root, "Row"), "Button B"), Long);

            Group(root);

            Assert.That(Resolved(ok), Is.EqualTo(Resolved(back)));
        }

        [Test]
        public void DisablingTheGroupRestoresEveryMaximum()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            Legacy(root, Long);
            var group = Group(root);
            Assert.That(ok.resizeTextMaxSize, Is.Not.EqualTo(72));

            group.enabled = false;

            Assert.That(ok.resizeTextMaxSize, Is.EqualTo(72));
        }

        [Test]
        public void ExcludedTextKeepsItsSizeAndDoesNotCount()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var okAlone = Resolved(ok);
            var back = Legacy(root, Long);
            var group = Group(root);

            group.SetExcluded(back, true);

            Assert.That(group.GroupSize, Is.EqualTo(okAlone));
            Assert.That(back.resizeTextMaxSize, Is.EqualTo(72));
            Assert.That(Resolved(ok), Is.EqualTo(okAlone));
        }

        [Test]
        public void InactiveTextIsLeftOut()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var okAlone = Resolved(ok);
            var back = Legacy(root, Long);
            back.gameObject.SetActive(false);

            var group = Group(root);

            Assert.That(group.GroupSize, Is.EqualTo(okAlone));
            Assert.That(back.resizeTextMaxSize, Is.EqualTo(72));
        }

        [Test]
        public void EmptyTextIsLeftOut()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var okAlone = Resolved(ok);
            Legacy(root, "");

            var group = Group(root);

            Assert.That(group.GroupSize, Is.EqualTo(okAlone));
        }

        [Test]
        public void FixedSizeTextIsLeftOutUnlessIncluded()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var fixedText = Legacy(root, "Title");
            fixedText.resizeTextForBestFit = false;
            fixedText.fontSize = 14;
            var group = Group(root);
            var okAlone = Resolved(ok);
            Assert.That(group.GroupSize, Is.EqualTo(okAlone));

            group.IncludeFixedSizeTexts = true;
            group.RefreshIfChanged();

            Assert.That(group.GroupSize, Is.EqualTo(14f));
            Assert.That(Resolved(ok), Is.EqualTo(14f));
            Assert.That(fixedText.fontSize, Is.EqualTo(14), "a fixed-size text is never changed");
        }

        [Test]
        public void TextNeverGoesBelowItsOwnMinimum()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            ok.resizeTextMinSize = 30;
            Legacy(root, Long);

            var group = Group(root);

            Assert.That(ok.resizeTextMaxSize, Is.EqualTo(30));
            Assert.That(StateOf(group, ok), Is.EqualTo(ImplicitTextSizeGroup.EntryState.MinimumAboveGroup));
        }

        [Test]
        public void GroupFurtherDownOwnsItsTexts()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var okAlone = Resolved(ok);
            var inner = Node(root, "Inner");
            var back = Legacy(inner, Long);
            var backAlone = Resolved(back);
            var innerGroup = Group(inner);

            var group = Group(root);

            Assert.That(group.GroupSize, Is.EqualTo(okAlone));
            Assert.That(innerGroup.GroupSize, Is.EqualTo(backAlone));
            Assert.That(Resolved(back), Is.EqualTo(backAlone));
        }

        [Test]
        public void ChangingATextRecomputesTheGroup()
        {
            // Reference sizes, measured outside the group.
            var okAlone = Resolved(Legacy(m_Canvas.transform, Short));
            var goAlone = Resolved(Legacy(m_Canvas.transform, "Go"));
            var root = Node(m_Canvas.transform, "Menu");
            Legacy(root, Short);
            var back = Legacy(root, Long);
            var group = Group(root);

            back.text = "Go";
            group.RefreshIfChanged();

            Assert.That(group.GroupSize, Is.EqualTo(Mathf.Min(okAlone, goAlone)));
        }

        [Test]
        public void MaximumChangedBySomeoneElseBecomesTheNewOriginal()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            Legacy(root, Long);
            var group = Group(root);

            ok.resizeTextMaxSize = 30;
            group.RefreshIfChanged();
            group.enabled = false;

            Assert.That(ok.resizeTextMaxSize, Is.EqualTo(30));
        }

        [Test]
        public void PreviewGuardPutsTheOriginalsBackAndTheGroupAppliesAgain()
        {
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            Legacy(root, Long);
            var group = Group(root);
            var applied = ok.resizeTextMaxSize;

            TextSizeGroupPreviewGuard.RestoreAllGroups();
            Assert.That(ok.resizeTextMaxSize, Is.EqualTo(72), "what a save or Play Mode sees");

            group.RefreshIfChanged();
            Assert.That(ok.resizeTextMaxSize, Is.EqualTo(applied));
        }

        [Test]
        public void CanvasScaleDoesNotChangeTheSizesInTextUnits()
        {
            m_Canvas.scaleFactor = 2f;
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Legacy(root, Short);
            var back = Legacy(root, Long);
            var backAlone = Resolved(back);

            var group = Group(root);

            Assert.That(group.GroupSize, Is.EqualTo(backAlone));
            Assert.That(Resolved(ok), Is.EqualTo(backAlone));
        }

#if IMPLICITUI_TMP
        [Test]
        public void TextMeshProTextsShareTheSmallestSize()
        {
            RequireTmpFont();
            var root = Node(m_Canvas.transform, "Menu");
            var ok = Tmp(root, Short);
            var back = Tmp(root, Long);
            var backAlone = back.fontSize;

            var group = Group(root);
            ok.ForceMeshUpdate();
            back.ForceMeshUpdate();

            Assert.That(group.GroupSize, Is.EqualTo(backAlone).Within(1e-3f));
            Assert.That(ok.fontSize, Is.EqualTo(backAlone).Within(1e-3f));
            Assert.That(ok.enableAutoSizing, Is.True, "auto size stays on");

            group.enabled = false;
            Assert.That(ok.fontSizeMax, Is.EqualTo(72f));
        }

        [Test]
        public void MixedWithLegacyTextTheGroupUsesWholeSizes()
        {
            RequireTmpFont();
            var root = Node(m_Canvas.transform, "Menu");
            var back = Tmp(root, Long);
            var backAlone = back.fontSize;
            Legacy(root, Short);

            var group = Group(root);

            Assert.That(backAlone % 1f, Is.Not.EqualTo(0f), "premise: TextMeshPro picks a fractional size");
            Assert.That(group.GroupSize, Is.EqualTo(Mathf.Floor(backAlone)));
            Assert.That(back.fontSizeMax, Is.EqualTo(Mathf.Floor(backAlone)));
        }

        private TextMeshProUGUI Tmp(Transform parent, string content)
        {
            var text = Box(parent, "TMP " + content).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 72f;
            text.ForceMeshUpdate();
            return text;
        }

        // The CI hosts have no TextMesh Pro Essential Resources, so there is no default font to size with.
        private static void RequireTmpFont()
        {
            TMP_FontAsset font = null;
            try
            {
                font = TMP_Settings.defaultFontAsset;
            }
            catch (System.NullReferenceException)
            {
            }

            if (font == null)
                Assert.Ignore("No default TextMesh Pro font: import TMP Essential Resources to run this test.");
        }
#endif

        private static ImplicitTextSizeGroup Group(Transform root)
        {
            var group = root.gameObject.AddComponent<ImplicitTextSizeGroup>();
            group.RefreshIfChanged();
            return group;
        }

        private static ImplicitTextSizeGroup.EntryState StateOf(ImplicitTextSizeGroup group, Component text)
        {
            foreach (var entry in group.Entries)
            {
                if (entry.Text.Component == text)
                    return entry.State;
            }

            throw new AssertionException("The group did not list " + text.name);
        }

        private Text Legacy(Transform parent, string content)
        {
            var text = Box(parent, "Text " + content).gameObject.AddComponent<Text>();
            text.font = m_Font;
            text.text = content;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 72;
            return text;
        }

        // The size best fit picks, in the text's own units.
        private static float Resolved(Text text)
        {
            Canvas.ForceUpdateCanvases();
            return text.cachedTextGenerator.fontSizeUsedForBestFit / text.pixelsPerUnit;
        }

        private static Transform Node(Transform parent, string name)
        {
            var node = new GameObject(name, typeof(RectTransform)).transform;
            node.SetParent(parent, false);
            return node;
        }

        private static RectTransform Box(Transform parent, string name)
        {
            var rect = (RectTransform)Node(parent, name);
            rect.sizeDelta = new Vector2(200f, 50f);
            return rect;
        }
    }
}
