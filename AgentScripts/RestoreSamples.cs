using System.IO;
using UnityEditor;

// Brings the package samples back under Assets, where their scripts compile again, so BuildSamples can rebuild them.
// It lives apart from BuildSamples because that one refers to the sample types, and those do not exist while the
// samples sit in Samples~ - the file would not compile exactly when it is needed.
//
// Unity Pipeline: run_script --file AgentScripts/RestoreSamples.cs --entry RestoreSamples.Run
public static class RestoreSamples
{
    private const string BuildFolder = "Assets/_SamplesBuild";
    private const string SamplesFolder = "Packages/com.bionics.implicitui/Samples~";

    public static string Run()
    {
        if (!Directory.Exists(SamplesFolder))
            return "Nothing in " + SamplesFolder;

        var names = "";
        foreach (var source in Directory.GetDirectories(SamplesFolder))
        {
            var name = Path.GetFileName(source);
            var target = Path.Combine(BuildFolder, name);
            if (Directory.Exists(target))
                Directory.Delete(target, true);

            Directory.CreateDirectory(BuildFolder);
            Directory.Move(source, target);
            names += name + ", ";
        }

        AssetDatabase.Refresh();
        return "Restored under " + BuildFolder + ": " + names.TrimEnd(',', ' ');
    }
}
