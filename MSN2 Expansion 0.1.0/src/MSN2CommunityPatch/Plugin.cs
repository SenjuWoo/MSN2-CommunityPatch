using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MSN2CommunityPatch.Fixes;
using MSN2CommunityPatch.Graphics;
using UnityEngine;

namespace MSN2CommunityPatch
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "senjuwoo.msn2.communitypatch";
        public const string Name = "MSN2 Community Patch";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> VerboseLog;
        internal static ConfigEntry<bool> AgentBridgeEnabled;
        internal static ConfigEntry<float> HitchThresholdMs;
        internal static ConfigEntry<bool> LoopTiming;
        internal static ConfigEntry<float> InterruptTimeoutSeconds;

        private void Awake()
        {
            Log = Logger;
            VerboseLog = Config.Bind("Debug", "VerboseLog", false, "Log resolved config, state changes and hitches in detail.");
            AgentBridgeEnabled = Config.Bind("Debug", "AgentBridge", false, "Poll BepInEx/MSN2/agent/cmd.txt for diagnostic/test commands (local file only).");
            LoopTiming = Config.Bind("Debug", "PlayerLoopTiming", false, "Time every engine player-loop phase; with VerboseLog, hitches name the slow phase. Small CPU cost.");
            HitchThresholdMs = Config.Bind("Debug", "HitchThresholdMs", 100f, "Frames slower than this are logged as hitches when VerboseLog is on.");
            var houseHud = Config.Bind("Fixes", "HouseHudExceptionSpam", true, "Fix the per-frame 'CurrentHouseDataSO is null' exception and wrong resident portraits.");
            var interrupt = Config.Bind("Fixes", "NpcInterruptTimeout", true, "Stop NPCs freezing forever when an action ignores an interrupt (traps, police, sex).");
            InterruptTimeoutSeconds = Config.Bind("Fixes", "NpcInterruptTimeoutSeconds", 8f, new ConfigDescription("Seconds to wait for an NPC action to acknowledge an interrupt.", new AcceptableValueRange<float>(2f, 60f)));
            GraphicsTuner.Bind(Config);

            Log.LogInfo($"{Name} {Version} | Unity {Application.unityVersion} | {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} | {SystemInfo.processorType}");
            Harmony = new Harmony(Guid);
            TryApply("HouseHud", houseHud.Value, () => HouseHudFix.Apply(Harmony));
            TryApply("InterruptTimeout", interrupt.Value, () => InterruptTimeoutFix.Apply(Harmony));
            if (VerboseLog.Value)
                foreach (var kv in Config) Log.LogInfo($"[cfg] {kv.Key.Section}.{kv.Key.Key} = {kv.Value.BoxedValue}");

            var go = new GameObject("MSN2CommunityPatch");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FrameMonitor>();
            go.AddComponent<GraphicsTuner>();
            if (AgentBridgeEnabled.Value) { go.AddComponent<AgentBridge>(); AgentWorld.Register(); AgentAI.Register(); }
        }

        private static void TryApply(string name, bool enabled, Action apply)
        {
            if (!enabled) { Log.LogInfo($"[fix] {name} disabled in config"); return; }
            try { apply(); }
            catch (Exception e) { Log.LogError($"[fix] {name} failed to apply, vanilla behaviour kept: {e}"); }
        }
    }
}
