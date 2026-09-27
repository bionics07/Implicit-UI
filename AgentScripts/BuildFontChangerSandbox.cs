using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the manual test assets for the Font Changer under Assets/Sandbox/FontChanger: two sandbox TextMeshPro fonts
// with yellow and cyan default materials (so a changed text is obvious on screen), an outline preset, prefabs covering
// every prefab case (plain, variant, nested, in a subfolder, legacy Text), and a scene using them. The Font Changer
// rewrites these files when tested: restore them with git afterwards, or rebuild. Refuses to run while the scene is open.
//
// Unity Pipeline: run_script --file AgentScripts/BuildFontChangerSandbox.cs --entry BuildFontChangerSandbox.Build
public static class BuildFontChangerSandbox
{
    private const string Folder = "Assets/Sandbox/FontChanger";
    private const string PrefabFolder = Folder + "/Prefabs";
    private const string ScenePath = Folder + "/FontChangerSandbox.unity";
    private const string YellowFontPath = Folder + "/Sandbox Yellow SDF.asset";
    private const string CyanFontPath = Folder + "/Sandbox Cyan SDF.asset";
    private const string OutlinePath = Folder + "/LiberationSans SDF - Sandbox Outline.mat";
    private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
    private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?'\"-()/";

    private static Sprite s_Square;
    private static TMP_FontAsset s_DefaultFont;

    public static string Build()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path == ScenePath)
                throw new InvalidOperationException("Close " + ScenePath + " before rebuilding it.");
        }

        CreateFolder("Assets/Sandbox", "FontChanger");
        CreateFolder(Folder, "Prefabs");
        CreateFolder(PrefabFolder, "Sub");

        s_Square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_DefaultFont = TMP_Settings.defaultFontAsset;
        SandboxFont(YellowFontPath, "Sandbox Yellow SDF", new Color(1f, 0.85f, 0.2f));

        // The instance override uses a sandbox font too: TextMeshPro's own dynamic fonts rewrite their asset when they render.
        var cyanFont = SandboxFont(CyanFontPath, "Sandbox Cyan SDF", new Color(0.3f, 0.9f, 1f));
        OutlinePreset();

        // Prefabs: plain, a variant that inherits the label font, one nesting the button, one in a subfolder, legacy.
        var button = SavePrefab(Button("Button", "Button label", true), PrefabFolder + "/Button.prefab");
        var variantInstance = (GameObject)PrefabUtility.InstantiatePrefab(button);
        SavePrefab(variantInstance, PrefabFolder + "/Button Variant.prefab");
        var panel = Box("Panel", new Vector2(300f, 200f));
        Label(panel, "Panel title (own font)", new Vector2(0f, 60f));
        var nested = (GameObject)PrefabUtility.InstantiatePrefab(button, panel);
        ((RectTransform)nested.transform).anchoredPosition = new Vector2(0f, -40f);
        SavePrefab(panel.gameObject, PrefabFolder + "/Panel.prefab");
        SavePrefab(Label(null, "Deep label (subfolder)", Vector2.zero).gameObject, PrefabFolder + "/Sub/Deep Label.prefab");
        SavePrefab(Button("Legacy Button", "Legacy button", false), PrefabFolder + "/Legacy Button.prefab");

        // Scene.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var root = (RectTransform)canvasObject.transform;

        Label(root, "Font Changer sandbox - a changed TextMeshPro text turns yellow (the target font's material)", new Vector2(0f, 480f), 1600f);
        Label(root, "Scene text (default font)", new Vector2(-600f, 360f));
        var hidden = Label(root, "Inactive scene text", new Vector2(-600f, 300f));
        hidden.gameObject.SetActive(false);
        var outlined = Label(root, "Scene text with the outline preset", new Vector2(-600f, 240f));
        outlined.fontSharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);

        var inherited = (GameObject)PrefabUtility.InstantiatePrefab(button, root);
        inherited.name = "Button instance (font inherited)";
        ((RectTransform)inherited.transform).anchoredPosition = new Vector2(0f, 300f);

        var overridden = (GameObject)PrefabUtility.InstantiatePrefab(button, root);
        overridden.name = "Button instance (font overridden)";
        ((RectTransform)overridden.transform).anchoredPosition = new Vector2(0f, 180f);
        var overriddenLabel = overridden.GetComponentInChildren<TextMeshProUGUI>();
        overriddenLabel.font = cyanFont;
        overriddenLabel.fontSharedMaterial = cyanFont.material;
        overriddenLabel.text = "Instance with its own font";
        PrefabUtility.RecordPrefabInstancePropertyModifications(overriddenLabel);

        var legacy = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Legacy Button.prefab"), root);
        ((RectTransform)legacy.transform).anchoredPosition = new Vector2(500f, 300f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        return ScenePath;
    }

    // A font from the project's TTF with a static atlas (a dynamic one would rewrite the asset whenever it renders a new
    // character) and a colored default material, so a text using it is recognizable on screen.
    private static TMP_FontAsset SandboxFont(string path, string name, Color color)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
            return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        var font = TMP_FontAsset.CreateFontAsset(source);
        font.name = name;
        font.TryAddCharacters(Characters);
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, path);

        var atlas = font.atlasTextures[0];
        atlas.name = name + " Atlas";
        AssetDatabase.AddObjectToAsset(atlas, font);

        var material = font.material;
        material.name = name + " Material";
        material.SetColor(ShaderUtilities.ID_FaceColor, color);
        AssetDatabase.AddObjectToAsset(material, font);

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    // A preset of the default font: same atlas, with an outline - what "flattening presets" would lose.
    private static void OutlinePreset()
    {
        if (File.Exists(OutlinePath))
            return;

        var preset = new Material(s_DefaultFont.material) { name = "LiberationSans SDF - Sandbox Outline" };
        preset.EnableKeyword(ShaderUtilities.Keyword_Outline);
        preset.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        preset.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        AssetDatabase.CreateAsset(preset, OutlinePath);
    }

    private static GameObject Button(string name, string text, bool tmp)
    {
        var button = Box(name, new Vector2(260f, 70f));
        var image = button.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.25f, 0.6f, 0.95f);

        if (tmp)
        {
            Label(button, text, Vector2.zero, 250f);
        }
        else
        {
            var label = Box("Label", new Vector2(250f, 60f), button);
            var legacy = label.gameObject.AddComponent<Text>();
            legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            legacy.text = text;
            legacy.fontSize = 24;
            legacy.alignment = TextAnchor.MiddleCenter;
            legacy.color = Color.white;
        }

        return button.gameObject;
    }

    private static TextMeshProUGUI Label(RectTransform parent, string text, Vector2 position, float width = 520f)
    {
        var rect = Box("Label", new Vector2(width, 50f), parent);
        rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = s_DefaultFont;
        label.text = text;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        return label;
    }

    private static RectTransform Box(string name, Vector2 size, RectTransform parent = null)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        if (parent != null)
            rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    // Saves the object as a prefab, destroys the scene copy and returns the asset.
    private static GameObject SavePrefab(GameObject source, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
        UnityEngine.Object.DestroyImmediate(source);
        return prefab;
    }

    private static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
