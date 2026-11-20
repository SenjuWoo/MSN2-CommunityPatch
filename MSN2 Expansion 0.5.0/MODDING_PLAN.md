# MSN2 modding plan — verified recon

Evidence levels: **runtime** = observed in the running game via the plugin; **file** = read from installed files/decompiled code; **review** = Steam user reviews (2026-10-11 pull, 100 recent).

## Target (file + runtime)
| Item | Value |
|---|---|
| Game | My Sexy Neighbour 2, Steam app 3684710, depot 3684711, buildid 21130045 |
| In-game version | `v10.12.2025 (1229)` (VersionSO, shown in menu) |
| Engine | Unity **6000.2.2f1**, **Mono** (Managed/Assembly-CSharp.dll 1.58 MB), x64, URP 17 |
| Graphics API | Direct3D 11 (D3D12Core.dll ships; boot.config asks gfx-threading-mode=6 which D3D11 rejects) |
| Content | Addressables, 95 bundles in StreamingAssets/aa (one 2.5 GB bundle) |
| Frameworks in game | Zenject DI, UniTask, R3, NodeCanvas FSMs (AI + challenges as assets), DOTween, Odin, Newtonsoft.Json, IngameDebugConsole (commands exist, UI not exposed) |
| Saves | `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My Sexy Neighbour 2\save00.dat` + `settings.dat`: Newtonsoft JSON (TypeNameHandling.All) → AES (PBKDF2 key, fixed salt) → base64 |
| Loader | **BepInEx 5.4.23.5 x64** (official GitHub release, sha256 82f98785…32c4) — runtime-confirmed loading plugins under Unity 6000.2.2 |
| Prior mods | none found for MSN2 (web search + local) |

MSN1 (app 2657120) for reference: Unity 2022.3.6f1, Mono, single 2.27 GB `data.unity3d`; mission/story based (MissionController, BriefingWindow, DialogueUI, Altar/Souvenirs, Safe, Telescope, Weather/rain, Bar minigame, Window opener, Statistics).

## Scene structure (runtime)
Scenes: Boot → MainMenu → Game. Game scene: ~14.9k GameObjects, 10.2k renderers (8.4k shadow casters), 359 Animators (356 AlwaysAnimate), 127 skinned meshes, 621 EPO outlinables, 44 traps, 120 hidden-camera spots. Houses: Player, Chloe, Camila, Emma (+ Old Man). Root groups: `Geometry/House - *`, `LIGHTS/{Day,Morning,Evening,Night,Realtime}/House*`, `Controllers/*`.

## Measured performance (9800X3D + 4080 SUPER, 2560x1440)
- Script Update/LateUpdate total ≈ 0.44 ms/frame (149 methods instrumented) — scripts are not the bottleneck.
- Main thread ≈ 3.6 ms; `FinishFrameRendering` ≈ 3.1 ms (render submission); ~420 draw calls, 170 SetPass, 4.1 M tris.
- **Bug 1 (runtime):** `ActorsInHouseView` throws `CurrentHouseDataSO is null` **every frame** (4,725 in 65 s; Player.log +4 MB/min). Cost: 151 → 186 fps (+23%), 1% low 93 → 133 when fixed.
- **Bug 2 (runtime):** `BootState` hardcodes `targetFrameRate = 60`; only the High preset enables VSync → Medium/Low capped at 60 fps (High 156 fps on same spot). Explains review "Low gives less FPS than High".
- High preset: MSAA 8x, 4096 additional-shadow atlas, soft shadows, 75 m shadows / 4 cascades. Low preset allows 6 per-object lights (Medium 3).

## Review-reported issues (review) and status
| Issue | Status |
|---|---|
| Lag / needs optimization / Low slower than High | Fixed: bugs 1+2, graphics options |
| Freeze when girls get stuck in traps | Root-cause candidate: `AIFSMController.InterruptGraphAsync` awaits sub-FSM `Complete` with **no timeout** → guarded with timeout. Normal bucket-trap cycle verified working (trap → trapped → auto-release). Hang not yet reproduced. |
| NPCs teleport to schedule spots / clip walls | **Not reproduced (0.2.0).** Warp log over 5 accelerated days: only Chloe's webcam FSM warps (2.4 m hop onto/off the bed). A forced schedule switch and a trap recall both made the NPC walk. `RotateToQuaternionAsync` (lerps the transform to the interact point, ignores walls) never moved an NPC more than 0.3 m. Probe `warplog` kept for new evidence. |
| Door interaction flings player off map | **Fixed (0.2.0), runtime-evidenced.** Not the doors: the wardrobe hide/exit interaction (`StepInsideAndRotate`) used two competing tweens, one targeting world origin. Plain doors/drawers/windows probed at 0-300 ms frame lag: no fling (max 0.57 m). |
| Stealable objects unreachable in drawers | **Fixed for the unreachable subset (0.2.0), tool-validated.** Items ride with their drawer and are reachable once it is opened (checked for all containers). 12 of 148 spawn points are inside furniture colliders and can never be reached: removed from rotation. |
| Sex scenes missing from gallery | **Not a bug: the feature does not exist.** MSN2's gallery only has world photos (`Common`) and promo images (`CrossPromo`); there is no sex-scene gallery or replay. Candidate Expansion feature. |
| No key remapping | **Done (0.2.0), runtime-evidenced.** Settings > Controls is rebindable. |
| "First game was better": story, missions, dialogue | Expansion scope |

## Approach
Runtime-only BepInEx plugins + Harmony; no edits to game files or bundles. Expansion state goes to a sidecar file next to the save so vanilla saves keep loading without the mod.
