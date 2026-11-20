using System.Linq;
using HarmonyLib;
using UnityEngine;
using _Game.Systems.CameraController;

namespace MSN2Expansion.Systems
{
    // Telescope (MSN1 TelescopeView): a telescope stands at the window of the player's room. Standing next to it, the
    // phone camera's zoom goes 5x further (MSN2 lets the phone zoom from FOV 70 to 20; here down to 4), so photos of
    // the neighbours' windows go through the normal phone-camera path and count for challenges and jobs.
    public static class Telescope
    {
        public static Vector3 Position = new Vector3(5.45f, 1.02f, 18.6f);   // measured spot at the player's window (TEST_RESULTS 0.5.0)
        public static float Yaw = 168f;
        public const float NormalZoomMax = 20f, TelescopeZoomMax = 4f, Reach = 1.6f;
        private static GameObject _root;
        public static bool Active;

        public static void Build()
        {
            if (_root) Object.Destroy(_root);
            if (Active)
            {
                // rebuilding while the player stands at the telescope: give the phone its normal zoom back first, or
                // Tick never sees the change and the 4x zoom limit sticks after walking away
                var cam = Object.FindFirstObjectByType<PhoneCameraController>();
                if (cam) Traverse.Create(cam).Field("ZoomMax").SetValue(NormalZoomMax);
            }
            Active = false;
            if (!SystemsConfig.Telescope.Value) return;
            var lit = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m && m.shader && m.shader.name == "Universal Render Pipeline/Lit");
            Material dark = null, brass = null;
            if (lit)
            {
                dark = new Material(lit) { name = "Telescope_Dark" };
                if (dark.HasProperty("_BaseMap")) dark.SetTexture("_BaseMap", null);
                if (dark.HasProperty("_BaseColor")) dark.SetColor("_BaseColor", new Color(0.08f, 0.08f, 0.1f));
                if (dark.HasProperty("_Smoothness")) dark.SetFloat("_Smoothness", 0.6f);
                brass = new Material(dark) { name = "Telescope_Brass" };
                if (brass.HasProperty("_BaseColor")) brass.SetColor("_BaseColor", new Color(0.7f, 0.52f, 0.2f));
                if (brass.HasProperty("_Metallic")) brass.SetFloat("_Metallic", 0.8f);
            }
            _root = new GameObject("MSN2Expansion.Telescope");
            _root.transform.SetPositionAndRotation(Position, Quaternion.Euler(0, Yaw, 0));
            // tripod: three legs from the head down to the floor
            for (int i = 0; i < 3; i++)
            {
                var leg = Part(PrimitiveType.Cylinder, dark, new Vector3(0.025f, 0.6f, 0.025f));
                var dir = Quaternion.Euler(0, i * 120f, 0) * Vector3.forward;
                leg.transform.localPosition = new Vector3(0, 0.58f, 0) + dir * 0.17f;
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 1.15f - dir * 0.34f).normalized);
            }
            var head = Part(PrimitiveType.Sphere, brass, Vector3.one * 0.09f);
            head.transform.localPosition = new Vector3(0, 1.17f, 0);
            var tube = Part(PrimitiveType.Cylinder, dark, new Vector3(0.09f, 0.42f, 0.09f));
            tube.transform.localPosition = new Vector3(0, 1.26f, 0.12f);
            tube.transform.localRotation = Quaternion.Euler(75f, 0, 0);
            var lens = Part(PrimitiveType.Cylinder, brass, new Vector3(0.11f, 0.03f, 0.11f));
            lens.transform.localPosition = new Vector3(0, 1.37f, 0.52f);
            lens.transform.localRotation = Quaternion.Euler(75f, 0, 0);
            ExpansionPlugin.Log.LogInfo($"[telescope] placed at {Position} (material {(lit ? "URP Lit clone" : "none")})");
        }

        private static GameObject Part(PrimitiveType t, Material m, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());   // decoration only: never blocks the player or NPC pathing
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = scale;
            if (m) go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        public static void Tick()
        {
            if (!_root) return;
            var player = Object.FindFirstObjectByType<Game.Entities.PlayerEntity>();
            bool near = player && Vector3.Distance(player.transform.position, Position) < Reach;
            if (near == Active) return;
            Active = near;
            var cam = Object.FindFirstObjectByType<PhoneCameraController>();
            if (cam) Traverse.Create(cam).Field("ZoomMax").SetValue(near ? TelescopeZoomMax : NormalZoomMax);
            if (near) MissionRunner.Notify("Telescope", "Open the phone camera and scroll to zoom all the way in.");
        }
    }
}
