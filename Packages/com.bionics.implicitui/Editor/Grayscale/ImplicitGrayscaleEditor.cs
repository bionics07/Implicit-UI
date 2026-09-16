using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplicitUI.Editor
{
    // Default fields, plus what the fields alone do not show: the cases where the component silently does nothing, and
    // the objects that change this image, or that this one changes, from another GameObject.
    [CustomEditor(typeof(ImplicitGrayscale))]
    [CanEditMultipleObjects]
    internal sealed class ImplicitGrayscaleEditor : UnityEditor.Editor
    {
        private const int MaxListedNames = 5;

        private readonly List<GrayscaleLinks.AffectedChild> m_Children = new List<GrayscaleLinks.AffectedChild>();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (AnyTarget(grayscale => !ImplicitGrayscale.IsSupported(grayscale.GetComponent<Graphic>())))
            {
                EditorGUILayout.HelpBox("Implicit Grayscale works on Image and RawImage. On other graphics it does nothing.",
                    MessageType.Warning);
            }
            else if (AnyTarget(grayscale => grayscale.HasCustomMaterial))
            {
                EditorGUILayout.HelpBox(
                    "This graphic uses a custom material, so Implicit Grayscale leaves it alone: swapping the shader " +
                    "would lose what that material does.",
                    MessageType.Warning);
            }

            if (AnyTarget(grayscale => !grayscale.HasShaderReference))
            {
                EditorGUILayout.HelpBox(
                    "This component was added from code, so the grayscale shader is found by name. A build includes the " +
                    "shader only if a component added in the Editor, or Always Included Shaders, references it.",
                    MessageType.Info);
            }

            // Links are about one object's place in its hierarchy; for several at once they would only confuse.
            if (targets.Length == 1 && target is ImplicitGrayscale single)
                DrawLinks(single);
        }

        // One box per object that drives this image, and one box for everything this image drives. The button and the
        // parent grayscale are usually the same object, so the common case is a single short line.
        private void DrawLinks(ImplicitGrayscale grayscale)
        {
            var selectable = GrayscaleLinks.ControllingSelectable(grayscale);
            var ancestor = GrayscaleLinks.ControllingAncestor(grayscale);

            if (selectable != null && ancestor != null && selectable.gameObject == ancestor.gameObject)
            {
                LinkBox("Controlled by '" + selectable.name + "': gray while its " + Nice(selectable) +
                        " is not interactable, and never less gray than it.", new Object[] { selectable.gameObject });
            }
            else
            {
                if (selectable != null)
                {
                    LinkBox("Controlled by '" + selectable.name + "': gray while its " + Nice(selectable) +
                            " is not interactable.", new Object[] { selectable.gameObject });
                }

                if (ancestor != null)
                {
                    LinkBox("Controlled by '" + ancestor.name + "': never less gray than it.",
                        new Object[] { ancestor.gameObject });
                }
            }

            GrayscaleLinks.AffectedChildren(grayscale, m_Children);
            if (m_Children.Count == 0)
                return;

            var own = grayscale.GetComponent<Selectable>();
            var text = new StringBuilder(m_Children.Count == 1 ? "Also controls" : "Also controls " + m_Children.Count);
            var objects = new Object[m_Children.Count];
            for (var i = 0; i < m_Children.Count; i++)
            {
                var child = m_Children[i];
                objects[i] = child.Grayscale.gameObject;
                if (i >= MaxListedNames)
                    continue;

                text.Append("\n• '").Append(child.Grayscale.name).Append("': ");
                if (child.FollowsSelectable)
                    text.Append("gray while this ").Append(Nice(own)).Append(" is not interactable");
                if (child.FollowsSelectable && child.KeptAsGray)
                    text.Append(", and ");
                if (child.KeptAsGray)
                    text.Append("never less gray than this");
            }

            if (m_Children.Count > MaxListedNames)
                text.Append("\n• and ").Append(m_Children.Count - MaxListedNames).Append(" more");

            LinkBox(text.ToString(), objects);
        }

        private static void LinkBox(string message, Object[] objects)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("Select", GUILayout.Width(56f), GUILayout.ExpandHeight(true)))
            {
                Selection.objects = objects;
                EditorGUIUtility.PingObject(objects[0]);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static string Nice(Component component)
        {
            return ObjectNames.NicifyVariableName(component.GetType().Name);
        }

        private bool AnyTarget(Func<ImplicitGrayscale, bool> predicate)
        {
            foreach (var candidate in targets)
            {
                if (candidate is ImplicitGrayscale grayscale && predicate(grayscale))
                    return true;
            }

            return false;
        }
    }
}
