using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Entities;
using Game.Interactables;
using Game.Inventory;
using Game.Zones;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MSN2Expansion
{
    // Loads packs/<id>/pack.json and rejects anything that does not resolve against the game's own enums.
    public static class PackLoader
    {
        public static readonly string[] ObjectiveTypes = { "photo", "steal", "hidden_camera", "trap", "sex", "cum_on", "enter" };
        public static readonly string[] ConstraintTypes = { "no_alert", "within_hours" };

        // Stealable item names come from StealableEntity's nested data types, e.g. PantiesStealableEntityData -> "Panties".
        public static readonly string[] StealableItems = typeof(StealableEntity).GetNestedTypes()
            .Where(t => typeof(StealableEntity.StealableEntityData).IsAssignableFrom(t) && t != typeof(StealableEntity.StealableEntityData))
            .Select(t => t.Name.Replace("StealableEntityData", "")).ToArray();

        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        public static List<Pack> LoadAll(string root, List<string> errors)
        {
            var packs = new List<Pack>();
            if (!Directory.Exists(root)) return packs;
            foreach (var file in Directory.GetFiles(root, "pack.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var p = JsonConvert.DeserializeObject<Pack>(File.ReadAllText(file), Json);
                    p.Dir = Path.GetDirectoryName(file);
                    int before = errors.Count;
                    Validate(p, file, errors, packs);
                    if (errors.Count == before) packs.Add(p);
                }
                catch (Exception e) { errors.Add($"{file}: {e.Message}"); }
            }
            return packs;
        }

        private static void Validate(Pack p, string file, List<string> err, List<Pack> loaded)
        {
            void E(string where, string msg) => err.Add($"{file}: {where}: {msg}");
            if (string.IsNullOrWhiteSpace(p.Id)) E("pack", "id is required");
            if (loaded.Any(x => x.Id == p.Id)) E("pack", $"duplicate pack id '{p.Id}'");
            if (!string.IsNullOrEmpty(p.MinFramework) && new Version(p.MinFramework) > new Version(ExpansionPlugin.Version))
                E("pack", $"needs framework {p.MinFramework}, this is {ExpansionPlugin.Version}");
            var ids = new HashSet<string>(loaded.SelectMany(x => x.Missions).Select(m => m.Id));
            foreach (var m in p.Missions)
            {
                string w = $"mission '{m.Id}'";
                m.PackId = p.Id;
                if (string.IsNullOrWhiteSpace(m.Id) || !ids.Add(m.Id)) E(w, "missing or duplicate id");
                if (string.IsNullOrWhiteSpace(m.Title)) E(w, "title is required");
                if (m.Objectives.Count == 0) E(w, "needs at least one objective");
                foreach (var o in m.Objectives) CheckObjective(o, false, $"{w} objective '{o.Id}'", E);
                foreach (var o in m.Bonus) CheckObjective(o, true, $"{w} bonus '{o.Id}'", E);
                if (m.Objectives.Concat(m.Bonus).GroupBy(o => o.Id).Any(g => g.Count() > 1)) E(w, "objective ids must be unique");
                CheckReward(m.Reward, w, E);
                foreach (var hz in m.Unlock.Houses) Enum<HouseZoneID>(hz, w, "unlock house", E);
                if (m.Unlock.FromHour < 0 || m.Unlock.ToHour > 24 || m.Unlock.FromHour >= m.Unlock.ToHour) E(w, "unlock hours must satisfy 0 <= fromHour < toHour <= 24");
            }
            // "after" may reference missions from this or earlier-loaded packs
            foreach (var m in p.Missions)
                foreach (var a in m.Unlock.After.Where(a => !ids.Contains(a))) E($"mission '{m.Id}'", $"unlock.after references unknown mission '{a}'");
        }

        private static void CheckObjective(Objective o, bool bonus, string w, Action<string, string> E)
        {
            if (string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.Text)) E(w, "id and text are required");
            bool constraint = ConstraintTypes.Contains(o.Type);
            if (!ObjectiveTypes.Contains(o.Type) && !(bonus && constraint)) E(w, $"unknown type '{o.Type}' (objectives: {string.Join(", ", ObjectiveTypes)}; bonus also: {string.Join(", ", ConstraintTypes)})");
            if (o.Type == "within_hours" && o.Hours <= 0) E(w, "within_hours needs hours > 0");
            if (o.Count < 1) E(w, "count must be >= 1");
            Enum<ActorID>(o.Actor, w, "actor", E);
            Enum<HouseZoneID>(o.House, w, "house", E);
            Enum<SubZoneTypes>(o.Room, w, "room", E);
            Enum<InteractType>(o.Activity, w, "activity", E);
            Enum<TrapEntity.TrapTypeEnum>(o.Trap, w, "trap", E);
            if (o.Item != null && !StealableItems.Contains(o.Item)) E(w, $"item '{o.Item}' is not a stealable ({string.Join(", ", StealableItems)})");
            if (o.Spawn && (o.Type != "steal" || o.Item == null || o.House == null)) E(w, "spawn needs type steal with item and house");
            CheckReward(o.Reward, w, E);
        }

        private static void CheckReward(Reward r, string w, Action<string, string> E)
        {
            if (r.Money < 0 || r.Subscribers < 0) E(w, "rewards cannot be negative");
            foreach (var k in r.Items.Keys) Enum<ItemID>(k, w, "reward item", E);
        }

        private static void Enum<T>(string v, string w, string field, Action<string, string> E) where T : struct
        {
            if (v != null && !System.Enum.TryParse<T>(v, false, out _)) E(w, $"{field} '{v}' is not one of {string.Join(", ", System.Enum.GetNames(typeof(T)))}");
        }
    }
}
