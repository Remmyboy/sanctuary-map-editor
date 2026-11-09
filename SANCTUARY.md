# Sanctuary Map Editor

A fork of the [FA Forever Map Editor](https://github.com/FAForever/FAForeverMapEditor)
(MIT, by ozonexo3 and the FAForever contributors) being ported to edit
**Sanctuary: Shattered Sun** maps (`.sanmap`) natively. The upstream licence
in `LICENSE.md` still applies; upstream is the `upstream` remote.

Why this editor: it already builds maps on Unity's own terrain system, as
Sanctuary does, so the sculpting and painting brushes, marker tools, undo,
camera and UI carry over. What has to change is everything that touches
Supreme Commander's files: the map format, the terrain and water shaders, and
the texture and prop libraries, which it reads from a Forged Alliance install.

## Status

| Phase | What | State |
|---|---|---|
| 1 | `.sanmap` format layer, no Unity dependency | **done**, tested on 150 maps |
| 2 | Open a `.sanmap` in the editor: terrain, water, markers, playable area | next; needs Unity 2022.3 |
| 3 | Save back: heightmap, stratum masks, markers, unedited fields kept | |
| 4 | Terrain rendering close to the game's `RTS/TerrainLit` (9 layers: albedo, normal, mask, tiling, `diffuseRemap`) | |
| 5 | Sanctuary texture and prop libraries (`Environment.sanpack`, `.santp`, `.sanmodel`) | |
| 6 | Remove or hide FA-only tools: units, AI markers, Lua scenario scripts, FAF vault upload, Neroxis generator | |

## Phase 1: the format layer

`Assets/Scripts/Sanctuary/Format/` - plain C#, Newtonsoft only, compiles in
Unity and outside it.

- `SanMapDocument` - the JSON. Keeps the whole object, so the ~80 lighting,
  fog, water and sky fields with no UI here survive a load/save untouched;
  typed accessors for size, water, `PlayableArea`, armies, markers, props and
  stratum layers.
- `SanHeightmap` - `Textures/heightmap.raw`, uint16 little-endian. Converts
  to and from the array `TerrainData.SetHeights` takes, flipping rows and
  scaling by 1/65536 exactly as the game does.
- `SanTga` - the four 32-bit TGAs. Both header variants the game ships
  (bottom-left `0x08` on the developers' maps, top-left `0x28` on converted
  ones) and the TGA 2.0 footer are kept verbatim.
- `SanMapFolder` - the folder as a whole: lazy loading (a stratum TGA can be
  64 MB), save in place or to a new folder, and `Validate()` for the faults
  the game accepts silently.

`Tools~/SanFormatTests` round-trips every map in the game folder (Unity ignores
`~` folders):

```
dotnet run -c Release --project Tools~/SanFormatTests -- [mapsDir] [--textures N|all] [--map Name]
```

It checks that the JSON survives load and save, that the heightmap and every
TGA rewrite byte for byte, and that saving a folder somewhere new reproduces
every file. It also checks that spawn markers sit on the terrain. That check
is what pins down the z orientation: the developers' maps agree within
0.67 m, and maps from the SCFA converter agree to 0.00 m.

### Format facts the code relies on

- Map folder `Maps/<Folder>/` holds `<Folder>.sanmap`, `preview.png` and
  `Textures/{heightmap.raw, stratums_1_4.tga, stratums_5_8.tga,
  tint_colors.tga, tint_geometry.tga}`. The game refuses the map if any of
  the four TGAs is missing.
- `heightmapResolution` must be 2^n+1 (the game falls back to `width + 1`
  when absent). Heights in metres = raw / 65536 × `height`. File row 0 is
  Unity heightmap row `res-1`, the +z edge.
- Stratum masks can be a different resolution from the heightmap (2048 or
  4096 on the developers' maps; equal to the heightmap on converted ones).
  `stratums_1_4` RGBA = layers 1-4, `stratums_5_8` RGBA = layers 5-8, layer 0
  is the base.
- `areas` must use the key `PlayableArea`; the engine ignores other names.
- The player count is the `armies` dictionary, not the spawn markers. Spawn
  markers are type `Spawn`, named `Army_N` (developers) or `ARMY_N`
  (converted); match case-insensitively. Resource markers are any type with
  `"resource": true` (`Alloys` in practice).
- Texture paths starting `map/` are relative to the map folder.
- One shipped map, Two Step Shuffle, has `width` 1023 on a 1025 heightmap and
  still plays, so `Validate()` reports that only as a warning.

The game's own map code (`EM.Map.SanMap`, `EM.Map.MapManager` in
`Trebuchet.dll`) was read to establish these facts. None of it is copied
here, and none may be: it is not MIT.

## Phase 2 onwards: where the seams are

- **Load:** `MapLuaParser.LoadingFile` refuses anything without a
  `version = 3` scenario `.lua` and an FA `env.scd`. Add a `.sanmap` branch
  ahead of those checks that fills the editor's state from `SanMapFolder`.
  `ScmapEditor.LoadHeights` builds the Unity terrain from `map.GetHeight`.
  The Sanctuary version feeds `SanHeightmap.ToUnityHeights()` instead.
- **World scale:** the editor works at 1/10 scale (`ScmapPosToWorld`,
  `xRes / 10f`). Keep that inside the editor and convert at the format
  boundary, so none of the brush and marker code changes.
- **Markers:** map `Spawn` onto the editor's army markers and resource types
  onto its mass markers. Write them back with
  `SanMapDocument.SetMarkers`, which keeps any extra fields a marker had.
- **FA coupling:** 77 of 240 scripts reference the SCMAP code or read FA game
  files. The heaviest are `ScmapEditor.cs` (29 call sites), `ResourceBrowser`
  (24), `FafEditorSettings` (19), `StratumInfo` (16), `UnitBrowser` (15),
  `PropsInfo` (11) and the HazardX `Map.cs` (11).
- **Shaders:** `Assets/GFX/Shaders/FaTerrainShader.shader` and
  `FaWaterShader.shader` get Sanctuary counterparts in phase 4.
- **Unity version:** stay on 2022.3. The editor reads raw files and never
  loads the game's own Unity assets, so it does not need to match the game's
  Unity 6000.3.
