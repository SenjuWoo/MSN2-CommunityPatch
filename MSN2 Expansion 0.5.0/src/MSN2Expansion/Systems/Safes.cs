using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Entities;
using Game.Interactables;
using Game.Noise;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSN2Expansion.Systems
{
    // Safe cracking (MSN1 SafeController/SafeView: a 4-digit keypad). MSN2's two "Safe" OpenCloseEntities in Emma's house
    // open a keypad instead of the lockpicking lock. Each safe has its own code per save; after every try the keypad
    // tells how many digits are right and in place / right but misplaced (so it can be worked out), and a wrong code
    // beeps loud enough to be heard (noise like a lockpick break-in). The first opening pays cash and an item.
    public static class Safes
    {
        public static OpenCloseEntity Current;
        public static bool KeypadOpen => Current;
        private static string _typed = "";
        private static readonly List<string> Tries = new List<string>();
        private static bool _passThrough;
        private static readonly string[] Items = { "SleepingPill", "Aphrodisiac", "BucketTrap", "FlashTrap" };

        public static void Patch(Harmony h)
        {
            var m = AccessTools.Method(typeof(OpenCloseEntity), nameof(OpenCloseEntity.InteractAsync)).MakeGenericMethod(typeof(PlayerEntity.PlayerData));
            h.Patch(m, prefix: new HarmonyMethod(typeof(Safes), nameof(InteractPrefix)));
        }

        public static bool IsSafe(OpenCloseEntity e) => e && e.name == "Safe";
        public static string SafeKey(OpenCloseEntity e) => e.EntityID?.ToString() ?? e.transform.position.ToString();

        // deterministic per save slot and safe: same code every time you come back
        public static string CodeFor(OpenCloseEntity e)
        {
            unchecked
            {
                int h = 17 + Sidecar.Slot * 7919;
                foreach (var c in SafeKey(e)) h = h * 31 + c;
                var r = new System.Random(h);
                return string.Concat(Enumerable.Range(0, 4).Select(_ => (char)('0' + r.Next(10))));
            }
        }

        private static bool InteractPrefix(OpenCloseEntity __instance, object actor, InteractArgs args, ref UniTask __result)
        {
            try
            {
                if (_passThrough || SystemsConfig.Safes == null || !SystemsConfig.Safes.Value || !IsSafe(__instance)) return true;
                if (!(actor is PlayerEntity) || args.InteractActionType != InteractActionType.PlayerUse) return true;
                if (__instance.Data.IsOpened || Sidecar.State.SafesOpened.Contains(SafeKey(__instance))) return true;
                Open(__instance);
                __result = UniTask.CompletedTask;
                return false;
            }
            catch (Exception e) { ExpansionPlugin.Log.LogError($"[safe] {e}"); return true; }
        }

        public static void Open(OpenCloseEntity safe)
        {
            Current = safe;
            _typed = "";
            Tries.Clear();
            ExpansionPlugin.Log.LogInfo($"[safe] keypad opened at {SafeKey(safe)}");
            Redraw(null);
        }

        public static void Close() { Current = null; ExpansionHud.Instance?.HideSystem(); }

        public static void Tick()
        {
            if (!Current) return;
            var hud = ExpansionHud.Instance;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerEntity>();
            if (!hud || hud.InputBlocked || !player || Vector3.Distance(player.transform.position, Current.transform.position) > 2.5f || Keyboard.current == null) { Close(); return; }
            var kb = Keyboard.current;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            for (int d = 0; d <= 9; d++)
            {
                if ((kb[Key.Digit0 + d].wasPressedThisFrame || kb[Key.Numpad0 + d].wasPressedThisFrame) && _typed.Length < 4) { _typed += (char)('0' + d); Redraw(null); }
            }
            if (kb.backspaceKey.wasPressedThisFrame && _typed.Length > 0) { _typed = _typed.Substring(0, _typed.Length - 1); Redraw(null); }
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && _typed.Length == 4) Try(_typed);
        }

        // returns (right place, right digit wrong place)
        public static (int exact, int near) Score(string code, string guess)
        {
            int exact = 0;
            var cc = new int[10]; var gc = new int[10];
            for (int i = 0; i < 4; i++)
            {
                if (code[i] == guess[i]) exact++;
                else { cc[code[i] - '0']++; gc[guess[i] - '0']++; }
            }
            int near = 0;
            for (int d = 0; d < 10; d++) near += Math.Min(cc[d], gc[d]);
            return (exact, near);
        }

        public static string Try(string guess)
        {
            if (!Current) return "no keypad";
            var code = CodeFor(Current);
            var (exact, near) = Score(code, guess);
            Tries.Insert(0, $"{guess}   {exact} in place, {near} misplaced");
            _typed = "";
            if (exact == 4) { Crack(Current); return "open"; }
            var nc = UnityEngine.Object.FindFirstObjectByType<NoiseController>();
            if (nc) nc.SendNoise(ActorID.Player, Current.transform.position, Mathf.Max(10f, Current.BreakInNoiseVolume));
            SystemsRuntime.Inc("safeWrongCodes");
            Redraw("<color=#ff6060>Wrong code. The keypad beeps.</color>");
            return $"{exact}/{near}";
        }

        private static void Crack(OpenCloseEntity safe)
        {
            var key = SafeKey(safe);
            Sidecar.State.SafesOpened.Add(key);
            safe.Data.IsLocked = false;
            var r = new System.Random(key.GetHashCode() ^ Sidecar.Slot);
            int money = 150 + r.Next(6) * 50;
            var item = Items[r.Next(Items.Length)];
            MissionRunner.GrantRewards(money, 0, new Dictionary<string, int> { [item] = 1 });
            MissionRunner.Notify("Safe cracked", $"+${money} and a {item} inside.");
            ExpansionPlugin.Log.LogInfo($"[safe] {key} cracked: +{money} money, {item}");
            Close();
            // open the door through the game's own interaction (animation, sound, saved state)
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerEntity>();
            if (player)
            {
                _passThrough = true;
                try { safe.InteractAsync(player, new InteractArgs { InteractActionType = InteractActionType.PlayerUse }).Forget(); }
                finally { _passThrough = false; }
            }
        }

        private static void Redraw(string status)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Enter the 4-digit code.\n");
            sb.AppendLine($"<size=48>{_typed.PadRight(4, '_')}</size>\n");
            if (status != null) sb.AppendLine(status + "\n");
            foreach (var t in Tries.Take(8)) sb.AppendLine(t);
            ExpansionHud.Instance?.ShowSystem("SAFE", sb.ToString(), "[0-9] type    [Backspace] delete    [Enter] try    [Esc] leave");
        }
    }
}
