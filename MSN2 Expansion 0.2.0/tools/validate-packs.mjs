// CI check for job packs (no game files needed). The in-game PackLoader is the authority; this mirrors its rules
// with the enum values observed in game build 21130045 so broken packs fail before release.
// usage: node validate-packs.mjs <packsDir>
import fs from 'node:fs';
import path from 'node:path';

const E = {
  type: ['photo', 'steal', 'hidden_camera', 'trap', 'sex', 'cum_on', 'enter'],
  constraint: ['no_alert', 'within_hours'],
  actor: ['None', 'Player', 'Chloe', 'Narrator', 'Camila', 'Jamal', 'Emma', 'Peter', 'Yuka', 'OldMan', 'PoliceOfficer', 'Other'],
  house: ['None', 'ChloeHouse', 'CamilaHouse', 'EmmaHouse'],
  activity: ['None', 'Undefined', 'Cooking', 'Rest', 'Eating', 'Sleep', 'Exercise', 'ChangingClothes', 'Shower', 'Toilet', 'Masturbation', 'Trapped', 'Sex', 'WaitSex', 'WaitSexAndReady', 'Housework'],
  trap: ['None', 'Coin', 'Bucket', 'Flash'],
  item: ['PinkDildo', 'HairElastic', 'Handkerchief', 'Comb', 'Razor', 'Toothbrush', 'Socks', 'Panties', 'Buttplug'],
  rewardItem: ['CoinTrap', 'BucketTrap', 'FlashTrap', 'SleepingPill', 'Aphrodisiac', 'DisappearanceSpray'],
  roomPrefix: ['ChloesHouse_', 'CamilaHouse_', 'EmmaHouse_'],
};
const PACK_KEYS = ['id', 'name', 'version', 'author', 'minFramework', 'missions'];
const MISSION_KEYS = ['id', 'title', 'giver', 'description', 'unlock', 'intro', 'objectives', 'bonus', 'reward', 'outro'];
const UNLOCK_KEYS = ['after', 'houses', 'minDay', 'minLevel', 'fromHour', 'toHour'];
const OBJ_KEYS = ['id', 'text', 'type', 'actor', 'house', 'room', 'item', 'trap', 'activity', 'naked', 'count', 'hours', 'reward', 'spawn'];

const root = process.argv[2];
const errors = [];
const ids = new Set();
const err = (f, w, m) => errors.push(`${f}: ${w}: ${m}`);
const keys = (f, w, o, allowed) => Object.keys(o).filter(k => !allowed.includes(k)).forEach(k => err(f, w, `unknown key '${k}'`));
const one = (f, w, field, v, list) => { if (v !== undefined && !list.includes(v)) err(f, w, `${field} '${v}' not in ${list.join(',')}`); };

const files = fs.readdirSync(root, { recursive: true }).filter(p => path.basename(p) === 'pack.json').map(p => path.join(root, p));
if (files.length === 0) errors.push(`no pack.json under ${root}`);
for (const f of files) {
  let p;
  try { p = JSON.parse(fs.readFileSync(f, 'utf8')); } catch (e) { err(f, 'json', e.message); continue; }
  keys(f, 'pack', p, PACK_KEYS);
  if (!p.id) err(f, 'pack', 'id required');
  const missions = p.missions ?? [];
  for (const m of missions) {
    const w = `mission '${m.id}'`;
    keys(f, w, m, MISSION_KEYS);
    if (!m.id || ids.has(m.id)) err(f, w, 'missing or duplicate id'); else ids.add(m.id);
    if (!m.title) err(f, w, 'title required');
    if (!(m.objectives ?? []).length) err(f, w, 'needs an objective');
    const u = m.unlock ?? {};
    keys(f, w, u, UNLOCK_KEYS);
    (u.houses ?? []).forEach(h => one(f, w, 'unlock house', h, E.house));
    const from = u.fromHour ?? 0, to = u.toHour ?? 24;
    if (!(from >= 0 && to <= 24 && from < to)) err(f, w, 'bad unlock hours');
    const seen = new Set();
    for (const [o, bonus] of [...(m.objectives ?? []).map(o => [o, false]), ...(m.bonus ?? []).map(o => [o, true])]) {
      const ow = `${w} ${bonus ? 'bonus' : 'objective'} '${o.id}'`;
      keys(f, ow, o, OBJ_KEYS);
      if (!o.id || !o.text) err(f, ow, 'id and text required');
      if (seen.has(o.id)) err(f, ow, 'duplicate objective id'); seen.add(o.id);
      if (!E.type.includes(o.type) && !(bonus && E.constraint.includes(o.type))) err(f, ow, `bad type '${o.type}'`);
      if (o.type === 'within_hours' && !(o.hours > 0)) err(f, ow, 'within_hours needs hours > 0');
      one(f, ow, 'actor', o.actor, E.actor); one(f, ow, 'house', o.house, E.house);
      one(f, ow, 'activity', o.activity, E.activity); one(f, ow, 'trap', o.trap, E.trap); one(f, ow, 'item', o.item, E.item);
      if (o.room !== undefined && !E.roomPrefix.some(px => o.room.startsWith(px))) err(f, ow, `room '${o.room}' does not look like a SubZoneTypes name`);
      if (o.spawn && (o.type !== 'steal' || !o.item || !o.house)) err(f, ow, 'spawn needs type steal with item and house');
      Object.keys(o.reward?.items ?? {}).forEach(k => one(f, ow, 'reward item', k, E.rewardItem));
    }
    Object.keys(m.reward?.items ?? {}).forEach(k => one(f, w, 'reward item', k, E.rewardItem));
  }
}
for (const f of files) {
  const p = JSON.parse(fs.readFileSync(f, 'utf8'));
  for (const m of p.missions ?? []) for (const a of m.unlock?.after ?? []) if (!ids.has(a)) err(f, `mission '${m.id}'`, `unlock.after unknown '${a}'`);
}
if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
console.log(`OK: ${files.length} pack(s), ${ids.size} job(s)`);
