// TileScrubber.cs
// Edit-mode preview of the treadmill: drag a slider to translate the tile root
// along -Z without entering Play mode. Reset restores the original position.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class TileScrubber : EditorWindow
    {
        private Transform tileRoot;
        private float tileLength = 2000f;
        private float t = 0f;
        private float baseZ = 0f;
        private bool  captured = false;

        [MenuItem("SynthRiders/Stagecraft/3. Tile Scrubber")]
        public static void ShowWindow() => GetWindow<TileScrubber>("Tile Scrubber").minSize = new Vector2(320, 170);

        private void OnGUI()
        {
            if (tileRoot == null) tileRoot = StagecraftUtil.FindByName("Ground Tiles");
            var newRoot = (Transform)EditorGUILayout.ObjectField("Tile Root", tileRoot, typeof(Transform), true);
            if (newRoot != tileRoot) { RestoreIfCaptured(); tileRoot = newRoot; captured = false; }

            tileLength = EditorGUILayout.FloatField("Tile Length", tileLength);

            EditorGUI.BeginChangeCheck();
            t = EditorGUILayout.Slider("Scrub", t, 0f, 1f);
            if (EditorGUI.EndChangeCheck() && tileRoot != null)
            {
                if (!captured) { baseZ = tileRoot.localPosition.z; captured = true; }
                var p = tileRoot.localPosition;
                p.z = baseZ - t * tileLength;
                Undo.RecordObject(tileRoot, "Scrub Tiles");
                tileRoot.localPosition = p;
            }

            using (new EditorGUI.DisabledScope(!captured))
                if (GUILayout.Button("Reset")) RestoreIfCaptured();

            EditorGUILayout.HelpBox(
                "Previews geometry motion only. Shader _Time animation still needs Play mode.",
                MessageType.None);
        }

        private void RestoreIfCaptured()
        {
            if (captured && tileRoot != null)
            {
                var p = tileRoot.localPosition; p.z = baseZ;
                Undo.RecordObject(tileRoot, "Reset Tiles");
                tileRoot.localPosition = p;
            }
            captured = false; t = 0f;
        }

        private void OnDisable() => RestoreIfCaptured();
    }
}
#endif
