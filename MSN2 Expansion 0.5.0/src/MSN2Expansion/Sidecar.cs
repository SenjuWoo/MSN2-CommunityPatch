using System;
using System.IO;
using Game.Core;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace MSN2Expansion
{
    // Expansion progress lives in saveNN.msn2x.json beside the vanilla saveNN.dat, which is never modified.
    // Removing the mod leaves vanilla saves loadable; the sidecar is simply ignored.
    public static class Sidecar
    {
        public static ExpansionState State = new ExpansionState();
        public static int Slot = -1;

        public static string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"save{slot:00}.msn2x.json");

        public static void Apply(Harmony h)
        {
            var t = typeof(GameDataManager);
            h.Patch(AccessTools.Method(t, nameof(GameDataManager.Load), new[] { typeof(int) }), postfix: new HarmonyMethod(typeof(Sidecar), nameof(LoadPostfix)));
            h.Patch(AccessTools.Method(t, nameof(GameDataManager.Save), new[] { typeof(GameData), typeof(int) }), postfix: new HarmonyMethod(typeof(Sidecar), nameof(SavePostfix)));
            h.Patch(AccessTools.Method(t, nameof(GameDataManager.ResetGameData)), postfix: new HarmonyMethod(typeof(Sidecar), nameof(ResetPostfix)));
            h.Patch(AccessTools.Method(t, nameof(GameDataManager.DeleteSave), new[] { typeof(int) }), postfix: new HarmonyMethod(typeof(Sidecar), nameof(DeletePostfix)));
        }

        private static void LoadPostfix(GameDataManager __instance, int slotIndex, GameData __result)
        {
            if (__result == null || !__instance.IsSavingEnabled) return;
            Slot = slotIndex;
            var file = PathFor(slotIndex);
            try
            {
                State = File.Exists(file) ? JsonConvert.DeserializeObject<ExpansionState>(File.ReadAllText(file)) ?? new ExpansionState() : new ExpansionState();
                ExpansionPlugin.Log.LogInfo($"[save] slot {slotIndex}: {(File.Exists(file) ? "loaded" : "no sidecar, fresh")} expansion state ({State.Missions.Count} missions)");
            }
            catch (Exception e)
            {
                // keep the broken file for inspection, start clean rather than blocking the vanilla load
                ExpansionPlugin.Log.LogError($"[save] sidecar {file} unreadable, starting fresh: {e.Message}");
                try { File.Copy(file, file + ".bad", true); } catch { }
                State = new ExpansionState();
            }
            Migrate(State);
            ExpansionPlugin.Runner?.OnStateReplaced();
        }

        // schema 1 -> 2: the new collections start empty (Newtonsoft keeps the initialisers when the keys are absent)
        public static void Migrate(ExpansionState s)
        {
            if (s.Schema >= ExpansionState.CurrentSchema) return;
            ExpansionPlugin.Log.LogInfo($"[save] sidecar schema {s.Schema} -> {ExpansionState.CurrentSchema} ({s.Missions.Count} missions kept)");
            s.Stats = s.Stats ?? new System.Collections.Generic.Dictionary<string, int>();
            s.Gallery = s.Gallery ?? new System.Collections.Generic.List<GalleryEntry>();
            s.Trophies = s.Trophies ?? new System.Collections.Generic.List<string>();
            s.SafesOpened = s.SafesOpened ?? new System.Collections.Generic.List<string>();
            s.Schema = ExpansionState.CurrentSchema;
        }

        private static void SavePostfix(GameDataManager __instance, int slotIndex)
        {
            if (!__instance.IsSavingEnabled) return;
            Slot = slotIndex;
            var file = PathFor(slotIndex);
            try
            {
                State.FrameworkVersion = ExpansionPlugin.Version;
                var tmp = file + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(State, Formatting.Indented));
                if (File.Exists(file)) File.Replace(tmp, file, null); else File.Move(tmp, file);
                ExpansionPlugin.Log.LogInfo($"[save] slot {slotIndex}: expansion state written");
            }
            catch (Exception e) { ExpansionPlugin.Log.LogError($"[save] writing {file} failed: {e}"); }
        }

        private static void ResetPostfix()
        {
            State = new ExpansionState { Schema = ExpansionState.CurrentSchema };
            ExpansionPlugin.Log.LogInfo("[save] new game: fresh expansion state");
            ExpansionPlugin.Runner?.OnStateReplaced();
        }

        private static void DeletePostfix(int slotIndex)
        {
            try { File.Delete(PathFor(slotIndex)); } catch (Exception e) { ExpansionPlugin.Log.LogWarning($"[save] delete sidecar {slotIndex}: {e.Message}"); }
        }
    }
}
