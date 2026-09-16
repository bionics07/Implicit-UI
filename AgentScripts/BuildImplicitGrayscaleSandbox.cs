using System;
using ImplicitUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/Sandbox/ImplicitGrayscaleSandbox.unity, the manual test scene for Implicit Grayscale. The scene is
// created additively, saved and closed, so whatever scene is open in the Editor is left alone. Rebuilding overwrites
// the saved scene, so it refuses to run while that scene is open.
//
// Unity Pipeline: run_script --file AgentScripts/BuildImplicitGrayscaleSandbox.cs --entry BuildImplicitGrayscaleSandbox.Build
public static class BuildImplicitGrayscaleSandbox
{
    private const string Folder = "Assets/Sandbox";
    private const string ScenePath = Folder + "/ImplicitGrayscaleSandbox.unity";
    private const string CustomMaterialPath = Folder + "/SandboxCustomUIMaterial.mat";

    private static readonly Color[] s_Colors =
    {
        new Color(0.92f, 0.26f, 0.22f),
        new Color(0.25f, 0.75f, 0.35f),
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.97f, 0.8f, 0.2f),
        new Color(0.75f, 0.35f, 0.9f),
    };

    private static readonly Color s_ButtonColor = new Color(0.25f, 0.6f, 0.95f);

    private static Sprite s_Square;
    private static Sprite s_Circle;

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
        s_Circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // A saved material asset, so the scene keeps the reference: the custom-material case must survive a reload.
        var customMaterial = AssetDatabase.LoadAssetAtPath<Material>(CustomMaterialPath);
        if (customMaterial == null)
        {
            customMaterial = new Material(Shader.Find("UI/Default"));
            AssetDatabase.CreateAsset(customMaterial, CustomMaterialPath);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasObject.transform;

        Label(root, "Implicit Grayscale sandbox - Play flips the button marked 'toggles'; in Edit Mode change Intensity, " +
                    "Tint or a Button's Interactable", 40f, 30f, 1500f);

        // Buttons: the image and the icon each have their own Implicit Grayscale; the TextMeshPro label has none.
        Label(root, "Buttons (button image and icon have the component; the label text does not)", 40f, 80f, 1200f);
        CreateButton(root, "Enabled", new Vector2(40f, 120f), true, true, false);
        CreateButton(root, "Disabled", new Vector2(300f, 120f), false, true, false);
        CreateButton(root, "Disabled, Gray When Disabled off", new Vector2(560f, 120f), false, false, false);
        CreateButton(root, "Toggles every second", new Vector2(820f, 120f), true, true, true);
        var group = Container(root, "Non-interactable CanvasGroup", new Vector2(1080f, 120f), new Vector2(240f, 70f));
        group.gameObject.AddComponent<CanvasGroup>().interactable = false;
        CreateButton(group, "In a non-interactable CanvasGroup", Vector2.zero, true, true, false);

        // Manual intensity.
        Label(root, "Intensity 0, 0.25, 0.5, 0.75, 1", 40f, 240f, 600f);
        for (var i = 0; i < 5; i++)
        {
            var image = CreateImage(root, "Intensity " + (i * 0.25f), new Vector2(40f + i * 170f, 280f), new Vector2(140f, 140f), s_Colors[i]);
            image.gameObject.AddComponent<ImplicitGrayscale>().Intensity = i * 0.25f;
        }

        // Tints.
        Label(root, "Tint: sepia, cold, half-strength sepia", 900f, 240f, 600f);
        CreateTinted(root, "Sepia", new Vector2(900f, 280f), new Color(0.95f, 0.8f, 0.55f), 1f);
        CreateTinted(root, "Cold", new Vector2(1070f, 280f), new Color(0.6f, 0.75f, 1f), 1f);
        CreateTinted(root, "Half sepia", new Vector2(1240f, 280f), new Color(0.95f, 0.8f, 0.55f), 0.5f);

        // Nesting and the cases where the component does nothing.
        Label(root, "Nesting: gray panel; left child has the component (at least as gray), right child does not (stays in color)", 40f, 460f, 1200f);
        var panel = CreateImage(root, "Gray panel", new Vector2(40f, 500f), new Vector2(400f, 180f), new Color(0.35f, 0.35f, 0.45f));
        panel.gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        CreateImage(panel.transform, "Child with component", new Vector2(30f, 30f), new Vector2(150f, 120f), s_Colors[0])
            .gameObject.AddComponent<ImplicitGrayscale>();
        CreateImage(panel.transform, "Child without component", new Vector2(220f, 30f), new Vector2(150f, 120f), s_Colors[1]);

        var custom = CreateImage(root, "Custom material", new Vector2(500f, 520f), new Vector2(140f, 140f), s_Colors[2]);
        custom.material = customMaterial;
        custom.gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        Label(root, "Custom material: left alone, inspector warns", 470f, 670f, 220f);

        var raw = Container(root, "RawImage", new Vector2(720f, 520f), new Vector2(140f, 140f)).gameObject.AddComponent<RawImage>();
        raw.texture = s_Circle.texture;
        raw.color = s_Colors[3];
        raw.gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        Label(root, "RawImage, fully gray", 720f, 670f, 200f);

        var mask = CreateImage(root, "Mask", new Vector2(940f, 520f), new Vector2(140f, 140f), Color.white);
        mask.sprite = s_Circle;
        mask.type = Image.Type.Simple;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        CreateImage(mask.transform, "Masked", Vector2.zero, new Vector2(140f, 140f), s_Colors[4])
            .gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        Label(root, "Inside a Mask (circle), gray", 930f, 670f, 200f);

        var rectMask = Container(root, "RectMask2D with softness", new Vector2(1160f, 520f), new Vector2(140f, 140f)).gameObject.AddComponent<RectMask2D>();
        rectMask.softness = new Vector2Int(30, 30);
        CreateImage(rectMask.transform, "Soft masked", new Vector2(-20f, -20f), new Vector2(180f, 180f), s_Colors[0])
            .gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        Label(root, "RectMask2D with softness, gray with soft edges", 1140f, 670f, 240f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        return ScenePath;
    }

    private static void CreateButton(Transform parent, string text, Vector2 topLeft, bool interactable, bool grayWhenDisabled, bool toggles)
    {
        var rect = Container(parent, text, topLeft, new Vector2(240f, 70f));
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_ButtonColor;
        rect.gameObject.AddComponent<ImplicitGrayscale>().GrayWhenDisabled = grayWhenDisabled;

        var button = rect.gameObject.AddComponent<Button>();
        button.interactable = interactable;
        if (toggles)
            rect.gameObject.AddComponent<SandboxInteractableToggler>();

        var icon = CreateImage(rect, "Icon", new Vector2(10f, 15f), new Vector2(40f, 40f), new Color(1f, 0.8f, 0.2f));
        icon.sprite = s_Circle;
        icon.type = Image.Type.Simple;
        icon.gameObject.AddComponent<ImplicitGrayscale>().GrayWhenDisabled = grayWhenDisabled;

        var labelRect = Container(rect, "Label", new Vector2(56f, 0f), new Vector2(180f, 70f));
        var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 18f;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void CreateTinted(Transform parent, string name, Vector2 topLeft, Color tint, float tintIntensity)
    {
        var grayscale = CreateImage(parent, name, topLeft, new Vector2(140f, 140f), s_Colors[2]).gameObject.AddComponent<ImplicitGrayscale>();
        grayscale.Intensity = 1f;
        grayscale.TintColor = tint;
        grayscale.TintIntensity = tintIntensity;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 topLeft, Vector2 size, Color color)
    {
        var image = Container(parent, name, topLeft, size).gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = color;
        return image;
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

    private static void Label(Transform parent, string text, float x, float y, float width)
    {
        var rect = Container(parent, "Label", new Vector2(x, y), new Vector2(width, 34f));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }
}
