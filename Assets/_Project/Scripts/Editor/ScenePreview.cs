using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoadSafety.Core;
using RoadSafety.Player;
using RoadSafety.UI;
using RoadSafety.World;
using Object = UnityEngine.Object;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Shows the game's real look in the Editor without pressing Play: the car interior, the driver's
    /// hands, the friend, the golden-hour street, the sea view and the new HUD, in the Scene and Game
    /// views. Everything it adds is marked "don't save" and every change it makes to the scene is
    /// undone before the scene is saved, before Play starts and before scripts reload, so Main.unity
    /// never changes because of it. RoadSafety > Preview > Refresh rebuilds it by hand.
    /// </summary>
    [InitializeOnLoad]
    public static class ScenePreview
    {
        private static readonly List<GameObject> Created = new List<GameObject>();
        private static readonly List<Component> AddedComponents = new List<Component>();
        private static bool enabled = true;

        static ScenePreview()
        {
            EditorApplication.delayCall += Rebuild;
            EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Rebuild;
            EditorSceneManager.sceneSaving += (s, p) => Clear();
            EditorSceneManager.sceneSaved += s => EditorApplication.delayCall += Rebuild;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Clear();
                else if (state == PlayModeStateChange.EnteredEditMode) Rebuild(); // right away: the old saved scene is never drawn
            };
        }

        [MenuItem("RoadSafety/Preview/Refresh preview")]
        public static void RefreshMenu()
        {
            enabled = true;
            Rebuild();
        }

        [MenuItem("RoadSafety/Preview/Hide preview")]
        public static void HideMenu()
        {
            enabled = false;
            Clear();
        }

        private static void Rebuild()
        {
            if (!enabled || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            Clear();
            if (Object.FindAnyObjectByType<GameManager>() == null)
                return; // not the gameplay scene

            var before = new HashSet<GameObject>(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                CarInterior interior = CarInterior.Ensure();
                if (interior != null)
                {
                    interior.hideFlags = HideFlags.DontSave;
                    AddedComponents.Add(interior);
                    interior.EditorBuildPreview();
                }
                StreetLook.Apply();
                RoadSafety.AI.RiggedNpc.DressAll(); // townspeople and kids with the real rigged body (undone before save / Play)
                PreviewHud();
                PlaceCamera(interior);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (before.Contains(go)) continue;
                go.hideFlags |= HideFlags.DontSave;
                Created.Add(go);
            }
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void Clear()
        {
            EditorPreviewLog.RevertAll();
            foreach (Component c in AddedComponents)
                if (c != null) Object.DestroyImmediate(c);
            AddedComponents.Clear();
            // children first is not needed: destroying a root destroys its children
            foreach (GameObject go in Created)
                if (go != null) Object.DestroyImmediate(go);
            Created.Clear();
        }

        /// <summary>The scene's own HUD is hidden and a restyled copy is shown in its place.</summary>
        private static void PreviewHud()
        {
            HUDController sceneHud = Object.FindAnyObjectByType<HUDController>();
            Transform parent = sceneHud != null ? sceneHud.transform.parent : null;
            if (sceneHud != null)
                EditorPreviewLog.SetActive(sceneHud.gameObject, false);
            HUDController preview = HUDBuilder.Build();
            if (parent != null) preview.transform.SetParent(parent, false);
            preview.name = "HUD (preview)";
            preview.UseInCarFriend();
        }

        /// <summary>Puts the Game view camera at the driver's eyes, looking where the game starts.</summary>
        private static void PlaceCamera(CarInterior interior)
        {
            Camera cam = Camera.main;
            if (cam == null || interior == null || interior.Eye == null) return;
            Transform t = cam.transform;
            Vector3 pos = t.position; Quaternion rot = t.rotation; float fov = cam.fieldOfView, near = cam.nearClipPlane;
            EditorPreviewLog.Record(() =>
            {
                if (cam == null) return;
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = fov;
                cam.nearClipPlane = near;
            });
            interior.GetLook(out float yaw, out float pitch);
            float carYaw = interior.transform.eulerAngles.y;
            t.SetPositionAndRotation(interior.Eye.position, Quaternion.Euler(0f, carYaw + yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f));
            cam.fieldOfView = 80f;
            cam.nearClipPlane = 0.02f;
        }
    }
}
