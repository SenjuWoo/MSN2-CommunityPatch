# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.1.0` (CURRENT.txt). Updated 2026-10-12.

## Done
- Patch + Expansion 0.1.0 runtime-tested: see `MSN2 Expansion 0.1.0/TEST_RESULTS.md`, open items in `TASKS.md`.
- Released v0.1.0: https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.1.0 (tag @ 09ecaf1, CI validate + CodeQL green, 0 open alerts). Assets sha256: CommunityPatch 67a380d3…68e5 (694,972 B), Expansion 6438bce3…d876 (53,831 B); downloaded bytes verified identical.
- Game folder now holds the release install (quiet defaults, AgentBridge off). Dev config backup: backups/devcfg (restore it or set AgentBridge=true before using tools/agent.ps1).
- User save restored byte-exact to the pre-test original; test end-state kept in backups/test-end-state.
- Snapshot 0.1.0 is now the released, frozen copy: start the next change by copying it to `MSN2 Expansion 0.1.1` (or 0.2.0) and updating CURRENT.txt.

## Next
See TASKS.md "Next": review bugs (door fling, NPC teleport, drawers, gallery, rebinding), MSN1 systems (telescope, safe, souvenir shelf, weather, stats), more packs.

## Dev notes
- Dev config in the game folder has AgentBridge=true and VerboseLog=true (release defaults false).
- `relaunch.ps1 -Config @{Key='value'}` edits every senjuwoo.msn2.*.cfg that has the key.
- Agent: `mx`, `mx accept|turnin|reset|audit|openhouse <House>`, `snap <Actor>`, `steal [Item] [House]`, `cam [House]`, `key <Key>`, `npcs`, `traps`, `tp`, `fps`, `loop`, `profile`, `screenshot`.
- Test runs modify save00 (backups in backups/saves/; the user's pre-test save is backups/saves/20261011-102329).
- Computer-use access to the game was declined by the user; use `agent key` for input.
- WindowsApps `python3` alias hangs; use node.
