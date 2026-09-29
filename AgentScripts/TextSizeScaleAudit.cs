using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Compares the two ways of deciding a size group's cap, at several canvas scales:
//   A - measure at the live canvas scale and convert back (what 1.0.0 does)
//   B - measure once at scale 1, in text units
// For each scale it prints the pixel size every text ends up rendering under each cap, next to the ideal: the size the
// smallest text picks on its own. Equal pixels across the texts is the group's whole promise. Maintainer diagnostic.
//
// Unity Pipeline: run_script --file AgentScripts/TextSizeScaleAudit.cs --entry TextSizeScaleAudit.Run
public static class TextSizeScaleAudit
{
    private static readonly string[] Contents = { "OK", "Settings", "Return to main menu", "Restore my purchases" };
    private static readonly Vector2[] Boxes =
    {
        new Vector2(260f, 64f), new Vector2(260f, 64f), new Vector2(260f, 64f), new Vector2(160f, 40f),
    };
    private static readonly float[] Scales = { 0.25f, 0.33f, 0.46f, 0.486f, 0.5f, 0.75f, 1f, 1.5f, 2f, 3f };
    private const int BaseMax = 72;
    private const int BaseMin = 10;

    public static string Run()
    {
        var canvasObject = new GameObject("ScaleAudit", typeof(Canvas)) { hideFlags = HideFlags.HideAndDontSave };
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

#if UNITY_2022_2_OR_NEWER
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif

        var texts = new Text[Contents.Length];
        for (var i = 0; i < Contents.Length; i++)
        {
            var node = new GameObject(Contents[i], typeof(RectTransform), typeof(Text));
            node.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Boxes[i];

            var text = node.GetComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = BaseMin;
            text.resizeTextMaxSize = BaseMax;
            text.text = Contents[i];
            texts[i] = text;
        }

        var report = new StringBuilder();

        // B decides once, in text units, with the canvas out of the picture.
        var capB = float.MaxValue;
        foreach (var text in texts)
            capB = Mathf.Min(capB, Measure(text, 1f, BaseMax));
        capB = Mathf.Floor(capB);
        report.AppendLine("cap B (medido em escala 1) = " + capB);
        var naturals = "";
        foreach (var text in texts)
            naturals += text.text + "=" + Measure(text, 1f, BaseMax) + "  ";
        report.AppendLine("naturais em escala 1: " + naturals);
        report.AppendLine("scale | capA | ideal px | px sob A | px sob B");

        foreach (var scale in Scales)
        {
            var capA = float.MaxValue;
            var ideal = int.MaxValue;
            foreach (var text in texts)
            {
                var free = Measure(text, scale, BaseMax);
                capA = Mathf.Min(capA, free / scale);
                ideal = Mathf.Min(ideal, free);
            }

            capA = Mathf.Floor(capA);

            var underA = "";
            var underB = "";
            foreach (var text in texts)
            {
                underA += Measure(text, scale, Mathf.FloorToInt(Mathf.Max(capA, BaseMin))) + " ";
                underB += Measure(text, scale, Mathf.FloorToInt(Mathf.Max(capB, BaseMin))) + " ";
            }

            report.AppendLine(scale.ToString("0.###") + " | " + capA + " | " + ideal + " | " + underA.Trim() +
                              " | " + underB.Trim());
        }

        Object.DestroyImmediate(canvasObject);
        return report.ToString();
    }

    private static int Measure(Text text, float scale, int maxInUnits)
    {
        var settings = text.GetGenerationSettings(((RectTransform)text.transform).rect.size);
        settings.scaleFactor = scale;
        settings.resizeTextMaxSize = maxInUnits;

        var generator = new TextGenerator();
        generator.Populate(text.text, settings);
        return generator.fontSizeUsedForBestFit;
    }
}
