# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.1.0` (see CURRENT.txt). Updated 2026-10-11.

## Done (runtime-verified)
- BepInEx 5.4.23.5 installed in game; plugin `MSN2CommunityPatch.dll` loads (Unity 6000.2.2 Mono).
- Dev loop: `tools/relaunch.ps1 [-Config @{Key='value'}]` → game in Game scene; `tools/agent.ps1 '<cmd>'` for state/perf/screenshots.
- Fixes: HouseHud exception spam (+23% FPS), 60-FPS cap on Medium/Low, interrupt-timeout guard (applied, not triggered).
- Recon + review triage: MODDING_PLAN.md. Measurements: TEST_RESULTS.md.

## Next
1. Expansion framework plugin (separate DLL): sidecar save per slot, data-driven missions (JSON packs), objective hooks on existing game events (photo, hidden cam, theft, trap, sex, fap), rewards (money/reputation/items), presentation via PC letters + phone notifications.
2. First playable slice: an MSN1-style story mission chain with full start → objectives → reward → save/load cycle.
3. More review bugs: door fling, NPC teleport, gallery, key remapping.

## Dev notes
- Dev config in game: BepInEx/config/senjuwoo.msn2.communitypatch.cfg has AgentBridge=true, VerboseLog=true (release defaults false).
- Agent `tp` does not snap to NavMesh yet (it teleported the player into a wall once). Pick floor points (trap positions work).
- Each launch backs up saves to backups/saves/<timestamp>.
- Python: use `py` only if installed; WindowsApps `python3` alias hangs. Node is available.
