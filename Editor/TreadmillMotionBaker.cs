// TreadmillMotionBaker.cs
// Bakes the TileManager/DOTween preview treadmill into a looping AnimationClip
// (+ AnimatorController) so the motion SURVIVES EXPORT. The clip translates the
// tile root one tile-length along -Z and loops; the snap is invisible because
// the three tiles are identical (periodic with tileLength).
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class TreadmillMotionBaker : EditorWindow
    {
        private Transform tileRoot;      // "Ground Tiles"
        private float tileLength = 2000f;
        private float speed      = 30f;  // world units / sec (TileManager m_speed * 1.5)
        private bool  addAnimator = true;
        private bool  disableTileManager = true;

        [MenuItem("SynthRiders/Stagecraft/1. Treadmill Motion Baker")]
        public static void ShowWindow() => GetWindow<TreadmillMotionBaker>("Motion Baker").minSize = new Vector2(340, 240);

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Bakes the tile treadmill into a looping clip that ships. Assign the tile root " +
                "(the object with TileManager), confirm the tile length and speed, and Bake.",
                MessageType.Info);

            if (tileRoot == null) tileRoot = StagecraftUtil.FindByName("Ground Tiles");
            tileRoot   = (Transform)EditorGUILayout.ObjectField("Tile Root", tileRoot, typeof(Transform), true);
            tileLength = EditorGUILayout.FloatField("Tile Length", tileLength);
            speed      = EditorGUILayout.FloatField("Speed (units/sec)", speed);
            addAnimator = EditorGUILayout.Toggle("Add Animator + Controller", addAnimator);
            disableTileManager = EditorGUILayout.Toggle("Disable TileManager after bake", disableTileManager);

            float period = speed > 0f ? tileLength / speed : 0f;
            EditorGUILayout.LabelField("Loop period", period > 0f ? $"{period:0.###}s" : "—");

            using (new EditorGUI.DisabledScope(tileRoot == null || speed <= 0f || tileLength <= 0f))
                if (GUILayout.Button("Bake Looping Clip"))
                    Bake();
        }

        private void Bake()
        {
            float period = tileLength / speed;
            float startZ = tileRoot.localPosition.z;

            var clip = new AnimationClip { frameRate = 60f };
            var curve = new AnimationCurve(
                new Keyframe(0f, startZ),
                new Keyframe(period, startZ - tileLength));
            // linear tangents for constant velocity
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            clip.SetCurve("", typeof(Transform), "m_LocalPosition.z", curve);
            StagecraftUtil.SetLooping(clip, true);

            string folder = StagecraftUtil.GeneratedFolder();
            string clipPath = StagecraftUtil.UniqueAssetPath(folder, "TreadmillLoop.anim");
            AssetDatabase.CreateAsset(clip, clipPath);

            if (addAnimator)
            {
                string ctrlPath = StagecraftUtil.UniqueAssetPath(folder, "TreadmillController.controller");
                var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
                var animator = tileRoot.GetComponent<Animator>();
                if (animator == null) animator = Undo.AddComponent<Animator>(tileRoot.gameObject);
                animator.runtimeAnimatorController = controller;
            }

            if (disableTileManager)
            {
                var tm = tileRoot.GetComponent("TileManager") as Behaviour;
                if (tm != null) { Undo.RecordObject(tm, "Disable TileManager"); tm.enabled = false; }
            }

            AssetDatabase.SaveAssets();
            StagecraftUtil.MarkSceneDirty();
            EditorUtility.DisplayDialog("Motion Baker",
                $"Baked looping clip ({period:0.###}s) at:\n{clipPath}\n\n" +
                "The tiles must be identical for the loop to be seamless.", "OK");
        }
    }
}
#endif
