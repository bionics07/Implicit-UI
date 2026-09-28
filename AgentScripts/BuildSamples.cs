using System.IO;
using ImplicitUI;
using ImplicitUI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the package samples: one demo scene per feature, uGUI only (no TextMeshPro, so they work in any project and in
// the CI line that has no TMP). Scenes can only be saved under Assets, so everything is built in Assets/_SamplesBuild
// and then moved - with its .meta files, to keep GUIDs stable between releases - into the package's Samples~ folder,
// which Unity hides from the project.
//
// Rebuilding: the sample scripts only compile while they are under Assets, so run RestoreSamples.Run first, let the
// Editor compile, then BuildSamples.Build.
//
// Unity Pipeline: run_script --file AgentScripts/BuildSamples.cs --entry BuildSamples.Build
public static class BuildSamples
{
    private const string BuildFolder = "Assets/_SamplesBuild";
    private const string SamplesFolder = "Packages/com.bionics.implicitui/Samples~";

    private static readonly Color s_Background = new Color(0.12f, 0.12f, 0.15f);
    private static readonly Color s_Panel = new Color(0.2f, 0.2f, 0.26f);
    private static readonly Color s_Accent = new Color(0.25f, 0.6f, 0.95f);
    private static readonly Color s_Health = new Color(0.9f, 0.3f, 0.3f);

    private static Sprite s_Square;
    private static Sprite s_Circle;
    private static Font s_Font;

    public static string Build()
    {
        // Batch mode starts on an untitled scene, and Unity refuses to create an additive scene next to one; opening a
        // saved scene first clears that.
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        s_Square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_Circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        s_Font = BuiltinFont();

        BuildFill();
        BuildGrayscale();
        BuildHitbox();
        BuildTextSize();
        BuildFontChanger();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var moved = MoveToSamples();
        return "Samples rebuilt: " + moved;
    }

    // --- Samples ------------------------------------------------------------------------------------------------

    private static void BuildFill()
    {
        var scene = NewScene(out var root, "Implicit Fill");
        Title(root, "Implicit Fill - a health bar that stays Sliced while it fills");
        Note(root, "Both bars use the same framed sprite and the same fillAmount, while the panel behind them is " +
                   "resized. Left: Implicit Fill keeps the frame's corners. Right: Unity's Filled type drops Sliced, " +
                   "so the frame stretches with the fill.", 110f);

        var resized = Panel(root, "Resized panel", new Vector2(0f, -40f), new Vector2(900f, 200f));
        var framed = Bar(resized, "Implicit Fill (Sliced)", 0.02f, 0.49f);

        // Image defaults to Radial 360; a health bar fills from the left, and Implicit Fill follows these same fields.
        framed.fillMethod = Image.FillMethod.Horizontal;
        framed.fillOrigin = (int)Image.OriginHorizontal.Left;
        framed.gameObject.AddComponent<ImplicitFill>();
        var plain = Bar(resized, "Plain Filled image", 0.51f, 0.98f);
        plain.type = Image.Type.Filled;
        plain.fillMethod = Image.FillMethod.Horizontal;
        plain.fillOrigin = (int)Image.OriginHorizontal.Left;

        var readout = Label(root, "fillAmount", new Vector2(0f, -200f), 400f);
        var driver = root.gameObject.AddComponent<FillSampleDriver>();
        Set(driver, "m_Bars", new[] { framed, plain });
        Set(driver, "m_ResizedPanel", resized);
        Set(driver, "m_Readout", readout);
        Save(scene, "Implicit Fill", "ImplicitFillDemo");
    }

    private static void BuildGrayscale()
    {
        var scene = NewScene(out var root, "Implicit Grayscale");
        Title(root, "Implicit Grayscale - a disabled button turns gray on its own");
        Note(root, "The buttons flip between interactable and not. The images with Implicit Grayscale follow, with no " +
                   "code and no material to set up. The right-hand button has no component, for comparison.", 120f);

        var withComponent = DemoButton(root, "With Implicit Grayscale", new Vector2(-160f, 40f), true);
        var without = DemoButton(root, "Without the component", new Vector2(160f, 40f), false);
        var panel = Panel(root, "Nested panel", new Vector2(0f, -110f), new Vector2(520f, 120f));
        panel.GetComponent<Image>().gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;
        Icon(panel, "Child with the component", new Vector2(-90f, 0f)).gameObject.AddComponent<ImplicitGrayscale>();
        Icon(panel, "Child without it", new Vector2(90f, 0f));
        Label(root, "A panel with the component keeps the images below it at least as gray.", new Vector2(0f, -195f), 760f);

        var readout = Label(root, "interactable = true", new Vector2(0f, -245f), 500f);
        var driver = root.gameObject.AddComponent<GrayscaleSampleDriver>();
        Set(driver, "m_Buttons", new Selectable[] { withComponent, without });
        Set(driver, "m_Readout", readout);
        Save(scene, "Implicit Grayscale", "ImplicitGrayscaleDemo");
    }

    private static void BuildHitbox()
    {
        var scene = NewScene(out var root, "Implicit Hitbox");
        Title(root, "Implicit Hitbox - a touch target larger than the icon");
        Note(root, "Both icons are 24x24. The green rectangle is the area that actually takes clicks: on the left it " +
                   "is 48 dp, because of Implicit Hitbox. Click just outside each icon and watch the counters.", 110f);

        var withHitbox = IconButton(root, "With Implicit Hitbox", new Vector2(-180f, -20f));
        withHitbox.gameObject.AddComponent<ImplicitHitbox>();
        var without = IconButton(root, "Without the component", new Vector2(180f, -20f));
        var leftArea = TouchArea(withHitbox);
        var rightArea = TouchArea(without);
        var left = Label(root, "0 clicks", new Vector2(-180f, -120f), 240f);
        var right = Label(root, "0 clicks", new Vector2(180f, -120f), 240f);

        var driver = root.gameObject.AddComponent<HitboxSampleDriver>();
        Set(driver, "m_Icons", new[] { withHitbox, without });
        Set(driver, "m_Counters", new[] { left, right });
        Set(driver, "m_TouchAreas", new[] { leftArea, rightArea });
        Save(scene, "Implicit Hitbox", "ImplicitHitboxDemo");
    }

    // A translucent rectangle behind an icon, resized by the sample to match its touch area.
    private static RectTransform TouchArea(Button icon)
    {
        var rect = Box("Touch area", Vector2.zero, (RectTransform)icon.transform);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.9f, 0.45f, 0.25f);
        image.raycastTarget = false;
        return rect;
    }

    private static void BuildTextSize()
    {
        var scene = NewScene(out var root, "Implicit Text Size Group");
        Title(root, "Implicit Text Size Group - sibling labels share one size");
        Note(root, "The same labels on both menus. On the left, an Implicit Text Size Group gives every label the " +
                   "smallest size any of them would pick alone; on the right, each label sizes itself.", 120f);

        var grouped = Panel(root, "With the group", new Vector2(-180f, -30f), new Vector2(300f, 240f));
        grouped.gameObject.AddComponent<ImplicitTextSizeGroup>();
        var loose = Panel(root, "Without it", new Vector2(180f, -30f), new Vector2(300f, 240f));

        // Real words from the start, so the scene shows the difference before anyone presses Play.
        var words = new[] { "OK", "Settings", "Back to the main menu" };
        var groupedLabels = new Text[words.Length];
        var looseLabels = new Text[words.Length];
        for (var i = 0; i < words.Length; i++)
        {
            var y = 70f - i * 70f;
            groupedLabels[i] = MenuButton(grouped, words[i], new Vector2(0f, y));
            looseLabels[i] = MenuButton(loose, words[i], new Vector2(0f, y));
        }

        var driver = root.gameObject.AddComponent<TextSizeSampleDriver>();
        Set(driver, "m_GroupedLabels", groupedLabels);
        Set(driver, "m_LooseLabels", looseLabels);
        Save(scene, "Implicit Text Size Group", "ImplicitTextSizeGroupDemo");
    }

    private static void BuildFontChanger()
    {
        var scene = NewScene(out var root, "Font Changer");
        Title(root, "Font Changer - swap fonts across scenes and prefabs");
        Note(root, "Open Tools > Implicit UI > Font Changer, pick any font of your project as To Font and press Scan. " +
                   "Nothing changes until you press Apply, and the list says which texts are skipped and why.", 120f);

        var panel = Panel(root, "Texts", new Vector2(0f, -30f), new Vector2(560f, 260f));
        var first = MenuButton(panel, "Play", new Vector2(0f, 80f));
        var second = MenuButton(panel, "Options", new Vector2(0f, 0f));
        var third = MenuButton(panel, "Quit", new Vector2(0f, -80f));

        var readout = Label(root, "", new Vector2(0f, -250f), 700f);
        readout.rectTransform.sizeDelta = new Vector2(700f, 120f);
        readout.alignment = TextAnchor.UpperCenter;
        var note = root.gameObject.AddComponent<FontChangerSampleNote>();
        Set(note, "m_Readout", readout);
        Set(note, "m_Texts", new[] { first, second, third });
        Save(scene, "Font Changer", "FontChangerDemo");
    }

    // --- Building blocks ----------------------------------------------------------------------------------------

    private static Scene NewScene(out RectTransform root, string name)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = s_Background;
        SceneManager.MoveGameObjectToScene(camera.gameObject, scene);

        var canvasObject = new GameObject(name + " Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        root = (RectTransform)canvasObject.transform;
        return scene;
    }

    private static void Save(Scene scene, string folder, string sceneName)
    {
        var path = BuildFolder + "/" + folder + "/" + sceneName + ".unity";
        EditorSceneManager.SaveScene(scene, path);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static Text Title(RectTransform root, string text)
    {
        var label = Label(root, text, new Vector2(0f, 300f), 1100f);
        label.fontSize = 28;
        return label;
    }

    private static Text Note(RectTransform root, string text, float y)
    {
        var label = Label(root, text, new Vector2(0f, 300f - y), 1000f);
        label.fontSize = 18;
        label.color = new Color(0.8f, 0.8f, 0.85f);
        return label;
    }

    private static Text Label(RectTransform parent, string text, Vector2 position, float width)
    {
        var rect = Box("Label", new Vector2(width, 60f), parent);
        rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<Text>();
        label.font = s_Font;
        label.text = text;
        label.fontSize = 20;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform Panel(RectTransform parent, string name, Vector2 position, Vector2 size)
    {
        var rect = Box(name, size, parent);
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_Panel;
        return rect;
    }

    // One bar filling the given horizontal slice of its panel, so both bars resize with it.
    private static Image Bar(RectTransform parent, string caption, float fromX, float toX)
    {
        var background = Box(caption, Vector2.zero, parent);
        background.anchorMin = new Vector2(fromX, 0.25f);
        background.anchorMax = new Vector2(toX, 0.6f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        var back = background.gameObject.AddComponent<Image>();
        back.sprite = s_Square;
        back.type = Image.Type.Sliced;
        back.color = new Color(0.1f, 0.1f, 0.12f);

        var fillRect = Box("Fill", Vector2.zero, background);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(6f, 6f);
        fillRect.offsetMax = new Vector2(-6f, -6f);
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.sprite = s_Square;
        fill.type = Image.Type.Sliced;
        fill.color = s_Health;

        var label = Label(background, caption, new Vector2(0f, -56f), 400f);
        label.fontSize = 16;
        return fill;
    }

    private static Button DemoButton(RectTransform parent, string caption, Vector2 position, bool withComponent)
    {
        var rect = Box(caption, new Vector2(280f, 80f), parent);
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_Accent;
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (withComponent)
            rect.gameObject.AddComponent<ImplicitGrayscale>();

        var icon = Icon(rect, "Icon", new Vector2(-100f, 0f));
        if (withComponent)
            icon.gameObject.AddComponent<ImplicitGrayscale>();

        Label(rect, caption, new Vector2(30f, 0f), 200f).fontSize = 16;
        return button;
    }

    private static Button IconButton(RectTransform parent, string caption, Vector2 position)
    {
        var rect = Box(caption, new Vector2(24f, 24f), parent);
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Circle;
        image.color = s_Accent;
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        Label(parent, caption, position + new Vector2(0f, -35f), 240f).fontSize = 16;
        return button;
    }

    private static Text MenuButton(RectTransform parent, string name, Vector2 position)
    {
        var rect = Box(name, new Vector2(260f, 60f), parent);
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_Accent;

        var labelRect = Box("Label", new Vector2(240f, 48f), rect);
        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = s_Font;
        label.text = name;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 8;
        label.resizeTextMaxSize = 40;
        return label;
    }

    private static Image Icon(RectTransform parent, string name, Vector2 position)
    {
        var rect = Box(name, new Vector2(48f, 48f), parent);
        rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Circle;
        image.color = new Color(1f, 0.8f, 0.25f);
        return image;
    }

    private static RectTransform Box(string name, Vector2 size, RectTransform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        return rect;
    }

    private static void Set(Object target, string field, object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (value is Object single)
        {
            property.objectReferenceValue = single;
        }
        else
        {
            var array = (Object[])value;
            property.arraySize = array.Length;
            for (var i = 0; i < array.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // The built-in font was renamed in 2022.2, and asking for the other name logs an error.
    private static Font BuiltinFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    // Samples live in a folder Unity hides ("~"), where scenes cannot be saved, so they are moved there afterwards.
    private static string MoveToSamples()
    {
        var names = "";
        foreach (var source in Directory.GetDirectories(BuildFolder))
        {
            var name = Path.GetFileName(source);
            var target = Path.Combine(SamplesFolder, name);
            if (Directory.Exists(target))
                Directory.Delete(target, true);

            Directory.CreateDirectory(SamplesFolder);
            Directory.Move(source, target);
            File.Delete(source + ".meta");
            names += name + ", ";
        }

        AssetDatabase.DeleteAsset(BuildFolder);
        AssetDatabase.Refresh();
        return names.TrimEnd(',', ' ');
    }
}
