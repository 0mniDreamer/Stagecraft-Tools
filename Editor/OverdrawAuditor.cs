// OverdrawAuditor.cs
// Lists transparent/additive renderers by estimated screen coverage, so a beam
// or fog plane that quietly fills the view gets caught before the headset does.
// Coverage is a bounds-projection estimate — directional, not exact.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class OverdrawAuditor : EditorWindow
    {
        private struct Row { public Renderer r; public float coverage; public bool additive; }
        private readonly List<Row> rows = new List<Row>();
        private float budget = 2.5f; // sum of coverage fractions considered "heavy"
        private Vector2 scroll;

        [MenuItem("SynthRiders/Stagecraft/5. Overdraw Auditor")]
        public static void ShowWindow() => GetWindow<OverdrawAuditor>("Overdraw").minSize = new Vector2(460, 400);

        private void OnGUI()
        {
            budget = EditorGUILayout.FloatField("Warn budget (Σ coverage)", budget);
            if (GUILayout.Button("Audit (uses Scene/main camera)")) Audit();

            float total = rows.Sum(r => r.coverage);
            EditorGUILayout.HelpBox(
                $"Transparent renderers: {rows.Count}   Σ coverage ≈ {total:0.00} screens" +
                (total > budget ? "  ⚠ over budget" : ""),
                total > budget ? MessageType.Warning : MessageType.Info);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var row in rows.OrderByDescending(r => r.coverage))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{(row.additive ? "[ADD] " : "")}{row.r.name}", GUILayout.Width(240));
                EditorGUILayout.LabelField($"{row.coverage:0.000}", GUILayout.Width(60));
                if (GUILayout.Button("Select", GUILayout.Width(60))) Selection.activeObject = row.r.gameObject;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private void Audit()
        {
            rows.Clear();
            Camera cam = Camera.main;
            if (cam == null && SceneView.lastActiveSceneView != null) cam = SceneView.lastActiveSceneView.camera;
            if (cam == null) { ShowNotification(new GUIContent("No camera found")); return; }

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var mat = r.sharedMaterial;
                if (mat == null) continue;
                bool transparent = mat.renderQueue >= 2450 ||
                                   (mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f) ||
                                   mat.GetTag("RenderType", false) == "Transparent";
                if (!transparent) continue;

                bool additive = mat.HasProperty("_DstBlend") && Mathf.Approximately(mat.GetFloat("_DstBlend"), 1f);
                rows.Add(new Row { r = r, coverage = ScreenCoverage(r, cam), additive = additive });
            }
        }

        // Projects the world-space bounds corners and measures the screen AABB area
        // as a fraction of the viewport. Rough but good for ranking.
        private static float ScreenCoverage(Renderer r, Camera cam)
        {
            Bounds b = r.bounds;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool anyFront = false;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var sp = cam.WorldToViewportPoint(c);
                if (sp.z > 0) anyFront = true;
                min = Vector2.Min(min, sp); max = Vector2.Max(max, sp);
            }
            if (!anyFront) return 0f;
            float w = Mathf.Clamp01(max.x) - Mathf.Clamp01(min.x);
            float h = Mathf.Clamp01(max.y) - Mathf.Clamp01(min.y);
            return Mathf.Clamp01(w) * Mathf.Clamp01(h);
        }
    }
}
#endif
