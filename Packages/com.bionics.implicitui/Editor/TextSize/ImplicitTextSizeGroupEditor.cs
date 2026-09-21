using UnityEditor;
using UnityEngine;

namespace ImplicitUI.Editor
{
    // Default fields, the size the group resolved, and every text it found - with a checkbox to leave each one out, the
    // size it would pick alone and the size it gets - so the group never changes a text the user cannot see listed.
    [CustomEditor(typeof(ImplicitTextSizeGroup))]
    internal sealed class ImplicitTextSizeGroupEditor : UnityEditor.Editor
    {
        private const float ToggleWidth = 18f;
        private const float ObjectWidth = 90f;
        private const float SizeWidth = 44f;
        private const float OutcomeWidth = 64f;
        private const int MaxPreviewLength = 60;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // Leaving Play Mode destroys the component before the inspector's last repaint; pattern matching does not see
            // Unity's destroyed state.
            if (!(target is ImplicitTextSizeGroup group) || group == null)
                return;

            group.RefreshIfChanged();
            var entries = group.Entries;
            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No Text or TextMeshPro text below this object.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(group.GroupSize > 0f
                ? "Group size: " + group.GroupSize.ToString("0.##")
                : "Group size: none (no text takes part)", EditorStyles.boldLabel);

            DrawListHeader();
            var anyAboveMinimum = false;
            Component toggled = null;
            foreach (var entry in entries)
            {
                if (DrawRow(group, entry, group.transform))
                    toggled = entry.Text.Component;

                anyAboveMinimum |= entry.State == ImplicitTextSizeGroup.EntryState.MinimumAboveGroup;
            }

            // Applied after the loop: changing the group recomputes it, which rebuilds the list being drawn.
            if (toggled != null)
            {
                var exclude = !group.IsExcluded(toggled);
                Undo.RecordObject(group, exclude ? "Exclude Text from Size Group" : "Include Text in Size Group");
                group.SetExcluded(toggled, exclude);
                EditorUtility.SetDirty(group);
                GUIUtility.ExitGUI();
            }

            if (anyAboveMinimum)
            {
                EditorGUILayout.HelpBox("Texts marked 'min' cannot go below their own auto size minimum, so they stay " +
                                        "larger than the group. Lower their minimum to match.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "In Edit Mode the sizes are a preview: the original maximums go back before saving, entering Play Mode and " +
                "reloading scripts, so saved scenes and prefabs are unchanged. Do not apply a text's Font Size Max as a " +
                "prefab override while the group is previewing it.",
                MessageType.Info);
        }

        private static void DrawListHeader()
        {
            var rect = EditorGUILayout.GetControlRect();
            var style = EditorStyles.miniBoldLabel;
            GUI.Label(new Rect(rect.x, rect.y, ToggleWidth, rect.height), "", style);
            GUI.Label(Column(rect, 1), "Text", style);
            GUI.Label(Column(rect, 2), "Object", style);
            GUI.Label(Column(rect, 3), "Alone", style);
            GUI.Label(Column(rect, 4), "Gets", style);
        }

        // Draws one text and returns true when its checkbox was clicked.
        private static bool DrawRow(ImplicitTextSizeGroup group, ImplicitTextSizeGroup.Entry entry, Transform root)
        {
            var component = entry.Text.Component;
            var rect = EditorGUILayout.GetControlRect();

            var included = !group.IsExcluded(component);
            var clicked = GUI.Toggle(new Rect(rect.x, rect.y, ToggleWidth, rect.height), included, GUIContent.none) !=
                          included;

            // Text objects are usually all called "Label" or "Text", so the content is what tells them apart. The path is
            // in the tooltip, and a click highlights the object in the Hierarchy without leaving this inspector.
            var preview = new GUIContent(Preview(entry.Text.Content), PathFrom(root, component.transform));
            if (GUI.Button(Column(rect, 1), preview, EditorStyles.label))
                EditorGUIUtility.PingObject(component.gameObject);

            using (new EditorGUI.DisabledScope(true))
                EditorGUI.ObjectField(Column(rect, 2), component, typeof(Component), true);

            var takesPart = entry.State == ImplicitTextSizeGroup.EntryState.Included ||
                            entry.State == ImplicitTextSizeGroup.EntryState.MinimumAboveGroup;
            GUI.Label(Column(rect, 3), takesPart ? entry.Natural.ToString("0.##") : "", EditorStyles.miniLabel);
            GUI.Label(Column(rect, 4), Outcome(entry), EditorStyles.miniLabel);
            return clicked;
        }

        private static string Outcome(ImplicitTextSizeGroup.Entry entry)
        {
            switch (entry.State)
            {
                case ImplicitTextSizeGroup.EntryState.Included:
                    return entry.Text.AutoSize ? entry.Applied.ToString("0.##") : "fixed";
                case ImplicitTextSizeGroup.EntryState.MinimumAboveGroup:
                    return entry.Applied.ToString("0.##") + " min";
                case ImplicitTextSizeGroup.EntryState.Excluded:
                    return "excluded";
                case ImplicitTextSizeGroup.EntryState.Inactive:
                    return "inactive";
                case ImplicitTextSizeGroup.EntryState.Empty:
                    return "empty";
                default:
                    return "no auto size";
            }
        }

        // The text on one line, cut to fit a row.
        private static string Preview(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "(empty)";

            var line = content.Replace("\r", " ").Replace("\n", " ").Trim();
            return line.Length > MaxPreviewLength ? line.Substring(0, MaxPreviewLength) + "\u2026" : line;
        }

        // Where the text sits below the group, e.g. "Buttons/Settings/Label".
        private static string PathFrom(Transform root, Transform target)
        {
            var path = target.name;
            for (var parent = target.parent; parent != null && parent != root; parent = parent.parent)
                path = parent.name + "/" + path;

            return path;
        }

        // Columns: the toggle, the text content taking what is left, then the object, and two size columns.
        private static Rect Column(Rect row, int index)
        {
            var fixedWidth = ObjectWidth + SizeWidth + OutcomeWidth + 12f;
            var x = row.x + ToggleWidth + 2f;
            var contentWidth = Mathf.Max(40f, row.width - ToggleWidth - 2f - fixedWidth);
            switch (index)
            {
                case 1:
                    return new Rect(x, row.y, contentWidth - 4f, row.height);
                case 2:
                    return new Rect(x + contentWidth, row.y, ObjectWidth, row.height);
                case 3:
                    return new Rect(x + contentWidth + ObjectWidth + 4f, row.y, SizeWidth, row.height);
                default:
                    return new Rect(x + contentWidth + ObjectWidth + SizeWidth + 8f, row.y, OutcomeWidth, row.height);
            }
        }
    }
}
