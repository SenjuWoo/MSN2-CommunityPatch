using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Game.Entities;
using Game.Input;
using Game.Interactables;
using Game.SceneCameras;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using _Game.Systems.CameraController;

namespace MSN2Expansion
{
    // Registers test commands with the Community Patch agent bridge when it is installed and enabled (soft dependency).
    // Test helpers drive the game's own code paths (MakeShot, InteractAsync, CompleteInstallation), not synthetic events.
    public static class AgentCommands
    {
        private static MethodInfo _register, _done, _write;

        public static void TryRegister()
        {
            var bridge = Type.GetType("MSN2CommunityPatch.AgentBridge, MSN2CommunityPatch");
            _register = bridge?.GetMethod("Register");
            _done = bridge?.GetMethod("Done");
            _write = bridge?.GetMethod("Write");
            if (_register == null) return;
            Reg("mx", Mx);
            Reg("snap", Snap);
            Reg("steal", Steal);
            Reg("cam", Cam);
            Reg("key", KeyPress);
            ExpansionPlugin.Log.LogDebug("[agent] expansion test commands registered (active only when the patch AgentBridge is enabled)");
        }

        private static void Reg(string name, Func<string, string[], IEnumerator> f) => _register.Invoke(null, new object[] { name, f });
        private static IEnumerator Done(string id, object r) => (IEnumerator)_done.Invoke(null, new[] { id, r });
        private static void Write(string id, object r) => _write.Invoke(null, new[] { id, r });

        private static MissionRunner R => ExpansionPlugin.Runner;

        private static object Summary() => new
        {
            attached = R.Hooks != null,
            slot = Sidecar.Slot,
            tracked = MissionRunner.S.TrackedMission,
            packs = R.Packs.Select(p => $"{p.Id} {p.Version}").ToArray(),
            errors = R.Errors,
            dialogue = ExpansionHud.Instance?.CurrentLine,
            journalOpen = ExpansionHud.Instance?.JournalOpen,
            missions = R.ById.Values.Select(m =>
            {
                var st = R.StateOf(m);
                return new
                {
                    m.Id, status = st.Status.ToString(),
                    objectives = m.Objectives.Concat(m.Bonus).Select(o => $"{o.Id}:{(R.IsDone(m, o) ? "done" : st.BonusFailed.Contains(o.Id) ? "FAILED" : R.ProgressOf(m, o) + "/" + o.Count)}").ToArray()
                };
            }).ToArray()
        };

        // mx | mx accept <id> | mx reset | mx event <type> key=value...
        private static IEnumerator Mx(string id, string[] a)
        {
            string sub = a.Length > 0 ? a[0] : "";
            if (sub == "accept") return Done(id, new { ok = R.Accept(a[1]), state = Summary() });
            if (sub == "hud") return Done(id, ExpansionHud.Instance?.Diagnose());
            if (sub == "arrest")
            {
                // the game's own caught-by-cop game-over state (same call CopActorEntity.CapturePlayer makes)
                var fsm = UnityEngine.Object.FindFirstObjectByType<Game.Core.GameFSMController>();
                Cysharp.Threading.Tasks.UniTaskExtensions.Forget(fsm.SetStateAsync<Game.GameEndState, Game.GameEndState.Arguments>(new Game.GameEndState.Arguments { GameOver = Game.GameEndState.Arguments.GameOverType.CaughtByCop }));
                return Done(id, new { ok = true });
            }
            if (sub == "audit") return Done(id, Audit());
            if (sub == "openhouse")
            {
                var hoc = UnityEngine.Object.FindFirstObjectByType<_Game.Systems.HouseOpenerController.HouseOpenerController>();
                hoc.OpenHouse((Game.Zones.HouseZoneID)Enum.Parse(typeof(Game.Zones.HouseZoneID), a[1]));
                return Done(id, new { ok = hoc.IsHouseOpened((Game.Zones.HouseZoneID)Enum.Parse(typeof(Game.Zones.HouseZoneID), a[1])) });
            }
            if (sub == "turnin") return Done(id, new { ok = R.TurnIn(a[1]), state = Summary() });
            if (sub == "reset") { Sidecar.State = new ExpansionState(); R.OnStateReplaced(); return Done(id, Summary()); }
            if (sub == "event" && R.Hooks != null)
            {
                var e = new GameEvent { Type = a[1] };
                foreach (var kv in a.Skip(2).Select(x => x.Split('=')))
                    typeof(GameEvent).GetField(char.ToUpper(kv[0][0]) + kv[0].Substring(1))?.SetValue(e, kv[0] == "naked" ? (object)bool.Parse(kv[1]) : kv[1]);
                R.Hooks.Raise(e);
                return Done(id, new { synthetic = e.ToString(), state = Summary() });
            }
            return Done(id, Summary());
        }

        // snap <Actor>: stand 2 m in front of the NPC, aim at her chest, take a real phone photo.
        private static IEnumerator Snap(string id, string[] a)
        {
            var npc = R.Hooks?.Entities.GetAllAiActors().FirstOrDefault(n => n.ActorID.ToString() == a[0]);
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var cam = UnityEngine.Object.FindFirstObjectByType<PhoneCameraController>();
            if (!npc || !player || !cam) { Write(id, new { ok = false, error = "npc/player/camera missing" }); yield break; }
            // nearest NavMesh spot on her floor with a clear line of sight to her chest (walls block the naive "in front of her")
            var chestPos = npc.transform.position + Vector3.up * 1.25f;
            Vector3? spot = null;
            foreach (var dist in new[] { 1.4f, 1.9f, 2.5f })
                for (int k = 0; k < 16 && spot == null; k++)
                {
                    var dir = Quaternion.Euler(0, k * 22.5f, 0) * npc.transform.forward;
                    if (!NavMesh.SamplePosition(npc.transform.position + dir * dist, out var nh, 0.5f, NavMesh.AllAreas) || Mathf.Abs(nh.position.y - npc.transform.position.y) > 0.6f) continue;
                    var eye = nh.position + Vector3.up * 1.6f;
                    if (!Physics.Linecast(eye, chestPos, out var lh, ~0, QueryTriggerInteraction.Ignore) || lh.transform.IsChildOf(npc.transform)) spot = nh.position;
                }
            if (spot == null) { Write(id, new { ok = false, error = "no visible spot near " + a[0] }); yield break; }
            Teleport(player, spot.Value + Vector3.up * 0.1f);
            yield return null;
            var camPos = player.CinemachineCameraTarget.transform.position;
            var chest = npc.transform.position + Vector3.up * 1.25f;
            var flat = chest - camPos; flat.y = 0;
            player.transform.rotation = Quaternion.LookRotation(flat);
            float pitch = Mathf.Atan2(camPos.y - chest.y, flat.magnitude) * Mathf.Rad2Deg;
            AccessTools.FieldRefAccess<FirstPersonController, float>("_cinemachineTargetPitch")(player) = pitch;
            player.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            cam.CameraTurnOn(true);
            yield return new WaitForSecondsRealtime(0.8f);
            int before = Progress();
            AccessTools.Method(typeof(PhoneCameraController), "MakeShot").Invoke(cam, null);
            yield return new WaitForSecondsRealtime(0.8f);
            cam.CameraTurnOn(false);
            Write(id, new { ok = true, npc = npc.ActorID.ToString(), npcPos = npc.transform.position.ToString("F1"), pitch, progressDelta = Progress() - before, state = Summary() });
        }

        // steal [Item] [House]: interact with the first matching stealable exactly like pressing the use key on it.
        private static IEnumerator Steal(string id, string[] a)
        {
            var player = R.Hooks?.Entities.GetPlayerEntity();
            var s = UnityEngine.Object.FindObjectsByType<StealableEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                .FirstOrDefault(x => (a.Length < 1 || x.Data.GetType().Name.StartsWith(a[0])) &&
                                     (a.Length < 2 || R.Hooks.StealHouse(x) == a[1]));
            if (!s || !player) { Write(id, new { ok = false, error = "no matching stealable/player" }); yield break; }
            int before = Progress();
            var m = typeof(StealableEntity).GetMethod("InteractAsync").MakeGenericMethod(typeof(PlayerEntity.PlayerData));
            m.Invoke(s, new object[] { player, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }, CancellationToken.None });
            yield return new WaitForSecondsRealtime(0.5f);
            Write(id, new { ok = true, item = s ? s.Data.GetType().Name : "(destroyed)", progressDelta = Progress() - before, state = Summary() });
        }

        // cam <House>: run the camera install completion on the first uninstalled hidden-camera spot in that house.
        private static IEnumerator Cam(string id, string[] a)
        {
            var c = UnityEngine.Object.FindObjectsByType<HiddenCameraEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID)
                .FirstOrDefault(x => !x.Data.IsInstalled && x.HouseZone && (a.Length < 1 || x.HouseZone.ZoneID.ToString() == a[0]));
            if (!c) { Write(id, new { ok = false, error = "no free camera spot" }); yield break; }
            int before = Progress();
            AccessTools.Method(typeof(HiddenCameraEntity), "CompleteInstallation").Invoke(c, null);
            yield return null;
            Write(id, new { ok = c.Data.IsInstalled, spot = c.name, room = c.SubZone ? c.SubZone.SubZoneType.ToString() : null, progressDelta = Progress() - before, state = Summary() });
        }

        // key <Name>: press and release through the Input System event queue (same path as a physical key).
        private static IEnumerator KeyPress(string id, string[] a)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !Enum.TryParse<UnityEngine.InputSystem.Key>(a[0], true, out var k)) { Write(id, new { ok = false, error = "no keyboard or bad key " + a[0] }); yield break; }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
            yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null; yield return null;
            Write(id, new { ok = true, key = k.ToString(), state = Summary() });
        }

        // World facts the pack depends on: NPC schedules, hidden-camera spots per room, theft spots per house.
        private static object Audit() => new
        {
            schedules = R.Hooks?.Entities.GetAllAiActors().Where(n => n.TimeTableController && n.TimeTableController.TimeTableSO)
                .ToDictionary(n => n.ActorID.ToString(), n => n.TimeTableController.TimeTableSO.Select(i => $"{i.StartHour:00}-{i.EndHour:00} {i.Name} [{i.FsmID}]").ToArray()),
            cameraSpots = UnityEngine.Object.FindObjectsByType<HiddenCameraEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .GroupBy(c => c.SubZone ? c.SubZone.SubZoneType.ToString() : "(no room)").OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            theftSpots = UnityEngine.Object.FindObjectsByType<Game.MapPoints.TheftMapPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .GroupBy(p => p.HouseZoneID.ToString()).ToDictionary(g => g.Key, g => g.Count()),
        };

        private static int Progress() => MissionRunner.S.Missions.Values.Sum(s => s.Progress.Values.Sum());

        private static void Teleport(FirstPersonController p, Vector3 pos)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            p.transform.position = pos;
            if (cc) cc.enabled = true;
        }
    }
}
