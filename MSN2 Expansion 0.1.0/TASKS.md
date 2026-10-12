# Tasks

## Done (runtime-verified, see TEST_RESULTS.md)
- BepInEx 5.4.23.5 on Unity 6000.2.2 Mono; build/install/launch/agent/package scripts
- Patch: HUD exception spam fix, portraits fix, FPS-cap fix, interrupt timeout guard, graphics options, Low preset fix
- Expansion: pack loader + validation, sidecar saves, journal/tracker/dialogue UI, notifications, rewards, chaining, turn-in, souvenir spawns
- Hooks verified in game: photo (actor/house/activity/naked), steal, hidden camera, trap, zone/room enter, alert, hour
- Save/load across restarts; vanilla load with the expansion removed; re-adoption without duplicates
- Pack content checks: camera spots exist in every targeted room; Chloe bath = Shower, Camila yoga = Exercise (gym)

## Partial
- `sex` and `cum_on` hooks: subscribed to SexController.MinigameWin / FapController.OnActorCummed, not yet triggered in a test; no shipped job uses them
- Jobs 3-5 (Emma, trap, Camila): objectives individually verified; not played start to finish
- Graphics options: values applied to URP assets (logged); no per-option visual A/B or low-end GPU measurement
- Interrupt timeout: guard active, normal trap cycle unaffected; the original freeze was not reproduced

## Next
- Review-reported bugs still open: door interaction flinging the player, NPC schedule teleports, unreachable stealables in drawers, gallery missing sex scenes, key remapping
- More MSN1 systems: telescope, safe cracking, souvenir shelf at home, weather, statistics screen
- More packs; localisation keys for pack text
