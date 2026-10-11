using System;
using System.Globalization;
using System.Linq;
using Game.Input;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MSN2CommunityPatch
{
    // World/test helpers for the agent bridge: player position, teleport, quality inspection.
    public static class AgentWorld
    {
        private static readonly AccessTools.FieldRef<FirstPersonController, float> Pitch =
            AccessTools.FieldRefAccess<FirstPersonController, float>("_cinemachineTargetPitch");

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        public static void Register()
        {
            AgentBridge.Register("pos", (id, a) =>
            {
                var p = Player();
                if (!p) return AgentBridge.Done(id, new { ok = false, error = "no player" });
                var t = p.transform;
                return AgentBridge.Done(id, new { ok = true, x = t.position.x, y = t.position.y, z = t.position.z, yaw = t.eulerAngles.y, pitch = Pitch(p) });
            });
            AgentBridge.Register("tp", (id, a) =>
            {
                var p = Player();
                if (!p || a.Length < 3) return AgentBridge.Done(id, new { ok = false, error = "usage: tp x y z [yaw] [pitch]" });
                Teleport(p, new Vector3(F(a[0]), F(a[1]), F(a[2])), a.Length > 3 ? F(a[3]) : (float?)null, a.Length > 4 ? F(a[4]) : (float?)null);
                return AgentBridge.Done(id, new { ok = true });
            });
            AgentBridge.Register("tpto", (id, a) =>
            {
                var p = Player();
                var q = string.Join(" ", a);
                var target = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(t => t.name.Equals(q, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (!p || !target) return AgentBridge.Done(id, new { ok = false, error = "player or target missing: " + q });
                Teleport(p, target.position, target.eulerAngles.y, 0);
                return AgentBridge.Done(id, new { ok = true, to = SceneStats.Path(target), target.position.x, target.position.y, target.position.z });
            });
            AgentBridge.Register("qualities", (id, a) => AgentBridge.Done(id, Enumerable.Range(0, QualitySettings.count).Select(i =>
            {
                var u = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                return new
                {
                    level = i,
                    asset = u ? u.name : "null(default)",
                    renderScale = u ? u.renderScale : 0, msaa = u ? u.msaaSampleCount : 0, hdr = u && u.supportsHDR,
                    shadowDistance = u ? u.shadowDistance : 0, cascades = u ? u.shadowCascadeCount : 0,
                    mainShadowRes = u ? u.mainLightShadowmapResolution : 0, addShadows = u && u.supportsAdditionalLightShadows,
                    addShadowRes = u ? u.additionalLightsShadowmapResolution : 0, soft = u && u.supportsSoftShadows,
                    opaqueTex = u && u.supportsCameraOpaqueTexture, srpBatcher = u && u.useSRPBatcher,
                    upscaling = u ? u.upscalingFilter.ToString() : "", maxAddLights = u ? u.maxAdditionalLightsCount : 0,
                };
            }).ToArray()));
            AgentBridge.Register("setq", (id, a) =>
            {
                QualitySettings.SetQualityLevel((int)F(a[0]), true);
                return AgentBridge.Done(id, new { ok = true, level = QualitySettings.GetQualityLevel() });
            });
        }

        public static FirstPersonController Player() => UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();

        public static void Teleport(FirstPersonController p, Vector3 pos, float? yaw, float? pitch)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            p.transform.position = pos;
            if (yaw.HasValue) p.transform.rotation = Quaternion.Euler(0, yaw.Value, 0);
            if (pitch.HasValue)
            {
                Pitch(p) = pitch.Value;
                if (p.CinemachineCameraTarget) p.CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(pitch.Value, 0, 0);
            }
            if (cc) cc.enabled = true;
        }
    }
}
