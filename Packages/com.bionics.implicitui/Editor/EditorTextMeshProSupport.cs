namespace ImplicitUI.Editor
{
    // Editor-assembly twin of ImplicitUI.TextMeshProSupport. Each asmdef evaluates its own versionDefines,
    // so the editor side can drift from the runtime side if only one of them is edited.
    internal static class EditorTextMeshProSupport
    {
#if IMPLICITUI_TMP
        internal const bool IsCompiledIn = true;
#else
        internal const bool IsCompiledIn = false;
#endif
    }
}
