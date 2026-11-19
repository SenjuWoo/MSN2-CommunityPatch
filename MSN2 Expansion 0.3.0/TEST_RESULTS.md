# Test results

Rig: Ryzen 7 9800X3D, RTX 4080 SUPER, 2560x1440 280 Hz, Windows 11, game build 21130045, BepInEx 5.4.23.5.
Method: `tools/relaunch.ps1` (install → launch → Continue → wait for Game scene + settle) then `tools/agent.ps1` commands. Same save, same spawn point (player house).

## 0.2.2 — 2026-11-19
Evidence level: **runtime-evidenced** (agent bridge, game build 21130045 unchanged), not owner-confirmed. Smoke test of the released v0.2.1 DLLs on the reinstalled game first: both plugins load, all fixes active, 0 `failed to apply`, 0 exceptions/warnings in LogOutput.log; fps 10 at the usual spot 324.5 avg (VSync on).

### 0.2.2 perf baseline
Usual spot = the save's spawn point in the player house, `tp 4.927 1.02 19.766 65.03 0`, 3 s settle, `perf 10` (10 s window), VSync off, FrameRateLimit 0, PlayerLoopTiming on. Values: avg FPS / p99 frame ms / 1% low FPS / animation phases ms (`PreLateUpdate.DirectorUpdateAnimationBegin+End`) / `PostLateUpdate.FinishFrameRendering` ms / `Update.ScriptRunBehaviourUpdate` ms.
| Preset (MSAA, shadow dist.) | avg | p99 | 1% low | anim | finish frame | scripts | animators | shadow casters |
|---|---|---|---|---|---|---|---|---|
| High (8x, 75 m) | 294.0 | 4.13 | 227.2 | 0.516 (0.186 + 0.203) | 1.235 | 0.474 | 356 AlwaysAnimate, 3 CullUpdateTransforms | 8,386 |
| Medium (4x, 40 m) | 297.8 | 4.10 | 185.3 | 0.509 | 1.214 | 0.456 | same | 8,388 |
| Low (2x, 20 m) | 308.3 | 3.77 | 200.0 | 0.506 | 1.133 | 0.465 | same | 8,388 |
| High, RenderScale 0.5 | 298.6 | 3.80 | 245.2 | 0.529 | 1.195 | 0.473 | same | 8,389 |
Reading: presets and half resolution move FPS by under 5%, so this rig is CPU-bound (main thread + render submission); GPU-side options cannot be judged here. Animation phases 0.51 ms ≥ the 0.4 ms gate, so AnimatorCulling was built. `perfscan`: 373 AlwaysAnimate animators = 205 drawers/cabinets (OpenCloseEntity), 44 DoorEntity, 40 small windows, 24 wardrobe doors, 14 bucket traps, 9 toilets, 5 fridges, 2 stoves, 29 other props (car wheels, sliding doors, freezer doors), 1 character; 8,243 mesh shadow casters, 365 of them static and smaller than 0.15 m.

### AnimatorCulling (T06) — default ON
`CullUpdateTransforms` on prop animators. Kept animating: characters (IActorEntity, player), cameras, UI (no renderer, Unity would freeze them) and every entity with a NavMeshObstacle (DoorEntity: the obstacle carves the NPC nav mesh from the animated leaf, a stale off-screen pose could block a doorway). Log: `[perf] animators culled=335 (... kept=38)`.
A/B at High, toggled at runtime (`cfg Performance AnimatorCulling`), 6 interleaved pairs (3 off→on, 3 on→off), same spot:
| | avg FPS | 1% low | anim phases ms |
|---|---|---|---|
| off (6 runs) | 265.7 (240.0-291.2) | 136.5 (67.6-202.6) | 0.569 |
| on (6 runs) | 275.5 (247.8-294.5) | 164.5 (118.8-199.1) | 0.492 |
| change | **+3.7%** | **+20.5%** | -0.077 |
An earlier 3-pair run (before doors were excluded, 372 culled) gave +4.5% avg and +25.5% 1% low, all three pairs in the same direction. Run-to-run noise on this machine is high (±8%); the direct saving is ~0.08 ms of animation work plus less transform sync in rendering. Gate (≥3% avg or 1% low) passed.
Regression checks with culling on:
| Check | Result |
|---|---|
| `offscreen`: toggle a prop while watched (reference poses), toggle it while facing away, look back; 6 drawers/cabinet doors + 3 wardrobe doors | PASS 9/9: the leaf does not move while unseen (stale = full travel 0.2-0.9 m) and lands exactly on the watched pose after one look (error 0.0000 open and closed) |
| `hideprobe 8 / 2` (wardrobe enter/exit, largest per-frame jump) | PASS: 0.026/0.057 and 0.031/0.182 m (0.2.0 fixed: 0.03/0.06 and 0.02/0.18) |
| `doorprobe` cabinet doors 45/47/48 oc_open + oc_closeon, DoorEntity 3/5 closeon | PASS: max 0.015-0.204 m (0.2.0 worst case 0.57) |
| Bucket trap on Chloe, triggered while the player is across the street | PASS: Bath FSM → Bucket Trap → Trapped → Rest schedule resumed in 55 s |
| 1 accelerated game day each way (`timescale 4`, `warplog`) | PASS: on 12 events, off 10, all Chloe's scripted webcam/bed hops (same as 0.2.0); no new warp kinds |

### SmallObjectShadows (T07) — default OFF
Shadow casting off for 365 static mesh renderers under 0.15 m (switches, sockets, mugs, cans, jars). A/B at High with AnimatorCulling on, 4 interleaved pairs:
| | avg FPS | 1% low |
|---|---|---|
| off | 283.0 | 180.2 |
| on | 281.8 | 163.1 |
| change | -0.4% | -9% (noise) |
Gate (≥3%) failed, so the option ships default-off; the screenshot check was not needed. 365 of 8,385 casters is too few to matter on a CPU-bound rig.

### Expansion test gaps (T08)
New agent commands drive the game's own code: `sex <Actor>` (SexController.StartSexAsync on the nearest bed, the call AIActorEntity.InteractAsync makes, then StartMinigameSlide / InterruptSlideAndGetResult until the win), `fap <Actor> [dist] [pitch] [capsule]` (PlayerToolsController.ActivateToolAsync(Penis), FapController's own hand-move code until it cums; particles hit what they hit), `calm`, `addhours`, `cam <House> [Room]`; `mx` now shows money/subscribers/day/hour.
| Test | Result | Evidence |
|---|---|---|
| `sex` hook | PASS | `sex Chloe`: 5/5 minigame hits, win, scene ended; event `sex actor=Chloe house=ChloeHouse` (LogOutput `[event] sex ...` with VerboseLog) |
| `cum_on` hook | PASS after fix | `fap Chloe 1.0`: FapController OnActorCummed x17 (Player.log `Cummed actor Chloe`) → **one** event `cum_on actor=Chloe house=ChloeHouse`. Before the fix the same finish raised 14 `cum_on` events (one per particle) and no house, so a counted or house-filtered objective would have completed from a single finish. Fixed in GameHooks (hits on one actor merge until a 6 s gap; house added; PACK_FORMAT updated). Note: the NPC's root capsule (the collider FapController credits) is off in many FSM states (alert, webcam, bath; CalmDown turns it back on), so a finish only registers where the game's FSM enables it; the test forced it with AIActorEntity.EnableCapsule |
| Job 1 nc01_old_habits | PASS | photo + souvenir + hidden cam + shadow + timed: money 0 → 550 (250+75+75+100+50), subs 10 → 185 (150+25), nc02 unlocked |
| Job 2 nc02_steam_room | PASS | Chloe in bath at 11:00, one shot = `shower` + `naked` (activity=Shower naked=True), Toothbrush souvenir: +625 money (350+100+75+100), +250 subs, completed |
| Job 3 nc03_lady_of_the_house | PASS | unlocked day 2 08:00 once EmmaHouse opened; cam in EmmaHouse_Bedroom, Panties souvenir (a vanilla Panties stolen first was correctly not counted), Emma photo in her bedroom at 22:00 (the 18:00 shot caught her in the corridor, room = NPC position, correctly not counted); `timed` (6 h) failed and was not paid: +775 (500+150+125), +300 subs |
| Job 4 nc04_caught | PASS (money/subs) | day 3 06:00: bucket trap on Chloe → trap + bucket bonus, photo with activity Trapped: +850 (600+100+150), +400 subs; item rewards BucketTrap/Aphrodisiac: names valid (game ItemID enum), count checked in T10 |
| Job 5 nc05_red_room | PASS | CamilaHouse opened; cam in CamilaHouse_RedRoom, Buttplug souvenir, Camila yoga in the gym at 20:00 (activity Exercise): +1150 (800+200+150), +500 subs, all 5 jobs Completed |
| Log scan of the session | PASS | LogOutput: 0 errors, 0 warnings, 0 `failed to apply`; Player.log: 31 vanilla OperationCanceledException (NPC move tasks cancelled by traps, time jumps and alerts), none from MSN2 |

### Gamepad and Steam triage (T09)
`pad <Map/Action> <control>` adds a virtual Input System gamepad (`InputSystem.AddDevice<Gamepad>`) and queues its state, the same event path as hardware.
| Test | Result | Evidence |
|---|---|---|
| Vanilla bindings: Jump (South), MakeShoot (South), Sprint (left trigger), Move (left stick), Look (right stick) | PASS 5/5 | action fired from device MSN2VirtualPad; player moved 1.30 m (Move), turned 60° (Look), rose (Jump) |
| After keyboard rebinds Jump → J, Sprint → Z | PASS 5/5 | bindings now `<Keyboard>/j,<Gamepad>/buttonSouth` and `<Keyboard>/z,<Gamepad>/leftTrigger`; every pad action still fires; J jumps, Space does not |
| After Settings > Controls > Reset all keys | PASS | bindings file removed (custom=false), `<Keyboard>/space,<Gamepad>/buttonSouth` etc.; pad Jump/Sprint/Move/Look fire again, Space jumps, J does not |
| Repeatability | NOTE | with other windows competing for focus, both injected keys (3/6) and pad presses (2/6) were dropped at the same rate; the binding lists, not the hit rate, are the deterministic evidence |
| Steam discussions hub (app 3684710) | NOT RUN | steamcommunity.com returns "You must be logged in to view this Community Hub" in the built-in browser; needs the owner's pasted threads |

### Regression + package (T10)
| Test | Result | Evidence |
|---|---|---|
| 15-minute session, quiet config (VerboseLog off, AgentBridge on for driving) | PASS | jobs 1-5 replayed (money/subs identical to T08), item rewards of nc04 arrived (BucketTrap 0 → 1, Aphrodisiac 0 → 1), 6 game days at timescale 3 with the player moved between houses; LogOutput: 0 errors, 0 warnings, 0 `failed to apply`, 0 exceptions; Player.log: 124 vanilla OperationCanceledException (stack: FsmAiHelpers.MoveToPositionAsync / MoveToInteractPointWithPathfindingCommand / DoorEntity.InteractAsync, NPC moves cancelled by time jumps and traps), 0 frames from MSN2 |
| Bindings persistence + gamepad after restart | PASS | Jump → J saved, game restarted: binding `<Keyboard>/j,<Gamepad>/buttonSouth`, pad South fires, J jumps, Space does not; `rebind Player/Jump reset` removed the file again |
| 10 s usual-spot FPS, 0.2.1 release DLLs vs 0.2.2, A/B/A/B sessions, VSync off | NOT CONCLUSIVE | another session was running a GPU audio job and then Minecraft on the same PC (GPU 80-100% without this game): 0.2.1 152.4 / 172.9 / 345.1 / 283.0, 0.2.2 165.5 / 154.4 / 189.0 / 197.9 avg FPS, 0 hitches over 50 ms in every run. The only per-frame change in 0.2.2 is AnimatorCulling, measured cleanly in T06 (before the other workloads started) |
| Package | PASS | `package.ps1 -Version 0.2.2`: MSN2_CommunityPatch_v0.2.2.zip 903407BD...1D8D (759,809 bytes), MSN2_Expansion_v0.2.2.zip 431B2252...7254 (65,334 bytes); listing payload-only (BepInEx core + licence, DLLs, README/CHANGELOG/PACK_FORMAT, pack.json), no .cs/.ps1/STATE/PLAN/TEST_RESULTS; plugin DLLs 0DFFFD36... / FD01C563... |

## 0.2.0 — 2026-10-12
Evidence level for everything below: **runtime-evidenced** (observed in the running game through the plugin, input injected through the Input System event queue) unless marked tool-validated. Not user-confirmed yet. Low-FPS conditions are emulated with `lag <ms>` (a Thread.Sleep per frame): 100 ms ≈ 9 FPS, 300 ms ≈ 3 FPS.

### Wardrobe fling (hide in / leave a wardrobe, `PlayerAnimationsController.StepInsideAndRotate`)
Largest per-frame jump of the player (m), wardrobes #8 / #2, `hideprobe`:
| frame lag | vanilla enter | vanilla exit | fixed enter | fixed exit |
|---|---|---|---|---|
| none (~260 FPS) | 0.05 / 0.03 | 0.06 / 0.18 | 0.03 / 0.02 | 0.06 / 0.18 |
| 100 ms | 4.40 / 3.53 | 1.89 / 2.04 | 0.55 / 0.56 | 0.31 / 0.31 |
| 300 ms | 11.20 / 7.46 | 4.21 / 2.23 | 0.80 / 0.97 | 0.77 / 0.81 |

Final position with the fix equals the no-lag result at every lag; vanilla at 300 ms ended 1.3-3.2 m off target. Plain doors (`doorprobe`: 6 DoorEntity doors x closing-on-player / open from either side, 0 and 100 ms; 6 cabinet doors at 0 and 120 ms): no fling, worst case 0.57 m (garage door closing on the player, drawer door at 120 ms).

### Theft spawn points (`spotaudit`, tool-validated)
148 points (Chloe 47, Emma 60, Camila 41). The player's own interaction ray (`ObjectsInteractor`: camera ray, 2.0 m, layer mask 131137) against a hair-tie-sized trigger on the Interactable layer riding with the point, from 8 radii x 24 angles of free standing/crouching positions (capsule fits, ground below), containers opened with the player's own interaction. 93 reachable as-is, 43 reachable once the blocking drawer/door is opened (spot-checked: the item rides with its drawer), **12 unreachable** (Emma 8, Camila 3, Chloe 1). After the fix: `freespots` total 148, marked 12, unreachable still free 0.

### Key remapping
| Test | Result |
|---|---|
| Settings > Controls upgraded in the pause menu path and the main menu (same SettingsView) | PASS, screenshots `controls_page`, `menu_controls` |
| Click Interact, press G: binding saved to `senjuwoo.msn2.bindings.json`; `Interact` action fires on G, not on E | PASS |
| Restart: saved bindings loaded, G still fires, E does not | PASS |
| HUD hint for Interact reads G | PASS (`texts G`) |
| Jump -> J: Space no longer jumps, J jumps (0.54 m rise) | PASS |
| Crouch -> J while Jump holds J: Jump swaps to Ctrl | PASS |
| Move wizard Y H N M: holding Y moves the player, holding W does not, arrow keys untouched | PASS |
| Reset all keys: defaults back, file removed, page text back to vanilla | PASS |
| Rebind in the main menu (detached GameInputs), Continue: real GameInputs has the binding | PASS |

Not tested: gamepad, other languages/layouts, physically typed keys (events were injected), 4:3 or small windows.

### Other
- NPC schedule teleports: warp log over 5 accelerated days (`warplog`): 18 warps, all Chloe's webcam FSM (2.4 m hop onto/off the bed); a forced schedule switch (`settime 11`) and a trap recall produced no warps (NPCs walked). Not reproduced, no change made.
- Gallery: no sex-scene gallery exists in MSN2 (static photos are `Common` and `CrossPromo` only); not a patchable bug.
- Regression: 10 s at the usual spot: 258.6 FPS avg (VSync 280 Hz), p99 7.1 ms, 1% low 140 FPS, 0 hitches over 50 ms, no errors or warnings in LogOutput.log; pack validator OK (1 pack, 5 jobs).

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
| House room label (vanilla panel) | PASS | screenshots show "Chloe's House / Bathroom" and "Living Room" with 0 exceptions |
| Save/load with plugin | PASS (vanilla path) | `console save` → "Save slot 0 saved"; reload via Continue works |

Not checked: low-end hardware, Steam Deck, D3D12, long sessions (>15 min), every NPC FSM for interrupt handling.

## Expansion 0.1.0 — 2026-10-12
Input for UI tests is injected through the Unity Input System event queue (`agent key <Key>`), the same path as a physical keyboard. Objective tests drive the game's own code: `PhoneCameraController.MakeShot`, `StealableEntity.InteractAsync`, `HiddenCameraEntity.CompleteInstallation`, `TrapEntity` trigger path.

| Test | Result | Evidence |
|---|---|---|
| Pack loads, 0 errors; broken pack rejected with file/field message | PASS | log `[packs]`; duplicate-id case caught; CI validator rejects actor typo |
| Unlock → game notification "New job from The Collector" | PASS | screenshot (game NotificationsView) |
| Journal (J), accept (Enter), dialogue box, tracker | PASS | screenshots `mx_journal`, `mx_dialogue` |
| Photo objective (actor + house) via real phone shot | PASS | progress 0→1 |
| Hidden-camera bonus via install completion | PASS | `HC_Chloe_Webcam_1`, room ChloesHouse_WebRoom |
| Shadow bonus fails when NPC reaches Alert | PASS | `shadow:FAILED` after being seen |
| Turn-in step (main done, bonuses left) via J + Enter | PASS | notification + tracker "Ready to turn in"; status Completed |
| Rewards paid | PASS | HUD money $0 → $300; later +$625 |
| Activity + naked photo (Chloe in shower 11-12) | PASS | shower + naked done from one shot, bonus 3/3 |
| Souvenir spawn on theft spot, saved by vanilla, re-adopted after reload, removed when stolen | PASS | log `[souvenir] placed`; 0 placements after reload; scene count 1→0 after steal |
| Sidecar persistence across 3 restarts | PASS | `save00.msn2x.json` contents + `mx` after relaunch |
| Vanilla load with expansion DLL removed | PASS | Game scene reached, souvenir present as normal stealable, 0 exceptions |
| Trap hook | PASS | `[event] trap actor=Chloe trap=Bucket` |
| Interrupt guard does not break trap cycle | PASS | Clothes FSM → trapped → released → schedule resumed |
| sex / cum_on hooks | NOT TESTED | |
| Jobs 3-5 end to end | NOT TESTED | rooms/activities verified individually (Camila yoga = Exercise in gym) |

## 0.2.1
- Journal text after reload (runtime): before fix, Exit -> Continue gave fontAlive=false, bodyFont=null, screenshot = backdrop only (matches the report "no text just the backdrop after getting arrested"). After fix: log `[hud] font re-resolved`, fontAlive=true, journal fully rendered (screenshot). Arrest itself was not triggered; the arrest "Load" button uses the same scene reload (GameOverView -> LoadSceneWithLoaderAsync<GameInitState>).
- Real arrest (runtime, `mx arrest` -> Continue): game over screen showed tracker + dialogue over the popup (now hidden, rootActive=False). After Continue: sidecar loaded (5 missions), mission re-attached, journal text OK, progress kept (souvenir done). Arrest adds 3 game hours, so a running 4-hour bonus fails (vanilla behaviour, left as is).
- Key blocking (runtime): J toggles the journal in play; while the pause menu is open J is ignored; after resuming J works again. Text-box blocking (TMP_InputField focus) is code-verified only: the PC registration box exists only on a new game, not tested.
- Job 1 end to end (runtime): accept, souvenir steal, photo, hidden camera -> auto-complete, rewards 400 money / 175 subs match the pack (250+75+75, 150+25), nc02 unlocked.
- Log scan of the whole session: no exceptions from either plugin (2 vanilla OperationCanceledException from NPC move interrupts on alert).

## 0.3.0 — Lola replaces Camila (MSN2 Characters) — 2026-11-19
Method: game build 21130045, all three plugins installed with `tools/install.ps1`, agent bridge on. Test save is day 1 08:00, where vanilla keeps Camila inactive (her house opens later in the story), so tests activate her with `actor Camila activate` and drive her schedule with `fsm Camila <FSMID>` / `fsm Camila timetable` (the calls `TimeTableController` makes). Screenshots come from a temporary camera (`look <Actor> <yaw> <dist> <h> <fov> <size> <name> <bone>`); copies are kept locally in `backups/evidence/0.3.0/` (not in git: explicit renders, 14 MB). Evidence level: runtime-evidenced, owner sign-off NOT RUN.

| Task | Test | Result | Evidence |
|---|---|---|---|
| T01 | 3 plugins build; `MSN2 Characters 0.3.0` loads | PASS | build 0 warnings; LogOutput `Loading [MSN2 Characters 0.3.0]` |
| T02 | `msnm.py --selftest` round trip (3 verts, 1 shape, tangents, row-major bindpose) | PASS | `msnm selftest OK` |
| T03 | converter: 7 meshes, 0 unmapped bones, weight sum error ≤ 1.2e-7, manifest | PASS | body 69,675 verts / 14 slots / 110 shapes (4 aliases), hair 90,244, scalp 801, 4 outfits; merged toes take their counterpart's offset (6 cm toe collapse fixed); hair physics bones follow their nearest mapped ancestor; MSN1 default-on shapes baked (`Armature SkinBinding`, `hairClipFix`, shirt `fix`) |
| T04 | skin applies: 149/149 bones, 0 ambiguous, bindpose diff 0.0 vs the live mesh | PASS | `skin info`; `t04_lola_casual_front/back`, `t04_lola_face` |
| T05 | materials: Camila `Shader Graphs/BodyShader` clones with Lola's UDIM tiles (Repeat wrap, u 0..7), DXT5nm normals rebuilt as RGB, legwear overlay off (`_WearLegwear` 0 + transparent `_Legwear_*`), eyes/brows on URP Lit clones, hair on `Shader Graphs/Hair` with Lola's own float/colour values; EyeMoisture/Tear hidden | PASS with limit | `t05_camila_vanilla_front` vs `t04_*`; 0 `[skin]` errors. Limit: Lola's eyelash cards have no texture in MSN1, so lashes do not show |
| T06 | deformation: cook, rest (sit), yoga (floor), bath (naked, stepping into the tub), toilet, sleep (nightie), gardening, eat, player sex (legs-up pose) | PASS | `t06_contact1`, `t06_contact_bath`, `t06_sex_1_120`, `t06b_contact`: no holes or visible pinching at shoulders, knees, crotch; face shapes driven (`blend Camila` shows facs_* weights) |
| T06 | JCM port decision | NOT NEEDED | no visible pinching in the poses above; vanilla drives 58 `pJCM*` shapes Lola does not have (missing shapes are skipped, 0 errors) |
| T07 | clothes: `wear Camila Camila_Casual / Camila_Naked / Camila_Nightie`, sleep FSM changes outfit | PASS | log `[skin] garment Camila_Casual_* -> clothes/*.msnm`, `Camila_Nightie_Nightie -> clothes/nightie.msnm`; naked shows genitals (`t07_contact_naked`). `ShowDick` uses blendshape `facs`, which vanilla Camila's mesh does not have either (vanilla warning), so nothing to alias |
| T08 | name + portraits | PASS | `texts Camila` 0 hits, house HUD portrait name = "Lola"; Russian key `Камила` and English `Camila` both rewritten; Portrait, Portrait2 and 6 alert-level HUD portraits rendered by `portrait Camila` (`t08_portraits`, `t08_cond`); Expansion journal/tracker/notifications show "Lola" through `ActorNames` |
| T09 | voice | GATE NOT MET, Camila's voice kept | MSN2 voice per actor = `AIAlertController` seek-start/seek-end/alert clip arrays (`Camila_00..08`); MSN1's 1,082 clips are story dialogue lines from many speakers (no Lola alert/seek set), so no clean category map |
| T10 | FPS A/B Lola vs vanilla on screen (High, 8 s, interleaved ×2) | NOT CONCLUSIVE | another process held the GPU at 100 % during the session (13.6 fps in both conditions: Lola 14.3 / 10.6, vanilla 19.2 / 9.5). CPU skinning identical (0.09-0.13 ms both). Hair decimation not done (no measurable loss to act on). Texture memory estimate 248 MB |
| T10 | removal: Characters DLL deleted, Continue | PASS | Game scene reached, plugin not loaded, 0 exceptions in LogOutput/Player.log |

Load cost: the skin loads in 8.5-10.8 s while the main menu is open (worker-thread parse, one texture per frame).
