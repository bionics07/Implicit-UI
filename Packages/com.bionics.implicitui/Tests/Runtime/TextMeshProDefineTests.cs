using System;
using NUnit.Framework;

namespace ImplicitUI.Tests
{
    // The TMP define is set by version rules that differ between Unity lines (TMP moved into uGUI in
    // Unity 6). If a rule stops matching, TMP code disappears silently - this fails instead.
    public class TextMeshProDefineTests
    {
        [Test]
        public void RuntimeDefineMatchesTextMeshProPresence()
        {
            var textMeshProLoaded = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro") != null;

            Assert.AreEqual(textMeshProLoaded, TextMeshProSupport.IsCompiledIn,
                "IMPLICITUI_TMP in ImplicitUI.Runtime does not match whether TextMeshPro is installed.");
        }
    }
}
