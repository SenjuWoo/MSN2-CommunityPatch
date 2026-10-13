using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using NodeCanvas.Framework;
using NodeCanvas.StateMachines;

namespace MSN2CommunityPatch.Fixes
{
    // Vanilla AIFSMController.InterruptGraphAsync waits for every running nested FSM to set "Complete" with no timeout.
    // Traps, police and sex interrupts await it; a sub-FSM that never acknowledges ShouldInterrupt freezes the NPC
    // (schedule stopped, trap left busy). Same logic, plus a realtime timeout after which the caller continues.
    public static class InterruptTimeoutFix
    {
        public static void Apply(Harmony h)
        {
            var m = AccessTools.Method(typeof(AIFSMController), "InterruptGraphAsync");
            if (m == null) { Plugin.Log.LogWarning("[fix] InterruptTimeout: InterruptGraphAsync not found; skipped"); return; }
            h.Patch(m, prefix: new HarmonyMethod(typeof(InterruptTimeoutFix), nameof(Prefix)));
            Plugin.Log.LogInfo($"[fix] InterruptTimeout active ({Plugin.InterruptTimeoutSeconds.Value:F0}s)");
        }

        private static bool Prefix(AIFSMController __instance, InterruptionType interruptionType, Graph fsm, CancellationToken cancellationToken, ref UniTask __result)
        {
            __result = Run(__instance, interruptionType, fsm, cancellationToken);
            return false;
        }

        private static async UniTask Run(AIFSMController ctl, InterruptionType type, Graph fsm, CancellationToken ct)
        {
            if (ctl.FsmOwner == null || ctl.FsmOwner.behaviour == null || fsm == null) return;
            foreach (var node in fsm.allNodes.ToList())
            {
                if (!(node is NestedFSMState nested) || nested.status != Status.Running) continue;
                var sub = nested.subGraph;
                sub.blackboard.SetVariableValue("InterruptionType", type);
                sub.blackboard.SetVariableValue("ShouldInterrupt", true);
                var complete = sub.blackboard.GetVariable<bool>("Complete");
                if (complete == null) continue;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    bool timedOut = await UniTask.WaitUntil(() => complete.value, PlayerLoopTiming.Update, cts.Token)
                        .TimeoutWithoutException(TimeSpan.FromSeconds(Plugin.InterruptTimeoutSeconds.Value), DelayType.Realtime, PlayerLoopTiming.Update, cts);
                    if (timedOut)
                        Plugin.Log.LogWarning($"[fix] InterruptTimeout: {ctl.name} sub-FSM '{sub.name}' state '{(sub as FSM)?.currentStateName}' ignored {type} interrupt for {Plugin.InterruptTimeoutSeconds.Value:F0}s; continuing");
                }
            }
        }
    }
}
