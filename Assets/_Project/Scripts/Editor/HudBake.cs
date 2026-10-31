using System.IO;
using UnityEditor;
using UnityEngine;
using RoadSafety.UI;

namespace RoadSafety.EditorTools
{
    /// <summary>
    /// Writes the 3D HUD buttons (HudRestyle) into the HUD sprite files themselves
    /// (Assets/_Project/UI/Resources/HUD/btn_*.png). The HUD saved in Main.unity points at these
    /// files, so the new buttons show everywhere: in the Editor, while Play mode starts or stops,
    /// and in the game. Run it again after changing HudRestyle.
    /// </summary>
    public static class HudBake
    {
        private const string Folder = "Assets/_Project/UI/Resources/HUD/";

        [MenuItem("RoadSafety/UI/Bake 3D buttons into the HUD sprites")]
        public static void Bake()
        {
            int n = 0;
            foreach (var (file, sprite) in HudRestyle.BakeList())
            {
                if (sprite == null || sprite.texture == null) continue;
                File.WriteAllBytes(Folder + file + ".png", sprite.texture.EncodeToPNG());
                n++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[HudBake] {n} HUD buttons written to {Folder}");
        }
    }
}
