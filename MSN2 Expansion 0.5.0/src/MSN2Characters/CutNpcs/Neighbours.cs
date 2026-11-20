using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Game.Entities;
using Game.GameTime;
using Game.NPCTimeTable;
using Game.Zones;
using HarmonyLib;
using R3;
using UnityEngine;
using _Game.Scripts.Evgeny.Clothes;
using _Game.Systems.ClothesController;

namespace MSN2Characters.CutNpcs
{
    // Yuka (Emma's maid), Jamal (Camila's house) and Peter (Emma's house) are residents in the shipped scene data and
    // have full timetables; vanilla activates them together with Camila/Emma when their house opens (letters
    // OpenHouse2 / OpenHouse3). This class gives each of them and each couple scene a switch, and restores Yuka's
    // shipped-but-unused bunny suit. Everything is runtime-only: the timetable assets are edited in memory per session,
    // nothing extra is written to the save.
    public static class Neighbours
    {
        internal static ConfigEntry<bool> Yuka, Jamal, Peter, CamilaJamal, EmmaPeter, EmmaYuka, YukaMasturbation, YukaGynChair, YukaBunnysuit;
        private static readonly Dictionary<TimeTableInterval, FSMID> Original = new Dictionary<TimeTableInterval, FSMID>();
        private static int _appliedScene = -1;
        public static readonly List<string> Log = new List<string>();

        public static void Bind(ConfigFile c)
        {
            Yuka = c.Bind("Neighbours", "Yuka", true, "Emma's maid lives in Emma's house once it opens (vanilla). false = she never appears.");
            Jamal = c.Bind("Neighbours", "Jamal", true, "Jamal lives in Camila's house once it opens (vanilla). false = he never appears (Camila's scenes with him become rest time).");
            Peter = c.Bind("Neighbours", "Peter", true, "Peter lives in Emma's house once it opens (vanilla). false = he never appears (Emma's night scene with him becomes sleep).");
            CamilaJamal = c.Bind("Scenes", "CamilaJamal", true, "Camila and Jamal's pillory (15-17) and bedroom (21-23) scenes.");
            EmmaPeter = c.Bind("Scenes", "EmmaPeter", true, "Emma and Peter's night scene (00-02).");
            EmmaYuka = c.Bind("Scenes", "EmmaYuka", true, "Emma and Yuka's scene (18-20).");
            YukaMasturbation = c.Bind("Scenes", "YukaMasturbation", true, "Yuka's masturbation (16-18).");
            YukaGynChair = c.Bind("Scenes", "YukaGynChair", true, "Yuka on the gyno chair in the laundry (14-16).");
            YukaBunnysuit = c.Bind("Restored", "YukaBunnysuit", true, "Yuka's bunny suit ships with the game but nothing puts it on: she wears it for her evening shift (20-22).");
        }

        // FSM swaps: (actor, scene FSM) -> solo FSM the actor already has in her own timetable
        private static IEnumerable<(ActorID actor, string from, string to, bool keep)> Swaps() => new[]
        {
            (ActorID.Camila, "Camila_Sex_Pillary", "Camila_Rest_2", CamilaJamal.Value && Jamal.Value),
            (ActorID.Camila, "Camila_Sex_Bedroom", "Camila_Rest", CamilaJamal.Value && Jamal.Value),
            (ActorID.Jamal, "Jamal_Sex_Pillary", "Jamal_HomeWork", CamilaJamal.Value),
            (ActorID.Jamal, "Jamal_Sex_Bedroom", "Jamal_Rest", CamilaJamal.Value),
            (ActorID.Emma, "Emma_Sex_Peter", "Emma_Sleep", EmmaPeter.Value && Peter.Value),
            (ActorID.Peter, "Peter_Sex_Emma", "Peter_Sleep", EmmaPeter.Value),
            (ActorID.Emma, "Emma_Lesbi_Yuka", "Emma_Rest", EmmaYuka.Value && Yuka.Value),
            (ActorID.Yuka, "Yuka_Lesbi_Emma", "Yuka_HomeWork", EmmaYuka.Value),
            (ActorID.Yuka, "Yuka_Masturbation", "Yuka_HomeWork", YukaMasturbation.Value),
            (ActorID.Yuka, "Yuka_GynChair", "Yuka_HomeWork", YukaGynChair.Value),
        };

        // Called every frame by CharactersRuntime in the Game scene; applies once per scene after entities are collected.
        public static void Tick(EntitiesController entities)
        {
            int scene = entities.GetInstanceID();
            if (_appliedScene == scene || entities.Entities == null || !entities.IsInitialized) return;
            _appliedScene = scene;
            Log.Clear();
            try { Apply(entities); }
            catch (Exception e) { CharactersPlugin.Log.LogError($"[neighbours] apply failed, vanilla kept: {e}"); }
        }

        private static void Apply(EntitiesController entities)
        {
            // 1) timetables: restore originals first (the SOs live for the whole session), then swap disabled scenes
            foreach (var kv in Original) kv.Key.FsmID = kv.Value;
            Original.Clear();
            var actors = entities.GetAllAiActors();
            foreach (var (actor, from, to, keep) in Swaps())
            {
                if (keep) continue;
                var ai = actors.FirstOrDefault(a => a.ActorID == actor);
                var tt = ai && ai.TimeTableController ? ai.TimeTableController.TimeTableSO : null;
                if (!tt) continue;
                foreach (var iv in tt.Where(i => i.FsmID != null && i.FsmID.ToString() == from))
                {
                    Original[iv] = iv.FsmID;
                    iv.FsmID = new FSMID(to);
                    Log.Add($"{actor} {iv.StartHour:00}-{iv.EndHour:00} {from} -> {to}");
                }
            }
            // 2) disabled NPCs: off the house's resident list (so opening the house does not show them) and hidden
            var zones = UnityEngine.Object.FindObjectsByType<HouseZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var (id, on) in new[] { (ActorID.Yuka, Yuka.Value), (ActorID.Jamal, Jamal.Value), (ActorID.Peter, Peter.Value) })
            {
                if (on) continue;
                foreach (var z in zones) if (z.Residents != null && z.Residents.Remove(id)) Log.Add($"{id} removed from {z.ZoneID} residents");
                var ai = actors.FirstOrDefault(a => a.ActorID == id);
                if (ai && ai.gameObject.activeSelf) { ai.gameObject.SetActive(false); Log.Add($"{id} hidden"); }
            }
            // 3) restored: Yuka's bunny suit (ClothesController.WearClothingPack prefix, see BunnyPrefix)
            _time = UnityEngine.Object.FindFirstObjectByType<GameTimeController>();
            if (YukaBunnysuit.Value && Yuka.Value) Log.Add("Yuka bunny suit 20-22");
            CharactersPlugin.Log.LogInfo($"[neighbours] Yuka={Yuka.Value} Jamal={Jamal.Value} Peter={Peter.Value}; {(Log.Count == 0 ? "vanilla" : string.Join("; ", Log))}");
        }

        private static GameTimeController _time;

        public static void Patch(Harmony h) =>
            h.Patch(AccessTools.Method(typeof(ClothesController), nameof(ClothesController.WearClothingPack)), prefix: new HarmonyMethod(typeof(Neighbours), nameof(BunnyPrefix)));

        // Every FSM puts Yuka back into her day pack (Yuka_Maid) when it starts; during her evening shift that call
        // gets the bunny suit instead, so the outfit holds through the 20-22 cooking/eating FSMs.
        private static void BunnyPrefix(ActorID actorID, ref ClothingPackID clothingPackID)
        {
            try
            {
                if (actorID != ActorID.Yuka || YukaBunnysuit == null || !YukaBunnysuit.Value || !Yuka.Value || clothingPackID == null) return;
                if (!_time) _time = UnityEngine.Object.FindFirstObjectByType<GameTimeController>();
                if (!_time) return;
                int h = _time.Hours % 24;
                if (h >= 20 && h < 22 && clothingPackID.ToString() == "Yuka_Maid") clothingPackID = new ClothingPackID("Yuka_Bunnysuit");
            }
            catch (Exception e) { CharactersPlugin.Log.LogWarning($"[neighbours] bunny suit: {e.Message}"); }
        }

        public static void Reset() { _appliedScene = -1; }
    }
}
