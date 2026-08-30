// ExportPreflight.cs
// Scans the active scene for the things that quietly break a Synth Riders stage
// export: custom scripts that won't ship, an unwired CustomStageInfo, movers left
// static-batched, realtime lights, missing scripts, and BiRP materials.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class ExportPreflight : EditorWindow
    {
        private enum Sev { Error, Warn, Info }
        private struct Issue { public Sev sev; public string msg; public Object ctx; }

        private readonly List<Issue> issues = new List<Issue>();
        private Vector2 scroll;

        [MenuItem("SynthRiders/Stagecraft/4. Export Preflight")]
        public static void ShowWindow() => GetWindow<ExportPreflight>("Preflight").minSize = new Vector2(460, 400);

        private void OnGUI()
        {
            if (GUILayout.Button("Run Preflight")) Run();
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var i in issues)
            {
                var t = i.sev == Sev.Error ? MessageType.Error : i.sev == Sev.Warn ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(i.msg, t);
                if (i.ctx != null && GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(38)))
                    Selection.activeObject = i.ctx;
                EditorGUILayout.EndHorizontal();
            }
            if (issues.Count == 0) EditorGUILayout.LabelField("Run to scan the open scene.");
            EditorGUILayout.EndScrollView();
        }

        private void Add(Sev s, string m, Object ctx = null) => issues.Add(new Issue { sev = s, msg = m, ctx = ctx });

        private void Run()
        {
            issues.Clear();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();

            // 1) CustomStageInfo present & wired
            var infos = all.Select(g => g.GetComponent("CustomStageInfo")).Where(c => c != null).ToArray();
            if (infos.Length == 0)
                Add(Sev.Error, "No CustomStageInfo in the scene — the exporter needs it to know what ships.");
            foreach (var info in infos)
                CheckStageInfo(info);

            // 2) Custom MonoBehaviours that won't resolve in the bundle
            foreach (var go in all)
            {
                var comps = go.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) { Add(Sev.Error, $"Missing script on '{Path(go)}'.", go); continue; }
                    if (c is Transform || c is MeshFilter || c is Renderer || c is Collider ||
                        c is ParticleSystem || c is Animator || c is Animation) continue;
                    var tn = c.GetType().Name;
                    var ns = c.GetType().Namespace ?? "";
                    bool unity = ns.StartsWith("UnityEngine") || ns.StartsWith("UnityEditor");
                    bool known = StagecraftUtil.KnownRuntimeComponents.Contains(tn);
                    if (!unity && !known)
                        Add(Sev.Warn, $"'{tn}' on '{Path(go)}' is a custom script — it is stripped on export and will be INERT in-game. Bake its behaviour to animation/shader.", go);
                }
            }

            // 3) Static movers (children of an animated/tile root marked static)
            foreach (var go in all)
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                bool anyStatic = flags != 0;
                if (anyStatic && InsideAnimatedRoot(go))
                    Add(Sev.Warn, $"'{Path(go)}' is marked Static but sits under a moving root — it won't move. Clear its Static flags.", go);
            }

            // 4) Realtime lights
            foreach (var l in all.Select(g => g.GetComponent<Light>()).Where(l => l != null))
                if (l.lightmapBakeType != LightmapBakeType.Baked)
                    Add(Sev.Warn, $"Realtime/mixed Light '{Path(l.gameObject)}' — costly on Quest; prefer emissive materials.", l.gameObject);

            // 5) BiRP / error materials
            var mats = new HashSet<Material>();
            foreach (var r in all.Select(g => g.GetComponent<Renderer>()).Where(r => r != null))
                foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            foreach (var m in mats)
            {
                if (m.shader == null || m.shader.name.Contains("InternalError"))
                    Add(Sev.Error, $"Material '{m.name}' has a missing/error shader.", m);
                else if (m.shader.name.StartsWith("Standard") || m.shader.name.StartsWith("Legacy") || m.shader.name.StartsWith("Mobile/"))
                    Add(Sev.Warn, $"Material '{m.name}' uses a Built-in RP shader ('{m.shader.name}') — may render magenta under URP.", m);
            }

            if (!issues.Any(i => i.sev == Sev.Error || i.sev == Sev.Warn))
                Add(Sev.Info, "No blocking issues found. Still worth an in-headset check.");
        }

        private void CheckStageInfo(Component info)
        {
            var so = new SerializedObject(info);
            void Req(string prop, string label)
            {
                var p = so.FindProperty(prop);
                if (p != null && p.objectReferenceValue == null)
                    Add(Sev.Error, $"CustomStageInfo.{label} is not assigned.", info);
            }
            var isNorm = so.FindProperty("isNormStage");
            if (isNorm != null && isNorm.boolValue)
            {
                Req("normTile01", "normTile01");
                Req("normTile02", "normTile02");
                Req("normTile03", "normTile03");
                Req("normplatform", "normplatform");
                Req("normSkybox", "normSkybox");
            }
        }

        private static bool InsideAnimatedRoot(GameObject go)
        {
            var t = go.transform.parent;
            while (t != null)
            {
                if (t.GetComponent<Animator>() != null || t.GetComponent("TileManager") != null) return true;
                t = t.parent;
            }
            return false;
        }

        private static string Path(GameObject go)
        {
            var t = go.transform; var s = go.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
#endif
