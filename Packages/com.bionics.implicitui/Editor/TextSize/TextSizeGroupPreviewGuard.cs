using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ImplicitUI.Editor
{
    // Text size groups preview their sizes in Edit Mode by lowering each text's maximum in memory. Those values follow
    // whatever the texts around them say today, so they must never reach a saved file or survive into Play Mode, where
    // the content may well be different: this puts the original
    // maximums back right before a scene or prefab is saved, before Play Mode serializes the scene, and before scripts
    // reload (the group's memory of the originals does not survive a reload), then lets the groups apply again.
    [InitializeOnLoad]
    internal static class TextSizeGroupPreviewGuard
    {
        static TextSizeGroupPreviewGuard()
        {
            EditorSceneManager.sceneSaving += (scene, path) => ForGroups(group => group.gameObject.scene == scene,
                group => group.RestoreAll());
            EditorSceneManager.sceneSaved += scene => ForGroups(group => group.gameObject.scene == scene,
                group => group.RefreshIfChanged());
            PrefabStage.prefabSaving += root => ForGroups(group => group.transform.IsChildOf(root.transform),
                group => group.RestoreAll());
            PrefabStage.prefabSaved += root => ForGroups(group => group.transform.IsChildOf(root.transform),
                group => group.RefreshIfChanged());
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    RestoreAllGroups();
            };
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAllGroups;
        }

        internal static void RestoreAllGroups()
        {
            ForGroups(group => true, group => group.RestoreAll());
        }

        private static void ForGroups(Func<ImplicitTextSizeGroup, bool> filter, Action<ImplicitTextSizeGroup> action)
        {
            // Copied first, so a text callback that enables or disables a group cannot change the set mid-loop.
            foreach (var group in new List<ImplicitTextSizeGroup>(ImplicitTextSizeGroup.Enabled))
            {
                if (group != null && filter(group))
                    action(group);
            }
        }
    }
}
