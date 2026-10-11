# Changelog

## 0.1.0 — 2026-10-11 (in progress)
AI: Claude Code, Opus 5.5. Parent: none. Goal: foundation + Community Patch.
- BepInEx 5.4.23.5 setup, build/install/launch/agent scripts.
- Agent bridge (file-based, off by default) + profiler, player-loop timer, scene stats.
- Fix: per-frame "CurrentHouseDataSO is null" exception and wrong house portraits (+23% FPS measured).
- Fix: Medium/Low presets capped at 60 FPS; VSync and FPS limit now configurable for every preset.
- Fix (guard): NPC interrupt wait now times out instead of freezing the NPC forever.
- Graphics options: MSAA, render scale + upscaler, shadow distance, lamp shadows + atlas, soft shadows; Low preset light cap.
