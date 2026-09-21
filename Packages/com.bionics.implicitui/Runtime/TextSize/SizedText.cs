using UnityEngine;
using UnityEngine.UI;
#if IMPLICITUI_TMP
using TMPro;
#endif

namespace ImplicitUI
{
    // One text a size group can read and limit, whatever its type: legacy Text, or TextMeshPro when it is installed.
    // The group only ever writes the auto size maximum, so the text keeps sizing itself below it.
    internal abstract class SizedText
    {
        internal abstract Component Component { get; }

        internal abstract bool AutoSize { get; }

        // The auto size bounds as stored on the text.
        internal abstract float Max { get; set; }

        internal abstract float Min { get; }

        // The size of a text with auto size off.
        internal abstract float FixedSize { get; }

        internal abstract string Content { get; }

        // Legacy Text only takes whole font sizes.
        internal abstract bool WholeSizes { get; }

        internal RectTransform RectTransform => (RectTransform)Component.transform;

        // The size auto size picks with this maximum: the text's own choice before any group limits it.
        internal abstract float MeasureNatural(float max);

        internal static SizedText From(Component component)
        {
            if (component is Text text)
                return new LegacySizedText(text);
#if IMPLICITUI_TMP
            if (component is TMP_Text tmp)
                return new TmpSizedText(tmp);
#endif
            return null;
        }
    }

    internal sealed class LegacySizedText : SizedText
    {
        // Measuring with a generator of its own leaves the text's cached layout alone.
        private static TextGenerator s_Generator;

        private readonly Text m_Text;

        internal LegacySizedText(Text text)
        {
            m_Text = text;
        }

        internal override Component Component => m_Text;

        internal override bool AutoSize => m_Text.resizeTextForBestFit;

        internal override float Max
        {
            get => m_Text.resizeTextMaxSize;
            set => m_Text.resizeTextMaxSize = Mathf.FloorToInt(value);
        }

        internal override float Min => m_Text.resizeTextMinSize;

        internal override float FixedSize => m_Text.fontSize;

        internal override string Content => m_Text.text;

        internal override bool WholeSizes => true;

        internal override float MeasureNatural(float max)
        {
            if (m_Text.font == null)
                return max;

            var settings = m_Text.GetGenerationSettings(RectTransform.rect.size);
            settings.resizeTextMaxSize = Mathf.FloorToInt(max);
            if (s_Generator == null)
                s_Generator = new TextGenerator();

            s_Generator.Populate(m_Text.text, settings);

            // The generator works in canvas pixels; the text's fields are in its own units.
            var scale = settings.scaleFactor > 0f ? settings.scaleFactor : 1f;
            return s_Generator.fontSizeUsedForBestFit / scale;
        }
    }

#if IMPLICITUI_TMP
    internal sealed class TmpSizedText : SizedText
    {
        private readonly TMP_Text m_Text;

        internal TmpSizedText(TMP_Text text)
        {
            m_Text = text;
        }

        internal override Component Component => m_Text;

        internal override bool AutoSize => m_Text.enableAutoSizing;

        internal override float Max
        {
            get => m_Text.fontSizeMax;
            set => m_Text.fontSizeMax = value;
        }

        internal override float Min => m_Text.fontSizeMin;

        internal override float FixedSize => m_Text.fontSize;

        internal override string Content => m_Text.text;

        internal override bool WholeSizes => false;

        // TextMeshPro resolves auto size while building its mesh, and reports the result through fontSize.
        internal override float MeasureNatural(float max)
        {
            var previous = m_Text.fontSizeMax;
            m_Text.fontSizeMax = max;
            m_Text.ForceMeshUpdate();
            var natural = m_Text.fontSize;
            m_Text.fontSizeMax = previous;
            return natural;
        }
    }
#endif
}
