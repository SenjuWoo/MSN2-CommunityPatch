# Test results

Rig: Ryzen 7 9800X3D, RTX 4080 SUPER, 2560x1440 280 Hz, Windows 11, game build 21130045, BepInEx 5.4.23.5.
Method: `tools/relaunch.ps1` (install → launch → Continue → wait for Game scene + settle) then `tools/agent.ps1` commands. Same save, same spawn point (player house).

## 0.2.0 — 2026-10-12
Evidence level for everything below: **runtime-evidenced** (observed in the running game through the plugin, input injected through the Input System event queue) unless marked tool-validated. Not user-confirmed yet. Low-FPS conditions are emulated with `lag <ms>` (a Thread.Sleep per frame): 100 ms ≈ 9 FPS, 300 ms ≈ 3 FPS.

### Wardrobe fling (hide in / leave a wardrobe, `PlayerAnimationsController.StepInsideAndRotate`)
Largest per-frame jump of the player (m), wardrobes #8 / #2, `hideprobe`:
| frame lag | vanilla enter | vanilla exit | fixed enter | fixed exit |
|---|---|---|---|---|
| none (~260 FPS) | 0.05 / 0.03 | 0.06 / 0.18 | 0.03 / 0.02 | 0.06 / 0.18 |
| 100 ms | 4.40 / 3.53 | 1.89 / 2.04 | 0.55 / 0.56 | 0.31 / 0.31 |
| 300 ms | 11.20 / 7.46 | 4.21 / 2.23 | 0.80 / 0.97 | 0.77 / 0.81 |

Final position with the fix equals the no-lag result at every lag; vanilla at 300 ms ended 1.3-3.2 m off target. Plain doors (`doorprobe`: 6 DoorEntity doors x closing-on-player / open from either side, 0 and 100 ms; 6 cabinet doors at 0 and 120 ms): no fling, worst case 0.57 m (garage door closing on the player, drawer door at 120 ms).

### Theft spawn points (`spotaudit`, tool-validated)
148 points (Chloe 47, Emma 60, Camila 41). The player's own interaction ray (`ObjectsInteractor`: camera ray, 2.0 m, layer mask 131137) against a hair-tie-sized trigger on the Interactable layer riding with the point, from 8 radii x 24 angles of free standing/crouching positions (capsule fits, ground below), containers opened with the player's own interaction. 93 reachable as-is, 43 reachable once the blocking drawer/door is opened (spot-checked: the item rides with its drawer), **12 unreachable** (Emma 8, Camila 3, Chloe 1). After the fix: `freespots` total 148, marked 12, unreachable still free 0.

### Key remapping
| Test | Result |
|---|---|
| Settings > Controls upgraded in the pause menu path and the main menu (same SettingsView) | PASS, screenshots `controls_page`, `menu_controls` |
| Click Interact, press G: binding saved to `senjuwoo.msn2.bindings.json`; `Interact` action fires on G, not on E | PASS |
| Restart: saved bindings loaded, G still fires, E does not | PASS |
| HUD hint for Interact reads G | PASS (`texts G`) |
| Jump -> J: Space no longer jumps, J jumps (0.54 m rise) | PASS |
| Crouch -> J while Jump holds J: Jump swaps to Ctrl | PASS |
| Move wizard Y H N M: holding Y moves the player, holding W does not, arrow keys untouched | PASS |
| Reset all keys: defaults back, file removed, page text back to vanilla | PASS |
| Rebind in the main menu (detached GameInputs), Continue: real GameInputs has the binding | PASS |

Not tested: gamepad, other languages/layouts, physically typed keys (events were injected), 4:3 or small windows.

### Other
- NPC schedule teleports: warp log over 5 accelerated days (`warplog`): 18 warps, all Chloe's webcam FSM (2.4 m hop onto/off the bed); a forced schedule switch (`settime 11`) and a trap recall produced no warps (NPCs walked). Not reproduced, no change made.
- Gallery: no sex-scene gallery exists in MSN2 (static photos are `Common` and `CrossPromo` only); not a patchable bug.
- Regression: 10 s at the usual spot: 258.6 FPS avg (VSync 280 Hz), p99 7.1 ms, 1% low 140 FPS, 0 hitches over 50 ms, no errors or warnings in LogOutput.log; pack validator OK (1 pack, 5 jobs).

## 0.1.0 — 2026-10-11
| Test | Result | Evidence |
|---|---|---|
| BepInEx loads under Unity 6000.2.2 Mono | PASS | LogOutput.log: chainloader loads plugin; agent `ping` replies |
| Agent bridge (ping/stats/fps/profile/loop/screenshot/click/console/tp/npcs/traps) | PASS | JSON replies in BepInEx/MSN2/agent/out |
| HouseHud fix: exception spam | PASS | vanilla 4,725 exceptions in load+30 s (6,274 after +10 s); fixed 0 |
| HouseHud fix: FPS, High preset, fixed spot, 10 s | PASS | vanilla 151 avg / p99 10.7 ms / 1% low 93 → fixed 186 / 7.1 ms / 133 |
| Frame-rate cap fix | PASS | before: High 156, Medium 60, Low 60 fps. After: High 186, Medium 165, Low 278 (VSync 280 Hz) |
| Graphics settings applied to URP assets | PASS (applied) | `[gfx]` log line shows resolved values; visual A/B of each option not yet done |
| Interrupt timeout guard | APPLIED, not triggered | Harmony patch active; normal bucket-trap cycle on Chloe (toilet FSM → trapped → released, schedule resumed) unaffected |
| House room label (vanilla panel) | PASS | screenshots show "Chloe's House / Bathroom" and "Living Room" with 0 exceptions |
| Save/load with plugin | PASS (vanilla path) | `console save` → "Save slot 0 saved"; reload via Continue works |

Not checked: low-end hardware, Steam Deck, D3D12, long sessions (>15 min), every NPC FSM for interrupt handling.

## Expansion 0.1.0 — 2026-10-12
Input for UI tests is injected through the Unity Input System event queue (`agent key <Key>`), the same path as a physical keyboard. Objective tests drive the game's own code: `PhoneCameraController.MakeShot`, `StealableEntity.InteractAsync`, `HiddenCameraEntity.CompleteInstallation`, `TrapEntity` trigger path.

| Test | Result | Evidence |
|---|---|---|
| Pack loads, 0 errors; broken pack rejected with file/field message | PASS | log `[packs]`; duplicate-id case caught; CI validator rejects actor typo |
| Unlock → game notification "New job from The Collector" | PASS | screenshot (game NotificationsView) |
| Journal (J), accept (Enter), dialogue box, tracker | PASS | screenshots `mx_journal`, `mx_dialogue` |
| Photo objective (actor + house) via real phone shot | PASS | progress 0→1 |
| Hidden-camera bonus via install completion | PASS | `HC_Chloe_Webcam_1`, room ChloesHouse_WebRoom |
| Shadow bonus fails when NPC reaches Alert | PASS | `shadow:FAILED` after being seen |
| Turn-in step (main done, bonuses left) via J + Enter | PASS | notification + tracker "Ready to turn in"; status Completed |
| Rewards paid | PASS | HUD money $0 → $300; later +$625 |
| Activity + naked photo (Chloe in shower 11-12) | PASS | shower + naked done from one shot, bonus 3/3 |
| Souvenir spawn on theft spot, saved by vanilla, re-adopted after reload, removed when stolen | PASS | log `[souvenir] placed`; 0 placements after reload; scene count 1→0 after steal |
| Sidecar persistence across 3 restarts | PASS | `save00.msn2x.json` contents + `mx` after relaunch |
| Vanilla load with expansion DLL removed | PASS | Game scene reached, souvenir present as normal stealable, 0 exceptions |
| Trap hook | PASS | `[event] trap actor=Chloe trap=Bucket` |
| Interrupt guard does not break trap cycle | PASS | Clothes FSM → trapped → released → schedule resumed |
| sex / cum_on hooks | NOT TESTED | |
| Jobs 3-5 end to end | NOT TESTED | rooms/activities verified individually (Camila yoga = Exercise in gym) |

## 0.2.1
- Journal text after reload (runtime): before fix, Exit -> Continue gave fontAlive=false, bodyFont=null, screenshot = backdrop only (matches the report "no text just the backdrop after getting arrested"). After fix: log `[hud] font re-resolved`, fontAlive=true, journal fully rendered (screenshot). Arrest itself was not triggered; the arrest "Load" button uses the same scene reload (GameOverView -> LoadSceneWithLoaderAsync<GameInitState>).
