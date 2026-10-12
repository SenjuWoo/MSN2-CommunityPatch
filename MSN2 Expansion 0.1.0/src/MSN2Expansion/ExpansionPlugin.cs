using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSN2Expansion
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("senjuwoo.msn2.communitypatch", BepInDependency.DependencyFlags.SoftDependency)]
    public class ExpansionPlugin : BaseUnityPlugin
    {
        public const string Guid = "senjuwoo.msn2.expansion";
        public const string Name = "MSN2 Expansion";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static MissionRunner Runner;
        internal static ConfigEntry<bool> VerboseLog, ShowTracker;
        internal static ConfigEntry<string> JournalKey, AcceptKey, PrevKey, NextKey;
        internal static string PacksDir;

        private void Awake()
        {
            Log = Logger;
            VerboseLog = Config.Bind("Debug", "VerboseLog", false, "Log every game event the mission system sees and every state change.");
            ShowTracker = Config.Bind("UI", "ShowTracker", true, "Show the tracked job's objectives on screen.");
            JournalKey = Config.Bind("Keys", "Journal", "J", "Open/close the job journal (Unity Input System key name).");
            AcceptKey = Config.Bind("Keys", "Accept", "Enter", "Accept/track a job; advance dialogue.");
            PrevKey = Config.Bind("Keys", "Previous", "Comma", "Previous job in the journal.");
            NextKey = Config.Bind("Keys", "Next", "Period", "Next job in the journal.");

            PacksDir = Path.Combine(Path.GetDirectoryName(Info.Location), "packs");
            var errors = new List<string>();
            var packs = PackLoader.LoadAll(PacksDir, errors);
            foreach (var e in errors) Log.LogError($"[packs] {e}");
            Log.LogInfo($"{Name} {Version} | packs dir {PacksDir} | loaded {packs.Count} pack(s): {string.Join(", ", packs.ConvertAll(p => $"{p.Id} {p.Version} ({p.Missions.Count} missions)"))} | {errors.Count} error(s)");
            Runner = new MissionRunner(packs, errors);

            var h = new Harmony(Guid);
            Sidecar.Apply(h);
            GameHooks.Apply(h);

            var go = new GameObject("MSN2Expansion");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ExpansionRuntime>();
            go.AddComponent<ExpansionHud>();
            Runner.Changed += (m, s) => ExpansionHud.Instance?.MarkDirty();
            AgentCommands.TryRegister();
        }
    }

    // Attaches the mission runner to the Game scene once its controllers are initialized; detaches on scene change.
    public class ExpansionRuntime : MonoBehaviour
    {
        private float _nextTry;

        private void Update()
        {
            var r = ExpansionPlugin.Runner;
            bool inGame = SceneManager.GetActiveScene().name == "Game";
            if (!inGame)
            {
                if (r.Hooks != null) { var h = r.Hooks; r.Detach(); h.Dispose(); ExpansionPlugin.Log.LogInfo("[missions] detached (left Game scene)"); }
                return;
            }
            if (r.Hooks == null)
            {
                if (Time.unscaledTime < _nextTry) return;
                _nextTry = Time.unscaledTime + 1f;
                var hooks = GameHooks.TryCreate();
                if (hooks != null) r.Attach(hooks);
                return;
            }
            r.Tick();
        }
    }
}
