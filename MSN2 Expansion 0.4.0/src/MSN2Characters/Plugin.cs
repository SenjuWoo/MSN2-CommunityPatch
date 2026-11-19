using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Game.Entities;
using HarmonyLib;
using MSN2Characters.Skins;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSN2Characters
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("senjuwoo.msn2.communitypatch", BepInDependency.DependencyFlags.SoftDependency)]
    public class CharactersPlugin : BaseUnityPlugin
    {
        public const string Guid = "senjuwoo.msn2.characters";
        public const string Name = "MSN2 Characters";
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;
        internal static Harmony Harmony;
        internal static ConfigEntry<bool> VerboseLog;
        internal static string SkinsDir;
        // actor -> skin id from [Skins] (empty = vanilla)
        internal static readonly Dictionary<ActorID, ConfigEntry<string>> SkinFor = new Dictionary<ActorID, ConfigEntry<string>>();

        private void Awake()
        {
            Log = Logger;
            VerboseLog = Config.Bind("Debug", "VerboseLog", false, "Log skin loading and garment swaps in detail.");
            SkinFor[ActorID.Camila] = Config.Bind("Skins", "Camila", "lola", "Skin folder (under plugins/MSN2/skins) that replaces Camila's looks. Empty = vanilla Camila. Visual only: saves, schedules and scenes are unchanged.");
            CutNpcs.Neighbours.Bind(Config);
            SkinsDir = Path.Combine(Path.GetDirectoryName(Info.Location), "skins");
            Log.LogInfo($"{Name} {Version} | skins dir {SkinsDir} | Camila skin '{SkinFor[ActorID.Camila].Value}'");

            Harmony = new Harmony(Guid);
            TryApply("Skins", true, () => SkinApplier.Patch(Harmony));
            TryApply("Neighbours", true, () => CutNpcs.Neighbours.Patch(Harmony));

            var go = new GameObject("MSN2Characters");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<CharactersRuntime>();
            CharacterAgent.TryRegister();
        }

        internal static void TryApply(string name, bool enabled, Action apply)
        {
            if (!enabled) { Log.LogInfo($"[feature] {name} disabled in config"); return; }
            try { apply(); }
            catch (Exception e) { Log.LogError($"[feature] {name} failed to apply, vanilla behaviour kept: {e}"); }
        }
    }

    // Loads configured skin packs once (spread over frames from the main menu on) and applies them to their actor as
    // soon as the Game scene's EntitiesController has collected the actors.
    public class CharactersRuntime : MonoBehaviour
    {
        public static CharactersRuntime Instance;
        public static readonly Dictionary<string, SkinPack> Packs = new Dictionary<string, SkinPack>(StringComparer.OrdinalIgnoreCase);
        private float _nextTry;
        private EntitiesController _entities;
        private readonly HashSet<int> _failed = new HashSet<int>();

        private void Awake() { Instance = this; LoadConfigured(); }

        public void LoadConfigured()
        {
            foreach (var kv in CharactersPlugin.SkinFor)
            {
                var id = kv.Value.Value?.Trim();
                if (string.IsNullOrEmpty(id) || Packs.ContainsKey(id)) continue;
                var dir = Path.Combine(CharactersPlugin.SkinsDir, id);
                if (!File.Exists(Path.Combine(dir, "skin.json"))) { CharactersPlugin.Log.LogError($"[skin] {kv.Key}: skin '{id}' not found at {dir}; vanilla kept"); continue; }
                var pack = new SkinPack(dir);
                Packs[id] = pack;
                StartCoroutine(pack.Load());
            }
        }

        public void ReloadAll()
        {
            foreach (var st in new List<AppliedSkin>(SkinApplier.Applied.Values)) SkinApplier.Revert(st);
            foreach (var p in Packs.Values) p.Unload();
            Packs.Clear();
            _failed.Clear();
            LoadConfigured();
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != "Game") { _entities = null; return; }
            if (Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 0.1f;
            if (!_entities) _entities = FindFirstObjectByType<EntitiesController>();
            if (!_entities || _entities.Entities == null) return;
            CutNpcs.Neighbours.Tick(_entities);
            foreach (var kv in CharactersPlugin.SkinFor)
            {
                var id = kv.Value.Value?.Trim();
                if (string.IsNullOrEmpty(id) || !Packs.TryGetValue(id, out var pack) || pack.State != SkinPack.LoadState.Ready) continue;
                IActorEntity actor;
                try { actor = _entities.GetActorByActorID(kv.Key); } catch { continue; }
                if (actor == null || !(actor as MonoBehaviour)) continue;
                if (SkinApplier.Applied.TryGetValue(kv.Key, out var st) && st.Body && (st.Actor as MonoBehaviour) == (actor as MonoBehaviour)) continue;
                if (st != null) SkinApplier.Applied.Remove(kv.Key);   // new scene instance; the old state died with the scene
                int key = (actor as MonoBehaviour).GetInstanceID();
                if (_failed.Contains(key)) continue;
                if (SkinApplier.Apply(actor, pack) == null) _failed.Add(key);   // failure already logged; vanilla kept until skin reload
            }
        }
    }
}
