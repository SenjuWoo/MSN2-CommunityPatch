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
            ExpansionPlugin.Runner?.OnStateReplaced();
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
            State = new ExpansionState();
            ExpansionPlugin.Log.LogInfo("[save] new game: fresh expansion state");
            ExpansionPlugin.Runner?.OnStateReplaced();
        }

        private static void DeletePostfix(int slotIndex)
        {
            try { File.Delete(PathFor(slotIndex)); } catch (Exception e) { ExpansionPlugin.Log.LogWarning($"[save] delete sidecar {slotIndex}: {e.Message}"); }
        }
    }
}
