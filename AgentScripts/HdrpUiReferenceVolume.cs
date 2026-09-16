using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// HDRP host only. HDRP draws Screen Space - Camera and World Space canvases before post-processing, so the default
// volume's ACES tonemapping darkens and desaturates them, while the Overlay column stays untouched. That makes the
// columns impossible to compare by eye. This adds a global volume that turns tonemapping off in the test scene.
//
// Copy into the HDRP host's Assets/Editor, then use the menu Implicit UI > Add UI Reference Volume and save the scene.
// Disable the created GameObject to see the pipeline's default look again.
public static class HdrpUiReferenceVolume
{
    private const string ProfilePath = "Assets/Settings/ImplicitUIReferenceVolumeProfile.asset";
    private const string ObjectName = "UI Reference Volume (no tonemapping)";

    [MenuItem("Implicit UI/Add UI Reference Volume")]
    public static void Add()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.name = nameof(Tonemapping);
            tonemapping.mode.Override(TonemappingMode.None);
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
            AssetDatabase.SaveAssets();
        }

        var existing = GameObject.Find(ObjectName);
        if (existing != null)
        {
            Selection.activeGameObject = existing;
            return;
        }

        var volume = new GameObject(ObjectName).AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;

        Undo.RegisterCreatedObjectUndo(volume.gameObject, "Add UI Reference Volume");
        EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
        Selection.activeGameObject = volume.gameObject;
    }
}
