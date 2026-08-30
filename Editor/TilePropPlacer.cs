// TilePropPlacer.cs
// Loop-safe placement of props that span the track — arches, gates, rails —
// repeated along Z at a spacing that divides the tile, identical on every tile.
// Parents under each tile so props ride the motion and recycle with the tile.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class TilePropPlacer : EditorWindow
    {
        private Transform[] tiles = new Transform[3];
        private GameObject prop;
        private float tileLength = 2000f;
        private float spacing    = 250f;
        private float xOffset    = 0f;    // 0 = centered over the track (arches/gates)
        private bool  mirrorX    = false; // true = a copy at -xOffset too (rails)
        private float yOffset    = 0f;
        private float yaw        = 0f;
        private float zCenterOffset = 0f;
        private const string Container = "TileProps";

        [MenuItem("SynthRiders/Stagecraft/6. Tile Prop Placer")]
        public static void ShowWindow() => GetWindow<TilePropPlacer>("Prop Placer").minSize = new Vector2(340, 380);

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Tiles", EditorStyles.boldLabel);
            for (int i = 0; i < 3; i++)
                tiles[i] = (Transform)EditorGUILayout.ObjectField($"Tile {i + 1}", tiles[i], typeof(Transform), true);
            if (GUILayout.Button("Auto-find (TileWrap1/2/3)"))
                for (int i = 0; i < 3; i++) tiles[i] = StagecraftUtil.FindByName($"TileWrap{i + 1}");

            EditorGUILayout.Space();
            prop       = (GameObject)EditorGUILayout.ObjectField("Prop Prefab", prop, typeof(GameObject), false);
            tileLength = EditorGUILayout.FloatField("Tile Length", tileLength);
            spacing    = EditorGUILayout.FloatField("Spacing (Z)", spacing);
            xOffset    = EditorGUILayout.FloatField("X Offset (0 = center)", xOffset);
            mirrorX    = EditorGUILayout.Toggle("Mirror X (both sides)", mirrorX);
            yOffset    = EditorGUILayout.FloatField("Y Offset", yOffset);
            yaw        = EditorGUILayout.FloatField("Yaw", yaw);
            zCenterOffset = EditorGUILayout.FloatField("Z Center Offset", zCenterOffset);

            float per = tileLength / Mathf.Max(spacing, 0.0001f);
            int n = Mathf.RoundToInt(per);
            if (spacing > 0f && Mathf.Abs(per - n) > 0.001f)
                EditorGUILayout.HelpBox($"Spacing doesn't divide {tileLength:0} evenly — the loop will jump.", MessageType.Warning);
            else if (spacing > 0f)
                EditorGUILayout.HelpBox($"{n} props per tile. Loop-safe.", MessageType.Info);

            using (new EditorGUI.DisabledScope(!Ready()))
                if (GUILayout.Button("Place / Rebuild")) Place();
            if (GUILayout.Button("Clear")) Clear();
        }

        private bool Ready()
        {
            if (prop == null || spacing <= 0f || tileLength <= 0f) return false;
            foreach (var t in tiles) if (t == null) return false;
            return true;
        }

        private void Place()
        {
            int n = Mathf.RoundToInt(tileLength / spacing);
            var xs = new List<float> { xOffset };
            if (mirrorX && !Mathf.Approximately(xOffset, 0f)) xs.Add(-xOffset);

            foreach (var tile in tiles)
            {
                var container = tile.Find(Container);
                if (container == null)
                {
                    var go = new GameObject(Container);
                    Undo.RegisterCreatedObjectUndo(go, "Create Prop Container");
                    go.transform.SetParent(tile, false);
                    container = go.transform;
                }
                for (int c = container.childCount - 1; c >= 0; c--)
                    Undo.DestroyObjectImmediate(container.GetChild(c).gameObject);

                for (int k = 0; k < n; k++)
                {
                    float z = zCenterOffset + (k + 0.5f) * spacing - tileLength * 0.5f;
                    foreach (var x in xs)
                    {
                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prop, container);
                        inst.transform.localPosition = new Vector3(x, yOffset, z);
                        inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        inst.name = $"{prop.name}_{k:00}";
                        Undo.RegisterCreatedObjectUndo(inst, "Place Prop");
                    }
                }
                EditorUtility.SetDirty(container.gameObject);
            }
        }

        private void Clear()
        {
            foreach (var tile in tiles)
            {
                if (tile == null) continue;
                var container = tile.Find(Container);
                if (container != null) Undo.DestroyObjectImmediate(container.gameObject);
            }
        }
    }
}
#endif
