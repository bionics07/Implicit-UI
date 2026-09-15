using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Editor
{
    // Implicit Fill has no settings of its own: this inspector edits the Image's fill fields, which the Image
    // inspector hides unless the type is Filled. Going through the Image's SerializedObject keeps undo,
    // multi-object editing and prefab overrides working as they do on the Image itself.
    [CustomEditor(typeof(ImplicitFill))]
    [CanEditMultipleObjects]
    internal sealed class ImplicitFillEditor : UnityEditor.Editor
    {
        internal const string FillMethodProperty = "m_FillMethod";
        internal const string FillOriginProperty = "m_FillOrigin";
        internal const string FillAmountProperty = "m_FillAmount";
        internal const string FillClockwiseProperty = "m_FillClockwise";

        private static readonly GUIContent s_FillMethodLabel = new GUIContent("Fill Method",
            "How the fill grows: along an axis, or sweeping around a corner, an edge or the center.");
        private static readonly GUIContent s_FillOriginLabel = new GUIContent("Fill Origin",
            "Where the fill starts from.");
        private static readonly GUIContent s_FillAmountLabel = new GUIContent("Fill Amount",
            "How much of the Image is shown, from 0 to 1. The same value as Image.fillAmount.");
        private static readonly GUIContent s_ClockwiseLabel = new GUIContent("Clockwise",
            "Direction a radial fill sweeps in.");

        private static readonly GUIContent[] s_HorizontalOrigins = Labels("Left", "Right");
        private static readonly GUIContent[] s_VerticalOrigins = Labels("Bottom", "Top");
        private static readonly GUIContent[] s_Radial90Origins = Labels("Bottom Left", "Top Left", "Top Right", "Bottom Right");
        private static readonly GUIContent[] s_Radial180Origins = Labels("Bottom", "Left", "Top", "Right");
        private static readonly GUIContent[] s_Radial360Origins = Labels("Bottom", "Right", "Top", "Left");

        private Image[] m_Images;
        private SerializedObject m_ImageObject;
        private SerializedProperty m_FillMethod;
        private SerializedProperty m_FillOrigin;
        private SerializedProperty m_FillAmount;
        private SerializedProperty m_FillClockwise;

        private void OnEnable()
        {
            m_Images = new Image[targets.Length];
            for (var i = 0; i < targets.Length; i++)
                m_Images[i] = ((ImplicitFill)targets[i]).GetComponent<Image>();

            // RequireComponent guarantees an Image; guard anyway so a broken object cannot throw every repaint.
            if (System.Array.Exists(m_Images, image => image == null))
                return;

            m_ImageObject = new SerializedObject(m_Images);
            m_FillMethod = m_ImageObject.FindProperty(FillMethodProperty);
            m_FillOrigin = m_ImageObject.FindProperty(FillOriginProperty);
            m_FillAmount = m_ImageObject.FindProperty(FillAmountProperty);
            m_FillClockwise = m_ImageObject.FindProperty(FillClockwiseProperty);
        }

        public override void OnInspectorGUI()
        {
            if (m_ImageObject == null || m_FillMethod == null || m_FillOrigin == null || m_FillAmount == null
                || m_FillClockwise == null)
            {
                EditorGUILayout.HelpBox("Implicit Fill needs an Image on the same GameObject.", MessageType.Error);
                return;
            }

            m_ImageObject.Update();

            if (System.Array.Exists(m_Images, image => image.type == Image.Type.Filled))
            {
                EditorGUILayout.HelpBox(
                    "The Image type is Filled, so the Image fills itself and Implicit Fill does nothing. " +
                    "Set the Image type to Sliced, Tiled or Simple to fill it here.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_FillMethod, s_FillMethodLabel);
            if (EditorGUI.EndChangeCheck())
            {
                // Same rule as the Image.fillMethod setter: origins mean different sides for each method.
                m_FillOrigin.intValue = 0;
            }

            if (!m_FillMethod.hasMultipleDifferentValues)
            {
                var method = (Image.FillMethod)m_FillMethod.intValue;
                DrawOrigin(OriginsFor(method));

                if (method != Image.FillMethod.Horizontal && method != Image.FillMethod.Vertical)
                    EditorGUILayout.PropertyField(m_FillClockwise, s_ClockwiseLabel);
            }

            EditorGUILayout.Slider(m_FillAmount, 0f, 1f, s_FillAmountLabel);

            m_ImageObject.ApplyModifiedProperties();
        }

        private void DrawOrigin(GUIContent[] options)
        {
            EditorGUI.showMixedValue = m_FillOrigin.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var origin = EditorGUILayout.Popup(s_FillOriginLabel, Mathf.Clamp(m_FillOrigin.intValue, 0, options.Length - 1), options);
            if (EditorGUI.EndChangeCheck())
                m_FillOrigin.intValue = origin;
            EditorGUI.showMixedValue = false;
        }

        private static GUIContent[] OriginsFor(Image.FillMethod method)
        {
            switch (method)
            {
                case Image.FillMethod.Horizontal:
                    return s_HorizontalOrigins;
                case Image.FillMethod.Vertical:
                    return s_VerticalOrigins;
                case Image.FillMethod.Radial90:
                    return s_Radial90Origins;
                case Image.FillMethod.Radial180:
                    return s_Radial180Origins;
                default:
                    return s_Radial360Origins;
            }
        }

        private static GUIContent[] Labels(params string[] names)
        {
            var labels = new GUIContent[names.Length];
            for (var i = 0; i < names.Length; i++)
                labels[i] = new GUIContent(names[i]);
            return labels;
        }
    }
}
