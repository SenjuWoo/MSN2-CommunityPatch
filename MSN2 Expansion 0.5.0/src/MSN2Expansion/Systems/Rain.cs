using System;
using System.IO;
using System.Linq;
using Game.Entities;
using Game.Noise;
using HarmonyLib;
using UnityEngine;

namespace MSN2Expansion.Systems
{
    // Rain (MSN1 WeatherController + RainMaker): some days get a rainy spell. Rain falls around the camera while the
    // player is outdoors, the rain loop (MSN1's own recording, data/rain_medium.wav) plays louder outside than inside,
    // and while it rains the player's noise carries less (x0.6, applied to NoiseController.SendNoise like a perk).
    public static class Rain
    {
        public const float NoiseFactor = 0.6f;
        public static bool Raining;
        public static bool Forced;   // agent: sys rain on|off
        public static int Day = -1, StartHour, EndHour;
        private static GameHooks _hooks;
        private static GameObject _go;
        private static ParticleSystem _ps;
        private static AudioSource _audio;
        private static AudioClip _clip;
        private static Material _mat;
        private static bool _indoors;
        public static bool Indoors => _indoors;
        private static float _nextIndoorCheck;

        public static void Patch(Harmony h) =>
            h.Patch(AccessTools.Method(typeof(NoiseController), nameof(NoiseController.SendNoise)), prefix: new HarmonyMethod(typeof(Rain), nameof(NoisePrefix)) { priority = Priority.Normal });

        private static void NoisePrefix(ActorID sender, ref float volume)
        {
            if (sender != ActorID.Player) return;
            if (Raining && SystemsConfig.Rain.Value) volume *= NoiseFactor;
            volume *= Difficulty.Noise;
        }

        public static void Attach(GameHooks hooks) { _hooks = hooks; Day = -1; }

        public static void Detach()
        {
            _hooks = null;
            Raining = false;
            if (_go) UnityEngine.Object.Destroy(_go);
            SetFog(false);
        }

        // deterministic per save slot and day
        public static void PlanDay(int day)
        {
            Day = day;
            var r = new System.Random(day * 7349 + Sidecar.Slot * 131 + 3);
            bool wet = r.NextDouble() < SystemsConfig.RainChance.Value;
            StartHour = wet ? r.Next(0, 22) : -1;
            EndHour = wet ? Math.Min(24, StartHour + 3 + r.Next(6)) : -1;
            ExpansionPlugin.Log.LogInfo($"[rain] day {day}: {(wet ? $"rain {StartHour:00}-{EndHour:00}" : "dry")}");
        }

        public static void Tick()
        {
            if (_hooks == null || !SystemsConfig.Rain.Value) { if (Raining) SetRain(false); return; }
            int day = _hooks.Time.Days + 1, hour = _hooks.Time.Hours;
            if (day != Day) PlanDay(day);
            bool want = Forced || (StartHour >= 0 && hour >= StartHour && hour < EndHour);
            if (want != Raining) SetRain(want);
            if (!Raining || !_go) return;
            var cam = Camera.main;
            if (!cam) return;
            _go.transform.position = cam.transform.position + Vector3.up * 7f;
            if (Time.unscaledTime >= _nextIndoorCheck)
            {
                _nextIndoorCheck = Time.unscaledTime + 0.25f;
                var player = _hooks.Entities.GetPlayerEntity();
                bool inZone = player && _hooks.Zones.IsPointInSubZone(player.transform.position, out var sub) && sub && !sub.SubZoneType.ToString().EndsWith("yard", StringComparison.OrdinalIgnoreCase);
                // the zones only cover the neighbours' houses (not the player's own), so also look for a ceiling/roof
                // straight above the camera
                _indoors = inZone || Physics.Raycast(cam.transform.position + Vector3.up * 0.2f, Vector3.up, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            bool indoors = _indoors;
            var em = _ps.emission;
            em.enabled = !indoors;
            if (_audio) _audio.volume = Mathf.MoveTowards(_audio.volume, indoors ? 0.12f : 0.45f, Time.deltaTime);
        }

        private static void SetRain(bool on)
        {
            Raining = on;
            if (!on) { if (_go) UnityEngine.Object.Destroy(_go); _go = null; SetFog(false); ExpansionPlugin.Log.LogInfo("[rain] stops"); return; }
            Build();
            ExpansionPlugin.Log.LogInfo($"[rain] starts (noise x{NoiseFactor}); particles={(_ps ? "on" : "off")} audio={(_clip ? _clip.length.ToString("0.0") + "s" : "none")}");
            MissionRunner.Notify("Rain", "It's raining: your noise carries less while it lasts.");
        }

        private static void Build()
        {
            if (_go) UnityEngine.Object.Destroy(_go);
            _go = new GameObject("MSN2Expansion.Rain");
            UnityEngine.Object.DontDestroyOnLoad(_go);
            _ps = _go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _ps.main;
            main.loop = true; main.startLifetime = 1.1f; main.startSpeed = 0f; main.maxParticles = 2500;
            main.startSize3D = true; main.startSizeX = 0.007f; main.startSizeY = 0.3f; main.startSizeZ = 0.007f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // additive shader: colour = brightness added, so keep it dim
            main.startColor = new Color(0.42f, 0.45f, 0.5f, 1f);
            var vel = _ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f); vel.y = new ParticleSystem.MinMaxCurve(-13f, -11f); vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var em = _ps.emission; em.rateOverTime = 1400f;
            var shape = _ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(20f, 0.5f, 20f);
            // drops die on the first roof, ceiling, awning or floor they hit, so none fall through into rooms
            var col = _ps.collision;
            col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Medium; col.lifetimeLoss = 1f; col.bounce = 0f; col.dampen = 1f;
            col.collidesWith = Physics.DefaultRaycastLayers; col.enableDynamicColliders = false;
            var r = _go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 4f; r.velocityScale = 0.02f;
            r.maxParticleSize = 0.02f;    // drops passing next to the lens would otherwise fill the screen as thick bars
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            // a clone of one of the game's own particle materials (its shader is certainly in the build), prefering
            // water/shower ones; texture and tint are reset to white so startColor alone colours the drops (the first
            // particle material found was red and drew red streaks)
            var mats = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>().Select(x => x ? x.sharedMaterial : null)
                .Where(m => m && m.shader && m.shader.name.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var src = mats.FirstOrDefault(m => m.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 || m.name.IndexOf("shower", StringComparison.OrdinalIgnoreCase) >= 0) ?? mats.FirstOrDefault();
            if (_mat) r.sharedMaterial = _mat;   // built once per session; a new clone per rain spell would leak
            else if (src)
            {
                var mat = _mat = new Material(src) { name = "MSN2Expansion.RainDrop" };
                UnityEngine.Object.DontDestroyOnLoad(mat);
                foreach (var p in new[] { "_BaseMap", "_MainTex" }) if (mat.HasProperty(p)) mat.SetTexture(p, Texture2D.whiteTexture);
                foreach (var p in new[] { "_BaseColor", "_Color", "_TintColor" }) if (mat.HasProperty(p)) mat.SetColor(p, Color.white);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
                r.sharedMaterial = mat;
                ExpansionPlugin.Log.LogInfo($"[rain] drop material from {src.name} ({src.shader.name})");
            }
            SetFog(true);
            _ps.Play();
            _audio = _go.AddComponent<AudioSource>();
            _audio.loop = true; _audio.spatialBlend = 0f; _audio.volume = 0.3f;
            // play through the game's own mixer (ambience/SFX group) so its volume sliders and pause muting apply
            var groups = Resources.FindObjectsOfTypeAll<AudioSource>().Select(s => s ? s.outputAudioMixerGroup : null).Where(g => g).Distinct().ToList();
            var grp = groups.FirstOrDefault(g => g.name.IndexOf("ambi", StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? groups.FirstOrDefault(g => g.name.IndexOf("sfx", StringComparison.OrdinalIgnoreCase) >= 0 || g.name.IndexOf("sound", StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? groups.FirstOrDefault(g => g.name.IndexOf("music", StringComparison.OrdinalIgnoreCase) < 0 && g.name.IndexOf("voice", StringComparison.OrdinalIgnoreCase) < 0);
            if (grp) _audio.outputAudioMixerGroup = grp;
            ExpansionPlugin.Log.LogInfo($"[rain] audio group {(grp ? grp.name : "none")} (groups: {string.Join(", ", groups.Select(g => g.name))})");
            _audio.clip = _clip ? _clip : (_clip = LoadWav(Path.Combine(Path.GetDirectoryName(typeof(Rain).Assembly.Location), "data", "rain_medium.wav")));
            if (_audio.clip) _audio.Play();
        }

        // grey haze while it rains; the previous fog settings come back when it stops (no effect if the build stripped
        // the fog shader variants)
        private static bool _fogSaved, _fogWas;
        private static Color _fogColor;
        private static FogMode _fogMode;
        private static float _fogDensity;

        private static void SetFog(bool on)
        {
            if (on && !_fogSaved)
            {
                _fogSaved = true; _fogWas = RenderSettings.fog; _fogColor = RenderSettings.fogColor; _fogMode = RenderSettings.fogMode; _fogDensity = RenderSettings.fogDensity;
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.012f; RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.62f);
            }
            else if (!on && _fogSaved)
            {
                _fogSaved = false;
                RenderSettings.fog = _fogWas; RenderSettings.fogColor = _fogColor; RenderSettings.fogMode = _fogMode; RenderSettings.fogDensity = _fogDensity;
            }
        }

        // 16-bit PCM WAV -> AudioClip (data/rain_*.wav written by tools/charport/extract_audio.py)
        public static AudioClip LoadWav(string path)
        {
            try
            {
                if (!File.Exists(path)) { ExpansionPlugin.Log.LogWarning($"[rain] {path} missing; rain is silent"); return null; }
                var b = File.ReadAllBytes(path);
                int ch = BitConverter.ToInt16(b, 22), rate = BitConverter.ToInt32(b, 24), bits = BitConverter.ToInt16(b, 34);
                int pos = 12;
                while (pos + 8 <= b.Length && System.Text.Encoding.ASCII.GetString(b, pos, 4) != "data") pos += 8 + BitConverter.ToInt32(b, pos + 4);
                if (bits != 16 || pos + 8 > b.Length) throw new InvalidDataException("not 16-bit PCM");
                int len = BitConverter.ToInt32(b, pos + 4) / 2;
                var s = new float[len];
                for (int i = 0; i < len; i++) s[i] = BitConverter.ToInt16(b, pos + 8 + i * 2) / 32768f;
                var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), len / ch, ch, rate, false);
                clip.SetData(s, 0);
                return clip;
            }
            catch (Exception e) { ExpansionPlugin.Log.LogWarning($"[rain] {path}: {e.Message}"); return null; }
        }
    }
}
