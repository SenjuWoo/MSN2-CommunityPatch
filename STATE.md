# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.2.0` (CURRENT.txt). Updated 2026-10-12.

## Scope (recovered 2026-10-12 after chat loss; original brief in research/RECOVERED_BRIEF.md)
Goal: a huge expansion for My Sexy Neighbour 2. Phase 1 = official patch for performance + bugs (Community Patch). Phase 2 = bring missing My Sexy Neighbor 1 content to MSN2 (Expansion). Runtime-only BepInEx/Harmony; no game files edited; no Unity Editor/MCP (editor 2022.3 ≠ game 6000.2.2).
- Phase 1: 0.1.0 shipped perf + freeze fixes. 0.2.0 (built, tested, not yet published) adds the wardrobe fling fix, unreachable theft spots, key remapping. Not reproduced / not a bug: NPC schedule teleports, gallery sex scenes (see MODDING_PLAN.md).
- Phase 2: 0.1.0 shipped job framework + Neighbourhood Classics. Next (owner's pick, in this order): souvenir shelf, telescope, safe cracking, weather/rain (0.3.0); then more packs.
- Resume rule: verify against `git status`, the release assets and `BepInEx/LogOutput.log` before trusting this file.

## Done
- 0.1.0 released: https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.1.0 (tag @ 09ecaf1, CI validate + CodeQL green). Frozen copy: `MSN2 Expansion 0.1.0`.
- 0.2.0 built from `MSN2 Expansion 0.2.0`, runtime-tested (TEST_RESULTS.md), packaged in `dist/0.2.0` (gitignored): CommunityPatch zip sha256 0DE569ED…8E30C (745,319 B), Expansion zip 1ECD5041…D35CF (54,485 B). DLLs inside match the build output. Release smoke test (no agent, quiet config): both plugins load 0.2.0, no warnings, game closes cleanly.
- Game folder holds the 0.2.0 release install with quiet defaults (AgentBridge off, VerboseLog off). Dev config backup: backups/devcfg (restore it, or set `AgentBridge = true`, before using tools/agent.ps1).
- User data untouched: save00.dat byte-identical to the original; settings.dat restored to its session-start state (backups/saves/20261012-204323).

## Next
- **Publish 0.2.0 (blocked on GitHub identity):** `gh` is logged in as other accounts, not SenjuWoo (hosts.yml unchanged since 2026-08-20), and the GitHub MCP (SenjuWoo) cannot create releases or upload assets. Pushing as another account would put another handle on the public Actions/events. Needs `gh auth login` as SenjuWoo (then: push, wait for CI on that SHA, tag v0.2.0, `gh release create` with both zips, download and compare bytes, check Dependabot/code-scanning/secret-scanning alerts). Local commit exists, nothing pushed.
- 0.3.0: MSN1 systems per TASKS.md "Next".

## Dev notes
- Dev config in the game folder is quiet by default now; `relaunch.ps1 -Config @{ AgentBridge='true'; VerboseLog='true' }` switches it on for a session (edits every senjuwoo.msn2.*.cfg that has the key).
- `tools/focus.ps1` brings the game window to the foreground; injected keys (`agent key`, `hold`, `actiontest`, `jumpkey`) are ignored while the game is unfocused.
- Agent: `mx`, `snap <Actor>`, `steal`, `cam`, `key <Key>`, `npcs`, `traps`, `tp`, `fps`, `loop`, `profile`, `screenshot`, `ui`, `click <label>`, `settings` (open Settings), `texts <exact text>`, `rects <path suffix>`, `bindings`, `actiontest <Map/Action> <Key..>`, `jumpkey <Key>`, `looking`, `doors`, `doorprobe <i> <closeon|open1|open2|oc_open|oc_closeon>`, `hideprobe [i]`, `lag <ms>`, `stealaudit [i]`, `spotaudit [House] [ids]`, `opennear <i> [r] [name]`, `faceitem <i> [any]`, `facepos x y z`, `nearby <i>`, `freespots`, `warplog start|dump|clear`, `noise x y z [vol]`, `hold <Key> <s>`.
- Game time: 1 game minute = 1 real second at timescale 1, but `GameTimeController` multiplies by Time.timeScale twice, so `console timescale N` runs the clock at N² speed.
- Test runs read the user's save00 slot but never save it; launch.ps1 still backs up `*.dat` to backups/saves/<timestamp>. Restore settings.dat from the first backup of a session when done.
- Computer-use access to the game was declined by the user; use the agent for input and screenshots.
- WindowsApps `python3` alias hangs; use node.
