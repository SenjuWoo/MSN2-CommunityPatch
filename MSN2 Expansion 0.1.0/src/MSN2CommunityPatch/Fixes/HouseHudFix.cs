using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Data;
using Game.Entities;
using Game.Views;
using Game.Zones;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Zenject;

namespace MSN2CommunityPatch.Fixes
{
    // Vanilla bug: ActorsInHouseView clears the player's current house whenever ANY NPC leaves ANY house,
    // then its per-frame room label throws "CurrentHouseDataSO is null" every frame (21k exceptions / 5 min measured).
    // It also rebuilt the resident portraits from the wrong house and never refreshed them when an NPC entered.
    public static class HouseHudFix
    {
        private static readonly AccessTools.FieldRef<ActorsInHouseView, HouseDataSO> Current = AccessTools.FieldRefAccess<ActorsInHouseView, HouseDataSO>("_currentHouseDataSO");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, HousesDataRepositorySO> Repo = AccessTools.FieldRefAccess<ActorsInHouseView, HousesDataRepositorySO>("_housesDataRepositorySO");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, ZonesController> Zones = AccessTools.FieldRefAccess<ActorsInHouseView, ZonesController>("_zonesController");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, PlayerEntity> Player = AccessTools.FieldRefAccess<ActorsInHouseView, PlayerEntity>("_playerEntity");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, TMP_Text> SubText = AccessTools.FieldRefAccess<ActorsInHouseView, TMP_Text>("_currentSubZoneText");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, LocalizationManager> Loc = AccessTools.FieldRefAccess<ActorsInHouseView, LocalizationManager>("_localizationManager");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, Transform> Portraits = AccessTools.FieldRefAccess<ActorsInHouseView, Transform>("_portraitsParent");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, ActorInHousePortrait> PortraitPrefab = AccessTools.FieldRefAccess<ActorsInHouseView, ActorInHousePortrait>("_actorInHousePortraitPrefab");
        private static readonly AccessTools.FieldRef<ActorsInHouseView, DiContainer> Di = AccessTools.FieldRefAccess<ActorsInHouseView, DiContainer>("_diContainer");

        private static string _lastRoomKey;
        private static int _lastTranslateFrame;

        public static void Apply(Harmony h)
        {
            var t = typeof(ActorsInHouseView);
            var label = AccessTools.GetDeclaredMethods(t).FirstOrDefault(m => m.Name.StartsWith("<Open>") && Il.ContainsString(m, "CurrentHouseDataSO is null"));
            if (label == null) { Plugin.Log.LogWarning("[fix] HouseHud: room-label lambda not found; game changed? fix skipped"); return; }
            h.Patch(label, prefix: new HarmonyMethod(typeof(HouseHudFix), nameof(LabelPrefix)));
            h.Patch(AccessTools.Method(t, "OnActorExitedHouseZone"), prefix: new HarmonyMethod(typeof(HouseHudFix), nameof(ExitPrefix)));
            h.Patch(AccessTools.Method(t, "OnActorEnteredHouseZone"), postfix: new HarmonyMethod(typeof(HouseHudFix), nameof(EnterPostfix)));
            Plugin.Log.LogInfo($"[fix] HouseHud active ({label.Name})");
        }

        private static bool LabelPrefix(ActorsInHouseView __instance)
        {
            var player = Player(__instance);
            var text = SubText(__instance);
            if (!player || !text) return false;
            var pos = player.transform.position;
            var zones = Zones(__instance);
            if (!zones.IsPointInSubZone(pos, out var sub))
            {
                if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
                return false;
            }
            var house = Current(__instance);
            if (!house && zones.IsPointInZone(pos, out var zone)) house = Current(__instance) = Repo(__instance).GetHouseData(zone.ZoneID);
            var room = house ? house.Rooms?.FirstOrDefault(r => r.RoomType == sub.SubZoneType) : null;
            if (room == null)
            {
                if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
                return false;
            }
            if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            // vanilla re-translated every frame; refresh on room change and twice a second for language switches
            if (room.RoomName != _lastRoomKey || Time.frameCount - _lastTranslateFrame > 30)
            {
                text.text = Loc(__instance).Translate(room.RoomName);
                _lastRoomKey = room.RoomName;
                _lastTranslateFrame = Time.frameCount;
            }
            return false;
        }

        private static bool ExitPrefix(ActorsInHouseView __instance, HouseZoneEnterExitData houseZoneEnterExitData)
        {
            var id = houseZoneEnterExitData.Actor.ActorID;
            if (id == ActorID.Player) return true; // vanilla player-exit path is correct
            if (id != ActorID.PoliceOfficer && IsCurrent(__instance, houseZoneEnterExitData.Zone)) Rebuild(__instance, houseZoneEnterExitData.Zone, id);
            return false;
        }

        private static void EnterPostfix(ActorsInHouseView __instance, HouseZoneEnterExitData houseZoneEnterExitData)
        {
            var id = houseZoneEnterExitData.Actor.ActorID;
            if (id != ActorID.Player && id != ActorID.PoliceOfficer && IsCurrent(__instance, houseZoneEnterExitData.Zone)) Rebuild(__instance, houseZoneEnterExitData.Zone, null);
        }

        private static bool IsCurrent(ActorsInHouseView v, HouseZone zone) => Current(v) && zone && Repo(v).GetHouseData(zone.ZoneID) == Current(v);

        private static void Rebuild(ActorsInHouseView v, HouseZone zone, ActorID? leaving)
        {
            var parent = Portraits(v);
            foreach (Transform child in parent) Object.Destroy(child.gameObject);
            foreach (var actor in zone.CurrentlyRegisteredActors.Where(x => x != ActorID.Player && x != ActorID.PoliceOfficer && x != ActorID.OldMan && x != leaving).Distinct().ToList())
                Di(v).InstantiatePrefabForComponent<ActorInHousePortrait>(PortraitPrefab(v), parent).Setup(actor);
        }
    }

    internal static class Il
    {
        // True if the method body contains `ldstr <s>`; used to find compiler-generated lambdas without hardcoding their names.
        public static bool ContainsString(MethodInfo m, string s)
        {
            var body = m.GetMethodBody()?.GetILAsByteArray();
            if (body == null) return false;
            for (int i = 0; i + 4 < body.Length; i++)
            {
                if (body[i] != 0x72) continue;
                int token = System.BitConverter.ToInt32(body, i + 1);
                if ((token >> 24) != 0x70) continue;
                try { if (m.Module.ResolveString(token) == s) return true; } catch { }
            }
            return false;
        }
    }
}
