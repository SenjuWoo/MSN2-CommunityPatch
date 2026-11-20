using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Configuration;
using Game.Entities;
using Game.Sex;
using HarmonyLib;
using UnityEngine;

namespace MSN2Expansion.Systems
{
    // MSN1 systems rebuilt on MSN2's own controllers (0.5.0). Each one has a [Systems] switch; all state lives in the
    // expansion sidecar (schema 2), never in the vanilla save.
    public static class SystemsConfig
    {
        public static ConfigEntry<bool> Gallery, Altar, Telescope, Safes, Rain, Statistics;
        public static ConfigEntry<string> Difficulty, GalleryKey, StatsKey;
        public static ConfigEntry<float> RainChance;

        public static void Bind(ConfigFile c)
        {
            Gallery = c.Bind("Systems", "SexGallery", true, "Sex gallery: every sex scene you finish is listed; replay it from the list (G).");
            Statistics = c.Bind("Systems", "Statistics", true, "Statistics screen (K): photos, thefts, traps, scenes, times spotted/caught, money earned.");
            Altar = c.Bind("Systems", "SouvenirAltar", true, "Stolen souvenirs are displayed on the bookshelf in your room; each kind adds +2% to job money (max +20%).");
            Telescope = c.Bind("Systems", "Telescope", true, "A telescope at your window: next to it the phone camera zooms 5x further; photos count as usual.");
            Safes = c.Bind("Systems", "SafeCracking", true, "The two safes in Emma's house get a 4-digit keypad (Mastermind hints, wrong codes make noise) with a cash and item reward.");
            Rain = c.Bind("Systems", "Rain", true, "Rainy spells on some days: rain outside, rain sound, your noise carries less (x0.6).");
            RainChance = c.Bind("Systems", "RainChance", 0.35f, new ConfigDescription("Chance that a day has a rainy spell.", new AcceptableValueRange<float>(0f, 1f)));
            Difficulty = c.Bind("Systems", "Difficulty", "Vanilla", new ConfigDescription("Detection preset on top of the game's own difficulty: Vanilla, Easy (x0.7 detection/noise/police), Normal (x1), Hard (x1.4).", new AcceptableValueList<string>("Vanilla", "Easy", "Normal", "Hard")));
            GalleryKey = c.Bind("Keys", "Gallery", "G", "Open/close the sex gallery.");
            StatsKey = c.Bind("Keys", "Statistics", "K", "Open/close the statistics screen.");
        }
    }

    public class SystemsRuntime : MonoBehaviour
    {
        public static SystemsRuntime Instance;
        private GameHooks _hooks;
        private int _lastMoney = -1;
        private bool _gameOverWasOpen;
        private Game.Views.GameOverView _gameOver;
        private float _nextSlow;
        private enum Panel { None, Gallery, Stats }
        private Panel _panel;
        private int _sel;
        public bool Replaying;
        private float _replayAt;

        private static ExpansionState S => Sidecar.State;
        private static ExpansionHud Hud => ExpansionHud.Instance;

        public static void Patch(Harmony h)
        {
            Safes.Patch(h);
            Rain.Patch(h);
            Difficulty.Patch(h);
            // a gallery replay is a safe re-watch: nobody calls the police while it runs
            h.Patch(AccessTools.Method(typeof(Game.Police.PoliceController), nameof(Game.Police.PoliceController.CallCops)),
                prefix: new HarmonyMethod(typeof(SystemsRuntime), nameof(CallCopsPrefix)));
        }

        private static bool CallCopsPrefix()
        {
            if (Instance == null || !Instance.Replaying) return true;
            ExpansionPlugin.Log.LogInfo("[gallery] police call ignored during replay");
            return false;
        }

        private void Awake() => Instance = this;

        private void Update()
        {
            var hooks = ExpansionPlugin.Runner?.Hooks;
            if (hooks != _hooks)
            {
                if (_hooks != null) _hooks.OnEvent -= OnEvent;
                _hooks = hooks;
                _lastMoney = -1;
                _panel = Panel.None;
                if (_hooks != null)
                {
                    _hooks.OnEvent += OnEvent;
                    Try("altar", Altar.Build);
                    Try("telescope", Telescope.Build);
                    Try("rain", () => Rain.Attach(_hooks));
                    ExpansionPlugin.Log.LogInfo($"[systems] attached: gallery={SystemsConfig.Gallery.Value} stats={SystemsConfig.Statistics.Value} altar={SystemsConfig.Altar.Value} telescope={SystemsConfig.Telescope.Value} safes={SystemsConfig.Safes.Value} rain={SystemsConfig.Rain.Value} difficulty={SystemsConfig.Difficulty.Value}; sidecar schema {S.Schema}, {S.Gallery.Count} gallery scenes, {S.Trophies.Count} trophies, {S.SafesOpened.Count} safes opened");
                }
                else Rain.Detach();
            }
            if (_hooks == null) return;

            if (Time.unscaledTime > _nextSlow)
            {
                _nextSlow = Time.unscaledTime + 1f;
                TrackMoney();
                TrackCaught();
                // a replay that ends without a win (minigame lost / aborted) must not swallow the next real scene
                if (Replaying && Time.unscaledTime - _replayAt > 5f && !(FindFirstObjectByType<SexController>()?.IsInSex ?? false)) Replaying = false;
            }
            Try("telescope", Telescope.Tick);
            Try("rain", Rain.Tick);
            Try("safes", Safes.Tick);
            HandlePanels();
        }

        private static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { ExpansionPlugin.Log.LogError($"[systems] {what} failed: {e}"); }
        }

        // ---------- statistics
        public static void Inc(string key, int n = 1)
        {
            if (!SystemsConfig.Statistics.Value || n == 0) return;
            S.Stats.TryGetValue(key, out var v);
            S.Stats[key] = v + n;
        }

        private void OnEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case "photo": Inc("photos"); Inc("photos:" + e.Actor); if (e.Naked) Inc("nakedPhotos"); break;
                case "steal": Inc("thefts"); if (SystemsConfig.Altar.Value && !string.IsNullOrEmpty(e.Item)) { S.Trophies.Add(e.Item); Try("altar", Altar.Build); } break;
                case "trap": Inc("traps"); break;
                case "hidden_camera": Inc("hiddenCameras"); break;
                case "cum_on": Inc("faps"); break;
                case "alert": Inc("timesSpotted"); break;
                case "sex":
                    Inc("sexScenes"); Inc("sex:" + e.Actor);
                    if (SystemsConfig.Gallery.Value && !string.IsNullOrEmpty(e.EntityId)) RecordScene(e);
                    break;
            }
        }

        private void TrackMoney()
        {
            var data = FindFirstObjectByType<Game.Core.GameDataAccessor>()?.GameData;
            if (data == null) return;
            int m = data.Money;
            if (_lastMoney >= 0 && m > _lastMoney) Inc("moneyEarned", m - _lastMoney);
            _lastMoney = m;
        }

        private void TrackCaught()
        {
            if (!_gameOver) _gameOver = FindFirstObjectByType<Game.Views.GameOverView>(FindObjectsInactive.Include);
            bool open = _gameOver && _gameOver.IsOpened;
            if (open && !_gameOverWasOpen) Inc("timesCaught");
            _gameOverWasOpen = open;
        }

        public static string StatsText()
        {
            string V(string k) => S.Stats.TryGetValue(k, out var v) ? v.ToString("N0") : "0";
            var sb = new StringBuilder();
            sb.AppendLine($"Photos taken            {V("photos")}   (naked {V("nakedPhotos")})");
            sb.AppendLine($"Items stolen            {V("thefts")}   (trophies on the shelf: {S.Trophies.Distinct().Count()} kinds)");
            sb.AppendLine($"NPCs trapped            {V("traps")}");
            sb.AppendLine($"Hidden cameras placed   {V("hiddenCameras")}");
            sb.AppendLine($"Sex scenes              {V("sexScenes")}");
            sb.AppendLine($"Fap finishes on NPCs    {V("faps")}");
            sb.AppendLine($"Times spotted           {V("timesSpotted")}");
            sb.AppendLine($"Times caught            {V("timesCaught")}");
            sb.AppendLine($"Money earned            ${V("moneyEarned")}");
            sb.AppendLine($"Safes cracked           {S.SafesOpened.Count}/2");
            var per = S.Stats.Where(k => k.Key.StartsWith("photos:")).OrderByDescending(k => k.Value).Select(k => $"{k.Key.Substring(7)} {k.Value}").ToArray();
            if (per.Length > 0) sb.AppendLine($"\n<color=#ffd166>Photos by neighbour:</color> {string.Join(", ", per)}");
            if (SystemsConfig.Difficulty.Value != "Vanilla") sb.AppendLine($"\nDifficulty preset: {SystemsConfig.Difficulty.Value}");
            return sb.ToString();
        }

        // ---------- sex gallery
        private void RecordScene(GameEvent e)
        {
            if (Replaying) { Replaying = false; return; }   // a replay does not add a new entry
            var g = S.Gallery.FirstOrDefault(x => x.Actor == e.Actor && x.EntityId == e.EntityId);
            if (g == null)
            {
                S.Gallery.Add(g = new GalleryEntry { Actor = e.Actor, EntityId = e.EntityId, Place = Pretty(e.Item, e.Room), House = e.House, Day = (_hooks?.Time.Days ?? 0) + 1 });
                MissionRunner.Notify("Sex gallery", $"New scene unlocked: {e.Actor}, {g.Place}. Press {SystemsConfig.GalleryKey.Value} to replay it.");
            }
            g.Times++;
        }

        private static string Pretty(string entityName, string room)
        {
            var place = (entityName ?? "").Replace(" - BedEntity", "").Replace("Prefab", "").Replace("BedEntity", "bed").Trim(' ', '-');
            if (string.IsNullOrEmpty(place) || place == "bed") place = "bed";
            if (!string.IsNullOrEmpty(room)) place += " (" + room.Replace("sHouse_", "House_").Replace("House_", "'s house, ").Replace("_", " ") + ")";
            return place;
        }

        private void HandlePanels()
        {
            if (Hud == null) return;
            if (SystemsConfig.Gallery.Value && Hud.KeyPressed(SystemsConfig.GalleryKey.Value)) { _panel = _panel == Panel.Gallery ? Panel.None : Panel.Gallery; _sel = 0; }
            if (SystemsConfig.Statistics.Value && Hud.KeyPressed(SystemsConfig.StatsKey.Value)) _panel = _panel == Panel.Stats ? Panel.None : Panel.Stats;
            if (Safes.KeypadOpen) return;   // the safe keypad owns the panel
            if (_panel == Panel.None) { if (Hud.SystemPanelOpen) Hud.HideSystem(); return; }
            if (_panel == Panel.Stats)
            {
                Hud.ShowSystem("STATISTICS", StatsText(), $"[{SystemsConfig.StatsKey.Value}] close");
                return;
            }
            var list = S.Gallery;
            if (list.Count > 0)
            {
                if (Hud.KeyPressed(ExpansionPlugin.NextKey.Value)) _sel++;
                if (Hud.KeyPressed(ExpansionPlugin.PrevKey.Value)) _sel--;
                _sel = (_sel % list.Count + list.Count) % list.Count;
            }
            var sb = new StringBuilder();
            if (list.Count == 0) sb.AppendLine("No scenes yet. Every sex scene you finish is listed here and can be replayed.");
            for (int i = 0; i < list.Count; i++)
                sb.AppendLine($"{(i == _sel ? "<color=#ff5ca8>></color> " : "  ")}{list[i].Actor}  -  {list[i].Place}   <color=#bbbbbb>day {list[i].Day}, {list[i].Times}x</color>");
            string status = null;
            if (list.Count > 0 && Hud.KeyPressed(ExpansionPlugin.AcceptKey.Value)) status = StartReplay(list[_sel]);
            Hud.ShowSystem("SEX GALLERY", sb.ToString() + (status != null ? $"\n<color=#ffd166>{status}</color>" : ""),
                $"[{ExpansionPlugin.PrevKey.Value}] [{ExpansionPlugin.NextKey.Value}] select    [{ExpansionPlugin.AcceptKey.Value}] replay    [{SystemsConfig.GalleryKey.Value}] close");
            if (status == null && Replaying) _panel = Panel.None;
        }

        // Replays a scene through the same SexController.StartSexAsync call the game makes, in a safe state: every NPC is
        // calmed first, and the partner is moved onto the bed/pillory she was on. When the scene ends the game returns the
        // player as usual.
        public string StartReplay(GalleryEntry g)
        {
            var sc = FindFirstObjectByType<SexController>();
            if (!sc) return "Sex scenes are not available right now.";
            if (sc.IsInSex) return "Already in a scene.";
            var police = FindFirstObjectByType<Game.Police.PoliceController>();
            if (police && police.CopsIsCalled) return "The police are on their way. Replay once they have left.";
            if (!Enum.TryParse<ActorID>(g.Actor, out var id)) return "Unknown neighbour " + g.Actor;
            var npc = _hooks.Entities.GetAIActorEntity(id);
            if (!npc || !npc.gameObject.activeInHierarchy) return $"{g.Actor} is not home right now.";
            var ent = _hooks.Entities.Entities.FirstOrDefault(x => x && x.EntityID != null && x.EntityID.ToString() == g.EntityId);
            if (!(ent is ISexEntity sexEntity) || !ent.gameObject.activeInHierarchy) return "That place is not available right now.";
            foreach (var n in _hooks.Entities.GetAllAiActors().Where(n => n.AIAlertController)) n.AIAlertController.CalmDown(true);
            if (sexEntity.SexPoint)
            {
                if (npc.NavMeshAgent && npc.NavMeshAgent.enabled) npc.NavMeshAgent.Warp(sexEntity.SexPoint.position);
                else npc.transform.position = sexEntity.SexPoint.position;
                npc.transform.rotation = sexEntity.SexPoint.rotation;
            }
            npc.Data.CurrentlyInteractingWith = ent.EntityID;
            Replaying = true;
            _replayAt = Time.unscaledTime;
            Cysharp.Threading.Tasks.UniTaskExtensions.Forget(sc.StartSexAsync(sexEntity, npc, CancellationToken.None));
            ExpansionPlugin.Log.LogInfo($"[gallery] replay {g.Actor} on {g.EntityId}");
            return null;
        }
    }
}
