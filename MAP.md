# MAP — where everything is (read this before touching code)

Owner folder for MSN2 Community Patch + MSN2 Expansion. Active snapshot: `CURRENT.txt`. Released: 0.1.0, 0.2.0. Older snapshots are frozen rollback copies, never edit them.

## Folders
| Path | Tracked | What |
|---|---|---|
| `MSN2 Expansion 0.2.0/` | yes | active code, tools, docs, packs |
| `MSN2 Expansion 0.1.0/` | yes | frozen copy of the released 0.1.0 |
| `.github/` (root) | yes | CI (`validate.yml`), Dependabot |
| `WORKSPACE_OWNERSHIP.md`, `LICENSE`, `.gitattributes` (root) | yes | ownership rules, MIT licence, CRLF for .cs/.ps1 |
| `research/` | no (gitignored) | decompiled game code, audits, review dumps, RECOVERED_BRIEF.md |
| `vendor/` | no | BepInEx 5.4.23.5 (unzipped + zip) |
| `dist/<version>/` | no | release zips to upload |
| `backups/` | no | `devcfg` dev configs, `saves` automatic save backups, `test-end-state` |

Game folder (only written by `tools/install.ps1`, removed by `uninstall.ps1`): `BepInEx/plugins/MSN2/`, plus `BepInEx/MSN2/agent/` for the test bridge.

## Code (in `MSN2 Expansion 0.2.0/src/`)
**MSN2CommunityPatch** (no game files, Harmony + BepInEx)
- `Plugin.cs`: entry, config binds, applies each fix in isolation (`TryApply`).
- `Fixes/HouseHudFix.cs`: house HUD null exception + portraits + zone handlers.
- `Fixes/InterruptTimeoutFix.cs`: NPC interrupt timeout (8 s default).
- `Fixes/WardrobeTweenFix.cs`: wardrobe hide/exit fling (`PlayerAnimationsController.StepInsideAndRotate`).
- `Fixes/TheftSpotFix.cs`: marks 12 unreachable `TheftMapPoint`s with an empty child (vanilla and Expansion skip points with children).
- `Controls/KeyBindings.cs`: rebinding core, saved to `BepInEx/config/senjuwoo.msn2.bindings.json`.
- `Controls/ControlsPage.cs`: Settings > Controls UI rows + HUD hint sync.
- `Graphics/GraphicsTuner.cs`: VSync/FPS cap every frame, URP quality overrides, graphics options.
- Diagnostics (off by default): `FrameMonitor`, `PlayerLoopTimer`, `ScriptProfiler`, `SceneStats`, `MarkerRecorder`.
- Test bridge (off by default): `AgentBridge.cs` (file channel), `AgentWorld`, `AgentAI`, `AgentProbes` (probes for the 0.2.0 fixes).

**MSN2Expansion** (job framework)
- `ExpansionPlugin.cs`: entry, config (keys J/Enter/,/., tracker toggle, VerboseLog).
- `PackLoader.cs`: reads `packs/<id>/pack.json`, validates against game enums; `Model.cs` = data types.
- `MissionRunner.cs`: state machine Locked → Available → Active → Completed, rewards, chaining, turn-in.
- `GameHooks.cs`: subscribes to the game's own event sources (photo, steal, trap, hidden cam, zone, alert, hour). Add a new objective source here and in `PackLoader.ObjectiveTypes`.
- `Sidecar.cs`: job progress in `saveNN.msn2x.json` beside the vanilla save (atomic write, schema field, `.bad` on unreadable).
- `Souvenirs.cs`: places stealable souvenirs on free theft points (`childCount == 0`). Without the patch, it can pick the 12 bad spots.
- `ExpansionHud.cs`: journal, tracker, dialogue, notifications (UGUI in the game's TMP font).
- `AgentCommands.cs`: test commands for the bridge.

Packs: `packs/neighbourhood-classics/pack.json` (5 jobs). Format: `docs/PACK_FORMAT.md`.

## Runtime paths (player machine)
- Logs: `<game>\BepInEx\LogOutput.log` (first stop for every bug report).
- Patch config: `BepInEx\config\senjuwoo.msn2.communitypatch.cfg`; expansion config: `senjuwoo.msn2.expansion.cfg` (BepInEx names by plugin GUID).
- Bindings: `BepInEx\config\senjuwoo.msn2.bindings.json` (delete to reset).
- Saves: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My Sexy Neighbour 2\` (`save00.dat` vanilla, `save00.msn2x.json` expansion sidecar). The vanilla save is never written by the expansion.
- Agent channel (dev only): `BepInEx\MSN2\agent\cmd.txt` in, `out\<id>.json` out.

## Tools (`MSN2 Expansion 0.2.0/tools/`)
Set `MSN2_GAME_DIR` if the game is not at the default in `common.ps1` (`S:\SteamLibrary\steamapps\common\My sexy Neighbour 2`).
- `build.ps1 [-Configuration Release]`: builds both plugins against the game's `Managed` DLLs.
- `package.ps1 -Version X.Y.Z`: build, then writes `dist/X.Y.Z/` (two zips, payload only).
- `install.ps1` / `uninstall.ps1` / `launch.ps1` / `relaunch.ps1 [-Config @{...}]`: game folder install and launch (launch backs up `*.dat`).
- `agent.ps1 <command>`: test bridge (commands list in STATE.md). `focus.ps1`: needed before injected keys.
- `validate-packs.mjs <packs dir>`: pack validator, run in CI (`node` only; WindowsApps `python3` hangs).

## Build and release
1. Edit the active snapshot. Bump version in both csproj and `VERSION.md`.
2. `build.ps1`, then in-game test (`relaunch.ps1` + `agent.ps1`); record results in `TEST_RESULTS.md`.
3. `package.ps1 -Version X.Y.Z`. Check zip listing: payload only (BepInEx core, plugin DLLs, docs, packs).
4. Update `CURRENT.txt`, root `CHANGELOG.md`, `docs/release/CHANGELOG.txt`.
5. Commit, push `main`, wait for CI on that exact SHA (validate + CodeQL).
6. `gh release create vX.Y.Z --target <sha>` with both zips. Notes go in a file, not process substitution (fails on Windows).
7. Download assets, compare SHA256 with `dist/`. Check Dependabot, code-scanning and secret-scanning alerts (0 required). A failed query = unknown.
8. Changelog for Nexus/LoversLab/Undertow: copy the two top blocks of `docs/release/CHANGELOG.txt`.
`gh` works as SenjuWoo (`gh api user -q .login`). The keyring label "ShugokiFable" is only a label.

## Known non-bugs / dead ends (do not re-investigate without new evidence)
- NPC schedule teleports: not reproduced over 5 accelerated days (only Chloe's webcam FSM hops). Use `warplog` with a save that shows it.
- Gallery sex scenes: the feature does not exist in MSN2.
- `sex` and `cum_on` hooks: subscribed, never triggered in a test; no shipped job uses them.
- Game time: `GameTimeController` multiplies by timeScale twice (timescale N = N² speed).
- Injected keys ignored while the game window is unfocused (use `focus.ps1`).
- Computer-use control of the game was declined by the owner; use the agent bridge.

## Evidence
- `TEST_RESULTS.md` (per version, with method and evidence level). Current evidence is runtime-evidenced, not owner-confirmed.
- `TASKS.md`: done / partial / investigated / next.
- `MODDING_PLAN.md`: game recon (types, build 21130045, verified facts).
- `ARCHITECTURE.md`: design, save compatibility, extension points.
