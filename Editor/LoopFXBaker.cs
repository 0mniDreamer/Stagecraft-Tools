// LoopFXBaker.cs
// Bakes simple time-driven motion (spin / bob / pulse) into looping clips that
// ship. Transform-only, so no material instancing and no runtime script needed.
// For scrolling/emission use a _Time-driven shader instead — that also ships.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class LoopFXBaker : EditorWindow
    {
        private enum Fx { SpinY, BobY, ScalePulse }
        private Fx    fx = Fx.SpinY;
        private Transform target;
        private float period = 4f;
        private float amount = 1f;      // spin: turns; bob: units; pulse: +/- fraction
        private bool  addAnimator = true;

        [MenuItem("SynthRiders/Stagecraft/2. Loop FX Baker")]
        public static void ShowWindow() => GetWindow<LoopFXBaker>("FX Baker").minSize = new Vector2(340, 230);

        private void OnGUI()
        {
            target = (Transform)EditorGUILayout.ObjectField("Target", target, typeof(Transform), true);
            fx     = (Fx)EditorGUILayout.EnumPopup("Effect", fx);
            period = Mathf.Max(0.05f, EditorGUILayout.FloatField("Period (s)", period));
            amount = EditorGUILayout.FloatField(AmountLabel(), amount);
            addAnimator = EditorGUILayout.Toggle("Add Animator + Controller", addAnimator);

            using (new EditorGUI.DisabledScope(target == null))
                if (GUILayout.Button("Bake Looping Clip"))
                    Bake();
        }

        private string AmountLabel() => fx switch
        {
            Fx.SpinY      => "Turns per loop",
            Fx.BobY       => "Bob height (units)",
            Fx.ScalePulse => "Pulse amount (0..1)",
            _             => "Amount"
        };

        private void Bake()
        {
            var clip = new AnimationClip { frameRate = 60f };

            switch (fx)
            {
                case Fx.SpinY:
                {
                    var c = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(period, 360f * amount));
                    Linear(c);
                    clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", c);
                    break;
                }
                case Fx.BobY:
                {
                    float y = target.localPosition.y;
                    var c = new AnimationCurve(
                        new Keyframe(0f, y),
                        new Keyframe(period * 0.5f, y + amount),
                        new Keyframe(period, y));
                    Smooth(c);
                    clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", c);
                    break;
                }
                case Fx.ScalePulse:
                {
                    Vector3 s = target.localScale;
                    AddScaleAxis(clip, "x", s.x, amount);
                    AddScaleAxis(clip, "y", s.y, amount);
                    AddScaleAxis(clip, "z", s.z, amount);
                    break;
                }
            }

            StagecraftUtil.SetLooping(clip, true);
            string folder = StagecraftUtil.GeneratedFolder();
            string path = StagecraftUtil.UniqueAssetPath(folder, $"{fx}_{target.name}.anim");
            AssetDatabase.CreateAsset(clip, path);

            if (addAnimator)
            {
                string ctrlPath = StagecraftUtil.UniqueAssetPath(folder, $"{fx}_{target.name}.controller");
                var controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, clip);
                var animator = target.GetComponent<Animator>() ?? Undo.AddComponent<Animator>(target.gameObject);
                animator.runtimeAnimatorController = controller;
            }

            AssetDatabase.SaveAssets();
            StagecraftUtil.MarkSceneDirty();
            EditorUtility.DisplayDialog("FX Baker", $"Baked {fx} at:\n{path}", "OK");
        }

        private void AddScaleAxis(AnimationClip clip, string axis, float baseVal, float amt)
        {
            var c = new AnimationCurve(
                new Keyframe(0f, baseVal),
                new Keyframe(period * 0.5f, baseVal * (1f + amt)),
                new Keyframe(period, baseVal));
            Smooth(c);
            clip.SetCurve("", typeof(Transform), $"m_LocalScale.{axis}", c);
        }

        private static void Linear(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
        }
        private static void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        }
    }
}
#endif
