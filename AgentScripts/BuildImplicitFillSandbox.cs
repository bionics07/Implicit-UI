using ImplicitUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/Sandbox/ImplicitFillSandbox.unity, the manual test scene for Implicit Fill. The scene is created
// additively, saved and closed, so whatever scene is open in the Editor is left alone.
//
// Unity Pipeline: run_script --file AgentScripts/BuildImplicitFillSandbox.cs --entry BuildImplicitFillSandbox.Build
public static class BuildImplicitFillSandbox
{
    private const string Folder = "Assets/Sandbox";
    private const string ScenePath = Folder + "/ImplicitFillSandbox.unity";

    private static readonly Color s_BarColor = new Color(0.36f, 0.82f, 0.52f);
    private static readonly Color s_ComparisonColor = new Color(0.95f, 0.62f, 0.3f);

    private static Sprite s_Square;
    private static Sprite s_Circle;

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Sandbox");

        s_Square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_Circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

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

        Label(root, "Implicit Fill sandbox - Play animates every bar; in Edit Mode change Fill Amount on Implicit Fill", 40f, 30f);

        var y = 110f;
        Row(root, "Sliced, Horizontal, origin Left", ref y, Image.Type.Sliced, Image.FillMethod.Horizontal, 0, 0.65f, true, 0f);
        Row(root, "Sliced, Horizontal, origin Right", ref y, Image.Type.Sliced, Image.FillMethod.Horizontal, 1, 0.4f, true, 0.3f);
        Row(root, "Native Filled, no Implicit Fill (borders squash)", ref y, Image.Type.Filled, Image.FillMethod.Horizontal, 0, 0.65f, false, 0f, s_ComparisonColor);
        Row(root, "Tiled, Horizontal", ref y, Image.Type.Tiled, Image.FillMethod.Horizontal, 0, 0.5f, true, 0.6f);
        Row(root, "Image type Filled + Implicit Fill (warning, no double cut)", ref y, Image.Type.Filled, Image.FillMethod.Horizontal, 0, 0.5f, true, 0.1f);
        Row(root, "Radial method (not supported yet, drawn without fill)", ref y, Image.Type.Sliced, Image.FillMethod.Radial360, 0, 0.5f, true, 0.2f);

        var circle = Bar(root, "Simple circle, preserveAspect", new Vector2(520f, y), new Vector2(600f, 120f), Image.Type.Simple, Image.FillMethod.Horizontal, 0, 0.5f, true, 0.4f, s_BarColor);
        circle.sprite = s_Circle;
        circle.preserveAspect = true;
        Label(root, "Simple circle, preserveAspect (cut follows the circle, not the rect)", 40f, y + 45f);
        y += 150f;

        var rectMask = Container(root, "RectMask2D (left half visible)", new Vector2(520f, y), new Vector2(300f, 60f));
        rectMask.gameObject.AddComponent<RectMask2D>();
        Bar(rectMask, "Bar", Vector2.zero, new Vector2(600f, 60f), Image.Type.Sliced, Image.FillMethod.Horizontal, 0, 0.8f, true, 0.5f, s_BarColor);
        Label(root, "Inside RectMask2D (mask covers the left half)", 40f, y + 15f);
        y += 90f;

        var stencil = Container(root, "Mask (rounded square)", new Vector2(520f, y), new Vector2(600f, 60f));
        var stencilImage = stencil.gameObject.AddComponent<Image>();
        stencilImage.sprite = s_Square;
        stencilImage.type = Image.Type.Sliced;
        stencil.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Bar(stencil, "Bar", Vector2.zero, new Vector2(600f, 60f), Image.Type.Sliced, Image.FillMethod.Horizontal, 0, 0.7f, true, 0.7f, s_BarColor);
        Label(root, "Inside Mask (stencil)", 40f, y + 15f);

        Bar(root, "Vertical Bottom", new Vector2(1400f, 110f), new Vector2(80f, 420f), Image.Type.Sliced, Image.FillMethod.Vertical, 0, 0.6f, true, 0.8f, s_BarColor);
        Label(root, "Vertical, Bottom", 1360f, 550f);
        Bar(root, "Vertical Top", new Vector2(1600f, 110f), new Vector2(80f, 420f), Image.Type.Sliced, Image.FillMethod.Vertical, 1, 0.3f, true, 0.9f, s_BarColor);
        Label(root, "Vertical, Top", 1580f, 550f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        return ScenePath;
    }

    private static void Row(Transform root, string label, ref float y, Image.Type type, Image.FillMethod method, int origin,
        float amount, bool implicitFill, float offset, Color? color = null)
    {
        Label(root, label, 40f, y + 15f);
        Bar(root, label, new Vector2(520f, y), new Vector2(600f, 60f), type, method, origin, amount, implicitFill, offset, color ?? s_BarColor);
        y += 90f;
    }

    private static Image Bar(Transform parent, string name, Vector2 topLeft, Vector2 size, Image.Type type, Image.FillMethod method,
        int origin, float amount, bool implicitFill, float offset, Color color)
    {
        var rect = Container(parent, name, topLeft, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = type;
        image.color = color;
        image.fillMethod = method;
        image.fillOrigin = origin;
        image.fillAmount = amount;

        if (implicitFill)
            rect.gameObject.AddComponent<ImplicitFill>();

        var animator = rect.gameObject.AddComponent<SandboxFillAnimator>();
        animator.Offset = offset;
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

    private static void Label(Transform parent, string text, float x, float y)
    {
        var rect = Container(parent, "Label", new Vector2(x, y), new Vector2(460f, 34f));
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 20f;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }
}
