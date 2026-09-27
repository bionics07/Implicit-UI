using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ImplicitUI.Editor
{
    // The dry run. Finds every text of the chosen kind that would change, and why some would not, without changing anything.
    // Inactive objects are included: UI panels spend most of their life disabled, and FindObjectsOfType skips them.
    internal static class FontChangeScanner
    {
        private const float LineHeightTolerance = 0.1f;

        internal static FontChangeScan Scan(FontChangeSettings settings)
        {
            var scan = new FontChangeScan(settings);
            if (settings.Validate().Count > 0)
                return scan;

            if (settings.Mode == FontChangeMode.Scene)
                ScanOpen(scan);
            else
                ScanFolder(scan);

            AddWarnings(scan);
            return scan;
        }

        // The prefab open in Prefab Mode is what the user is looking at, so it replaces the scenes behind it.
        private static void ScanOpen(FontChangeScan scan)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                ScanRoot(scan, stage.prefabContentsRoot, 0, FontChangeLocation.PrefabStage, stage.assetPath, default);
                return;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var r = 0; r < roots.Length; r++)
                    ScanRoot(scan, roots[r], r, FontChangeLocation.Scene, scene.path, scene);
            }
        }

        private static void ScanFolder(FontChangeScan scan)
        {
            var settings = scan.Settings;
            var folder = settings.Folder.TrimEnd('/');
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!settings.IncludeSubfolders && Path.GetDirectoryName(path)?.Replace('\\', '/') != folder)
                    continue;

                // Model files show up as prefabs but cannot be opened or saved as one.
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Model)
                    continue;

                paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ScanRoot(scan, root, 0, FontChangeLocation.PrefabAsset, path, default);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ScanRoot(FontChangeScan scan, GameObject root, int rootIndex, FontChangeLocation location,
            string assetPath, Scene scene)
        {
            var type = FontKinds.ComponentType(scan.Settings.Kind);
            foreach (var component in root.GetComponentsInChildren(type, true))
            {
                var item = Inspect(scan.Settings, component);
                if (item == null)
                    continue;

                item.Location = location;
                item.AssetPath = assetPath;
                item.Scene = scene;
                item.RootIndex = rootIndex;
                item.ChildPath = ChildPath(root.transform, component.transform);
                item.ComponentIndex = Array.IndexOf(component.GetComponents(type), component);
                item.DisplayPath = HierarchyPath(component.transform);
                scan.Items.Add(item);
            }
        }

        // Null when the text is not a match or already has the target font and material.
        internal static FontChangeItem Inspect(FontChangeSettings settings, Component component)
        {
            var serialized = new SerializedObject(component);
            var fontProperty = serialized.FindProperty(FontKinds.FontProperty(settings.Kind));
            var font = fontProperty.objectReferenceValue;
            if (settings.FromFont != null && font != settings.FromFont)
                return null;

            var hasMaterial = FontKinds.HasMaterial(settings.Kind);
            var material = hasMaterial
                ? serialized.FindProperty(FontKinds.TmpMaterialProperty).objectReferenceValue as Material
                : null;
            if (settings.FromMaterial != null && material != settings.FromMaterial)
                return null;

            if (font == settings.ToFont && (!hasMaterial || material == settings.TargetMaterial))
                return null;

            var item = new FontChangeItem { CurrentFont = font, CurrentMaterial = material };

            // A value the instance takes from its prefab is changed in that prefab (Folder mode), never here: changing it
            // on every instance would leave an override on each of them.
            if (PrefabUtility.IsPartOfPrefabInstance(component) && !PrefabUtility.IsAddedComponentOverride(component) &&
                !fontProperty.prefabOverride)
            {
                item.Reason = FontChangeReason.Inherited;
                item.InheritedFrom = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component);
            }
            else if (hasMaterial && settings.FromMaterial == null && material != null &&
                     material != FontKinds.DefaultMaterial(font))
            {
                // A preset (outline, shadow...) would be flattened into the target material; the user opts in per group.
                item.Reason = FontChangeReason.ReplacesCustomMaterial;
            }
            else
            {
                item.Reason = FontChangeReason.WillChange;
            }

            item.Selected = item.Reason == FontChangeReason.WillChange;
            return item;
        }

        private static void AddWarnings(FontChangeScan scan)
        {
            var settings = scan.Settings;
            var inherited = 0;
            var fonts = new HashSet<Object>();
            foreach (var item in scan.Items)
            {
                if (item.Reason == FontChangeReason.Inherited)
                    inherited++;
                else if (item.CurrentFont != null)
                    fonts.Add(item.CurrentFont);
            }

            if (inherited > 0)
            {
                scan.Warnings.Add(inherited + " text(s) take their font from a prefab and are skipped here. Run the Folder " +
                                  "mode on the prefab listed next to each one.");
            }

            if (settings.Kind == FontKind.LegacyText)
            {
                scan.Warnings.Add("Fonts differ in width and line height: check the changed texts for overflow.");
            }
            else
            {
                var target = FontKinds.RelativeLineHeight(settings.ToFont);
                foreach (var font in fonts)
                {
                    var current = FontKinds.RelativeLineHeight(font);
                    if (current <= 0f || target <= 0f)
                        continue;

                    var difference = Mathf.Abs(target - current) / current;
                    if (difference > LineHeightTolerance)
                    {
                        scan.Warnings.Add("'" + settings.ToFont.name + "' has a line height " + (difference * 100f).ToString("0") +
                                          "% different from '" + font.name + "': texts may overflow or leave gaps.");
                    }

                    if (FontKinds.HasFallbacks(font) && !FontKinds.HasFallbacks(settings.ToFont))
                    {
                        scan.Warnings.Add("'" + font.name + "' has fallback fonts and '" + settings.ToFont.name + "' has " +
                                          "none: characters only the fallbacks had will show as missing.");
                    }
                }
            }

            if (settings.Mode == FontChangeMode.Scene && PrefabStageUtility.GetCurrentPrefabStage() != null)
                scan.Warnings.Add("Prefab Mode is open, so only that prefab was scanned. Save it after applying.");
        }

        private static int[] ChildPath(Transform root, Transform target)
        {
            var path = new List<int>();
            for (var current = target; current != root; current = current.parent)
                path.Add(current.GetSiblingIndex());

            path.Reverse();
            return path.ToArray();
        }

        private static string HierarchyPath(Transform target)
        {
            var path = target.name;
            for (var parent = target.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;

            return path;
        }
    }
}
