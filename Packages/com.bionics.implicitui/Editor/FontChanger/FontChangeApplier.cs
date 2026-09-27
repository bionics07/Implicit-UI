using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplicitUI.Editor
{
    // One line of the Font Changer log, with what to highlight when clicked.
    internal readonly struct FontChangeLogEntry
    {
        internal readonly string Message;
        internal readonly Object Context;
        internal readonly bool Changed;

        internal FontChangeLogEntry(string message, Object context, bool changed)
        {
            Message = message;
            Context = context;
            Changed = changed;
        }
    }

    // Applies the checked items of a scan. Each text is found again from the path stored at scan time and checked against
    // what the scan saw; anything that moved since is skipped and reported, never changed blindly. Scene objects get Undo;
    // prefab assets are opened, changed and saved directly, with version control as the way back.
    internal static class FontChangeApplier
    {
        internal static List<FontChangeLogEntry> Apply(FontChangeScan scan)
        {
            var log = new List<FontChangeLogEntry>();
            var settings = scan.Settings;
            if (settings.Validate().Count > 0)
                return log;

            Undo.SetCurrentGroupName("Change Fonts");
            var undoGroup = Undo.GetCurrentGroup();
            var prefabItems = new Dictionary<string, List<FontChangeItem>>();

            foreach (var item in scan.Items)
            {
                if (!item.Selected || !item.CanChange)
                    continue;

                if (item.Location == FontChangeLocation.PrefabAsset)
                {
                    if (!prefabItems.TryGetValue(item.AssetPath, out var list))
                        prefabItems[item.AssetPath] = list = new List<FontChangeItem>();
                    list.Add(item);
                    continue;
                }

                var root = OpenRoot(item);
                var component = root != null ? Find(root, item, settings.Kind) : null;
                if (Change(settings, item, component, true, log) && root != null)
                    EditorSceneManager.MarkSceneDirty(root.scene);
            }

            Undo.CollapseUndoOperations(undoGroup);

            foreach (var pair in prefabItems)
                ApplyToPrefab(settings, pair.Key, pair.Value, log);

            return log;
        }

        private static void ApplyToPrefab(FontChangeSettings settings, string path, List<FontChangeItem> items,
            List<FontChangeLogEntry> log)
        {
            // Saving over a prefab that has unsaved edits in Prefab Mode would lose one side or the other.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                log.Add(new FontChangeLogEntry("Skipped " + path + ": it is open in Prefab Mode. Close it and scan again.",
                    AssetDatabase.LoadAssetAtPath<GameObject>(path), false));
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var changed = false;
                foreach (var item in items)
                    changed |= Change(settings, item, Find(root, item, settings.Kind), false, log);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                    if (!saved)
                    {
                        log.Add(new FontChangeLogEntry("Could not save " + path + "; its texts were not changed.",
                            AssetDatabase.LoadAssetAtPath<GameObject>(path), false));
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Changes one text if it is still what the scan saw. Returns whether it changed.
        private static bool Change(FontChangeSettings settings, FontChangeItem item, Component component, bool withUndo,
            List<FontChangeLogEntry> log)
        {
            var where = item.AssetPath + (string.IsNullOrEmpty(item.AssetPath) ? "" : " > ") + item.DisplayPath;

            // A prefab asset's contents are unloaded right after saving, so its log lines point at the asset instead.
            var context = item.Location == FontChangeLocation.PrefabAsset
                ? AssetDatabase.LoadAssetAtPath<GameObject>(item.AssetPath)
                : (Object)component;
            if (component == null)
            {
                log.Add(new FontChangeLogEntry("Skipped " + where + ": not found any more. Scan again.", context, false));
                return false;
            }

            var serialized = new SerializedObject(component);
            var fontProperty = serialized.FindProperty(FontKinds.FontProperty(settings.Kind));
            var materialProperty = FontKinds.HasMaterial(settings.Kind)
                ? serialized.FindProperty(FontKinds.TmpMaterialProperty)
                : null;
            if (fontProperty.objectReferenceValue != item.CurrentFont ||
                (materialProperty != null && materialProperty.objectReferenceValue != item.CurrentMaterial))
            {
                log.Add(new FontChangeLogEntry("Skipped " + where + ": its font or material changed since the scan. Scan again.",
                    context, false));
                return false;
            }

            fontProperty.objectReferenceValue = settings.ToFont;
            if (materialProperty != null)
                materialProperty.objectReferenceValue = settings.TargetMaterial;

            if (withUndo)
                serialized.ApplyModifiedProperties();
            else
                serialized.ApplyModifiedPropertiesWithoutUndo();

            log.Add(new FontChangeLogEntry("Changed " + where + ": " + Name(item.CurrentFont) + " -> " + settings.ToFont.name +
                                           (materialProperty != null ? " (" + Name(settings.TargetMaterial) + ")" : ""),
                context, true));
            return true;
        }

        // The scene root or Prefab Mode root the item was found under, or null when it is gone.
        internal static GameObject OpenRoot(FontChangeItem item)
        {
            if (item.Location == FontChangeLocation.PrefabStage)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                return stage != null && stage.assetPath == item.AssetPath ? stage.prefabContentsRoot : null;
            }

            if (!item.Scene.IsValid() || !item.Scene.isLoaded)
                return null;

            var roots = item.Scene.GetRootGameObjects();
            return item.RootIndex < roots.Length ? roots[item.RootIndex] : null;
        }

        internal static Component Find(GameObject root, FontChangeItem item, FontKind kind)
        {
            var current = root.transform;
            foreach (var index in item.ChildPath)
            {
                if (index >= current.childCount)
                    return null;
                current = current.GetChild(index);
            }

            var components = current.GetComponents(FontKinds.ComponentType(kind));
            return item.ComponentIndex >= 0 && item.ComponentIndex < components.Length
                ? components[item.ComponentIndex]
                : null;
        }

        private static string Name(Object asset)
        {
            return asset != null ? asset.name : "None";
        }
    }
}
