MSN2 Community Patch 0.2.0 - My Sexy Neighbour 2 (Steam build 21130045, v10.12.2025 1229)
by SenjuWoo

INSTALL
  Extract the zip into the game folder (the one with "My Sexy Neighbour 2.exe"), overwrite if asked.
  It contains BepInEx 5.4.23.5 (mod loader, MIT licence) and the patch. Start the game normally.

UNINSTALL
  Delete BepInEx\plugins\MSN2\MSN2CommunityPatch.dll (keeps BepInEx), or delete winhttp.dll,
  doorstop_config.ini, .doorstop_version and the BepInEx folder to remove everything.
  Saves are never modified by the patch.

WHAT IT FIXES (measured on the developer's PC, same save and spot)
  - Exception thrown every frame by the house/room HUD whenever any NPC leaves any house
    (about 150 errors per second, log growing ~4 MB per minute). Fixed: 151 -> 186 FPS, 1% lows 93 -> 133.
    Also fixes the resident portraits showing the wrong house and not updating when someone walks in.
  - Medium and Low presets were hard-capped at 60 FPS (only High had VSync), which is why Low ran
    worse than High. Now every preset follows your VSync / FPS-limit setting.
  - NPCs could freeze forever when one of their actions ignored an interrupt (traps, police, sex):
    the game now waits at most 8 seconds, then continues.
  - Low preset allowed more per-object lights than Medium; Low now caps them at 3 and drops lamp shadows.
  - Hiding in a wardrobe / leaving it threw the player across the map or off target when the game ran at a low
    frame rate (the "doors throw me over the edge of the map" reports). The game moved the player with two
    competing tweens; measured jumps of 4 m per frame at ~9 FPS and 11 m at ~3 FPS, now under 1 m.
  - 12 of the 148 theft spawn points sit inside furniture (armchairs, a utility cart, a gym shelf, a corner
    cabinet, kitchen blocker colliders) where an item can never be reached, even with every drawer open.
    Theft tasks (and Expansion souvenirs) no longer use them.

KEY REMAPPING (new)
  Settings > Controls (main menu or pause menu) is now live: click a key, then press the new one. Esc cancels.
  Move asks for four keys (forward, left, back, right), Abilities for two. A key already used by another action
  swaps with it. "Reset all keys" restores the defaults. The arrow keys stay as alternate movement keys.
  Choices are saved in BepInEx\config\senjuwoo.msn2.bindings.json (delete it to reset). Mouse and gamepad
  bindings are unchanged. The on-screen hints (Q, Tab, E, R, F) follow your keys.

SETTINGS
  BepInEx\config\senjuwoo.msn2.communitypatch.cfg (created after the first launch; edit with the game closed,
  or live in-game with BepInEx ConfigurationManager if you have it).
  [Graphics] VSync, FrameRateLimit, MSAA, RenderScale, Upscaler (FSR etc.), ShadowDistance,
             AdditionalLightShadows, AdditionalShadowAtlas, SoftShadows, FixLowPreset
  [Fixes]    HouseHudExceptionSpam, NpcInterruptTimeout, WardrobeTween, TheftSpots, KeyRemapping:
             each fix can be switched off
  [Debug]    VerboseLog (off by default), developer tools

LOG
  BepInEx\LogOutput.log - shows the patch version, which fixes applied, and any fix that failed
  (a failed fix leaves the original game behaviour in place). Set [Debug] VerboseLog = true for details.

Source and issue tracker: https://github.com/SenjuWoo/MSN2-CommunityPatch
