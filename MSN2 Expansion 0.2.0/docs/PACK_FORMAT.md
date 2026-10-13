# MSN2 Expansion — job pack format (framework 0.1.0)

A pack is a folder with a `pack.json` inside `BepInEx/plugins/MSN2/packs/`. Packs load in alphabetical folder order; later packs may chain off earlier packs' jobs. Every value is checked against the game's own enums at startup. Invalid packs are skipped and each problem is logged to `BepInEx/LogOutput.log` as `[packs] <file>: <where>: <problem>`. Unknown keys are errors (typos never pass silently).

```json
{
  "id": "my-pack", "name": "My Pack", "version": "1.0.0", "author": "you", "minFramework": "0.1.0",
  "missions": [ { ...job... } ]
}
```

## Job
| key | meaning |
|---|---|
| `id`, `title`, `giver`, `description` | text shown in the journal (`id` is unique across all packs) |
| `unlock.after` | job ids that must be completed first |
| `unlock.minDay` / `minLevel` | in-game day (1-based) / channel level |
| `unlock.fromHour`, `toHour` | job only appears during these hours (0–24) |
| `unlock.houses` | houses vanilla progression must have opened: `ChloeHouse`, `CamilaHouse`, `EmmaHouse` |
| `intro`, `outro` | dialogue lines `{ "speaker": "...", "text": "..." }`, shown on accept / completion |
| `objectives` | all must be done before the job can be turned in |
| `bonus` | optional; each pays its own `reward` on turn-in |
| `reward` | `{ "money": 0, "subscribers": 0, "items": { "BucketTrap": 1 } }` (items: `CoinTrap`, `BucketTrap`, `FlashTrap`, `SleepingPill`, `Aphrodisiac`, `DisappearanceSpray`) |

The job ends when the player turns it in from the journal, or automatically once nothing collectable is left.

## Objective
`{ "id": "...", "text": "...", "type": "...", "count": 1, ...filters }`. Every filter you set must match; unset filters match anything.

| type | fired when | filters that apply |
|---|---|---|
| `photo` | the phone camera shot includes the NPC (game's own visibility test) | `actor`, `house`, `room`, `activity`, `naked`, `trap` |
| `steal` | the player takes a stealable | `item`, `house`, `room`; `"spawn": true` places the item on a free theft spot in `house` when the job starts |
| `hidden_camera` | a hidden camera finishes installing | `house`, `room` |
| `trap` | an NPC gets caught in a trap | `actor`, `trap`, `house`, `room` |
| `sex` | the player wins the sex minigame | `actor`, `house` |
| `cum_on` | the fap minigame finishes on an NPC | `actor` |
| `enter` | the player enters a house / room | `house`, `room` |

Bonus-only constraints (start satisfied, can only fail): `no_alert` (fails when any NPC — or `actor` — reaches full alert), `within_hours` with `"hours": N` (fails after N in-game hours).

Values: `actor` = `Chloe`, `Camila`, `Emma`, `Yuka`, `Jamal`, `Peter`, `OldMan`. `activity` = `Cooking`, `Rest`, `Eating`, `Sleep`, `Exercise`, `ChangingClothes`, `Shower`, `Toilet`, `Masturbation`, `Trapped`, `Sex`, `WaitSex`, `WaitSexAndReady`, `Housework`. `trap` = `Coin`, `Bucket`, `Flash`. `item` = `PinkDildo`, `HairElastic`, `Handkerchief`, `Comb`, `Razor`, `Toothbrush`, `Socks`, `Panties`, `Buttplug`. `room` = any `SubZoneTypes` name, e.g. `ChloesHouse_Bathroom`, `EmmaHouse_Bedroom`, `CamilaHouse_RedRoom` (full list is printed in the error message if you get one wrong).

## Schedules worth knowing (vanilla timetables, observed)
Chloe: bath 11–12 (`Shower`), training 07–09, webcam 20–24. Camila: yoga 20–21 in the gym (`Exercise`), red-room sex 15–17. Emma: bath 14–15 and 20–21, webcam 22–24. NPCs in houses vanilla has not opened yet stand idle, so gate those jobs with `unlock.houses`.
