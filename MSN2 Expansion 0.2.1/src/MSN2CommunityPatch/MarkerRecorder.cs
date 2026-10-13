using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace MSN2CommunityPatch
{
    // Engine-side timings (ProfilerRecorder + FrameTimingManager). Markers unavailable in release players report valid=false.
    public static class MarkerRecorder
    {
        private static readonly (ProfilerCategory cat, string name)[] Markers =
        {
            (ProfilerCategory.Internal, "Main Thread"), (ProfilerCategory.Internal, "Render Thread"),
            (ProfilerCategory.Internal, "PlayerLoop"), (ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate"),
            (ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate"), (ProfilerCategory.Physics, "FixedUpdate.PhysicsFixedUpdate"),
            (ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationBegin"), (ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd"),
            (ProfilerCategory.Render, "PostLateUpdate.FinishFrameRendering"), (ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread"),
            (ProfilerCategory.Memory, "GC.Collect"), (ProfilerCategory.Memory, "GC Allocated In Frame"), (ProfilerCategory.Memory, "GC Allocation In Frame Count"),
            (ProfilerCategory.Render, "Batches Count"), (ProfilerCategory.Render, "SetPass Calls Count"), (ProfilerCategory.Render, "Draw Calls Count"),
            (ProfilerCategory.Render, "Shadow Casters Count"), (ProfilerCategory.Render, "Triangles Count"), (ProfilerCategory.Render, "Visible Skinned Meshes Count"),
        };

        public static IEnumerator Record(float seconds, System.Action<object> done)
        {
            var recs = Markers.Select(m => (m.name, rec: ProfilerRecorder.StartNew(m.cat, m.name, 1))).ToList();
            var sums = new double[recs.Count];
            var maxs = new double[recs.Count];
            double cpuMain = 0, cpuRender = 0, gpu = 0; int timed = 0, frames = 0;
            var timings = new FrameTiming[1];
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (Time.realtimeSinceStartup < end)
            {
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                { cpuMain += timings[0].cpuMainThreadFrameTime; cpuRender += timings[0].cpuRenderThreadFrameTime; gpu += timings[0].gpuFrameTime; timed++; }
                for (int i = 0; i < recs.Count; i++)
                    if (recs[i].rec.Valid) { double v = recs[i].rec.LastValue; sums[i] += v; if (v > maxs[i]) maxs[i] = v; }
                frames++;
                yield return null;
            }
            var result = new Dictionary<string, object> { ["frames"] = frames, ["frameTimingSamples"] = timed };
            if (timed > 0) { result["ftm_cpuMainMs"] = cpuMain / timed; result["ftm_cpuRenderMs"] = cpuRender / timed; result["ftm_gpuMs"] = gpu / timed; }
            for (int i = 0; i < recs.Count; i++)
            {
                var r = recs[i].rec;
                bool time = r.Valid && r.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
                result[recs[i].name] = !r.Valid ? (object)"invalid" : new
                {
                    avg = time ? sums[i] / frames / 1e6 : sums[i] / frames,
                    max = time ? maxs[i] / 1e6 : maxs[i],
                    unit = time ? "ms" : r.UnitType.ToString()
                };
                r.Dispose();
            }
            done(result);
        }
    }
}
