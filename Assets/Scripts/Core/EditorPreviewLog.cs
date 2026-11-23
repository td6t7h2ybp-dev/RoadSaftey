using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoadSafety.Core
{
    /// <summary>
    /// Lets the runtime look (cockpit, friend, street art, HUD style) be shown in the Editor without
    /// pressing Play, and taken away again without a trace: every change made to an object that
    /// belongs to the scene is recorded here with a way to undo it, and RoadSafety's editor preview
    /// undoes them all before the scene is saved and before Play starts.
    /// In Play mode nothing is recorded.
    /// </summary>
    public static class EditorPreviewLog
    {
        private static readonly List<Action> Undo = new List<Action>();

        /// <summary>True while the Editor preview is being built (not in Play mode).</summary>
        public static bool Recording => !Application.isPlaying;

        public static void Record(Action undo)
        {
            if (Recording && undo != null)
                Undo.Add(undo);
        }

        /// <summary>Changes a renderer's material, remembering the original for the preview.</summary>
        public static void SetMaterial(Renderer r, Material m)
        {
            if (r == null) return;
            if (Recording)
            {
                Material old = r.sharedMaterial;
                Record(() => { if (r != null) r.sharedMaterial = old; });
            }
            r.sharedMaterial = m;
        }

        public static void SetEnabled(Renderer r, bool enabled)
        {
            if (r == null || r.enabled == enabled) return;
            if (Recording)
            {
                bool old = r.enabled;
                Record(() => { if (r != null) r.enabled = old; });
            }
            r.enabled = enabled;
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go == null || go.activeSelf == active) return;
            if (Recording)
            {
                bool old = go.activeSelf;
                Record(() => { if (go != null) go.SetActive(old); });
            }
            go.SetActive(active);
        }

        /// <summary>Destroy that works both in Play mode and in the Editor.</summary>
        public static void Kill(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        public static void RevertAll()
        {
            for (int i = Undo.Count - 1; i >= 0; i--)
            {
                try { Undo[i](); }
                catch (Exception e) { Debug.LogWarning("[Preview] " + e.Message); }
            }
            Undo.Clear();
        }
    }
}
