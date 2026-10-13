using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MSN2CommunityPatch.Graphics
{
    // Vanilla hardcodes Application.targetFrameRate = 60 and only the High preset enables VSync, so Medium/Low are capped
    // at 60 FPS (measured: High 156 fps, Medium/Low 60 fps on the same spot) - the "Low runs worse than High" reports.
    // Also exposes the URP costs the presets hide (MSAA 8x, 4096 additional-shadow atlas) and fixes Low allowing more lights than Medium.
    public class GraphicsTuner : MonoBehaviour
    {
        public enum Toggle { Preset, On, Off }

        private class Snapshot { public int Msaa, MaxLights, AddRes; public float Scale, Shadow; public bool AddShadows, Soft; public UpscalingFilterSelection Filter; }

        private static ConfigEntry<bool> _vsync;
        private static ConfigEntry<int> _fpsLimit, _msaa, _addShadowRes;
        private static ConfigEntry<float> _renderScale, _shadowDistance;
        private static ConfigEntry<UpscalingFilterSelection> _upscaler;
        private static ConfigEntry<Toggle> _addShadows, _soft;
        private static ConfigEntry<bool> _fixLow;

        private readonly Dictionary<UniversalRenderPipelineAsset, Snapshot> _orig = new Dictionary<UniversalRenderPipelineAsset, Snapshot>();
        private int _lastLevel = -1;
        private bool _dirty = true;

        public static void Bind(ConfigFile c)
        {
            _vsync = c.Bind("Graphics", "VSync", true, "Sync to monitor refresh. Vanilla only enabled this on High.");
            _fpsLimit = c.Bind("Graphics", "FrameRateLimit", 0, new ConfigDescription("FPS cap when VSync is off. 0 = unlimited. Vanilla forced 60.", new AcceptableValueRange<int>(0, 1000)));
            _msaa = c.Bind("Graphics", "MSAA", -1, new ConfigDescription("-1 = preset (High 8x, Medium 4x, Low 2x), 1 = off, 2/4/8 samples.", new AcceptableValueList<int>(-1, 1, 2, 4, 8)));
            _renderScale = c.Bind("Graphics", "RenderScale", 1f, new ConfigDescription("3D resolution scale. <1 renders fewer pixels and upscales.", new AcceptableValueRange<float>(0.5f, 2f)));
            _upscaler = c.Bind("Graphics", "Upscaler", UpscalingFilterSelection.Auto, "Filter used when RenderScale < 1 (FSR = AMD FidelityFX Super Resolution 1).");
            _shadowDistance = c.Bind("Graphics", "ShadowDistance", -1f, new ConfigDescription("-1 = preset (High 75, Medium 40, Low 20) metres.", new AcceptableValueRange<float>(-1f, 200f)));
            _addShadows = c.Bind("Graphics", "AdditionalLightShadows", Toggle.Preset, "Shadows from lamps/point/spot lights.");
            _addShadowRes = c.Bind("Graphics", "AdditionalShadowAtlas", -1, new ConfigDescription("-1 = preset (High 4096). Lamp shadow atlas size.", new AcceptableValueList<int>(-1, 512, 1024, 2048, 4096)));
            _soft = c.Bind("Graphics", "SoftShadows", Toggle.Preset, "Filtered shadow edges.");
            _fixLow = c.Bind("Graphics", "FixLowPreset", true, "Low preset: cap per-object lamps at 3 (vanilla Low allowed 6, more than Medium's 3) and disable lamp shadows.");
        }

        private void Awake()
        {
            _msaa.ConfigFile.SettingChanged += (s, a) => _dirty = true;
        }

        private void Update()
        {
            // the game resets these on boot and on every quality change; cheap property checks each frame
            int vs = _vsync.Value ? 1 : 0;
            if (QualitySettings.vSyncCount != vs) QualitySettings.vSyncCount = vs;
            int fps = _vsync.Value || _fpsLimit.Value <= 0 ? -1 : _fpsLimit.Value;
            if (Application.targetFrameRate != fps) Application.targetFrameRate = fps;

            int level = QualitySettings.GetQualityLevel();
            if (level != _lastLevel) { _lastLevel = level; _dirty = true; }
            if (_dirty) { _dirty = false; ApplyAll(); }
        }

        private static readonly System.Action<UniversalRenderPipelineAsset, bool> SetAddShadows = (a, v) => AccessTools.PropertySetter(typeof(UniversalRenderPipelineAsset), "supportsAdditionalLightShadows").Invoke(a, new object[] { v });
        private static readonly System.Action<UniversalRenderPipelineAsset, bool> SetSoft = (a, v) => AccessTools.PropertySetter(typeof(UniversalRenderPipelineAsset), "supportsSoftShadows").Invoke(a, new object[] { v });

        private void ApplyAll()
        {
            for (int i = 0; i < QualitySettings.count; i++)
            {
                if (!(QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset a)) continue;
                if (!_orig.TryGetValue(a, out var o))
                    _orig[a] = o = new Snapshot
                    {
                        Msaa = a.msaaSampleCount, MaxLights = a.maxAdditionalLightsCount, AddRes = a.additionalLightsShadowmapResolution, Scale = a.renderScale,
                        Shadow = a.shadowDistance, AddShadows = a.supportsAdditionalLightShadows, Soft = a.supportsSoftShadows, Filter = a.upscalingFilter
                    };
                bool low = _fixLow.Value && a.name.EndsWith("Low");
                a.msaaSampleCount = _msaa.Value > 0 ? _msaa.Value : o.Msaa;
                a.renderScale = _renderScale.Value;
                a.upscalingFilter = _upscaler.Value == UpscalingFilterSelection.Auto ? o.Filter : _upscaler.Value;
                a.shadowDistance = _shadowDistance.Value >= 0 ? _shadowDistance.Value : o.Shadow;
                a.maxAdditionalLightsCount = low ? Mathf.Min(3, o.MaxLights) : o.MaxLights;
                a.additionalLightsShadowmapResolution = _addShadowRes.Value > 0 ? _addShadowRes.Value : o.AddRes;
                SetAddShadows(a, _addShadows.Value == Toggle.Preset ? o.AddShadows && !low : _addShadows.Value == Toggle.On);
                SetSoft(a, _soft.Value == Toggle.Preset ? o.Soft : _soft.Value == Toggle.On);
            }
            var cur = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (Plugin.VerboseLog.Value && cur)
                Plugin.Log.LogInfo($"[gfx] {cur.name}: vsync={QualitySettings.vSyncCount} fpsCap={Application.targetFrameRate} msaa={cur.msaaSampleCount} scale={cur.renderScale} {cur.upscalingFilter} shadowDist={cur.shadowDistance} addShadows={cur.supportsAdditionalLightShadows}@{cur.additionalLightsShadowmapResolution} soft={cur.supportsSoftShadows} maxLights={cur.maxAdditionalLightsCount}");
        }
    }
}
