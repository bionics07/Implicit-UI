using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // The Edit Mode tests call RefreshIfChanged directly. This one leaves it to the player loop: nothing tells the group
    // that a descendant's text changed, so it has to notice on its own within a frame.
    public class ImplicitTextSizeGroupPlayModeTests
    {
        private Canvas m_Canvas;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(m_Canvas.gameObject);
        }

        [UnityTest]
        public IEnumerator ChangingATextAtRuntimeUpdatesTheGroupWithinAFrame()
        {
            m_Canvas = UiTestUtility.CreateCanvas();
            var font = UiTestUtility.BuiltinFont();
            var ok = Text(m_Canvas.transform, font, "OK");
            var back = Text(m_Canvas.transform, font, "OK");
            var group = m_Canvas.gameObject.AddComponent<ImplicitTextSizeGroup>();
            yield return null;
            var sameText = group.GroupSize;

            back.text = "Return to main menu";
            yield return null;

            Assert.That(group.GroupSize, Is.LessThan(sameText));
            Assert.That(ok.resizeTextMaxSize, Is.EqualTo((int)group.GroupSize));
        }

        private static Text Text(Transform parent, Font font, string content)
        {
            var gameObject = new GameObject(content, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            ((RectTransform)gameObject.transform).sizeDelta = new Vector2(200f, 50f);
            var text = gameObject.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 72;
            return text;
        }
    }
}
