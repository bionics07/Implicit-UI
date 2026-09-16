using ImplicitUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds Assets/ImplicitUIPipelineTest.unity inside a render pipeline host project (Built-in, HDRP): the same Implicit
// Grayscale cases on a Screen Space - Overlay, a Screen Space - Camera and a World Space canvas, to check the shader by
// eye in each pipeline. Overlay skips the pipeline entirely; Camera and World are where a pipeline can break it.
//
// The host has no Pipeline package, so this runs in batch mode. Copy it into the host's Assets/Editor, then:
//   Unity -batchmode -quit -projectPath <host> -executeMethod BuildPipelineHostScene.Build
public static class BuildPipelineHostScene
{
    private const string ScenePath = "Assets/ImplicitUIPipelineTest.unity";

    private static readonly Color s_Color = new Color(0.92f, 0.3f, 0.25f);

    private static Sprite s_Square;
    private static Sprite s_Circle;
    private static Font s_Font;

    public static void Build()
    {
        s_Square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        s_Circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Default game objects give a camera and a light set up for whichever pipeline the host uses.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);

        // Overlay: left third of the screen.
        var overlay = CreateCanvas("Overlay Canvas", RenderMode.ScreenSpaceOverlay, camera);
        Column(Area(overlay.transform, 0f, 0.33f), "Screen Space - Overlay");

        // Camera: middle third, drawn by the camera five units ahead.
        var cameraCanvas = CreateCanvas("Camera Canvas", RenderMode.ScreenSpaceCamera, camera);
        cameraCanvas.planeDistance = 5f;
        Column(Area(cameraCanvas.transform, 0.33f, 0.66f), "Screen Space - Camera");

        // World: a panel in the scene, right of center, facing the camera.
        var world = CreateCanvas("World Canvas", RenderMode.WorldSpace, camera);
        var worldRect = (RectTransform)world.transform;
        worldRect.sizeDelta = new Vector2(500f, 900f);
        worldRect.localScale = Vector3.one * 0.01f;
        worldRect.position = new Vector3(4.2f, 0f, 0f);
        Column(worldRect, "World Space");

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[Implicit UI] Built " + ScenePath);
    }

    private static Canvas CreateCanvas(string name, RenderMode mode, Camera camera)
    {
        var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = mode;
        canvas.worldCamera = camera;
        if (mode != RenderMode.WorldSpace)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
        }

        return canvas;
    }

    private static RectTransform Area(Transform parent, float fromX, float toX)
    {
        var rect = (RectTransform)new GameObject("Area", typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(fromX, 0f);
        rect.anchorMax = new Vector2(toX, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // One case per row, top to bottom, inside a stretched area.
    private static void Column(RectTransform area, string title)
    {
        Label(area, title, 0, 36);

        Label(area, "No component (reference color)", 1, 22);
        Case(area, 1, null);

        Label(area, "Implicit Grayscale, intensity 1", 2, 22);
        Case(area, 2, grayscale => grayscale.Intensity = 1f);

        Label(area, "Sepia tint", 3, 22);
        Case(area, 3, grayscale =>
        {
            grayscale.Intensity = 1f;
            grayscale.TintColor = new Color(0.95f, 0.8f, 0.55f);
            grayscale.TintIntensity = 1f;
        });

        Label(area, "Inside a Mask (circle)", 4, 22);
        var mask = Box(area, 4, "Mask");
        mask.sprite = s_Circle;
        mask.type = Image.Type.Simple;
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var masked = Fill(mask.transform, "Masked");
        masked.gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;

        Label(area, "RectMask2D with softness", 5, 22);
        var softHolder = Box(area, 5, "Soft mask holder");
        softHolder.enabled = false;
        var softMask = softHolder.gameObject.AddComponent<RectMask2D>();
        softMask.softness = new Vector2Int(25, 25);
        var soft = Fill(softHolder.transform, "Soft masked");
        soft.gameObject.AddComponent<ImplicitGrayscale>().Intensity = 1f;

        Label(area, "Disabled Button (no color transition)", 6, 22);
        var button = Case(area, 6, null);
        button.gameObject.AddComponent<ImplicitGrayscale>();
        var selectable = button.gameObject.AddComponent<Button>();
        selectable.transition = Selectable.Transition.None;
        selectable.interactable = false;
    }

    private static Image Case(RectTransform area, int row, System.Action<ImplicitGrayscale> configure)
    {
        var image = Box(area, row, "Case " + row);
        if (configure != null)
            configure(image.gameObject.AddComponent<ImplicitGrayscale>());
        return image;
    }

    private static Image Box(RectTransform area, int row, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(area, false);
        rect.anchorMin = new Vector2(0.55f, 1f - (row + 0.9f) / 7f);
        rect.anchorMax = new Vector2(0.95f, 1f - (row + 0.1f) / 7f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_Color;
        return image;
    }

    private static Image Fill(Transform parent, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-10f, -10f);
        rect.offsetMax = new Vector2(10f, 10f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = s_Square;
        image.type = Image.Type.Sliced;
        image.color = s_Color;
        return image;
    }

    private static void Label(RectTransform area, string text, int row, int size)
    {
        var rect = (RectTransform)new GameObject("Label", typeof(RectTransform)).transform;
        rect.SetParent(area, false);
        rect.anchorMin = new Vector2(0.03f, 1f - (row + 0.9f) / 7f);
        rect.anchorMax = new Vector2(row == 0 ? 0.97f : 0.53f, 1f - (row + 0.1f) / 7f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var label = rect.gameObject.AddComponent<Text>();
        label.font = s_Font;
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
    }
}
