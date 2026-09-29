using System;
using UnityEditor;
using UnityEngine;

// Sets the Editor up for a store screenshot: a floating Inspector, big enough to read, parked over the right
// hand side of the Editor window, plus helpers to pick the object each shot is about. Maintainer tool.
//
// Unity Pipeline: run_script --file AgentScripts/ShotSetup.cs --entry ShotSetup.<Method>
public static class ShotSetup
{
    // The Asset Store refuses anything under 1200 px wide, so the window has to be captured at that size or more
    // rather than scaled up afterwards. Tunable through EditorPrefs so a shot can be reframed without a recompile.
    private static int X => EditorPrefs.GetInt("ImplicitUI.ShotX", 1090);
    private static int Y => EditorPrefs.GetInt("ImplicitUI.ShotY", 120);
    private static int Width => EditorPrefs.GetInt("ImplicitUI.ShotWidth", 1450);
    private static int Height => EditorPrefs.GetInt("ImplicitUI.ShotHeight", 900);

    private static EditorWindow s_Inspector;

    public static string OpenInspector()
    {
        var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        if (type == null)
            return "InspectorWindow type not found";

        var window = ScriptableObject.CreateInstance(type) as EditorWindow;
        if (window == null)
            return "could not create the window";

        window.titleContent = new GUIContent("Inspector");
        window.Show();
        window.position = new Rect(X, Y, Width, Height);
        window.Focus();
        s_Inspector = window;
        return "inspector at " + window.position;
    }

    // Unity ignores the rect set in the same frame the window is shown, so placing it is a second step.
    public static string PlaceInspector()
    {
        var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        foreach (var candidate in Resources.FindObjectsOfTypeAll(type))
        {
            var window = candidate as EditorWindow;
            if (window == null || window.docked)
                continue;

            window.position = new Rect(X, Y, Width, Height);
            window.Repaint();
            return "placed at " + window.position;
        }

        return "no floating inspector";
    }

    // Collapses everything the shot is not about, so the Implicit UI component sits at the top of the Inspector.
    public static string Tidy()
    {
        var target = Selection.activeGameObject;
        if (target == null)
            return "nothing selected";

        var collapsed = 0;
        foreach (var component in target.GetComponents<Component>())
        {
            if (component == null)
                continue;

            var keep = component.GetType().Name.StartsWith("Implicit", StringComparison.Ordinal);
            UnityEditorInternal.InternalEditorUtility.SetIsInspectorExpanded(component, keep);
            if (!keep)
                collapsed++;
        }

        var height = EditorPrefs.GetInt("ImplicitUI.ShotHeight", 0);
        if (height > 0)
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            foreach (var candidate in Resources.FindObjectsOfTypeAll(type))
            {
                var window = candidate as EditorWindow;
                if (window == null || window.docked)
                    continue;

                window.position = new Rect(X, Y, Width, height);
            }
        }

        // A floating Inspector keeps its OWN tracker, so rebuilding the shared one changes nothing there. Its
        // preview pane also has to be folded away, or it eats most of the window in the screenshot.
        var notes = "";
        var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        foreach (var candidate in Resources.FindObjectsOfTypeAll(inspectorType))
        {
            var window = candidate as EditorWindow;
            if (window == null)
                continue;

            try
            {
                var trackerProperty = window.GetType().GetProperty("tracker",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                var tracker = trackerProperty?.GetValue(window) as ActiveEditorTracker;
                tracker?.ForceRebuild();
                notes += tracker != null ? " tracker" : " no-tracker";
            }
            catch (Exception error)
            {
                notes += " tracker-failed(" + error.GetType().Name + ")";
            }

            try
            {
                var field = window.GetType().GetField("m_PreviewResizer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.FlattenHierarchy);
                var resizer = field?.GetValue(window);
                var setExpanded = resizer?.GetType().GetMethod("SetExpanded",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (setExpanded != null)
                {
                    setExpanded.Invoke(resizer, new object[] { false });
                    notes += " preview-folded";
                }
            }
            catch (Exception error)
            {
                notes += " preview-failed(" + error.GetType().Name + ")";
            }

            window.Repaint();
        }

        return "collapsed " + collapsed + " component(s):" + notes;
    }

    public static string CloseExtraInspectors()
    {
        var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        var windows = Resources.FindObjectsOfTypeAll(type);
        var closed = 0;
        foreach (var candidate in windows)
        {
            var window = candidate as EditorWindow;
            if (window == null || window.docked)
                continue;

            window.Close();
            closed++;
        }

        s_Inspector = null;
        return "closed " + closed + " floating inspector(s)";
    }

    public static string Select()
    {
        var name = EditorPrefs.GetString("ImplicitUI.ShotTarget", "");
        if (string.IsNullOrEmpty(name))
            return "set ImplicitUI.ShotTarget first";

        // A full path first: several sandboxes hold more than one object of the same name.
        var target = GameObject.Find(name) ?? FindByPath(name);
        if (target == null)
            return "not found: " + name;

        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);
        SceneView.lastActiveSceneView?.FrameSelected();
        return "selected " + name;
    }

    private static GameObject FindByPath(string path)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == path)
                return root;

            var found = root.transform.Find(path);
            if (found != null)
                return found.gameObject;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == path)
                    return child.gameObject;
            }
        }

        return null;
    }

    // In Edit Mode the Game view only repaints when it needs to, so a capture right after a scene change finds it
    // blank. This forces every view to draw before the screenshot is taken.
    public static string RepaintAll()
    {
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        EditorApplication.QueuePlayerLoopUpdate();
        return "repainted";
    }

    // UI reads far better in 2D, and framing the selection alone zooms past everything around it.
    public static string FrameScene()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null)
            return "no scene view";

        var target = Selection.activeGameObject;
        if (target == null)
            return "nothing selected";

        var size = EditorPrefs.GetFloat("ImplicitUI.ShotFrame", 240f);
        view.in2DMode = true;

        // The sandbox scenes keep the template's skybox; behind flat UI it is just noise in a screenshot.
        var state = view.sceneViewState;
        state.showSkybox = false;
        state.showFog = false;
        state.showFlares = false;

        view.LookAt(target.transform.position, Quaternion.identity, size, true, true);
        view.Repaint();
        return "framed " + target.name + " at size " + size;
    }

    public static string OpenFontChanger()
    {
        if (!EditorApplication.ExecuteMenuItem("Tools/Implicit UI/Font Changer"))
            return "menu item not found";

        foreach (var candidate in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (candidate.titleContent.text.Contains("Font Changer"))
            {
                candidate.position = new Rect(560, 150, 1180, 1080);
                candidate.Focus();
                return "font changer at " + candidate.position;
            }
        }

        return "window not found after opening";
    }
}
