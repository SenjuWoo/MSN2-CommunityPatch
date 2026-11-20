<p align="center">
  <img src="assets/mark.svg" width="72" height="72" alt="MSN2 Community Patch + Expansion">
</p>

<div align="center">

# MSN2 Community Patch + Expansion

<p>
  <a href="https://github.com/SenjuWoo/MSN2-CommunityPatch/actions/workflows/validate.yml"><img src="https://github.com/SenjuWoo/MSN2-CommunityPatch/actions/workflows/validate.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-A06A3E?labelColor=0d1117" alt="MIT License"></a>
  <a href="https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/latest"><img src="https://img.shields.io/github/v/release/SenjuWoo/MSN2-CommunityPatch?label=release&color=A06A3E&labelColor=0d1117" alt="latest release"></a>
</p>

</div>

Runtime mods for **My Sexy Neighbour 2** (Steam, Unity 6000.2.2 Mono) built on BepInEx 5.4.23.5 + Harmony. No game files are modified.

| Mod | What it does |
|---|---|
| **MSN2 Community Patch** | Fixes the per-frame HUD exception (+23% FPS measured), the 60 FPS cap on Medium/Low, NPCs freezing on ignored interrupts, the Low preset light budget, the wardrobe interaction that threw the player off the map at low FPS, and theft spawn points inside furniture; adds graphics options, **key remapping** on Settings > Controls and off-screen animator culling. |
| **MSN2 Expansion** | MSN1-style story jobs: journal, objective tracker, dialogue, main goals + Souvenir / Hidden-Cam / Shadow / Timed bonuses, chained jobs, JSON job packs, saves beside the game save. Ships the 5-job *Neighbourhood Classics* pack. **First-game systems**: sex gallery with replay, statistics, souvenir altar, telescope, safe cracking, rain, difficulty presets. |
| **MSN2 Characters** (optional) | **Lola from the first game replaces Camila**: body, face, hair, outfits, name and portraits. Visual only, so schedules, scenes and saves are untouched; delete the DLL and Camila is back. Switches for **Yuka, Jamal and Peter** and their scenes; Yuka's unused bunny suit restored. |

## Lola replaces Camila (optional plugin)
`MSN2_Characters_v<ver>.zip` adds `MSN2Characters.dll` and `skins/lola/`. Lola's MSN1 meshes are re-bound onto Camila's skeleton at runtime and wear clones of Camila's own materials with Lola's textures, so every Camila animation, sex scene and photo works unchanged. Turn it off with `[Skins] Camila =` (empty) in `BepInEx\config\senjuwoo.msn2.characters.cfg`. The skin data is generated from the first game with `tools/charport/convert_skin.py`; only `skin.json` lives in git.

Players: download the zips from [Releases](../../releases) and extract into the game folder. In-zip readmes cover keys, settings and logs (`BepInEx\LogOutput.log`).

## Cut neighbours restored
Yuka (Emma's maid), Jamal (Camila's housemate) and Peter (Emma's husband) look cut, but they ship as residents of Camila's and Emma's houses with full schedules and couple scenes; they appear when the story opens those houses (`CUT_CONTENT.md` has the audit). MSN2 Characters adds per-neighbour and per-scene switches in `senjuwoo.msn2.characters.cfg` (`[Neighbours]`, `[Scenes]`, default on), restores Yuka's never-worn bunny suit for her evening shift, and the Expansion's *House Help* pack has a job for each of them.

## First-game systems (Expansion 0.5.0)
Sex gallery with replay (G), statistics screen (K), a souvenir altar in your room (+2% job money per trophy kind), a telescope at your window (5x phone zoom), keypad safes in Emma's house (Mastermind hints, cash + item), rainy spells (noise x0.6) and Easy/Normal/Hard detection presets. Each has a switch in `[Systems]` of `senjuwoo.msn2.expansion.cfg`.

## Repository layout
```
CURRENT.txt                    active snapshot folder
MSN2 Expansion 0.5.0/
  src/MSN2CommunityPatch       patch plugin
  src/MSN2Expansion            expansion framework plugin (Systems/ = first-game systems)
  src/MSN2Characters           optional skins plugin (Lola replaces Camila)
  skins/lola/skin.json         skin definition (meshes/textures are generated, not in git)
  tools/charport/              MSN1 -> MSN2 character converter (UnityPy) + .msnm format
  packs/                       job packs (docs/PACK_FORMAT.md)
  tools/                       build / install / launch / agent / package scripts
  MODDING_PLAN.md              verified recon of the game
  ARCHITECTURE.md              design, save compatibility, extension points
  TEST_RESULTS.md              in-game test evidence
  TASKS.md                     done / partial / blocked
  CUT_CONTENT.md               cut-content audit (0.4.0)
MAP.md                         full project map: code, runtime paths, tools, release steps
```

## Build
Needs the game installed (assemblies are referenced from its `Managed` folder, never committed), .NET SDK 8+, and BepInEx 5.4.23.5 extracted to `vendor/BepInEx_5.4.23.5` (gitignored).
```powershell
$env:MSN2_GAME_DIR = 'D:\Steam\steamapps\common\My sexy Neighbour 2'   # only if not S:\SteamLibrary\steamapps\common\My sexy Neighbour 2 (tools/common.ps1 default)
& '.\MSN2 Expansion 0.5.0\tools\build.ps1'
& '.\MSN2 Expansion 0.5.0\tools\package.ps1' -Version 0.5.0
```
`tools/relaunch.ps1` + `tools/agent.ps1` drive the running game for tests (enable `[Debug] AgentBridge` in the patch config).

## Licence
MIT (this repository). BepInEx is MIT and bundled unmodified in the patch archive. My Sexy Neighbour 2 and My Sexy Neighbor belong to their developers. Nothing from either game is in this repository; the Characters release archive contains Lola's meshes and textures converted from My Sexy Neighbor.
