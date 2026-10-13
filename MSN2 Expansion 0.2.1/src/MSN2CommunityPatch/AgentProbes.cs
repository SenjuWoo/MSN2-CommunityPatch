using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.AI;
using Game.Entities;
using Game.GameTime;
using Game.Input;
using Game.Interactables;
using Game.Objects;
using HarmonyLib;
using NodeCanvas.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MSN2CommunityPatch
{
    // Dev probes behind the agent bridge: door/fling repro, interactable reachability audit, NPC warp log, key hold, frame-time lag.
    // Nothing here runs unless [Debug] AgentBridge is on.
    public static class AgentProbes
    {
        private static readonly List<object> Warps = new List<object>();
        private static bool _warpHooked, _lagger;
        internal static int LagMs;

        // Burns frame time to emulate a slow machine (door/physics behaviour changes with big frame deltas).
        private class Lagger : MonoBehaviour { private void LateUpdate() { if (LagMs > 0) Thread.Sleep(LagMs); } }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        public static void Register()
        {
            AgentBridge.Register("doors", (id, a) => AgentBridge.Done(id, Doors().Select(DescribeDoor).ToArray()));
            AgentBridge.Register("doorprobe", DoorProbe);
            AgentBridge.Register("hold", Hold);
            AgentBridge.Register("stealaudit", StealAudit);
            AgentBridge.Register("opennear", OpenNear);
            AgentBridge.Register("faceitem", FaceItem);
            AgentBridge.Register("facepos", FacePos);
            AgentBridge.Register("actiontest", ActionTest);
            AgentBridge.Register("jumpkey", JumpKey);
            AgentBridge.Register("nearby", Nearby);
            AgentBridge.Register("spotaudit", SpotAudit);
            AgentBridge.Register("hideprobe", HideProbe);
            AgentBridge.Register("warplog", WarpLog);
            AgentBridge.Register("settings", (id, a) =>
            {
                var va = UnityEngine.Object.FindFirstObjectByType<Game.Core.ViewAccessor>();
                if (!va) return AgentBridge.Done(id, new { ok = false, error = "no ViewAccessor" });
                va.GetView<Game.SettingsView>().Open(new Game.SettingViewArguments()).Forget();
                return AgentBridge.Done(id, new { ok = true });
            });
            AgentBridge.Register("texts", (id, a) =>
            {
                var q = a.Length > 0 ? string.Join(" ", a) : "";
                var hits = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(t => t.text != null && string.Equals(t.text.Trim(), q, StringComparison.OrdinalIgnoreCase))
                    .Select(t => new { path = SceneStats.Path(t.transform), active = t.gameObject.activeInHierarchy, text = t.text, font = t.font ? t.font.name : null, size = t.fontSize }).Take(60).ToArray();
                return AgentBridge.Done(id, new { count = hits.Length, hits });
            });
            AgentBridge.Register("rects", (id, a) =>
            {
                var root = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => SceneStats.Path(t).EndsWith(string.Join(" ", a), StringComparison.OrdinalIgnoreCase));
                if (!root) return AgentBridge.Done(id, new { ok = false, error = "no RectTransform path ends with " + string.Join(" ", a) });
                var rows = root.GetComponentsInChildren<RectTransform>(true).Take(120).Select(t => new
                {
                    path = SceneStats.Path(t).Substring(Math.Max(0, SceneStats.Path(t).IndexOf(root.name, StringComparison.Ordinal))),
                    active = t.gameObject.activeSelf, anchor = t.anchorMin.ToString("F2") + "-" + t.anchorMax.ToString("F2"), pos = t.anchoredPosition.ToString("F0"), size = t.rect.size.ToString("F0"),
                    comps = string.Join(",", t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "null").Where(n => n != "RectTransform" && n != "CanvasRenderer")),
                }).ToArray();
                return AgentBridge.Done(id, new { ok = true, count = rows.Length, rows });
            });
            AgentBridge.Register("bindings", (id, a) =>
            {
                var rows = new[] { ("Player/Move", "up", 0), ("Player/Move", "left", 0), ("Player/Move", "down", 0), ("Player/Move", "right", 0), ("Player/Move", "up", 1), ("Player/Sprint", null, 0), ("Player/Jump", null, 0), ("Player/Crouch", null, 0),
                        ("Player/Interact", null, 0), ("Player/CameraOnOff", null, 0), ("Player/CameraBackFront", null, 0), ("Phone/ShowHide", null, 0), ("Player/ShoutSkill", null, 0), ("Player/EchoSenseSkill", null, 0), ("Player/Tools", null, 0) }
                    .Select(r => { var s = Controls.KeyBindings.Find(r.Item1, r.Item2, r.Item3); return s == null ? r.Item1 + " (missing)" : $"{s.Name,-26} {s.Path,-24} display='{s.Display}' default={s.IsDefault}"; }).ToArray();
                return AgentBridge.Done(id, new { custom = Controls.KeyBindings.HasCustom, file = Controls.KeyBindings.FilePath, rows });
            });
            // rebind <Map/Action> <Key|reset>: programmatic equivalent of the Controls page (override + the same Commit that saves the file)
            AgentBridge.Register("rebind", (id, a) =>
            {
                var slot = a.Length > 0 ? Controls.KeyBindings.Find(a[0]) : null;
                if (slot == null || a.Length < 2) return AgentBridge.Done(id, new { ok = false, error = "usage: rebind <Map/Action> <Key|reset>" });
                if (a[1] == "reset") Controls.KeyBindings.Reset(slot);
                else { slot.Action.ApplyBindingOverride(slot.Index, "<Keyboard>/" + a[1].ToLowerInvariant()); AccessTools.Method(typeof(Controls.KeyBindings), "Commit").Invoke(null, new object[] { slot.Inputs }); }
                return AgentBridge.Done(id, new { ok = true, custom = Controls.KeyBindings.HasCustom });
            });
            AgentBridge.Register("looking", (id, a) =>
            {
                var oi = UnityEngine.Object.FindFirstObjectByType<Game.ObjectsInteractor>();
                var it = oi ? oi.IsLookOn : null; var mb = it as MonoBehaviour;
                bool? opened = mb is OpenCloseEntity oc ? oc.Data.IsOpened : mb is DoorEntity de ? de.Data.IsOpened : (bool?)null;
                return AgentBridge.Done(id, new { target = mb ? SceneStats.Path(mb.transform) : null, type = mb ? mb.GetType().Name : null, text = it?.InteractText, opened, state = mb ? StateOf(mb) : null });
            });
            AgentBridge.Register("freespots", (id, a) =>
            {
                var mpc = UnityEngine.Object.FindFirstObjectByType<Game.MapPoints.MapPointsController>();
                if (mpc == null) return AgentBridge.Done(id, new { ok = false, error = "no MapPointsController" });
                var pts = mpc.MapPoints.OfType<Game.MapPoints.TheftMapPoint>().ToList();
                var free = pts.Where(p => p.transform.childCount == 0).ToList(); // vanilla: PrepareChallengeForPlay treats a point with a child as in use
                return AgentBridge.Done(id, new
                {
                    total = pts.Count, free = free.Count, marked = pts.Count(p => p.transform.Find(Fixes.TheftSpotFix.Marker)),
                    freeByHouse = free.GroupBy(p => p.HouseZoneID.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                    unreachableStillFree = free.Where(p => Fixes.TheftSpotFix.IsUnreachable(p.ID.ToString())).Select(p => p.ID.ToString()).ToArray(),
                });
            });
            AgentBridge.Register("noise", (id, a) =>
            {
                var nc = UnityEngine.Object.FindFirstObjectByType<Game.Noise.NoiseController>();
                if (nc == null || a.Length < 3) return AgentBridge.Done(id, new { ok = false, error = "usage: noise x y z [volume]" });
                nc.SendNoise(ActorID.Player, new Vector3(F(a[0]), F(a[1]), F(a[2])), a.Length > 3 ? F(a[3]) : 40f);
                return AgentBridge.Done(id, new { ok = true });
            });
            AgentBridge.Register("lag", (id, a) =>
            {
                LagMs = a.Length > 0 ? (int)F(a[0]) : 0;
                if (LagMs > 0 && !_lagger) { var go = new GameObject("MSN2ProbeLag"); UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<Lagger>(); _lagger = true; }
                return AgentBridge.Done(id, new { ok = true, lagMs = LagMs });
            });
        }

        // ---------- doors ----------
        private static List<MonoBehaviour> Doors() =>
            UnityEngine.Object.FindObjectsByType<DoorEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID).Cast<MonoBehaviour>()
                .Concat(UnityEngine.Object.FindObjectsByType<OpenCloseEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
                .Concat(UnityEngine.Object.FindObjectsByType<SmallWindowEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
                .ToList();

        private static string StateOf(MonoBehaviour d) => AccessTools.Field(d.GetType(), "OpenableState")?.GetValue(d)?.ToString();

        private static object DescribeDoor(MonoBehaviour d, int i)
        {
            var cols = d.GetComponentsInChildren<Collider>(true);
            bool opened = d is DoorEntity de ? de.Data.IsOpened : d is OpenCloseEntity oc ? oc.Data.IsOpened : false;
            return new { i, type = d.GetType().Name, path = SceneStats.Path(d.transform), pos = d.transform.position.ToString("F1"), opened, state = StateOf(d), solid = cols.Count(c => !c.isTrigger), triggers = cols.Count(c => c.isTrigger) };
        }

        // doorprobe <index> <closeon|open1|open2> [seconds]
        // closeon: open door, stand in the doorway, run the NPC auto-close (DoorEntity.CloseAfterSec). openN: stand at side N, 0.3 m toward the door, player-open it.
        private static IEnumerator DoorProbe(string id, string[] a)
        {
            var list = Doors();
            int idx = a.Length > 0 ? int.Parse(a[0]) : 0;
            string scen = a.Length > 1 ? a[1] : "closeon";
            float secs = a.Length > 2 ? F(a[2]) : 4f;
            var door = idx < list.Count ? list[idx] as DoorEntity : null;
            var player = AgentWorld.Player();
            var pe = AgentAI.Entities()?.GetPlayerEntity();
            if (idx < list.Count && list[idx] is OpenCloseEntity oce && player && pe)
            {
                // oc_open: stand 0.3 m in front of the closed door (InteractPoint side) and open it; oc_closeon: open it, stand inside the swing area, close it.
                var c0 = oce.GetComponentsInChildren<Collider>().FirstOrDefault(c => !c.isTrigger);
                var ctr = c0 ? c0.bounds.center : oce.transform.position;
                var ip = oce.InteractPoint ? oce.InteractPoint.position : oce.transform.position;
                bool wantClosed = scen == "oc_open";
                if (oce.Data.IsOpened == wantClosed) { oce.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, CancellationToken.None).Forget(); yield return new WaitForSecondsRealtime(1.5f); }
                var floor = new Vector3(ctr.x, ip.y, ctr.z);
                var toward0 = (floor - ip); toward0.y = 0;
                var at = scen == "oc_open" ? ip + toward0.normalized * 0.3f : floor + (ip - floor).normalized * 0.25f;
                AgentWorld.Teleport(player, at + Vector3.up * 0.05f, null, null);
                yield return new WaitForSecondsRealtime(0.6f);
                var st0 = player.transform.position; var fr0 = new List<object>();
                oce.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, CancellationToken.None).Forget();
                float m0 = 0; yield return Sample(player, secs, fr0, m => m0 = m);
                AgentBridge.Write(id, new { ok = true, scen, door = SceneStats.Path(oce.transform), start = st0.ToString("F2"), end = player.transform.position.ToString("F2"), maxFrameDelta = m0, spikes = fr0, state = StateOf(oce), opened = oce.Data.IsOpened, lagMs = LagMs });
                yield break;
            }
            if (!door || !player || !pe) { AgentBridge.Write(id, new { ok = false, error = "need a DoorEntity/OpenCloseEntity index (see doors), player and entities" }); yield break; }

            var cancel = CancellationToken.None;
            var closeAfter = AccessTools.Method(typeof(DoorEntity), "CloseAfterSec");
            float dur = Mathf.Max(0.5f, (float)(door.OpeningAsset ? door.OpeningAsset.duration : 1.0));
            Vector3 p1 = door.InteractPoint1 ? door.InteractPoint1.position : door.transform.position;
            Vector3 p2 = door.InteractPoint2 ? door.InteractPoint2.position : door.transform.position;

            if (scen == "closeon")
            {
                if (!door.Data.IsOpened) { door.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, cancel).Forget(); yield return new WaitForSecondsRealtime(dur + 0.5f); }
                AgentWorld.Teleport(player, (p1 + p2) * 0.5f + Vector3.up * 0.05f, null, null);
                yield return new WaitForSecondsRealtime(0.6f);
                var start = player.transform.position;
                var frames = new List<object>();
                closeAfter.Invoke(door, new object[] { 0.01f, null });
                float max = 0; yield return Sample(player, secs, frames, m => max = m);
                AgentBridge.Write(id, new { ok = true, scen, door = SceneStats.Path(door.transform), start = start.ToString("F2"), end = player.transform.position.ToString("F2"), maxFrameDelta = max, spikes = frames, state = StateOf(door), opened = door.Data.IsOpened, lagMs = LagMs });
                yield break;
            }

            bool side2 = scen == "open2";
            if (door.Data.IsOpened) { closeAfter.Invoke(door, new object[] { 0.01f, null }); yield return new WaitForSecondsRealtime(dur + 1f); }
            var stand = side2 ? p2 : p1;
            var toward = (door.transform.position - stand); toward.y = 0;
            AgentWorld.Teleport(player, stand + toward.normalized * 0.3f + Vector3.up * 0.05f, null, null);
            yield return new WaitForSecondsRealtime(0.6f);
            var s0 = player.transform.position;
            var fr = new List<object>();
            door.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, cancel).Forget();
            float mx = 0; yield return Sample(player, secs, fr, m => mx = m);
            AgentBridge.Write(id, new { ok = true, scen, door = SceneStats.Path(door.transform), start = s0.ToString("F2"), end = player.transform.position.ToString("F2"), maxFrameDelta = mx, spikes = fr, state = StateOf(door), opened = door.Data.IsOpened, lagMs = LagMs });
        }

        private static IEnumerator Sample(FirstPersonController p, float secs, List<object> spikes, Action<float> done)
        {
            var prev = p.transform.position; float t0 = Time.realtimeSinceStartup, max = 0;
            while (Time.realtimeSinceStartup - t0 < secs)
            {
                yield return null;
                var cur = p.transform.position; float d = Vector3.Distance(cur, prev);
                if (d > max) max = d;
                if (d > 0.35f) spikes.Add(new { t = Time.realtimeSinceStartup - t0, d, from = prev.ToString("F2"), to = cur.ToString("F2") });
                prev = cur;
            }
            done(max);
        }

        // hold <Key> <seconds>: key down through the Input System queue, released afterwards.
        private static IEnumerator Hold(string id, string[] a)
        {
            var kb = Keyboard.current;
            if (kb == null || a.Length < 2 || !Enum.TryParse<Key>(a[0], true, out var k)) { AgentBridge.Write(id, new { ok = false, error = "usage: hold <Key> <seconds>" }); yield break; }
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            yield return new WaitForSecondsRealtime(F(a[1]));
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null; yield return null;
            AgentBridge.Write(id, new { ok = true, key = k.ToString() });
        }

        // ---------- reachability audit ----------
        private static void BuildTriggers(out Dictionary<IInteractable, List<Collider>> triggers, out Dictionary<Collider, string> owner)
        {
            triggers = new Dictionary<IInteractable, List<Collider>>();
            owner = new Dictionary<Collider, string>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OfType<IInteractableTrigger>())
            {
                IInteractable it; try { it = t.Interactable; } catch { continue; }
                var mb = (MonoBehaviour)t;
                foreach (var c in mb.GetComponents<Collider>())
                {
                    if (!triggers.TryGetValue(it, out var l)) triggers[it] = l = new List<Collider>();
                    l.Add(c);
                    owner[c] = (it as MonoBehaviour)?.GetType().Name + ":" + (it as MonoBehaviour)?.name;
                }
            }
        }

        private static object AuditItem(StealableEntity s, Dictionary<IInteractable, List<Collider>> triggers, Dictionary<Collider, string> owner, LayerMask mask, float reach)
        {
            if (!triggers.TryGetValue(s, out var targets)) return new { name = SceneStats.Path(s.transform), item = s.Data.GetType().Name, problem = "no interactable trigger collider found" };
            var center = targets[0].bounds.center;
            var r = Reach(targets, center, owner, mask, reach);
            var enclosing = owner.Where(kv => kv.Key && !targets.Contains(kv.Key) && kv.Key.bounds.Contains(center)).Select(kv => kv.Value).Distinct().ToArray();
            return new
            {
                name = SceneStats.Path(s.transform), item = s.Data.GetType().Name, pos = s.transform.position.ToString("F2"), center = center.ToString("F2"),
                r.tried, reachableStanding = r.stand, reachableCrouching = r.crouch, unreachable = r.stand + r.crouch == 0,
                enclosedBy = enclosing, topBlockers = r.blockers.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key + " x" + kv.Value).ToArray(),
            };
        }

        private static StealableEntity[] AllStealables() => UnityEngine.Object.FindObjectsByType<StealableEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        // stealaudit [index]: can the player's interaction ray (ObjectsInteractor: camera ray, maxDistance, interactable layers) hit each stealable from any reachable spot?
        private static IEnumerator StealAudit(string id, string[] a)
        {
            var inter = UnityEngine.Object.FindFirstObjectByType<Game.ObjectsInteractor>();
            if (!inter) { AgentBridge.Write(id, new { ok = false, error = "no ObjectsInteractor (not in Game scene)" }); yield break; }
            var mask = (LayerMask)AccessTools.Field(typeof(Game.ObjectsInteractor), "_interactableLayer").GetValue(inter);
            float reach = (float)AccessTools.Field(typeof(Game.ObjectsInteractor), "maxDistance").GetValue(inter);
            BuildTriggers(out var triggers, out var owner);
            var all = AllStealables();
            int? only = a.Length > 0 && int.TryParse(a[0], out var n0) ? n0 : (int?)null;
            var rows = new List<object>();
            for (int i = 0; i < all.Length; i++)
            {
                if (only.HasValue && only.Value != i) continue;
                rows.Add(AuditItem(all[i], triggers, owner, mask, reach));
                yield return null;
            }
            AgentBridge.Write(id, new { ok = true, count = rows.Count, layerMask = mask.value, reach, rows });
        }

        // opennear <stealableIndex> [radius] [nameContains]: player-open every closed, unlocked OpenCloseEntity within radius of the item (optionally only names containing a string).
        private static IEnumerator OpenNear(string id, string[] a)
        {
            var all = AllStealables(); int i = a.Length > 0 ? int.Parse(a[0]) : 0; float r = a.Length > 1 ? F(a[1]) : 1.3f;
            var pe = AgentAI.Entities()?.GetPlayerEntity();
            if (i >= all.Length || !pe) { AgentBridge.Write(id, new { ok = false, error = "bad index or no player" }); yield break; }
            var s = all[i]; var opened = new List<string>(); string only = a.Length > 2 ? a[2] : null;
            foreach (var oc in UnityEngine.Object.FindObjectsByType<OpenCloseEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
            {
                if (oc.Data.IsOpened || oc.Data.IsLocked) continue;
                if (only != null && oc.name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!oc.GetComponentsInChildren<Collider>().Any(c => (c.ClosestPoint(s.transform.position) - s.transform.position).sqrMagnitude < r * r)) continue;
                oc.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, CancellationToken.None).Forget();
                opened.Add(SceneStats.Path(oc.transform));
            }
            yield return new WaitForSecondsRealtime(1.6f);
            AgentBridge.Write(id, new { ok = true, item = s.Data.GetType().Name, opened });
        }

        // faceitem <stealableIndex>: stand 0.6-1.5 m from the item on the NavMesh with a clear sightline and aim the camera at it (then use "screenshot").
        private static IEnumerator FaceItem(string id, string[] a)
        {
            var all = AllStealables(); int i = a.Length > 0 ? int.Parse(a[0]) : 0;
            var player = AgentWorld.Player();
            if (i >= all.Length || !player) { AgentBridge.Write(id, new { ok = false, error = "bad index or no player" }); yield break; }
            var s = all[i]; var c = s.GetComponentsInChildren<Collider>().FirstOrDefault();
            var target = c ? c.bounds.center : s.transform.position;
            bool allowOccluded = a.Length > 1 && a[1] == "any";
            foreach (var r in new[] { 0.8f, 1.0f, 1.25f, 1.5f, 0.6f })
                for (int k = 0; k < 24; k++)
                {
                    var off = Quaternion.Euler(0, k * 15f, 0) * Vector3.forward * r;
                    var probe = target + off; probe.y = target.y - 0.9f;
                    if (!NavMesh.SamplePosition(probe, out var nh, 1.3f, NavMesh.AllAreas)) continue;
                    var eye = nh.position + Vector3.up * 1.6f;
                    if (!allowOccluded && Physics.Linecast(eye, target, out var lh, ~0, QueryTriggerInteraction.Ignore) && !lh.transform.IsChildOf(s.transform)) continue;
                    var flat = target - eye; flat.y = 0;
                    float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(eye.y - target.y, flat.magnitude) * Mathf.Rad2Deg;
                    AgentWorld.Teleport(player, nh.position + Vector3.up * 0.05f, yaw, pitch);
                    yield return new WaitForSecondsRealtime(0.6f);
                    AgentBridge.Write(id, new { ok = true, item = s.Data.GetType().Name, standAt = nh.position.ToString("F2"), yaw, pitch });
                    yield break;
                }
            AgentBridge.Write(id, new { ok = false, error = "no clear sightline spot found" });
        }

        // nearby <stealableIndex> [radius]: containers around the item - state, lock, perk requirement and collider bounds.
        private static IEnumerator Nearby(string id, string[] a)
        {
            var all = AllStealables(); int i = a.Length > 0 ? int.Parse(a[0]) : 0; float r = a.Length > 1 ? F(a[1]) : 1.5f;
            if (i >= all.Length) { AgentBridge.Write(id, new { ok = false, error = "bad index" }); yield break; }
            var s = all[i]; var c0 = s.GetComponentsInChildren<Collider>().FirstOrDefault(); var center = c0 ? c0.bounds.center : s.transform.position;
            var perks = UnityEngine.Object.FindFirstObjectByType<Game.Perks.PerksController>();
            var rows = new List<object>();
            foreach (var oc in UnityEngine.Object.FindObjectsByType<OpenCloseEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID))
            {
                var cols = oc.GetComponentsInChildren<Collider>();
                float d = cols.Length == 0 ? 99 : cols.Min(c => (c.ClosestPoint(center) - center).magnitude);
                if (d > r) continue;
                var req = (System.Collections.IEnumerable)AccessTools.Field(oc.GetType(), "RequiredPerksToUnlock")?.GetValue(oc);
                var perkList = req == null ? new List<object>() : req.Cast<object>().ToList();
                rows.Add(new
                {
                    name = SceneStats.Path(oc.transform), dist = d, opened = oc.Data.IsOpened, locked = oc.Data.IsLocked, state = StateOf(oc), requires = perkList.Select(p => p.ToString()).ToArray(),
                    colliders = cols.Select(c => new { c.name, trigger = c.isTrigger, layer = LayerMask.LayerToName(c.gameObject.layer), center = c.bounds.center.ToString("F2"), size = c.bounds.size.ToString("F2"), containsItem = c.bounds.Contains(center) }).ToArray(),
                });
            }
            AgentBridge.Write(id, new { ok = true, item = s.Data.GetType().Name, center = center.ToString("F2"), containers = rows });
            yield break;
        }

        // hideprobe [index] [seconds]: DoorToHideEntity (closet "hide"/"exit") enter then exit as the player, sampling position every frame. No index = list.
        private static IEnumerator HideProbe(string id, string[] a)
        {
            var list = UnityEngine.Object.FindObjectsByType<DoorToHideEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (a.Length == 0) { AgentBridge.Write(id, list.Select((d, i) => new { i, path = SceneStats.Path(d.transform), pos = d.transform.position.ToString("F1"), hidden = d.Data.IsHidden }).ToArray()); yield break; }
            int idx = int.Parse(a[0]); float secs = a.Length > 1 ? F(a[1]) : 3.5f;
            var player = AgentWorld.Player(); var pe = AgentAI.Entities()?.GetPlayerEntity();
            if (idx >= list.Length || !player || !pe) { AgentBridge.Write(id, new { ok = false, error = "bad index or no player" }); yield break; }
            var d0 = list[idx]; var ip = d0.InteractPoint1;
            var stand = ip.position - ip.forward * 0.8f;
            AgentWorld.Teleport(player, stand + Vector3.up * 0.05f, null, null);
            yield return new WaitForSecondsRealtime(0.7f);
            var phases = new List<object>();
            foreach (var phase in new[] { "enter", "exit" })
            {
                var start = player.transform.position; var spikes = new List<object>(); float max = 0;
                d0.InteractAsync(pe, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, CancellationToken.None).Forget();
                yield return Sample(player, secs, spikes, m => max = m);
                phases.Add(new { phase, start = start.ToString("F2"), end = player.transform.position.ToString("F2"), maxFrameDelta = max, spikes, hidden = d0.Data.IsHidden });
            }
            AgentBridge.Write(id, new { ok = true, door = SceneStats.Path(d0.transform), interactPoint = ip.position.ToString("F2"), lagMs = LagMs, phases });
        }

        // Reachability of a target collider set by the player's interaction ray: camera positions around it where a player capsule fits
        // (feet on any walkable surface, not only the NavMesh), standing and crouching.
        private static readonly Dictionary<OpenCloseEntity, int> OcBlockers = new Dictionary<OpenCloseEntity, int>();

        private static (int tried, int stand, int crouch, Dictionary<string, int> blockers) Reach(List<Collider> targets, Vector3 center, Dictionary<Collider, string> owner, LayerMask mask, float reach)
        {
            int stand = 0, crouch = 0, tried = 0;
            var blockers = new Dictionary<string, int>();
            OcBlockers.Clear();
            Physics.SyncTransforms();
            foreach (var r in new[] { 0.3f, 0.45f, 0.6f, 0.8f, 1.0f, 1.25f, 1.55f, 1.9f })
                for (int k = 0; k < 24; k++)
                {
                    var off = Quaternion.Euler(0, k * 15f, 0) * Vector3.forward * r;
                    var top = center + off; top.y = center.y + 0.6f;
                    if (!Physics.Raycast(top, Vector3.down, out var g, 3.0f, ~0, QueryTriggerInteraction.Ignore) || g.normal.y < 0.7f) continue;
                    var feet = g.point; if (center.y - feet.y < -0.5f || center.y - feet.y > 2.2f) continue;
                    foreach (var camH in new[] { 1.65f, 1.0f })
                    {
                        bool crouching = camH < 1.5f;
                        var cam = feet + Vector3.up * camH;
                        if (Physics.CheckCapsule(feet + Vector3.up * (crouching ? 0.35f : 0.45f), feet + Vector3.up * (crouching ? 0.85f : 1.45f), crouching ? 0.28f : 0.33f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        var dir = center - cam; if (dir.magnitude > reach) continue;
                        tried++;
                        bool ok = Physics.Raycast(new Ray(cam, dir.normalized), out var hit, reach, mask) && targets.Contains(hit.collider);
                        if (ok) { if (crouching) crouch++; else stand++; }
                        else if (hit.collider)
                        {
                            var n = owner.TryGetValue(hit.collider, out var o) ? o : hit.collider.name; blockers[n] = blockers.TryGetValue(n, out var c0) ? c0 + 1 : 1;
                            var oc = hit.collider.GetComponentInParent<OpenCloseEntity>();
                            if (oc) OcBlockers[oc] = OcBlockers.TryGetValue(oc, out var c1) ? c1 + 1 : 1;
                        }
                    }
                }
            return (tried, stand, crouch, blockers);
        }

        private static object Summ((int tried, int stand, int crouch, Dictionary<string, int> blockers) r) =>
            new { r.tried, stand = r.stand, crouch = r.crouch, ok = r.stand + r.crouch > 0, top = r.blockers.OrderByDescending(kv => kv.Value).Take(2).Select(kv => kv.Key + " x" + kv.Value).ToArray() };

        // spotaudit [HouseZoneID]: every TheftMapPoint with a probe collider the size of a hair tie (child of the map point, so it rides with a drawer),
        // reachability with its containers as-is and after the player opens them.
        private static IEnumerator SpotAudit(string id, string[] a)
        {
            var inter = UnityEngine.Object.FindFirstObjectByType<Game.ObjectsInteractor>();
            var mpc = UnityEngine.Object.FindFirstObjectByType<Game.MapPoints.MapPointsController>();
            var pe = AgentAI.Entities()?.GetPlayerEntity();
            if (!inter || mpc == null || !pe) { AgentBridge.Write(id, new { ok = false, error = "needs the Game scene" }); yield break; }
            var mask = (LayerMask)AccessTools.Field(typeof(Game.ObjectsInteractor), "_interactableLayer").GetValue(inter);
            float reach = (float)AccessTools.Field(typeof(Game.ObjectsInteractor), "maxDistance").GetValue(inter);
            int layer = LayerMask.NameToLayer("Interactable");
            BuildTriggers(out _, out var owner);
            var all = UnityEngine.Object.FindObjectsByType<OpenCloseEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            var wanted = a.Length > 1 ? a[1].Split(',') : null;
            var points = mpc.MapPoints.OfType<Game.MapPoints.TheftMapPoint>().Where(p => (a.Length == 0 || a[0] == "*" || p.HouseZoneID.ToString() == a[0]) && (wanted == null || wanted.Contains(p.ID.ToString()))).ToList();
            var use = new InteractArgs { InteractActionType = InteractActionType.PlayerUse };
            var rows = new List<object>();
            foreach (var p in points)
            {
                var go = new GameObject("spotprobe") { layer = layer };
                go.transform.SetParent(p.transform, false);
                var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(0.13f, 0.04f, 0.14f);
                yield return null;
                var targets = new List<Collider> { box };
                targets.AddRange(p.GetComponentsInChildren<Collider>().Where(c => c != box && c.isTrigger && c.gameObject.layer == layer)); // a real item already sitting here
                Physics.SyncTransforms();
                var center = box.bounds.center;
                var closedR = Reach(targets, center, owner, mask, reach);
                var ancestors = all.Where(oc => oc && !oc.Data.IsLocked && p.transform.IsChildOf(oc.transform)).Distinct().ToList();
                var blockingOc = OcBlockers.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Where(oc => oc && !oc.Data.IsLocked).Take(2).ToList();
                var attempts = new List<List<OpenCloseEntity>>();
                if (ancestors.Count > 0) attempts.Add(ancestors);
                if (blockingOc.Count > 0) attempts.Add(new List<OpenCloseEntity> { blockingOc[0] });
                if (blockingOc.Count > 1) attempts.Add(new List<OpenCloseEntity> { blockingOc[0], blockingOc[1] });
                if (ancestors.Count > 0 && blockingOc.Count > 0) attempts.Add(ancestors.Concat(new[] { blockingOc[0] }).Distinct().ToList());
                object openR = null; var toOpen = new List<OpenCloseEntity>();
                if (!(closedR.stand + closedR.crouch > 0))
                    foreach (var attempt in attempts.Select(t => t.Where(oc => !oc.Data.IsOpened).ToList()).Where(t => t.Count > 0))
                    {
                        toOpen = attempt;
                        foreach (var oc in toOpen) oc.InteractAsync(pe, use, CancellationToken.None).Forget();
                        yield return new WaitForSecondsRealtime(1.4f);
                        var rr = Reach(targets, box.bounds.center, owner, mask, reach);
                        openR = Summ(rr);
                        foreach (var oc in toOpen) oc.InteractAsync(pe, use, CancellationToken.None).Forget();
                        yield return new WaitForSecondsRealtime(1.0f);
                        if (rr.stand + rr.crouch > 0) break;
                    }
                rows.Add(new { point = p.ID.ToString(), house = p.HouseZoneID.ToString(), pos = center.ToString("F2"), parent = p.transform.parent ? p.transform.parent.name : null, opened = toOpen.Select(c => c.name).ToArray(), asIs = Summ(closedR), afterOpening = openR });
                UnityEngine.Object.Destroy(go);
            }
            AgentBridge.Write(id, new { ok = true, count = rows.Count, reach, rows });
        }

        // facepos <x> <y> <z>: stand 0.7-1.5 m from a world point on the NavMesh and aim the camera at it.
        private static IEnumerator FacePos(string id, string[] a)
        {
            var player = AgentWorld.Player();
            if (!player || a.Length < 3) { AgentBridge.Write(id, new { ok = false, error = "usage: facepos x y z" }); yield break; }
            var target = new Vector3(F(a[0]), F(a[1]), F(a[2]));
            foreach (var r in new[] { 0.8f, 1.0f, 1.25f, 1.5f, 0.7f })
                for (int k = 0; k < 24; k++)
                {
                    var off = Quaternion.Euler(0, k * 15f, 0) * Vector3.forward * r;
                    var probe = target + off; probe.y = target.y - 0.6f;
                    if (!NavMesh.SamplePosition(probe, out var nh, 1.2f, NavMesh.AllAreas)) continue;
                    var eye = nh.position + Vector3.up * 1.6f;
                    if (Physics.Linecast(eye, target, out var lh, ~0, QueryTriggerInteraction.Ignore) && Vector3.Distance(lh.point, target) > 0.15f) continue;
                    var flat = target - eye; flat.y = 0;
                    float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, pitch = Mathf.Atan2(eye.y - target.y, flat.magnitude) * Mathf.Rad2Deg;
                    AgentWorld.Teleport(player, nh.position + Vector3.up * 0.05f, yaw, pitch);
                    yield return new WaitForSecondsRealtime(0.6f);
                    AgentBridge.Write(id, new { ok = true, standAt = nh.position.ToString("F2"), yaw, pitch });
                    yield break;
                }
            AgentBridge.Write(id, new { ok = false, error = "no clear sightline spot found" });
        }

        // actiontest <Map/Action> <Key> [Key...]: inject each key through the Input System queue and report whether the action fires (what the game reads).
        private static IEnumerator ActionTest(string id, string[] a)
        {
            var act = Controls.KeyBindings.Primary()?.asset.FindAction(a.Length > 0 ? a[0] : "");
            var kb = Keyboard.current;
            if (act == null || kb == null) { AgentBridge.Write(id, new { ok = false, error = "no such action or no keyboard" }); yield break; }
            var results = new List<object>();
            foreach (var name in a.Skip(1))
            {
                if (!Enum.TryParse<Key>(name, true, out var k)) { results.Add(new { key = name, error = "bad key" }); continue; }
                bool was = act.enabled; if (!was) act.Enable();
                bool hit = false;
                InputSystem.QueueStateEvent(kb, new KeyboardState(k));
                for (int i = 0; i < 4; i++) { yield return null; if (act.WasPressedThisFrame()) hit = true; }
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null; yield return null;
                if (!was) act.Disable();
                results.Add(new { key = name, fired = hit });
            }
            AgentBridge.Write(id, new { ok = true, action = act.name, binding = act.GetBindingDisplayString(), results });
        }

        // jumpkey <Key>: press a key with the player standing and report the highest point reached over the next 1.3 s.
        private static IEnumerator JumpKey(string id, string[] a)
        {
            var p = AgentWorld.Player(); var kb = Keyboard.current;
            if (!p || kb == null || a.Length < 1 || !Enum.TryParse<Key>(a[0], true, out var k)) { AgentBridge.Write(id, new { ok = false, error = "usage: jumpkey <Key>" }); yield break; }
            yield return new WaitForSecondsRealtime(0.6f);
            float y0 = p.transform.position.y, max = y0;
            InputSystem.QueueStateEvent(kb, new KeyboardState(k));
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.3f)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 0.15f) InputSystem.QueueStateEvent(kb, new KeyboardState());
                max = Mathf.Max(max, p.transform.position.y);
            }
            AgentBridge.Write(id, new { ok = true, key = k.ToString(), startY = y0, maxRise = max - y0, jumped = max - y0 > 0.25f });
        }

        // ---------- NPC warp log ----------
        // warplog start | dump | clear   (drive the clock with the console commands timescale / settime)
        private static IEnumerator WarpLog(string id, string[] a)
        {
            string sub = a.Length > 0 ? a[0] : "dump";
            if (sub == "start" && !_warpHooked)
            {
                var h = Plugin.Harmony;
                h.Patch(AccessTools.Method(typeof(FsmAiHelpers), "WarpToPositionAsync"), prefix: new HarmonyMethod(typeof(AgentProbes), nameof(HelperWarpPrefix)));
                h.Patch(AccessTools.Method(typeof(WarpToPosition), "ExecuteAsync"), prefix: new HarmonyMethod(typeof(AgentProbes), nameof(ActionWarpPrefix)));
                h.Patch(AccessTools.Method(typeof(FsmAiHelpers), "RotateToQuaternionAsync", new[] { typeof(ActionTask<AIActorEntity>), typeof(Quaternion), typeof(Vector3), typeof(float), typeof(CancellationToken) }),
                    prefix: new HarmonyMethod(typeof(AgentProbes), nameof(SnapPrefix)));
                h.Patch(AccessTools.Method(typeof(NavMeshAgent), "Warp", new[] { typeof(Vector3) }), postfix: new HarmonyMethod(typeof(AgentProbes), nameof(AgentWarpPostfix)));
                _warpHooked = true;
            }
            if (sub == "clear") Warps.Clear();
            AgentBridge.Write(id, new { ok = true, hooked = _warpHooked, count = Warps.Count, events = sub == "dump" ? Warps.ToArray() : null });
            yield break;
        }

        private static bool Visible(Vector3 p)
        {
            var cam = Camera.main; if (!cam) return false;
            var vp = cam.WorldToViewportPoint(p + Vector3.up);
            if (vp.z <= 0 || vp.x < 0 || vp.x > 1 || vp.y < 0 || vp.y > 1) return false;
            return !Physics.Linecast(cam.transform.position, p + Vector3.up, out _, ~0, QueryTriggerInteraction.Ignore);
        }

        private static void Log(string kind, AIActorEntity actor, Vector3 from, Vector3 to, string extra)
        {
            var player = AgentWorld.Player();
            var gt = UnityEngine.Object.FindFirstObjectByType<GameTimeController>();
            Warps.Add(new
            {
                kind, actor = actor ? actor.ActorID.ToString() : "?", day = gt ? gt.Days + 1 : -1, time = gt ? gt.FormattedTime() : "?",
                from = from.ToString("F1"), to = to.ToString("F1"), dist = Vector3.Distance(from, to),
                playerDist = player ? Vector3.Distance(player.transform.position, to) : -1f,
                fromVisible = Visible(from), toVisible = Visible(to),
                fsm = actor?.AiFsmController?.FsmOwner?.behaviour ? actor.AiFsmController.FsmOwner.behaviour.name : null,
                state = actor?.AiFsmController?.FsmOwner?.currentDeepStateName, extra,
            });
        }

        private static void HelperWarpPrefix(ActionTask<AIActorEntity> aiActorEntityTask, Entity entity, bool toWarpPoint, bool useAgent)
        {
            Vector3 to = Vector3.zero;
            if (toWarpPoint) { if (entity is IHaveWarpPoint w) to = w.WarpPoint.position; }
            else if (entity is IHaveInteractPoint ip) to = ip.InteractPoint.position;
            Log("fsm-warp", aiActorEntityTask.agent, aiActorEntityTask.agent.transform.position, to, $"entity={entity?.name} warpPoint={toWarpPoint} agent={useAgent}");
        }

        private static void ActionWarpPrefix(WarpToPosition __instance)
        {
            var actor = __instance.AIActionsController ? __instance.AIActionsController.GetComponentInParent<AIActorEntity>() : null;
            Log("action-warp", actor, actor ? actor.transform.position : Vector3.zero, Vector3.zero, "WarpToPosition action");
        }

        private static void SnapPrefix(ActionTask<AIActorEntity> aiActorEntityTask, Vector3 position)
        {
            var from = aiActorEntityTask.agent.transform.position;
            if (Vector3.Distance(from, position) > 0.3f) Log("align-snap", aiActorEntityTask.agent, from, position, "RotateToQuaternion lerps the transform through geometry");
        }

        private static void AgentWarpPostfix(NavMeshAgent __instance, object[] __args)
        {
            var actor = __instance.GetComponentInParent<AIActorEntity>();
            var to = __args[0] is Vector3 v ? v : Vector3.zero;
            var st = new System.Diagnostics.StackTrace(2, false);
            string caller = string.Join(" < ", Enumerable.Range(0, Math.Min(4, st.FrameCount)).Select(i => st.GetFrame(i).GetMethod()).Where(m => m != null).Select(m => m.DeclaringType?.Name + "." + m.Name));
            Log("agent.Warp", actor, actor ? actor.transform.position : Vector3.zero, to, caller);
        }
    }
}
