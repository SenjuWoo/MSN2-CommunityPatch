# Architecture

Two independent BepInEx 5 plugins (Mono, net472, Harmony 2 via BepInEx). Nothing in the game folder or its bundles is modified.

```
BepInEx/plugins/MSN2/
  MSN2CommunityPatch.dll   performance + bug fixes, graphics options, dev diagnostics (off by default)
  MSN2Expansion.dll        job framework (soft-depends on the patch only for dev test commands)
  packs/<id>/pack.json     data-driven content
```

## MSN2CommunityPatch
| file | role |
|---|---|
| Plugin.cs | config, applies each fix in isolation (a failing fix logs and leaves vanilla behaviour) |
| Fixes/HouseHudFix.cs | replaces the `ActorsInHouseView` room-label lambda (found by its IL string, not by compiler name) and the zone enter/exit handlers |
| Fixes/InterruptTimeoutFix.cs | `AIFSMController.InterruptGraphAsync` with a realtime timeout |
| Fixes/WardrobeTweenFix.cs | replaces `PlayerAnimationsController.StepInsideAndRotate` (wardrobe hide/exit): vanilla moved the player with a placeholder `DOMove(Vector3.zero)` tween plus a second tween started from its `OnPlay`; at low FPS they fight (4 m/frame at ~9 FPS). One tween per step instead |
| Fixes/TheftSpotFix.cs | 12 `TheftMapPoint`s are inside furniture colliders (probe audit, build 21130045). Each gets an empty child object: vanilla's `TheftChallengeSO.PrepareChallengeForPlay` treats a point with a child as in use, so neither vanilla tasks nor Expansion souvenirs pick it. `SceneRuntime` applies it once per Game scene |
| Controls/KeyBindings.cs | key remapping core: binding overrides on the game's own `GameInputs` actions (`InputController.Inputs`), saved to `config/senjuwoo.msn2.bindings.json`, re-applied by a postfix on the `GameInputs` constructor so every instance gets them; conflict swap; `ToolSelector/Close` mirrors `Player/Tools`; the main menu (no InputController) edits a detached `GameInputs` |
| Controls/ControlsPage.cs | upgrades the static Settings > Controls page in place (clickable rows, Move/Abilities wizards, extra Selfie camera + Reset rows) and keeps the static HUD key hints in sync (`HintSync`) |
| Graphics/GraphicsTuner.cs | enforces VSync/FPS cap every frame (game resets them), applies URP overrides to all quality assets, snapshots originals for "Preset" |
| AgentBridge.cs + Agent*.cs, ScriptProfiler, PlayerLoopTimer, MarkerRecorder, SceneStats, FrameMonitor | dev/test tooling: file command channel `BepInEx/MSN2/agent/cmd.txt` → `out/<id>.json` |
| AgentProbes.cs | dev probes used to find and verify the 0.2.0 fixes: `doorprobe`/`hideprobe` (per-frame player displacement, `lag <ms>` emulates a slow PC), `stealaudit`/`spotaudit` (can the player's interaction ray reach each theft spot), `warplog` (NPC warps), `bindings`/`actiontest`/`jumpkey` (remapping), `looking`, `freespots`, `noise` |

## MSN2Expansion
```
GameDataManager.Load/Save/Reset/Delete ──Harmony postfix──► Sidecar (saveNN.msn2x.json, atomic write)
Game scene ready ──► GameHooks.TryCreate ──► subscribes to the game's own Subjects:
   PhoneCameraController.OnCameraShotAction, SexController.MinigameWin, FapController.OnActorCummed,
   ZonesController.OnZoneEntered, GameTimeController.OnHourChanged, AIAlertController.AlertStatusChanged,
   StealableEntity.OnInteract / TrapEntity.OnInteract (per entity, rescanned every 2 s),
   HiddenCameraEntity.CompleteInstallation (postfix: no public per-camera signal exists)
        └──► GameEvent (type, actor, house, room, activity, item, trap, naked, entityId)
              └──► MissionRunner: Locked → Available → Active → Completed, rewards, souvenir spawns
                     └──► ExpansionHud (UGUI built at runtime with the game's TMP font) + NotificationsController
```

## Save compatibility (verified)
- Vanilla `saveNN.dat` only ever contains vanilla types. Souvenir items are vanilla stealable entities saved by the game.
- Removing the expansion: the save loads, the souvenir stays in the world as a normal stealable, the sidecar is ignored.
- Re-adding it: the sidecar re-adopts the souvenir by entity id; nothing is duplicated.
- Sidecar schema field `Schema` (1) for future migrations; unreadable sidecar → copied to `.bad`, fresh state, vanilla load never blocked.

## Extension points
- New jobs: packs (docs/PACK_FORMAT.md). New objective type: add a `GameEvent` source in GameHooks + the type name in `PackLoader.ObjectiveTypes`.
- Version dependencies: game build 21130045 (v10.12.2025 1229), Unity 6000.2.2f1 Mono, BepInEx 5.4.23.5. Fixes target private members by name; if the game updates, each fix logs `failed to apply` and the game runs vanilla.
