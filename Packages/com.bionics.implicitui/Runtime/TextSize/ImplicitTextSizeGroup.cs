using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI
{
    /// <summary>
    /// Gives every auto-sized text below this GameObject the same font size: the smallest size any of them picks on its
    /// own, so "OK", "Settings" and "Back to main menu" line up instead of each filling its box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Works on legacy <see cref="Text"/> with Best Fit and on TextMeshPro with Auto Size, mixed freely. The group only
    /// lowers each text's auto size maximum, so auto size stays on and the texts still react to their own content;
    /// removing or disabling the group puts every maximum back. A group further down owns its own texts.
    /// </para>
    /// <para>
    /// Inactive texts and texts with auto size off are left out, and any text can be excluded in the inspector. With
    /// <see cref="IncludeFixedSizeTexts"/> on, texts with auto size off take part in finding the smallest size without
    /// being changed. A text never goes below its own auto size minimum; legacy Text takes whole sizes only, so a group
    /// with any legacy Text rounds down.
    /// </para>
    /// <para>
    /// Sizes are measured in the text's own units, so a group settles on the same size whatever the canvas scale, the
    /// Game view size or the device resolution.
    /// </para>
    /// <para>
    /// In the Editor the group previews the sizes, and puts the original maximums back whenever a scene or prefab is
    /// saved, Play Mode starts or scripts reload: a saved file keeps the maximums you authored, never the ones the
    /// group worked out from the texts around them.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/Implicit UI/Implicit Text Size Group")]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class ImplicitTextSizeGroup : MonoBehaviour
    {
        private static readonly HashSet<ImplicitTextSizeGroup> s_Enabled = new HashSet<ImplicitTextSizeGroup>();
        private static readonly List<Component> s_Components = new List<Component>();

        [SerializeField]
        [Tooltip("Texts with auto size off also count when finding the smallest size. They are never changed.")]
        private bool m_IncludeFixedSizeTexts;

        [SerializeField, HideInInspector]
        private List<Component> m_Excluded = new List<Component>();

        private readonly List<Entry> m_Entries = new List<Entry>();
        private readonly Dictionary<Component, SizedText> m_Wrappers = new Dictionary<Component, SizedText>();
        private readonly Dictionary<Component, float> m_BaseMax = new Dictionary<Component, float>();
        private readonly Dictionary<Component, float> m_WrittenMax = new Dictionary<Component, float>();
        private readonly List<Component> m_Stale = new List<Component>();
        private int m_Signature;
        private bool m_Dirty = true;

        /// <summary>
        /// Whether texts with auto size off take part in finding the smallest size. They are never changed.
        /// </summary>
        public bool IncludeFixedSizeTexts
        {
            get => m_IncludeFixedSizeTexts;
            set
            {
                if (m_IncludeFixedSizeTexts == value)
                    return;

                m_IncludeFixedSizeTexts = value;
                m_Dirty = true;
            }
        }

        /// <summary>
        /// The font size the group gives its texts, or 0 when no text takes part.
        /// </summary>
        public float GroupSize { get; private set; }

        internal static IEnumerable<ImplicitTextSizeGroup> Enabled => s_Enabled;

        // One row per text found below the group, including the ones left out, for the inspector.
        internal IReadOnlyList<Entry> Entries => m_Entries;

        /// <summary>
        /// Whether <paramref name="text"/> is left out of the group.
        /// </summary>
        /// <param name="text">A legacy Text or TextMeshPro text below this GameObject.</param>
        /// <returns>True when the text was excluded with <see cref="SetExcluded"/> or in the inspector.</returns>
        public bool IsExcluded(Component text)
        {
            return text != null && m_Excluded.Contains(text);
        }

        /// <summary>
        /// Leaves <paramref name="text"/> out of the group, or brings it back. An excluded text gets its own maximum
        /// back and does not count when finding the smallest size.
        /// </summary>
        /// <param name="text">A legacy Text or TextMeshPro text below this GameObject.</param>
        /// <param name="excluded">True to leave the text out.</param>
        public void SetExcluded(Component text, bool excluded)
        {
            if (text == null || IsExcluded(text) == excluded)
                return;

            if (excluded)
                m_Excluded.Add(text);
            else
                m_Excluded.Remove(text);

            m_Dirty = true;
            RefreshIfChanged();
        }

        // Recomputes only when something that affects the result moved: the texts found, their content, size, state,
        // or a maximum changed by someone else. Changing a maximum rebuilds the text, so a check that reacted to its own
        // writes would never settle.
        internal void RefreshIfChanged()
        {
            var signature = ComputeSignature();
            if (!m_Dirty && signature == m_Signature)
                return;

            Refresh();
            m_Signature = ComputeSignature();
            m_Dirty = false;
        }

        internal void Refresh()
        {
            m_Entries.Clear();
            CollectTexts(transform);

            // Everything the group limited before and no longer finds (moved out, destroyed) gets its maximum back.
            m_Stale.Clear();
            foreach (var written in m_WrittenMax.Keys)
                m_Stale.Add(written);

            var smallest = float.MaxValue;
            var wholeSizes = false;
            for (var i = 0; i < m_Entries.Count; i++)
            {
                var entry = m_Entries[i];
                var text = entry.Text;
                if (entry.State == EntryState.Excluded || entry.State == EntryState.Inactive ||
                    entry.State == EntryState.Empty || entry.State == EntryState.AutoSizeOff)
                    continue;

                if (!text.AutoSize)
                {
                    entry.Natural = text.FixedSize;
                }
                else
                {
                    var component = text.Component;
                    if (!m_WrittenMax.TryGetValue(component, out var written) || !Mathf.Approximately(text.Max, written))
                        m_BaseMax[component] = text.Max;

                    entry.Natural = text.MeasureNatural(m_BaseMax[component]);
                    m_Stale.Remove(component);
                }

                wholeSizes |= text.WholeSizes;
                smallest = Mathf.Min(smallest, entry.Natural);
                m_Entries[i] = entry;
            }

            GroupSize = smallest < float.MaxValue ? smallest : 0f;
            if (wholeSizes)
                GroupSize = Mathf.Floor(GroupSize);

            for (var i = 0; i < m_Entries.Count; i++)
            {
                var entry = m_Entries[i];
                if (entry.State != EntryState.Included || !entry.Text.AutoSize)
                    continue;

                var text = entry.Text;
                var cap = Mathf.Max(GroupSize, text.Min);
                if (!Mathf.Approximately(text.Max, cap))
                    text.Max = cap;

                m_WrittenMax[text.Component] = text.Max;
                entry.Applied = text.Max;
                if (cap > GroupSize)
                    entry.State = EntryState.MinimumAboveGroup;

                m_Entries[i] = entry;
            }

            foreach (var component in m_Stale)
                Restore(component);
        }

        // Puts back every maximum the group changed. Editor code calls this before saving and before reloads.
        internal void RestoreAll()
        {
            m_Stale.Clear();
            foreach (var written in m_WrittenMax.Keys)
                m_Stale.Add(written);

            foreach (var component in m_Stale)
                Restore(component);

            m_Dirty = true;
        }

        private void Restore(Component component)
        {
            if (component != null && m_WrittenMax.TryGetValue(component, out var written) &&
                m_BaseMax.TryGetValue(component, out var original))
            {
                var text = Wrap(component);
                if (text != null && Mathf.Approximately(text.Max, written))
                    text.Max = original;
            }

            m_WrittenMax.Remove(component);
            m_BaseMax.Remove(component);
        }

        // Every text below this GameObject, stopping at groups further down: they own their texts.
        private void CollectTexts(Transform parent)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var nested = child.GetComponent<ImplicitTextSizeGroup>();
                if (nested != null && nested.isActiveAndEnabled)
                    continue;

                child.GetComponents(s_Components);
                foreach (var component in s_Components)
                {
                    var text = Wrap(component);
                    if (text != null)
                        m_Entries.Add(new Entry(text, StateOf(text)));
                }

                CollectTexts(child);
            }
        }

        private EntryState StateOf(SizedText text)
        {
            var component = text.Component;
            if (m_Excluded.Contains(component))
                return EntryState.Excluded;
            if (!(component is Behaviour behaviour) || !behaviour.isActiveAndEnabled)
                return EntryState.Inactive;
            if (string.IsNullOrEmpty(text.Content))
                return EntryState.Empty;
            if (!text.AutoSize && !m_IncludeFixedSizeTexts)
                return EntryState.AutoSizeOff;
            return EntryState.Included;
        }

        private SizedText Wrap(Component component)
        {
            if (component == null)
                return null;

            if (!m_Wrappers.TryGetValue(component, out var text))
            {
                text = SizedText.From(component);
                if (text == null)
                    return null;

                m_Wrappers[component] = text;
            }

            return text;
        }

        private int ComputeSignature()
        {
            unchecked
            {
                var hash = m_IncludeFixedSizeTexts ? 17 : 23;
                hash = SignatureOf(transform, hash);
                return hash;
            }
        }

        private int SignatureOf(Transform parent, int hash)
        {
            unchecked
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    var nested = child.GetComponent<ImplicitTextSizeGroup>();
                    if (nested != null && nested.isActiveAndEnabled)
                    {
                        hash = hash * 31 + RuntimeHelpers.GetHashCode(nested);
                        continue;
                    }

                    child.GetComponents(s_Components);
                    foreach (var component in s_Components)
                    {
                        var text = Wrap(component);
                        if (text == null)
                            continue;

                        hash = hash * 31 + RuntimeHelpers.GetHashCode(component);
                        hash = hash * 31 + ((Behaviour)component).isActiveAndEnabled.GetHashCode();
                        hash = hash * 31 + (text.AutoSize ? 1 : 2);
                        hash = hash * 31 + (text.Content != null ? RuntimeHelpers.GetHashCode(text.Content) : 0);
                        hash = hash * 31 + text.RectTransform.rect.size.GetHashCode();
                        hash = hash * 31 + text.Min.GetHashCode();
                        hash = hash * 31 + (text.AutoSize ? 0 : text.FixedSize.GetHashCode());

                        // A maximum that is not the one the group wrote was changed by someone else.
                        if (!m_WrittenMax.TryGetValue(component, out var written) || !Mathf.Approximately(text.Max, written))
                            hash = hash * 31 + text.Max.GetHashCode();
                    }

                    hash = SignatureOf(child, hash);
                }

                return hash;
            }
        }

        private void OnEnable()
        {
            s_Enabled.Add(this);
            m_Dirty = true;
            RefreshIfChanged();
        }

        private void OnDisable()
        {
            s_Enabled.Remove(this);
            RestoreAll();
        }

        private void OnTransformChildrenChanged()
        {
            m_Dirty = true;
        }

        private void OnValidate()
        {
            m_Dirty = true;
        }

        // Nothing notifies a parent when a descendant's text or size changes, so the group compares a cheap signature
        // once a frame (in the Editor, whenever the scene updates) and only recomputes when it moved.
        private void LateUpdate()
        {
            RefreshIfChanged();
        }

        internal enum EntryState
        {
            Included,
            MinimumAboveGroup,
            Excluded,
            Inactive,
            Empty,
            AutoSizeOff,
        }

        internal struct Entry
        {
            internal readonly SizedText Text;
            internal EntryState State;
            internal float Natural;
            internal float Applied;

            internal Entry(SizedText text, EntryState state)
            {
                Text = text;
                State = state;
                Natural = 0f;
                Applied = 0f;
            }
        }
    }
}
