using System;
using ImplicitUI.Editor;
using NUnit.Framework;

namespace ImplicitUI.Tests.EditorTests
{
    // The TMP define is set by version rules that differ between Unity lines (TMP moved into uGUI in
    // Unity 6). If a rule stops matching, TMP code disappears silently - this fails instead.
    public class TextMeshProDefineTests
    {
        private static bool TextMeshProLoaded => Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;

        [Test]
        public void RuntimeDefineMatchesTextMeshProPresence()
        {
            Assert.AreEqual(TextMeshProLoaded, TextMeshProSupport.IsCompiledIn,
                "IMPLICITUI_TMP in ImplicitUI.Runtime does not match whether TextMeshPro is installed.");
        }

        [Test]
        public void EditorDefineMatchesTextMeshProPresence()
        {
            Assert.AreEqual(TextMeshProLoaded, EditorTextMeshProSupport.IsCompiledIn,
                "IMPLICITUI_TMP in ImplicitUI.Editor does not match whether TextMeshPro is installed.");
        }
    }
}
