using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MSN2CommunityPatch
{
    // Read-only snapshot of render/CPU cost drivers in the loaded scenes.
    public static class SceneStats
    {
        private static T[] All<T>() where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        public static Dictionary<string, object> Collect()
        {
            var r = new Dictionary<string, object>();
            r["scenes"] = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name).ToArray();
            r["screen"] = $"{Screen.width}x{Screen.height} {Screen.fullScreenMode} {Screen.currentResolution.refreshRateRatio.value:F0}Hz";
            r["targetFrameRate"] = Application.targetFrameRate;
            r["vSyncCount"] = QualitySettings.vSyncCount;
            r["quality"] = $"{QualitySettings.GetQualityLevel()}:{QualitySettings.names[QualitySettings.GetQualityLevel()]}";
            r["qualityNames"] = QualitySettings.names;
            r["lodBias"] = QualitySettings.lodBias;
            r["gfx"] = $"{SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceVersion}";

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                r["urp"] = new Dictionary<string, object>
                {
                    ["asset"] = urp.name,
                    ["renderScale"] = urp.renderScale,
                    ["upscaling"] = urp.upscalingFilter.ToString(),
                    ["msaa"] = urp.msaaSampleCount,
                    ["hdr"] = urp.supportsHDR,
                    ["shadowDistance"] = urp.shadowDistance,
                    ["cascades"] = urp.shadowCascadeCount,
                    ["mainShadowRes"] = urp.mainLightShadowmapResolution,
                    ["addLightShadows"] = urp.supportsAdditionalLightShadows,
                    ["addShadowRes"] = urp.additionalLightsShadowmapResolution,
                    ["addLightsMode"] = urp.additionalLightsRenderingMode.ToString(),
                    ["maxAddLights"] = urp.maxAdditionalLightsCount,
                    ["softShadows"] = urp.supportsSoftShadows,
                    ["srpBatcher"] = urp.useSRPBatcher,
                    ["depthTex"] = urp.supportsCameraDepthTexture,
                    ["opaqueTex"] = urp.supportsCameraOpaqueTexture,
                    ["gpuResidentDrawer"] = urp.gpuResidentDrawerMode.ToString(),
                };
            }

            var lights = All<Light>().Where(l => l.isActiveAndEnabled).ToArray();
            r["lights"] = new Dictionary<string, object>
            {
                ["active"] = lights.Length,
                ["realtime"] = lights.Count(l => l.bakingOutput.lightmapBakeType != LightmapBakeType.Baked),
                ["shadowed"] = lights.Count(l => l.shadows != LightShadows.None),
                ["shadowedPoint"] = lights.Count(l => l.shadows != LightShadows.None && l.type == LightType.Point),
                ["shadowedSpot"] = lights.Count(l => l.shadows != LightShadows.None && l.type == LightType.Spot),
                ["byType"] = lights.GroupBy(l => l.type.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            };
            var cams = All<Camera>().Where(c => c.isActiveAndEnabled).ToArray();
            r["cameras"] = cams.Select(c => $"{Path(c.transform)} depth={c.depth} rt={(c.targetTexture ? c.targetTexture.width + "x" + c.targetTexture.height : "screen")}").ToArray();
            var probes = All<ReflectionProbe>().Where(p => p.isActiveAndEnabled).ToArray();
            r["reflectionProbes"] = new Dictionary<string, object>
            {
                ["active"] = probes.Length,
                ["realtime"] = probes.Count(p => p.mode == ReflectionProbeMode.Realtime),
                ["everyFrame"] = probes.Count(p => p.mode == ReflectionProbeMode.Realtime && p.refreshMode == ReflectionProbeRefreshMode.EveryFrame),
            };
            var smr = All<SkinnedMeshRenderer>().Where(s => s.enabled).ToArray();
            r["skinnedMeshes"] = new Dictionary<string, object>
            {
                ["active"] = smr.Length,
                ["updateWhenOffscreen"] = smr.Count(s => s.updateWhenOffscreen),
                ["withBlendShapes"] = smr.Count(s => s.sharedMesh && s.sharedMesh.blendShapeCount > 0),
            };
            var anims = All<Animator>().Where(a => a.isActiveAndEnabled).ToArray();
            r["animators"] = new Dictionary<string, object>
            {
                ["active"] = anims.Length,
                ["byCulling"] = anims.GroupBy(a => a.cullingMode.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            };
            r["renderers"] = All<Renderer>().Count(x => x.enabled);
            r["shadowCasters"] = All<Renderer>().Count(x => x.enabled && x.shadowCastingMode != ShadowCastingMode.Off);
            r["cloth"] = All<Cloth>().Count(c => c.enabled);
            r["particleSystems"] = All<ParticleSystem>().Count(p => p.isPlaying);
            r["audioPlaying"] = All<AudioSource>().Count(a => a.isPlaying);
            r["navAgents"] = All<NavMeshAgent>().Count(a => a.isActiveAndEnabled);
            r["rigidbodies"] = All<Rigidbody>().Count(b => !b.isKinematic && !b.IsSleeping());
            r["canvases"] = All<Canvas>().Count(c => c.isActiveAndEnabled);
            r["behaviours"] = All<MonoBehaviour>().Where(b => b.isActiveAndEnabled)
                .GroupBy(b => b.GetType().FullName).OrderByDescending(g => g.Count()).Take(40)
                .ToDictionary(g => g.Key, g => g.Count());
            r["gameObjects"] = All<Transform>().Length;
            r["monoHeapMB"] = GC.GetTotalMemory(false) / 1048576;
            return r;
        }

        public static string Path(Transform t)
        {
            var s = t.name;
            while ((t = t.parent) != null) s = t.name + "/" + s;
            return s;
        }
    }
}
