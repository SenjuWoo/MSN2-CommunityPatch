MSN2 Characters 0.4.0 - Lola from My Sexy Neighbor replaces Camila in My Sexy Neighbour 2
by SenjuWoo

Camila's neighbour slot gets Lola from the first game: her body, face, hair and outfits, her name in the
game's text and her portraits in the house HUD. Camila's schedule, animations, sex scenes, photos and
your save stay exactly as they are; only the looks change.

REQUIRES
  BepInEx 5.4.23.5 - included in the MSN2 Community Patch zip.

INSTALL
  Extract into the game folder. Files go to BepInEx\plugins\MSN2\ (MSN2Characters.dll and skins\lola\).

WHAT CHANGES
  - Camila's body, face, hair and eyes are Lola's (re-fitted to Camila's skeleton, so every animation
    and scene lines up; Lola keeps her face, skin, hair and figure, with Camila's height and arm length).
  - Outfits: casual = Lola's shirt, plaid skirt with belt, socks and shoes; night = lace brami, thong
    and slippers; naked stays naked.
  - Her name reads "Lola" in the HUD, phone, journal and job text; house HUD and phone portraits show Lola.

NEIGHBOURS (since 0.4.0)
  Yuka (Emma's maid), Jamal (Camila's housemate) and Peter (Emma's husband) are in the game: they move in
  with Camila and Emma when the story opens their houses. Each of them and each of their scenes can be
  switched off, and Yuka's unused bunny suit is back for her evening shift (20:00-22:00).

OPTIONS (BepInEx\config\senjuwoo.msn2.characters.cfg, restart the game after a change)
  [Skins] Camila = lola     empty = vanilla Camila
  [Neighbours] Yuka / Jamal / Peter = true    false = that neighbour never appears
  [Scenes] CamilaJamal / EmmaPeter / EmmaYuka / YukaMasturbation / YukaGynChair = true
                            false = the scene's hours become ordinary rest/sleep/work time
  [Restored] YukaBunnysuit = true

REMOVE
  Delete BepInEx\plugins\MSN2\MSN2Characters.dll and the skins folder. Camila is back; your save still loads.

KNOWN LIMITS
  - Lola's eyelashes are not shown (her lash geometry has no texture of its own).
  - Camila's voice is kept.
  - Loading the skin takes about 10 seconds in the main menu and about 250 MB of video memory.

LOG
  BepInEx\LogOutput.log, lines starting with [skin].

Source: https://github.com/SenjuWoo/MSN2-CommunityPatch
