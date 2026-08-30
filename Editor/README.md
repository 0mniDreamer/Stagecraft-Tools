# Stagecraft Tools

An editor toolkit for the SRStageWrench template. One coherent kit:

```
StagecraftTools/
  Editor/                       (editor-only assembly — never ships)
    Stagecraft.Editor.asmdef
    *.cs  (9 tools + shared util)
  Art/MarkerPylon/              (ships: shader + material + prefab)
```

Menu: **SynthRiders ▸ Stagecraft ▸ …**

Core principle: your runtime C# is stripped on export, so these tools bake
behaviour into things that DO ship (animation clips, shaders, textures, wired
toolkit events) or catch problems before you export. The editor tools live in
their own assembly (`Stagecraft.Editor.asmdef`) and reference your runtime and
toolkit types only by reflection, so the assembly stays isolated — nothing to
wire, and it can't accidentally drag editor code into a build.

## Tools
1. **Treadmill Motion Baker** — TileManager preview → looping clip (+ controller)
   that ships. Tiles must be identical for the loop to be seamless.
2. **Loop FX Baker** — spin / bob / scale-pulse looping clips (Transform-only).
   Scrolling & emission belong in a `_Time` shader.
3. **Tile Scrubber** — drag to preview the treadmill in edit mode.
4. **Export Preflight** — inert custom scripts, unwired `CustomStageInfo`, static
   movers, realtime lights, BiRP/error materials, missing scripts.
5. **Overdraw Auditor** — ranks transparent/additive renderers by screen coverage.
6. **Tile Prop Placer** — loop-safe arches / gates / rails across the tiles.
7. **Tileable Texture Generator** — seamless noise / sparkle / ramp PNGs.
8. **Stage Event Wiring** — StageEvents / Combo / Score / Time / SpecialsFX →
   SetActive / enable actions, and sets the private target values.
9. **Peripheral Marker Placer** — grid edge rows + loop-safe off-track scatter,
   feeds the MarkerPylon prefab (or your own).
10. **Strobe Emulator** — drives the `_SingleStrobe1..N` shader globals live in
   the Scene view (no Play mode) for tuning strobe FX against SG_StrobeDrive.
   Pure preview — nothing to ship. Also under **Window ▸ StageWrench**.

## MarkerPylon (Art/)
Opaque emissive edge-marker: hue driven by world-Z so it flows as it rides the
treadmill and stays continuous across seams. Built on the built-in Cube, base-
pivoted, shadows off, SRP-batches with itself. Feed it to the Marker Placer.

## Notes
Not compile-tested outside your project — smoke-test each once. The likeliest
snags are the reflection bits (event wiring's `set_enabled` bind; preflight
reading `CustomStageInfo` fields by name), which depend on your exact toolkit
build. Overdraw coverage is a ranking estimate, not a true fill count.
Generated assets land in a `Generated/` folder beside the open scene.
