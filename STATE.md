# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.1.0` (CURRENT.txt). Updated 2026-10-12.

## Done
- Patch + Expansion 0.1.0 runtime-tested: see `MSN2 Expansion 0.1.0/TEST_RESULTS.md`, open items in `TASKS.md`.
- Release: `tools/package.ps1 -Version 0.1.0` → dist/0.1.0/*.zip. GitHub repo SenjuWoo/MSN2-CommunityPatch.

## Next
See TASKS.md "Next": review bugs (door fling, NPC teleport, drawers, gallery, rebinding), MSN1 systems (telescope, safe, souvenir shelf, weather, stats), more packs.

## Dev notes
- Dev config in the game folder has AgentBridge=true and VerboseLog=true (release defaults false).
- `relaunch.ps1 -Config @{Key='value'}` edits every senjuwoo.msn2.*.cfg that has the key.
- Agent: `mx`, `mx accept|turnin|reset|audit|openhouse <House>`, `snap <Actor>`, `steal [Item] [House]`, `cam [House]`, `key <Key>`, `npcs`, `traps`, `tp`, `fps`, `loop`, `profile`, `screenshot`.
- Test runs modify save00 (backups in backups/saves/; the user's pre-test save is backups/saves/20261011-102329).
- Computer-use access to the game was declined by the user; use `agent key` for input.
- WindowsApps `python3` alias hangs; use node.
