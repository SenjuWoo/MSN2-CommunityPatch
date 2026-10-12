using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using Game.Entities;
using Game.Fapping;
using Game.GameTime;
using Game.SceneCameras;
using Game.Sex;
using Game.Zones;
using HarmonyLib;
using R3;
using UnityEngine;
using _Game.Systems.CameraController;
using _Game.Systems.ClothesController;

namespace MSN2Expansion
{
    // Turns existing game signals into normalized GameEvents. Only subscribes to public Subjects/Observables the game
    // already raises, plus one postfix on HiddenCameraEntity.CompleteInstallation (no public per-camera signal exists).
    public class GameHooks : IDisposable
    {
        public event Action<GameEvent> OnEvent;

        private readonly CompositeDisposable _subs = new CompositeDisposable();
        private readonly HashSet<int> _seenEntities = new HashSet<int>();
        public ZonesController Zones { get; private set; }
        public EntitiesController Entities { get; private set; }
        public GameTimeController Time { get; private set; }
        private ClothesController _clothes;
        private float _nextScan;
        private SubZoneTypes _lastRoom;

        public static GameHooks Current;

        public static void Apply(Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(HiddenCameraEntity), "CompleteInstallation"), postfix: new HarmonyMethod(typeof(GameHooks), nameof(CameraInstalled)));
        }

        private static void CameraInstalled(HiddenCameraEntity __instance)
        {
            if (Current == null || !__instance.Data.IsInstalled) return;
            Current.Raise(new GameEvent
            {
                Type = "hidden_camera",
                House = __instance.HouseZone ? __instance.HouseZone.ZoneID.ToString() : null,
                Room = __instance.SubZone ? __instance.SubZone.SubZoneType.ToString() : null,
            });
        }

        // Returns null until the Game scene controllers exist and are initialized.
        public static GameHooks TryCreate()
        {
            var entities = UnityEngine.Object.FindFirstObjectByType<EntitiesController>();
            var zones = UnityEngine.Object.FindFirstObjectByType<ZonesController>();
            var time = UnityEngine.Object.FindFirstObjectByType<GameTimeController>();
            var camera = UnityEngine.Object.FindFirstObjectByType<PhoneCameraController>();
            if (!entities || !entities.IsInitialized || !zones || !zones.IsInitialized || !time || !time.IsInitialized || !camera) return null;
            var h = new GameHooks { Entities = entities, Zones = zones, Time = time, _clothes = UnityEngine.Object.FindFirstObjectByType<ClothesController>() };
            h.Subscribe(camera);
            return h;
        }

        private void Subscribe(PhoneCameraController camera)
        {
            camera.OnCameraShotAction.Subscribe(OnShots).AddTo(_subs);
            var sex = UnityEngine.Object.FindFirstObjectByType<SexController>();
            if (sex) sex.MinigameWin.Subscribe(a => Raise(new GameEvent { Type = "sex", Actor = a.ActorID.ToString(), House = HouseAt(a.transform.position) })).AddTo(_subs);
            var fap = UnityEngine.Object.FindFirstObjectByType<FapController>();
            if (fap) fap.OnActorCummed.Subscribe(id => Raise(new GameEvent { Type = "cum_on", Actor = id.ToString() })).AddTo(_subs);
            Zones.OnZoneEntered.Subscribe(d =>
            {
                if (d.Actor.ActorID == ActorID.Player) Raise(new GameEvent { Type = "enter", House = d.Zone.ZoneID.ToString() });
            }).AddTo(_subs);
            Time.OnHourChanged.Subscribe(x => Raise(new GameEvent { Type = "hour", Item = x.hour.ToString() })).AddTo(_subs);
            foreach (var npc in Entities.GetAllAiActors().Where(n => n.AIAlertController))
            {
                var id = npc.ActorID.ToString();
                npc.AIAlertController.AlertStatusChanged.Subscribe(x =>
                {
                    if (x.status == AIAlertStatus.Alert) Raise(new GameEvent { Type = "alert", Actor = id });
                }).AddTo(_subs);
            }
            Current = this;
            ScanEntities();
        }

        // Stealables and traps can be (re)spawned by EntitiesController, so new instances are picked up periodically.
        public void Tick()
        {
            if (UnityEngine.Time.unscaledTime < _nextScan) return;
            _nextScan = UnityEngine.Time.unscaledTime + 2f;
            ScanEntities();
            var player = Entities.GetPlayerEntity();
            if (player && Zones.IsPointInSubZone(player.transform.position, out var sub))
            {
                if (sub.SubZoneType != _lastRoom) { _lastRoom = sub.SubZoneType; Raise(new GameEvent { Type = "enter", House = HouseAt(player.transform.position), Room = sub.SubZoneType.ToString() }); }
            }
            else _lastRoom = SubZoneTypes.None;
        }

        private void ScanEntities()
        {
            // EntitiesController.Entities holds every spawned entity: far cheaper than a scene-wide FindObjectsByType
            var all = Entities.Entities.ToList();
            foreach (var s in all.OfType<StealableEntity>())
            {
                if (!_seenEntities.Add(s.GetInstanceID())) continue;
                var ent = s;
                s.OnInteract.Subscribe(actor =>
                {
                    if (actor is PlayerEntity && ent)
                        Raise(new GameEvent { Type = "steal", EntityId = ent.Data?.Id, Item = ent.Data?.GetType().Name.Replace("StealableEntityData", ""), House = StealHouse(ent), Room = RoomAt(ent.transform.position) });
                }).AddTo(_subs);
            }
            foreach (var t in all.OfType<TrapEntity>())
            {
                if (!_seenEntities.Add(t.GetInstanceID())) continue;
                var trap = t;
                t.OnInteract.Subscribe(actor =>
                {
                    if (actor is AIActorEntity npc && npc.Data.InteractType == Game.Interactables.InteractType.Trapped)
                        Raise(new GameEvent { Type = "trap", Actor = npc.ActorID.ToString(), Trap = trap.TrapType.ToString(), House = trap.HouseZone ? trap.HouseZone.ZoneID.ToString() : null, Room = trap.SubZone ? trap.SubZone.SubZoneType.ToString() : null });
                }).AddTo(_subs);
            }
        }

        private void OnShots(List<ShotData> shots)
        {
            foreach (var s in shots)
            {
                var npc = Entities.GetActorByActorID(s.ActorID) as AIActorEntity;
                var trap = Entities.GetObjectByEntityID(s.InteractWithID) as TrapEntity;
                Raise(new GameEvent
                {
                    Type = "photo",
                    Actor = s.ActorID.ToString(),
                    House = s.HouseZoneID == HouseZoneID.None ? null : s.HouseZoneID.ToString(),
                    Room = s.SubZoneType == SubZoneTypes.None ? null : s.SubZoneType.ToString(),
                    Activity = npc ? npc.Data.InteractType.ToString() : null,
                    Trap = trap ? trap.TrapType.ToString() : null,
                    Naked = IsNaked(s),
                });
            }
        }

        // Same test the game's own photo challenge uses: wearing the actor's "Naked" clothing pack.
        private bool IsNaked(ShotData s)
        {
            try { return _clothes && _clothes.ClothesRepositorySO.GetPackDatasOfActor(s.ActorID).Any(p => p.Category == ClothesCategory.Naked && p.ClothingPackID == s.WearingClothingPack); }
            catch { return false; }
        }

        // Theft spots carry the authoritative house (vanilla theft challenges use it); some spots sit outside the zone colliders.
        public string StealHouse(StealableEntity e)
        {
            var tp = e.GetComponentInParent<Game.MapPoints.TheftMapPoint>();
            return tp && tp.HouseZoneID != HouseZoneID.None ? tp.HouseZoneID.ToString() : HouseAt(e.transform.position);
        }

        private string HouseAt(Vector3 p) => Zones.IsPointInZone(p, out var z) && z ? z.ZoneID.ToString() : null;
        private string RoomAt(Vector3 p) => Zones.IsPointInSubZone(p, out var z) && z ? z.SubZoneType.ToString() : null;

        public void Raise(GameEvent e)
        {
            if (ExpansionPlugin.VerboseLog.Value && e.Type != "hour") ExpansionPlugin.Log.LogInfo($"[event] {e}");
            try { OnEvent?.Invoke(e); }
            catch (Exception ex) { ExpansionPlugin.Log.LogError($"[event] handler failed for {e}: {ex}"); }
        }

        public void Dispose()
        {
            _subs.Dispose();
            if (Current == this) Current = null;
        }
    }
}
