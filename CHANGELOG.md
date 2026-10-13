# Changelog

## 0.2.1 — unreleased
AI: Claude Code, Sonnet 5.5. Parent: 0.2.0. Goal: bug-fix pass.
- Fix (Expansion): journal/tracker text invisible (backdrop only) after reloading a save, e.g. arrest -> Load. Cause: the game destroys its TMP font on scene unload; the HUD kept the dead reference. Reproduced and verified in game (Exit -> Continue).
- Fix (Expansion): J/Enter/comma/period fired while typing in game text boxes (PC channel name, password, debt amount) and while the pause menu was open (rebinding a key to one of them toggled the journal)
- Fix (Expansion): job HUD drew over the game-over (caught) screen; journal stays open over the pause menu
- New: dev probes `mx hud`, `mx arrest` (real caught-by-cop game over), `rebind`
- Checked, no bug: arrest -> Continue keeps sidecar progress and re-attaches; controls hint scan (6/6 found in 3.5 ms once)

## 0.2.0 — 2026-10-12
AI: Claude Code, Sonnet 5.5 (max reasoning). Parent: 0.1.0. Goal: finish the official patch.
- Fix: wardrobe hide/exit threw the player across the map at low frame rates (two competing tweens, one aimed at world origin); max jump per frame 11 m -> 0.8 m at ~3 FPS
- Fix: 12 of 148 theft spawn points are inside furniture colliders and can never be reached; taken out of rotation (vanilla tasks and Expansion souvenirs)
- New: Settings > Controls is rebindable (main menu and pause menu), conflict swap, Move/Abilities wizards, selfie-camera row, Reset all keys, HUD key hints follow the bindings
- New: dev probes (door/hideprobe, spot audits, warp log, lag emulation, binding tests) behind the agent bridge
- Investigated, no change: NPC schedule teleports (not reproduced), gallery sex scenes (feature does not exist)
- Expansion: version bump only

## 0.1.0 — 2026-10-12
AI: Claude Code, Opus 5.5. Parent: none. Goal: foundation, Community Patch, Expansion framework + first pack.
- BepInEx 5.4.23.5 setup, build/install/launch/agent/package scripts, CI pack validation.
- Patch: per-frame HUD exception fix (+23% FPS measured), portraits fix, 60-FPS cap fix for Medium/Low, NPC interrupt timeout, Low preset light cap, graphics options.
- Expansion: JSON job packs validated against game enums, sidecar saves (vanilla save untouched), journal/tracker/dialogue UI in the game's font, notifications, rewards, chained jobs, turn-in, souvenir item spawns.
- Pack: Neighbourhood Classics (5 jobs).
