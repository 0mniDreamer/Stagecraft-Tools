// TileableTextureGenerator.cs
// Bakes seamless tiling textures (noise / sparkle / gradient ramp) to PNG in the
// editor, so tiling is guaranteed and there's no Python round-trip.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CustomStage.Stagecraft
{
    public class TileableTextureGenerator : EditorWindow
    {
        private enum Kind { TilingNoise, Sparkle, GradientRamp }
        private Kind kind = Kind.TilingNoise;
        private int size = 512;
        private int seed = 1;
        private float frequency = 8f;   // lattice period across the texture (integer -> seamless)
        private int octaves = 4;
        private float sparkleDensity = 0.02f;
        private Color colA = Color.black, colB = Color.white;

        [MenuItem("SynthRiders/Stagecraft/7. Tileable Texture Generator")]
        public static void ShowWindow() => GetWindow<TileableTextureGenerator>("Tex Gen").minSize = new Vector2(340, 300);

        private void OnGUI()
        {
            kind = (Kind)EditorGUILayout.EnumPopup("Kind", kind);
            size = Mathf.ClosestPowerOfTwo(EditorGUILayout.IntField("Size", size));
            seed = EditorGUILayout.IntField("Seed", seed);
            if (kind == Kind.TilingNoise)
            {
                frequency = Mathf.Max(1, Mathf.Round(EditorGUILayout.FloatField("Frequency (int)", frequency)));
                octaves = EditorGUILayout.IntSlider("Octaves", octaves, 1, 6);
            }
            if (kind == Kind.Sparkle) sparkleDensity = EditorGUILayout.Slider("Density", sparkleDensity, 0.001f, 0.2f);
            if (kind == Kind.GradientRamp) { colA = EditorGUILayout.ColorField("Color A", colA); colB = EditorGUILayout.ColorField("Color B", colB); }

            if (GUILayout.Button("Generate PNG")) Generate();
        }

        private void Generate()
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(seed);
            switch (kind)
            {
                case Kind.TilingNoise: FillNoise(tex); break;
                case Kind.Sparkle:     FillSparkle(tex, rng); break;
                case Kind.GradientRamp:FillRamp(tex); break;
            }
            tex.Apply();

            string folder = StagecraftUtil.GeneratedFolder();
            string path = StagecraftUtil.UniqueAssetPath(folder, $"{kind}_{size}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp != null) { imp.wrapMode = TextureWrapMode.Repeat; imp.SaveAndReimport(); }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Object.DestroyImmediate(tex);
        }

        // Seamless value-noise fBm: hash an integer lattice taken modulo the
        // frequency, so left/right and top/bottom edges meet exactly.
        private void FillNoise(Texture2D tex)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float amp = 0.5f, sum = 0f, norm = 0f; int f = (int)frequency;
                for (int o = 0; o < octaves; o++)
                {
                    sum += amp * TileValue(u, v, f);
                    norm += amp; amp *= 0.5f; f *= 2;
                }
                float n = sum / norm;
                tex.SetPixel(x, y, new Color(n, n, n, 1f));
            }
        }

        private float TileValue(float u, float v, int period)
        {
            float fx = u * period, fy = v * period;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            float a = Hash(x0 % period, y0 % period);
            float b = Hash((x0 + 1) % period, y0 % period);
            float c = Hash(x0 % period, (y0 + 1) % period);
            float d = Hash((x0 + 1) % period, (y0 + 1) % period);
            tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private float Hash(int x, int y)
        {
            int h = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h & 0x7fffffff) % 10000) / 10000f;
        }

        private void FillSparkle(Texture2D tex, System.Random rng)
        {
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 1);
            int count = Mathf.RoundToInt(size * size * sparkleDensity);
            for (int i = 0; i < count; i++)
            {
                int x = rng.Next(size), y = rng.Next(size);
                float b = 0.4f + 0.6f * (float)rng.NextDouble();
                px[y * size + x] = new Color(b, b, b, 1);
            }
            tex.SetPixels(px);
        }

        private void FillRamp(Texture2D tex)
        {
            for (int y = 0; y < size; y++)
            {
                Color c = Color.Lerp(colA, colB, (float)y / (size - 1));
                for (int x = 0; x < size; x++) tex.SetPixel(x, y, c);
            }
        }
    }
}
#endif
