# CUT_CONTENT — My Sexy Neighbour 2 (game build 21130045)

Audit for 0.4.0 (PLAN.md T12/T13), 2026-11-19. Sources: decompiled `Assembly-CSharp` (enums, controllers), the runtime dump `cutaudit` (MSN2 Characters agent command: repositories, timetables, house residents, house-opener config, AI actors; raw JSON kept locally in `research/cutaudit_0.4.0.json`), the Addressables catalog (`StreamingAssets/aa/catalog.bin`) and a 2-day accelerated run with every house open.

Status words: **live** = shipped data uses it and vanilla reaches it; **gated** = live, but only after story progress; **cut** = enum value or asset exists, nothing in vanilla reaches it; **missing** = enum value only, the asset is not shipped (cannot be restored without new assets).

## Activation (T13): why Yuka, Jamal and Peter "never show up"
They are **not cut**. They are residents in the shipped scene data and have complete timetables:

| House | `HouseZone.Residents` (scene data) | Opened by |
|---|---|---|
| ChloeHouse | Chloe | new game (`HouseOpenerController.InitializationSetupAsync`) |
| CamilaHouse | **Camila, Jamal** | letter `OpenHouse2` ("New characters arrived", 255 subscribers)* |
| EmmaHouse | **Emma, Peter, Yuka** | letter `OpenHouse3` ("A house full of secrets", 780 subscribers)* |

\* Mapping from the letter subjects and thresholds; the `OpenHouse` action's `House` field was not dumped.

`HouseOpenerController.OpenHouse(house)` sets every resident's GameObject active (and `CloseHouse` hides them); `AIActorEntity.RefreshObject` restores that visibility from the save afterwards. On a save that has not reached 255 / 780 subscribers all of them, Camila and Emma included, are saved inactive, which is why the scene dump shows them as inactive variants. Calling the same `OpenHouse` (agent `house open CamilaHouse|EmmaHouse`) activated Jamal, Peter and Yuka at once, and their timetables ran every FSM over 2 accelerated days (TEST_RESULTS 0.4.0).

Hook chosen: none needed to restore them. MSN2 Characters 0.4.0 adds switches instead (`[Neighbours]`, `[Scenes]`): a disabled NPC is taken off his house's `Residents` list at scene load (so opening the house no longer shows him) and hidden, and every couple scene with him is swapped for the partner's own solo FSM in memory (`TimeTableInterval.FsmID`, restored each scene, never saved).

## ActorID
| Value | Status | Notes |
|---|---|---|
| Player, Narrator, Chloe, PoliceOfficer | live | |
| Camila, Emma | gated | houses open at 255 / 780 subscribers |
| Jamal, Peter, Yuka | gated | residents of Camila's / Emma's house, see above |
| OldMan | live (event) | own entity, shown by the Emma-house opener config |
| None, Other | technical | |

## FSMID (85 in the repository)
| Group | Status |
|---|---|
| All Chloe, Camila, Emma, Jamal, Peter, Yuka timetable FSMs, incl. couple scenes `Camila_Sex_Pillary/Bedroom` + `Jamal_Sex_Pillary/Bedroom`, `Emma_Sex_Peter` + `Peter_Sex_Emma`, `Emma_Lesbi_Yuka` + `Yuka_Lesbi_Emma`, `Yuka_Masturbation`, `Yuka_GynChair` + `Peter_GynChair`, `Emma_WebCam_3`, `Peter_WebCam_2` | live / gated (every one entered over 2 accelerated days with both houses open) |
| `*_Sleepy`, `*_Alert`, `Chloe_Seek`, `Male_Alert`, `Cop*`, `CoinTrap/BucketTrap/FlashTrap` | live (event FSMs: sleeping pill, alert, police, traps) |
| `Chloe_Monday`, `TheftChallenge`, `MakeNPCPhotoChallenge` | **missing**: enum only, no FSM in the repository |

## ClothID / ClothingPackID
| Item | Status | Notes |
|---|---|---|
| Chloe Casual/Nightie/Webcam/Bikini/Naked, Camila Casual/Nightie/Naked, Emma Casual/Underwear/Naked, Jamal and Peter Casual/Underwear/Naked, Yuka Maid/Underwear/Naked, Player, OldMan | live / gated | packs worn by FSMs |
| `Yuka_Bunnysuit` (pack, Sexy category) + `Yuka_Bunnysuit_Bunnysuit` | **cut, restored in 0.4.0** | in Yuka's clothes repository and the Addressables catalog, but no FSM ever wears it. `[Restored] YukaBunnysuit`: she wears it 20-22 when she is in her maid outfit |
| `Chloe_Casual_Shirt`, `Chloe_Casual_Shorts`, `Chloe_Casual_Socks`, `Chloe_Casual_Sneakers_Timurio`, `Chloe_Casual_Necklace` | **missing** | ClothIDs only: in no pack and not in the Addressables catalog (an earlier Chloe outfit) |

## LetterID
All 64 letters are in the letters repository (story: Registration ... Club_1-8, Goverment_1-8, LetterFromMom 1-3, gifts, spam, `OpenHouse2/3`, endings `EndClub*`, `EndPolice*`, `EndMotherLetter*`, `EndTrue`). Status: live (sent by subscriber thresholds and story choices). Nothing cut.

## StaticPhotoID
All Chloe/Emma/Camila spots incl. `EmmaHouse_Yuka_Corridor` are live once the houses open; `CrossPromo_1-6` are store promo images. Nothing cut.

## PerkID
All 30 perks are in the perks repository (live). Nothing cut.

## ChallengeType
Theft, PhotoNPC, InstallHiddenCamera, FapOn, Sex, PhotoNPCTrap: all six challenge SOs present (live). Photo/fap challenges pick targets from the open houses' `Residents`, so they include Yuka, Jamal and Peter automatically.

## AchievementID
All live. `ac_sex_uka` (Yuka) and `ac_new_kamila` / `ac_new_emma` confirm Yuka and the two houses are planned content, not leftovers.

## Restorable list (what 0.4.0 does)
| Item | Done |
|---|---|
| Yuka, Jamal, Peter | already live; per-NPC switches added (default on = vanilla) |
| Couple scenes Camila×Jamal, Emma×Peter, Emma×Yuka, Yuka masturbation, gyno chair | already live; per-scene switches added (default on) |
| Yuka's bunny suit | restored (evening shift 20-22), switch `[Restored] YukaBunnysuit` |
| Chloe_Monday FSM, old Chloe casual outfit | not restorable (assets not shipped) |

## Broken or disabled
None of the cut-NPC FSMs threw or stalled in the 2-day run (0 exceptions from MSN2 code; 112 vanilla `OperationCanceledException` from movement tasks cancelled by timetable switches, the same pattern as Chloe alone). Nothing had to be disabled.
