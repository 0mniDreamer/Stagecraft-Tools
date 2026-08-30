#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

// Strobe Emulator - drives the strobe globals (_SingleStrobe1..N) live in the SCENE VIEW,
// with no Play mode and no game load. Open via  Window > StageWrench > Strobe Emulator.
//
// Two modes:
//   Custom Channels  - per-channel colour + pattern (BeatFlash, Chase, etc.)
//   Standard 4-Colour - emulates a default rig: pick 4 palette colours and the tool cycles
//                       them across the strobe channels on the beat. Approximates the game's
//                       built-in behaviour; tune Beats-per-Colour / Rotate / Sustain to match.
//
// Put this file in an "Editor" folder (e.g. Assets/Editor/). It ticks off EditorApplication.update
// and repaints all views so materials reading these globals animate live while you tune them.
public class StrobeEmulatorWindow : EditorWindow
{
    enum Mode    { CustomChannels, Standard4Colour }
    enum StdSub  { Unified, Rotate }
    enum Pattern { Off, BeatFlash, Sustained, SinePulse, EveryOtherBeat, Chase, FastStrobe, Manual }

    [System.Serializable]
    class Channel
    {
        public bool    enabled        = true;
        public string  reference      = "_SingleStrobe1";
        public Color   color          = Color.white;
        public Pattern pattern        = Pattern.BeatFlash;
        public float   beatMultiplier = 1f;
        public float   phaseOffset    = 0f;
        public float   intensity      = 1f;
        [System.NonSerialized] public float manualTrigger = 0f;
    }

    [SerializeField] Mode  mode = Mode.CustomChannels;
    [SerializeField] List<Channel> channels = new List<Channel>();

    // Standard 4-colour mode
    [SerializeField] Color[] palette = new Color[4]
    {
        new Color(1f, 0.20f, 0.30f), new Color(0.20f, 0.55f, 1f),
        new Color(1f, 0.85f, 0.20f), new Color(0.40f, 1f, 0.45f)
    };
    [SerializeField] float  cycleBeats = 1f;                // beats before advancing to next colour
    [SerializeField] StdSub stdSub     = StdSub.Rotate;
    [SerializeField] bool   sustain    = false;             // hold colour vs flash it each step

    [SerializeField] float bpm             = 120f;
    [SerializeField] float masterIntensity = 1f;
    [SerializeField] float sharpness       = 6f;            // match the shader's _Decay

    bool    playing;
    double  startTime;
    double  lastTick;
    Vector2 scroll;

    [MenuItem("SynthRiders/Stagecraft/10. Strobe Emulator")]
    [MenuItem("Window/StageWrench/Strobe Emulator")]
    static void Open()
    {
        var w = GetWindow<StrobeEmulatorWindow>("Strobe Emulator");
        w.minSize = new Vector2(340, 420);
    }

    void OnEnable()
    {
        if (channels.Count == 0)
        {
            channels.Add(new Channel { reference = "_SingleStrobe1", color = new Color(1f, 0.25f, 0.3f) });
            channels.Add(new Channel { reference = "_SingleStrobe2", color = new Color(0.3f, 0.5f, 1f), phaseOffset = 0.5f });
        }
        lastTick  = EditorApplication.timeSinceStartup;
        startTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        Blackout();
    }

    static int Mod4(int v) => ((v % 4) + 4) % 4;
    float CurrentSongTime() => (float)(EditorApplication.timeSinceStartup - startTime);
    float CurrentBeats()    => CurrentSongTime() * (bpm / 60f);

    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float  dt  = (float)(now - lastTick);
        lastTick = now;

        foreach (var ch in channels)
            if (ch.pattern == Pattern.Manual)
                ch.manualTrigger = Mathf.Max(0f, ch.manualTrigger - dt * (sharpness * 0.5f));

        if (!playing) return;

        float beats = CurrentBeats();
        float songTime = CurrentSongTime();

        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            if (string.IsNullOrEmpty(ch.reference)) continue;
            Color c = ch.enabled ? OutputFor(ch, i, beats, songTime) : Color.black;
            c.a = 1f;
            Shader.SetGlobalColor(ch.reference, c);
        }

        InternalEditorUtility.RepaintAllViews();
        Repaint();
    }

    // Final colour for a channel in whichever mode is active
    Color OutputFor(Channel ch, int index, float beats, float songTime)
    {
        if (mode == Mode.Standard4Colour)
        {
            float b     = beats / Mathf.Max(0.01f, cycleBeats);
            int   step  = Mathf.FloorToInt(b);
            float phase = b - step;
            float env   = sustain ? 1f : Mathf.Pow(1f - phase, sharpness);
            Color pc    = (stdSub == StdSub.Rotate) ? palette[Mod4(index + step)] : palette[Mod4(step)];
            return pc * (env * masterIntensity);
        }
        // Custom
        float v = Evaluate(ch, beats, songTime);
        return ch.color * (v * ch.intensity * masterIntensity);
    }

    float Evaluate(Channel ch, float beats, float songTime)
    {
        float b;
        switch (ch.pattern)
        {
            case Pattern.Off:       return 0f;
            case Pattern.Sustained: return 1f;
            case Pattern.BeatFlash:
            case Pattern.Chase:
                b = beats * ch.beatMultiplier + ch.phaseOffset;
                return Mathf.Pow(1f - (b - Mathf.Floor(b)), sharpness);
            case Pattern.EveryOtherBeat:
                b = beats * ch.beatMultiplier + ch.phaseOffset;
                int whole = Mathf.FloorToInt(b);
                if ((whole & 1) == 1) return 0f;
                return Mathf.Pow(1f - (b - whole), sharpness);
            case Pattern.SinePulse:
                b = beats * ch.beatMultiplier + ch.phaseOffset;
                return 0.5f + 0.5f * Mathf.Sin(b * Mathf.PI * 2f);
            case Pattern.FastStrobe:
                b = beats * ch.beatMultiplier * 4f + ch.phaseOffset;
                return (Mathf.FloorToInt(b) % 2 == 0) ? 1f : 0f;
            case Pattern.Manual:
                return ch.manualTrigger;
        }
        return 0f;
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        mode = (Mode)EditorGUILayout.EnumPopup("Mode", mode);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = playing ? new Color(1f, 0.45f, 0.45f) : new Color(0.45f, 1f, 0.5f);
            if (GUILayout.Button(playing ? "\u25a0  Stop" : "\u25b6  Play", GUILayout.Height(30)))
            {
                playing = !playing;
                if (playing) { startTime = EditorApplication.timeSinceStartup; lastTick = startTime; }
                else Blackout();
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Blackout",   GUILayout.Height(30), GUILayout.Width(90))) Blackout();
            if (GUILayout.Button("Reset Time", GUILayout.Height(30), GUILayout.Width(90)))
            { startTime = EditorApplication.timeSinceStartup; lastTick = startTime; }
        }

        EditorGUILayout.Space();
        bpm             = EditorGUILayout.Slider("BPM", bpm, 40f, 240f);
        masterIntensity = EditorGUILayout.Slider("Master Intensity", masterIntensity, 0f, 4f);
        sharpness       = EditorGUILayout.Slider("Flash Sharpness (= _Decay)", sharpness, 1f, 20f);

        EditorGUILayout.Space();
        if (mode == Mode.Standard4Colour) DrawStandard();
        else                              DrawCustom();

        EditorGUILayout.HelpBox(
            "Drives the strobe globals live in the Scene view - no Play mode, no game load.\n" +
            "If props stay black: confirm the material uses StageWrench/StrobeFX (or reads these " +
            "references), and enable HDR + Bloom in your URP asset/volume for the glow.",
            MessageType.Info);
    }

    void DrawStandard()
    {
        EditorGUILayout.LabelField("Player Palette (4 Colours)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
            for (int i = 0; i < 4; i++)
                palette[i] = EditorGUILayout.ColorField(GUIContent.none, palette[i], false, false, true);

        cycleBeats = EditorGUILayout.Slider("Beats per Colour", cycleBeats, 0.25f, 8f);
        stdSub     = (StdSub)EditorGUILayout.EnumPopup(
                        new GUIContent("Spread", "Unified = all channels show the current colour; " +
                                                 "Rotate = the 4 colours move across the channels"), stdSub);
        sustain    = EditorGUILayout.Toggle(
                        new GUIContent("Sustain (hold, no flash)"), sustain);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Target Channels", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add", GUILayout.Width(56)))
                channels.Add(new Channel { reference = "_SingleStrobe" + (channels.Count + 1) });
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        int removeIdx = -1;
        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                ch.enabled   = EditorGUILayout.Toggle(ch.enabled, GUILayout.Width(16));
                ch.reference = EditorGUILayout.TextField(ch.reference);
                Color live = (ch.enabled && playing) ? OutputFor(ch, i, CurrentBeats(), CurrentSongTime()) : Color.black;
                Rect r = GUILayoutUtility.GetRect(46, 16, GUILayout.Width(46));
                EditorGUI.DrawRect(r, Color.black);
                EditorGUI.DrawRect(r, new Color(live.r, live.g, live.b, 1f) * Mathf.Clamp01(live.maxColorComponent));
                if (GUILayout.Button("\u00d7", GUILayout.Width(22))) removeIdx = i;
            }
        }
        EditorGUILayout.EndScrollView();
        if (removeIdx >= 0) channels.RemoveAt(removeIdx);
    }

    void DrawCustom()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Strobe Channels", EditorStyles.boldLabel);
            if (GUILayout.Button("Distribute Chase", GUILayout.Width(130))) DistributeChase();
            if (GUILayout.Button("+ Add", GUILayout.Width(56)))
                channels.Add(new Channel { reference = "_SingleStrobe" + (channels.Count + 1) });
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        int removeIdx = -1;
        for (int i = 0; i < channels.Count; i++)
        {
            var ch = channels[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ch.enabled   = EditorGUILayout.Toggle(ch.enabled, GUILayout.Width(16));
                    ch.reference = EditorGUILayout.TextField(ch.reference);
                    float v = (ch.enabled && playing)
                        ? Evaluate(ch, CurrentBeats(), CurrentSongTime()) * ch.intensity * masterIntensity : 0f;
                    Rect r = GUILayoutUtility.GetRect(46, 16, GUILayout.Width(46));
                    EditorGUI.DrawRect(r, Color.black);
                    EditorGUI.DrawRect(r, ch.color * Mathf.Clamp01(v));
                    if (GUILayout.Button("\u00d7", GUILayout.Width(22))) removeIdx = i;
                }
                ch.color          = EditorGUILayout.ColorField("Color", ch.color);
                ch.pattern        = (Pattern)EditorGUILayout.EnumPopup("Pattern", ch.pattern);
                ch.beatMultiplier = EditorGUILayout.Slider("Rate (\u00d7 beat)", ch.beatMultiplier, 0.25f, 8f);
                ch.phaseOffset    = EditorGUILayout.Slider("Phase Offset", ch.phaseOffset, 0f, 1f);
                ch.intensity      = EditorGUILayout.Slider("Intensity", ch.intensity, 0f, 4f);
                if (ch.pattern == Pattern.Manual && GUILayout.Button("Trigger Flash"))
                    ch.manualTrigger = 1f;
            }
        }
        EditorGUILayout.EndScrollView();
        if (removeIdx >= 0) channels.RemoveAt(removeIdx);
    }

    void DistributeChase()
    {
        int n = channels.Count;
        for (int i = 0; i < n; i++)
        {
            channels[i].pattern     = Pattern.Chase;
            channels[i].phaseOffset = n > 0 ? (float)i / n : 0f;
        }
    }

    void Blackout()
    {
        foreach (var ch in channels)
            if (!string.IsNullOrEmpty(ch.reference))
                Shader.SetGlobalColor(ch.reference, Color.black);
        InternalEditorUtility.RepaintAllViews();
    }
}
#endif
