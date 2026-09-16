using System.Collections.Generic;
using UnityEngine.UI;

namespace ImplicitUI.Editor
{
    // Which other objects drive a grayscale, and which grayscales an object drives. Both rules - the nearest Selectable
    // above, the nearest Implicit Grayscale above - reach across GameObjects, so without this the inspector would give
    // no hint that a child changes because of its parent. Built on the component's own lookups, so it cannot drift from
    // what happens at runtime.
    internal static class GrayscaleLinks
    {
        // The Selectable on another GameObject that this component follows; null when it follows none, or its own.
        internal static Selectable ControllingSelectable(ImplicitGrayscale grayscale)
        {
            if (!grayscale.GrayWhenDisabled)
                return null;

            var selectable = grayscale.FindSelectable();
            return selectable != null && selectable.gameObject != grayscale.gameObject ? selectable : null;
        }

        // The Implicit Grayscale above that this component never draws less gray than; null when there is none.
        internal static ImplicitGrayscale ControllingAncestor(ImplicitGrayscale grayscale)
        {
            return grayscale.FindAncestor();
        }

        // Components below this GameObject that turn gray when the Selectable on this GameObject is disabled.
        internal static void ChildrenFollowingSelectable(ImplicitGrayscale grayscale, List<ImplicitGrayscale> result)
        {
            result.Clear();
            var own = grayscale.GetComponent<Selectable>();
            if (own == null)
                return;

            foreach (var child in grayscale.GetComponentsInChildren<ImplicitGrayscale>())
            {
                if (child != grayscale && child.GrayWhenDisabled && child.FindSelectable() == own)
                    result.Add(child);
            }
        }

        // Components below this GameObject whose nearest Implicit Grayscale above is this one.
        internal static void ChildrenKeptAsGray(ImplicitGrayscale grayscale, List<ImplicitGrayscale> result)
        {
            result.Clear();
            foreach (var child in grayscale.GetComponentsInChildren<ImplicitGrayscale>())
            {
                if (child != grayscale && child.FindAncestor() == grayscale)
                    result.Add(child);
            }
        }

        // Both lists merged, one entry per child: a child under a button whose image has the component follows both
        // rules, and listing it twice only adds noise.
        internal static void AffectedChildren(ImplicitGrayscale grayscale, List<AffectedChild> result)
        {
            result.Clear();
            var children = new List<ImplicitGrayscale>();

            ChildrenFollowingSelectable(grayscale, children);
            foreach (var child in children)
                result.Add(new AffectedChild(child, true, false));

            ChildrenKeptAsGray(grayscale, children);
            foreach (var child in children)
            {
                var index = result.FindIndex(entry => entry.Grayscale == child);
                if (index >= 0)
                    result[index] = new AffectedChild(child, true, true);
                else
                    result.Add(new AffectedChild(child, false, true));
            }
        }

        internal readonly struct AffectedChild
        {
            internal readonly ImplicitGrayscale Grayscale;
            internal readonly bool FollowsSelectable;
            internal readonly bool KeptAsGray;

            internal AffectedChild(ImplicitGrayscale grayscale, bool followsSelectable, bool keptAsGray)
            {
                Grayscale = grayscale;
                FollowsSelectable = followsSelectable;
                KeptAsGray = keptAsGray;
            }
        }
    }
}
