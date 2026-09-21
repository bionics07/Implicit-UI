using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

// CI host only - never part of the package. TextMeshPro sizes text only with a font, and its default font comes from the
// TMP Essential Resources, which a fresh project has not imported. Without them every TextMeshPro test would have to be
// skipped, so the host imports them from whichever package carries TextMeshPro on this Unity line: com.unity.textmeshpro
// before Unity 6, com.unity.ugui from Unity 6 on. Nothing happens on the line without TextMeshPro.
[InitializeOnLoad]
internal static class ImportTextMeshProEssentials
{
    private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string EssentialsFile = "Package Resources/TMP Essential Resources.unitypackage";

    static ImportTextMeshProEssentials()
    {
        if (File.Exists(SettingsPath))
            return;

        foreach (var package in new[] { "com.unity.textmeshpro", "com.unity.ugui" })
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + package);
            if (info == null)
                continue;

            var essentials = Path.Combine(info.resolvedPath, EssentialsFile);
            if (!File.Exists(essentials))
                continue;

            // Queued here and finished during the startup import, before the test run starts (checked on 2021.3,
            // 2022.3 and 6000.6). Obsolete from Unity 6.6, but its replacement does not exist on the older lines.
#pragma warning disable CS0618
            AssetDatabase.ImportPackage(essentials, false);
#pragma warning restore CS0618
            Debug.Log("[CI] Importing TMP Essential Resources from " + package);
            return;
        }

        Debug.Log("[CI] No TextMeshPro package with Essential Resources; TextMeshPro tests will be skipped.");
    }
}
