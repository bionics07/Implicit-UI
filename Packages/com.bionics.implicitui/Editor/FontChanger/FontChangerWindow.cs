using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ImplicitUI.Editor
{
    // Tools > Implicit UI > Font Changer. Three steps, never fewer: set up, Scan (a dry run listing everything that would
    // change, with a checkbox each), then Apply to the checked texts only. Any change to the setup throws the scan away, so
    // Apply always matches what is on screen.
    internal sealed class FontChangerWindow : EditorWindow
    {
        private const int MaxPreviewLength = 60;

        private readonly FontChangeSettings m_Settings = new FontChangeSettings();
        private FontChangeScan m_Scan;
        private List<FontChangeLogEntry> m_Log;
        private Vector2 m_Scroll;

        [MenuItem("Tools/Implicit UI/Font Changer")]
        private static void Open()
        {
            GetWindow<FontChangerWindow>("Font Changer");
        }

        private void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUI.BeginChangeCheck();
            DrawSettings();
            if (EditorGUI.EndChangeCheck())
                m_Scan = null;

            var errors = m_Settings.Validate();
            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);

            EditorGUILayout.HelpBox(
                "Prefab assets are saved as soon as you apply, and that cannot be undone from Unity. Commit your project " +
                "before applying so version control can bring anything back. Scene changes can be undone with Ctrl+Z.",
                MessageType.Warning);

            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                if (GUILayout.Button("Scan (nothing is changed)", GUILayout.Height(26f)))
                {
                    m_Scan = FontChangeScanner.Scan(m_Settings);
                    m_Log = null;
                }
            }

            if (m_Scan != null)
                DrawScan();

            if (m_Log != null)
                DrawLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSettings()
        {
            var kind = (FontKind)GUILayout.Toolbar((int)m_Settings.Kind, new[] { "TextMeshPro", "Legacy Text" });
            if (kind != m_Settings.Kind)
            {
                // Fonts of one kind are not valid for the other.
                m_Settings.Kind = kind;
                m_Settings.FromFont = m_Settings.ToFont = null;
                m_Settings.FromMaterial = m_Settings.ToMaterial = null;
            }

            m_Settings.Mode = (FontChangeMode)GUILayout.Toolbar((int)m_Settings.Mode,
                new[] { "Open scenes (or Prefab Mode)", "Prefabs in a folder" });
            if (m_Settings.Mode == FontChangeMode.Folder)
            {
                var current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(m_Settings.Folder);
                var folder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", current, typeof(DefaultAsset), false);
                if (folder != current)
                    m_Settings.Folder = folder != null ? AssetDatabase.GetAssetPath(folder) : null;

                m_Settings.IncludeSubfolders = EditorGUILayout.Toggle("Include Subfolders", m_Settings.IncludeSubfolders);
            }

            EditorGUILayout.Space();
            var fontType = FontKinds.FontType(m_Settings.Kind);
            var hasMaterial = FontKinds.HasMaterial(m_Settings.Kind);
            m_Settings.FromFont = EditorGUILayout.ObjectField(
                new GUIContent("From Font", "Only texts using this font. Empty: every font."),
                m_Settings.FromFont, fontType, false);
            if (hasMaterial)
            {
                m_Settings.FromMaterial = (Material)EditorGUILayout.ObjectField(
                    new GUIContent("From Material", "Only texts using this material, to keep each preset (outline, " +
                                                    "shadow...) by running once per preset. Empty: every material."),
                    m_Settings.FromMaterial, typeof(Material), false);
            }

            m_Settings.ToFont = EditorGUILayout.ObjectField("To Font", m_Settings.ToFont, fontType, false);
            if (hasMaterial)
            {
                m_Settings.ToMaterial = (Material)EditorGUILayout.ObjectField(
                    new GUIContent("To Material", "A material made for the To font. Empty: that font's default material."),
                    m_Settings.ToMaterial, typeof(Material), false);
            }
        }

        private void DrawScan()
        {
            EditorGUILayout.Space();
            foreach (var warning in m_Scan.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            if (m_Scan.Items.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing to change.", MessageType.Info);
                return;
            }

            // Grouped by the material (or font) texts use now, so presets are visible before they are flattened.
            var groups = new Dictionary<Object, List<FontChangeItem>>();
            var order = new List<Object>();
            foreach (var item in m_Scan.Items)
            {
                var key = FontKinds.HasMaterial(m_Settings.Kind) ? item.CurrentMaterial : item.CurrentFont;
                var groupKey = key != null ? key : this;
                if (!groups.TryGetValue(groupKey, out var list))
                {
                    groups[groupKey] = list = new List<FontChangeItem>();
                    order.Add(groupKey);
                }

                list.Add(item);
            }

            foreach (var key in order)
                DrawGroup(key == this ? null : key, groups[key]);

            EditorGUILayout.Space();
            var selected = m_Scan.SelectedCount;
            using (new EditorGUI.DisabledScope(selected == 0))
            {
                if (GUILayout.Button("Apply to " + selected + " text(s)", GUILayout.Height(26f)) && Confirm(selected))
                {
                    m_Log = FontChangeApplier.Apply(m_Scan);
                    m_Scan = null;
                    ReportToConsole();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void DrawGroup(Object key, List<FontChangeItem> items)
        {
            var kindLabel = FontKinds.HasMaterial(m_Settings.Kind) ? "Material" : "Font";
            var allSelected = true;
            foreach (var item in items)
                allSelected &= !item.CanChange || item.Selected;

            EditorGUILayout.Space();
            var toggled = EditorGUILayout.ToggleLeft(
                kindLabel + ": " + (key != null ? key.name : "None") + " (" + items.Count + ")", allSelected,
                EditorStyles.boldLabel);
            if (toggled != allSelected)
            {
                foreach (var item in items)
                {
                    if (item.CanChange)
                        item.Selected = toggled;
                }
            }

            EditorGUI.indentLevel++;
            foreach (var item in items)
                DrawItem(item);
            EditorGUI.indentLevel--;
        }

        private void DrawItem(FontChangeItem item)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!item.CanChange))
                item.Selected = EditorGUILayout.Toggle(item.Selected && item.CanChange, GUILayout.Width(34f));

            // What the text says tells two "Label" objects apart; the path and the file are in the tooltip.
            var where = item.Location == FontChangeLocation.PrefabAsset ? item.AssetPath + " > " : "";
            var label = new GUIContent(Preview(item), where + item.DisplayPath + "\nClick to highlight it");
            if (GUILayout.Button(label, EditorStyles.label))
                Ping(item);

            GUILayout.FlexibleSpace();
            GUILayout.Label(Note(item), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // The text on one line, cut to fit a row, with the object name when the text is empty.
        private static string Preview(FontChangeItem item)
        {
            var content = item.Content;
            if (string.IsNullOrEmpty(content))
                return "(empty) " + item.DisplayPath;

            content = content.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return content.Length > MaxPreviewLength ? content.Substring(0, MaxPreviewLength) + "…" : content;
        }

        private static string Note(FontChangeItem item)
        {
            if (!item.Active)
                return item.Reason == FontChangeReason.Inherited ? "inactive, inherited - skipped" : "inactive";

            switch (item.Reason)
            {
                case FontChangeReason.Inherited:
                    return "inherited from " + item.InheritedFrom + " - skipped";
                case FontChangeReason.ReplacesCustomMaterial:
                    return "replaces its own material";
                default:
                    return item.CurrentFont != null ? "from " + item.CurrentFont.name : "no font";
            }
        }

        private void Ping(FontChangeItem item)
        {
            if (item.Location == FontChangeLocation.PrefabAsset)
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(item.AssetPath));
                return;
            }

            var root = FontChangeApplier.OpenRoot(item);
            var component = root != null ? FontChangeApplier.Find(root, item, m_Settings.Kind) : null;
            if (component != null)
                EditorGUIUtility.PingObject(component.gameObject);
        }

        private bool Confirm(int selected)
        {
            var prefabs = new HashSet<string>();
            foreach (var item in m_Scan.Items)
            {
                if (item.Selected && item.CanChange && item.Location == FontChangeLocation.PrefabAsset)
                    prefabs.Add(item.AssetPath);
            }

            var message = "Change the font of " + selected + " text(s) to '" + m_Settings.ToFont.name + "'?";
            if (prefabs.Count > 0)
                message += "\n\n" + prefabs.Count + " prefab asset(s) will be saved right away. Unity cannot undo that; " +
                           "version control can.";

            return EditorUtility.DisplayDialog("Font Changer", message, "Apply", "Cancel");
        }

        private void DrawLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log (click a line to highlight it)", EditorStyles.boldLabel);
            foreach (var entry in m_Log)
            {
                var style = entry.Changed ? EditorStyles.label : EditorStyles.boldLabel;
                if (GUILayout.Button(entry.Message, style) && entry.Context != null)
                    EditorGUIUtility.PingObject(entry.Context);
            }
        }

        private void ReportToConsole()
        {
            var changed = 0;
            foreach (var entry in m_Log)
            {
                if (entry.Changed)
                    changed++;
            }

            var skipped = m_Log.Count - changed;
            Debug.Log("[Implicit UI] Font Changer changed " + changed + " text(s)" +
                      (skipped > 0 ? " and skipped " + skipped + " (see the Font Changer window)." : "."));
        }
    }
}
