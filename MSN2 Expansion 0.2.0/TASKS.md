# Tasks

## Done (runtime-verified, see TEST_RESULTS.md)
- BepInEx 5.4.23.5 on Unity 6000.2.2 Mono; build/install/launch/agent/package scripts
- Patch 0.1.0: HUD exception spam fix, portraits fix, FPS-cap fix, interrupt timeout guard, graphics options, Low preset fix
- Patch 0.2.0: wardrobe fling fix (the "doors throw me off the map" reports), 12 unreachable theft spawn points taken out of rotation, key remapping on Settings > Controls (+ HUD hint sync)
- Expansion: pack loader + validation, sidecar saves, journal/tracker/dialogue UI, notifications, rewards, chaining, turn-in, souvenir spawns
- Hooks verified in game: photo (actor/house/activity/naked), steal, hidden camera, trap, zone/room enter, alert, hour
- Save/load across restarts; vanilla load with the expansion removed; re-adoption without duplicates
- Pack content checks: camera spots exist in every targeted room; Chloe bath = Shower, Camila yoga = Exercise (gym)

## Partial
- `sex` and `cum_on` hooks: subscribed to SexController.MinigameWin / FapController.OnActorCummed, not yet triggered in a test; no shipped job uses them
- Jobs 3-5 (Emma, trap, Camila): objectives individually verified; not played start to finish
- Graphics options: values applied to URP assets (logged); no per-option visual A/B or low-end GPU measurement
- Interrupt timeout: guard active, normal trap cycle unaffected; the original freeze was not reproduced
- Key remapping: verified with injected key events only (keyboard); gamepad, other languages and physically typed keys untested. Mouse buttons are not rebindable on purpose
- Theft spots: the 12-spot list comes from a probe audit of build 21130045; spots reachable only by an unusual route (standing on furniture, crouch-jumping) may have been counted unreachable

## Investigated, no change
- NPC schedule teleports / wall clipping: not reproduced (see MODDING_PLAN.md); keep `warplog` for new evidence, ask for a save/situation where it happens
- Gallery missing sex scenes: the feature does not exist in MSN2 (only world photos and promo images)

## Next
- 0.3.0 MSN1 systems, in the order picked by the owner: souvenir shelf at home (MSN1 altar), telescope, safe cracking (MSN2 already has 2 `Safe` OpenCloseEntity objects and a LockPickController: check them first), weather/rain; optional: statistics screen
- Candidate: sex-scene gallery with replay (needs research into how scenes start)
- More packs; localisation keys for pack text
