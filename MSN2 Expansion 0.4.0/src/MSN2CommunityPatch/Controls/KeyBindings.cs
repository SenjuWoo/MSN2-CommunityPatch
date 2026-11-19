using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Game.Input;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSN2CommunityPatch.Controls
{
    // Key remapping on top of the game's own Input System actions (GameInputs). Gameplay reads these actions everywhere, so a binding override
    // is all it takes; the vanilla game just never shipped a UI for it. Overrides persist in BepInEx/config/senjuwoo.msn2.bindings.json and are
    // re-applied to every GameInputs instance the game creates (menu and game scenes may each build their own).
    public static class KeyBindings
    {
        // One rebindable keyboard binding (a plain binding, or one part of a composite such as Move/up).
        public class Slot
        {
            public GameInputs Inputs;
            public InputAction Action;
            public int Index;
            public string Display => Action.GetBindingDisplayString(Index, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            public string Path => Action.bindings[Index].effectivePath;
            public bool IsDefault => string.IsNullOrEmpty(Action.bindings[Index].overridePath);
            public string Name => Action.actionMap.name + "/" + Action.name + (Action.bindings[Index].isPartOfComposite ? "." + Action.bindings[Index].name : "");
        }

        // Every binding that may swap keys with another one (the primary and alternate Move keys included).
        private static readonly (string action, string part, int occurrence)[] Catalogue =
        {
            ("Player/Move", "up", 0), ("Player/Move", "down", 0), ("Player/Move", "left", 0), ("Player/Move", "right", 0),
            ("Player/Move", "up", 1), ("Player/Move", "down", 1), ("Player/Move", "left", 1), ("Player/Move", "right", 1),
            ("Player/Jump", null, 0), ("Player/Sprint", null, 0), ("Player/Crouch", null, 0), ("Player/Interact", null, 0),
            ("Player/CameraOnOff", null, 0), ("Player/CameraBackFront", null, 0), ("Player/Tools", null, 0),
            ("Player/ShoutSkill", null, 0), ("Player/EchoSenseSkill", null, 0), ("Player/WaiterSkill", null, 0), ("Phone/ShowHide", null, 0),
        };

        // Bindings that must follow another one (the tool selector closes with the key that opened it).
        private static readonly (string action, string follows)[] Mirrors = { ("ToolSelector/Close", "Player/Tools") };

        private static readonly List<WeakReference<GameInputs>> Live = new List<WeakReference<GameInputs>>();
        private static string _json;

        public static event Action Changed;
        public static bool HasCustom => !string.IsNullOrEmpty(_json);
        public static string FilePath => System.IO.Path.Combine(Paths.ConfigPath, "senjuwoo.msn2.bindings.json");

        public static void Apply(Harmony h)
        {
            try { _json = File.Exists(FilePath) ? File.ReadAllText(FilePath) : null; }
            catch (Exception e) { Plugin.Log.LogWarning($"[controls] could not read {FilePath}: {e.Message}"); }
            var ctor = AccessTools.Constructor(typeof(GameInputs));
            if (ctor == null) { Plugin.Log.LogWarning("[controls] GameInputs constructor not found; key remapping skipped"); return; }
            h.Patch(ctor, postfix: new HarmonyMethod(typeof(KeyBindings), nameof(Created)));
            Plugin.Log.LogInfo($"[controls] key remapping active ({(HasCustom ? "saved bindings loaded" : "no saved bindings")})");
        }

        private static void Created(GameInputs __instance)
        {
            Live.RemoveAll(w => !w.TryGetTarget(out _));
            Live.Add(new WeakReference<GameInputs>(__instance));
            if (!HasCustom) return;
            try { __instance.asset.LoadBindingOverridesFromJson(_json); }
            catch (Exception e) { Plugin.Log.LogWarning($"[controls] saved bindings ignored: {e.Message}"); }
        }

        private static IEnumerable<GameInputs> AllLive() => Live.Select(w => w.TryGetTarget(out var t) ? t : null).Where(t => t != null);

        private static InputController _controller;
        private static GameInputs _menuInputs;

        // The instance the running scene actually reads from (InputController owns it). The main menu has none, so a detached one stands in:
        // edits are saved to the file and every GameInputs the game creates later picks them up.
        public static GameInputs Primary()
        {
            if (!_controller) _controller = UnityEngine.Object.FindFirstObjectByType<InputController>();
            var inputs = _controller ? _controller.Inputs : null;
            return inputs ?? AllLive().LastOrDefault() ?? (_menuInputs = new GameInputs());
        }

        // "Player/Interact" -> its Nth keyboard binding; with part: the Nth keyboard composite part of that name ("Player/Move", "up").
        public static Slot Find(string actionPath, string part = null, int occurrence = 0)
        {
            var inputs = Primary();
            var a = inputs?.asset.FindAction(actionPath);
            if (a == null) return null;
            int seen = 0;
            for (int i = 0; i < a.bindings.Count; i++)
            {
                var b = a.bindings[i];
                if (b.isComposite || !b.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) continue;
                if (part == null ? b.isPartOfComposite : !(b.isPartOfComposite && string.Equals(b.name, part, StringComparison.OrdinalIgnoreCase))) continue;
                if (seen++ == occurrence) return new Slot { Inputs = inputs, Action = a, Index = i };
            }
            return null;
        }

        // Interactive rebind of one keyboard binding. Esc cancels; mouse left/right/pointer stay reserved for the UI.
        public static void Rebind(Slot slot, Action onDone, Action onCancel)
        {
            bool wasEnabled = slot.Action.enabled;
            if (wasEnabled) slot.Action.Disable(); // PerformInteractiveRebinding needs a disabled action
            string old = slot.Path;
            var op = slot.Action.PerformInteractiveRebinding(slot.Index)
                .WithControlsHavingToMatchPath("<Keyboard>/*")
                .WithControlsHavingToMatchPath("<Mouse>/middleButton")
                .WithControlsHavingToMatchPath("<Mouse>/forwardButton")
                .WithControlsHavingToMatchPath("<Mouse>/backButton")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f);
            op.OnComplete(o =>
            {
                string now = slot.Path;
                o.Dispose();
                if (wasEnabled) slot.Action.Enable();
                ResolveConflicts(slot, old, now);
                Commit(slot.Inputs);
                onDone?.Invoke();
            });
            op.OnCancel(o => { o.Dispose(); if (wasEnabled) slot.Action.Enable(); onCancel?.Invoke(); });
            op.Start();
        }

        // Another binding already on that key swaps to the key this one gave up, so no two actions end up on one key.
        private static void ResolveConflicts(Slot slot, string oldPath, string newPath)
        {
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var (action, part, occurrence) in Catalogue)
            {
                var other = Find(action, part, occurrence);
                if (other == null || (other.Action == slot.Action && other.Index == slot.Index)) continue;
                if (!string.Equals(other.Path, newPath, StringComparison.OrdinalIgnoreCase)) continue;
                other.Action.ApplyBindingOverride(other.Index, oldPath);
                Plugin.Log.LogInfo($"[controls] {newPath} was on {other.Name}, which now uses {oldPath}");
            }
        }

        public static void Reset(Slot slot)
        {
            slot.Action.RemoveBindingOverride(slot.Index);
            Commit(slot.Inputs);
        }

        public static void ResetAll()
        {
            var inputs = Primary();
            inputs?.asset.RemoveAllBindingOverrides();
            Commit(inputs);
        }

        // Mirrors follow their source, the result is saved and copied to every other live GameInputs.
        private static void Commit(GameInputs source)
        {
            if (source == null) return;
            foreach (var (action, follows) in Mirrors)
            {
                var src = source.asset.FindAction(follows);
                var dst = source.asset.FindAction(action);
                if (src == null || dst == null) continue;
                int si = FirstKeyboard(src), di = FirstKeyboard(dst);
                if (si < 0 || di < 0) continue;
                if (string.IsNullOrEmpty(src.bindings[si].overridePath)) dst.RemoveBindingOverride(di);
                else dst.ApplyBindingOverride(di, src.bindings[si].overridePath);
            }
            var json = source.asset.SaveBindingOverridesAsJson();
            bool empty = json.IndexOf("\"path\"", StringComparison.Ordinal) < 0;
            _json = empty ? null : json;
            try
            {
                if (empty) { if (File.Exists(FilePath)) File.Delete(FilePath); }
                else File.WriteAllText(FilePath, json);
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[controls] could not save {FilePath}: {e.Message}"); }
            foreach (var other in AllLive().Where(g => g != source))
            {
                try { if (empty) other.asset.RemoveAllBindingOverrides(); else other.asset.LoadBindingOverridesFromJson(json); }
                catch (Exception e) { Plugin.Log.LogWarning($"[controls] could not sync bindings: {e.Message}"); }
            }
            if (Plugin.VerboseLog.Value) Plugin.Log.LogInfo($"[controls] bindings saved ({(empty ? "defaults" : "custom")}) -> {FilePath}");
            Changed?.Invoke();
        }

        private static int FirstKeyboard(InputAction a)
        {
            for (int i = 0; i < a.bindings.Count; i++)
                if (a.bindings[i].path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
