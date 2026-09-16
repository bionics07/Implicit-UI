using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds a sandbox scene for a real device with the settings a shipped mobile game uses: IL2CPP, ARM64 and high managed
// stripping. It changes Player Settings, so run it on a throwaway copy of the project, never on the development one.
//
// Copy into the copy's Assets/Editor, then:
//   Unity -batchmode -quit -projectPath <copy> -buildTarget Android -executeMethod BuildDeviceSandbox.Android
//     -buildOutput <path.apk> -sandboxScene Assets/Sandbox/ImplicitGrayscaleSandbox.unity
public static class BuildDeviceSandbox
{
    public static void Android()
    {
        var output = Argument("-buildOutput");
        var scene = Argument("-sandboxScene");

        var target = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
        PlayerSettings.SetApplicationIdentifier(target, "com.bionics.implicitui.sandbox");
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.productName = "Implicit UI Sandbox";
        // Sandbox scenes are laid out for a wide screen; in portrait the right half is cut off.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.buildAppBundle = false;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scene },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log("[Implicit UI] Build " + summary.result + ", " + summary.totalErrors + " errors, " +
                  summary.totalWarnings + " warnings, " + summary.totalSize + " bytes: " + output);

        foreach (var file in report.packedAssets)
        {
            foreach (var content in file.contents)
            {
                if (content.sourceAssetPath.Contains("com.bionics.implicitui"))
                    Debug.Log("[Implicit UI] Packed " + content.type + " from " + content.sourceAssetPath);
            }
        }

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string Argument(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }

        throw new ArgumentException("Missing " + name);
    }
}
