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
            Reg("sex", Sex);
            Reg("fap", Fap);
            // addhours <n>: advance the clock through GameTimeController.AddHours (day changes fire as in play; console settime resets to day 1)
            // calm [Actor]: AIAlertController.CalmDown(resetAlert: true) for one or all NPCs, the call TimeSkipView/PoliceController make
            Reg("calm", (id, a) =>
            {
                var npcs = R.Hooks.Entities.GetAllAiActors().Where(n => n.AIAlertController && (a.Length == 0 || n.ActorID.ToString() == a[0])).ToList();
                foreach (var n in npcs) n.AIAlertController.CalmDown(true);
                return Done(id, new { ok = true, calmed = npcs.Select(n => n.ActorID.ToString()).ToArray() });
            });
            Reg("addhours", (id, a) => { R.Hooks.Time.AddHours(int.Parse(a[0])); return Done(id, Summary()); });
            ExpansionPlugin.Log.LogDebug("[agent] expansion test commands registered (active only when the patch AgentBridge is enabled)");
        }

        private static void Reg(string name, Func<string, string[], IEnumerator> f) => _register.Invoke(null, new object[] { name, f });
        private static IEnumerator Done(string id, object r) => (IEnumerator)_done.Invoke(null, new[] { id, r });
        private static void Write(string id, object r) => _write.Invoke(null, new[] { id, r });

        private static MissionRunner R => ExpansionPlugin.Runner;

        private static object Summary() => new
        {
            attached = R.Hooks != null,
            money = UnityEngine.Object.FindFirstObjectByType<Game.Core.GameDataAccessor>()?.GameData?.Money,
            subscribers = UnityEngine.Object.FindFirstObjectByType<Game.Core.GameDataAccessor>()?.GameData?.PCData?.Subscribers,
            day = R.Hooks != null ? R.Hooks.Time.Days + 1 : 0, hour = R.Hooks != null ? R.Hooks.Time.Hours : 0,
            inventory = Inventory(),
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
                .FirstOrDefault(x => !x.Data.IsInstalled && x.HouseZone && (a.Length < 1 || x.HouseZone.ZoneID.ToString() == a[0]) && (a.Length < 2 || (x.SubZone && x.SubZone.SubZoneType.ToString() == a[1])));
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

        private static object Inventory()
        {
            var inv = UnityEngine.Object.FindFirstObjectByType<Game.Inventory.InventoryController>();
            return inv ? Enum.GetValues(typeof(Game.Inventory.ItemID)).Cast<Game.Inventory.ItemID>().ToDictionary(i => i.ToString(), i => inv.GetItemCount(i)) : null;
        }

        private static int Progress() => MissionRunner.S.Missions.Values.Sum(s => s.Progress.Values.Sum());

        // Records the events GameHooks raises (the same stream MissionRunner consumes) while a test runs.
        private static System.Collections.Generic.List<string> Listen(out Action stop)
        {
            var seen = new System.Collections.Generic.List<string>();
            Action<GameEvent> h = e => seen.Add(e.ToString());
            R.Hooks.OnEvent += h;
            stop = () => R.Hooks.OnEvent -= h;
            return seen;
        }

        private static object PsInfo(Component pcc)
        {
            var ps = pcc ? pcc.GetComponent<ParticleSystem>() : null;
            if (!ps) return null;
            var col = ps.collision;
            var mask = col.collidesWith.value;
            var names = Enumerable.Range(0, 32).Where(i => (mask & (1 << i)) != 0).Select(i => LayerMask.LayerToName(i)).ToArray();
            return new { col.enabled, type = col.type.ToString(), mode = col.mode.ToString(), quality = col.quality.ToString(), sendMessages = col.sendCollisionMessages, layers = names, radiusScale = col.radiusScale };
        }

        // sex <Actor> [bedIndex]: the game's sex scene with the NPC on the nearest bed of her house (SexController.StartSexAsync, the call
        // AIActorEntity.InteractAsync makes), then the minigame through SexController's own slide/interrupt calls until the win.
        private static IEnumerator Sex(string id, string[] a)
        {
            var npc = R.Hooks?.Entities.GetAllAiActors().FirstOrDefault(n => a.Length > 0 && n.ActorID.ToString() == a[0]);
            var sc = UnityEngine.Object.FindFirstObjectByType<Game.Sex.SexController>();
            var beds = UnityEngine.Object.FindObjectsByType<BedEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            if (!npc || !sc || beds.Length == 0) { Write(id, new { ok = false, error = "npc, SexController or bed missing" }); yield break; }
            var bed = a.Length > 1 ? beds[int.Parse(a[1])] : beds.OrderBy(b => Vector3.Distance(b.transform.position, npc.transform.position)).First();
            var seen = Listen(out var stop);
            npc.Data.CurrentlyInteractingWith = bed.EntityID;
            Cysharp.Threading.Tasks.UniTaskExtensions.Forget(sc.StartSexAsync(bed, npc, CancellationToken.None));
            float t0 = Time.realtimeSinceStartup;
            while (!sc.IsInSex && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            bool started = sc.IsInSex;
            yield return new WaitForSecondsRealtime(1.5f);
            int hits = 0, results = 0;
            while (sc.IsInSex && sc.Progress < 1f && results < 30)
            {
                sc.StartMinigameSlide();                                   // tap button pressed
                float s0 = Time.realtimeSinceStartup;
                while (sc.SliderValue < 0.72f && Time.realtimeSinceStartup - s0 < 1.5f) yield return null;
                if (sc.InterruptSlideAndGetResult(0.6f, 1f) == Game.Sex.SexController.SexMinigameResult.Success) hits++;   // released inside the success bar
                results++;
                yield return new WaitForSecondsRealtime(0.4f);
            }
            t0 = Time.realtimeSinceStartup;
            while (sc.IsInSex && Time.realtimeSinceStartup - t0 < 20f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            stop();
            Write(id, new { ok = started && !sc.IsInSex, npc = npc.ActorID.ToString(), bed = bed.name, started, hits, results, ended = !sc.IsInSex, events = seen, state = Summary() });
        }

        // fap <Actor>: stand ~1 m in front of the NPC, take out the Penis tool (PlayerToolsController) and stroke through FapController's own
        // hand-move code until it cums; reports the OnActorCummed hits and the events GameHooks raised.
        private static IEnumerator Fap(string id, string[] a)
        {
            var npc = R.Hooks?.Entities.GetAllAiActors().FirstOrDefault(n => a.Length > 0 && n.ActorID.ToString() == a[0]);
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            var fc = UnityEngine.Object.FindFirstObjectByType<Game.Fapping.FapController>();
            var tools = UnityEngine.Object.FindFirstObjectByType<Game.ToolsController.PlayerToolsController>();
            if (!npc || !player || !fc || !tools) { Write(id, new { ok = false, error = "npc/player/FapController/tools missing" }); yield break; }
            float dist = a.Length > 1 ? float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 1.0f;
            Vector3? spot = null;
            for (int k = 0; k < 16 && spot == null; k++)
            {
                var dir = Quaternion.Euler(0, k * 22.5f, 0) * npc.transform.forward;
                if (!NavMesh.SamplePosition(npc.transform.position + dir * dist, out var nh, 0.4f, NavMesh.AllAreas) || Mathf.Abs(nh.position.y - npc.transform.position.y) > 0.4f) continue;
                // triggers count as blockers: FapController credits the first collider a particle hits (a bed's hide trigger steals the hit)
                if (Physics.Linecast(nh.position + Vector3.up * 0.9f, npc.transform.position + Vector3.up * 0.9f, out var lh, ~0, QueryTriggerInteraction.Collide) && lh.transform.IsChildOf(npc.transform)) spot = nh.position;
            }
            if (spot == null) { Write(id, new { ok = false, error = "no clear spot near " + a[0] }); yield break; }
            Teleport(player, spot.Value + Vector3.up * 0.05f);
            var flat = npc.transform.position - spot.Value; flat.y = 0;
            player.transform.rotation = Quaternion.LookRotation(flat);
            float pitch = a.Length > 2 ? float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;   // the penis rig follows the camera pitch; negative = up
            AccessTools.FieldRefAccess<FirstPersonController, float>("_cinemachineTargetPitch")(player) = pitch;
            player.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            // The NPC's root capsule (what FapController credits as an actor hit) is off in most FSM states; the FSM turns it on where
            // the game expects a cum-on. `fap <Actor> <dist> <pitch> capsule` turns it on through AIActorEntity.EnableCapsule for the test.
            bool forceCapsule = a.Length > 3 && a[3] == "capsule";
            var capsule = npc.GetComponent<CapsuleCollider>();
            bool capsuleWas = capsule && capsule.enabled;
            if (forceCapsule) npc.EnableCapsule(true);
            var cummed = new System.Collections.Generic.List<string>();
            var sub = R3.ObservableSubscribeExtensions.Subscribe(fc.OnActorCummed, x => cummed.Add(x.ToString()));
            var seen = Listen(out var stop);
            Cysharp.Threading.Tasks.UniTaskExtensions.Forget(tools.ActivateToolAsync(Game.Core.PlayerTool.Penis, CancellationToken.None));
            yield return new WaitForSecondsRealtime(1.5f);
            var move = AccessTools.Method(typeof(Game.Fapping.FapController), "MoveHand");
            float t0 = Time.realtimeSinceStartup; int strokes = 0; bool came = false;
            while (Time.realtimeSinceStartup - t0 < 30f)
            {
                if (!came) { move.Invoke(fc, new object[] { (strokes % 2 == 0 ? 1f : -1f) * 8f }); strokes++; }
                if (fc.CumBarValue >= 1f || (came == false && strokes > 2 && fc.CumBarValue <= 0.26f && strokes > 50)) came = true;
                if (came && Time.realtimeSinceStartup - t0 > 1f && cummed.Count > 0) { yield return new WaitForSecondsRealtime(1f); break; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(3f);
            Cysharp.Threading.Tasks.UniTaskExtensions.Forget(tools.ActivateToolAsync(Game.Core.PlayerTool.Hand, CancellationToken.None));
            yield return new WaitForSecondsRealtime(1f);
            sub.Dispose(); stop();
            if (forceCapsule) npc.EnableCapsule(capsuleWas);
            var pcc = AccessTools.Field(typeof(Game.Fapping.FapController), "_particlesCollisionCallback").GetValue(fc) as Component;
            var caps = npc.GetComponent<Collider>();
            Write(id, new { ok = cummed.Count > 0, npc = npc.ActorID.ToString(), forceCapsule, strokes, cumBar = fc.CumBarValue, onActorCummed = cummed, events = seen,
                emitter = pcc ? pcc.transform.position.ToString("F2") + " fwd " + pcc.transform.forward.ToString("F2") : null,
                npcCollider = caps ? caps.GetType().Name + " enabled=" + caps.enabled + " layer=" + LayerMask.LayerToName(caps.gameObject.layer) + " " + caps.bounds.center.ToString("F2") + " size " + caps.bounds.size.ToString("F2") : "none",
                npcState = npc.AiFsmController?.FsmOwner?.graph?.name,
                particleCollision = PsInfo(pcc),
                npcColliders = npc.GetComponentsInChildren<Collider>(true).Select(c => $"{c.name} {c.GetType().Name} {LayerMask.LayerToName(c.gameObject.layer)} en={c.enabled} trig={c.isTrigger} actor={(c.GetComponent<IActorEntity>() != null)}").Take(30).ToArray(),
                state = Summary() });
        }

        private static void Teleport(FirstPersonController p, Vector3 pos)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            p.transform.position = pos;
            if (cc) cc.enabled = true;
        }
    }
}
