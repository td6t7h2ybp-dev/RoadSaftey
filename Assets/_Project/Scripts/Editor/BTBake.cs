using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RoadSafety.AI;
using Object = UnityEngine.Object;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > AI > Attach Behavior Designer trees
    /// Gives every agent of the open scene (other cars, school kid, townspeople, player car, traffic
    /// light) a Behavior Designer "Behavior Tree" component holding its tree, and saves the scene.
    /// Select any of them and open Tools > Behavior Designer > Editor to see (and in Play mode
    /// watch) its tree. Run it again after changing a tree in code.
    /// </summary>
    public static class BTBake
    {
        [MenuItem("RoadSafety/AI/Attach Behavior Designer trees")]
        public static void Bake()
        {
            ScenePreview.HideMenu(); // only the real scene is saved
            MethodInfo save = FindSave();
            if (save == null)
            {
                Debug.LogError("[BTBake] Behavior Designer's serializer was not found (BinarySerialization.Save / JSONSerialization.Save).");
                return;
            }

            int count = 0;
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(mb is IBTAgent agent) || mb.gameObject.scene != EditorSceneManager.GetActiveScene())
                    continue;
                Node tree = agent.DescribeTree();
                if (tree == null) continue;

                var bt = mb.GetComponent<BehaviorDesigner.Runtime.BehaviorTree>();
                if (bt == null) bt = Undo.AddComponent<BehaviorDesigner.Runtime.BehaviorTree>(mb.gameObject);
                else Undo.RecordObject(bt, "Behavior Designer tree");
                BDBridge.Fill(bt, tree, tree.Name);
                save.Invoke(null, new object[] { bt.GetBehaviorSource() });
                EditorUtility.SetDirty(bt);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bt);
                count++;
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            ScenePreview.RefreshMenu();
            Debug.Log($"[BTBake] {count} Behavior Designer trees attached and saved in {scene.name}.");
        }

        private static MethodInfo FindSave()
        {
            foreach (string typeName in new[] { "BehaviorDesigner.Editor.BinarySerialization", "BehaviorDesigner.Editor.JSONSerialization" })
            {
                Type t = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(typeName, false))
                    .FirstOrDefault(x => x != null);
                if (t == null) continue;
                MethodInfo m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(x => x.Name == "Save" && x.GetParameters().Length == 1
                                         && x.GetParameters()[0].ParameterType == typeof(BehaviorDesigner.Runtime.BehaviorSource));
                if (m != null) return m;
            }
            return null;
        }
    }
}
