using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace MSN2CommunityPatch
{
    // Temporary Harmony timing of every MonoBehaviour Update/LateUpdate/FixedUpdate in game assemblies.
    public static class ScriptProfiler
    {
        private class Stat { public long Ticks; public int Calls; public long Bytes; }
        public struct Sample { public long T; public long M; }
        private static bool _alloc;

        private static readonly string[] Assemblies =
        {
            "Assembly-CSharp", "Assembly-CSharp-firstpass", "NodeCanvas", "ParadoxNotion", "Unity.Cinemachine",
            "EPO", "AllIn1SpriteShaderAssembly", "UnityUIExtensions", "R3.Unity", "Unity.Animation.Rigging", "DOTween"
        };
        private static readonly string[] Methods = { "Update", "LateUpdate", "FixedUpdate", "OnGUI", "OnWillRenderObject", "OnRenderImage" };

        private static readonly Dictionary<RuntimeMethodHandle, Stat> Stats = new Dictionary<RuntimeMethodHandle, Stat>();
        private static readonly Dictionary<RuntimeMethodHandle, MethodBase> Names = new Dictionary<RuntimeMethodHandle, MethodBase>();
        private static readonly List<MethodBase> Patched = new List<MethodBase>();
        private static Harmony _h;
        private static int _startFrame;
        public static bool Running => _h != null;

        public static int Start(bool alloc = false)
        {
            if (Running) return Patched.Count;
            _h = new Harmony(Plugin.Guid + ".profiler");
            _alloc = alloc;
            Stats.Clear(); Names.Clear(); Patched.Clear();
            var pre = new HarmonyMethod(typeof(ScriptProfiler), nameof(Prefix)) { priority = Priority.First };
            var post = new HarmonyMethod(typeof(ScriptProfiler), nameof(Postfix)) { priority = Priority.Last };
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Where(a => Assemblies.Contains(a.GetName().Name)))
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                {
                    if (t.ContainsGenericParameters || !typeof(Behaviour).IsAssignableFrom(t)) continue;
                    foreach (var name in Methods)
                    {
                        var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                        if (m == null || m.IsAbstract || m.GetMethodBody() == null) continue;
                        try { _h.Patch(m, pre, post); Patched.Add(m); Stats[m.MethodHandle] = new Stat(); Names[m.MethodHandle] = m; }
                        catch (Exception e) { Plugin.Log.LogWarning($"[profiler] skip {t.FullName}.{name}: {e.Message}"); }
                    }
                }
            }
            _startFrame = Time.frameCount;
            Plugin.Log.LogInfo($"[profiler] instrumented {Patched.Count} methods");
            return Patched.Count;
        }

        private static void Prefix(out Sample __state) { __state.M = _alloc ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : 0; __state.T = Stopwatch.GetTimestamp(); }

        private static void Postfix(Sample __state, MethodBase __originalMethod)
        {
            if (Stats.TryGetValue(__originalMethod.MethodHandle, out var s)) { s.Ticks += Stopwatch.GetTimestamp() - __state.T; s.Calls++; if (_alloc) s.Bytes += Math.Max(0, UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - __state.M); }
        }

        public static List<Dictionary<string, object>> Stop(int top, bool byAlloc = false)
        {
            if (!Running) return new List<Dictionary<string, object>>();
            int frames = Math.Max(1, Time.frameCount - _startFrame);
            _h.UnpatchSelf();
            _h = null;
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            var rows = Stats.Where(kv => kv.Value.Calls > 0)
                .OrderByDescending(kv => byAlloc ? kv.Value.Bytes : kv.Value.Ticks)
                .Take(top)
                .Select(kv => new Dictionary<string, object>
                {
                    ["method"] = Names[kv.Key].DeclaringType.FullName + "." + Names[kv.Key].Name,
                    ["totalMs"] = Math.Round(kv.Value.Ticks * msPerTick, 2),
                    ["msPerFrame"] = Math.Round(kv.Value.Ticks * msPerTick / frames, 4),
                    ["callsPerFrame"] = Math.Round((double)kv.Value.Calls / frames, 1),
                    ["usPerCall"] = Math.Round(kv.Value.Ticks * msPerTick * 1000 / kv.Value.Calls, 2),
                    ["allocBytesPerFrame"] = kv.Value.Bytes / frames
                }).ToList();
            rows.Insert(0, new Dictionary<string, object>
            {
                ["frames"] = frames,
                ["instrumented"] = Patched.Count,
                ["sumMsPerFrame"] = Math.Round(Stats.Values.Sum(s => s.Ticks) * msPerTick / frames, 3)
            });
            return rows;
        }
    }
}
