using System;
using System.IO;
using ImplicitUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/Sandbox/ImplicitHitboxSandbox.unity, the manual test scene for Implicit Hitbox. Every target shows its
// real touch area in translucent green while playing and counts its taps, so it works on a device too. The scene is
// created additively, saved and closed, leaving the open scene alone; it refuses to run while that scene is open.
//
// Unity Pipeline: run_script --file AgentScripts/BuildImplicitHitboxSandbox.cs --entry BuildImplicitHitboxSandbox.Build
public static class BuildImplicitHitboxSandbox
{
    private const string Folder = "Assets/Sandbox";
    private const string ScenePath = Folder + "/ImplicitHitboxSandbox.unity";
    private const string ReadableCirclePath = Folder + "/SandboxHitboxCircle.png";
    private const string UnreadableCirclePath = Folder + "/SandboxHitboxCircleUnreadable.png";

    private static readonly Color s_TargetColor = new Color(0.25f, 0.6f, 0.95f);

    private static Sprite s_Square;
    private static Font s_Font;

    public static string Build()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path == ScenePath)
                throw new InvalidOperationException("Close " + ScenePath + " before rebuilding it.");
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Sandbox");

        s_Square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var readableCircle = CircleSprite(ReadableCirclePath, true);
        var unreadableCircle = CircleSprite(UnreadableCirclePath, false);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        AddInputModule(eventSystem);
        SceneManager.MoveGameObjectToScene(eventSystem, scene);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasObject.transform;

        Label(root, "Implicit Hitbox sandbox - Play, then tap around each target. Green is the real touch area; the counter " +
                    "shows the taps it got. In Edit Mode, select a target to see its area in the Scene view.", 40f, 20f, 1840f, 56f);
        Label(root, "", 40f, 76f, 1840f, 30f).gameObject.AddComponent<SandboxScreenReadout>();

        // Minimum size.
        Label(root, "Minimum size 48 dp", 40f, 130f, 800f, 34f);
        var y = 180f;
        Target(root, "20x20, hitbox", 40f, y, new Vector2(20f, 20f)).AddComponent<ImplicitHitbox>();
        Target(root, "20x20, no hitbox", 340f, y, new Vector2(20f, 20f));
        Target(root, "240x16 bar: grows only in height", 640f, y, new Vector2(240f, 16f)).AddComponent<ImplicitHitbox>();
        Target(root, "160x160: already big, unchanged", 940f, y, new Vector2(160f, 160f)).AddComponent<ImplicitHitbox>();
        // 220 units of padding: larger than 48 dp at any usual Game view size or phone, so the padding is what shows.
        var padded = Target(root, "20x20, Raycast Padding -100: padding wins", 1240f, y, new Vector2(20f, 20f));
        padded.GetComponent<Image>().raycastPadding = new Vector4(-100f, -100f, -100f, -100f);
        padded.AddComponent<ImplicitHitbox>();
        Target(root, "20x20, hitbox with Minimum Size off", 1540f, y, new Vector2(20f, 20f))
            .AddComponent<ImplicitHitbox>().UseMinimumSize = false;

        // Alpha hit test on a circle: the corners of its square are transparent.
        Label(root, "Alpha hit test on a circle (tap the corners of the square)", 40f, 540f, 1200f, 34f);
        y = 590f;
        AlphaTarget(root, "Hitbox alpha 0.5: corners miss", 40f, y, readableCircle, 0.5f, Vector4.zero);
        AlphaTarget(root, "Hitbox alpha 0.5 + padding -40: corners miss, band around hits", 400f, y, readableCircle, 0.5f,
            new Vector4(-40f, -40f, -40f, -40f));
        AlphaTarget(root, "Unreadable texture: whole square hits, one warning in the log", 760f, y, unreadableCircle, 0.5f,
            Vector4.zero);
        var native = AlphaTarget(root, "Unity's own threshold 0.5 + padding -40, no hitbox (for comparison)", 1120f, y,
            readableCircle, 0f, new Vector4(-40f, -40f, -40f, -40f));
        native.GetComponent<Image>().alphaHitTestMinimumThreshold = 0.5f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        return ScenePath;
    }

    // A target centered in a 300x300 cell, with its caption and tap counter under it.
    private static GameObject Target(Transform parent, string caption, float x, float y, Vector2 size)
    {
        var cell = Container(parent, caption, new Vector2(x, y), new Vector2(280f, 340f));
        var target = Container(cell, "Target", new Vector2(140f - size.x * 0.5f, 130f - size.y * 0.5f), size);
        var image = target.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_TargetColor;

        var counter = Label(cell, "0 taps", 0f, 270f, 280f, 30f);
        counter.alignment = TextAnchor.MiddleCenter;
        target.gameObject.AddComponent<SandboxHitArea>().Counter = counter;

        var label = Label(cell, caption, 0f, 300f, 280f, 44f);
        label.alignment = TextAnchor.UpperCenter;
        label.fontSize = 18;
        return target.gameObject;
    }

    private static GameObject AlphaTarget(Transform parent, string caption, float x, float y, Sprite sprite,
        float threshold, Vector4 padding)
    {
        var target = Target(parent, caption, x, y, new Vector2(180f, 180f));
        var image = target.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.raycastPadding = padding;
        if (threshold > 0f)
        {
            var hitbox = target.AddComponent<ImplicitHitbox>();
            hitbox.UseMinimumSize = false;
            hitbox.AlphaThreshold = threshold;
        }

        return target;
    }

    // A white circle on a transparent square, written as a PNG so its import settings (Read/Write) are real.
    private static Sprite CircleSprite(string path, bool readable)
    {
        if (!File.Exists(path))
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            for (var py = 0; py < size; py++)
            {
                for (var px = 0; px < size; px++)
                {
                    var distance = Vector2.Distance(new Vector2(px, py), new Vector2(center, center));
                    texture.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Clamp01(size * 0.5f - distance)));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = readable;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // The project uses the Input System; fall back to the legacy module where it is not installed.
    private static void AddInputModule(GameObject eventSystem)
    {
        var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputSystemModule != null)
            eventSystem.AddComponent(inputSystemModule);
        else
            eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static RectTransform Container(Transform parent, string name, Vector2 topLeft, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
        return rect;
    }

    private static Text Label(Transform parent, string text, float x, float y, float width, float height)
    {
        var rect = Container(parent, "Label", new Vector2(x, y), new Vector2(width, height));
        var label = rect.gameObject.AddComponent<Text>();
        label.text = text;
        label.font = s_Font;
        label.fontSize = 22;
        label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
        return label;
    }
}
