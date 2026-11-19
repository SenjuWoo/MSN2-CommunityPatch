using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Entities;
using Game.Zones;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace MSN2Characters.CutNpcs
{
    // `cutaudit`: one JSON dump of what the shipped game data actually contains (repositories, timetables, house
    // residents, house-opener config, AI actors), written to agent/cutaudit.json for CUT_CONTENT.md.
    public static class CutAudit
    {
        public static IEnumerator Run(string id, string[] a)
        {
            var r = new Dictionary<string, object>();
            string Try(Func<object> f, string key) { try { r[key] = f(); return null; } catch (Exception e) { r[key] = "ERROR " + e.Message; return e.Message; } }

            Try(() => Repo<FSMsRepositorySO>(x => Prop(x, "ID")), "fsms");
            Try(() => Repo<ClothesRepositorySO>(x => new
            {
                actor = Prop(x, "ActorID"),
                packs = ((IEnumerable)Traverse.Create(x).Field("ClothingPacks").GetValue()).Cast<object>().Select(p => $"{Prop(p, "ClothingPackID")}/{Prop(p, "Category")}: {string.Join(",", ((IEnumerable)Traverse.Create(p).Field("Clothes").GetValue() ?? new object[0]).Cast<object>())}").ToArray(),
            }), "clothes");
            Try(() => Repo<Game.Letters.LettersRepositorySO>(x => $"{Prop(x, "LetterID")} subs={Prop(x, "SubscribersRequired")} '{Prop(x, "Subject")}' [{x.GetType().Name}]"), "letters");
            Try(() => Repo<Game.Perks.PerksRepositorySO>(x => Prop(x, "PerkID") ?? (x as UnityEngine.Object)?.name), "perks");
            Try(() => Repo<ChallengesRepositorySO>(x => $"{Prop(x, "ChallengeType")} [{x.GetType().Name}] {(x as UnityEngine.Object)?.name}"), "challenges");
            Try(() => Repo<Game.Data.ActorsDataRepositorySO>(x => $"{Prop(x, "ID")} name={Prop(x, "Name")}"), "actorData");
            Try(() => UnityEngine.Object.FindObjectsByType<HouseZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(z => $"{z.ZoneID}: residents={string.Join(",", z.Residents ?? new List<ActorID>())}").ToArray(), "houseZones");
            Try(() =>
            {
                var hoc = UnityEngine.Object.FindFirstObjectByType<_Game.Systems.HouseOpenerController.HouseOpenerController>();
                return new
                {
                    opened = hoc ? string.Join(",", hoc.OpenedHouseZoneIDs) : null,
                    config = hoc ? hoc.HouseOpenerConfig.Select(d => $"{d.HouseZoneID}: show={d.EntitiesToShow?.Count} hide={d.EntitiesToHide?.Count} interact={d.EntitiesToEnableInteraction?.Count}").ToArray() : null,
                };
            }, "houseOpener");
            Try(() => UnityEngine.Object.FindFirstObjectByType<EntitiesController>().GetAllAiActors().Select(n => new
            {
                actor = n.ActorID.ToString(), go = n.name, active = n.gameObject.activeInHierarchy, visibleInSave = n.Data.IsVisible,
                timetable = n.TimeTableController && n.TimeTableController.TimeTableSO
                    ? n.TimeTableController.TimeTableSO.Select(i => $"{i.StartHour:00}-{i.EndHour:00} {i.FsmID}").ToArray() : null,
                timetableSO = n.TimeTableController && n.TimeTableController.TimeTableSO ? n.TimeTableController.TimeTableSO.name : null,
                running = n.TimeTableController ? n.TimeTableController.IsRunning : (bool?)null,
                pack = n.WearingClothingPack?.ToString(),
            }).ToArray(), "aiActors");
            var file = Path.Combine(BepInEx.Paths.BepInExRootPath, "MSN2", "agent", "cutaudit.json");
            File.WriteAllText(file, JsonConvert.SerializeObject(r, Formatting.Indented));
            yield return null;
            CharacterAgent.Write(id, new { ok = true, file, keys = r.Keys });
        }

        private static object[] Repo<T>(Func<object, object> f) where T : UnityEngine.Object
        {
            var repo = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault();
            if (!repo) return new object[] { "repository not loaded: " + typeof(T).Name };
            var items = (Traverse.Create(repo).Field("_items").GetValue() ?? Traverse.Create(repo).Field("Items").GetValue()) as IEnumerable;
            return items == null ? new object[0] : items.Cast<object>().Where(x => x != null).Select(f).ToArray();
        }

        private static object Prop(object o, string name)
        {
            if (o == null) return null;
            var t = Traverse.Create(o);
            var v = t.Property(name).GetValue() ?? t.Field(name).GetValue() ?? t.Field($"<{name}>k__BackingField").GetValue();
            return v?.ToString();
        }
    }
}
