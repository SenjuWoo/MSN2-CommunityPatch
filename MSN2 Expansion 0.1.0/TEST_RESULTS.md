# Test results

Rig: Ryzen 7 9800X3D, RTX 4080 SUPER, 2560x1440 280 Hz, Windows 11, game build 21130045, BepInEx 5.4.23.5.
Method: `tools/relaunch.ps1` (install → launch → Continue → wait for Game scene + settle) then `tools/agent.ps1` commands. Same save, same spawn point (player house).

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
