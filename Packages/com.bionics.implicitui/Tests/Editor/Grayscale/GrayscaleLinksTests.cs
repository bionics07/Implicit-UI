using System.Collections.Generic;
using ImplicitUI.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests.EditorTests
{
    // What the inspector reports about objects that drive each other must match what the component does.
    public class GrayscaleLinksTests
    {
        private readonly List<ImplicitGrayscale> m_Result = new List<ImplicitGrayscale>();
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
        }

        [Test]
        public void ChildOfAButtonReportsThatButton()
        {
            var parent = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 60f));
            var button = parent.gameObject.AddComponent<Button>();
            var child = Gray(parent.transform);

            Assert.That(GrayscaleLinks.ControllingSelectable(child), Is.SameAs(button));
        }

        [Test]
        public void ButtonOnTheSameObjectIsNotReportedAsAnotherObject()
        {
            var grayscale = Gray(m_Canvas.transform);
            grayscale.gameObject.AddComponent<Button>();

            Assert.That(GrayscaleLinks.ControllingSelectable(grayscale), Is.Null);
        }

        [Test]
        public void GrayWhenDisabledOffReportsNoButton()
        {
            var parent = UiTestUtility.CreateImage(m_Canvas.transform, new Vector2(200f, 60f));
            parent.gameObject.AddComponent<Button>();
            var child = Gray(parent.transform);

            child.GrayWhenDisabled = false;

            Assert.That(GrayscaleLinks.ControllingSelectable(child), Is.Null);
        }

        [Test]
        public void ButtonListsTheImagesBelowThatFollowIt()
        {
            var parent = Gray(m_Canvas.transform);
            parent.gameObject.AddComponent<Button>();
            var follows = Gray(parent.transform);
            var ignores = Gray(parent.transform);
            ignores.GrayWhenDisabled = false;
            var grandchild = Gray(follows.transform);

            GrayscaleLinks.ChildrenFollowingSelectable(parent, m_Result);

            Assert.That(m_Result, Is.EquivalentTo(new[] { follows, grandchild }));
        }

        [Test]
        public void ImagesUnderAnotherButtonAreNotListed()
        {
            var parent = Gray(m_Canvas.transform);
            parent.gameObject.AddComponent<Button>();
            var direct = Gray(parent.transform);
            var inner = Gray(parent.transform);
            inner.gameObject.AddComponent<Button>();
            Gray(inner.transform);

            GrayscaleLinks.ChildrenFollowingSelectable(parent, m_Result);

            // inner and the image under it follow inner's own button.
            Assert.That(m_Result, Is.EquivalentTo(new[] { direct }));
        }

        [Test]
        public void ObjectWithoutAButtonListsNoFollowers()
        {
            var parent = Gray(m_Canvas.transform);
            Gray(parent.transform);

            GrayscaleLinks.ChildrenFollowingSelectable(parent, m_Result);

            Assert.That(m_Result, Is.Empty);
        }

        [Test]
        public void ChildReportsTheNearestGrayscaleAbove()
        {
            var top = Gray(m_Canvas.transform);
            var middle = Gray(top.transform);
            var bottom = Gray(middle.transform);

            Assert.That(GrayscaleLinks.ControllingAncestor(bottom), Is.SameAs(middle));
            Assert.That(GrayscaleLinks.ControllingAncestor(top), Is.Null);
        }

        [Test]
        public void GrayscaleListsOnlyTheImagesItKeepsDirectly()
        {
            var top = Gray(m_Canvas.transform);
            var middle = Gray(top.transform);
            Gray(middle.transform);

            GrayscaleLinks.ChildrenKeptAsGray(top, m_Result);

            Assert.That(m_Result, Is.EquivalentTo(new[] { middle }));
        }

        [Test]
        public void DisabledGrayscaleAboveIsNotReported()
        {
            var top = Gray(m_Canvas.transform);
            var child = Gray(top.transform);

            top.enabled = false;

            Assert.That(GrayscaleLinks.ControllingAncestor(child), Is.Null);
            GrayscaleLinks.ChildrenKeptAsGray(top, m_Result);
            Assert.That(m_Result, Is.Empty);
        }

        [Test]
        public void ChildFollowingBothRulesIsListedOnce()
        {
            var parent = Gray(m_Canvas.transform);
            parent.gameObject.AddComponent<Button>();
            var both = Gray(parent.transform);
            var onlyKept = Gray(parent.transform);
            onlyKept.GrayWhenDisabled = false;
            var affected = new List<GrayscaleLinks.AffectedChild>();

            GrayscaleLinks.AffectedChildren(parent, affected);

            Assert.That(affected.Count, Is.EqualTo(2));
            var first = affected.Find(entry => entry.Grayscale == both);
            Assert.That(first.FollowsSelectable && first.KeptAsGray, Is.True);
            var second = affected.Find(entry => entry.Grayscale == onlyKept);
            Assert.That(!second.FollowsSelectable && second.KeptAsGray, Is.True);
        }

        private static ImplicitGrayscale Gray(Transform parent)
        {
            return UiTestUtility.CreateImage(parent, new Vector2(100f, 40f)).gameObject.AddComponent<ImplicitGrayscale>();
        }
    }
}
