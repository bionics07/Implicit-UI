using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Renders one PNG per sample scene into Docs/images, for the README. It does not use Play Mode: the demos animate, so a
// screenshot taken while they run catches whatever frame it lands on (the first one caught the health bar at zero).
// Instead each scene is opened, put into the state worth showing, pointed at a camera with a RenderTexture and rendered
// once - which also works with the Editor closed, in batch mode.
//
//   Unity -batchmode -quit -projectPath <repo> -executeMethod CaptureSampleShots.Run -logFile <log>
public static class CaptureSampleShots
{
    private const string SamplesFolder = "Packages/com.bionics.implicitui/Samples~";
    private const string WorkFolder = "Assets/_SampleShots";
    private const string ImagesFolder = "Docs/images";
    private const int Width = 1280;
    private const int Height = 720;

    public static void Run()
    {
        var failed = false;
        try
        {
            CopySamples();
            Directory.CreateDirectory(ImagesFolder);

            Capture("Implicit Fill/ImplicitFillDemo", "implicit-fill.png", PrepareFill);
            Capture("Implicit Grayscale/ImplicitGrayscaleDemo", "implicit-grayscale.png", PrepareGrayscale);
            Capture("Implicit Hitbox/ImplicitHitboxDemo", "implicit-hitbox.png", PrepareHitbox);
            Capture("Implicit Text Size Group/ImplicitTextSizeGroupDemo", "implicit-text-size-group.png", PrepareTextSize);
            Capture("Font Changer/FontChangerDemo", "font-changer.png", PrepareFontChanger);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("SHOTS: " + exception);
            failed = true;
        }
        finally
        {
            AssetDatabase.DeleteAsset(WorkFolder);
            AssetDatabase.Refresh();
        }

        EditorApplication.Exit(failed ? 1 : 0);
    }

    private static void Capture(string scene, string file, System.Action<Scene> prepare)
    {
        var loaded = EditorSceneManager.OpenScene(WorkFolder + "/" + scene + ".unity", OpenSceneMode.Single);
        prepare?.Invoke(loaded);

        var camera = Object.FindFirstObjectByType<Camera>();
        var canvas = Object.FindFirstObjectByType<Canvas>();

        // Screen Space - Overlay is drawn straight to the display and never reaches a RenderTexture, so for the shot the
        // canvas is pointed at the camera instead.
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;

        var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = texture;

        // Components that rebuild the mesh (Implicit Fill among them) only do it on the next canvas update, so the first
        // frame can show a half-built mesh; a few passes settle it.
        for (var pass = 0; pass < 3; pass++)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
        }

        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        File.WriteAllBytes(Path.Combine(ImagesFolder, file), image.EncodeToPNG());
        Debug.Log("SHOTS: wrote " + file);

        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        texture.Release();
        Object.DestroyImmediate(texture);
    }

    // The bars animate; a screenshot needs a fixed, meaningful amount and a panel wide enough to show the frame.
    private static void PrepareFill(Scene scene)
    {
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name == "FillSampleDriver")
                behaviour.enabled = false;
        }

        foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            if (rect.name == "Resized panel")
                rect.sizeDelta = new Vector2(880f, 200f);
        }

        foreach (var image in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
        {
            if (image.name == "Fill")
                image.fillAmount = 0.62f;
        }

        SetText("fillAmount", "fillAmount 0.62");
    }

    // One button disabled and one not, which is the whole point of the feature.
    private static void PrepareGrayscale(Scene scene)
    {
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name == "GrayscaleSampleDriver")
                behaviour.enabled = false;
        }

        foreach (var selectable in Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (selectable.name.StartsWith("With Implicit Grayscale"))
                selectable.interactable = false;
        }

        SetText("interactable", "interactable = false on the left button");
    }

    // Implicit Hitbox writes the padding while the game runs, and this capture never plays: the same numbers are written
    // here, for the density the component falls back to (160 dpi, where 48 dp is 48 units).
    private static void PrepareHitbox(Scene scene)
    {
        foreach (var button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            var hasHitbox = false;
            foreach (var component in button.GetComponents<MonoBehaviour>())
            {
                if (component != null && component.GetType().Name == "ImplicitHitbox")
                    hasHitbox = true;
            }

            if (!hasHitbox)
                continue;

            var size = ((RectTransform)button.transform).rect.size;
            var grow = new Vector2((48f - size.x) * 0.5f, (48f - size.y) * 0.5f);
            button.image.raycastPadding = new Vector4(-grow.x, -grow.y, -grow.x, -grow.y);
        }

        foreach (var area in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            if (area.name != "Touch area" || area.parent == null)
                continue;

            var image = area.parent.GetComponent<Image>();
            var padding = image != null ? image.raycastPadding : Vector4.zero;
            area.offsetMin = new Vector2(padding.x, padding.y);
            area.offsetMax = new Vector2(-padding.z, -padding.w);
        }
    }

    private static void PrepareTextSize(Scene scene)
    {
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name == "TextSizeSampleDriver")
                behaviour.enabled = false;
        }

        Canvas.ForceUpdateCanvases();

        // The group recomputes once a frame, and this capture never runs a frame, so it is asked directly. The method is
        // internal to the package; reaching it by reflection is fine in a maintainer script, never in the package itself.
        foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name != "ImplicitTextSizeGroup")
                continue;

            var refresh = behaviour.GetType().GetMethod("RefreshIfChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            refresh?.Invoke(behaviour, null);
        }

        Canvas.ForceUpdateCanvases();
    }

    // The readout is filled by the sample at runtime; without Play Mode it has to be written here.
    private static void PrepareFontChanger(Scene scene)
    {
        var report = "";
        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.InstanceID))
        {
            // The menu labels are the texts with best fit on; the captions are plain labels.
            if (text.resizeTextForBestFit)
                report += "\"" + text.text + "\" uses " + (text.font != null ? text.font.name : "no font") + "\n";
        }

        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(text.text) && !text.resizeTextForBestFit)
                text.text = report.TrimEnd();
        }
    }

    private static void SetText(string startsWith, string value)
    {
        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
        {
            if (text.text.StartsWith(startsWith))
                text.text = value;
        }
    }

    private static void CopySamples()
    {
        if (Directory.Exists(WorkFolder))
            Directory.Delete(WorkFolder, true);

        Directory.CreateDirectory(WorkFolder);
        foreach (var source in Directory.GetDirectories(SamplesFolder))
            CopyTree(source, Path.Combine(WorkFolder, Path.GetFileName(source)));

        AssetDatabase.Refresh();
    }

    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);

        foreach (var folder in Directory.GetDirectories(source))
            CopyTree(folder, Path.Combine(target, Path.GetFileName(folder)));
    }
}
