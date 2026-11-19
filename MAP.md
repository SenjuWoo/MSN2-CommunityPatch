# MAP — where everything is (read this before touching code)

Owner folder for MSN2 Community Patch + MSN2 Expansion. Active snapshot: `CURRENT.txt` (0.4.0). Released: 0.1.0, 0.2.0, 0.2.1, 0.2.2, 0.3.0, 0.4.0. Older snapshots are frozen rollback copies, never edit them.

## Folders
| Path | Tracked | What |
|---|---|---|
| `MSN2 Expansion 0.4.0/` | yes | active code, tools, docs, packs, `CUT_CONTENT.md`, `skins/<id>/skin.json` (generated skin data is gitignored) |
| `MSN2 Expansion 0.3.0/`, `0.2.2/`, `0.2.1/`, `0.2.0/`, `0.1.0/` | yes | frozen copies of the released versions (0.2.2 also holds the archived `PLAN-0.2.2.md`) |
| `PLAN.md` (root) | yes | current plan (0.3.0-0.5.0) |
| `.github/` (root) | yes | CI (`validate.yml`), Dependabot |
| `WORKSPACE_OWNERSHIP.md`, `LICENSE`, `.gitattributes` (root) | yes | ownership rules, MIT licence, CRLF for .cs/.ps1 |
| `research/` | no (gitignored) | decompiled game code, audits, review dumps, RECOVERED_BRIEF.md |
| `vendor/` | no | BepInEx 5.4.23.5 (unzipped + zip) |
| `dist/<version>/` | no | release zips to upload |
| `backups/` | no | `devcfg` dev configs, `saves` automatic save backups, `evidence/<ver>` test screenshots, `test-end-state` |

Game folder (only written by `tools/install.ps1`, removed by `uninstall.ps1`): `BepInEx/plugins/MSN2/`, plus `BepInEx/MSN2/agent/` for the test bridge.

## Code (in `MSN2 Expansion 0.4.0/src/`)
**MSN2CommunityPatch** (no game files, Harmony + BepInEx)
- `Plugin.cs`: entry, config binds, applies each fix in isolation (`TryApply`).
- `Fixes/HouseHudFix.cs`: house HUD null exception + portraits + zone handlers.
- `Fixes/InterruptTimeoutFix.cs`: NPC interrupt timeout (8 s default).
- `Fixes/WardrobeTweenFix.cs`: wardrobe hide/exit fling (`PlayerAnimationsController.StepInsideAndRotate`).
- `Fixes/TheftSpotFix.cs`: marks 12 unreachable `TheftMapPoint`s with an empty child (vanilla and Expansion skip points with children).
- `Controls/KeyBindings.cs`: rebinding core, saved to `BepInEx/config/senjuwoo.msn2.bindings.json`.
- `Controls/ControlsPage.cs`: Settings > Controls UI rows + HUD hint sync.
- `Graphics/GraphicsTuner.cs`: VSync/FPS cap every frame, URP quality overrides, graphics options.
- `Graphics/PerfTweaks.cs`: `[Performance]` AnimatorCulling (prop animators -> CullUpdateTransforms; characters, cameras, UI, NavMeshObstacle doors excluded) and SmallObjectShadows; applied once per Game-scene load from `SceneRuntime`, reverted exactly when switched off.
- Diagnostics (off by default): `FrameMonitor`, `PlayerLoopTimer`, `ScriptProfiler`, `SceneStats`, `MarkerRecorder`.
- Test bridge (off by default): `AgentBridge.cs` (file channel), `AgentWorld`, `AgentAI`, `AgentProbes` (probes for the 0.2.0 fixes; `perf`, `perfscan`, `cfg`, `offscreen`, `pad` since 0.2.2).

**MSN2Expansion** (job framework)
- `ExpansionPlugin.cs`: entry, config (keys J/Enter/,/., tracker toggle, VerboseLog).
- `PackLoader.cs`: reads `packs/<id>/pack.json`, validates against game enums; `Model.cs` = data types.
- `MissionRunner.cs`: state machine Locked → Available → Active → Completed, rewards, chaining, turn-in.
- `GameHooks.cs`: subscribes to the game's own event sources (photo, steal, trap, hidden cam, zone, alert, hour). Add a new objective source here and in `PackLoader.ObjectiveTypes`.
- `Sidecar.cs`: job progress in `saveNN.msn2x.json` beside the vanilla save (atomic write, schema field, `.bad` on unreadable).
- `Souvenirs.cs`: places stealable souvenirs on free theft points (`childCount == 0`). Without the patch, it can pick the 12 bad spots.
- `ExpansionHud.cs`: journal, tracker, dialogue, notifications (UGUI in the game's TMP font).
- `AgentCommands.cs`: test commands for the bridge (`mx`, `snap`, `steal`, `cam <House> [Room]`, `key`, `sex`, `fap`, `calm`, `addhours`).

- `ActorNames.cs`: shows a skin's display name in job text (reads `MSN2Characters.Skins.SkinApplier.DisplayNames` by reflection).

Packs: `packs/neighbourhood-classics/pack.json` (5 jobs), `packs/house-help/pack.json` (3 jobs: Yuka, Jamal, Peter). Format: `docs/PACK_FORMAT.md`.

**MSN2Characters** (optional; GUID `senjuwoo.msn2.characters`; compiles against the game's own mscorlib because Unity 6 APIs have Span overloads)
- `Plugin.cs`: config `[Skins] Camila = lola`; `CharactersRuntime` loads packs from the main menu on and applies them once the Game scene's `EntitiesController` has the actor.
- `Skins/MsnmReader.cs`: `.msnm` v1 parser (worker thread) + `Mesh` builder. Format spec: `tools/charport/msnm.py`.
- `Skins/SkinPack.cs`: `skin.json` model, loading (one texture/mesh per frame, normal maps `_n.png` linear, Repeat wrap for UDIM tiles), portrait sprites.
- `Skins/SkinApplier.cs`: body swap on the actor's `ActorSkinnedMeshRenderer` (bones by name), extra renderers (hair, scalp) beside it, garments via a postfix on `ClothesController.AttachClothing`, materials cloned from Camila's (`template`), identity (portraits incl. 6 `ConditionalPortraits`, name rewrite in `LocalizationManager.Translate` + one sweep of existing TMP texts), full revert.
- `CutNpcs/Neighbours.cs`: `[Neighbours]`/`[Scenes]`/`[Restored]` switches: resident removal + timetable FSM swaps applied once per Game scene (originals restored first), Yuka bunny suit via a `WearClothingPack` prefix.
- `CutNpcs/CutAudit.cs`: `cutaudit` runtime dump of repositories/timetables/residents (CUT_CONTENT.md source).
- `CharacterAgent.cs`: test commands (also `cutaudit`, `house open|close`) (`skin`, `wear`, `blend`, `look`, `portrait`, `spritedump`, `actor`, `fsm`, `beds`).

Skins: `skins/lola/skin.json` (git) + generated `body.msnm`, `hair.msnm`, `scalp.msnm`, `clothes/*.msnm`, `tex/*.png`, `portrait*.png`, `manifest.json` (release zip only). Rebuild: `vendor/pyenv/Scripts/python.exe -I tools/charport/convert_skin.py --msn1 "<MSN1>/My Sexy Neighbor_Data" --msn2 "<MSN2>/My Sexy Neighbour 2_Data" --out skins/lola` (cache in `skins/lola/.build`), then `portrait Camila` in game and copy `portrait*.png` into the skin folder.

## Runtime paths (player machine)
- Logs: `<game>\BepInEx\LogOutput.log` (first stop for every bug report).
- Patch config: `BepInEx\config\senjuwoo.msn2.communitypatch.cfg`; expansion config: `senjuwoo.msn2.expansion.cfg` (BepInEx names by plugin GUID).
- Bindings: `BepInEx\config\senjuwoo.msn2.bindings.json` (delete to reset).
- Saves: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My Sexy Neighbour 2\` (`save00.dat` vanilla, `save00.msn2x.json` expansion sidecar). The vanilla save is never written by the expansion.
- Agent channel (dev only): `BepInEx\MSN2\agent\cmd.txt` in, `out\<id>.json` out.

## Tools (`<active snapshot>/tools/`)
Set `MSN2_GAME_DIR` if the game is not at the default in `common.ps1` (`S:\SteamLibrary\steamapps\common\My sexy Neighbour 2`).
- `build.ps1 [-Configuration Release]`: builds the three plugins against the game's `Managed` DLLs.
- `package.ps1 -Version X.Y.Z`: build, then writes `dist/X.Y.Z/` (three zips, payload only; Characters zip carries `skins/` without `.build`).
- `charport/convert_skin.py`, `charport/msnm.py`, `charport/bone_map_msn1_to_msn2.json`: offline character converter (Python venv `vendor/pyenv`, UnityPy).
- `install.ps1` / `uninstall.ps1` / `launch.ps1` / `relaunch.ps1 [-Config @{...}]`: game folder install and launch (launch backs up `*.dat`).
- `agent.ps1 <command>`: test bridge (commands list in STATE.md). `focus.ps1`: needed before injected keys.
- `validate-packs.mjs <packs dir>`: pack validator, run in CI (`node` only; WindowsApps `python3` hangs).

## Build and release
1. Edit the active snapshot. Bump version in both csproj and `VERSION.md`.
2. `build.ps1`, then in-game test (`relaunch.ps1` + `agent.ps1`); record results in `TEST_RESULTS.md`.
3. `package.ps1 -Version X.Y.Z`. Check zip listing: payload only (BepInEx core, plugin DLLs, docs, packs).
4. Update `CURRENT.txt`, root `CHANGELOG.md`, `docs/release/CHANGELOG.txt`.
5. Commit, push `main`, wait for CI on that exact SHA (validate + CodeQL).
6. `gh release create vX.Y.Z --target <sha>` with all zips. Notes go in a file, not process substitution (fails on Windows).
7. Download assets, compare SHA256 with `dist/`. Check Dependabot, code-scanning and secret-scanning alerts (0 required). A failed query = unknown.
8. Changelog for Nexus/LoversLab/Undertow: copy the two top blocks of `docs/release/CHANGELOG.txt`.
`gh` works as SenjuWoo (`gh api user -q .login`). The keyring label "ShugokiFable" is only a label.

## Known non-bugs / dead ends (do not re-investigate without new evidence)
- Yuka/Jamal/Peter are not cut: house residents activated by `HouseOpenerController.OpenHouse` (CUT_CONTENT.md). Day-1 test saves keep Camila's/Emma's houses closed: `house open <House>` or `actor <X> activate`.
- NPC schedule teleports: not reproduced over 5 accelerated days (only Chloe's webcam FSM hops). Use `warplog` with a save that shows it.
- Gallery sex scenes: the feature does not exist in MSN2.
- `sex` and `cum_on` hooks: verified in 0.2.2 (agent `sex` / `fap`). An NPC's root capsule, which the game's fap code credits, is off in many FSM states (alert, webcam, bath); that is vanilla.
- Shared PC: other sessions' GPU jobs (AssetForge, Minecraft) skew FPS; check `nvidia-smi` before measuring and A/B inside one session (`cfg` toggles).
- Game time: `GameTimeController` multiplies by timeScale twice (timescale N = N² speed).
- Injected keys ignored while the game window is unfocused (use `focus.ps1`).
- Computer-use control of the game was declined by the owner; use the agent bridge.

## Evidence
- `TEST_RESULTS.md` (per version, with method and evidence level). Current evidence is runtime-evidenced, not owner-confirmed.
- `TASKS.md`: done / partial / investigated / next.
- `MODDING_PLAN.md`: game recon (types, build 21130045, verified facts).
- `ARCHITECTURE.md`: design, save compatibility, extension points.
