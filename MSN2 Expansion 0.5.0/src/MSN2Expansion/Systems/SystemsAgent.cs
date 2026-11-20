using System;
using System.Collections;
using System.Linq;
using Game.Entities;
using Game.Interactables;
using HarmonyLib;
using MSN2Expansion.Systems;
using UnityEngine;

namespace MSN2Expansion
{
    // sys ... : test commands for the 0.5.0 systems (agent bridge)
    public static partial class AgentCommands
    {
        private static IEnumerator Sys(string id, string[] a)
        {
            var sub = a.Length > 0 ? a[0].ToLowerInvariant() : "info";
            var S = Sidecar.State;
            float F(int i, float d) => a.Length > i && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;
            switch (sub)
            {
                case "info":
                    return Done(id, new
                    {
                        schema = S.Schema, stats = S.Stats, gallery = S.Gallery, trophies = S.Trophies, safes = S.SafesOpened,
                        altarShown = Altar.Shown, altarMult = Altar.MoneyMultiplier(), telescopeActive = Telescope.Active,
                        rain = new { Rain.Raining, Rain.Day, Rain.StartHour, Rain.EndHour, Rain.Forced },
                        difficulty = SystemsConfig.Difficulty.Value, panel = ExpansionHud.Instance?.SystemPanelText,
                    });
                case "ray":
                {
                    var cam = Camera.main;
                    if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return Done(id, new { ok = false });
                    return Done(id, new { ok = true, point = V(hit.point), normal = V(hit.normal), obj = Path(hit.transform), cam = V(cam.transform.position), yaw = cam.transform.eulerAngles.y });
                }
                case "scan":   // sys scan [height]: horizontal rays every 15 degrees from the camera, to find walls/windows of a room
                {
                    var c = Camera.main.transform.position; c.y += F(1, 0);
                    return Done(id, Enumerable.Range(0, 24).Select(i =>
                    {
                        var d = Quaternion.Euler(0, i * 15, 0) * Vector3.forward;
                        return Physics.Raycast(c, d, out var h, 15f, ~0, QueryTriggerInteraction.Ignore) ? $"{i * 15,3}: {h.distance:0.00} m {V(h.point)} {h.transform.name}" : $"{i * 15,3}: none";
                    }).ToArray());
                }
                case "altar":
                    if (a.Length >= 4) { Altar.ShelfOrigin = new Vector3(F(1, 0), F(2, 0), F(3, 0)); if (a.Length >= 5) Altar.ShelfRight = Quaternion.Euler(0, F(4, 0), 0) * Vector3.right; }
                    Altar.Build();
                    return Done(id, new { shown = Altar.Shown, origin = V(Altar.ShelfOrigin), mult = Altar.MoneyMultiplier() });
                case "trophy":   // sys trophy <Kind>: as if stolen (test)
                    S.Trophies.Add(a[1]); Altar.Build();
                    return Done(id, new { shown = Altar.Shown, kinds = Altar.Kinds() });
                case "telescope":
                    if (a.Length >= 4) { Telescope.Position = new Vector3(F(1, 0), F(2, 0), F(3, 0)); Telescope.Yaw = F(4, Telescope.Yaw); }
                    Telescope.Build();
                    return Done(id, new { pos = V(Telescope.Position), active = Telescope.Active, zoomMax = Traverse.Create(UnityEngine.Object.FindFirstObjectByType<_Game.Systems.CameraController.PhoneCameraController>()).Field("ZoomMax").GetValue() });
                case "safes":
                {
                    var safes = UnityEngine.Object.FindObjectsByType<OpenCloseEntity>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID).Where(Safes.IsSafe).ToArray();
                    if (a.Length >= 3 && a[1] == "open") Safes.Open(safes[int.Parse(a[2])]);
                    if (a.Length >= 3 && a[1] == "try") return Done(id, new { result = Safes.Try(a[2]), panel = ExpansionHud.Instance?.SystemPanelText });
                    return Done(id, safes.Select((s, i) => new { i, key = Safes.SafeKey(s), pos = V(s.transform.position), opened = s.Data.IsOpened, locked = s.Data.IsLocked, cracked = S.SafesOpened.Contains(Safes.SafeKey(s)), code = a.Length > 1 && a[1] == "codes" ? Safes.CodeFor(s) : null }).ToArray());
                }
                case "rain":
                    if (a.Length > 1) { Rain.Forced = a[1] == "on"; if (a[1] == "plan") Rain.PlanDay(int.Parse(a[2])); }
                    return Done(id, new { Rain.Raining, Rain.Forced, Rain.Day, Rain.StartHour, Rain.EndHour, Rain.Indoors });
                case "noise":   // sys noise <volume>: what one player noise adds to the accumulator under the current rain/difficulty
                {
                    var nc = UnityEngine.Object.FindFirstObjectByType<Game.Noise.NoiseController>();
                    var p = R.Hooks.Entities.GetPlayerEntity();
                    float before = nc.NoiseAccumulation;
                    nc.SendNoise(ActorID.Player, p.transform.position, F(1, 20));
                    return Done(id, new { added = nc.NoiseAccumulation - before, rain = Rain.Raining, difficulty = SystemsConfig.Difficulty.Value, inZone = nc.NoiseAccumulation != before });
                }
                case "alerttime":   // sys alerttime <dist>: EyesSensor.GetAlertTime for every NPC eye sensor
                {
                    var m = AccessTools.Method(typeof(EyesSensor), "GetAlertTime");
                    var es = UnityEngine.Object.FindObjectsByType<EyesSensor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Take(3).ToArray();
                    return Done(id, new { difficulty = SystemsConfig.Difficulty.Value, times = es.Select(e => (float)m.Invoke(e, new object[] { F(1, 5f) })).ToArray() });
                }
                case "diff":   // sys diff <Vanilla|Easy|Normal|Hard>: set the preset for this session
                    if (a.Length > 1) SystemsConfig.Difficulty.Value = a[1];
                    return Done(id, new { difficulty = SystemsConfig.Difficulty.Value, mult = Difficulty.Mult() });
                case "unstick":   // test only: the agent `sex` command finishes the minigame faster than the cum animation event; clear IsInSex
                {
                    var sc = UnityEngine.Object.FindFirstObjectByType<Game.Sex.SexController>();
                    bool was = sc.IsInSex;
                    Traverse.Create(sc).Property("IsInSex").SetValue(false);
                    return Done(id, new { was, now = sc.IsInSex });
                }
                case "replay":
                    return Done(id, new { result = SystemsRuntime.Instance.StartReplay(S.Gallery[int.Parse(a[1])]) ?? "started" });
                case "key":   // sys key <G|K|...>: same as pressing it (uses the `key` command path)
                    return KeyPress(id, a.Skip(1).ToArray());
                default:
                    return Done(id, new { ok = false, error = "sys info|ray|altar [x y z yaw]|trophy <Kind>|telescope [x y z yaw]|safes [open i|try code|codes]|rain [on|off|plan d]|noise [vol]|alerttime [dist]|replay <i>" });
            }
        }

        private static string V(Vector3 v) => $"{v.x:0.00} {v.y:0.00} {v.z:0.00}";
        private static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
    }
}
