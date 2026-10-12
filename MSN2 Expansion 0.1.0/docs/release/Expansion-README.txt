MSN2 Expansion 0.1.0 - "Neighbourhood Classics" story jobs for My Sexy Neighbour 2
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

SAVES
  Job progress is stored in saveNN.msn2x.json next to the game's own save. The game's save file is
  never changed by this mod; removing the mod leaves your save loadable.

ADDING JOBS
  Drop a folder with a pack.json into BepInEx\plugins\MSN2\packs\. Format: docs\PACK_FORMAT.md.
  Mistakes are listed in BepInEx\LogOutput.log on startup ([packs] lines).

LOG
  BepInEx\LogOutput.log. Set [Debug] VerboseLog = true in the expansion config to log every event and job change.

Source: https://github.com/SenjuWoo/MSN2-CommunityPatch
