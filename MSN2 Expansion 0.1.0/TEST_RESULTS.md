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
| House room label when house unlocked | NOT TESTED | Day-1 save has no unlocked house |
| Save/load with plugin | PASS (vanilla path) | `console save` → "Save slot 0 saved"; reload via Continue works |

Not checked: low-end hardware, Steam Deck, D3D12, long sessions (>15 min), every NPC FSM for interrupt handling.
