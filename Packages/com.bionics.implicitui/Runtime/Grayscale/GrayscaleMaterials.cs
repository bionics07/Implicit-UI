using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ImplicitUI
{
    // Shared grayscale materials, one per source material and tint color. The source is the material the Graphic
    // would render with - the default UI material, or the stencil copy a Mask made of it - so stencil settings and
    // keywords carry over and only the shader changes. Gray amounts live in the vertices, so images that differ only
    // in amount keep sharing one material and batch together.
    internal static class GrayscaleMaterials
    {
        private static readonly int s_GrayTint = Shader.PropertyToID("_GrayTint");

        private static readonly Dictionary<Key, Material> s_Materials = new Dictionary<Key, Material>();
        private static readonly List<Key> s_Destroyed = new List<Key>();

        internal static int Count => s_Materials.Count;

        internal static Material Get(Material source, Shader shader, Color tint)
        {
            var key = new Key(source, tint);
            if (s_Materials.TryGetValue(key, out var material))
            {
                // Unity null: the cached copy was destroyed from outside.
                if (material != null)
                    return material;

                s_Materials.Remove(key);
            }

            material = new Material(source)
            {
                shader = shader,
                name = source.name + " (Implicit Grayscale)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetColor(s_GrayTint, tint);
            s_Materials[key] = material;
            return material;
        }

        // Drops entries whose source material is gone. Materials that are still in use stay untouched, so graphics
        // keep rendering across Play Mode sessions that do not reload the domain.
        internal static void RemoveDestroyed()
        {
            s_Destroyed.Clear();
            foreach (var pair in s_Materials)
            {
                if (pair.Key.Source == null || pair.Value == null)
                    s_Destroyed.Add(pair.Key);
            }

            foreach (var key in s_Destroyed)
            {
                Destroy(s_Materials[key]);
                s_Materials.Remove(key);
            }

            s_Destroyed.Clear();
        }

        private static void Destroy(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
        }

        // Keyed by the managed reference itself rather than an instance id: Object.GetInstanceID is an error from
        // Unity 6.6 on while its replacement does not exist in 2021.3, and a reference cannot be reused by a later
        // material the way an id could.
        private readonly struct Key : IEquatable<Key>
        {
            public readonly Material Source;
            private readonly Color32 m_Tint;

            public Key(Material source, Color32 tint)
            {
                Source = source;
                m_Tint = tint;
            }

            public bool Equals(Key other)
            {
                return ReferenceEquals(Source, other.Source) && m_Tint.r == other.m_Tint.r && m_Tint.g == other.m_Tint.g
                       && m_Tint.b == other.m_Tint.b && m_Tint.a == other.m_Tint.a;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (RuntimeHelpers.GetHashCode(Source) * 397)
                       ^ (m_Tint.r | (m_Tint.g << 8) | (m_Tint.b << 16) | (m_Tint.a << 24));
            }
        }
    }
}
