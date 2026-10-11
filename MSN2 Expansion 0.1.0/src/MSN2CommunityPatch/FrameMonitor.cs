using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSN2CommunityPatch
{
    // Always-on frame timing; capture windows feed the agent `fps` command.
    public class FrameMonitor : MonoBehaviour
    {
        public static FrameMonitor Instance;
        private readonly List<float> _capture = new List<float>();
        private bool _capturing;
        private int _gcAtStart;

        private void Awake() => Instance = this;

        private void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (_capturing) _capture.Add(ms);
            if (!PlayerLoopTimer.Installed && Plugin.LoopTiming.Value && Time.frameCount > 2) PlayerLoopTimer.Install();
            if (!PlayerLoopTimer.Installed && Plugin.VerboseLog.Value && ms > Plugin.HitchThresholdMs.Value && Time.frameCount > 10)
                Plugin.Log.LogInfo($"[hitch] {ms:F1} ms frame {Time.frameCount} scene={SceneManager.GetActiveScene().name} gc0={GC.CollectionCount(0)}");
        }

        public void BeginCapture()
        {
            _capture.Clear();
            _gcAtStart = GC.CollectionCount(0);
            _capturing = true;
        }

        public Dictionary<string, object> EndCapture()
        {
            _capturing = false;
            var r = new Dictionary<string, object>();
            if (_capture.Count == 0) return r;
            var sorted = _capture.OrderBy(x => x).ToList();
            float total = _capture.Sum();
            float Pct(double p) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
            r["frames"] = _capture.Count;
            r["seconds"] = total / 1000f;
            r["avgFps"] = _capture.Count / (total / 1000f);
            r["avgMs"] = total / _capture.Count;
            r["p50Ms"] = Pct(0.50);
            r["p99Ms"] = Pct(0.99);
            r["p999Ms"] = Pct(0.999);
            r["maxMs"] = sorted[sorted.Count - 1];
            r["low1PctFps"] = 1000f / sorted.Skip((int)(sorted.Count * 0.99)).DefaultIfEmpty(sorted.Last()).Average();
            r["hitchesOver50ms"] = _capture.Count(x => x > 50f);
            r["gc0Collections"] = GC.CollectionCount(0) - _gcAtStart;
            r["targetFrameRate"] = Application.targetFrameRate;
            r["vSyncCount"] = QualitySettings.vSyncCount;
            r["scene"] = SceneManager.GetActiveScene().name;
            return r;
        }
    }
}
