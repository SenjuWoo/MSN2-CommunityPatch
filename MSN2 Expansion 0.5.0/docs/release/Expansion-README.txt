MSN2 Expansion 0.5.0 - story jobs and first-game systems for My Sexy Neighbour 2
by SenjuWoo

The first game had a story: a client, a target, a plan, side objectives. This brings that back.
An anonymous big spender, The Collector, sends you a chain of five jobs across Chloe's, Emma's and
Camila's houses, each with a main goal and MSN1-style bonuses (Souvenir, Hidden Camera, Shadow = don't
get spotted, Timed). Bonuses pay extra.

REQUIRES
  BepInEx 5.4.23.5 - included in the MSN2 Community Patch zip (recommended alongside this).

INSTALL
  Extract into the game folder. Files go to BepInEx\plugins\MSN2\.

HOW TO PLAY
  A notification tells you when a job is available.
  J       open/close the job journal
  Enter   accept the selected job / turn it in once the main goal is done / skip dialogue
  , .     previous / next job in the journal
  The tracked job's objectives show on the left side of the screen.
  Emma's and Camila's jobs appear once the base game has opened their houses.
  Keys can be changed in BepInEx\config\senjuwoo.msn2.expansion.cfg (Unity Input System key names).

FIRST-GAME SYSTEMS (0.5.0)
  G       sex gallery: every sex scene you finish is listed; , . select, Enter replays it (no police during a replay)
  K       statistics: photos, naked photos, thefts, traps, scenes, times spotted/caught, money earned
  Souvenir altar   stolen souvenirs stand on the bookshelf in your room; each kind adds +2% job money (max +20%)
  Telescope        at your bedroom window; next to it the phone camera zooms 5x further
  Safes            Emma's two safes have a 4-digit keypad: type digits, Enter tries, hints show digits in place
                   and misplaced; wrong codes beep. Cash and an item inside
  Rain             some days rain for a few hours; your noise carries less (x0.6)
  Difficulty       [Systems] Difficulty = Vanilla | Easy | Normal | Hard (x0.7 / x1 / x1.4 detection, noise, police)
  Each system can be switched off in [Systems] of BepInEx\config\senjuwoo.msn2.expansion.cfg; G/K in [Keys].

WITH THE COMMUNITY PATCH
  Souvenir items are never placed on the 12 theft spots that sit inside furniture and cannot be reached.

SAVES
  Job progress is stored in saveNN.msn2x.json next to the game's own save. The game's save file is
  never changed by this mod; removing the mod leaves your save loadable.

ADDING JOBS
  Drop a folder with a pack.json into BepInEx\plugins\MSN2\packs\. Format: docs\PACK_FORMAT.md.
  Mistakes are listed in BepInEx\LogOutput.log on startup ([packs] lines).

WITH MSN2 CHARACTERS
  If the optional Characters plugin gives a neighbour another look and name (Lola for Camila), the journal,
  tracker, dialogue and notifications use that name.

LOG
  BepInEx\LogOutput.log. Set [Debug] VerboseLog = true in the expansion config to log every event and job change.

Source: https://github.com/SenjuWoo/MSN2-CommunityPatch
