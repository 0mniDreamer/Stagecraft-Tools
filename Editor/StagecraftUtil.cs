// StagecraftUtil.cs — shared helpers for the Stagecraft editor tools.
// Editor-only. Keep under an "Editor" folder.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomStage.Stagecraft
{
    public static class StagecraftUtil
    {
        // Components the Synth Riders runtime already has compiled in, so they
        // resolve from an exported bundle even though their .cs is stripped.
        public static readonly HashSet<string> KnownRuntimeComponents = new HashSet<string>
        {
            "StageEvents", "StageComboEvents", "StageScoreEvents", "StageTimeEvents",
            "SynthSpecialsFX", "CustomStageInfo"
        };

        // Where to drop generated assets: a "Generated" folder next to the scene.
        public static string GeneratedFolder()
        {
            Scene s = SceneManager.GetActiveScene();
            string baseDir = string.IsNullOrEmpty(s.path) ? "Assets" : Path.GetDirectoryName(s.path);
            string gen = (baseDir + "/Generated").Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(gen))
                AssetDatabase.CreateFolder(baseDir.Replace("\\", "/"), "Generated");
            return gen;
        }

        public static string UniqueAssetPath(string folder, string fileName)
        {
            return AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}");
        }

        // Mark a clip as looping.
        public static void SetLooping(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Default;
        }

        public static Transform FindByName(string name)
        {
            var go = GameObject.Find(name);
            return go ? go.transform : null;
        }

        public static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }
}
#endif
