using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Inventory;
using Game.PC;
using UnityEngine;
using _Game.Systems.NotificationsController;

namespace MSN2Expansion
{
    // Mission lifecycle: Locked -> Available (unlock rules met) -> Active (player accepts) -> Completed (all objectives done).
    // Bonus objectives pay extra; constraint bonuses (no_alert, within_hours) start satisfied and can only be failed.
    public class MissionRunner
    {
        public readonly List<Pack> Packs;
        public readonly List<string> Errors;
        public readonly Dictionary<string, Mission> ById;
        public GameHooks Hooks { get; private set; }
        public event Action<Mission, MissionState> Changed;

        private float _nextUnlockCheck;

        public MissionRunner(List<Pack> packs, List<string> errors)
        {
            Packs = packs;
            Errors = errors;
            ById = packs.SelectMany(p => p.Missions).ToDictionary(m => m.Id);
        }

        public static ExpansionState S => Sidecar.State;

        public MissionState StateOf(Mission m)
        {
            if (!S.Missions.TryGetValue(m.Id, out var st)) S.Missions[m.Id] = st = new MissionState();
            return st;
        }

        public void Attach(GameHooks hooks)
        {
            Hooks = hooks;
            hooks.OnEvent += Handle;
            _nextUnlockCheck = 0;
            ExpansionPlugin.Log.LogInfo($"[missions] attached to game scene; {ById.Count} missions from {Packs.Count} packs");
            foreach (var m in ById.Values.Where(x => StateOf(x).Status == MissionStatus.Active)) Souvenirs.Ensure(this, m, StateOf(m));
        }

        public void Detach()
        {
            if (Hooks != null) Hooks.OnEvent -= Handle;
            Hooks = null;
        }

        public void OnStateReplaced() { _nextUnlockCheck = 0; Changed?.Invoke(null, null); }

        public void Tick()
        {
            if (Hooks == null) return;
            Hooks.Tick();
            if (Time.unscaledTime < _nextUnlockCheck) return;
            _nextUnlockCheck = Time.unscaledTime + 1f;
            foreach (var m in ById.Values)
            {
                var st = StateOf(m);
                if (st.Status == MissionStatus.Locked && Unlocked(m)) SetStatus(m, st, MissionStatus.Available);
            }
        }

        private bool Unlocked(Mission m)
        {
            var u = m.Unlock;
            if (u.After.Any(a => StateOf(ById[a]).Status != MissionStatus.Completed)) return false;
            if (Hooks.Time.Days + 1 < u.MinDay) return false;
            var opened = UnityEngine.Object.FindFirstObjectByType<GameDataAccessor>()?.GameData?.OpenedHouses;
            if (u.Houses.Count > 0 && (opened == null || u.Houses.Any(h => !opened.Contains((Game.Zones.HouseZoneID)Enum.Parse(typeof(Game.Zones.HouseZoneID), h))))) return false;
            int hour = Hooks.Time.Hours;
            if (hour < u.FromHour || hour >= u.ToHour) return false;
            var pc = UnityEngine.Object.FindFirstObjectByType<PCController>();
            return u.MinLevel <= 0 || (pc && pc.GetCurrentLevel() >= u.MinLevel);
        }

        public bool Accept(string id)
        {
            if (!ById.TryGetValue(id, out var m)) return false;
            var st = StateOf(m);
            if (st.Status != MissionStatus.Available) return false;
            st.Progress.Clear();
            st.BonusFailed.Clear();
            st.StartedAtMinutes = Hooks?.Time.TotalMinutesFloat ?? 0;
            S.TrackedMission = id;
            SetStatus(m, st, MissionStatus.Active);
            Souvenirs.Ensure(this, m, st);
            ExpansionHud.Instance?.PlayDialogue(m.Intro);
            return true;
        }

        public bool IsDone(Mission m, Objective o)
        {
            var st = StateOf(m);
            if (PackLoader.ConstraintTypes.Contains(o.Type)) return !st.BonusFailed.Contains(o.Id);
            return st.Progress.TryGetValue(o.Id, out var n) && n >= o.Count;
        }

        public int ProgressOf(Mission m, Objective o) => StateOf(m).Progress.TryGetValue(o.Id, out var n) ? n : 0;

        private void Handle(GameEvent e)
        {
            foreach (var m in ById.Values.ToList())
            {
                var st = StateOf(m);
                if (st.Status != MissionStatus.Active) continue;
                bool wasReady = MainDone(m);
                bool changed = false;
                foreach (var o in m.Objectives.Concat(m.Bonus))
                {
                    if (o.Type == "no_alert" && e.Type == "alert" && !st.BonusFailed.Contains(o.Id) && (o.Actor == null || o.Actor == e.Actor))
                    {
                        st.BonusFailed.Add(o.Id); changed = true;
                        Notify("Bonus failed", o.Text);
                    }
                    else if (o.Type == "within_hours" && e.Type == "hour" && !st.BonusFailed.Contains(o.Id) && Hooks.Time.TotalMinutesFloat - st.StartedAtMinutes > o.Hours * 60f)
                    {
                        st.BonusFailed.Add(o.Id); changed = true;
                        Notify("Bonus failed", o.Text);
                    }
                    else if (o.Type == e.Type && !IsDone(m, o) && Matches(o, e))
                    {
                        st.Progress[o.Id] = ProgressOf(m, o) + 1;
                        if (e.EntityId != null) Souvenirs.OnStolen(this, st, e.EntityId);
                        changed = true;
                        bool main = m.Objectives.Contains(o);
                        Notify(main ? m.Title : "Bonus", o.Count > 1 ? $"{o.Text} ({ProgressOf(m, o)}/{o.Count})" : $"{o.Text} - done");
                    }
                }
                if (!changed) continue;
                // MSN1 style: the job ends on turn-in, so bonuses can still be collected after the main objective.
                // Auto turn-in once nothing collectable is left.
                bool bonusesLeft = m.Bonus.Any(b => !PackLoader.ConstraintTypes.Contains(b.Type) && !IsDone(m, b));
                if (MainDone(m) && !bonusesLeft) Complete(m, st);
                else
                {
                    if (MainDone(m) && !wasReady) Notify(m.Title, $"Main job done. Grab the bonuses or turn it in: [{ExpansionPlugin.JournalKey.Value}] then [{ExpansionPlugin.AcceptKey.Value}]");
                    Changed?.Invoke(m, st);
                }
            }
        }

        public bool MainDone(Mission m) => m.Objectives.All(o => IsDone(m, o));

        public bool TurnIn(string id)
        {
            if (!ById.TryGetValue(id, out var m) || StateOf(m).Status != MissionStatus.Active || !MainDone(m)) return false;
            Complete(m, StateOf(m));
            return true;
        }

        public static bool Matches(Objective o, GameEvent e) =>
            (o.Actor == null || o.Actor == e.Actor) && (o.House == null || o.House == e.House) && (o.Room == null || o.Room == e.Room) &&
            (o.Item == null || o.Item == e.Item) && (o.Trap == null || o.Trap == e.Trap) && (o.Activity == null || o.Activity == e.Activity) &&
            (o.Naked == null || o.Naked == e.Naked);

        private void Complete(Mission m, MissionState st)
        {
            foreach (var o in m.Bonus.Where(o => o.Type == "within_hours" && Hooks != null && Hooks.Time.TotalMinutesFloat - st.StartedAtMinutes > o.Hours * 60f && !st.BonusFailed.Contains(o.Id)))
                st.BonusFailed.Add(o.Id);
            int money = m.Reward.Money, subs = m.Reward.Subscribers;
            var items = new Dictionary<string, int>(m.Reward.Items);
            int bonuses = 0;
            foreach (var b in m.Bonus.Where(b => IsDone(m, b)))
            {
                bonuses++;
                money += b.Reward.Money; subs += b.Reward.Subscribers;
                foreach (var kv in b.Reward.Items) items[kv.Key] = (items.TryGetValue(kv.Key, out var n) ? n : 0) + kv.Value;
            }
            GrantRewards(money, subs, items);
            Souvenirs.Cleanup(this, st);
            st.CompletedDay = Hooks?.Time.Days + 1 ?? -1;
            if (S.TrackedMission == m.Id) S.TrackedMission = ById.Values.FirstOrDefault(x => StateOf(x).Status == MissionStatus.Active && x != m)?.Id;
            SetStatus(m, st, MissionStatus.Completed);
            Notify($"Job complete: {m.Title}", $"+${money}, +{subs} subscribers, bonus {bonuses}/{m.Bonus.Count}");
            ExpansionHud.Instance?.PlayDialogue(m.Outro);
            ExpansionPlugin.Log.LogInfo($"[missions] completed {m.Id}: money={money} subs={subs} items={string.Join(",", items.Select(k => k.Key + "x" + k.Value))} bonus={bonuses}/{m.Bonus.Count}");
        }

        private static void GrantRewards(int money, int subs, Dictionary<string, int> items)
        {
            var data = UnityEngine.Object.FindFirstObjectByType<GameDataAccessor>();
            if (money > 0 && data) data.AddMoney(money);
            var pc = UnityEngine.Object.FindFirstObjectByType<PCController>();
            if (subs > 0 && pc) pc.IncrementSubscribersCount(subs);
            var inv = UnityEngine.Object.FindFirstObjectByType<InventoryController>();
            foreach (var kv in items)
                if (inv && Enum.TryParse<ItemID>(kv.Key, out var id)) inv.IncrementItem(id, kv.Value);
        }

        private void SetStatus(Mission m, MissionState st, MissionStatus status)
        {
            st.Status = status;
            ExpansionPlugin.Log.LogInfo($"[missions] {m.Id} -> {status}");
            if (status == MissionStatus.Available) Notify($"New job from {m.Giver}", $"{m.Title} - press {ExpansionPlugin.JournalKey.Value} to read");
            Changed?.Invoke(m, st);
        }

        public static void Notify(string title, string text)
        {
            var n = UnityEngine.Object.FindFirstObjectByType<NotificationsController>();
            try { if (n) n.ShowNotification(title, text, NotificationsController.NotificationType.Default); }
            catch (Exception e) { ExpansionPlugin.Log.LogWarning($"[notify] {title}: {e.Message}"); }
        }
    }
}
