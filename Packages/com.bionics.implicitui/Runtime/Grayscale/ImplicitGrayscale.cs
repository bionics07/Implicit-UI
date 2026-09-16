using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI
{
    /// <summary>
    /// Turns an <see cref="Image"/> or <see cref="RawImage"/> gray - on its own when the button it belongs to is
    /// disabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With <see cref="GrayWhenDisabled"/> on, the image is fully gray whenever the nearest <see cref="Selectable"/>
    /// on this GameObject or above it is not interactable, including through a <see cref="CanvasGroup"/>. No code,
    /// material or setup is needed.
    /// </para>
    /// <para>
    /// The gray amount travels in the mesh vertices, so images with different amounts share one material and still
    /// batch together. A component higher in the hierarchy never grays images without their own component, but when
    /// both have one, the image is at least as gray as its ancestor.
    /// </para>
    /// <para>
    /// Works on Image and RawImage using the default UI material. An image with a custom material is left alone,
    /// since swapping its shader would lose what that material does.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/Implicit UI/Implicit Grayscale")]
    [RequireComponent(typeof(Graphic))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ImplicitGrayscale : BaseMeshEffect, IMaterialModifier
    {
        internal const string ShaderName = "Bionics/ImplicitUI/Grayscale";

        private static Shader s_FoundShader;
        private static bool s_WarnedMissingShader;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How gray the image is when nothing else makes it gray, from 0 to 1.")]
        private float m_Intensity;

        [SerializeField]
        [Tooltip("Turn fully gray while the nearest Selectable on this object or above it is not interactable.")]
        private bool m_GrayWhenDisabled = true;

        [SerializeField]
        [Tooltip("Color multiplied over the gray, for sepia or cold looks. White keeps a neutral gray.")]
        private Color m_TintColor = Color.white;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How much of Tint Color is applied over the gray, from 0 to 1.")]
        private float m_TintIntensity;

        // Filled by the script's default reference when the component is added in the Editor, which makes the
        // shader part of every build that uses the component. Empty when added from code: the shader is then
        // looked up by name.
        [SerializeField, HideInInspector]
        private Shader m_Shader;

        private float m_AppliedIntensity = -1f;

        /// <summary>
        /// How gray the image is when nothing else makes it gray, from 0 (original colors) to 1 (fully gray).
        /// </summary>
        public float Intensity
        {
            get => m_Intensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_Intensity, value))
                    return;

                m_Intensity = value;
                MarkVerticesDirty();
            }
        }

        /// <summary>
        /// Whether the image turns fully gray while the nearest <see cref="Selectable"/> on this GameObject or above
        /// it is not interactable. On by default.
        /// </summary>
        public bool GrayWhenDisabled
        {
            get => m_GrayWhenDisabled;
            set
            {
                if (m_GrayWhenDisabled == value)
                    return;

                m_GrayWhenDisabled = value;
                MarkVerticesDirty();
            }
        }

        /// <summary>
        /// Color multiplied over the gray, for sepia or cold looks. Images with different tint colors use different
        /// materials, so they do not batch together.
        /// </summary>
        public Color TintColor
        {
            get => m_TintColor;
            set
            {
                if (m_TintColor == value)
                    return;

                m_TintColor = value;
                MarkMaterialDirty();
            }
        }

        /// <summary>
        /// How much of <see cref="TintColor"/> is applied over the gray, from 0 (neutral gray) to 1.
        /// </summary>
        public float TintIntensity
        {
            get => m_TintIntensity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_TintIntensity, value))
                    return;

                m_TintIntensity = value;
                MarkVerticesDirty();
            }
        }

        /// <summary>
        /// The gray amount actually drawn: the highest of <see cref="Intensity"/>, full gray while disabled when
        /// <see cref="GrayWhenDisabled"/> is on, and the effective intensity of the nearest active
        /// <see cref="ImplicitGrayscale"/> above this GameObject.
        /// </summary>
        public float EffectiveIntensity
        {
            get
            {
                var intensity = m_Intensity;

                if (m_GrayWhenDisabled)
                {
                    var selectable = FindSelectable();
                    if (selectable != null && !selectable.IsInteractable())
                        intensity = 1f;
                }

                var ancestor = FindAncestor();
                if (ancestor != null)
                    intensity = Mathf.Max(intensity, ancestor.EffectiveIntensity);

                return intensity;
            }
        }

        // The Selectable that GrayWhenDisabled follows: the nearest one on this GameObject or above it. Shared with the
        // inspector, so what it reports is exactly what the component does.
        internal Selectable FindSelectable()
        {
            return GetComponentInParent<Selectable>();
        }

        // The nearest Implicit Grayscale above this GameObject, when it is active; this one never draws less gray than it.
        internal ImplicitGrayscale FindAncestor()
        {
            var parent = transform.parent;
            if (parent == null)
                return null;

            var ancestor = parent.GetComponentInParent<ImplicitGrayscale>();
            return ancestor != null && ancestor.isActiveAndEnabled ? ancestor : null;
        }

        internal bool HasCustomMaterial => graphic != null && graphic.material != graphic.defaultMaterial;

        internal bool HasShaderReference => m_Shader != null;

        /// <summary>
        /// Writes the gray amounts into UV0.z and UV0.w of every vertex.
        /// </summary>
        /// <param name="vh">The mesh the graphic generated, after any mesh modifiers above this component.</param>
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!AppliesToGraphic())
            {
                m_AppliedIntensity = -1f;
                return;
            }

            var intensity = EffectiveIntensity;
            var vertex = new UIVertex();
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.uv0 = new Vector4(vertex.uv0.x, vertex.uv0.y, intensity, m_TintIntensity);
                vh.SetUIVertex(vertex, i);
            }

            m_AppliedIntensity = intensity;
        }

        /// <summary>
        /// Swaps the default UI material for the shared grayscale material of this tint color.
        /// </summary>
        /// <param name="baseMaterial">The material the graphic would render with, after modifiers above this one.</param>
        /// <returns>The grayscale material, or <paramref name="baseMaterial"/> when this component does not apply.</returns>
        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (baseMaterial == null || !AppliesToGraphic())
                return baseMaterial;

            var shader = ResolveShader();
            return shader == null ? baseMaterial : GrayscaleMaterials.Get(baseMaterial, shader, m_TintColor);
        }

        internal static bool IsSupported(Graphic graphic)
        {
            return graphic is Image || graphic is RawImage;
        }

        // Nothing notifies when a Selectable, a CanvasGroup or an ancestor changes, so the effective intensity is
        // compared once a frame and the mesh is rebuilt only when it moved.
        internal void RefreshIntensity()
        {
            if (AppliesToGraphic() && !Mathf.Approximately(EffectiveIntensity, m_AppliedIntensity))
                graphic.SetVerticesDirty();
        }

        // Static state survives Play Mode sessions when domain reload is off. The found shader is an asset and stays
        // valid; the warning flag and materials whose source is gone are reset.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStatics()
        {
            s_WarnedMissingShader = false;
            GrayscaleMaterials.RemoveDestroyed();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MarkMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            MarkMaterialDirty();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            MarkMaterialDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            MarkMaterialDirty();
        }
#endif

        private void LateUpdate()
        {
            RefreshIntensity();
        }

        private bool AppliesToGraphic()
        {
            return IsActive() && IsSupported(graphic) && !HasCustomMaterial;
        }

        private Shader ResolveShader()
        {
            if (m_Shader != null)
                return m_Shader;

            if (s_FoundShader == null)
                s_FoundShader = Shader.Find(ShaderName);

            if (s_FoundShader == null && !s_WarnedMissingShader)
            {
                s_WarnedMissingShader = true;
                Debug.LogWarning("[Implicit UI] The shader " + ShaderName + " is not included in this build, so " +
                                 "Implicit Grayscale draws images in their original colors. Add the component to an " +
                                 "object in the Editor, or add the shader to Always Included Shaders.", this);
            }

            return s_FoundShader;
        }

        private void MarkVerticesDirty()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        private void MarkMaterialDirty()
        {
            if (graphic != null)
                graphic.SetMaterialDirty();
        }
    }
}
