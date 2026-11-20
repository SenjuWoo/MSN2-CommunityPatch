# STATE — MSN2 Community Patch + Expansion

Active snapshot: `MSN2 Expansion 0.5.0` (CURRENT.txt). Updated 2026-11-20. Project map: MAP.md. Plan: PLAN.md (0.3.0 -> 0.5.0; the 0.2.2 plan is archived in `MSN2 Expansion 0.2.2/PLAN-0.2.2.md`).

**NEXT ACTION: next round (owner's call): Lola polish (hair dithering, dark eye area, halve texture VRAM ~250 MB), House Help pack turn-ins/traps/pills, measure the police difficulty multiplier in game.**

## 0.5.0 (2026-11-20)
- Expansion Systems/: sex gallery + replay (G), statistics (K), souvenir altar, telescope, safe cracking, rain, difficulty presets; sidecar schema 2. All verified in game (`MSN2 Expansion 0.5.0/TEST_RESULTS.md`, evidence `backups/evidence/0.5.0/`).
- Fixes from testing: rain indoors (owner report; ceiling raycast + particle collision), rain colour/size, rain audio on the Ambient mixer group, drop material reused, telescope zoom reset, replay vs police.
- Regression: 14-min quiet session 0 errors; leak check 242 -> 238 FPS after 20 toggles + 48 h.
- Release: RELEASE_PENDING

## 0.4.0 released (2026-11-19)
- https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.4.0 (tag @ 47458eb; validate + CodeQL green; alerts 0/0/0). Assets sha256 (downloaded = dist): CommunityPatch 8E5BA774CBDCDB8D8FD1E276A4884075012ED8D06D8A2B2BD3DD4947043EC45C, Expansion AD0473DADD7DCD758A905FE308AC632AC39B93DC5B63FC1274F9B34D14EC93E7, Characters 951423F06DC538FF4344F35473E5D53DC45E591E9820D62F9444FEB0DCE147BD. Quiet install; save00 identical; settings.dat restored.
- T12-T16 done: CUT_CONTENT.md; Yuka/Jamal/Peter are live house residents (not cut); switches + Yuka bunny suit + house-help pack. Evidence `backups/evidence/0.4.0/`.

## 0.3.0 released (2026-11-19)
- https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.3.0 (tag @ 5136fd3; validate + CodeQL green; alerts 0/0/0). Assets sha256: CommunityPatch 10A2884F87F4098E3C9014F145DB2213CC18758C74C22C196E1AB1B91FAA2788, Expansion 78634C8253F282B38A2BA85A60279210CFFF88055CF285F2E3D3ED90C2A694E1, Characters E65FB686C6E6092622C875F1AD445F4AFFCE70E657AD57BC8BA6388978FAAD10 (downloaded = dist). DLLs 9584bba1 / d03de8da / 8c7ec1e9 (fresh clone identical). Game folder: quiet install of 0.3.0; save00 byte-identical to session start, settings.dat restored.
- T01-T10 done (PLAN.md rows + TEST_RESULTS.md 0.3.0). New plugin `MSN2Characters`; skin `lola` generated in `MSN2 Expansion 0.3.0/skins/lola/` (gitignored except skin.json; cache `.build/`). dist/0.3.0 holds 3 zips (Characters 92 MB).
- Test notes: the test save is day 1, where vanilla keeps Camila/Emma (and the cut NPCs) inactive; `actor <X> activate` + `fsm <X> timetable` drives them. `sex Camila` fails on her own beds at day 1 (NRE in SexController.StartSexAsync); `sex Camila 5` (Chloe's bed index from `beds`) works but leaves her in place. Another session's python process held the GPU at 100 % all session, so FPS numbers are unusable.
- Evidence screenshots: `backups/evidence/0.3.0/` (local only).
- Save backup for this session: `backups/saves/20261119-session-0.3.0-start` (save00 41D0F527..., settings 9BF3B481...).

## 0.3.0 planning recon (in progress, 2026-11-19)
- Tools: UnityPy 1.25.4 in `vendor/pyenv` (SAFE-TOOLS verdict recorded). Scripts: `research/scripts/index_bundles.py` (done), `research/scripts/dump_actor.py` (crashes in UnityPyBoost native typetree reader: set `UnityPy.helpers.TypeTreeHelper.read_typetree_boost = None` and parse only GameObject/Transform/SkinnedMeshRenderer, skip MonoBehaviours). AssetRipper GUI in Reverse-this sandbox; ilspycmd via `Reverse-this\sandbox\bin\ilspycmd.cmd`. Unity editor installed: 6000.6.8f1 only (game is 6000.2.2f1).
- MSN2 cut content (index `research/msn2_index.json`): ActorID has Jamal, Peter, Yuka beyond the 4 live girls. All three still ship in main bundle `839cebfd…bundle` (2.4 GB): `YukaAI Variant` (pid 7976), `JamalAI  Variant` (21244), `PeterAI Variant` (3475), plus `CamilaAI Variant` (5835); per-actor FSM graphs (sleep/eat/bath/toilet/homework/clothes/sex), TimeTable SOs, DataSOs, ClothesSOs, 9 voice clips each (Jamal/Peter), clothes prefabs in own bundles (Yuka Maid/Bunnysuit/Underwear, Jamal/Peter Casual/Underwear), Yuka spawn zones in Emma's house (servant room, corridor, laundry). Couple scenes in data: Emma_Sex_Peter, Emma_Lesbi_Yuka, Jamal_Sex_Pillary/Bedroom_BJ, Yuka masturbation, GynChair (Peter, Yuka). Letters enum has a full story set (Government/Club/Mom, endings Police/Club/True). Still unknown: whether the variants carry body meshes (next: dump hierarchy).
- Characters are Daz3D (namespace `Daz3D`, `ActorMeshRenderer`); MSN1 likely Daz too → bone-name match may make a mesh swap onto Camila's rig feasible (verify).
- MSN1 (decompiled to `research/msn1/decompiled`, 505 files; asset index running → `research/msn1_index.json`): girls **LOLA**, **NATALI** (males TUCKER, MARK, plus CAT). Systems: Althar/souvenirs, Safe (+view), Lockpicking, Telescope, Weather (RainMaker), Bar minigame, Window opener, Mounting, Statistics, Sex gallery + static gallery, Difficulty levels, Missions/briefing/quest panel, outfits (Casual, Nude, NudeTowel, Topless, Swimsuit, Naked_Blindfold, Leopardish, Topless_transp_panties), DynamicBone, PINK_DILDO items.
- Owner decision needed: Camila → Lola or Natali (show portraits extracted from MSN1 first).

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
