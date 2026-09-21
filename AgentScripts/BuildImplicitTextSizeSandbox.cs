using System;
using ImplicitUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/Sandbox/ImplicitTextSizeSandbox.unity, the manual test scene for Implicit Text Size Group. Each row
// shows the same buttons with and without a group, plus the cases the group leaves alone. The scene is created
// additively, saved and closed, leaving the open scene alone; it refuses to run while that scene is open.
//
// Unity Pipeline: run_script --file AgentScripts/BuildImplicitTextSizeSandbox.cs --entry BuildImplicitTextSizeSandbox.Build
public static class BuildImplicitTextSizeSandbox
{
    private const string Folder = "Assets/Sandbox";
    private const string ScenePath = Folder + "/ImplicitTextSizeSandbox.unity";

    private static readonly Color s_ButtonColor = new Color(0.25f, 0.6f, 0.95f);
    private static readonly Color s_PanelColor = new Color(0.2f, 0.2f, 0.26f);

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

        Caption(root, "Implicit Text Size Group sandbox - left: no group, each text sizes itself; right: a group. Select a " +
                      "group to see its texts, exclude one, or change a label.", 40f, 20f, 1840f);

        // TextMeshPro buttons.
        Caption(root, "TextMeshPro buttons", 40f, 80f, 900f);
        Row(Panel(root, "No group (TMP)", 40f, 120f), true, false, "OK", "Settings", "Return to main menu");
        Row(Panel(root, "Group (TMP)", 980f, 120f), true, true, "OK", "Settings", "Return to main menu");

        // Legacy Text buttons.
        Caption(root, "Legacy Text buttons (whole sizes)", 40f, 260f, 900f);
        Row(Panel(root, "No group (Text)", 40f, 300f), false, false, "OK", "Settings", "Return to main menu");
        Row(Panel(root, "Group (Text)", 980f, 300f), false, true, "OK", "Settings", "Return to main menu");

        // Cases the group handles differently.
        Caption(root, "Group cases: 'Excluded' is unchecked in the group; 'Min 40' cannot go below its minimum; " +
                      "'Fixed 18' has auto size off; the right button changes its label every 2 s in Play Mode", 40f, 440f, 1840f);
        var cases = Panel(root, "Group with special cases", 40f, 480f, 1840f);
        var group = cases.gameObject.AddComponent<ImplicitTextSizeGroup>();
        var slot = 0;
        Button(cases, slot++, true, "Return to main menu");
        var excluded = Button(cases, slot++, true, "Excluded");
        var minimum = (TMP_Text)Button(cases, slot++, true, "Min 40");
        minimum.fontSizeMin = 40f;
        var fixedText = (TMP_Text)Button(cases, slot++, true, "Fixed 18");
        fixedText.enableAutoSizing = false;
        fixedText.fontSize = 18f;
        var inactive = Button(cases, slot++, true, "Inactive");
        inactive.transform.parent.gameObject.SetActive(false);
        var cycling = Button(cases, slot, true, "Play");
        cycling.gameObject.AddComponent<SandboxTextCycler>();
        group.SetExcluded(excluded, true);

        // Nested: the inner group sizes its own texts.
        Caption(root, "Nested: the outer group ignores the texts under the inner group, which sizes them on its own", 40f, 640f, 1840f);
        var outer = Panel(root, "Outer group", 40f, 680f, 1840f, 220f);
        outer.gameObject.AddComponent<ImplicitTextSizeGroup>();
        Button(outer, 0, true, "OK");
        Button(outer, 1, true, "Settings");
        var inner = Container(outer, "Inner group", new Vector2(980f, 20f), new Vector2(840f, 180f));
        inner.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.28f, 0.36f);
        inner.gameObject.AddComponent<ImplicitTextSizeGroup>();
        Button(inner, 0, true, "Yes");
        Button(inner, 1, true, "No, take me back");

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        return ScenePath;
    }

    private static RectTransform Panel(Transform parent, string name, float x, float y, float width = 900f, float height = 120f)
    {
        var panel = Container(parent, name, new Vector2(x, y), new Vector2(width, height));
        panel.gameObject.AddComponent<Image>().color = s_PanelColor;
        return panel;
    }

    private static void Row(RectTransform panel, bool tmp, bool grouped, params string[] labels)
    {
        if (grouped)
            panel.gameObject.AddComponent<ImplicitTextSizeGroup>();

        for (var i = 0; i < labels.Length; i++)
            Button(panel, i, tmp, labels[i]);
    }

    // A 280x80 button in slot `index` of a panel, with its label as a child, like a real menu.
    private static Graphic Button(RectTransform panel, int index, bool tmp, string label)
    {
        var button = Container(panel, "Button " + label, new Vector2(20f + index * 300f, 20f), new Vector2(280f, 80f));
        var image = button.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_ButtonColor;
        button.gameObject.AddComponent<Button>();

        var labelRect = Container(button, "Label", new Vector2(10f, 8f), new Vector2(260f, 64f));
        if (tmp)
        {
            var text = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 72f;
            return text;
        }

        var legacy = labelRect.gameObject.AddComponent<Text>();
        legacy.font = s_Font;
        legacy.text = label;
        legacy.color = Color.white;
        legacy.alignment = TextAnchor.MiddleCenter;
        // Wrap, so best fit also shrinks for the width; with Overflow it only looks at the height.
        legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
        legacy.resizeTextForBestFit = true;
        legacy.resizeTextMinSize = 10;
        legacy.resizeTextMaxSize = 72;
        return legacy;
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

    private static void Caption(Transform parent, string text, float x, float y, float width)
    {
        var rect = Container(parent, "Caption", new Vector2(x, y), new Vector2(width, 34f));
        var label = rect.gameObject.AddComponent<Text>();
        label.font = s_Font;
        label.text = text;
        label.fontSize = 22;
        label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft;
    }
}
