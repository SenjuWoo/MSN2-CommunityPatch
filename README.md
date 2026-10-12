# MSN2 Community Patch + Expansion

Runtime mods for **My Sexy Neighbour 2** (Steam, Unity 6000.2.2 Mono) built on BepInEx 5.4.23.5 + Harmony. No game files are modified.

| Mod | What it does |
|---|---|
| **MSN2 Community Patch** | Fixes the per-frame HUD exception (+23% FPS measured), the 60 FPS cap on Medium/Low, NPCs freezing on ignored interrupts, the Low preset light budget; adds graphics options. |
| **MSN2 Expansion** | MSN1-style story jobs: journal, objective tracker, dialogue, main goals + Souvenir / Hidden-Cam / Shadow / Timed bonuses, chained jobs, JSON job packs, saves beside the game save. Ships the 5-job *Neighbourhood Classics* pack. |

Players: download the zips from [Releases](../../releases) and extract into the game folder. In-zip readmes cover keys, settings and logs (`BepInEx\LogOutput.log`).

## Repository layout
```
CURRENT.txt                    active snapshot folder
MSN2 Expansion 0.1.0/
  src/MSN2CommunityPatch       patch plugin
  src/MSN2Expansion            expansion framework plugin
  packs/                       job packs (docs/PACK_FORMAT.md)
  tools/                       build / install / launch / agent / package scripts
  MODDING_PLAN.md              verified recon of the game
  ARCHITECTURE.md              design, save compatibility, extension points
  TEST_RESULTS.md              in-game test evidence
  TASKS.md                     done / partial / blocked
```

## Build
Needs the game installed (assemblies are referenced from its `Managed` folder, never committed), .NET SDK 8+, and BepInEx 5.4.23.5 extracted to `vendor/BepInEx_5.4.23.5` (gitignored).
```powershell
$env:MSN2_GAME_DIR = 'D:\Steam\steamapps\common\My sexy Neighbour 2'   # if not the default path
& '.\MSN2 Expansion 0.1.0\tools\build.ps1'
& '.\MSN2 Expansion 0.1.0\tools\package.ps1' -Version 0.1.0
```
`tools/relaunch.ps1` + `tools/agent.ps1` drive the running game for tests (enable `[Debug] AgentBridge` in the patch config).

## Licence
MIT (this repository). BepInEx is MIT and bundled unmodified in the patch archive. My Sexy Neighbour 2 belongs to its developers; nothing from the game is included here.
