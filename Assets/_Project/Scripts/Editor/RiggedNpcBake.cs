using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoadSafety.AI;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > AI > Bake rigged people into the scene
    /// Saves the rigged body of every townsperson, cafe visitor, school kid and garden kid into
    /// Main.unity, switches the primitive capsules off in the scene file, and saves the materials
    /// as assets. Run it again after changing the people or the level.
    /// </summary>
    public static class RiggedNpcBake
    {
        private const string MatDir = "Assets/_Project/Materials/Generated/NPC";
        private const string StripesPath = "Assets/_Project/Textures/NpcStripes.asset";

        [MenuItem("RoadSafety/AI/Bake rigged people into the scene")]
        public static void Bake()
        {
            ScenePreview.HideMenu();
            if (Resources.Load<GameObject>(RiggedNpc.ModelPath) == null)
            {
                Debug.LogError("[RiggedNpcBake] Models/rs_boy_rigged not found in Resources.");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();

            var targets = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.GetComponent<Townsperson>() || t.GetComponent<Pedestrian>() || t.GetComponent<CafeVisitor>()
                        || t.name.StartsWith("PlayingKid_"))
                        targets.Add(t.gameObject);

            // fresh start: old bodies and old NPC materials are replaced
            foreach (GameObject go in targets)
            {
                Transform old = go.transform.Find(RiggedNpc.BodyName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            if (AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.DeleteAsset(MatDir);
            AssetDatabase.CreateFolder("Assets/_Project/Materials/Generated", "NPC");

            RiggedNpc.BakeSave = (m, id) =>
            {
                AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath($"{MatDir}/{id}.mat"));
                return m;
            };
            RiggedNpc.BakeStripes = () =>
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(StripesPath);
                if (tex != null) return tex;
                tex = MakeStripes();
                AssetDatabase.CreateAsset(tex, StripesPath);
                return tex;
            };

            int baked = 0, failed = 0;
            try
            {
                foreach (GameObject go in targets)
                {
                    var npc = go.AddComponent<RiggedNpc>();
                    bool ok = npc.BakeInto(RiggedNpc.HeightFor(go));
                    Object.DestroyImmediate(npc); // only the body stays in the scene
                    if (ok) baked++; else failed++;
                }
            }
            finally
            {
                RiggedNpc.BakeSave = null;
                RiggedNpc.BakeStripes = null;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[RiggedNpcBake] {baked} rigged people saved into {scene.name} ({failed} failed).");
        }

        private static Texture2D MakeStripes()
        {
            var tex = new Texture2D(8, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 8; x++)
                    tex.SetPixel(x, y, (y / 8) % 2 == 0 ? Color.white : new Color(0.82f, 0.82f, 0.8f));
            tex.Apply();
            return tex;
        }
    }
}
