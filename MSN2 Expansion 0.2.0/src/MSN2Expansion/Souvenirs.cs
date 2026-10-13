using System;
using System.Linq;
using Game.Core;
using Game.Entities;
using Game.MapPoints;
using Game.Zones;
using UnityEngine;

namespace MSN2Expansion
{
    // Vanilla only spawns stealables for its own theft challenges, so "spawn" steal objectives place their item the same
    // way (InstantiateObjectFromEntityPrefabIDOnMapPointAsync on a free TheftMapPoint). The item is a vanilla entity and is
    // saved/restored by the game; the sidecar keeps its Data.Id so it can be removed when stolen or when the job ends.
    public static class Souvenirs
    {
        public static void Ensure(MissionRunner r, Mission m, MissionState st)
        {
            var ec = r.Hooks?.Entities;
            if (ec == null) return;
            foreach (var o in m.Objectives.Concat(m.Bonus).Where(o => o.Spawn && !r.IsDone(m, o)))
            {
                if (st.Spawned.TryGetValue(o.Id, out var existing) && ec.TryGetObjectById(existing, out _)) continue;
                var prefab = EnumString<EntityPrefabID>.GetValues().FirstOrDefault(v => string.Equals(v, o.Item, StringComparison.OrdinalIgnoreCase));
                var house = (HouseZoneID)Enum.Parse(typeof(HouseZoneID), o.House);
                var points = UnityEngine.Object.FindFirstObjectByType<MapPointsController>()?.MapPoints.OfType<TheftMapPoint>()
                    .Where(p => p.HouseZoneID == house && p.transform.childCount == 0).ToList();
                if (prefab == null || points == null || points.Count == 0)
                {
                    ExpansionPlugin.Log.LogWarning($"[souvenir] {m.Id}/{o.Id}: cannot place {o.Item} in {o.House} (prefab={prefab ?? "missing"}, free spots={points?.Count ?? 0})");
                    continue;
                }
                try
                {
                    var point = points[UnityEngine.Random.Range(0, points.Count)];
                    var e = ec.InstantiateObjectFromEntityPrefabIDOnMapPointAsync(new EntityPrefabID(prefab), point.ID);
                    st.Spawned[o.Id] = e.Data.Id;
                    ExpansionPlugin.Log.LogInfo($"[souvenir] {m.Id}/{o.Id}: placed {prefab} at {point.ID} {point.transform.position:F1} id={e.Data.Id}");
                }
                catch (Exception ex) { ExpansionPlugin.Log.LogError($"[souvenir] {m.Id}/{o.Id}: spawn failed: {ex}"); }
            }
        }

        public static void OnStolen(MissionRunner r, MissionState st, string entityId)
        {
            foreach (var kv in st.Spawned.Where(kv => kv.Value == entityId).ToList())
            {
                Destroy(r, kv.Value);
                st.Spawned.Remove(kv.Key);
            }
        }

        public static void Cleanup(MissionRunner r, MissionState st)
        {
            foreach (var id in st.Spawned.Values.ToList()) Destroy(r, id);
            st.Spawned.Clear();
        }

        private static void Destroy(MissionRunner r, string id)
        {
            var ec = r.Hooks?.Entities;
            if (ec != null && ec.TryGetObjectById(id, out var e)) ec.DestroyObject(e);
        }
    }
}
