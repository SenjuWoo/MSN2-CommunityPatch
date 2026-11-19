# Changelog

## 0.4.0 — 2026-11-19
Parent: 0.3.0. Goal: cut content (PLAN.md 0.4.0).
- Audit: `CUT_CONTENT.md` classifies every ActorID, FSMID, ClothID/pack, letter, static photo, perk, challenge and achievement (runtime dump `cutaudit` + decompiled code + Addressables catalog)
- Finding: Yuka, Jamal and Peter are not cut. They are residents of Emma's / Camila's houses in the shipped scene data, with full timetables; `HouseOpenerController.OpenHouse` activates them with Camila/Emma. Verified: both houses open, 2 accelerated days, every FSM entered (incl. all couple scenes), 0 exceptions from mod code
- Characters: `[Neighbours] Yuka/Jamal/Peter` and `[Scenes] CamilaJamal/EmmaPeter/EmmaYuka/YukaMasturbation/YukaGynChair` switches (default on = vanilla; off = removed from the house's residents and/or the scene hours swapped for the partner's solo FSM in memory, never saved)
- Restored: Yuka's bunny suit (in her clothes repository and the Addressables catalog, never worn): she wears it 20-22 (prefix on `ClothesController.WearClothingPack`)
- Not restorable (assets not shipped): `Chloe_Monday` FSM, an older Chloe casual outfit (5 ClothIDs)
- Expansion: new pack `house-help` (3 jobs: Yuka, Jamal, Peter)
- Dev commands: `cutaudit`, `house open|close <House>`

## 0.3.0 — 2026-11-19
Parent: 0.2.2. Goal: Lola from My Sexy Neighbor replaces Camila (PLAN.md 0.3.0).
- New plugin **MSN2 Characters** (`senjuwoo.msn2.characters`, optional): data-driven skins in `plugins/MSN2/skins/<id>/` (`skin.json` + `.msnm` meshes + PNG textures). `[Skins] Camila = lola`. Visual only: `ActorID.Camila`, saves, FSMs, schedules, sex scenes and photos are unchanged; removing the DLL restores Camila
- Lola: MSN1 body (14 material slots, 110 shapes incl. FACS face and genital morphs + 4 aliases for Camila-only morphs), hair, scalp, casual outfit (shirt / plaid skirt + belt / shoes + socks), night outfit (brami + thong + slippers); re-bound onto Camila's 149-bone skeleton; Camila's materials cloned with Lola's textures
- Name: every spelling of Camila's name (localization key, translation, enum) reads "Lola" in translated text, texts already on screen and the house HUD; house HUD (6 alert levels), phone and timetable portraits are renders of Lola
- Converter `tools/charport/convert_skin.py` (UnityPy, offline) + `msnm.py` (format writer/reader/self-test): translation-only conform onto the target joints (Genitals/Anus take the pelvis offset, merged toes take their counterpart's offset, hair physics bones follow their nearest mapped ancestor), MSN1 default-on shapes baked, DXT5nm normals rebuilt as RGB, manifest with sha256
- Expansion: journal, tracker, dialogue and notifications show a skin's display name (`ActorNames`, soft dependency)
- New dev commands (Characters, agent bridge): `skin info|reload|off|mats`, `wear <Actor> <Pack>`, `blend <Actor>`, `look <Actor> ...` (temporary camera render), `portrait <Actor>`, `spritedump <Actor>`, `actor <Name> info|activate`, `fsm <Actor> <FSMID|timetable|stop>`, `beds`

## 0.2.2 — 2026-11-19
Parent: 0.2.1. Goal: maintenance after a workspace recovery: runtime re-check on game build 21130045, measured performance pass, test-gap closure, reproducible builds.
- Perf (Patch): `[Performance] AnimatorCulling` (default on): about 335 prop animators (drawers, cabinets, windows, wardrobe doors, traps) use `CullUpdateTransforms` and stop posing while off screen; characters, cameras, UI and nav-mesh doors are excluded. A/B at High: +3.7% average FPS, +20% 1% lows; off-screen pose, wardrobe/door fling, trap and NPC warp regressions pass
- Perf (Patch): `[Performance] SmallObjectShadows` (default off): no shadows from static props under `SmallShadowMaxSize` (0.15 m); measured no gain on a CPU-bound PC
- Fix (Expansion): `cum_on` counted every cum particle that hit an NPC (about a dozen per finish) and had no house; now one event per finish with `house`
- Tests: `sex` and `cum_on` hooks fired through the game's own code; jobs 1-5 played start to finish with rewards matching the pack; gamepad actions verified with a virtual Input System gamepad before/after key rebinds, Reset all keys and a restart
- Build: reproducible DLLs (PathMap, ContinuousIntegrationBuild, SDK SourceLink off); a rebuild of the same sources at another path is byte-identical
- New dev commands behind the agent bridge: `perf`, `perfscan`, `cfg`, `offscreen`, `pad` (patch); `sex`, `fap`, `calm`, `addhours`, `cam <House> [Room]`, inventory/money/day in `mx` (expansion)

## 0.2.1 — 2026-10-13
Parent: 0.2.0. Goal: bug-fix pass.
- Fix (Expansion): journal/tracker text invisible (backdrop only) after reloading a save, e.g. arrest -> Load. Cause: the game destroys its TMP font on scene unload; the HUD kept the dead reference. Reproduced and verified in game (Exit -> Continue).
- Fix (Expansion): J/Enter/comma/period fired while typing in game text boxes (PC channel name, password, debt amount) and while the pause menu was open (rebinding a key to one of them toggled the journal)
- Fix (Expansion): job HUD drew over the game-over (caught) screen; journal stays open over the pause menu
- New: dev probes `mx hud`, `mx arrest` (real caught-by-cop game over), `rebind`
- Checked, no bug: arrest -> Continue keeps sidecar progress and re-attaches; controls hint scan (6/6 found in 3.5 ms once)

## 0.2.0 — 2026-10-12
Parent: 0.1.0. Goal: finish the official patch.
- Fix: wardrobe hide/exit threw the player across the map at low frame rates (two competing tweens, one aimed at world origin); max jump per frame 11 m -> 0.8 m at ~3 FPS
- Fix: 12 of 148 theft spawn points are inside furniture colliders and can never be reached; taken out of rotation (vanilla tasks and Expansion souvenirs)
- New: Settings > Controls is rebindable (main menu and pause menu), conflict swap, Move/Abilities wizards, selfie-camera row, Reset all keys, HUD key hints follow the bindings
- New: dev probes (door/hideprobe, spot audits, warp log, lag emulation, binding tests) behind the agent bridge
- Investigated, no change: NPC schedule teleports (not reproduced), gallery sex scenes (feature does not exist)
- Expansion: version bump only

## 0.1.0 — 2026-10-12
Parent: none. Goal: foundation, Community Patch, Expansion framework + first pack.
- BepInEx 5.4.23.5 setup, build/install/launch/agent/package scripts, CI pack validation.
- Patch: per-frame HUD exception fix (+23% FPS measured), portraits fix, 60-FPS cap fix for Medium/Low, NPC interrupt timeout, Low preset light cap, graphics options.
- Expansion: JSON job packs validated against game enums, sidecar saves (vanilla save untouched), journal/tracker/dialogue UI in the game's font, notifications, rewards, chained jobs, turn-in, souvenir item spawns.
- Pack: Neighbourhood Classics (5 jobs).
