# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.2.2` (CURRENT.txt, released as v0.2.2). Updated 2026-11-19. Project map: MAP.md. Last plan: PLAN.md (0.2.2, all tasks closed).

**NEXT ACTION: wait for the owner.** 0.2.2 is the stable base. Open: owner's pick for the next features (0.3.0 MSN1 systems per TASKS.md "Next"), the Steam threads newer than 2026-10-13 (hub is login-gated: paste them into TASKS.md "Steam triage 2026-11"), the arrest design question (job progress kept, vanilla resets entities), owner hands-on of Settings > Controls.

## Recovery 2026-11-19 (third: local workspace lost)
- Found: the owner folder held only the two extracted v0.2.1 release zips (sha256-identical to the GitHub assets). git checkout, `research/`, `vendor/`, `backups/`, `dist/` were gone; the game folder was vanilla (files re-downloaded 2026-10-27, buildid still 21130045, no BepInEx).
- Done by the planner: `git init` + fetch + `checkout -B main origin/main` in place (main @ f722dfe). Baseline: packs validator PASS, build of both plugins PASS (0 warnings), CI on f722dfe green, alerts 0/0/0. Runtime smoke NOT RUN (PLAN.md T03).
- Lost for good: decompiled code (regenerate, T02), dev config backups, save backups, RECOVERED_BRIEF.md text (scope survives in "Scope" below).

## Scope (recovered 2026-10-12 after chat loss; original brief in research/RECOVERED_BRIEF.md)
Goal: a huge expansion for My Sexy Neighbour 2. Phase 1 = official patch for performance + bugs (Community Patch). Phase 2 = bring missing My Sexy Neighbor 1 content to MSN2 (Expansion). Runtime-only BepInEx/Harmony; no game files edited; no Unity Editor/MCP (editor 2022.3 ≠ game 6000.2.2).
- Phase 1: 0.1.0 shipped perf + freeze fixes. 0.2.0 (released 2026-10-13) adds the wardrobe fling fix, unreachable theft spots, key remapping. Not reproduced / not a bug: NPC schedule teleports, gallery sex scenes (see MODDING_PLAN.md).
- Phase 2: 0.1.0 shipped job framework + Neighbourhood Classics. Next (owner's pick, in this order): souvenir shelf, telescope, safe cracking, weather/rain (0.3.0); then more packs.
- Resume rule: verify against `git status`, the release assets and `BepInEx/LogOutput.log` before trusting this file.

## Done
- **0.2.2 released 2026-11-19:** https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.2.2 (tag @ 663df4f; validate/packs + CodeQL actions/csharp/javascript green on that SHA; open alerts Dependabot 0, code scanning 0, secret scanning 0; downloaded assets byte-identical to dist/0.2.2: CommunityPatch sha256 37018FFB9D78CF43C22CF0ADF1EC5144B5F5F7C6CD48298BE2C68DE026C3B03F, Expansion 9EC44213AFF98B73A1F68FDD9D0025F4098D94772E4BB7395A9FEFB722115D4D; plugin DLLs E655A4B3... / 6AE21C34..., a fresh clone of 663df4f builds the same bytes). Content: AnimatorCulling default on, SmallObjectShadows default off, cum_on counted once per finish, test gaps closed (TEST_RESULTS.md 0.2.2). Push note: GitHub printed 'Required status check "packs" is expected' (owner bypass on main); the check then passed on that SHA.
- Game folder holds the 0.2.2 release install with quiet defaults (cfg files regenerated: AgentBridge off, VerboseLog off, PlayerLoopTiming off, VSync on, AnimatorCulling on); release smoke without agent: both plugins load 0.2.2, 0 warnings/errors, clean exit. Dev config with the [Performance] keys: backups/devcfg.
- User data: save00.dat byte-identical to backups/saves/20261119-014624-session-start (41D0F527...); settings.dat restored from it; no expansion sidecar left behind.
- **0.2.1 released:** https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.2.1 (main @ 98e1793; CI validate + CodeQL green; alerts 0/0/0; downloaded asset bytes match dist: CommunityPatch sha256 9BA3D5FA…CF1F8, Expansion 9B051B1C…CDB3). Fixes: journal font after reload, key blocking while typing/paused, HUD off caught screen.
- 0.1.0 released: https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.1.0 (tag @ 09ecaf1, CI validate + CodeQL green). Frozen copy: `MSN2 Expansion 0.1.0`.
- **0.2.0 released:** https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.2.0 (main @ 18b2ebd; CI validate + CodeQL green; open alerts 0/0/0; downloaded asset bytes match dist: CommunityPatch sha256 0DE569ED…8E30C, Expansion 1ECD5041…D35CF). `gh` works as SenjuWoo (`gh api user -q .login`); keyring label "ShugokiFable" is a label only.
- 0.2.0 runtime-tested (TEST_RESULTS.md). Release smoke test (no agent, quiet config): both plugins load 0.2.0, no warnings, game closes cleanly.
- Game folder holds the 0.2.0 release install with quiet defaults (AgentBridge off, VerboseLog off). Dev config backup: backups/devcfg (restore it, or set `AgentBridge = true`, before using tools/agent.ps1).
- User data untouched: save00.dat byte-identical to the original; settings.dat restored to its session-start state (backups/saves/20261012-204323).

## Next
- Owner decides the next scope (0.3.0 MSN1 systems in the owner's order: souvenir shelf, telescope, safe cracking, weather/rain; then more packs).
- Steam triage: paste threads newer than 2026-10-13 (hub needs login).
- Not measurable here: low-end GPU / Steam Deck / D3D12 (SmallObjectShadows might help GPU-bound PCs), physical gamepad, non-English layouts, owner hands-on of Controls.
- Arrest keeps job progress while vanilla resets entities (cams/items): owner's call.

## Dev notes
- Dev config in the game folder is quiet by default now; `relaunch.ps1 -Config @{ AgentBridge='true'; VerboseLog='true' }` switches it on for a session (edits every senjuwoo.msn2.*.cfg that has the key).
- `tools/focus.ps1` brings the game window to the foreground; injected keys (`agent key`, `hold`, `actiontest`, `jumpkey`) are ignored while the game is unfocused.
- Agent (0.2.2 adds `perf [s]`, `perfscan [size]`, `cfg <Section> <Key> [value]`, `offscreen <oc|hide> <i>`, `pad <Map/Action> <control>`, `sex <Actor>`, `fap <Actor> [dist] [pitch] [capsule]`, `calm [Actor]`, `addhours <n>`, `cam <House> [Room]`): `mx`, `snap <Actor>`, `steal`, `cam`, `key <Key>`, `npcs`, `traps`, `tp`, `fps`, `loop`, `profile`, `screenshot`, `ui`, `click <label>`, `settings` (open Settings), `texts <exact text>`, `rects <path suffix>`, `bindings`, `actiontest <Map/Action> <Key..>`, `jumpkey <Key>`, `looking`, `doors`, `doorprobe <i> <closeon|open1|open2|oc_open|oc_closeon>`, `hideprobe [i]`, `lag <ms>`, `stealaudit [i]`, `spotaudit [House] [ids]`, `opennear <i> [r] [name]`, `faceitem <i> [any]`, `facepos x y z`, `nearby <i>`, `freespots`, `warplog start|dump|clear`, `noise x y z [vol]`, `hold <Key> <s>`.
- Game time: 1 game minute = 1 real second at timescale 1, but `GameTimeController` multiplies by Time.timeScale twice, so `console timescale N` runs the clock at N² speed.
- Test runs read the user's save00 slot but never save it; launch.ps1 still backs up `*.dat` to backups/saves/<timestamp>. Restore settings.dat from the first backup of a session when done.
- Computer-use access to the game was declined by the user; use the agent for input and screenshots.
- WindowsApps `python3` alias hangs; use node.

## Recovery 2026-10-13 (second chat loss)
- Cause: BleachBit wiped Claude transcripts. They live in `%USERPROFILE%\.claude\projects\<project>\<id>.jsonl` (+ Desktop metadata in `%APPDATA%\Claude\claude-code-sessions`), not in temp. Whitelist `.claude` and `.codex` in BleachBit.
- Codex backup thread 01a13620 (`.codex\sessions\2026\10\13\rollout-...01a13620...jsonl`) ends at the v0.2.0 release; repo is newer (docs commits 86a7d65, 71d804f). No code lost. Lost: the post-release Steam bug triage conversation.
- Steam hub threads (3684710 / 569291110057293585, 594069606514129316) are login-gated; fetch needs the owner's pasted text.
