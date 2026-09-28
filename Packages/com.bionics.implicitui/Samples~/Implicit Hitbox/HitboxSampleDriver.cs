using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImplicitUI.Samples
{
    /// <summary>
    /// Counts the clicks each icon gets, so the one with Implicit Hitbox can be compared with the one
    /// without by clicking just outside them. It also sets up the EventSystem, using the input module the
    /// project has, so the scene works whichever input system is installed.
    /// </summary>
    public sealed class HitboxSampleDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("Icons to count clicks for, in the same order as the counters.")]
        private Button[] m_Icons;

        [SerializeField]
        private Text[] m_Counters;

        [SerializeField, Tooltip("Images showing each icon's touch area, in the same order as the icons.")]
        private RectTransform[] m_TouchAreas;

        private int[] m_Clicks;

        private void Awake()
        {
            EnsureEventSystem();

            m_Clicks = new int[m_Icons.Length];
            for (var i = 0; i < m_Icons.Length; i++)
            {
                var index = i;
                if (m_Icons[i] != null)
                    m_Icons[i].onClick.AddListener(() => Count(index));
            }

            UpdateCounters();
        }

        // The touch area is invisible by nature, so the demo draws it: the icon's Raycast Padding, which Implicit Hitbox
        // grows while the game runs, turned into a rectangle around the icon.
        private void LateUpdate()
        {
            for (var i = 0; i < m_TouchAreas.Length && i < m_Icons.Length; i++)
            {
                if (m_TouchAreas[i] == null || m_Icons[i] == null)
                    continue;

                var padding = m_Icons[i].image.raycastPadding;
                m_TouchAreas[i].offsetMin = new Vector2(padding.x, padding.y);
                m_TouchAreas[i].offsetMax = new Vector2(-padding.z, -padding.w);
            }
        }

        private void Count(int index)
        {
            m_Clicks[index]++;
            UpdateCounters();
        }

        private void UpdateCounters()
        {
            for (var i = 0; i < m_Counters.Length && i < m_Clicks.Length; i++)
            {
                if (m_Counters[i] != null)
                    m_Counters[i].text = m_Clicks[i] + (m_Clicks[i] == 1 ? " click" : " clicks");
            }
        }

        // A scene shipped as a sample cannot know which input system the project uses, and the wrong module
        // logs an error at startup, so the right one is added here.
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
                eventSystem.AddComponent(inputSystemModule);
            else
                eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
