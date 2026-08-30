// PeripheralMarkerPlacer.cs
//
// Editor-only tool. Lays marker objects along the three ground tiles, OUTSIDE
// the beam corridor, so they ride the tile motion and recycle with the tile
// (they parent under the TileWrap, which UpdateTile moves).
//
// Two modes, each in its own container so they can coexist:
//   * Grid     -> evenly spaced edge row on each side ("EdgeMarkers")
//   * Scatter  -> random off-track field ("ScatterMarkers")
//
// Both are laid out to stay seamless on the current C# treadmill AND after you
// bake the motion to a looping clip: the pattern is periodic with the tile
// (identical on every tile), so a one-tile-length loop snaps invisibly.
//
// IMPORTANT: keep this file inside a folder named "Editor" so it is excluded
// from the exported stage bundle. The GameObjects it instantiates are normal
// mesh objects and DO ship — only this tool is editor-only.

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CustomStage.EditorTools
{
    public class PeripheralMarkerPlacer : EditorWindow
    {
        // --- Tiles ---
        private Transform[] tiles = new Transform[3];

        // --- What to place ---
        private GameObject markerPrefab;

        // --- Shared layout ---
        private float tileLength    = 2000f; // m_tileSize from TileManager
        private float innerX        = 12f;   // beam half-width (10) + margin -> track keep-out
        private float yOffset       = 0f;    // height above the tile surface (tile-local)
        private float zCenterOffset = 0f;    // nudge if the StageTile pivot isn't centered

        // --- Grid mode ---
        private float spacing  = 100f;       // must divide tileLength for a seamless loop
        private bool  mirrorLR = true;

        // --- Scatter mode ---
        private float outerX       = 60f;    // outer edge of the scatter band (tile edge)
        private int   scatterCount = 30;     // markers per tile
        private float minSpacing   = 6f;     // reject overlaps closer than this (XZ)
        private int   randomSeed   = 12345;
        private bool  randomYaw    = true;
        private float scaleJitter  = 0.25f;  // 0..1 uniform scale variation
        private bool  loopSafe     = true;   // same field on all 3 tiles (export-safe)

        private const string GridContainer    = "EdgeMarkers";
        private const string ScatterContainer = "ScatterMarkers";

        private struct Placement { public Vector3 pos; public float yaw; public float scale; }

        [MenuItem("SynthRiders/Stagecraft/9. Peripheral Marker Placer")]
        public static void ShowWindow()
        {
            GetWindow<PeripheralMarkerPlacer>("Marker Placer").minSize = new Vector2(340, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tiles (TileWrap1 / 2 / 3)", EditorStyles.boldLabel);
            for (int i = 0; i < tiles.Length; i++)
                tiles[i] = (Transform)EditorGUILayout.ObjectField($"Tile {i + 1}", tiles[i], typeof(Transform), true);
            if (GUILayout.Button("Auto-find tiles by name"))
                AutoFindTiles();

            EditorGUILayout.Space();
            markerPrefab = (GameObject)EditorGUILayout.ObjectField("Marker Prefab", markerPrefab, typeof(GameObject), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Shared", EditorStyles.boldLabel);
            tileLength    = EditorGUILayout.FloatField("Tile Length (Z)", tileLength);
            innerX        = EditorGUILayout.FloatField("Inner X (track keep-out)", innerX);
            yOffset       = EditorGUILayout.FloatField("Y Offset", yOffset);
            zCenterOffset = EditorGUILayout.FloatField("Z Center Offset", zCenterOffset);

            // ---------- Grid ----------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid Edge Row", EditorStyles.boldLabel);
            spacing  = EditorGUILayout.FloatField("Spacing (Z)", spacing);
            mirrorLR = EditorGUILayout.Toggle("Mirror Left / Right", mirrorLR);

            float per = tileLength / Mathf.Max(spacing, 0.0001f);
            int gridN = Mathf.RoundToInt(per);
            if (spacing > 0f && Mathf.Abs(per - gridN) > 0.001f)
                EditorGUILayout.HelpBox(
                    $"Spacing doesn't divide the tile evenly ({per:0.###} per tile). The loop seam " +
                    $"will jump — pick a spacing that divides {tileLength:0} (e.g. 100, 125, 200, 250).",
                    MessageType.Warning);

            using (new EditorGUI.DisabledScope(!ReadyGrid()))
                if (GUILayout.Button("Place / Rebuild Grid"))
                    ApplyList(GridContainer, _ => BuildGrid());

            // ---------- Scatter ----------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scatter (off-track)", EditorStyles.boldLabel);
            outerX       = EditorGUILayout.FloatField("Outer X (band edge)", outerX);
            scatterCount = EditorGUILayout.IntField("Count per tile", scatterCount);
            minSpacing   = EditorGUILayout.FloatField("Min Spacing", minSpacing);
            randomSeed   = EditorGUILayout.IntField("Seed", randomSeed);
            randomYaw    = EditorGUILayout.Toggle("Random Yaw", randomYaw);
            scaleJitter  = EditorGUILayout.Slider("Scale Jitter", scaleJitter, 0f, 1f);
            loopSafe     = EditorGUILayout.Toggle("Loop-safe (same on all tiles)", loopSafe);

            if (!loopSafe)
                EditorGUILayout.HelpBox(
                    "Loop-safe is OFF: each tile gets a different field. Fine for the C# treadmill " +
                    "preview, but a baked looping clip will jump every tile-length.",
                    MessageType.Warning);
            if (outerX <= innerX)
                EditorGUILayout.HelpBox("Outer X must be greater than Inner X.", MessageType.Error);

            using (new EditorGUI.DisabledScope(!ReadyScatter()))
                if (GUILayout.Button("Scatter (off-track)"))
                    ApplyList(ScatterContainer, tileIndex =>
                        BuildScatter(loopSafe ? randomSeed : randomSeed + tileIndex));

            // ---------- Clear ----------
            EditorGUILayout.Space();
            if (GUILayout.Button("Clear All Markers"))
                ClearAll();
        }

        // ---- readiness ----
        private bool ReadyBase()
        {
            if (markerPrefab == null || tileLength <= 0f) return false;
            foreach (var t in tiles) if (t == null) return false;
            return true;
        }
        private bool ReadyGrid()    => ReadyBase() && spacing > 0f;
        private bool ReadyScatter() => ReadyBase() && outerX > innerX && scatterCount > 0;

        private void AutoFindTiles()
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                var go = GameObject.Find($"TileWrap{i + 1}");
                if (go != null) tiles[i] = go.transform;
            }
        }

        // ---- layout builders (tile-local positions) ----
        private List<Placement> BuildGrid()
        {
            var list = new List<Placement>();
            int n = Mathf.RoundToInt(tileLength / spacing);
            for (int k = 0; k < n; k++)
            {
                // cell-centered: nothing lands on a seam, and the cross-tile gap equals spacing
                float z = zCenterOffset + (k + 0.5f) * spacing - tileLength * 0.5f;
                list.Add(new Placement { pos = new Vector3(innerX, yOffset, z), yaw = 0f, scale = 1f });
                if (mirrorLR)
                    list.Add(new Placement { pos = new Vector3(-innerX, yOffset, z), yaw = 0f, scale = 1f });
            }
            return list;
        }

        private List<Placement> BuildScatter(int seed)
        {
            var rng = new System.Random(seed);
            var list = new List<Placement>();
            float zHalf = tileLength * 0.5f;
            int maxAttempts = Mathf.Max(scatterCount * 40, 200);

            for (int a = 0; a < maxAttempts && list.Count < scatterCount; a++)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float x = side * Mathf.Lerp(innerX, outerX, (float)rng.NextDouble());
                float z = zCenterOffset + Mathf.Lerp(-zHalf, zHalf, (float)rng.NextDouble());
                var pos = new Vector3(x, yOffset, z);

                bool ok = true;
                if (minSpacing > 0f)
                {
                    float min2 = minSpacing * minSpacing;
                    foreach (var e in list)
                    {
                        float dx = e.pos.x - pos.x, dz = e.pos.z - pos.z;
                        if (dx * dx + dz * dz < min2) { ok = false; break; }
                    }
                }
                if (!ok) continue;

                float yaw   = randomYaw ? (float)(rng.NextDouble() * 360.0) : 0f;
                float scale = Mathf.Max(0.05f, 1f + ((float)rng.NextDouble() * 2f - 1f) * scaleJitter);
                list.Add(new Placement { pos = pos, yaw = yaw, scale = scale });
            }
            return list;
        }

        // ---- apply ----
        private void ApplyList(string containerName, System.Func<int, List<Placement>> perTileList)
        {
            for (int ti = 0; ti < tiles.Length; ti++)
            {
                var tile = tiles[ti];
                var container = GetOrCreateContainer(tile, containerName);

                for (int c = container.childCount - 1; c >= 0; c--)
                    Undo.DestroyObjectImmediate(container.GetChild(c).gameObject);

                var list = perTileList(ti);
                for (int k = 0; k < list.Count; k++)
                {
                    var p = list[k];
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(markerPrefab, container);
                    inst.transform.localPosition = p.pos;
                    inst.transform.localRotation = Quaternion.Euler(0f, p.yaw, 0f);
                    inst.transform.localScale    = Vector3.one * p.scale;
                    inst.name = $"{markerPrefab.name}_{k:000}";
                    Undo.RegisterCreatedObjectUndo(inst, "Place Marker");
                }
                EditorUtility.SetDirty(container.gameObject);
            }
        }

        private Transform GetOrCreateContainer(Transform tile, string name)
        {
            var existing = tile.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Marker Container");
            go.transform.SetParent(tile, false);
            return go.transform;
        }

        private void ClearAll()
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                foreach (var name in new[] { GridContainer, ScatterContainer })
                {
                    var container = tile.Find(name);
                    if (container != null) Undo.DestroyObjectImmediate(container.gameObject);
                }
            }
        }
    }
}
#endif
