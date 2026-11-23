using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BehaviorDesigner.Runtime;
using RoadSafety.AI;
using RoadSafety.AI.Pedestrians;
using RoadSafety.Player;
using RoadSafety.World;
using Object = UnityEngine.Object;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// RoadSafety > AI > Copy the attached trees to every character of the same type
    ///
    /// Every character carries its OWN Behavior Designer tree inside its Behavior Tree component
    /// (External Behavior: None). The tree is attached by hand on one character of each type in
    /// Tools > Behavior Designer (select the object, build / paste the tree); this menu then copies
    /// that attached tree onto every other character of the same type (64 townspeople, 12 cars...)
    /// and saves the scene.
    /// The model for a type is the selected character of that type, otherwise the first one that has
    /// its own tree. Types with no attached tree yet are left alone (a warning says so).
    /// </summary>
    public static class BTUseAssets
    {
        [MenuItem("RoadSafety/AI/Copy the attached trees to every character of the same type")]
        public static void CopyToSameType()
        {
            if (Application.isPlaying) { Debug.LogWarning("[BTCopy] Stop Play mode first."); return; }

            var scene = EditorSceneManager.GetActiveScene();
            var groups = new SortedDictionary<string, List<BehaviorTree>>();
            foreach (BehaviorTree bt in Object.FindObjectsByType<BehaviorTree>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
            {
                if (bt.gameObject.scene != scene) continue;
                string type = TypeOf(bt.gameObject);
                if (type == null) continue;
                if (!groups.TryGetValue(type, out var list)) groups[type] = list = new List<BehaviorTree>();
                list.Add(bt);
            }

            var report = new System.Text.StringBuilder();
            int copied = 0;
            foreach (var pair in groups)
            {
                List<BehaviorTree> members = pair.Value;
                BehaviorTree model =
                    members.FirstOrDefault(b => Selection.gameObjects.Contains(b.gameObject) && HasOwnTree(b)) ??
                    members.FirstOrDefault(HasOwnTree);
                if (model == null)
                {
                    Debug.LogWarning($"[BTCopy] {pair.Key}: no character has its own tree yet: attach one by hand first ({members.Count} left as they are).");
                    report.Append($"\n  {pair.Key}: NOT DONE (no attached tree yet)");
                    continue;
                }

                var source = new SerializedObject(model);
                SerializedProperty tree = source.FindProperty("mBehaviorSource");
                int n = 0;
                foreach (BehaviorTree bt in members)
                {
                    var target = new SerializedObject(bt);
                    if (bt != model)
                    {
                        target.CopyFromSerializedProperty(tree);
                        n++;
                    }
                    target.FindProperty("externalBehavior").objectReferenceValue = null; // the tree lives on the object
                    target.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(bt);
                }
                copied += n;
                report.Append($"\n  {pair.Key}: model '{model.name}' -> copied to {n} more");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(scene.path); // reload so Behavior Designer shows every copied tree
            Debug.Log($"[BTCopy] {copied} characters got the tree attached to their type's model, scene saved:{report}");
        }

        /// <summary>The component holds its own tree (not an External Behavior file).</summary>
        private static bool HasOwnTree(BehaviorTree bt)
        {
            var so = new SerializedObject(bt);
            if (so.FindProperty("externalBehavior").objectReferenceValue != null) return false;
            SerializedProperty data = so.FindProperty("mBehaviorSource.mTaskData");
            if (data == null) return false;
            SerializedProperty types = data.FindPropertyRelative("types");
            SerializedProperty json = data.FindPropertyRelative("JSONSerialization");
            return (types != null && types.arraySize > 0) || (json != null && !string.IsNullOrEmpty(json.stringValue));
        }

        /// <summary>The character type, from the script that drives it.</summary>
        private static string TypeOf(GameObject go)
        {
            if (go.GetComponent<TrafficLight>() != null) return "Traffic light";
            if (go.GetComponent<NpcCar>() != null) return "Other car";
            if (go.GetComponent<Pedestrian>() != null) return "School kid";
            if (go.GetComponent<CafeVisitor>() != null) return "Café visitor";
            if (go.GetComponent<Townsperson>() != null) return "Townsperson / café customer";
            if (go.GetComponent<PigeonFlock>() != null) return "Pigeons";
            if (go.GetComponent<StreetCat>() != null) return "Cat";
            if (go.GetComponent<Gull>() != null) return "Seagull";
            if (go.GetComponent<PlayerActions>() != null) return "Player";

            var pedestrian = go.GetComponent<PedestrianAgent>();
            if (pedestrian != null) return "Pedestrian: " + pedestrian.kind;
            return null;
        }
    }
}
