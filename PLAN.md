goal: replace Camila with Lola from My Sexy Neighbor 1, bring back MSN2's cut NPCs (Yuka, Jamal, Peter), and add the best MSN1 systems, shipped as 0.3.0 → 0.4.0 → 0.5.0 GitHub releases.

# PLAN — MSN2 0.3.0–0.5.0 (characters, cut content, MSN1 systems)

Planned 2026-11-19 (owner brief, verbatim intent): "restore cut content, re-add stuff from the first game if it still has good systems we can re-add, and swap out Camila for a girl from the first game as Camila is ugly." Owner picked **Lola**. Releases go to GitHub only (no Nexus/LoversLab for now). The previous plan is archived at `MSN2 Expansion 0.2.2/PLAN-0.2.2.md`.

Builder: read this file, `STATE.md` and `MAP.md`, then start at the first `pending` row. Update Status/Evidence as you go and keep STATE.md NEXT ACTION current. Explicit adult content (nudity, genitals, sex scenes, couple scenes, masturbation) is in scope and stays intact.

## Success criteria
1. **0.3.0 Lola:** with the new `MSN2Characters` plugin installed, every place Camila appears shows Lola: body, face, hair, outfits (casual, nightie, naked), her name in UI, house HUD and job text, and her portraits. Animations, schedules, sex scenes and photos work as before: sex-scene screenshots show no obvious misalignment and no holes. Removing the plugin brings Camila back and the save still loads.
2. **0.4.0 Cut NPCs:** Yuka (Emma's maid), Jamal (Lola's house) and Peter (Emma's house) walk their schedules for 2 accelerated in-game days with no exceptions from MSN2 code. Their couple scenes that work are enabled: Emma×Peter, Emma×Yuka, Jamal×Lola, Yuka masturbation, gyno chair. Anything broken is listed in `CUT_CONTENT.md` with the reason and is left off. Each NPC can be toggled in config.
3. **0.5.0 MSN1 systems:** sex-scene gallery with replay, souvenir altar, telescope, safe cracking, rain weather, statistics screen and difficulty presets. Each is toggleable in config, has an agent test and a TEST_RESULTS row.
4. Every release: CI green on the exact SHA, alerts 0/0/0, downloaded asset sha256 matches `dist/`, installed with quiet config, and the user's `save00.dat` byte-identical to the session-start backup.

## Out of scope
- Editing game files or bundles; runtime only (BepInEx + Harmony + files we ship).
- MSN1 systems not picked (decided): bar drinking minigame (MSN2 has the aphrodisiac instead), window-opener minigame (MSN2 already has house opening), climbing (`Mounting`), Steam leaderboards.
- New voice recording. Lola's MSN1 voice clips are reused only if T09 passes its gate.
- Swapping anyone other than Camila. The skin system is generic, but only `lola` ships.

## Starting state (verified by the planner, 2026-11-19)
| Fact | Evidence |
|---|---|
| 0.2.2 released and installed; main @ e816fc1 clean; CI and alerts green | see 0.2.2 plan archive, STATE.md |
| Tools: UnityPy 1.25.4 + numpy in `vendor/pyenv` (uv venv, SAFE-TOOLS verdict recorded); ilspycmd via the Reverse-this sandbox `bin\ilspycmd.cmd` (path from the machine map's tool roots; call it from PowerShell, not git-bash); AssetRipper GUI in the same sandbox (fallback only); Blender 5.3 on PATH (only for the optional hair decimation); Unity editor 6000.6.8f1 only (game is 6000.2.2f1, so **no AssetBundles**) | commands run this session |
| Decompiled code: MSN2 in `research/decompiled`, MSN1 in `research/msn1/decompiled` (505 files) | files exist |
| MSN2 asset index `research/msn2_index.json`, MSN1 index `research/msn1_index.json`, CAB→bundle map `research/msn2_cab_map.json` | files exist |
| Main game-scene bundle: `StreamingAssets/aa/StandaloneWindows64/839cebfdebea6683d9dd62dba1291e21.bundle` (2.4 GB). Its CAB `CAB-41dace0bcc48d7c9605e82fa9a44b094` holds the actor objects; meshes are in that CAB's `.sharedAssets` | `research/actors_dump.txt` |
| Actors in that scene CAB (all saved inactive, including the live Camila): `CamilaAI Variant` pid 5835 (`SK_Black_Girl` › `Catara Mesh.001` + `TG4 Female Hair 07 (Genesis 8 Female)`), `YukaAI Variant` 7976 (`SK_Asian_Girl`, Catara mesh + `TG4 Female Hair 03`), `JamalAI  Variant` 21244 (two spaces; `SK_Black_Guy`, `Genesis 8.1 Male Mesh`), `PeterAI Variant` 3475 (`SK_White_Man`, G8.1 Male + `TG4 Male Hair 03`) | dump |
| Cut NPC data still shipped: per-actor NodeCanvas FSM graphs (`Yuka-FSM-01-Sleep` … `Yuka-FSM-11-Masturbation`, `Jamal-FSM-10-Sex_Pillary`, `Jamal-FSM-10-Sex_Bedroom_BJ`, `Peter-FSM-10-Sex_Emma`, `Peter-FSM-10-GynChair`, `Emma-FSM-10-Sex_Peter`, `Emma-FSM-10-Lesbi_Yuka`, `Yuka-FSM-10-Lesbi_Emma` …), `TimeTable_<Name>-SO`, `<Name>DataSO`, `<Name>ClothesSO`, voice clips `Jamal_00..08`, `Peter_00..08`, clothes prefabs (Yuka Maid/MaidItems/Sneakers/Bunnysuit/Underwear, Jamal and Peter Casual/Underwear) as Addressables keyed by ClothID, Yuka spawn zones `SPZ_EmmaHouse_Yuka_*` and `SPZ_EmmaHouse_Emma&Yuka_ServantRoom*` | `research/msn2_index.json` hits |
| Code facts: `ActorID` {Player 0, Chloe 1, Narrator 2, Camila 3, Jamal 4, Emma 5, Peter 6, Yuka 7, OldMan 8, PoliceOfficer 100}. `EntitiesController.CollectAllObjectsOnScene` uses `FindObjectsByType<Entity>(FindObjectsInactive.Include)`. `ClothesController.LoadSkinnedMeshes` loads each `ClothID` with `Addressables.LoadAssetAsync<GameObject>(clothID.ToString())` and instantiates the SMR under `actorEntity.ActorMeshRenderer.transform`; naked pack → `aIActorEntity.BlendShapeSync.ShowDick(true)`. `ActorDataSO` {ID, Name, Gender, Portrait, Portrait2, ConditionalPortraits}. Localization: `Game.Core/LocalizationManager.cs`. Letters: `LetterID` has a full Government/Club/Mom story + endings (usage not yet audited) | decompiled files named |
| **Rig compatibility:** Lola (MSN1) and Camila (MSN2) use the same Daz G8F→Rigify rig; MSN2 renamed the main deform bones to Humanoid names and dropped toes. 93 bone names identical; **all 169 Lola bones map onto the 149 Camila bones** | `research/charport/bone_map_msn1_to_msn2.json` |
| **Rest poses match:** same mesh space and scale, identical bone orientations (e.g. hand rotation matrices equal). Lola is ~4 cm taller with ~4.5 cm wider shoulders. Genitals joint differs by 5 cm (Lola 0.885 vs Camila 0.938 height) | `research/joints_lola.json`, `research/joints_camila.json` |
| **Meshes:** Camila `Catara Mesh` 20,574 verts, 3 submeshes (Body, Eyelashes, Eyes), 159 blendshapes. Lola `Body` 69,675 verts, 14 submeshes, 108 blendshapes; 97 shared (all FACS face shapes + PBMAnusOpen/PBMGensOpen01…). Missing on Lola: 4 genital morphs (`PBMG8FAnusOpen`, `PBMAnusStyle03`, `PBMGensOpen03`, `PBMGensPullOutE`) and 58 `pJCM*` joint correctives. Lola Hair 90,244 verts, Scalp 801 | `research/msn2_actor_meshes.json`, `research/msn1_lola_meshes.json` |
| **UnityPy decodes Lola fully:** positions, normals, UV0, 4-bone weights, per-submesh triangles, blendshape deltas (1.02 M sparse entries) in 25 s | planner run |
| **Materials:** Camila Body (`_BaseMap` one baked atlas, `_BumpMap`, `_Legwear_*` overlay), Eyelashes (`_BaseMap`), Eyes (`_BaseMap`); Camila hair uses the Daz-bridge hair props `_DiffuseMap/_AlphaMap/_HeightMap/_SpecularMap`, the same names as Lola's hair. Lola body uses UDIM tiles `HM-Luna_BaseColor/Normal/SSS.1001..1007` (1001 Face, 1002 Head, 1003 Body/Genitalia/Anus, 1004 Legs, 1005 Arms, 1006 Teeth/Mouth, 1007 Pupils) + `brows_d/n`; Eyelashes_UP and Tear have no textures | `research/mats_camila.txt`, `research/mats_lola.txt` |
| Lola's MSN1 outfits on her prefab: Yoshino Shirt/Skirt/Belt/Socks/Shoes/Bra (casual), Night Shirt Slippers, Sweet Brami, GR Thong, BraGen8/PanGen8, Club Skirt, Mask, Between_Flipflops | `research/lola_dump.txt` |
| MSN1 systems in code: `AltharController`/`Souvenir`, `SafeController`/`SafeView`/`LockpickingController`, `TelescopeView`/`EnableTelescopeNode`, `WeatherController`/`WeatherPresetSO` (+ DigitalRuby.RainMaker), `Statistics`/`GameStatistics`/`StatController`, `SexGalleryUI`/`RealtimeGalleryState`/`PCGallery`, `DifficultySettings`/`DifficultyLevelSettingsSO` | `research/msn1/decompiled` |

Research scripts (local, gitignored): `research/scripts/index_bundles.py`, `dump_actor.py` (per-file aware, pure-Python typetree), `bone_list.py`, `bindpose_joints.py`, `smr_materials.py`. **UnityPy rule:** always set `UnityPy.helpers.TypeTreeHelper.read_typetree_boost = None` and parse only engine types you need; the native reader segfaults on this game's MonoBehaviours. Pointers resolve per assets file: FileID n → `assets_file.externals[n-1]`. Loading many bundles in one process needs `del env; gc.collect()`, and skip the 2.4 GB one unless needed (MemoryError).

## Architecture

### New third plugin `MSN2Characters` (GUID `senjuwoo.msn2.characters`)
Same build setup as the other two (`src/MSN2Characters/MSN2Characters.csproj`, `Directory.Build.props`, net472; add `UnityEngine.ImageConversionModule` and `UnityEngine.AssetBundleModule` only if needed, plus `Unity.Addressables`/`Unity.ResourceManager` references for clothes). It owns:
- **Skin system** (`Skins/`): data-driven like job packs. `BepInEx/plugins/MSN2/skins/<id>/skin.json` + `.msnm` meshes + `tex/*.png`. Config `[Skins] Camila = lola` (empty = vanilla). Visual only: `ActorID` stays `Camila`, so saves, FSMs, schedules and sex scenes are untouched.
- **Cut NPC restore** (`CutNpcs/`): config `[CutNpcs] Yuka/Jamal/Peter = true` and per-scene toggles (`EmmaPeterScene`, `EmmaYukaScene`, `JamalLolaScenes`, `YukaMasturbation`, `GynChair`).
- Agent commands (registered on the existing bridge like `AgentCommands.cs`): `skin reload`, `skin info`, `wear <Actor> <ClothingPackID>`, `portrait <Actor>` (render a 512² face shot to PNG), `actor <Name> info|tp|activate`, `fsm <Actor> <FSMID>`.

### `.msnm` mesh format (version 1, little-endian) — writer `tools/charport/msnm.py`, reader `src/MSN2Characters/Skins/MsnmReader.cs`
```
char[4] "MSNM"; u32 version=1
u32 vertexCount, submeshCount, boneCount, blendShapeCount, flags   // flags bit0 = tangents present
boneCount × { u16 len; utf8 name }            // TARGET rig bone names (MSN2 names)
boneCount × f32[16]                            // bindposes, Unity Matrix4x4 row-major m00 m01 m02 m03 m10 … m33
f32[vertexCount*3] positions; f32[vertexCount*3] normals
[f32[vertexCount*4] tangents if flags&1]; f32[vertexCount*2] uv0
vertexCount × { u16 boneIndex[4]; f32 weight[4] }   // weights normalised, sorted descending
submeshCount × { u16 len; utf8 slotName; u32 indexCount; u32 indices[indexCount] }
blendShapeCount × { u16 len; utf8 name; u32 frameCount(=1); f32 frameWeight(=100); u32 nnz; nnz × { u32 index; f32 dv[3]; f32 dn[3] } }
```
Reader: `Mesh { indexFormat = UInt32 }`, `SetVertices/SetNormals/SetTangents/SetUVs(0)`, `boneWeights = BoneWeight[]`, `bindposes`, `SetTriangles` per submesh, `AddBlendShapeFrame` with dense arrays built from the sparse deltas, then `RecalculateBounds`. Bones are resolved by name under the target actor's skeleton root (`SK_*` › `Root`/`Hips`). A missing bone name is a hard load error naming the bone, and the actor stays vanilla.

### Conversion pipeline (offline, Python only, run by the builder; outputs are committed as release data, not to git)
`tools/charport/convert_skin.py --msn1 "<MSN1>/My Sexy Neighbor_Data" --msn2 "<MSN2>/My Sexy Neighbour 2_Data" --out skins/lola`
1. **Extract** (UnityPy, pure-Python typetree): Lola GameObject `Lola` (level0 CAB) › `PFB_Girl` › SMRs. Meshes live in `sharedassets0.assets`. For each SMR take vertices, normals, UV0, weights (`MeshHandler.process()`), triangles per submesh, blendshape channels with sparse deltas, `m_BindPose`, bone names from `m_Bones`, and material texture names.
2. **Target rig:** Camila `Catara Mesh` (pid 4794 in the scene CAB's `.sharedAssets`) gives bone names in SMR order + `m_BindPose`; joint position of bone b = translation of `inverse(bindpose_b)`.
3. **Conform (decided: translation-only LBS; orientations verified identical):** for each Lola vertex `v' = Σ_i w_i · (v + (c_map(i) − l_i))`, where `l_i` is the Lola joint position, `c_map(i)` the mapped Camila joint position (`bone_map_msn1_to_msn2.json`). **Override:** `Genitals` and `Anus` use the `pelvis` offset (their 5 cm joint mismatch would tear the crotch). Apply the same per-vertex offset to blendshape deltas: unchanged (they are deltas). Normals unchanged.
4. **Rebind:** remap bone indices through the map (merge weights that land on the same target bone, keep top 4, renormalise), set bindposes = Camila's bindposes for the target bones, and write the target bone names.
5. **Blendshape aliases** (written as extra shapes in the msnm): `PBMGensPullOutE` ← `PBMGensPullOut`, `PBMG8FAnusOpen` ← `PBMAnusOpen`, `PBMGensOpen03` ← `PBMGensOpen01`, `PBMAnusStyle03` ← `PBMAnusOpen` ×0.5. `pJCM*` correctives are not ported (T06 decides whether that is visible).
6. **Textures:** export every referenced texture as PNG; tile 1001 (face) and 1002 (head) at native size up to 4096, everything else downscaled to ≤ 2048. Runtime: `Texture2D.LoadImage` then `Compress(true)`; normal maps are loaded linear (`new Texture2D(w,h,TextureFormat.RGBA32, true, linear:true)`).
7. Same steps for Hair, Scalp and each outfit SMR. Outfits keep their own submeshes.
8. Write `skin.json` (schema below) and a `manifest.json` with sha256 of every output file.

### `skin.json` (schema v1)
```json
{ "schema": 1, "id": "lola", "displayName": "Lola", "replaces": "Camila",
  "body":  { "mesh": "body.msnm", "target": "Catara Mesh.001", "slots": {
      "Body":    {"template": "Body", "tex": {"_BaseMap": "tex/HM-Luna_BaseColor.1003.png", "_BumpMap": "tex/HM-Luna_Normal.1003.png"}},
      "Face":    {"template": "Body", "tex": {"_BaseMap": "tex/HM-Luna_BaseColor.1001.png", "_BumpMap": "tex/HM-Luna_Normal.1001.png"}},
      "Pupils":  {"template": "Eyes", "tex": {"_BaseMap": "tex/HM-Luna_BaseColor.1007.01.png"}},
      "Eyelashes_UP": {"template": "Eyelashes"}, "Luna_Brows": {"template": "Eyelashes", "tex": {"_BaseMap": "tex/brows_d.png"}},
      "EyeMoisture": {"hide": true}, "Tear": {"hide": true} } },
  "hair":  { "mesh": "hair.msnm",  "target": "TG4 Female Hair 07 (Genesis 8 Female)", "slots": { "*": {"template": "Hair"} } },
  "extra": [ { "mesh": "scalp.msnm", "name": "Lola_Scalp", "slots": { "*": {"template": "Hair"} } } ],
  "clothes": { "Camila_Casual_TankTop": "clothes/yoshino_shirt.msnm", "Camila_Casual_Shorts": "clothes/yoshino_skirt_belt.msnm",
               "Camila_Casual_Sneakers": "clothes/yoshino_shoes_socks.msnm", "Camila_Nightie_Nightie": "clothes/night_shirt_slippers.msnm" },
  "blendshapeAliases": {}, "portraits": { "Portrait": "portrait.png", "Portrait2": "portrait2.png" } }
```
(The slot list above is the starting map. The builder completes it for all 14 body submeshes: Head/Legs/Arms/Teeth/Mouth/Genitalia/Anus use template `Body` with their tile. `template` names Camila's original material by name; the clone keeps its shader and every other property. Set any `_Legwear_*` texture on the clone to Unity's `Texture2D.blackTexture`, or to whatever T05 finds disables the overlay.)

### Runtime flow (skin)
`EntitiesController` collect → Harmony postfix finds `IActorEntity` with `ActorID == replaces` → `SkinApplier` loads the msnm/PNGs once (cached) → swaps `sharedMesh` + `sharedMaterials` on the target SMRs, adds extra SMRs bound to the same bones, and updates `OutlineHelper.UpdateOutlineRenderers()` → Harmony on `ClothesController.LoadSkinnedMeshes`: for `actorID == Camila` and a `ClothID` listed in `clothes`, instantiate the vanilla part as usual, then replace its `sharedMesh`/materials with the converted garment (bones re-resolved by name on the body skeleton) → `ActorDataSO` of Camila: set `Name`, `Portrait`, `Portrait2` (backing fields `<Name>k__BackingField`) → `LocalizationManager` getter postfix replaces whole-word `Camila` with `displayName` (also `Camila's`). Everything is in try/catch per step: a failed step logs `[skin] <step> failed: …` and leaves that part vanilla.

### Cut NPC restore flow
Recon first (T10), because why they are inactive decides the hook: find what activates the live AI actors (Camila is saved inactive too) and which lists (`EntitiesRepositorySO`, timetable controller, actor data repository, `ChallengesController` pools, house HUD residents) contain Camila but not Yuka/Jamal/Peter. Restore = activate the scene object at the same point vanilla activates the others, plus add the actor to each list vanilla needs, plus enable its timetable. Validate each FSM in game; disable any FSM entry that throws or stalls (timetable entry removed at runtime) and record it in `CUT_CONTENT.md`.

### MSN1 systems (in `MSN2Expansion`, 0.5.0)
Each is a self-contained feature class bound in `ExpansionPlugin` behind a `[Systems]` config toggle, built on MSN2's own entities/controllers. MSN1 code is the design reference: copy behaviour, not classes.
- **Sex gallery + replay:** record every completed sex scene (`SexController.MinigameWin`, actor + scene/location id) in the sidecar. A Gallery tab in the main menu or PC lists unlocked scenes; replay starts the same scene through the code path `agent sex` already uses (0.2.2, `AgentCommands.cs`) in a safe state (no police/noise), then returns.
- **Souvenir altar:** shelf object in the player house that displays stolen souvenirs/underwear as trophies (clone of a stealable's mesh), counts towards bonuses.
- **Telescope:** usable at the player-house window; zoomed camera with photo support through the phone camera path.
- **Safe cracking:** MSN2's two `Safe` OpenCloseEntity objects get a dial minigame (MSN1 `SafeView` rules) with cash/item rewards; reuse `LockPickController` UI style.
- **Rain:** weather presets per day; a particle system attached to the main camera + rain audio; while raining, noise radius × 0.6 via the existing noise modifier path (`NoiseFillModifierAction` shows how perks scale it).
- **Statistics screen:** counters (photos, naked photos, thefts, traps, sex scenes, times caught, money earned) in the sidecar, shown in the journal (tab) and pause menu.
- **Difficulty presets:** Easy/Normal/Hard multipliers on NPC visual detection speed, noise fill and police detection (perk action targets show the fields).

## Tasks
| ID | Task | Status | Depends on | Verify | Evidence |
|---|---|---|---|---|---|
| T01 | Snapshot `MSN2 Expansion 0.3.0` from 0.2.2 (git-tracked files), bump versions, CURRENT.txt, VERSION.md (goal = this plan). Add `src/MSN2Characters` project (Plugin.cs with config + TryApply pattern, empty Skins/CutNpcs folders); add it to `$Projects` in `tools/common.ps1`; extend `package.ps1` with a third zip `MSN2_Characters_v<ver>.zip` (DLL + `skins/`, docs) and `install.ps1` to copy `skins/` | done | - | `cmd:powershell -File "MSN2 Expansion 0.3.0/tools/build.ps1"` builds 3 DLLs; game log shows `MSN2 Characters 0.3.0` loaded | 3 DLLs build (0 warnings); LogOutput `Loading [MSN2 Characters 0.3.0]` |
| T02 | `tools/charport/msnm.py` (writer + reader + `--selftest` round trip on a 3-vertex mesh with 1 blendshape) and `src/MSN2Characters/Skins/MsnmReader.cs` per the format above | done | T01 | `cmd:vendor/pyenv/Scripts/python.exe -I "MSN2 Expansion 0.3.0/tools/charport/msnm.py" --selftest` prints OK | `msnm selftest OK` |
| T03 | `tools/charport/convert_skin.py` per pipeline steps 1-8 (copy `research/charport/bone_map_msn1_to_msn2.json` into `tools/charport/`). Output to `MSN2 Expansion 0.3.0/skins/lola/` and add `skins/*/` (except `skin.json`) to .gitignore: generated data ships in the release zip, not git. Print a report: per mesh verts/submeshes/shapes, max conform offset per bone, unmapped bones (must be 0), weight-sum error | done | T02 | `cmd:` converter exits 0; report shows 0 unmapped, weight sums 1±1e-4; `file:skins/lola/manifest.json` | 7 meshes, 0 unmapped, weight err 1.2e-7, manifest.json; fixes: merged toes take counterpart offset, hair bones -> nearest mapped ancestor, MSN1 default-on shapes baked |
| T04 | `SkinApplier` + Harmony hooks (body, hair, extra scalp, materials from Camila templates, outline refresh) + `skin reload/info` agent commands. Load once, cache, apply on Game scene load and after reload | done | T03 | `cmd:` agent `skin info` lists Lola's meshes bound (0 missing bones); screenshots front/side/back of Camila's spot in her house, mid-day and night, saved to `TEST_RESULTS` | `skin info` 149/149 bones, 0 ambiguous, bindpose diff 0; TEST_RESULTS 0.3.0 + backups/evidence/0.3.0/t04_* |
| T05 | Material tuning: find Camila's shader names (`mat.shader.name`) and property set at runtime; make skin tones/normal maps match lighting (no black or shiny patches); disable the legwear overlay; eyes/lashes/brows render with correct alpha; hide EyeMoisture/Tear. Screenshot A/B Camila vs Lola same spot | done | T04 | `file:` TEST_RESULTS rows + screenshot names; no `[skin]` errors in LogOutput.log | BodyShader/Hair/URP Lit clones; legwear off; lashes not shown (no MSN1 lash texture); TEST_RESULTS 0.3.0 |
| T06 | Deformation check in motion: walk, sit, shower, sleep, toilet, trapped, naked photo, and each sex scene Camila has (`agent sex Camila` and the FSM sex states). Look for holes or stretching at shoulders, knees, crotch and face expressions. If knees/elbows/hips visibly pinch, port the `pJCM*` correctives: Blender headless script binding Camila's JCM shape deltas to Lola via Surface Deform (Camila is decimated, so topology differs), writing extra shapes into the msnm. Decide by screenshots | done | T05 | `file:` TEST_RESULTS "Lola deformation" table: scene → PASS/FAIL + screenshot | daily FSMs + bath + sleep + player sex: no holes/pinch; JCM port not needed (TEST_RESULTS "T06") |
| T07 | Clothes: convert the 4 outfit groups; hook `ClothesController.LoadSkinnedMeshes` for Camila; verify `wear Camila Camila_Casual`, `Camila_Nightie`, `Camila_Naked` (naked must show genitals: the `ShowDick` blendshape path must still work, or alias it) and the FSM-driven outfit changes (Clothes FSM, shower) | done | T04 | `file:` screenshots per pack; log shows garments swapped, 0 exceptions | casual/naked/nightie garments swapped (log), sleep FSM outfit change; ShowDick shape `facs` missing in vanilla too |
| T08 | Name + portraits: ActorDataSO Name/Portrait/Portrait2, localization replace, house HUD residents, journal/job text (the pack text says "Camila": the Expansion should read the display name through the actor data, not hard-code it), photo captions. `portrait <Actor>` renders Lola's face shots to `skins/lola/portrait*.png` | done | T05 | `cmd:` agent `texts Camila` finds 0 visible occurrences while the skin is on; screenshot of the HUD portrait | `texts Camila` 0, HUD name Lola, portraits + 6 alert portraits rendered; Expansion ActorNames |
| T09 | Voice (gated): extract Lola's MSN1 voice clips (UnityPy `AudioClip.samples` → WAV) and map them by category to Camila's playback lists (`AudioPlaybackController` / girl voice SOs). **Gate:** do it only if MSN2 voice playback is clip-list based per actor and the categories map cleanly (moan, alert, idle, orgasm); else keep Camila's voice and record why | done (gate not met) | T07 | `file:` TEST_RESULTS row with decision; if on, log `[skin] voice clips=N` | Camila voice kept: MSN2 per-actor alert/seek clip arrays, MSN1 clips are story dialogue (no category map) |
| T10 | Perf + removal: FPS A/B with Lola vs vanilla Camila on screen (High, fixed spot, interleaved ×4). If the loss is over 3 %, decimate the hair to ≤ 35k verts (Blender headless decimate, UVs kept) and re-measure. Then uninstall the Characters DLL: save loads, Camila is back, 0 exceptions | done (perf not conclusive) | T06, T07, T08, T09 | `file:` TEST_RESULTS perf + removal rows | FPS A/B unusable (other process at 100% GPU); CPU skinning equal; no decimation; removal PASS (0 exceptions) |
| T11 | Release 0.3.0: docs (README section "Lola replaces Camila (optional plugin)", CHANGELOG blocks, MAP.md new plugin/files, PACK_FORMAT note on display names), package 3 zips, commit, push, CI on SHA, `gh release create v0.3.0`, asset sha256 check, alerts, install quiet, save byte-identical | done | T10 | `cmd:gh release view v0.3.0 -R SenjuWoo/MSN2-CommunityPatch` | https://github.com/SenjuWoo/MSN2-CommunityPatch/releases/tag/v0.3.0 @ 5136fd3; CI validate+CodeQL green; alerts 0/0/0; 3 assets sha256 = dist; fresh clone builds identical DLLs; quiet install; save00 identical, settings.dat restored |
| T12 | Cut-content audit → `MSN2 Expansion 0.4.0/CUT_CONTENT.md` (snapshot 0.4.0 first): every ActorID/FSMID/ClothID/ClothingPackID/LetterID/StaticPhotoID/Achievement/PerkID/ChallengeType value classified as live (seen at runtime or referenced by live data) or cut (assets present? code path present?), from decompiled code + `research/msn2_index.json` + a runtime dump (`actor`/`fsm` agent commands, letters delivered list). Note which cut items are restorable | pending | T11 | `file:MSN2 Expansion 0.4.0/CUT_CONTENT.md` lists all enums with status | - |
| T13 | Why are Yuka/Jamal/Peter off: trace what activates the live AI actors and which repositories/lists exclude the three (see "Cut NPC restore flow"). Write the finding + chosen hook into CUT_CONTENT.md | pending | T12 | `file:` CUT_CONTENT.md "Activation" section names the classes/fields and the hook | - |
| T14 | Restore Yuka (Emma's maid): activate, register, timetable, clothes packs (Maid default, Underwear, Naked, Bunnysuit), photos/hidden-cam/theft/job hooks recognise her (GameHooks actor list already allows `Yuka`). 2 accelerated days with `warplog`; every FSM entered at least once (`fsm Yuka <id>` for rare ones) | pending | T13 | `file:` TEST_RESULTS Yuka table: FSM → PASS/FAIL; LogOutput 0 MSN2 exceptions | - |
| T15 | Restore Jamal (Lola's house) and Peter (Emma's house) the same way; enable couple FSMs Jamal×Lola (Sex_Pillary, Sex_Bedroom_BJ), Emma×Peter, Peter GynChair, Emma×Yuka, Yuka masturbation; each behind its own config toggle; disable any that break and record why | pending | T14 | `file:` TEST_RESULTS rows per NPC and per scene with screenshots; CUT_CONTENT.md updated | - |
| T16 | Interactions: police/alert reactions of the restored NPCs, traps on them, sleeping pills (`<Name> Sleep from pills` FSMs), challenges/jobs can target them (add 1 Expansion job per NPC in a new pack `cut-neighbours`; validator passes) | pending | T15 | `cmd:node "MSN2 Expansion 0.4.0/tools/validate-packs.mjs" "MSN2 Expansion 0.4.0/packs"` OK; job run-through rows | - |
| T17 | Release 0.4.0 (as T11; README "Cut neighbours restored", toggles documented) | pending | T16 | `cmd:gh release view v0.4.0 -R SenjuWoo/MSN2-CommunityPatch` | - |
| T18 | Snapshot 0.5.0. Sex gallery + replay (sidecar unlocks, UI tab, safe replay, return to world). Include scenes of restored NPCs if they use the same controller | pending | T17 | `file:` TEST_RESULTS: unlock → listed → replay → return, per actor | - |
| T19 | Souvenir altar + statistics screen (both read/write the sidecar; schema bump with migration from 1) | pending | T18 | `file:` TEST_RESULTS rows; old sidecar migrates (load a 0.4.0 sidecar) | - |
| T20 | Telescope + safe cracking | pending | T18 | `file:` TEST_RESULTS rows (telescope photo counts for photo objectives; safe opens with correct code, rewards) | - |
| T21 | Rain weather + difficulty presets (noise/detection multipliers verified with `noise` probe and detection timing before/after) | pending | T18 | `file:` TEST_RESULTS numbers | - |
| T22 | Release 0.5.0 (regression: all three plugins, 15-min quiet session, FPS vs 0.4.0) | pending | T19, T20, T21 | `cmd:gh release view v0.5.0 -R SenjuWoo/MSN2-CommunityPatch` | - |

## Decisions
- **Lola is ported by re-binding her MSN1 meshes onto Camila's existing skeleton at runtime**, using a custom `.msnm` file and PNG textures loaded by the plugin. Rejected: AssetBundles. They need the exact 6000.2.2f1 editor (only 6000.6.8f1 is installed) and a Unity account licence, and there is no material gain because we clone the game's own materials anyway. Rejected: replacing Camila's skeleton or animations, which would break sex-scene alignment.
- **Conform by translation-only skinning offsets.** Bone orientations, scale and mesh space are identical between the rigs (measured), so per-joint offsets carry Lola's mesh onto Camila's joints without rotation math. Cost: Lola takes Camila's proportions (~4 cm shorter, slightly narrower shoulders); her face, body shape, skin and hair stay hers. Genitals/Anus use the pelvis offset.
- **Visual swap only:** `ActorID.Camila` stays, so saves, FSMs, schedules, challenges and jobs are untouched and the swap is removable.
- **New plugin `MSN2Characters`** instead of growing the patch or the Expansion: optional download, owns skins and cut NPCs. MSN1 systems go in the Expansion (gameplay layer).
- **Converted Lola data ships in the release zip**, generated by `convert_skin.py`; git holds only the code and `skin.json`. The generated folder is about 100+ MB of textures.
- **Order: Lola (0.3.0) → cut NPCs (0.4.0) → MSN1 systems (0.5.0).** The owner asked for the swap explicitly, and Jamal's restored scenes are with Camila (now Lola), so the swap must come first.
- **MSN1 systems picked:** gallery/replay (most-requested in reviews), altar, telescope, safe, rain, statistics, difficulty. Rejected: bar minigame, window opener, climbing, leaderboards (duplicate MSN2 features or low value).

## Assumptions (fastest check)
- Bone names under `SK_Black_Girl` are unique (no duplicate names in other branches) → `skin info` lists 0 ambiguous names.
- Camila's material names are exactly `Body`, `Eyelashes`, `Eyes`, `Hair` at runtime (seen in the bundle) → log the clone source names.
- Cut NPC FSM graphs reference scene objects that still exist → T14/T15 FSM tables.
- Camila's sex-scene partners/props align to bones, not to mesh positions → T06 screenshots.
- Lola's genital/anus submeshes plus PBM morphs cover what MSN2 sex scenes animate → T06/T07 naked + sex screenshots; animation curves on `Catara Mesh.001` blendshapes (`blendShape.<name>`) can be listed from the AnimationClips with UnityPy if a scene looks wrong.

## Risks
- Missing `pJCM*` correctives may pinch joints in extreme poses → T06 Surface-Deform port.
- 14 materials and ~160k verts on one NPC cost FPS → T10 gate + hair decimation.
- Cut FSMs may stall or teleport NPCs (that is likely why they were cut) → per-FSM disable switch, recorded in CUT_CONTENT.md.
- The localization replace could hit an unrelated word → whole-word, case-sensitive match on `Camila` only.
- VRAM: 7 tiles × 3 maps at 2048-4096 → runtime `Compress(true)`; measure memory in T10.
- Test runs touch the user's save slot → back up `*.dat` first and restore `settings.dat` (same rule as 0.2.x).

## Checks that cannot run here
- Low-end GPU/VRAM behaviour: NOT RUN (one test PC).
- Owner's visual sign-off on Lola: NOT RUN until the owner plays. Screenshots in TEST_RESULTS are the proxy.
