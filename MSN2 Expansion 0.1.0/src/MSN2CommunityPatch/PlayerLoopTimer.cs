using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.LowLevel;

namespace MSN2CommunityPatch
{
    // Inserts a timestamp before every PlayerLoop subsystem so each frame's time can be attributed to engine phases
    // (rendering, animation, preloading, scripts, UniTask runners) in release players where profiler markers are absent.
    public static class PlayerLoopTimer
    {
        private struct Stamp { }
        private static string[] _names = new string[0];
        private static long[] _ts = new long[0];
        private static double[] _frameMs = new double[0];
        private static double[] _sumMs = new double[0];
        private static double[] _maxMs = new double[0];
        private static int _frames;
        public static bool Installed { get; private set; }

        public static void Install()
        {
            if (Installed) return;
            var root = PlayerLoop.GetCurrentPlayerLoop();
            var names = new List<string>();
            var phases = root.subSystemList.ToArray();
            var first = phases[0].subSystemList.ToList();
            first.Insert(0, new PlayerLoopSystem { type = typeof(Stamp), updateDelegate = Collect });
            phases[0].subSystemList = first.ToArray();
            for (int p = 0; p < phases.Length; p++)
            {
                var subs = new List<PlayerLoopSystem>();
                foreach (var s in phases[p].subSystemList ?? new PlayerLoopSystem[0])
                {
                    int idx = names.Count;
                    names.Add(phases[p].type.Name + "." + (s.type != null ? s.type.Name.Replace("UniTaskLoopRunner", "UniTask") : "?"));
                    subs.Add(new PlayerLoopSystem { type = typeof(Stamp), updateDelegate = () => _ts[idx] = Stopwatch.GetTimestamp() });
                    subs.Add(s);
                }
                int end = names.Count;
                names.Add(phases[p].type.Name + ".End");
                subs.Add(new PlayerLoopSystem { type = typeof(Stamp), updateDelegate = () => _ts[end] = Stopwatch.GetTimestamp() });
                phases[p].subSystemList = subs.ToArray();
            }
            _names = names.ToArray();
            _ts = new long[_names.Length];
            _frameMs = new double[_names.Length];
            _sumMs = new double[_names.Length];
            _maxMs = new double[_names.Length];
            root.subSystemList = phases;
            PlayerLoop.SetPlayerLoop(root);
            Installed = true;
            Plugin.Log.LogInfo($"[loop] timing {_names.Length} player-loop slots");
        }

        // Runs first in the player loop, so every stamp belongs to the previous, completed frame.
        private static void Collect()
        {
            if (!Installed) return;
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            for (int i = 0; i < _names.Length - 1; i++)
            {
                long a = _ts[i], b = _ts[i + 1];
                double ms = a > 0 && b > a ? (b - a) * msPerTick : 0;
                if (_names[i].EndsWith(".End")) ms = 0; // gap between phases is not a subsystem
                _frameMs[i] = ms;
                _sumMs[i] += ms;
                if (ms > _maxMs[i]) _maxMs[i] = ms;
            }
            _frames++;
            long t0 = _ts[1], t1 = _ts[_ts.Length - 1];
            double total = t1 > t0 ? (t1 - t0) * msPerTick : 0;
            if (Plugin.VerboseLog.Value && total > Plugin.HitchThresholdMs.Value && UnityEngine.Time.frameCount > 10)
                Plugin.Log.LogInfo($"[hitch] loop {total:F0} ms at t={UnityEngine.Time.realtimeSinceStartup:F1}s: {TopThisFrame(6)}");
        }

        public static string TopThisFrame(int n) =>
            string.Join(", ", Enumerable.Range(0, _names.Length).OrderByDescending(i => _frameMs[i]).Take(n).Select(i => $"{_names[i]}={_frameMs[i]:F1}"));

        public static void Reset() { Array.Clear(_sumMs, 0, _sumMs.Length); Array.Clear(_maxMs, 0, _maxMs.Length); _frames = 0; }

        public static object Report(int n) => new
        {
            frames = _frames,
            top = Enumerable.Range(0, _names.Length).Where(i => _sumMs[i] > 0).OrderByDescending(i => _sumMs[i]).Take(n)
                .Select(i => new { slot = _names[i], avgMs = Math.Round(_sumMs[i] / Math.Max(1, _frames), 3), maxMs = Math.Round(_maxMs[i], 2) }).ToArray()
        };
    }
}
