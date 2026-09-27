using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ImplicitUI.Editor
{
    // Where the Font Changer looks: the loaded scenes (or the prefab open in Prefab Mode), or the prefabs in a folder.
    internal enum FontChangeMode
    {
        Scene,
        Folder,
    }

    // What the user asked for. From font and from material are filters (empty means any); the target material is
    // optional and defaults to the target font's own.
    internal sealed class FontChangeSettings
    {
        internal FontKind Kind = FontKind.TextMeshPro;
        internal FontChangeMode Mode = FontChangeMode.Scene;
        internal Object FromFont;
        internal Material FromMaterial;
        internal Object ToFont;
        internal Material ToMaterial;
        internal string Folder = "Assets";
        internal bool IncludeSubfolders = true;

        // The material texts end up with: the chosen one, or the target font's default.
        internal Material TargetMaterial => ToMaterial != null ? ToMaterial : FontKinds.DefaultMaterial(ToFont);

        // Problems that must stop a scan; an empty list means the settings can run.
        internal List<string> Validate()
        {
            var errors = new List<string>();
            if (!FontKinds.IsAvailable(Kind))
                errors.Add("TextMeshPro is not installed in this project.");
            if (ToFont == null)
                errors.Add("Choose the font to change to.");
            if (FromFont != null && FromFont == ToFont && FromMaterial == null && ToMaterial == null)
                errors.Add("From and To are the same font.");
            if (ToMaterial != null && ToFont != null && !FontKinds.MaterialBelongsTo(ToMaterial, ToFont))
            {
                errors.Add("The material '" + ToMaterial.name + "' was made for another font's atlas and would render " +
                           "garbage with '" + ToFont.name + "'. Pick a material of '" + ToFont.name + "', or leave it empty.");
            }

            // Prefabs inside packages are read-only.
            if (Mode == FontChangeMode.Folder &&
                (Folder == null || !(Folder == "Assets" || Folder.StartsWith("Assets/")) ||
                 !UnityEditor.AssetDatabase.IsValidFolder(Folder)))
                errors.Add("Choose a folder inside Assets.");
            return errors;
        }
    }

    // Why a text found by the scan is or is not changed.
    internal enum FontChangeReason
    {
        // Changed when applied.
        WillChange,

        // Changed when applied, but its own material is replaced by the target one: unchecked by default.
        ReplacesCustomMaterial,

        // Never changed: the value comes from a prefab, and changing it here would spread an override per instance.
        Inherited,
    }

    // Where a found text lives, which decides how it is changed: scene objects with Undo, the prefab open in Prefab Mode
    // like a scene (the user saves it), prefab assets loaded, changed and saved directly - no Undo, version control covers it.
    internal enum FontChangeLocation
    {
        Scene,
        PrefabStage,
        PrefabAsset,
    }

    // One text found by the scan. It stores how to find the component again - the asset or scene, the child indices from
    // the root, and which component of its type on that GameObject - because prefabs are unloaded between scan and apply,
    // and names ("Label", "Text") are rarely unique.
    internal sealed class FontChangeItem
    {
        internal FontChangeLocation Location;

        // The scene or prefab path, for display and for loading prefab assets.
        internal string AssetPath;

        // Scenes can be unsaved and share an empty path, so scene items keep the scene itself.
        internal Scene Scene;
        internal int RootIndex;
        internal int[] ChildPath;
        internal int ComponentIndex;
        internal string DisplayPath;
        internal Object CurrentFont;
        internal Material CurrentMaterial;
        internal FontChangeReason Reason;
        internal string InheritedFrom;
        internal bool Selected;

        internal bool CanChange => Reason != FontChangeReason.Inherited;
    }

    // The dry run: what would change, and the warnings to read before applying.
    internal sealed class FontChangeScan
    {
        internal readonly FontChangeSettings Settings;
        internal readonly List<FontChangeItem> Items = new List<FontChangeItem>();
        internal readonly List<string> Warnings = new List<string>();

        internal FontChangeScan(FontChangeSettings settings)
        {
            Settings = settings;
        }

        internal int SelectedCount
        {
            get
            {
                var count = 0;
                foreach (var item in Items)
                {
                    if (item.Selected && item.CanChange)
                        count++;
                }

                return count;
            }
        }
    }
}
