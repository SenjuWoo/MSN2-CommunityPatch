using System.Linq;
using System.Threading;
using Game.Entities;
using HarmonyLib;
using UnityEngine;

namespace MSN2CommunityPatch
{
    // NPC/trap inspection and repro helpers for the agent bridge.
    public static class AgentAI
    {
        public static EntitiesController Entities() => Object.FindFirstObjectByType<EntitiesController>();

        public static object Describe(AIActorEntity a) => new
        {
            actor = a.ActorID.ToString(),
            pos = a.transform.position.ToString("F1"),
            fsm = a.AiFsmController?.FsmOwner?.behaviour ? a.AiFsmController.FsmOwner.behaviour.name : null,
            rootState = a.AiFsmController?.FsmOwner?.currentRootStateName,
            deepState = a.AiFsmController?.FsmOwner?.currentDeepStateName,
            timeTableRunning = a.TimeTableController ? a.TimeTableController.IsRunning : (bool?)null,
            interval = a.TimeTableController?.CurrentInterval?.ToString(),
            interactingWith = a.Data?.CurrentlyInteractingWith?.ToString(),
            interactType = a.Data?.InteractType.ToString(),
            alert = a.AIAlertController ? a.AIAlertController.AlertStatus.ToString() : null,
            active = a.gameObject.activeInHierarchy,
        };

        private static TrapEntity[] Traps() => Object.FindObjectsByType<TrapEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);

        public static void Register()
        {
            AgentBridge.Register("npcs", (id, a) => AgentBridge.Done(id, Entities()?.GetAllAiActors().Select(Describe).ToArray()));
            AgentBridge.Register("traps", (id, a) => AgentBridge.Done(id, Traps().Select((t, i) => new
            {
                i, t.name, type = t.TrapType.ToString(), entity = t.EntityID?.ToString(), installed = t.Data.IsInstalled, busy = t.Data.IsBusy,
                house = t.HouseZone ? t.HouseZone.name : null, sub = t.SubZone ? t.SubZone.name : null, pos = t.transform.position.ToString("F1"),
                fsm = t.FSMToSwitch?.ToString()
            }).ToArray()));
            AgentBridge.Register("trapinstall", (id, a) =>
            {
                var t = Traps()[int.Parse(a[0])];
                t.Data.IsInstalled = true;
                return AgentBridge.Done(id, new { ok = true, t.name });
            });
            // Same path as the trap's trigger collider: TrapEntity.SwitchFSMAsync(npc).
            AgentBridge.Register("traptrigger", (id, a) =>
            {
                var t = Traps()[int.Parse(a[0])];
                var npc = Entities().GetAllAiActors().FirstOrDefault(n => n.ActorID.ToString() == a[1]);
                if (npc == null) return AgentBridge.Done(id, new { ok = false, error = "actor not found " + a[1] });
                AccessTools.Method(typeof(TrapEntity), "SwitchFSMAsync").Invoke(t, new object[] { npc, CancellationToken.None });
                return AgentBridge.Done(id, new { ok = true, trap = t.name, npc = Describe(npc) });
            });
        }
    }
}
