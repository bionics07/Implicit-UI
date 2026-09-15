using UnityEngine;
using UnityEngine.UI;

namespace ImplicitUI.Tests
{
    // Counts layout passes on its RectTransform: a layout rebuild calls every enabled ILayoutController there.
    [ExecuteAlways]
    public sealed class LayoutRebuildCounter : MonoBehaviour, ILayoutSelfController
    {
        public int Rebuilds { get; private set; }

        public void ResetCount()
        {
            Rebuilds = 0;
        }

        public void SetLayoutHorizontal()
        {
            Rebuilds++;
        }

        public void SetLayoutVertical()
        {
        }
    }
}
