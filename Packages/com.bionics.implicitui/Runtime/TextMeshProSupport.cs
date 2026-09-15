namespace ImplicitUI
{
    // Single source of truth for whether this assembly was compiled with TextMeshPro. IMPLICITUI_TMP
    // comes from the asmdef versionDefines: com.unity.ugui 2.0+ (Unity 6, where TMP lives inside uGUI)
    // or com.unity.textmeshpro 3.0+ (older editors, where TMP is its own package).
    internal static class TextMeshProSupport
    {
#if IMPLICITUI_TMP
        internal const bool IsCompiledIn = true;
#else
        internal const bool IsCompiledIn = false;
#endif
    }
}
