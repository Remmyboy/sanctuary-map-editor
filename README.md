# Sanctuary Map Editor

A map editor for **Sanctuary: Shattered Sun**. It opens, edits, creates and
saves the game's own `.sanmap` maps, and needs only the game installed.

It began as a fork of the
[FA Forever Map Editor](https://github.com/FAForever/FAForeverMapEditor)
(MIT, by ozonexo3 and the FAForever contributors), whose terrain tools,
brushes, marker tools and undo it keeps. The Supreme Commander side is gone:
the editor no longer reads or writes SupCom maps or needs a Forged Alliance
install, and FAF's vault upload, map generator and links are removed. The
upstream licence in `LICENSE.md` still applies; upstream is the `upstream`
remote.

## Status

| Phase | What | State |
|---|---|---|
| 1 | `.sanmap` format layer, no Unity dependency | **done**, tested on 150 maps |
| 1b | Project on Unity 6000.3 LTS, headless build | **done**: compiles clean, builds, launches |
| 2 | Open a `.sanmap` in the editor: terrain, water, markers, playable area | **done** (see below) |
| 3 | Save back: heightmap, stratum masks, markers, unedited fields kept | **done**: verified in game |
| 3b | Save As, regenerated `preview.png`, New Map for Sanctuary | **done**: a new map plays in game |
| 4 | Terrain rendering close to the game's `RTS/TerrainLit` | partly: layer colours calibrated against the game's previews; the game's shader source would make it exact |
| 5 | Sanctuary prop library (`.santp`, `.sanmodel`): show and edit props | **done**: show, paint, erase, select/move/turn/scale/delete, undo; the saved map plays in game |
| 5c | Playable area editor (type or drag), undo for the Sanctuary tools | **done**: the game reads the drawn area |
| 5b | Markers tool, texture layer picker | **done**: the saved map plays in game |
| 7 | Release zip (`Tools~/package.ps1`) | **done**; not published |
| 6a | Sanctuary-only: SupCom formats, FA install, FAF services and links removed from the product | **done** |
| 6b | Delete the FA internals left behind (units, FA props/decals/lighting code, Lua libraries, HazardX scmap reader) | **done**: verified in game |

## FA internals deleted (6b)

- **Gone:**
  - **Systems:** units (tool, browser, controller, blueprints, strategic
    icons), FA props and decals (with the decal renderer on both cameras), the
    resource browser, the Lighting tab, wave generators and FAF adaptive
    markers.
  - **File formats:** the `.scmap` reader and writer and the SupCom model
    reader.
  - **Lua:** NLua, KopiLua and KeraLua, the Lua parser and writer, and every
    Lua load/save in the scenario model.
  - **FA game data:** the `.scd` reader, cubemaps, install paths and FAF
    vault versioning.
- **What remains of the FA design** is the editor's in-memory model:
  `ScenarioLua` / `SaveLua` (armies, markers, areas, now plain data with no
  Lua in them) and the HazardX `Map` (terrain, water, layers). Sanctuary maps
  are loaded into it and saved out of it. Renaming and slimming it is
  cosmetic now.
- **Kept or moved:** the spawn rings' drawing helper moved into
  `RenderSpawnRanges`. Text-field number parsing moved to `TextNumbers`, with
  identical behaviour. `EnvPaths` is now only "which folder does this dialog
  open in".
- **Scene:** `StandaloneCleanup.RemoveFaSystems` and `RemoveMissingScripts`
  removed the FA objects and dead components. The build has no
  missing-script warnings.
- **Dev tools:**
  - `Tools~/fastcheck.mjs` compiles the Unity-generated project with dotnet
    in seconds; regenerate it with
    `-executeMethod Packages.Rider.Editor.RiderScriptEditor.SyncSolution`.
  - `dropmember.mjs` and `dropenclosing.mjs` delete C# members by signature
    or by the compiler's error lines.
- **Size:** 180 scripts and about 31,000 lines, down from 240 scripts. This
  is the plugin and library list after the deletions.
- **Checked (2026-11-09):**
  - **Opening:** Two Step Shuffle, The Forge, White Desert, Alpha 7 Quarantine
    and a generated map open with no exceptions.
  - **Saving:** unedited saves of Two Step Shuffle and The Forge are
    identical to the originals, and the test edit diffs exactly.
  - **Save As and New Map:** both work.
  - **In game:** the test map, re-edited in place through File > Save, loads
    in a skirmish. `Engine.SampleTerrainHeight` reads the file's 22.234 m in
    the twice-raised square and the untouched 25.337 m outside it.
- **Known gap:** undoing a stratum texture change reloads the layer without
  the display colour bake (`diffuseRemap`), so it draws brighter until the map
  is reopened. Display only; files are unaffected.

## Sanctuary-only (6a)

What changed, so it can be reviewed:

- **Scene:** `Assets/Editor/Scripts/StandaloneCleanup.cs` made the scene
  changes, and it can be run again (`-executeMethod StandaloneCleanup.Run`).
  - **Hidden:** the Lighting, Props, Decals and Units tabs; File > Save as new
    version, Play map and Render; Tools > Resource browser, Unit browser and
    Map uploader; the AI intel grid; FAF's Discord, forum, wiki and Gaea help
    links; the map page's vault version, FA slot advice and scmap
    version/shader; FA wave generators; the FA terrain-shader texture tools;
    the FAF and FA folders and play-in-FA options in Preferences.
  - **Destroyed:** the vault-upload and Neroxis objects.
  - **Splash:** retitled, still crediting the original editor.
  - Objects that scripts still reference are deactivated rather than
    destroyed. `SceneTools.Dump` lists the scene, with each button's call.
- **Deleted:** FAF vault upload (`UploadMapPopUp`, DotNetOpenAuth, log4net),
  the Neroxis map generator and toolsuite runner, the stray Neroxis snapshot
  map at the repo root.
- **Code:**
  - **Opening and saving:** File > Open, recent maps and drag-and-drop take
    only `.sanmap`. The SupCom scenario/scmap loader and writer in
    `MapLuaParser` are gone, and Save only writes Sanctuary maps.
  - **Startup:** no longer scans for a Forged Alliance install. It indexes the
    game's `.sanpack` files instead.
  - **New Map:** always makes a Sanctuary map.
  - **Preferences:** set the Sanctuary install folder and the backup folder.
  - **Update check and Help:** both point at this repository.
  - **Version:** v0.1.
- **Checked after the change:**
  - **Startup:** no exceptions; the splash is retitled.
  - **Unedited saves:** Two Step Shuffle and The Forge come back identical
    (JSON and every binary).
  - **Test edit:** exactly the intended diff.
  - **Save As and New Map:** both still work.
  - **Not tested:** the game wasn't launched again for this change, which
    doesn't touch what gets written.

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

## Phase 2: opening Sanctuary maps

**File > Open** now offers `.sanmap` and starts in the game's Maps folder. To
open a map without the UI:

```
Build\SanctuaryMapEditor.exe -sanmap <map folder or .sanmap> [-screenshot out.png] [-quit]
powershell -File Tools~/open-map.ps1 -Map The_Forge      # same, then prints the loader's log lines
```

`Assets/Scripts/Sanctuary/Runtime/`:

- `SanctuaryPaths` - finds the game through Steam's library folders.
- `SanGamedata` - indexes the game's `*.sanpack` files (plain zips) and
  loads textures the way the game does (the extension a map names is ignored,
  `.dds` is tried first, `map/` paths resolve in the map folder). It decodes
  BC7/DX10 DDS, which most Sanctuary strata use and the FA loader can't read.
- `SanctuaryMapLoader` - fills the editor's FA-shaped state (scenario, markers,
  HazardX `Map`) from the `.sanmap`, the same way `NewMap` fills it for a blank
  map, and keeps the `SanMapFolder` open for saving.

What loads:

- **Terrain:** heights come in at full 16-bit precision. The terrain's height is
  the map's own `height` (128 to 1600 m across the shipped maps) instead of
  FA's fixed 512 m.
- **Strata:** all nine layers with the map's tiling, plus both splat masks.
- **Water:** level and depth.
- **Playable area:** `PlayableArea`.
- **Markers:** spawns become FA army markers named after their army; resource
  markers become mass markers.
- **Sun direction:** from `sunRA` and `sunDA`.

Checked on 8 maps (developer maps, SCFA conversions, a generated map, 513 to
4097 resolution, with and without water), with no exceptions. Every marker sits
within 0.08 m of the terrain on the developer maps and conversions. The Forge's
one 6.35 m outlier is in the map file itself (`AlloyMarker_3`).

Not yet:

- **Saving** is phase 3.
- **Props and decals** don't load.
- **Look:** terrain colours skip the game's per-layer `diffuseRemap`, so they
  run bright and saturated, and lighting is FA's model (phase 4).
- **Generated sea:** the water colour is a generated stand-in.
- **Units:** the FA unit browser stays empty and no longer forces the
  Preferences dialog open at startup.

Two upstream bugs fixed along the way:

- **Stratum texture array:** building it no longer fails when the layers have
  different sizes or mip counts.
- **Loading popup:** it no longer throws a null reference when infos are queued.

### Colours

The game draws each stratum as albedo × `diffuseRemap`, and the remap carries
most of the colour (The Forge's sand texture is nearly white; its remap is a
dark tan). `SanctuaryMapLoader.BakeDiffuseRemap` bakes that product into the
copy of each layer shown on screen, under neutral light built from the map's
sun temperature and tint. Ambient is a 10000 K daylight sky, because every
shipped map has `skylightIntensity` 0, so the game takes ambient from the
skybox. A fixed share of sky haze is added on top. This was calibrated against
the game's own `preview.png` with `Tools~/compare-preview.ps1`. Mean colour,
editor/preview, red/green/blue:

| Map | Red | Green | Blue |
|---|---|---|---|
| The Forge | 0.88 | 0.94 | 0.96 |
| Two Step Shuffle | 1.14 | 1.15 | 0.97 |
| There Is Time (not tuned on) | 0.82 | 0.79 | 0.82 |
| White Desert | 0.86 | 0.98 | 1.05 |

White Desert's hue still leans grey where the game is pinkish tan.
Overrides: `-remapgain -ambient -sunlevel -haze -hazebright`.

## Phase 3: saving

**File > Save** on a Sanctuary map runs `SanctuaryMapSaver` instead of the FA
writer. It first copies the files it rewrites to
`%LOCALAPPDATA%\SanctuaryMapEditor\Backups\<map>\<time>\`. It writes the
heightmap, both stratum masks, markers (spawns, resource markers), armies,
`PlayableArea`, water level, name and description. Everything else in the
`.sanmap` passes through. FA-only marker types (AI, camera, ...) are not
saved, and the report names them.

Nothing the editor didn't change gets rewritten:

- **Heightmap:** cells still equal to the terrain as loaded keep their
  original 16-bit value, so Unity's internal height precision never leaks in.
- **Masks:** unpainted pixels keep their bytes.
- **Markers, area and water:** an axis the editor didn't move keeps its
  original double.

Checks, with `-testedit` (a fixed edit: 40×40 cells raised 5 m, a 16×16
mask square set to layer 1, the first spawn moved 8 m east), `-saveto <dir>`
and `dotnet run --project Tools~/SanFormatTests -- --diff <original> <saved>`:

- **Unedited save** (Two Step Shuffle, Alpha 7 Quarantine, The Forge):
  the JSON is identical and the heightmap and all four TGAs are
  byte-identical.
- **Edited save** (the same three maps): the diff shows exactly the 1600
  raised cells (5.00 m; 4.98 m on The Forge, whose 1600 m height scale makes
  Unity's step about 0.05 m), the mask square, and the one marker `x`. Nothing
  else changed.
- **In game** (2026-11-09): the edited Two Step Shuffle, saved into the
  game's Maps folder, shows in the map list and starts a skirmish against
  the AI. `Engine.SampleTerrainHeight` reads 17.229 m inside the raised square
  (12.224 m on the original map) and 25.337 m outside it (unchanged), matching
  the file. The raised square shows in the central crater.

Not yet: saving through the menu by hand is untested. Every check above ran
from the command line, through the same functions the menu calls.

## Save As, previews, new maps

- **Save As** (and *Save as new version*, which means nothing to Sanctuary)
  picks or creates a folder in the game's Maps folder. The folder name is the
  map's key, so it is made safe (`My Test Map` becomes `My_Test_Map`). An
  existing non-empty folder is never overwritten. The map is written there
  with all its textures, and if its name wasn't edited it takes the folder's
  name, so two copies don't share a name in the lobby.
- **`preview.png`** is redrawn on every save. The lobby reads only that file
  (`LobbyManager`). It is a top-down view of the playable area, north up, with
  numbered slot badges in the developers' colours, which were read off their
  previews with `Tools~/sample-badges.ps1`. A true orthographic camera renders
  this terrain shader black, so it is a 5° telephoto from far above.
- **New Map**, when a Sanctuary install is found, makes a Sanctuary map
  (`SanMapFactory`):
  - **Look:** texture layers, lighting, fog, sky and water style come from the
    open Sanctuary map (else Two Step Shuffle).
  - **Size:** the window's width and height are the playable area. The terrain
    is twice that, capped at 4096 m, with the area centred, as on the
    developers' maps.
  - **Contents:** flat ground at the chosen height, optional water, a ring of
    spawns, empty masks and neutral tint maps (values measured on the shipped
    maps).
  - **Finish:** the new map is then opened and saved once, which draws its
    preview.

Command line, through the same functions:

```
SanctuaryMapEditor.exe -newmap <maps dir> <name> <playable m> <armies> <ground m> <water m or -1>
SanctuaryMapEditor.exe -sanmap <map> -saveas <folder>     # Save As minus the dialog
SanctuaryMapEditor.exe -sanmap <map> -menusave            # File > Save
```

Checked:

- **Save As:** Two Step Shuffle saved as "My Test Map". The only JSON change is
  the name; the heightmap and TGAs are byte-identical, and the preview is
  redrawn.
- **Previews:** for The Forge, Two Step Shuffle and White Desert the framing
  and badge positions match the game's own previews.
- **New map in game:** made into the game's Maps folder, it starts a skirmish
  against the AI. `Engine.SampleTerrainHeight` reads the chosen 30 m, the
  simulation ticks, and the commander and the AI start on their spawns
  (2026-11-09).

## Markers, texture layers, props

- **Markers:** the Markers tab adds and deletes spawns and alloy spots. New
  ones take the map's own naming (`Army_5` on a map that uses `Army_`,
  `ARMY_9` on one that uses `ARMY_`), the lowest free number, compared without
  case. Saving keeps the scenario's army list in step with the spawns.
- **Texture layers:** clicking a layer's albedo or normal on the Textures tab
  opens the picker: the game's stratum textures by biome, with thumbnails. A
  pick sets the albedo with its sibling normal and mask, and the tint the
  shipped maps use with it (or white). The sliders set the layer's tint
  (`diffuseRemap`, linear). Scale is the layer's usual field.
- **Props:** the **Props** button (middle of the menu bar) opens the panel.
  All props draw instanced with their albedo, lit like the terrain (no normal
  maps or wind). A circle on the ground shows the brush; `[` and `]` resize it.
  *Paint* scatters the chosen prop over the circle, on the ground, with random
  turn; *Erase* removes every prop in it. *Select* picks the prop clicked
  (ringed): drag it to move it along the ground, turn it 15° or 90°, make it
  smaller or bigger, drop it to the ground, or delete it (Delete key). Props
  are replaced, never changed in place, and the list is rewritten only when it
  differs from the last load or save, so an unedited map keeps its props byte
  for byte.
- **Playable area:** Map > Areas edits the map's one playable area (FA maps had
  a list of script areas; the extra ones were never saved). Type the edges, or
  drag a rectangle on the map; it snaps to 4 m with rounding on.
- **Undo:** Ctrl+Z / Ctrl+Y undo and redo each brush stroke, prop drag, prop
  button, texture pick, tint and playable-area drag, through the editor's own
  undo list. An undone prop edit counts as unsaved only if it differs from the
  last save.

Command line checks, each followed by `-menusave` or `-saveto`:
`-testmarkers` (a spawn and an alloy spot added at the centre, one alloy spot
removed), `-testlayers` (layer 2 to Winter `dirty_ground`, layer 1 scale
doubled), `-testprops` (25 Highlands trees painted at the centre, every prop
within 40 m of a point 120 m east erased), `-testtools` (19 PASS/FAIL checks
of the Select tool, a paint stroke, a texture pick, a tint and a drawn
playable area, each undone and redone through the editor's undo; leaves the
playable area at 200-824 m, one prop moved 20 m east and one deleted).

`-testtools` on Two Step Shuffle: 19 of 19 pass, and the saved map differs from
the original in exactly the area and the two props. Saved into the game, a
skirmish's playable area reads 200-824 m: points at 210 and 815 m are inside,
190 and 830 m outside (2026-11-09). The mouse handling itself (the drags, the
circle following the cursor) has not been run: the scripts call the same
edit functions the mouse does, and nobody has used it by hand yet.

Checked: on Forge and Alpha 7 the marker files diff exactly as intended;
the layer edit diffs exactly; Forge's 63,538 props load, and paint, erase and
save diff exactly. All three edits saved together onto a copy of Two Step
Shuffle start a skirmish against the AI with nothing in the game's log, and
the new alloy spot is where it was put (2026-11-09). The new trees and the
erased circle weren't found in the game's camera view; only the alloy spot was
seen.

## Clicking through the UI

`SanctuaryMapEditor.exe -sanmap <map> -uitour <out dir> -quit` opens each
tab and menu, presses every button and toggle that doesn't open a file
dialog, a browser or quit, and writes `tour.txt` (each press, and any
exception or error it caused), a screenshot of every page, and `texts.txt`
(every UI text in the scene, shown or hidden). Nothing is saved. Its first
run found the Markers tab broken (a page deleted with the FA code) and the
Props button covering File > Open; it now passes with no problems.

FA-only controls still shown after 6a/6b were hidden by
`StandaloneCleanup.SanctuaryUiPass` and `ToolPage`: Map > Armies (FA teams,
factions, colours, AI plans), Markers > Chains and the AI marker layers,
FAF marker import/export, Terrain > Type (FA terrain types), FA water
shader inputs, and Textures > Tools (FA stratum templates).

## Release zip

`Tools~/package.ps1 [-Version v0.1] [-NoBuild]` builds and writes
`release\SanctuaryMapEditor-<version>-win64.zip` (about 93 MB): the exe, Unity's
runtime, LICENSE.md and a short README.txt. Publishing it as a GitHub release
is a separate step.

## What's next

- **Rendering:** layer colours are calibrated by eye against the game's own
  previews (above); White Desert is still about 30% dark. Matching the game's
  terrain and water shaders needs the game's shader code.
- **Unity version:** 6000.3 LTS (6000.3.28f1), the same line the game is
  built on (6000.3.22f1), so loading the game's own assets stays possible.

## Building

Needs Unity 6000.3.28f1 (install it through Unity Hub) and a signed-in Unity
licence (the free Personal licence will do).

```
"C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe" -batchmode -quit ^
  -projectPath . -executeMethod SanctuaryBuild.Build -buildPath Build -logFile build.log
```

This writes `Build\SanctuaryMapEditor.exe`. The same build is on the
**Build > Sanctuary Map Editor (64bit)** menu in the Unity editor.
